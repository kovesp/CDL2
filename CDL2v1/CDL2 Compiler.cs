using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;

using static CDL2v1.Logger;
using static CDL2v1.TokenList;

namespace CDL2v1 {
   /// <summary>
   /// Base class for Compiler phases Parser and SemanticAnalyzer.
   /// Used to keep track of errors and warnings.
   /// </summary>
   public abstract class CompilationPhase {
      protected readonly CDL2 Compiler;

      public CompilationPhase(CDL2 compiler) {
         Compiler = compiler;
         Compiler.CompilationPhase = this;
         PhaseName = GetType().Name;
      }

      public Notes notes = [];

      public string PhaseName { get; }
      private IEnumerable<Note> Notes => (notes.Any() ? notes : Database.Instance.ElementsWithNotes.SelectMany(guid => Database.Instance.NamedElements[guid].Notes)).Where(note => note.PhaseName == PhaseName);
      private IEnumerable<Note> Errors => Notes.Where(note => note.NoteType == NoteType.Error);
      private IEnumerable<Note> Warnings => Notes.Where(note => note.NoteType == NoteType.Warning);
      private IEnumerable<Note> Infos => Notes.Where(note => note.NoteType == NoteType.Info);

      /// <summary>
      /// Add a note to given subject. Increment counters.
      /// Ensure that the subject is also maintained in <see cref="Database.ElementsWithNotes"/>.
      /// </summary>
      /// <param name="subject"></param>
      /// <param name="note"></param>
      /// <param name="insertions"></param>
      protected void AddNote(NamedElement subject, Note note, params object[] insertions) => subject.AddNote(PhaseName, note, insertions);
      protected void AddNote(Note note, params object[] insertions) => notes.Add(new Note(note, PhaseName, null, insertions));

      /// <summary>
      /// Report errors and warnings for this phase.
      /// List them if any, and return true.
      /// Otherwisw return false.
      /// </summary>
      /// <returns></returns>
      public bool AbortCompilation() {
         bool stop = Errors.Any() && !Settings.SettingValue<bool>("AllowErrors");
         string? message = null;
         if (stop) {
            message = $"{PhaseName}: Compilation aborted due to errors";
         } else {
            stop = Warnings.Any() && Settings.SettingValue<bool>("StopOnWarnings");
            if (stop)
               message = $"{PhaseName}: Compilation aborted due to warnings";
         }

         if (stop) {
            ReportNoteCounts(null,message);
            return true;
         } else {
            return false;
         }
      }

      /// <summary>
      /// Report the number of errors and warnings for the phase.
      /// Renamed for readability in some contexts.
      /// </summary>
      /// <param name="message">Optional termination message.</param>
      /// <returns></returns>
      public virtual void ReportNoteCounts(Reachable? reachable,string? message = null) {
         Log(0, $"{PhaseName}: {Errors.Count().Plural("error")}, {Warnings.Count().Plural("warning")}, {Infos.Count().Plural("info message")}");
         if (message != null) Log(0, message);

         NoteType messages = Settings.SettingValue<NoteType>("Messages")!;
         bool all = messages == NoteType.Info || Settings.SettingValue<bool>("ReportAll");
         ReportByType(Errors,all);
         if (messages == NoteType.Warning || messages == NoteType.Info)  ReportByType(Warnings,all);
         if (messages == NoteType.Info) ReportByType(Infos,all);

         void ReportByType(IEnumerable<Note> list,bool all) {
            foreach (Note note in list) {
               // Report messages only for reachable objects
               NamedElement? noteOwner = NamedElement.From<NamedElement>(note.Owner);
               if (all || reachable is null || note.Owner == Guid.Empty || noteOwner is Container _ || (noteOwner is CDL2Object obj && reachable.Objects.Contains(obj))) {
                  string head = $"{note.NoteType,7} {note.Number:D3}: ";
                  Log(0, $"   {head} {noteOwner?.FQDN()??PhaseName}\n    {new string(' ', head.Length)}{note.Text}");
               }
            }
         }
      }
   }

   /// <summary>
   /// The CDL2 Compiler. A singleton.
   /// Processes command line options and compiles the source files.
   /// </summary>
   public partial class CDL2 {
      public static readonly string Version = "1.0.0";

      static CDL2() => Compiler = new CDL2();
      private CDL2() { }
      public static readonly CDL2 Compiler;

      public CompilationPhase? CompilationPhase;

      private static void Main(string[] args) {
         Log(0, $"CDL2 Compiler v{Version}");

         Settings.ProcessCommandLine(args);

         Compiler.CompileSources(Settings.SettingValue<string[]>("Sources")!);
      }

      public Parser? Parser;
      public Reachable Reachable = new();
      public SemanticAnalyzer? SemanticAnalyzer;
      public CodeGenerator? codeGenerator;

