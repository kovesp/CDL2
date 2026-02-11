// <auto-gen>
//=======================================================================
// <copyright file="CDL2 Compiler.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-02-10</creation-date>
// 
// <summary>
//   The main program of the compiler and laboratory.
//   It initializes the compiler, processes command line options, and compiles the source files.
//   In -lab mode it loads the database and starts the command line interface for the laboratory.
// </summary>
// <attribution>
//   This file is part of the clean room reimplementation of the
//      CDL2 Compiler
//      CDL2 Laboratory
//      CDL2 Target Code Generators
//
//    Based on original work on CDL and CDL2 led by C. H. A. Koster
//    and the CDL2 team at the Universities of Berlin, Germany and
//    Nijmegen, The Netherlands.
//
//    The CDL2 Laboratory was the work of Epsilon GmbH, Berlin.
//    H. M. Stahl, H. Feuerhahn, JP. Dehotay, B. Böhringer
//    (and others I don't remember ... sorry).
//
//    Program icon by
//    <link rel="author" href="https://www.flaticon.com/free-icons/cdl" title="cdl icons">Cdl icons created by Icon home - Flaticon</link>
//    This project is not affiliated with the original CDL2 project.
// </attribution>
//=======================================================================
// </auto-gen>

using System.Data;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;

using static CDL2v1.Logger;

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
      private IEnumerable<Note> Errors => Notes.Where(note => note.NoteType == Severity.Error);
      private IEnumerable<Note> Warnings => Notes.Where(note => note.NoteType == Severity.Warning);
      private IEnumerable<Note> Infos => Notes.Where(note => note.NoteType == Severity.Info);

      /// <summary>
      /// Add a note to given subject. Increment counters.
      /// Ensure that the subject is also maintained in <see cref="Database.ElementsWithNotes"/>.
      /// </summary>
      /// <param name="subject"></param>
      /// <param name="note"></param>
      /// <param name="insertions"></param>
      protected Note AddNote(NamedElement subject,Note note,params object[] insertions) => subject.AddNote(PhaseName,note,insertions);
      protected Note AddNote(Note note,params object[] insertions) {
         Note newNote = new Note(note,PhaseName,null,insertions);
         notes.Add(newNote);
         return newNote;
      }

      /// <summary>
      /// Report errors and warnings for this phase.
      /// List them if any, and return true.
      /// Otherwise return false.
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
      public virtual void ReportNoteCounts(Reachable? reachable,string? message = null,Action<string>? reporter = null,bool summaryOnly=false) {
         reporter ??= msg=>Log(0,msg);
         reporter($"{PhaseName,-16}: {Errors.Count().Plural("error",",")} {Warnings.Count().Plural("warning",",")} {Infos.Count().Plural("info message")}");
         if (message != null) reporter(message);

         if (summaryOnly) {
            //ReportByType(Errors);
         } else { 
            Severity messages = Settings.SettingValue<Severity>("Messages")!;
            bool all = messages == Severity.Info || Settings.SettingValue<bool>("ReportAll");
            ReportByType(Errors,all);
            if (messages == Severity.Warning || messages == Severity.Info) ReportByType(Warnings,all);
            if (messages == Severity.Info) ReportByType(Infos,all);
         }

         void ReportByType(IEnumerable<Note> list,bool all=false) {
            foreach (Note note in list) {
               // Report messages only for reachable objects
               NamedElement? noteOwner = NamedElement.From<NamedElement>(note.Owner);
               if (all || reachable is null || note.Owner == Guid.Empty || noteOwner is Container _ || (noteOwner is CDL2Object obj && reachable.Objects.Contains(obj))) {
                  string head = $"{note.NoteType,7} {note.Number:D3}: ";
                  reporter($"   {head} {noteOwner?.FQDN() ?? PhaseName}\n    {new string(' ',head.Length)}{note.Text}");
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
      public const string LabName = "CDL2 Laboratory Redux";

      /// <summary>
      /// Static constructor
      /// </summary>
      static CDL2() => Compiler = new CDL2();

      private CDL2() { }
      public static readonly CDL2 Compiler;

      public CompilationPhase? CompilationPhase;

      [STAThread]
      private static void Main(string[] args) {
         Settings.LoadSettings();
         Settings.ProcessCommandLine(args);

         AllocateConsole();

         Log(2,$"\n {(Settings.LabMode ? CDL2.LabName : "Compiler")} v{Version}");
         Compiler.CompileSources(Settings.SettingValue<string[]>("Sources")!);
      }

      public Parser? Parser;

      public void CompileSources(string[] args) {
         Log(4,Settings.AllSettings.First().ToTabularString(title: true,compact: true)!);
         foreach (ISetting setting in Settings.AllSettings.OrderBy(s => s.LongOption)) {
            string? settingString = setting.ToTabularString(compact: true);
            if (settingString != null) Log(4,settingString);
         }


         if (Settings.LabMode) {
            bool usingGUI = Settings.OnWindows && !Settings.SettingValue<bool>("Console");
#if WINDOWS
            IToaster toaster = usingGUI ? new ToastWindow() : new ToastConsole();
#else
            IToaster toaster = new ToastConsole();
#endif
            Serializer.Toaster = toaster;
            Database.Load();

            // Ensure all programs are in an analyzed state
            foreach (Program program in Database.Instance.ProgramObjects) SemanticAnalyzer.AnalyzeProgram(program);

            if (usingGUI) {
#if WINDOWS
               Thread CLIThread = new(() => {
                  Application app = new();
                  // Create and show the window
                  ToastWindow guiToaster = new();
                  CommandWindow commandWindow = new(guiToaster);
                  Application.Current.MainWindow = commandWindow;
                  CommandInterpreter CLI = new(commandWindow,
                     new EmitterCommandWindow(commandWindow) { SuppressDebug = !Settings.SettingValue<bool>("PrettyPrintDebug") },
                     toaster = guiToaster);
                  Database.Instance.CLI = CLI;

                  // Handle commands
                  commandWindow.SetInputProcessor(CLI.ProcessInput);
                  commandWindow.Closed += (s,e) => app.Shutdown();
                  commandWindow.Title = $"{CDL2.LabName} - {Settings.LabDBName}";
                  CLI.SetStatus();
                  app.Run(commandWindow);
               });

               CLIThread.SetApartmentState(ApartmentState.STA);
               CLIThread.Start();
               CLIThread.Join();
#else
               throw new PlatformNotSupportedException("GUI mode is only supported on Windows");
#endif
            } else {
               CommandConsole repl = new();
               Emitter emitter = new EmitterAnsi(repl) { SuppressDebug = !Settings.SettingValue<bool>("PrettyPrintDebug") };
               CommandInterpreter CLI = new(repl,emitter,toaster);
               repl.SetInputProcessor(CLI.ProcessInput);
               Database.Instance.CLI = CLI;
               CLI.SetStatus();
               Settings.LoadSettings(repl);
               repl.Open();
            }
         } else if (args.Length > 0) {
            // Compiler mode
            Parser = new Parser(this);
            foreach (string arg in args) {
               string source = Path.GetFullPath(arg);
               if (File.Exists(source)) {
                  Log(0,$"Compiling {source}");
                  Parser.Parse(source);
               }
            }

            if (Parser.AbortCompilation()) return;

            Program? MainProgram = GetMainProgram();
            if (MainProgram != null) {
               if (MainProgram.SemanticAnalyzer.AbortCompilation()) return;
               Database.Save();

               string? PrettyPrint = Settings.SettingValue<string>("PrettyPrint");
               if (PrettyPrint != "" && (Database.Instance.Programs.Count > 0 || Database.Instance.Modules.Count > 0)) {
                  Emitter emitter;
                  if (PrettyPrint == null) {
                     emitter = new EmitterDebug();
#if WINDOWS
                  } else if (Regex.IsMatch(PrettyPrint,@"^w(?:indow)$",RegexOptions.IgnoreCase)) {
                     emitter = new EmitterWindow();
#endif
                  } else if (PrettyPrint.IsValidFileName) {
                     emitter = new EmitterFile(PrettyPrint);
                  } else {
                     emitter = new EmitterDebug();
                  }
                  new PrettyPrinter(emitter).Print(Database.Instance.NamedElements.Values.OfType<Program>(),Database.Instance.NamedElements.Values.OfType<Module>());
                  emitter.Close();
               }

               if (!Settings.SettingValue<bool>("ParseOnly")) {
                  string targetFileName = Settings.SettingValue<string>("file") ?? "";
                  string? target = Settings.SettingValue<string>("target")!;
                  CodeGenerator.GenerateCode(ref targetFileName,(note,args) => Log(0,$"{note.NoteType} {note.FormattedText(args)}"),target: target,program: MainProgram);
               }

               Log(0,"");
               Parser.ReportNoteCounts(null);
               Log(0,"");
               MainProgram.SemanticAnalyzer.ReportNoteCounts(MainProgram.SemanticAnalyzer.Reachable);
            }
         }
      }

      /// <summary>
      /// Perform semantic analysis on the given program.
      /// Note that a new semantic analyzer is always created.
      /// This is because semantic analysis may be performed multiple times in the laboratory, and we want to keep the notes separate for each run.
      /// The only use ot the property is to retrieve statistics.
      /// </summary>
      /// <param name="program"></param>
      //public void PerformSemanticAnalysis(Program program) => SemanticAnalyzer = SemanticAnalyzer.PerformSemanticAnalysis(this,program);

      public static Program? GetMainProgram() {
         Program? program = null;
         string? ProgramName = Settings.SettingValue<string>("ProgramName");
         if (ProgramName == "" && Database.Instance.FirstProgram != null) {
            program = Database.Instance.FirstProgram;
         } else if (ProgramName.IsNotNullEmptyOrWhitespace) {
            program = Database.Instance.ProgramByName(ProgramName!);
            if (program is null) {
               if (Database.Instance.FirstProgram != null) {
                  program = Database.Instance.FirstProgram;
                  ReportError($"Program {ProgramName} not found, using {program.Id} instead.");
               } else {
                  ReportError("No program found");
               }
            }
         }

         Settings.SettingValue<string>("ProgramName",program?.Id.Name ?? "");
         return program;
      }
      internal void SkipToNextEnd() => Parser?.SkipToNextEnd();

#if WINDOWS
      private static void AllocateConsole() {
         // On Windows with WinExe, allocate console if needed
         if (Settings.SettingValue<bool>("Console")) {
            AllocConsole();

            StreamWriter standardOutput = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            Console.SetOut(standardOutput);
            Console.SetError(standardOutput);

            StreamReader standardInput = new StreamReader(Console.OpenStandardInput());
            Console.SetIn(standardInput);
         }
      }

      [DllImport("kernel32.dll")]
      private static extern IntPtr GetConsoleWindow();

      [DllImport("user32.dll")]
      private static extern bool ShowWindow(IntPtr hWnd,int nCmdShow);

      private const int SW_HIDE = 0;

      [DllImport("kernel32.dll",SetLastError = true)]
      [return: MarshalAs(UnmanagedType.Bool)]
      private static extern bool AllocConsole();

      [DllImport("kernel32.dll",SetLastError = true)]
      [return: MarshalAs(UnmanagedType.Bool)]
      private static extern bool AttachConsole(int dwProcessId);

      [DllImport("kernel32.dll",SetLastError = true)]
      private static extern IntPtr GetStdHandle(int nStdHandle);

      private const int ATTACH_PARENT_PROCESS = -1;
      private const int STD_OUTPUT_HANDLE = -11;
      private const int STD_INPUT_HANDLE = -10;
#else
      private static void AllocateConsole() {}
#endif // WINDOWS
   }
}