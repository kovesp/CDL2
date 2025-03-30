using System;
using System.IO;
using System.CommandLine;
using System.CommandLine.Invocation;

using static CDL2v1.Logger;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.ComponentModel;

namespace CDL2v1 {
   public abstract class CompilationPhase {
      private readonly CDL2 compiler;

      public CompilationPhase(CDL2 compiler) {
         this.compiler = compiler;
         PhaseName = GetType().Name;
      }

      public string PhaseName { get; }
      public int Errors { get; protected set; } = 0;
      public int Warnings { get; protected set; } = 0;
      void ResetNotes() => Errors = Warnings = 0;
      /// <summary>
      /// Add a note to given subject. Increment counters.
      /// </summary>
      /// <param name="subject"></param>
      /// <param name="note"></param>
      /// <param name="insertions"></param>
      protected void AddNote(NamedElement subject, Note note, params object[] insertions) {
         subject.AddNote(PhaseName, note, insertions);
         if (note.Type == NoteType.Warning) Warnings++;
         if (note.Type == NoteType.Error) Errors++;
      }
      /// <summary>
      /// Report errors and warnings for a phas.
      /// List them if any, and return true.
      /// Otherwisw return false.
      /// </summary>
      /// <returns></returns>
      public bool AbortCompilation() {
         
         bool stop = Errors > 0 && !compiler.AllowErrors;
         string? message = null;
         if (stop) {
            message = $"{PhaseName}: Compilation aborted due to errors";
         } else {
            stop = Warnings > 0 && compiler.StopOnWarnings;
            if (stop) message = $"{PhaseName}: Compilation aborted due to warnings";
         }

         if (stop) {
            ReportNoteCounts(message);
            return true;
         }
         else {
            return false;
         }
      }
      /// <summary>
      /// Report the number of errors and warnings for the phase.
      /// Rename for readability in some contexts.
      /// </summary>
      /// <param name="message">Optional termination message.</param>
      /// <returns></returns>
      public void ReportNoteCounts(string? message = null) {
         Log(0, $"{PhaseName,-16}: {Errors.Plural("error")}, {Warnings.Plural("warning")}");
         if (message != null) Log(0, message);
         foreach (NoteType type in new List<NoteType>() { NoteType.Error, NoteType.Warning }) {
            foreach (NamedElement element in Database.Instance.ElementsWithNotes) {
               foreach (Note note in element.Notes.Where(note => note.Type == type && note.PhaseName == PhaseName)) {
                  string head = $"{PhaseName,-16}: {note.Type,-7} {note.Number:D3}";
                  Log(0, $"{head} {element.FQDN()}\n {new string(' ', head.Length)}{note.Text}");
               }
            }
         }
      }
   }


   [Serializable]
   public class CDL2 {
      public static string Version = "1.0.0";

      public static CDL2 Compiler;

      public int VerbosityLevel { get; set; }
      public bool SaveDB { get; set; }
      public string Target { get; set; } = "";
      public int DebugVerbosityLevel { get; internal set; }
      public bool ParseOnly { get; internal set; }
      public string? PrettyPrint { get; internal set; } = null;
      public string? ProgramName { get; set; }
      public bool AllowErrors { get; set; }
      public bool StopOnWarnings { get; set; }

      static CDL2() {
         Compiler = new CDL2();
      }

      // Configuration class to hold all options
      private class CompilerOptions {
         public string[] Sources { get; set; } = Array.Empty<string>();
         public int VerbosityLevel { get; set; } = -1;
         public int DebugVerbosityLevel { get; set; } = -1;
         public string Target { get; set; } = "PowerShell";
         public string ProgramName { get; set; } = "";
         public bool SaveDB { get; set; } = false;
         public bool ParseOnly { get; set; } = false;
         public bool StopOnWarnings { get; set; } = false;
         public bool AllowErrors { get; set; } = false;
         public string? PrettyPrint { get; set; } = "";
         public bool GenerateDebugInfo { get; set; } = false;
         public string? OutputDirectory { get; set; } = null;
         // Add any additional options here
      }

