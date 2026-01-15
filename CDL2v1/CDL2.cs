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

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
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
      protected Note AddNote(NamedElement subject, Note note, params object[] insertions) => subject.AddNote(PhaseName, note, insertions);
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
      public virtual void ReportNoteCounts(Reachable? reachable,string? message = null) {
         Log(0, $"{PhaseName,-16}: {Errors.Count().Plural("error",",")} {Warnings.Count().Plural("warning",",")} {Infos.Count().Plural("info message")}");
         if (message != null) Log(0, message);

         Severity messages = Settings.SettingValue<Severity>("Messages")!;
         bool all = messages == Severity.Info || Settings.SettingValue<bool>("ReportAll");
         ReportByType(Errors,all);
         if (messages == Severity.Warning || messages == Severity.Info)  ReportByType(Warnings,all);
         if (messages == Severity.Info) ReportByType(Infos,all);

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
      public static readonly Dictionary<string,Type> AvailableCodeGenerators = [];

      public const string LabName = "CDL2 Laboratory Redux";

      /// <summary>
      /// Static constructor
      /// </summary>
      static CDL2() {
         Compiler = new CDL2();
         foreach (Type cg in GetAvailableCodeGenerators()) {
            AvailableCodeGenerators[cg.Name.Replace("CodeGenerator","")] = cg;
         }
      }

      private CDL2() { }
      public static readonly CDL2 Compiler;



      public CompilationPhase? CompilationPhase;
      [STAThread]
      private static void Main(string[] args) {

         // Load saved settings first
         Settings.LoadSettings();
         
         // Then process command line args (they'll override saved settings)
         Settings.ProcessCommandLine(args);

         Log(0, $"\nCDL2 {(Settings.LabMode?CDL2.LabName:"Compiler")} v{Version}");

         Compiler.CompileSources(Settings.SettingValue<string[]>("Sources")!);
      }

      public Parser? Parser;
      public Reachable Reachable = new();
      public SemanticAnalyzer? SemanticAnalyzer;

      

      public void CompileSources(string[] args) {
         Log(Settings.AllSettings.First().ToTabularString(title: true, compact: true)!);
         foreach (ISetting setting in Settings.AllSettings.OrderBy(s => s.LongOption)) {
            string? settingString = setting.ToTabularString(compact: true);
            if (settingString != null) Log(1, settingString);
         }


         if (Settings.LabMode) {
            bool usingGUI = Settings.OnWindows && !Settings.SettingValue<bool>("Console") && Settings.LabMode;
            IToaster toaster = usingGUI ? new ToastWindow() : new ToastConsole(); // Kludge, but ...;;
            Serializer.Toaster = toaster;
            Database.Load();

            SemanticAnalyzer = SemanticAnalysis(GetMainProgram()!,Reachable);
            if (SemanticAnalyzer.AbortCompilation()) return;

            if (usingGUI) {
               Thread CLIThread = new(() => {
                  Application app = new();
                  // Create and show the window
                  ToastWindow guiToater = new();
                  CommandPromptWindow commandWindow = new(guiToater);
                  CommandInterpreter CLI = new(commandWindow,
                     new EmitterCommandWindow(commandWindow) { SuppressDebug = !Settings.SettingValue<bool>("PrettyPrintDebug") },
                     toaster=guiToater); // Needs to be set again 
                  Database.Instance.CLI = CLI;

                  void ProcessInput(string input) {
                     input = input.Trim();

                     if (char.IsAsciiLetterLower(input[0])) {
                        // Commands start with a lowercase letter
                        CLI.InterpretCommand(input);
                     } else if (!input.StartsWith(CommandInterpreter.CommandComment)) { // A caommand comment. Can't be the CDL2 comment delimiter # becasue that is valid in CDL2 source
                        // Assume it is a cdl2 construct that must be parsed
                        CLI.EnterCode(input);
                     }
                  }

                  // Handle commands
                  commandWindow.CommandEntered += (sender,input) => ProcessInput(input);
                  commandWindow.Closed += (s,e) => app.Shutdown();
                  commandWindow.Title = $"{CDL2.LabName} - {Settings.LabDBName}";
                  CLI.SetStatus();
                  app.Run(commandWindow);
               });

               CLIThread.SetApartmentState(ApartmentState.STA);
               CLIThread.Start();
               CLIThread.Join(); // Wait for the command window to close before continuing
            } else {
               // Command line interface mode
               // TODO: Implement command line interface mode
               Log(0, "CDL2 Laboratory command line interface mode is not yet implemented.");
               // toaster is already set to new ToastConsole(); 
            }
         } else if (args.Length > 0) { // File compiler mode
            Parser = new Parser(this);
            foreach (string arg in args) {
               string source = Path.GetFullPath(arg);
               if (File.Exists(source)) {
                  Log(0, $"Compiling {source}");
                  Parser.Parse(source);
               }
            }

            if (Parser.AbortCompilation()) return;

            Program? MainProgram = GetMainProgram();
            if (MainProgram != null) {
               // Perform semantic checks
               SemanticAnalyzer = SemanticAnalysis(MainProgram, Reachable);
               if (SemanticAnalyzer.AbortCompilation()) return;

               // Save the database after parsing and semantic analysis
               Database.Save();

               string? PrettyPrint = Settings.SettingValue<string>("PrettyPrint");
               if (PrettyPrint != "" && (Database.Instance.Programs.Count > 0 || Database.Instance.Modules.Count > 0)) {
                  Emitter emitter;
                  if (PrettyPrint == null) {
                     emitter = new EmitterDebug();
                  } else if (Regex.IsMatch(PrettyPrint, @"^w(?:indow)$", RegexOptions.IgnoreCase)) {
                     emitter = new EmitterWindow();
                  } else if (PrettyPrint.IsValidFileName) {  // Must be placed after check for window
                     emitter = new EmitterFile(PrettyPrint);
                  } else {
                     emitter = new EmitterDebug();
                  }
                  new PrettyPrinter(emitter).Print(Database.Instance.NamedElements.Values.OfType<Program>(), Database.Instance.NamedElements.Values.OfType<Module>());
                  emitter.Close();
               }

               if (!Settings.SettingValue<bool>("ParseOnly")) {
                  GenerateCode(out _,PrettyPrint,MainProgram);
               }

               Log(0, "");
               Parser.ReportNoteCounts(Reachable);
               Log(0, "");
               SemanticAnalyzer.ReportNoteCounts(Reachable);
            }
         }
      }

      public static void GenerateCode(out string targetFileName,string? target=null,Program? MainProgram = null) {
         MainProgram ??= CDL2.GetMainProgram();
         if (target == null) target = Settings.SettingValue<string>("Target");
         ICodeGenerator? cg = CreateCodeGenerator(target!);

         targetFileName = "";

         if (cg != null) {
            Emitter? emitter = null;
            try {
               targetFileName = Path.Combine(Settings.OutputDirectory,Path.ChangeExtension(MainProgram!.Id.Name,cg.FileExtension));
               emitter = new EmitterFile(targetFileName) { IgnoreLineLength = true,SuppressDebug = ! Settings.SettingValue<bool>("CGDebug") };
               Log(0,$"\nGenerating {Settings.SettingValue<string>("Target")!} code for {MainProgram}");
               CodeGenerator codeGenerator = new(cg,Compiler);
               codeGenerator.GenerateCode(MainProgram,emitter);
               Log(0, $"Code generation complete. Output written to {targetFileName}");
            } catch (Exception ex) {
               ReportError($"Error during code generation: {ex.Message} {ex.StackTrace}");
            } finally {
               emitter?.Close();
            }
         } else {
            ReportError("No target code generator");
         }
      }

      public static Program? GetMainProgram() {
         Program? program = null;
         string? ProgramName = Settings.SettingValue<string>("ProgramName");
         if (ProgramName == "" && Database.Instance.FirstProgram != null) {
            program = Database.Instance.FirstProgram;
         } else if (ProgramName != null && ProgramName != "") {
            program = Database.Instance.ProgramByName(ProgramName);
            if (program is null) {
               if (Database.Instance.FirstProgram != null) {
                  program = Database.Instance.FirstProgram;
                  ReportError($"Program {ProgramName} not found, using {program.Id} instead.");
               } else {
                  ReportError("No program found");
               }
            }
         }

         Settings.SettingValue<string>("ProgramName",program?.Id.Name??"");
         return program;
      }

      public SemanticAnalyzer SemanticAnalysis(Program MainProgram,Reachable reachable) {
         SemanticAnalyzer semanticAnalyzer = new (this);
         semanticAnalyzer.Analyze(MainProgram);
         // The following two calls always clear any previously collected objects, so we can report unused objects.
         reachable.CollectAllObjects(MainProgram);       // Collect all the objects in the modules comprising the program, so we can report unused objects.
         reachable.CollectReachableObjects(MainProgram); // Collect all the objects reachable from the program's ludes.
         semanticAnalyzer.AnalyzeUnused(MainProgram, reachable);
         return semanticAnalyzer;
      }

      private static readonly Dictionary<string,ICodeGenerator?> CodeGeneratorCache = [];
      private static ICodeGenerator? CreateCodeGenerator(string target, string dataType = "long") {
         if (CodeGeneratorCache.TryGetValue(target,out ICodeGenerator? cached)) return cached;
         try {
            if (AvailableCodeGenerators.TryGetValue(target,out Type? type)) {
               return CodeGeneratorCache[target]=Activator.CreateInstance(type, dataType) as ICodeGenerator;
            }
         } catch (Exception ex) {
            ReportError($"Error creating code generator for target {target} with Data type {dataType}: {ex.Message}");
         }
         return CodeGeneratorCache[target]=null;
      }

      private static IEnumerable<Type> GetAvailableCodeGenerators() {
         Assembly currentAssembly = Assembly.GetExecutingAssembly();
         return currentAssembly.GetTypes()
            .Where(t => 
               t.IsClass && 
               !t.IsAbstract &&
               typeof(ICodeGenerator).IsAssignableFrom(t) &&
               t.Name.StartsWith("CodeGenerator"));
      }

      /// <summary>
      /// Called by <see cref="ReportError"/> to skip to the next END token."/>
      /// </summary>
      internal void SkipToNextEnd() => Parser?.SkipToNextEnd();
   }
}

