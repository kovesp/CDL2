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
      private bool _statusLineEnabled = false;
      private int _terminalHeight = 0;

      private const string _editModeHelp = """
Editing keys

Key | Action
---
Enter       | Insert a new line, but submit when caret
            | is at end and line ends with period ('.').
Ctrl-Enter  | Submit the CDL2 construct (last line must end with '.').
Esc         | Cancel editing.
Arrows      | Navigate within the text. With Shift, select text.
Home        | Move to beginning of current line.
End         | Move to end of current line.
Backspace   | Delete character before cursor or merge
            | with previous line at start of line.
Delete      | Delete character at cursor or merge with
            | next line at end of line.
Ctrl-C      | Copy selected text
Ctrl-X      | Cut selected text
Ctrl-V      | Paste text from
Ctrl-Z      | Undo last change (up to 100 levels).
Ctrl-Y      | Redo last undone change.
F1          | Show this help message.

Background color indicates syntax validity:
  White  = Valid syntax
  Yellow = Syntax errors detected
""";

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
         
         // Set up status line and scrolling region
         SetupStatusLine();
         
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
         CleanupStatusLine();
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
      /// Set status bar text using ANSI scrolling region
      /// </summary>
      public void SetStatus(string message) {
         if (_statusLineEnabled) {
            // Build right-side status (same as GUI version)
            string programName = Settings.SettingValue<string>("ProgramName")!;
            string marker = programName.IsNotEmptyOrWhitespace && Database.Instance.ProgramByName(programName)!.Modified ? "*" : "";
            string rightStatus = $"[{Database.Instance.GetModificationCount()}/{Settings.SettingValue<int>("AutosaveCount")}] {marker}Prog {programName}";
            
            // Save cursor position
            Console.Write("\x1b[s");
            
            // Move to line 1 (status line)
            Console.Write("\x1b[1;1H");
            
            // Clear the line and write status
            Console.Write("\x1b[2K");
            Console.ForegroundColor = ConsoleColor.Black;
            Console.BackgroundColor = ConsoleColor.Gray;
            
            // Calculate spacing to right-justify the right portion
            int width = Console.WindowWidth;
            int leftLen = message.Length;
            int rightLen = rightStatus.Length;
            int spacesNeeded = Math.Max(1, width - leftLen - rightLen);
            
            // Write left part, spaces, then right part
            string statusLine = (message + new string(' ', spacesNeeded) + rightStatus);
            
            // Truncate or pad to exact width
            if (statusLine.Length > width) {
               statusLine = statusLine[..(width - 3)] + "...";
            } else if (statusLine.Length < width) {
               statusLine = statusLine.PadRight(width);
            }
            
            Console.Write(statusLine);
            
            // Reset colors
            Console.ResetColor();
            
            // Restore cursor position
            Console.Write("\x1b[u");
         } else {
            // Fallback: set console title
            try {
               Console.Title = $"{CDL2.LabName}";
            } catch {
               // Ignore if console title cannot be set
            }
         }
      }

      /////////////////////
      // Local functions //
      /////////////////////

      /// <summary>
      /// Setup status line with scrolling region
      /// </summary>
      private void SetupStatusLine() {
         try {
            _terminalHeight = Console.WindowHeight;

            // Clear screen
            Console.Write("\x1b[2J");

            // Initialize status line (line 1) with blank content
            Console.Write("\x1b[1;1H");
            Console.ForegroundColor = ConsoleColor.Black;
            Console.BackgroundColor = ConsoleColor.Gray;
            Console.Write(new string(' ',Console.WindowWidth));
            Console.ResetColor();

            // Set scrolling region to lines 2 through bottom
            Console.Write($"\x1b[2;{_terminalHeight}r");

            // Move cursor to line 2 (start of scrolling region)
            Console.Write("\x1b[2;1H");

            _statusLineEnabled = true;

            // Initialize status line with right-side content
            SetStatus("Nothing");
         } catch {
            // If anything fails, disable status line
            _statusLineEnabled = false;
         }
      }

      /// <summary>
      /// Cleanup status line and restore normal scrolling
      /// </summary>
      private void CleanupStatusLine() {
         if (_statusLineEnabled) {
            // Reset scrolling region
            Console.Write("\x1b[r");
            
            // Move cursor to bottom of screen
            Console.Write($"\x1b[{_terminalHeight};1H");
            
            Console.WriteLine();
            _statusLineEnabled = false;
         }
      }

      /// <summary>
      /// Get the appropriate prompt string
      /// </summary>
      private string GetPrompt(bool multiline) {
         if (multiline) return "... ";

         // When status line is enabled, always use short prompt (FQDN is in status line)
         if (_statusLineEnabled || !Settings.SettingValue<bool>("LongConsolePrompt")) return "> ";

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
            } else if (keyInfo.Key == ConsoleKey.Delete && keyInfo.Modifiers == ConsoleModifiers.Alt) {
               // Alt+Delete: Clear the console
               ClearConsole();
               // Redraw the prompt and current buffer
               Console.Write(prompt);
               Console.Write(new string(buffer.ToArray()));
               Console.SetCursorPosition(promptVisualLength + cursorPosition, Console.CursorTop);
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
            // Set edit mode colors to black on white
            Console.ForegroundColor = ConsoleColor.Black;
            Console.BackgroundColor = ConsoleColor.White;
            
            // Display header line
            Console.WriteLine("[Edit text, F1 for help]");
            
            // Trim trailing whitespace and split into lines
            string trimmedText = initialText.TrimEnd();
            List<string> lines = string.IsNullOrEmpty(trimmedText) 
               ? [""] 
               : trimmedText.Split('\n').ToList();
            
            int currentLine = lines.Count - 1;
            int cursorPosition = lines[currentLine].Length;
            int linesDisplayed = 0;
            int lastCursorLine = 0;

            // Undo/Redo stacks
            Stack<EditState> undoStack = new();
            Stack<EditState> redoStack = new();
            const int maxUndoLevels = 100;

            // Save initial state
            SaveState(undoStack, lines, currentLine, cursorPosition, maxUndoLevels);

            RedrawAllLines(lines, currentLine, cursorPosition, ref linesDisplayed, ref lastCursorLine);

            while (true) {
               ConsoleKeyInfo keyInfo = Console.ReadKey(intercept: true);

               if (keyInfo.Key == ConsoleKey.F1) {
                  // Show help message
                  ShowEditModeHelp(lines, currentLine, cursorPosition, ref linesDisplayed, ref lastCursorLine, savedForeground, savedBackground);
               } else if (keyInfo.Key == ConsoleKey.Z && keyInfo.Modifiers == ConsoleModifiers.Control) {
                  // Ctrl-Z: Undo
                  if (undoStack.Count > 1) { // Keep at least the initial state
                     EditState currentState = new(lines, currentLine, cursorPosition);
                     redoStack.Push(currentState);
                     if (redoStack.Count > maxUndoLevels) {
                        // Remove oldest redo entry
                        Stack<EditState> temp = new(redoStack.Reverse().Skip(1));
                        redoStack.Clear();
                        foreach (EditState state in temp.Reverse()) redoStack.Push(state);
                     }
                     
                     undoStack.Pop(); // Remove current state
                     EditState prevState = undoStack.Peek();
                     lines = prevState.Lines.Select(s => s).ToList(); // Deep copy
                     currentLine = prevState.CurrentLine;
                     cursorPosition = prevState.CursorPosition;
                     RedrawAllLines(lines, currentLine, cursorPosition, ref linesDisplayed, ref lastCursorLine);
                  }
               } else if (keyInfo.Key == ConsoleKey.Y && keyInfo.Modifiers == ConsoleModifiers.Control) {
                  // Ctrl-Y: Redo
                  if (redoStack.Count > 0) {
                     SaveState(undoStack, lines, currentLine, cursorPosition, maxUndoLevels);
                     EditState nextState = redoStack.Pop();
                     lines = nextState.Lines.Select(s => s).ToList(); // Deep copy
                     currentLine = nextState.CurrentLine;
                     cursorPosition = nextState.CursorPosition;
                     RedrawAllLines(lines, currentLine, cursorPosition, ref linesDisplayed, ref lastCursorLine);
                  }
               } else if (keyInfo.Key == ConsoleKey.Enter && keyInfo.Modifiers == ConsoleModifiers.Control) {
                  // Ctrl+Enter: Submit regardless of cursor position if text ends with period
                  string fullText = string.Join("\n", lines).TrimEnd();
                  if (fullText.Length > 0 && fullText[^1] == '.') {
                     ClearEditAreaWithHeader(linesDisplayed, lastCursorLine, savedForeground, savedBackground);
                     return string.Join("\n", lines);
                  }
               } else if (keyInfo.Key == ConsoleKey.Enter) {
                  // Only terminate if on last line, at end, and ends with period
                  if (IsAtTerminationPoint(lines, currentLine, cursorPosition)) {
                     // Clear the edit area and move to next line
                     ClearEditAreaWithHeader(linesDisplayed, lastCursorLine, savedForeground, savedBackground);
                     return string.Join("\n", lines);
                  } else {
                     // Insert new line
                     string currentLineText = lines[currentLine];
                     lines[currentLine] = currentLineText[..cursorPosition];
                     lines.Insert(currentLine + 1, currentLineText[cursorPosition..]);
                     currentLine++;
                     cursorPosition = 0;
                     SaveState(undoStack, lines, currentLine, cursorPosition, maxUndoLevels);
                     redoStack.Clear(); // Clear redo stack on new change
                     RedrawAllLines(lines, currentLine, cursorPosition, ref linesDisplayed, ref lastCursorLine);
                  }
               } else if (keyInfo.Key == ConsoleKey.Escape) {
                  // Clear the edit area and show cancellation message
                  ClearEditAreaWithHeader(linesDisplayed, lastCursorLine, savedForeground, savedBackground);
                  Console.WriteLine("[Editing cancelled]");
                  return null;
               } else if (keyInfo.Key == ConsoleKey.UpArrow) {
                  if (currentLine > 0) {
                     currentLine--;
                     cursorPosition = Math.Min(cursorPosition, lines[currentLine].Length);
                     RedrawAllLines(lines, currentLine, cursorPosition, ref linesDisplayed, ref lastCursorLine);
                  }
               } else if (keyInfo.Key == ConsoleKey.DownArrow) {
                  if (currentLine < lines.Count - 1) {
                     currentLine++;
                     cursorPosition = Math.Min(cursorPosition, lines[currentLine].Length);
                     RedrawAllLines(lines, currentLine, cursorPosition, ref linesDisplayed, ref lastCursorLine);
                  }
               } else if (keyInfo.Key == ConsoleKey.LeftArrow) {
                  if (cursorPosition > 0) {
                     cursorPosition--;
                     RedrawAllLines(lines, currentLine, cursorPosition, ref linesDisplayed, ref lastCursorLine);
                  } else if (currentLine > 0) {
                     currentLine--;
                     cursorPosition = lines[currentLine].Length;
                     RedrawAllLines(lines, currentLine, cursorPosition, ref linesDisplayed, ref lastCursorLine);
                  }
               } else if (keyInfo.Key == ConsoleKey.RightArrow) {
                  if (cursorPosition < lines[currentLine].Length) {
                     cursorPosition++;
                     RedrawAllLines(lines, currentLine, cursorPosition, ref linesDisplayed, ref lastCursorLine);
                  } else if (currentLine < lines.Count - 1) {
                     currentLine++;
                     cursorPosition = 0;
                     RedrawAllLines(lines, currentLine, cursorPosition, ref linesDisplayed, ref lastCursorLine);
                  }
               } else if (keyInfo.Key == ConsoleKey.Home) {
                  cursorPosition = 0;
                  RedrawAllLines(lines, currentLine, cursorPosition, ref linesDisplayed, ref lastCursorLine);
               } else if (keyInfo.Key == ConsoleKey.End) {
                  cursorPosition = lines[currentLine].Length;
                  RedrawAllLines(lines, currentLine, cursorPosition, ref linesDisplayed, ref lastCursorLine);
               } else if (keyInfo.Key == ConsoleKey.Backspace) {
                  if (cursorPosition > 0) {
                     lines[currentLine] = lines[currentLine].Remove(cursorPosition - 1, 1);
                     cursorPosition--;
                     SaveState(undoStack, lines, currentLine, cursorPosition, maxUndoLevels);
                     redoStack.Clear(); // Clear redo stack on new change
                     RedrawAllLines(lines, currentLine, cursorPosition, ref linesDisplayed, ref lastCursorLine);
                  } else if (currentLine > 0) {
                     // Merge with previous line
                     string mergedLine = lines[currentLine - 1] + lines[currentLine];
                     cursorPosition = lines[currentLine - 1].Length;
                     lines.RemoveAt(currentLine);
                     currentLine--;
                     lines[currentLine] = mergedLine;
                     SaveState(undoStack, lines, currentLine, cursorPosition, maxUndoLevels);
                     redoStack.Clear(); // Clear redo stack on new change
                     RedrawAllLines(lines, currentLine, cursorPosition, ref linesDisplayed, ref lastCursorLine);
                  }
               } else if (keyInfo.Key == ConsoleKey.Delete) {
                  if (cursorPosition < lines[currentLine].Length) {
                     lines[currentLine] = lines[currentLine].Remove(cursorPosition, 1);
                     SaveState(undoStack, lines, currentLine, cursorPosition, maxUndoLevels);
                     redoStack.Clear(); // Clear redo stack on new change
                     RedrawAllLines(lines, currentLine, cursorPosition, ref linesDisplayed, ref lastCursorLine);
                  } else if (currentLine < lines.Count - 1) {
                     // Merge with next line
                     lines[currentLine] += lines[currentLine + 1];
                     lines.RemoveAt(currentLine + 1);
                     SaveState(undoStack, lines, currentLine, cursorPosition, maxUndoLevels);
                     redoStack.Clear(); // Clear redo stack on new change
                     RedrawAllLines(lines, currentLine, cursorPosition, ref linesDisplayed, ref lastCursorLine);
                  }
               } else if (!char.IsControl(keyInfo.KeyChar)) {
                  lines[currentLine] = lines[currentLine].Insert(cursorPosition, keyInfo.KeyChar.ToString());
                  cursorPosition++;
                  SaveState(undoStack, lines, currentLine, cursorPosition, maxUndoLevels);
                  redoStack.Clear(); // Clear redo stack on new change
                  RedrawAllLines(lines, currentLine, cursorPosition, ref linesDisplayed, ref lastCursorLine);
               }
            }
         } finally {
            // Restore original console colors
            Console.ForegroundColor = savedForeground;
            Console.BackgroundColor = savedBackground;
         }
      }

      /// <summary>
      /// Clear the console screen
      /// </summary>
      private void ClearConsole() {
         if (_statusLineEnabled) {
            // Move to line 2 (first line of scrolling region)
            Console.Write("\x1b[2;1H");

            // Clear from cursor to end of screen
            Console.Write("\x1b[J");
         } else {
            // Use ANSI clear screen sequence
            Console.Write("\x1b[2J\x1b[H");
         }
      }

      /// <summary>
      /// Show help message in edit mode
      /// </summary>
      private void ShowEditModeHelp(List<string> lines,int currentLine,int cursorPosition,
                                     ref int linesDisplayed,ref int lastCursorLine,
                                     ConsoleColor savedForeground,ConsoleColor savedBackground) {
         // Switch to alternate screen buffer
         Console.Write("\x1b[?1049h");

         // Clear alternate screen and move to top
         Console.Write("\x1b[2J\x1b[H");

         // Display help
         WriteLine("\n" + _editModeHelp,Severity.Info);
         WriteLine("\nPress any key to continue editing...");

         // Wait for key press
         Console.ReadKey(intercept: true);

         // Switch back to main screen buffer (restores everything exactly as it was)
         Console.Write("\x1b[?1049l");
      }

      /// <summary>
      /// Clear the edit area including header line and reset cursor position
      /// </summary>
      private static void ClearEditAreaWithHeader(int linesDisplayed, int lastCursorLine, ConsoleColor foreground, ConsoleColor background) {
         // First restore the original colors BEFORE clearing
         Console.ForegroundColor = foreground;
         Console.BackgroundColor = background;
         
         // Move cursor back to first edit line
         if (lastCursorLine > 0) {
            Console.Write($"\x1b[{lastCursorLine}A");
         }
         Console.Write("\r");
         
         // Clear all edit lines
         for (int i = 0; i < linesDisplayed; i++) {
            Console.Write("\x1b[2K"); // Clear line
            if (i < linesDisplayed - 1) {
               Console.WriteLine();
               Console.Write("\r");
            }
         }
         
         // Move back to first edit line
         if (linesDisplayed > 1) {
            Console.Write($"\x1b[{linesDisplayed - 1}A");
         }
         Console.Write("\r");
         
         // Move up one more line to the header and clear it
         Console.Write("\x1b[A\r\x1b[2K");
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
      /// Redraw all lines in the multi-line editor with syntax-based background color using ANSI codes
      /// </summary>
      private static void RedrawAllLines(List<string> lines, int currentLine, int cursorPosition, ref int linesDisplayed, ref int lastCursorLine) {
         int windowWidth = Console.WindowWidth;
         
         // Verify syntax and set background color accordingly
         bool syntaxValid = VerifySyntax(lines);
         Console.BackgroundColor = syntaxValid ? ConsoleColor.White : ConsoleColor.Yellow;
         Console.ForegroundColor = ConsoleColor.Black;
         
         // Move cursor back to the first line of the edit area
         // The cursor is currently at lastCursorLine, so move up that many lines
         if (linesDisplayed > 0 && lastCursorLine > 0) {
            Console.Write($"\x1b[{lastCursorLine}A");
         }
         Console.Write("\r"); // Move to start of line
         
         // Determine how many lines to display (max of current and previous)
         int linesToDisplay = Math.Max(lines.Count, linesDisplayed);
         
         // Write all lines
         for (int i = 0; i < linesToDisplay; i++) {
            Console.Write("\x1b[2K"); // Clear entire line
            if (i < lines.Count) {
               Console.Write(": " + lines[i]);
            }
            if (i < linesToDisplay - 1) {
               Console.WriteLine();
               Console.Write("\r"); // Ensure we're at start of new line
            }
         }
         
         // Update the number of lines displayed
         linesDisplayed = lines.Count;
         
         // Position cursor at the correct line and column
         // We're at the end of line (linesToDisplay - 1), move up to currentLine
         if (currentLine < linesToDisplay - 1) {
            int linesToMoveUp = linesToDisplay - 1 - currentLine;
            Console.Write($"\x1b[{linesToMoveUp}A");
         }
         Console.Write($"\r\x1b[{2 + cursorPosition}C"); // Move to column position
         
         // Remember where we left the cursor for next time
         lastCursorLine = currentLine;
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

      /// <summary>
      /// Save the current state for undo/redo functionality
      /// </summary>
      private record EditState(List<string> Lines, int CurrentLine, int CursorPosition) {
         public List<string> Lines { get; init; } = Lines.Select(s => s).ToList(); // Deep copy
      }

      /// <summary>
      /// Save the current state to the undo stack
      /// </summary>
      private static void SaveState(Stack<EditState> stack, List<string> lines, int currentLine, int cursorPosition, int maxLevels) {
         EditState state = new(lines, currentLine, cursorPosition);
         stack.Push(state);
         if (stack.Count > maxLevels) {
            // Remove oldest entry
            Stack<EditState> temp = new(stack.Reverse().Skip(1));
            stack.Clear();
            foreach (EditState s in temp.Reverse()) stack.Push(s);
         }
      }
   }
}