      private static void Main(string[] args) {
         Log(0, $"CDL2 Compiler v{Version}");

         // Define the root command with options
         var rootCommand = new RootCommand {
               new Option<string[]>("--sources",                              "The source files to compile"),
               new Option<int>     (["-v", "--verbose"],  () => -1,           "Set the verbosity level (0-3)"),
               new Option<int>     (["-d", "--debug-log"],() => -1,           "Set the debug verbosity level (0-3)"),
               new Option<string>  (["-t","--target"],    () => "PowerShell", "Generate code for the specified target language. Default is PowerShell."),
               new Option<string>  (["-p","--program"],   () => "",           "Make program the one for which code is generated. The default is the first or only program that has been read."),
               new Option<bool>    ("--save",             () => false,        "Save the parsed code to a file using JSON"),
               new Option<bool>    ("--parse-only",       () => false,        "Do not generate code. Verifies whether the source is valid."),
               new Option<bool>    ("--stop-on-warnings", () => false,        "Stop processing if any warnings are generated."),
               new Option<bool>    ("--allow-errors",     () => false,        "Continue even if there are errors. Mainly for debugging the compiler."),
               new Option<string?> ("--pretty-print",     () => "",           "Pretty print the parsed code. If a value is given, it is assumed to be a file-name, Otherwise output goes to the Debugger.") { Arity = ArgumentArity.ZeroOrOne },
               new Option<bool>    ("--gen-debug-info",   () => false,        "Generate debug information"),
               new Option<string?> ("--output-dir",       () => null,         "Specify output directory for generated code")
           };

         rootCommand.Description = "CDL2 Compiler";

         // Set the handler using a configuration object
         rootCommand.SetHandler((context) => {
            var options = new CompilerOptions {
               Sources = context.ParseResult.GetValueForOption<string[]>((Option<string[]>)rootCommand.Options[0])!,
               VerbosityLevel = context.ParseResult.GetValueForOption<int>((Option<int>)rootCommand.Options[1]),
               DebugVerbosityLevel = context.ParseResult.GetValueForOption<int>((Option<int>)rootCommand.Options[2]),
               Target = context.ParseResult.GetValueForOption<string>((Option<string>)rootCommand.Options[3])!,
               ProgramName = context.ParseResult.GetValueForOption<string>((Option<string>)rootCommand.Options[4])!,
               SaveDB = context.ParseResult.GetValueForOption<bool>((Option<bool>)rootCommand.Options[5]),
               ParseOnly = context.ParseResult.GetValueForOption<bool>((Option<bool>)rootCommand.Options[6]),
               StopOnWarnings = context.ParseResult.GetValueForOption<bool>((Option<bool>)rootCommand.Options[7]),
               AllowErrors = context.ParseResult.GetValueForOption<bool>((Option<bool>)rootCommand.Options[8]),
               PrettyPrint = context.ParseResult.GetValueForOption<string?>((Option<string?>)rootCommand.Options[9]),
               GenerateDebugInfo = context.ParseResult.GetValueForOption<bool>((Option<bool>)rootCommand.Options[10]),
               OutputDirectory = context.ParseResult.GetValueForOption<string?>((Option<string?>)rootCommand.Options[11])
            };

            ProcessOptions(options);
         });

         // Invoke the command handler
         rootCommand.Invoke(args);
      }

      private static void ProcessOptions(CompilerOptions options) {
         Compiler.VerbosityLevel = options.VerbosityLevel;
         Compiler.DebugVerbosityLevel = options.DebugVerbosityLevel;
         Compiler.SaveDB = options.SaveDB;
         Compiler.Target = options.Target;
         Compiler.ProgramName = options.ProgramName;
         Compiler.ParseOnly = options.ParseOnly;
         Compiler.StopOnWarnings = options.StopOnWarnings;
         Compiler.AllowErrors = options.AllowErrors;
         Compiler.PrettyPrint = options.PrettyPrint;
         // Process any additional options

         Compiler.CompileSources(options.Sources);
      }


      private string BoolOption(bool option, string name) => option ? name + " " : "";
      private string IntOption(int option, string name) => option > 0 ? $"{name} {option} " : "";
      private string StringOption(string? option, string name) => option == null || option.IsWhiteSpace() ? "" : $"{name} {option} ";

      public Parser? Parser;
      public SemanticAnalyzer? SemanticAnalyzer;
      public CodeGenerator? codeGenerator;