      public void CompileSources(string[] args) {
         Log(0, $"Options: --sources {string.Join(',', args)} {Settings.IntOption("VerbosityLevel")}{Settings.IntOption("DebugVerbosityLevel")}" +
                                    $"{Settings.StringOption("Target")}{Settings.StringOption("ProgramName")}{Settings.BoolOption("SaveDB")}{Settings.StringOption("LoadDB")}" +
                                    $"{Settings.BoolOption("ParseOnly")}{Settings.BoolOption("StopOnWarnings")}{Settings.BoolOption("AllowErrors")}" +
                                    $"{Settings.StringOption("PrettyPrint")}");

         string? labOption = Settings.SettingValue<string>("LoadDB");
         if (labOption == string.Empty) labOption = "CDL2v1";
         if (labOption is not null) {
            Database.Load(labOption);

            Thread CLIThread = new(() => {
               Application app = new();
               // Create and show the window
               CommandPromptWindow commandWindow = new();
               CommandInterpreter CLI = new();
               

               // Handle commands
               commandWindow.CommandEntered += (sender, command) => {
                  // Parse and execute command
                  Command.Type commandType = Command.Identify(command);
                  CLI.IntepretCommand(command, commandType, commandWindow);
               };
               commandWindow.Closed += (s, e) => app.Shutdown();
               app.Run(commandWindow);
            });
            CLIThread.SetApartmentState(ApartmentState.STA);
            CLIThread.Start();
            CLIThread.Join(); // Wait for the command window to close before continuing

         } else if (args.Length > 0) {
            Parser = new Parser(this);
            foreach (string arg in args) {
               string source = Path.GetFullPath(arg);
               if (File.Exists(source)) {
                  Log(0, $"Compiling {source}");
                  Parser.Parse(source);
               }
            }

            if (Parser.AbortCompilation()) return;

            Program? MainProgram = null;
            string? ProgramName = Settings.SettingValue<string>("ProgramName");
            if (ProgramName == "" && Database.Instance.FirstProgram != null) {
               MainProgram = Database.Instance.FirstProgram;
            } else if (ProgramName != null && ProgramName != "") {
               MainProgram = Database.Instance.ProgramByName(ProgramName);
               if (MainProgram is null) {
                  if (Database.Instance.FirstProgram != null) {
                     MainProgram = Database.Instance.FirstProgram;
                     ReportError($"Program {ProgramName} not found, using {MainProgram.Id} instead.");
                  } else {
                     ReportError("No program found");
                  }
               }
            }
            if (MainProgram != null) {
               //if (Settings.SettingValue<int>("DebugVerbosityLevel") >= 4) ID.Dump();            

               // Perform semantic checks


               SemanticAnalyzer = SemanticAnalysis(MainProgram, Reachable);
               if (SemanticAnalyzer.AbortCompilation()) return;

               if (Settings.SettingValue<bool>("SaveDB")) {
                  Database.Save("CDL2v1");
                  Database.Load("CDL2v1");
                  SemanticAnalyzer = SemanticAnalysis(MainProgram, Reachable);
                  if (SemanticAnalyzer.AbortCompilation()) return;
               }

               string? PrettyPrint = Settings.SettingValue<string>("PrettyPrint");
               if (PrettyPrint != "" && (Database.Instance.Programs.Count > 0 || Database.Instance.Modules.Count > 0)) {
                  EmitterBase emitter;
                  if (PrettyPrint == null) {
                     emitter = new EmitterDebug();
                  } else if (Regex.IsMatch(PrettyPrint, @"^w(?:indow)$", RegexOptions.IgnoreCase)) {
                     emitter = new EmitterWindow();
                  } else if (PrettyPrint.IsValidFileName()) {  // Must be placed after check for window
                     emitter = new EmitterFile(PrettyPrint);
                  } else {
                     emitter = new EmitterDebug();
                  }
                  new PrettyPrinter(emitter).Print(Database.Instance.NamedElements.Values.OfType<Program>(), Database.Instance.NamedElements.Values.OfType<Module>());
                  emitter.Close();
               }

               if (!Settings.SettingValue<bool>("ParseOnly")) {
                  ICodeGenerator? cg = CreateCodeGenerator(Settings.SettingValue<string>("Target")!);

                  Debug.Assert(MainProgram != null);

                  if (cg != null) {
                     string targetFileName = Path.ChangeExtension(args[0], cg.FileExtension);
                     EmitterBase emitter = new EmitterFile(targetFileName) { IgnoreLineLength = true, SupressDebug = true };
                     Log(0, $"Generating code for {Settings.SettingValue<string>("Target")!} into {emitter.Target}");
                     codeGenerator = new CodeGenerator(cg, Compiler);
                     codeGenerator.GenerateCode(MainProgram, emitter);
                     emitter.Close();
                  } else {
                     ReportError("No target code generator");
                  }
               }

               Log(0, "");
               Parser.ReportNoteCounts(Reachable);
               Log(0, "");
               SemanticAnalyzer.ReportNoteCounts(Reachable);
            }
         }
      }

      private SemanticAnalyzer SemanticAnalysis(Program MainProgram,Reachable reachable) {
         SemanticAnalyzer semanticAnalyzer = new (this);
         semanticAnalyzer.Analyze(MainProgram);
         // The following two calls always clear any previously collected objects, so we can report unused objects.
         reachable.CollectAllObjects(MainProgram);       // Collect all the objects in the modules comprising the program, so we can report unused objects.
         reachable.CollectReachableObjects(MainProgram); // Collect all the objects reachable from the program's ludes.
         semanticAnalyzer.AnalyzeUnused(MainProgram, reachable);
         return semanticAnalyzer;
      }

      private static ICodeGenerator? CreateCodeGenerator(string target, string dataType = "Int64") {
         try {
            string className = $"CDL2v1.CodeGenerator{target}";
            Type? type = Type.GetType(className);
            if (type != null && typeof(ICodeGenerator).IsAssignableFrom(type)) {
               return Activator.CreateInstance(type, dataType) as ICodeGenerator;
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
}
