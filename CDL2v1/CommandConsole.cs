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

using System.Text.RegularExpressions;

namespace CDL2v1 {
   /// <summary>
   /// Console-based command prompt for CDL2 Laboratory
   /// </summary>
   public class CommandConsole : ICLIREPL {
      private readonly History _commandHistory = new();
      private bool _isRunning = false;

      private Action<string>? inputProcessor;

      public Emitter? Emitter { get; set; }

      public IEnumerable<string> CommandHistory {
         get => _commandHistory.Commands;
         set => _commandHistory.Commands = value;
      }

      /// <summary>
      /// Open the console REPL and start processing commands
      /// </summary>
      public void Open() {
         _isRunning = true;
         Settings.LoadSettings(this);
         WriteLine($"\n{CDL2.LabName} v{CDL2.Version}");
         WriteLine("Type 'help' for available commands, 'exit' or 'quit' to exit.",severity:Severity.Info);

         bool multiline = false;
         List<string> lines = [];
         while (_isRunning) {
            string prompt = GetPrompt(multiline);
            string? line = ReadLineWithHistory(prompt, !multiline);

            if (line == null) break;

            line = line.Trim();

            if (string.IsNullOrEmpty(line)) continue;

            // Skip comment lines (starting with !)
            if (line[0] == '!') continue;

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
         Settings.SaveSettings(this);
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

      /////////////////////
      // Local functions //
      /////////////////////

      /// <summary>
      /// Get the appropriate prompt string
      /// </summary>
      private string GetPrompt(bool multiline) {
         if (multiline) return "... ";

         if (!Settings.SettingValue<bool>("LongConsolePrompt")) return "> ";

         string fqdn = Focus.Current.Object?.FQDN() ?? "";
         if (Settings.SettingValue<bool>("ANSI")) return $"\x1b[93m{fqdn}\x1b[0m> ";

         return $"{fqdn}> ";
      }

      /// <summary>
      /// Calculate the visual length of a string, excluding ANSI escape codes
      /// </summary>
      private static int GetVisualLength(string text) {
         // Remove ANSI escape sequences (pattern: ESC [ ... m)
         string withoutAnsi = Regex.Replace(text, @"\x1b\[[0-9;]*m", "");
         return withoutAnsi.Length;
      }

      /// <summary>
      /// Read a line of input with history navigation support
      /// </summary>
      private string? ReadLineWithHistory(string prompt, bool enableHistory) {
         Console.Write(prompt);
         
         if (!enableHistory) return Console.ReadLine();

         int promptVisualLength = GetVisualLength(prompt);
         List<char> buffer = [];
         int cursorPosition = 0;

         while (true) {
            ConsoleKeyInfo keyInfo = Console.ReadKey(intercept: true);

            if (keyInfo.Key == ConsoleKey.Enter) {
               Console.WriteLine();
               return new string(buffer.ToArray());
            } else if (keyInfo.Key == ConsoleKey.UpArrow) {
               string? previous = _commandHistory.Previous();
               if (previous != null) {
                  ReplaceBuffer(buffer, previous, ref cursorPosition, prompt, promptVisualLength);
               }
            } else if (keyInfo.Key == ConsoleKey.DownArrow) {
               string? next = _commandHistory.Next();
               if (next != null) {
                  ReplaceBuffer(buffer, next, ref cursorPosition, prompt, promptVisualLength);
               }
            } else if (keyInfo.Key == ConsoleKey.Backspace) {
               if (cursorPosition > 0) {
                  buffer.RemoveAt(cursorPosition - 1);
                  cursorPosition--;
                  RedrawLine(buffer, cursorPosition, prompt, promptVisualLength);
               }
            } else if (keyInfo.Key == ConsoleKey.Delete) {
               if (cursorPosition < buffer.Count) {
                  buffer.RemoveAt(cursorPosition);
                  RedrawLine(buffer, cursorPosition, prompt, promptVisualLength);
               }
            } else if (keyInfo.Key == ConsoleKey.LeftArrow) {
               if (cursorPosition > 0) {
                  cursorPosition--;
                  Console.SetCursorPosition(promptVisualLength + cursorPosition, Console.CursorTop);
               }
            } else if (keyInfo.Key == ConsoleKey.RightArrow) {
               if (cursorPosition < buffer.Count) {
                  cursorPosition++;
                  Console.SetCursorPosition(promptVisualLength + cursorPosition, Console.CursorTop);
               }
            } else if (keyInfo.Key == ConsoleKey.Home) {
               cursorPosition = 0;
               Console.SetCursorPosition(promptVisualLength, Console.CursorTop);
            } else if (keyInfo.Key == ConsoleKey.End) {
               cursorPosition = buffer.Count;
               Console.SetCursorPosition(promptVisualLength + cursorPosition, Console.CursorTop);
            } else if (!char.IsControl(keyInfo.KeyChar)) {
               buffer.Insert(cursorPosition, keyInfo.KeyChar);
               cursorPosition++;
               RedrawLine(buffer, cursorPosition, prompt, promptVisualLength);
            }
         }
      }

      /// <summary>
      /// Replace the current buffer with history content
      /// </summary>
      private static void ReplaceBuffer(List<char> buffer, string text, ref int cursorPosition, string prompt, int promptVisualLength) {
         buffer.Clear();
         buffer.AddRange(text);
         cursorPosition = buffer.Count;
         RedrawLine(buffer, cursorPosition, prompt, promptVisualLength);
      }

      /// <summary>
      /// Redraw the current input line
      /// </summary>
      private static void RedrawLine(List<char> buffer, int cursorPosition, string prompt, int promptVisualLength) {
         int currentTop = Console.CursorTop;
         int windowWidth = Console.WindowWidth;
         
         // Clear the entire line by overwriting with spaces
         Console.SetCursorPosition(0, currentTop);
         Console.Write(new string(' ', windowWidth - 1));
         
         // Redraw the prompt and buffer
         Console.SetCursorPosition(0, currentTop);
         Console.Write(prompt + new string(buffer.ToArray()));
         
         // Position cursor at the correct location using visual length
         Console.SetCursorPosition(promptVisualLength + cursorPosition, currentTop);
      }
   }
}