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

         while (_isRunning) {
            string prompt = GetPrompt(false);
            string? line = ReadLineWithHistory(prompt, true);

            if (line == null) break;

            line = line.Trim();

            if (string.IsNullOrEmpty(line)) continue;

            // Skip comment lines (starting with !)
            if (line[0] == '!') continue;

            if (char.IsAsciiLetterLower(line[0])) {
               // Command
               _commandHistory.Add(line);
               inputProcessor!(line);
            } else if (!line.EndsWith('.')) {
               // Start of multiline code snippet - enter edit mode
               string? editedText = ReadMultilineText(line);
               if (editedText != null) {
                  inputProcessor!(editedText);
               }
            } else {
               // Single line code snippet
               inputProcessor!(line);
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
      /// Edit text in console with multi-line support
      /// </summary>
      public void EditText(string text = "") {
         string? result = ReadMultilineText(text);
         if (result != null) {
            inputProcessor!(result);
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
      /// Read multi-line text with pre-loaded content and full editing support
      /// </summary>
      private string? ReadMultilineText(string initialText = "") {
         // Save current console colors
         ConsoleColor savedForeground = Console.ForegroundColor;
         ConsoleColor savedBackground = Console.BackgroundColor;

         try {
            // Trim trailing whitespace and split into lines
            string trimmedText = initialText.TrimEnd();
            List<string> lines = string.IsNullOrEmpty(trimmedText) 
               ? [""] 
               : trimmedText.Split('\n').ToList();
            
            int currentLine = lines.Count - 1;
            int cursorPosition = lines[currentLine].Length;
            int startTop = Console.CursorTop;
            int maxLinesDisplayed = lines.Count;

            RedrawAllLines(lines, currentLine, cursorPosition, startTop, ref maxLinesDisplayed);

            while (true) {
               ConsoleKeyInfo keyInfo = Console.ReadKey(intercept: true);

               if (keyInfo.Key == ConsoleKey.Enter) {
                  // Only terminate if on last line, at end, and ends with period
                  if (IsAtTerminationPoint(lines, currentLine, cursorPosition)) {
                     Console.SetCursorPosition(0, startTop + lines.Count);
                     Console.WriteLine();
                     return string.Join("\n", lines);
                  } else {
                     // Insert new line
                     string currentLineText = lines[currentLine];
                     lines[currentLine] = currentLineText[..cursorPosition];
                     lines.Insert(currentLine + 1, currentLineText[cursorPosition..]);
                     currentLine++;
                     cursorPosition = 0;
                     RedrawAllLines(lines, currentLine, cursorPosition, startTop, ref maxLinesDisplayed);
                  }
               } else if (keyInfo.Key == ConsoleKey.Escape) {
                  Console.SetCursorPosition(0, startTop + Math.Max(lines.Count, maxLinesDisplayed));
                  Console.WriteLine("\n[Editing cancelled]");
                  return null;
               } else if (keyInfo.Key == ConsoleKey.UpArrow) {
                  if (currentLine > 0) {
                     currentLine--;
                     cursorPosition = Math.Min(cursorPosition, lines[currentLine].Length);
                     RedrawAllLines(lines, currentLine, cursorPosition, startTop, ref maxLinesDisplayed);
                  }
               } else if (keyInfo.Key == ConsoleKey.DownArrow) {
                  if (currentLine < lines.Count - 1) {
                     currentLine++;
                     cursorPosition = Math.Min(cursorPosition, lines[currentLine].Length);
                     RedrawAllLines(lines, currentLine, cursorPosition, startTop, ref maxLinesDisplayed);
                  }
               } else if (keyInfo.Key == ConsoleKey.LeftArrow) {
                  if (cursorPosition > 0) {
                     cursorPosition--;
                     RedrawAllLines(lines, currentLine, cursorPosition, startTop, ref maxLinesDisplayed);
                  } else if (currentLine > 0) {
                     currentLine--;
                     cursorPosition = lines[currentLine].Length;
                     RedrawAllLines(lines, currentLine, cursorPosition, startTop, ref maxLinesDisplayed);
                  }
               } else if (keyInfo.Key == ConsoleKey.RightArrow) {
                  if (cursorPosition < lines[currentLine].Length) {
                     cursorPosition++;
                     RedrawAllLines(lines, currentLine, cursorPosition, startTop, ref maxLinesDisplayed);
                  } else if (currentLine < lines.Count - 1) {
                     currentLine++;
                     cursorPosition = 0;
                     RedrawAllLines(lines, currentLine, cursorPosition, startTop, ref maxLinesDisplayed);
                  }
               } else if (keyInfo.Key == ConsoleKey.Home) {
                  cursorPosition = 0;
                  RedrawAllLines(lines, currentLine, cursorPosition, startTop, ref maxLinesDisplayed);
               } else if (keyInfo.Key == ConsoleKey.End) {
                  cursorPosition = lines[currentLine].Length;
                  RedrawAllLines(lines, currentLine, cursorPosition, startTop, ref maxLinesDisplayed);
               } else if (keyInfo.Key == ConsoleKey.Backspace) {
                  if (cursorPosition > 0) {
                     lines[currentLine] = lines[currentLine].Remove(cursorPosition - 1, 1);
                     cursorPosition--;
                     RedrawAllLines(lines, currentLine, cursorPosition, startTop, ref maxLinesDisplayed);
                  } else if (currentLine > 0) {
                     // Merge with previous line
                     string mergedLine = lines[currentLine - 1] + lines[currentLine];
                     cursorPosition = lines[currentLine - 1].Length;
                     lines.RemoveAt(currentLine);
                     currentLine--;
                     lines[currentLine] = mergedLine;
                     RedrawAllLines(lines, currentLine, cursorPosition, startTop, ref maxLinesDisplayed);
                  }
               } else if (keyInfo.Key == ConsoleKey.Delete) {
                  if (cursorPosition < lines[currentLine].Length) {
                     lines[currentLine] = lines[currentLine].Remove(cursorPosition, 1);
                     RedrawAllLines(lines, currentLine, cursorPosition, startTop, ref maxLinesDisplayed);
                  } else if (currentLine < lines.Count - 1) {
                     // Merge with next line
                     lines[currentLine] += lines[currentLine + 1];
                     lines.RemoveAt(currentLine + 1);
                     RedrawAllLines(lines, currentLine, cursorPosition, startTop, ref maxLinesDisplayed);
                  }
               } else if (!char.IsControl(keyInfo.KeyChar)) {
                  lines[currentLine] = lines[currentLine].Insert(cursorPosition, keyInfo.KeyChar.ToString());
                  cursorPosition++;
                  RedrawAllLines(lines, currentLine, cursorPosition, startTop, ref maxLinesDisplayed);
               }
            }
         } finally {
            // Restore original console colors
            Console.ForegroundColor = savedForeground;
            Console.BackgroundColor = savedBackground;
         }
      }

      /// <summary>
      /// Check if cursor is at termination point (last line, at end, ends with period)
      /// </summary>
      private static bool IsAtTerminationPoint(List<string> lines, int currentLine, int cursorPosition) {
         if (currentLine != lines.Count - 1) return false;
         if (cursorPosition != lines[currentLine].Length) return false;
         
         string fullText = string.Join("\n", lines).TrimEnd();
         return fullText.Length > 0 && fullText[^1] == '.';
      }

      /// <summary>
      /// Verify the syntax of the current text
      /// </summary>
      private static bool VerifySyntax(List<string> lines) {
         string text = string.Join("\n", lines);
         return Database.Instance.CLI?.VerifySyntax(text) ?? false;
      }

      /// <summary>
      /// Redraw all lines in the multi-line editor with syntax-based background color
      /// </summary>
      private static void RedrawAllLines(List<string> lines, int currentLine, int cursorPosition, int startTop, ref int maxLinesDisplayed) {
         int windowWidth = Console.WindowWidth;
         
         // Update the maximum number of lines we've displayed
         if (lines.Count > maxLinesDisplayed) maxLinesDisplayed = lines.Count;
         
         // Verify syntax and set background color accordingly
         bool syntaxValid = VerifySyntax(lines);
         Console.BackgroundColor = syntaxValid ? ConsoleColor.White : ConsoleColor.Yellow;
         Console.ForegroundColor = ConsoleColor.Black;
         
         // Clear and redraw all lines
         for (int i = 0; i < maxLinesDisplayed; i++) {
            Console.SetCursorPosition(0, startTop + i);
            Console.Write(new string(' ', windowWidth - 1));
            Console.SetCursorPosition(0, startTop + i);
            if (i < lines.Count) {
               Console.Write(": " + lines[i]);
            }
         }
         
         // Position cursor
         Console.SetCursorPosition(2 + cursorPosition, startTop + currentLine);
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