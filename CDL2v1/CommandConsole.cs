// <auto-gen>
//=======================================================================
// <copyright file="CommandPromptConsole.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-01-15</creation-date>
// 
// <summary>
//   Console-based REPL (Read-Eval-Print Loop) for the CDL2 Laboratory.
//   Provides command-line interface for interactive use.
// </summary>
//=======================================================================
// </auto-gen>


namespace CDL2v1 {
   /// <summary>
   /// Console-based command prompt for CDL2 Laboratory
   /// </summary>
   public class CommandConsole : ICLIREPL {
      private readonly List<string> _commandHistory = [];
      private int _historyIndex = -1;
      private bool _isRunning = false;

      private Action<string>? inputProcessor;

      public Emitter? Emitter { get; set; }

      public IEnumerable<string> CommandHistory {
         get => _commandHistory;
         set {
            _commandHistory.Clear();
            _commandHistory.AddRange(value);
            _historyIndex = _commandHistory.Count;
         }
      }

      /// <summary>
      /// Open the console REPL and start processing commands
      /// </summary>
      public void Open() {
         _isRunning = true;
         WriteLine($"\n{CDL2.LabName} v{CDL2.Version}");
         WriteLine("Type 'help' for available commands, 'exit' or 'quit' to exit.",severity:Severity.Info);

         bool multiline = false;
         List<string> lines = [];
         while (_isRunning) {
            if (multiline) {
               Console.Write("... ");
            } else if (Settings.SettingValue<bool>("LongConsolePrompt")) {
               if (Settings.SettingValue<bool>("ANSI")) {
                  Console.Write($"\x1b[93m{Focus.Current.Object?.FQDN() ?? ""}\x1b[0m> ");
               } else {
                  Console.Write($"{Focus.Current.Object?.FQDN() ?? ""}> ");
               }
            } else {
               Console.Write("> ");
            }
            string? line = Console.ReadLine();

            if (line == null) break;

            line = line.Trim();

            if (string.IsNullOrEmpty(line)) continue;

            if (multiline) {
               lines.Add(line);
               if (line.EndsWith('.')) {                  
                  // End of multiline input
                  line = string.Join("\n", lines).Trim();
                  lines.Clear();
                  multiline = false;
                  inputProcessor!(line);
               }
               continue;
            } else {
               if (char.IsAsciiLetterLower(line[0])) {
                  // Command
                  _commandHistory.Add(line);
                  _historyIndex = _commandHistory.Count;
                  inputProcessor!(line);
               } else if (!line.EndsWith('.')) {
                  // Start of multiline code snippet
                  multiline = true;
                  lines.Add(line);
                  continue;
               } else {
                  // Single line code snippet
                  inputProcessor!(line);
               }
            }
         }
      }

      public void Close() {
         _isRunning = false;
      }

      /// <summary>
      /// Configure formatted output (no-op for console)
      /// </summary>
      public void ConfigureFormattedOutput() {
         // Console doesn't need special configuration
      }

      /// <summary>
      /// Begin batch updating (no-op for console)
      /// </summary>
      public void BeginFormattedUpdate() { }

      /// <summary>
      /// End batch updating (no-op for console)
      /// </summary>
      public void EndFormattedUpdate() { }

      /// <summary>
      /// Force UI update (no-op for console)
      /// </summary>
      public void UpdateFormattedUI() { }

      /// <summary>
      /// Write a line of text with optional severity coloring
      /// </summary>
      public void WriteLine(string text,Severity severity = Severity.NONE) {
         ConsoleColor originalColor = Console.ForegroundColor;

         Console.ForegroundColor = severity switch {
            Severity.Error => ConsoleColor.Red,
            Severity.Warning => ConsoleColor.Yellow,
            Severity.Info => ConsoleColor.Cyan,
            Severity.Note => ConsoleColor.Gray,
            _ => originalColor
         };

         Console.WriteLine(text);
         Console.ForegroundColor = originalColor;
      }

      /// <summary>
      /// Display a query box and get user response
      /// </summary>
      public bool QueryBox(string message) {
         Console.Write($"{message} (y/n): ");
         string? response = Console.ReadLine();
         return response?.Trim().Equals("y",StringComparison.OrdinalIgnoreCase) == true;
      }

      /// <summary>
      /// Edit text in console (simple line input)
      /// </summary>
      public void EditText(string text = "") {
         Console.Write(": ");
         if (!string.IsNullOrEmpty(text)) {
            Console.Write(text);
         }

         string? input = Console.ReadLine();
         if (input != null) {
            inputProcessor!(input);
         }
      }

      public void SetInputProcessor(Action<string> processor) => inputProcessor = processor;

      /// <summary>
      /// Set status bar text (displays in console title)
      /// </summary>
      public void SetStatus(string message) {
         try {
            Console.Title = $"{CDL2.LabName} - {message}";
         } catch {
            // Ignore if console title cannot be set (e.g., in some environments)
         }
      }
   }
}