      public void CompileSources(string[] args) {
         Log(0, $"Options: --sources {string.Join(',', args)} {IntOption(VerbosityLevel, "--verbose")}{IntOption(DebugVerbosityLevel, "--debug-log")}" +
                                    $"{StringOption(Target, "--target")}{StringOption(ProgramName, "--program")}{BoolOption(SaveDB, "--save")}" +
                                    $"{BoolOption(ParseOnly, "--parse-only")}{BoolOption(StopOnWarnings, "--stop-on-warnings")}{BoolOption(AllowErrors, "--allow-errors")}" +
                                    $"{StringOption(PrettyPrint, "--pretty-print")}");
         if (args.Length > 0) {
            Parser = new Parser(this);
            foreach (string arg in args) {
               string source = Path.GetFullPath(arg);
               if (File.Exists(source)) {
                  Log(0, $"Compiling {source}");
                  TokenList sourceTokens = LexicalAnalyzer.Tokenize(source);
                  // Add the tokens comprising the file to the syntax tree
                  Parser.Parse(sourceTokens);
               }
            }
            if (Parser.AbortCompilation()) return;


            Program? MainProgram = null;
            if (ProgramName == "" && Database.Instance.FirstProgram != null) {
               MainProgram = Database.Instance.FirstProgram;
            }
            else if (ProgramName != null && ProgramName != "") {
               MainProgram = Database.Instance.FindProgramByName(ProgramName);
               if (MainProgram is null) {
                  if (Database.Instance.FirstProgram != null) {
                     MainProgram = Database.Instance.FirstProgram;
                     ReportError($"Program {ProgramName} not found, using {MainProgram.id} instead.");
                  }
                  else {
                     ReportError("No program found");
                  }
               }
            }
            if (MainProgram != null) {
               if (DebugVerbosityLevel >= 4) ID.Dump();

               // Perform semantic checks
               SemanticAnalyzer = new SemanticAnalyzer(this);
               if (Database.Instance.Programs.Count >= 1) {
                  // TODO: If errors are found, null out the program object.
                  SemanticAnalyzer.Analyze(MainProgram);
               }

               if (SemanticAnalyzer.AbortCompilation()) return;

               if (SaveDB) Database.Save("CDL2v1");

               if (PrettyPrint != "" && (Database.Instance.Programs.Count > 0 || Database.Instance.Modules.Count > 0)) {
                  EmitterBase emitter;
                  if (PrettyPrint == null) {
                     emitter = new EmitterDebug();
                  }
                  else if (Regex.IsMatch(PrettyPrint, @"^w(?:indow)$", RegexOptions.IgnoreCase)) {
                     emitter = new EmitterWindow();
                  }
                  else if (PrettyPrint.IsValidFileName()) {  // Must be placed after check for window
                     emitter = new EmitterFile(PrettyPrint);
                  }
                  else {
                     emitter = new EmitterDebug();
                  }
                  new PrettyPrinter(emitter).Print(Database.Instance.Programs, Database.Instance.Modules);
                  emitter.Close();
               }

               if (!ParseOnly) {
                  ICodeGenerator? cg = CreateCodeGenerator(Target);
                  /// TODO: Add a command line option to specify the CG output file (or default it with the appropriate extension <see cref="ICodeGenerator.FileExtension"/>

                  Debug.Assert(MainProgram != null);

                  if (cg != null) {
                     string targetFileName = Path.ChangeExtension(args[0], cg.FileExtension);
                     EmitterBase emitter = new EmitterFile(targetFileName) { IgnoreLineLength = true };
                     Log(0, $"Generating code for {Target} into {emitter.Target}");
                     codeGenerator = new CodeGenerator(cg);
                     codeGenerator.GenerateCode(MainProgram, emitter);
                     emitter.Close();
                  }
                  else {
                     ReportError("No target code generator");
                  }
               }
               Parser.ReportNoteCounts();
               Log(0,"");
               SemanticAnalyzer.ReportNoteCounts();
            }
         }
      }



      private static ICodeGenerator? CreateCodeGenerator(string target, string dataType = "Int64") {
         try {
            string className = $"CDL2v1.CodeGenerator{target}";
            Type? type = Type.GetType(className);
            if (type != null && typeof(ICodeGenerator).IsAssignableFrom(type)) {
               return Activator.CreateInstance(type, dataType) as ICodeGenerator;
            }
         }
         catch (Exception ex) {
            ReportError($"Error creating code generator for target {target} with Data type {dataType}: {ex.Message}");
         }
         return null;
      }

      /// <summary>
      /// Called by <see cref="ReportError"/> to skip to the next END token."/>
      /// </summary>
      internal void SkipToNextEnd() => Parser?.SkipToNextEnd();
   }
}
