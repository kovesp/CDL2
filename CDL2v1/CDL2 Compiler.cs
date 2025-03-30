using System;
using System.IO;
using System.CommandLine;
using System.CommandLine.Invocation;

using static CDL2v1.Logger;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Reflection.Metadata.Ecma335;
using static CDL2v1.TokenList;

namespace CDL2v1 {
   /// <summary>
   /// Base class for compiler phases Parser and SemanticAnalyzer.
   /// Used to keep track of errors and warnings.
   /// </summary>
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
      /// Ensure that the subject is also maintained in <see cref="Database.ElementsWithNotes"/>.
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
         bool stop = Errors > 0 && !Settings.SettingValue<bool>("AllowErrors");
         string? message = null;
         if (stop) {
            message = $"{PhaseName}: Compilation aborted due to errors";
         } else {
            stop = Warnings > 0 && Settings.SettingValue<bool>("StopOnWarnings");
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
      /// Renamed for readability in some contexts.
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

   /// <summary>
   /// The CDL2 compiler. A singleton.
   /// Processes command line options and compiles the source files.
   /// </summary>
   [Serializable]
   public partial class CDL2 {
      public static string Version = "1.0.0";

      static CDL2() => Compiler = new CDL2();
      private CDL2() { }
      public static readonly CDL2 Compiler;

      private static void Main(string[] args) {
         Log(0, $"CDL2 Compiler v{Version}");

         Settings.ProcessCommandLine(args);

         Compiler.CompileSources(Settings.SettingValue<string[]>("Sources")!);
      }

      public Parser? Parser;
      public SemanticAnalyzer? SemanticAnalyzer;
      public CodeGenerator? codeGenerator;

      public void CompileSources(string[] args) {
         Log(0, $"Options: --sources {string.Join(',', args)} {Settings.IntOption("VerbosityLevel")}{Settings.IntOption("DebugVerbosityLevel")}" +
                                    $"{Settings.StringOption("Target")}{Settings.StringOption("ProgramName")}{Settings.BoolOption("SaveDB")}" +
                                    $"{Settings.BoolOption("ParseOnly")}{Settings.BoolOption("StopOnWarnings")}{Settings.BoolOption("AllowErrors")}" +
                                    $"{Settings.StringOption("PrettyPrint")}");
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
            string? ProgramName = Settings.SettingValue<string>("ProgramName");
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
               if (Settings.SettingValue<int>("DebugVerbosityLevel") >= 4) ID.Dump();

               // Perform semantic checks
               SemanticAnalyzer = new SemanticAnalyzer(this);
               if (Database.Instance.Programs.Count >= 1) {
                  // TODO: If errors are found, null out the program object.
                  SemanticAnalyzer.Analyze(MainProgram);
               }

               if (SemanticAnalyzer.AbortCompilation()) return;

               if (Settings.SettingValue<bool>("SaveDB")) Database.Save("CDL2v1");

               string? PrettyPrint = Settings.SettingValue<string>("PrettyPrint");
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

               if (!Settings.SettingValue<bool>("ParseOnly")) {
                  ICodeGenerator ? cg = CreateCodeGenerator(Settings.SettingValue<string>("Target")!);
                  /// TODO: Add a command line option to specify the CG output file (or default it with the appropriate extension <see cref="ICodeGenerator.FileExtension"/>

                  Debug.Assert(MainProgram != null);

                  if (cg != null) {
                     string targetFileName = Path.ChangeExtension(args[0], cg.FileExtension);
                     EmitterBase emitter = new EmitterFile(targetFileName) { IgnoreLineLength = true };
                     Log(0, $"Generating code for {Settings.SettingValue<string>("Target")!} into {emitter.Target}");
                     codeGenerator = new CodeGenerator(cg);
                     codeGenerator.GenerateCode(MainProgram, emitter);
                     emitter.Close();
                  }
                  else {
                     ReportError("No target code generator");
                  }               }
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
