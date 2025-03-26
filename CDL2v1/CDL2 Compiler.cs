using CDL2v1;

using System;
using System.IO;
using System.CommandLine;
using System.CommandLine.Invocation;

using static CDL2v1.Logger;
using System.Diagnostics;
using System.Text.RegularExpressions;

[Serializable]
internal class CDL2 {
   public static string Version = "1.0.0";

   public static CDL2 Compiler;

   public int VerbosityLevel { get; set; }
   public bool SaveDB { get; set; }
   public string Target { get; set; } = "";
   public int DebugVerbosityLevel { get; internal set; }
   public bool ParseOnly { get; internal set; }
   public string? PrettyPrint { get; internal set; } = null;
   public string? ProgramName { get; set; }

   static CDL2() {
      Compiler = new CDL2();
   }

   private static void Main(string[] args) {
      Log(0,$"CDL2 Compiler v{Version}");

      // Define the root command with serializationOptions
      var rootCommand = new RootCommand {
            new Option<string[]>(
                "--sources",
                description: "The source files to compile"),
            new Option<int>(
                ["-v", "--verbose"],
                getDefaultValue: () => -1,   // Means off
                description: "Set the verbosity level (0-3)"),
            new Option<int>(
                ["-d", "--debug-log"],
                getDefaultValue: () => -1,   // Means off
                description: "Set the debug verbosity level (0-3)"),
            new Option<string>(
                ["-t","--target"],
                getDefaultValue: () => "PowerShell",
                description: "Generate code for the specified target language. Default is PowerShell."),
            new Option<string>(
                ["-p","--program"],
                getDefaultValue: () => "",
                description: "Make program the one for which code is generated. The default is the first or only program that has been read."),
            new Option<bool>(
                "--save",
                getDefaultValue: () => false,
                description: "Save the parsed code to a file using JSON"),
            new Option<bool>(
                "--parse-only",
                getDefaultValue: () => false,
                description: "Do not generate code. Verifies whether the source is valid."),
            new Option<string?>(
                "--pretty-print",
                getDefaultValue: () => "",
                description: "Pretty print the parsed code. If a value is given, it is assumed to be a file-name, Otherwise output goes to the Debugger."){Arity = ArgumentArity.ZeroOrOne},
      };

      rootCommand.Description = "CDL2 Compiler";

      // Set the handler for the root command
      rootCommand.SetHandler((string[] sources,int verbosity,int debugVerbosity,string target,string programName,
            bool SaveDB,bool parseOnly,string? prettyPrint) => {
         Compiler.VerbosityLevel = verbosity;
         Compiler.DebugVerbosityLevel = debugVerbosity;
         Compiler.SaveDB = SaveDB;
         Compiler.Target = target;
         Compiler.ProgramName = programName;
         Compiler.ParseOnly = parseOnly;
         Compiler.PrettyPrint = prettyPrint;
         Compiler.CompileSources(sources);
      },
      (Option<string[]>)rootCommand.Options[0],
      (Option<int>)rootCommand.Options[1],
      (Option<int>)rootCommand.Options[2],
      (Option<string>)rootCommand.Options[3],
      (Option<string>)rootCommand.Options[4],
      (Option<bool>)rootCommand.Options[5],
      (Option<bool>)rootCommand.Options[6],
      (Option<string?>)rootCommand.Options[7]);

      // Invoke the command handler
      rootCommand.Invoke(args);
   }

   private string BoolOption(bool option, string name) => option ? name+" ": "";
   private string IntOption(int option, string name) => option > 0 ? $"{name} {option} " : "";
   private string StringOption(string? option, string name) => option == null || option.IsWhiteSpace() ? "" : $"{name} {option} ";

   public Parser? Parser;
   public SemanticAnalyzer? semanticAnalyzer;
   public CodeGenerator? codeGenerator;

