using CDL2v1;

using System;
using System.IO;
using System.CommandLine;
using System.CommandLine.Invocation;

using static CDL2v1.Logger;
using System.Diagnostics;

internal class CDL2 {
   public static string Version = "1.0.0";

   public static CDL2 Compiler;

   public int VerbosityLevel { get; set; }
   public bool LineNumbers { get; set; }
   public string Target { get; set; } = "";
   public int DebugVerbosityLevel { get; internal set; }
   public bool ParseOnly { get; internal set; }
   public string? PrettyPrint { get; internal set; } = null;
   public string? ProgramName { get; set; }

   static CDL2() {
      Compiler = new CDL2();
   }

   private static void Main(string[] args) {
      Console.WriteLine($"CDL2 Compiler v{Version}");

      // Define the root command with options
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
                description: "Make program the one for which code is generated. The default is the first or only program that has beean read."),
            new Option<bool>(
                "--line-numbers",
                getDefaultValue: () => false,
                description: "Add line numbers to the token stream."),
            new Option<bool>(
                "--parse-only",
                getDefaultValue: () => false,
                description: "Do not generate code. Verifies whether the source is valid."),
            new Option<string?>(
                "--pretty-print",
                getDefaultValue: () => "",
                description: "Pretty print the parsed code. If a value is given, it is assumed to be a filename, Otherwise output goes to the Debugger."){Arity = ArgumentArity.ZeroOrOne},
      };

      rootCommand.Description = "CDL2 Compiler";

      // Set the handler for the root command
      rootCommand.SetHandler((string[] sources,int verbosity,int debugVerbosity,string target,string programName,bool lineNumbers,bool parseOnly,string? prettyPrint) => {
         Compiler.VerbosityLevel = verbosity;
         Compiler.DebugVerbosityLevel = debugVerbosity;
         Compiler.LineNumbers = lineNumbers;
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

   public Parser? Parser;
   public SemanticAnalyzer? semanticAnalyzer;
   public CodeGenerator? codeGenerator;

   public void CompileSources(string[] args) {
      Debug.WriteLine( $"Options: --sources {args} --verbose {VerbosityLevel} --debug-log {DebugVerbosityLevel} "+
                                 "--target {Target} --program {ProgramName} --line-numbers {LineNumbers} --parse-only {ParseOnly} --pretty-print {PrettyPrint}");
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
         if (ProgramName == "" && Program.FirstProgram != null) {
            MainProgram = Program.FirstProgram;
         } else if (ProgramName != null && ProgramName != "") {
            MainProgram = Program.FindProgramByName(ProgramName);
            if (MainProgram is null) {
               if (Program.FirstProgram != null) {
                  MainProgram = Program.FirstProgram;
                  ReportError($"Program {ProgramName} not found, using {MainProgram.id} instead.");
               } else {
                  ReportError("No program found");
               }
            }
         }
         if (MainProgram == null) return;

         // Perform semantic checks
         semanticAnalyzer = new SemanticAnalyzer();
         if (Program.Programs.Count >= 1) {
            // TODO: If errors are found, null out the program object.
            semanticAnalyzer.Analyze(MainProgram);
         }

         if (PrettyPrint != "" && (Program.Programs.Count > 0 || Program.Modules.Count > 0)) {
            EmitterBase emitter;
            if (PrettyPrint == null) {
               emitter = new EmitterDebug();
            } else if (PrettyPrint == "w" || PrettyPrint == "Window") {
               emitter = new EmitterWindow();
            } else if (PrettyPrint.IsValidFileName()) {  // Must be placed after check for window
               emitter = new EmitterFile(PrettyPrint);
            } else {
               emitter = new EmitterDebug();
            }
            new PrettyPrinter(emitter).Print(Program.Programs,Program.Modules);
            emitter.Close();
         }

         if (!ParseOnly) {
            ICodeGenerator? cg = CreateCodeGenerator(Target);
            /// TODO: Add a command line option to specify the CG output file (or default it with the appropriate extension <see cref="ICodeGenerator.FileExtension"/>
            EmitterBase emitter = new EmitterDebug() { IgnoreLineLength = true };
            Debug.Assert(MainProgram != null);
            if (cg != null) {
               string targetFileName = Path.ChangeExtension(args[0],cg.FileExtension);
               Log(0,$"Generating code for {Target} into {emitter.Target}");
               codeGenerator = new CodeGenerator(cg);
               codeGenerator.GenerateCode(MainProgram,emitter);
            } else {
               Console.WriteLine("No program found in the source files");
            }
         }
      }
   }

   private static ICodeGenerator? CreateCodeGenerator(string target) {
      try {
         string className = $"CDL2v1.CodeGenerator{target}";
         Type? type = Type.GetType(className);
         if (type != null && typeof(ICodeGenerator).IsAssignableFrom(type)) {
            return Activator.CreateInstance(type) as ICodeGenerator;
         }
      } catch (Exception ex) {
         Console.WriteLine($"Error creating code generator for target {target}: {ex.Message}");
      }
      return null;
   }

   /// <summary>
   /// Called by <see cref="ReportError"/> to skip to the next END token."/>
   /// </summary>
   internal void SkipToNextEnd() => Parser?.SkipToNextEnd();
}