   public void CompileSources(string[] args) {
      Log(0,$"Options: --sources {string.Join(',',args)} {IntOption(VerbosityLevel,"--verbose")}{IntOption(DebugVerbosityLevel,"--debug-log")}"+
                                 $"{StringOption(Target,"--target")}{StringOption(ProgramName,"--program")}{BoolOption(SaveDB,"--save")}{BoolOption(ParseOnly,"--parse-only")}{StringOption(PrettyPrint,"--pretty-print")}");
      if (args.Length > 0) {
         Parser = new Parser();
         foreach (string arg in args) {
            string source = Path.GetFullPath(arg);
            if (File.Exists(source)) {
               Log(0,$"Compiling {source}");
               TokenList sourceTokens = LexicalAnalyzer.Tokenize(source);
               // Add the tokens comprising the file to the syntax tree
               Parser.Parse(sourceTokens);
            }
         }

         Program? MainProgram = null;
         if (ProgramName == "" && Database.Instance.FirstProgram != null) {
            MainProgram = Database.Instance.FirstProgram;
         } else if (ProgramName != null && ProgramName != "") {
            MainProgram = Database.Instance.FindProgramByName(ProgramName);
            if (MainProgram is null) {
               if (Database.Instance.FirstProgram != null) {
                  MainProgram = Database.Instance.FirstProgram;
                  ReportError($"Program {ProgramName} not found, using {MainProgram.id} instead.");
               } else {
                  ReportError("No program found");
               }
            }
         }
         if (MainProgram == null) return;

         if (DebugVerbosityLevel >= 4) ID.Dump();

         // Perform semantic checks
         semanticAnalyzer = new SemanticAnalyzer();
         if (Database.Instance.Programs.Count >= 1) {
            // TODO: If errors are found, null out the program object.
            semanticAnalyzer.Analyze(MainProgram);
         }

         if (SaveDB) Database.Save("CDL2v1");

         if (PrettyPrint != "" && (Database.Instance.Programs.Count > 0 || Database.Instance.Modules.Count > 0)) {
            EmitterBase emitter;
            if (PrettyPrint == null) {
               emitter = new EmitterDebug();
            } else if (Regex.IsMatch(PrettyPrint,@"^w(?:indow)$",RegexOptions.IgnoreCase)) {
               emitter = new EmitterWindow();
            } else if (PrettyPrint.IsValidFileName()) {  // Must be placed after check for window
               emitter = new EmitterFile(PrettyPrint);
            } else {
               emitter = new EmitterDebug();
            }
            new PrettyPrinter(emitter).Print(Database.Instance.Programs,Database.Instance.Modules);
            emitter.Close();
         }

         if (!ParseOnly) {
            ICodeGenerator? cg = CreateCodeGenerator(Target);
            /// TODO: Add a command line option to specify the CG output file (or default it with the appropriate extension <see cref="ICodeGenerator.FileExtension"/>
            
            Debug.Assert(MainProgram != null);

            if (cg != null) {
               string targetFileName = Path.ChangeExtension(args[0], cg.FileExtension);
               EmitterBase emitter = new EmitterFile(targetFileName) { IgnoreLineLength = true };               
               Log(0,$"Generating code for {Target} into {emitter.Target}");
               codeGenerator = new CodeGenerator(cg);
               codeGenerator.GenerateCode(MainProgram,emitter);
               emitter.Close();
            } else {
               ReportError("No target code generator");
            }
         }
      }
   }

   private static ICodeGenerator? CreateCodeGenerator(string target,string dataType="Int64") {
      try {
         string className = $"CDL2v1.CodeGenerator{target}";
         Type? type = Type.GetType(className);
         if (type != null && typeof(ICodeGenerator).IsAssignableFrom(type)) {
            return Activator.CreateInstance(type,dataType) as ICodeGenerator;
         }
      } catch (Exception ex) {     
         ReportError($"Error creating code generator for target {target} with Data type {dataType}: {ex.Message}");
      }
      return null;
   }

   /// <summary>
   /// Called by <see cref="ReportError"/> to skip to the next END token."/>
   /// </summary>
   internal void SkipToNextEnd() => Parser?.SkipToNextEnd();
}
