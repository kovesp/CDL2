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
      private int _terminalWidth = 0;

      // Scrollback buffer
      private readonly List<OutputLine> _scrollbackBuffer = [];
      private const int MaxScrollbackLines = 1000;
      private int _scrollOffset = 0;
      private bool _inScrollMode = false;

      private const string _editModeHelp = """
Editing keys

Key | Action
---
Enter       | Insert a new line, but submit when caret
            | is at end and line ends with period ('.').
Ctrl-Enter  | Submit the CDL2 construct (last line must end with '.').
Esc         | Cancel editing.
← → ↑ ↓     | Navigate within the text. With Shift, select text.
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

      private const string _scrollModeHelp = """
Scroll Mode Navigation

Key         | Action
---
↑           | Scroll up one line
↓           | Scroll down one line
PgUp        | Scroll up one page
PgDn        | Scroll down one page
Home        | Jump to oldest line in buffer
End         | Jump to newest line (exit scroll mode)
Esc         | Exit scroll mode
Mouse Wheel | Scroll up/down

Press Ctrl+B to enter scroll mode at any time.
""";
      private const string _inputModeHelp = """
Input Mode Navigation and Commands

Key         | Action
---
Enter       | Execute the current command
Tab         | Name completion (if multiple matches, shows menu)
Esc         | Clear the input line
↑           | Previous command in history
↓           | Next command in history
←           | Move cursor left
→           | Move cursor right
Home        | Move to beginning of line
End         | Move to end of line
Backspace   | Delete character before cursor
Delete      | Delete character at cursor
Ctrl+B      | Enter scroll mode to review output history
Alt+Delete  | Clear the console screen
F1          | Show this help message

Tab Completion Menu (when shown):
  ↑/↓       | Navigate items
  Tab       | Cycle through items
  PgUp/PgDn | Navigate by page
  Home/End  | Jump to first/last item
  Enter     | Select current item
  Space     | Select current item
  Esc       | Cancel menu
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
         Settings.LoadCommandHistory(this);

         // Set up status line and scrolling region
         SetupStatusLine();

         WriteLine($"\n{CDL2.LabName} v{CDL2.Version}");
         WriteLine("Type 'help' for available commands, 'exit' or 'quit' to exit.",severity: Severity.Info);
         WriteLine("Press Ctrl+B to enter scroll mode to review output history.",severity: Severity.Info);

         while (_isRunning) {
            HandleConsoleResize();
            
            string prompt = GetPrompt(false);
            string? line = ReadLineWithHistory(prompt,true);

            if (line == null) break;

            InputType inputType = TokenList.ClassifyInput(line,out string trimmed,out string firstWord);

            switch (inputType) {
               case InputType.Empty:
                  continue;

               case InputType.CommandComment:
                  // Command comments are ignored - just continue to next prompt
                  continue;

               case InputType.CDL2Comment:
                  // CDL2 comments are passed through as-is
                  inputProcessor!(trimmed);
                  break;

               case InputType.Command:
                  _commandHistory.Add(trimmed);
                  inputProcessor!(trimmed);
                  break;

               case InputType.CDL2Construct:
                  // Expand abbreviation if needed
                  SelectorType type = Abbreviation<SelectorType>.Identify(firstWord.ToUpper());
                  string expandedInput = $"{type} {trimmed[firstWord.Length..]}";

                  if (!trimmed.EndsWith('.')) {
                     // Start of multiline code snippet - enter edit mode
                     string? editedText = ReadMultilineText(expandedInput);
                     if (editedText != null) {
                        inputProcessor!(editedText);
                     }
                  } else {
                     // Single line complete CDL2 construct
                     inputProcessor!(expandedInput);
                  }
                  break;

               case InputType.Invalid:
                  WriteLine($"Invalid input: '{firstWord}' is not a recognized command or CDL2 reserved word.",Severity.Error);
                  break;
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
         string outputText = text;
         
         // If severity is set and text has no ANSI codes, wrap with 24-bit RGB ANSI color codes
         // Look up colors from PrettyPrinter.Decorators to keep all styling in one place
         if (severity != Severity.NONE && !text.Contains("\x1b[")) {
            SE syntaxElement = severity switch {
               Severity.Error => SE.NoteError,
               Severity.Warning => SE.NoteWarning,
               Severity.Info => SE.NoteInfo,
               Severity.Note => SE.Comment,
               _ => SE.Other
            };
            
            string hexColor = PrettyPrinter.Decorators[syntaxElement].FG;
            (int r, int g, int b) = ParseHexColor(hexColor);
            
            string sev = severity == Severity.NONE ? "" : $"{severity}: ";
            outputText = $"\x1b[38;2;{r};{g};{b}m{sev}{text}\x1b[0m";
         }

         // Add to scrollback buffer WITH ANSI codes intact
         AddToScrollback(outputText,severity);

         // Display if not in scroll mode
         if (!_inScrollMode) {
            Console.WriteLine(outputText);
         }
      }

      /// <summary>
      /// Parse hex color string (#RRGGBB) to RGB values
      /// </summary>
      private static (int r, int g, int b) ParseHexColor(string hexColor) {
         if (string.IsNullOrEmpty(hexColor) || !hexColor.StartsWith("#") || hexColor.Length != 7) 
            return (255, 255, 255);

         try {
            string hex = hexColor.TrimStart('#');
            int r = Convert.ToInt32(hex.Substring(0,2),16);
            int g = Convert.ToInt32(hex.Substring(2,2),16);
            int b = Convert.ToInt32(hex.Substring(4,2),16);
            return (r, g, b);
         } catch {
            return (255, 255, 255);
         }
      }

      /// <summary>
      /// Display a query box and get user response
      /// </summary>
      public bool QueryBox(string message) {
         // Display the prompt through WriteLine so it goes to scrollback
         WriteLine($"{message} (y/n): ",Severity.Info);
         string? response = Console.ReadLine();
         // Add the response to scrollback
         if (response != null) WriteLine($"  Response: {response}");
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
            string marker = programName.IsNotNullEmptyOrWhitespace && (Database.Instance.ProgramByName(programName)?.Modified ?? false) ? "*" : "";
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
            int spacesNeeded = Math.Max(1,width - leftLen - rightLen);

            // Write left part, spaces, then right part
            string statusLine = (message + new string(' ',spacesNeeded) + rightStatus);

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
               Console.Title = $"{CDL2.LabName} - {Settings.LabDBPath}";
            } catch {
               // Ignore if console title cannot be set
            }
         }
      }

      /////////////////////
      // Local functions //
      /////////////////////

      /// <summary>
      /// Record for storing output lines in scrollback buffer
      /// </summary>
      private record OutputLine(string Text);

      /// <summary>
      /// Add a line to the scrollback buffer
      /// </summary>
      private void AddToScrollback(string text,Severity severity = Severity.NONE) {
         // Split by newlines to handle text with embedded \n characters
         string[] lines = text.Split('\n');
         
         foreach (string line in lines) {
            _scrollbackBuffer.Add(new OutputLine(line));
            if (_scrollbackBuffer.Count > MaxScrollbackLines) _scrollbackBuffer.RemoveAt(0);
         }
      }

      /// <summary>
      /// Enter scroll mode to review output history
      /// </summary>
      private void EnterScrollMode() {
         if (_scrollbackBuffer.Count == 0) return;

         _inScrollMode = true;
         _scrollOffset = Math.Max(0,_scrollbackBuffer.Count - (_terminalHeight - 2));

         RedrawScrollView();

         while (_inScrollMode) {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.End) {
               ExitScrollMode();
            } else if (key.Key == ConsoleKey.UpArrow) {
               ScrollUp(1);
            } else if (key.Key == ConsoleKey.DownArrow) {
               ScrollDown(1);
            } else if (key.Key == ConsoleKey.PageUp) {
               ScrollUp(_terminalHeight - 2);
            } else if (key.Key == ConsoleKey.PageDown) {
               ScrollDown(_terminalHeight - 2);
            } else if (key.Key == ConsoleKey.Home) {
               _scrollOffset = 0;
               RedrawScrollView();
            } else if (key.Key == ConsoleKey.F1) {
               ShowScrollModeHelp();
            }
         }
      }

      /// <summary>
      /// Exit scroll mode and return to normal output
      /// </summary>
      private void ExitScrollMode() {
         _inScrollMode = false;
         _scrollOffset = 0;
         Console.Write("\x1b[2;1H\x1b[J"); // Clear scrolling region
         RedisplayRecentOutput();
      }

      /// <summary>
      /// Scroll up by the specified number of lines
      /// </summary>
      private void ScrollUp(int lines) {
         int oldOffset = _scrollOffset;
         _scrollOffset = Math.Max(0,_scrollOffset - lines);
         
         // Only redraw if we actually moved
         if (_scrollOffset != oldOffset) {
            RedrawScrollView();
         }
      }

      /// <summary>
      /// Scroll down by the specified number of lines
      /// </summary>
      private void ScrollDown(int lines) {
         int oldOffset = _scrollOffset;
         int maxOffset = Math.Max(0,_scrollbackBuffer.Count - (_terminalHeight - 2));
         _scrollOffset = Math.Min(maxOffset,_scrollOffset + lines);
   
         // Only redraw if we actually moved
         if (_scrollOffset != oldOffset) {
            RedrawScrollView();
         }
      }

      /// <summary>
      /// Redraw the scrollable view
      /// </summary>
      private void RedrawScrollView() {
         int visibleLines = _terminalHeight - 2;
         int endLine = Math.Min(_scrollOffset + visibleLines,_scrollbackBuffer.Count);

         // Draw each visible line
         for (int i = _scrollOffset ; i < endLine ; i++) {
            OutputLine line = _scrollbackBuffer[i];
            int screenLine = 2 + (i - _scrollOffset); // Screen line number (2-based because line 1 is status)
            
            // Move to specific line and clear it
            Console.Write($"\x1b[{screenLine};1H\x1b[2K");
            // Write the content (no WriteLine to avoid unexpected line advance)
            Console.Write(line.Text);
         }

         // Clear any remaining lines below the content
         for (int i = endLine - _scrollOffset ; i < visibleLines ; i++) {
            int screenLine = 2 + i;
            Console.Write($"\x1b[{screenLine};1H\x1b[2K");
         }

         // Update status to show scroll position
         int totalLines = _scrollbackBuffer.Count;
         int currentLine = _scrollOffset + 1;
         int endDisplayLine = Math.Min(_scrollOffset + visibleLines,totalLines);
         SetStatus($"Scroll Mode: Lines {currentLine}-{endDisplayLine} of {totalLines} (Esc to exit, F1 for help)");
      }

      /// <summary>
      /// Show help for scroll mode
      /// </summary>
      private void ShowScrollModeHelp() {
         // Switch to alternate screen buffer
         Console.Write("\x1b[?1049h");

         // Clear alternate screen and move to top
         Console.Write("\x1b[2J\x1b[H");

         // Display help directly to alternate screen (not through WriteLine)
         Console.ForegroundColor = ConsoleColor.Cyan;
         Console.WriteLine("\n" + _scrollModeHelp);
         Console.WriteLine("\nPress any key to return to scroll mode...");
         Console.ResetColor();

         // Wait for key press
         Console.ReadKey(intercept: true);

         // Switch back to main screen buffer
         Console.Write("\x1b[?1049l");

         RedrawScrollView();
      }

      /// <summary>
      /// Show help for input mode
      /// </summary>
      private void ShowInputModeHelp() {
         // Switch to alternate screen buffer
         Console.Write("\x1b[?1049h");

         // Clear alternate screen and move to top
         Console.Write("\x1b[2J\x1b[H");

         // Display help directly to alternate screen (not through WriteLine)
         Console.ForegroundColor = ConsoleColor.Cyan;
         Console.WriteLine("\n" + _inputModeHelp);
         Console.WriteLine("\nPress any key to return to input...");
         Console.ResetColor();

         // Wait for key press
         Console.ReadKey(intercept: true);

         // Switch back to main screen buffer
         Console.Write("\x1b[?1049l");
      }

      /// <summary>
      /// Setup status line with scrolling region
      /// </summary>
      private void SetupStatusLine() {
         try {
            EnableAnsiSupport();
            
            // Set console encoding to UTF-8 for proper Unicode display
            try {
               Console.OutputEncoding = System.Text.Encoding.UTF8;
            } catch {
               // Ignore if encoding cannot be set
            }
            
            // Set initial console size (Windows only)
#if WINDOWS
            try {
               // Set a reasonable default size (e.g., 120 columns x 30 rows)
               int desiredWidth = 120;
               int desiredHeight = 60;

               // Ensure buffer is large enough before setting window size
               if (OperatingSystem.IsWindows()) Console.SetBufferSize(
                  Math.Max(desiredWidth, Console.BufferWidth),
                  Math.Max(MaxScrollbackLines + 100, desiredHeight)
               );
               
               // Now set the window size
               if (OperatingSystem.IsWindows()) Console.SetWindowSize(desiredWidth, desiredHeight);
            } catch {
               // Ignore if resizing fails (may not be supported in all environments)
            }
#endif
            
            _terminalHeight = Console.WindowHeight;
            _terminalWidth = Console.WindowWidth;

            ReconfigureScrollingRegion();
            _statusLineEnabled = true;
            SetStatus("Nothing");
         } catch {
            _statusLineEnabled = false;
         }
      }

      /// <summary>
      /// Enable ANSI escape sequence support in Windows
      /// </summary>
      private static void EnableAnsiSupport() {
#if WINDOWS
         try {
            IntPtr handle = GetStdHandle(-11); // STD_OUTPUT_HANDLE
            if (GetConsoleMode(handle,out uint mode)) {
               mode |= 0x0004; // ENABLE_VIRTUAL_TERMINAL_PROCESSING
               SetConsoleMode(handle,mode);
            }
         } catch {
            // Ignore if ANSI support cannot be enabled
         }
#endif
      }

      #region Windows Console API for ANSI support
#if WINDOWS
      [System.Runtime.InteropServices.DllImport("kernel32.dll",SetLastError = true)]
      private static extern IntPtr GetStdHandle(int nStdHandle);

      [System.Runtime.InteropServices.DllImport("kernel32.dll")]
      private static extern bool GetConsoleMode(IntPtr hConsoleHandle,out uint lpMode);

      [System.Runtime.InteropServices.DllImport("kernel32.dll")]
      private static extern bool SetConsoleMode(IntPtr hConsoleHandle,uint dwMode);
#endif
      #endregion

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
         return $"\x1b[93m{fqdn}\x1b[0m> ";
      }

      /// <summary>
      /// Calculate the visual length of a string, excluding ANSI escape codes
      /// </summary>
      private static int GetVisualLength(string text) {
         // Remove ANSI escape sequences (pattern: ESC [ ... m)
         string withoutAnsi = Regex.Replace(text,@"\x1b\[[0-9;]*m","");
         return withoutAnsi.Length;
      }

      /// <summary>
      /// Read a line of input with history navigation support
      /// </summary>
      private string? ReadLineWithHistory(string prompt,bool enableHistory) {
         Console.Write(prompt);

         if (!enableHistory) return Console.ReadLine();

         int promptVisualLength = GetVisualLength(prompt);
         List<char> buffer = [];
         int cursorPosition = 0;

         while (true) {
            ConsoleKeyInfo keyInfo = Console.ReadKey(intercept: true);
            

            if (keyInfo.Key == ConsoleKey.Enter) {
               Console.WriteLine();
               string input = new string(buffer.ToArray());               
               // Add user input to scrollback with prompt (already a single line)
               if (input.Length > 0) {
                  _scrollbackBuffer.Add(new OutputLine($"{prompt}{input}"));
                  if (_scrollbackBuffer.Count > MaxScrollbackLines) _scrollbackBuffer.RemoveAt(0);
               }               
               return input;
            } else if (keyInfo.Key == ConsoleKey.Tab) {
               // Tab: Name completion
               string currentText = new([.. buffer]);

               if (NameCompletion.GetCommandCompletions(currentText,out CompletionResult? result)) {
                  ApplyCompletions(currentText,result);
               } else if (NameCompletion.GetSettingCompletions(currentText,out result)) {
                  ApplyCompletions(currentText,result);
               } else if (NameCompletion.GetSelectorCompletions(currentText,out result)) {
                  ApplyCompletions(currentText,result);
               }
            } else if (keyInfo.Key == ConsoleKey.Escape) {
               // Esc: Clear the input line
               buffer.Clear();
               cursorPosition = 0;
               RedrawLine(buffer,cursorPosition,prompt,promptVisualLength);
            } else if (keyInfo.Key == ConsoleKey.F1) {
               // F1: Show help
               ShowInputModeHelp();
            } else if (keyInfo.Key == ConsoleKey.B && keyInfo.Modifiers == ConsoleModifiers.Control) {
               // Ctrl+B: Enter scroll mode
               Console.WriteLine();
               EnterScrollMode();
               // After exiting scroll mode, redraw prompt and buffer
               Console.Write(prompt);
               Console.Write(new string(buffer.ToArray()));
               Console.SetCursorPosition(promptVisualLength + cursorPosition,Console.CursorTop);
            } else if (keyInfo.Key == ConsoleKey.Delete && keyInfo.Modifiers == ConsoleModifiers.Alt) {
               // Alt+Delete: Clear the console
               ClearConsole();
               // Redraw the prompt and current buffer
               Console.Write(prompt);
               Console.Write(new string(buffer.ToArray()));
               Console.SetCursorPosition(promptVisualLength + cursorPosition,Console.CursorTop);
            } else if (keyInfo.Key == ConsoleKey.UpArrow) {
               string? previous = _commandHistory.Previous();
               if (previous != null) {
                  ReplaceBuffer(buffer,previous,ref cursorPosition,prompt,promptVisualLength);
               }
            } else if (keyInfo.Key == ConsoleKey.DownArrow) {
               string? next = _commandHistory.Next();
               if (next != null) {
                  ReplaceBuffer(buffer,next,ref cursorPosition,prompt,promptVisualLength);
               }
            } else if (keyInfo.Key == ConsoleKey.Backspace) {
               if (cursorPosition > 0) {
                  buffer.RemoveAt(cursorPosition - 1);
                  cursorPosition--;
                  RedrawLine(buffer,cursorPosition,prompt,promptVisualLength);
               }
            } else if (keyInfo.Key == ConsoleKey.Delete) {
               if (cursorPosition < buffer.Count) {
                  buffer.RemoveAt(cursorPosition);
                  RedrawLine(buffer,cursorPosition,prompt,promptVisualLength);
               }
            } else if (keyInfo.Key == ConsoleKey.LeftArrow) {
               if (cursorPosition > 0) {
                  cursorPosition--;
                  Console.SetCursorPosition(promptVisualLength + cursorPosition,Console.CursorTop);
               }
            } else if (keyInfo.Key == ConsoleKey.RightArrow) {
               if (cursorPosition < buffer.Count) {
                  cursorPosition++;
                  Console.SetCursorPosition(promptVisualLength + cursorPosition,Console.CursorTop);
               }
            } else if (keyInfo.Key == ConsoleKey.Home) {
               cursorPosition = 0;
               Console.SetCursorPosition(promptVisualLength,Console.CursorTop);
            } else if (keyInfo.Key == ConsoleKey.End) {
               cursorPosition = buffer.Count;
               Console.SetCursorPosition(promptVisualLength + cursorPosition,Console.CursorTop);
            } else if (!char.IsControl(keyInfo.KeyChar)) {
               buffer.Insert(cursorPosition,keyInfo.KeyChar);
               cursorPosition++;
               RedrawLine(buffer,cursorPosition,prompt,promptVisualLength);
            }
         }

         void ApplyCompletions(string currentText,CompletionResult result) {
            if (result.Completions.Length == 1) {
               // Single match: apply directly
               string replacement = result.Completions[0];
               string newText = currentText[..result.StartPosition] + replacement;
               buffer.Clear();
               buffer.AddRange(newText);
               cursorPosition = buffer.Count;
               RedrawLine(buffer,cursorPosition,prompt,promptVisualLength);
            } else {
               // Multiple matches: show menu
               string? selected = ShowCompletionMenu(result.Completions,buffer,cursorPosition,prompt,promptVisualLength);
               if (selected != null) {
                  string newText = currentText[..result.StartPosition] + selected;
                  buffer.Clear();
                  buffer.AddRange(newText);
                  cursorPosition = buffer.Count;
                  RedrawLine(buffer,cursorPosition,prompt,promptVisualLength);
               } else {
                  // Menu was cancelled, just redraw the line
                  RedrawLine(buffer,cursorPosition,prompt,promptVisualLength);
               }
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

            // Display header line (directly, not through WriteLine - this is temporary UI)
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
            SaveState(undoStack,lines,currentLine,cursorPosition,maxUndoLevels);

            RedrawAllLines(lines,currentLine,cursorPosition,ref linesDisplayed,ref lastCursorLine);

            while (true) {
               ConsoleKeyInfo keyInfo = Console.ReadKey(intercept: true);

               if (keyInfo.Key == ConsoleKey.F1) {
                  // Show help message
                  ShowEditModeHelp(lines,currentLine,cursorPosition,ref linesDisplayed,ref lastCursorLine,savedForeground,savedBackground);
               } else if (keyInfo.Key == ConsoleKey.Z && keyInfo.Modifiers == ConsoleModifiers.Control) {
                  // Ctrl-Z: Undo
                  if (undoStack.Count > 1) { // Keep at least the initial state
                     EditState currentState = new(lines,currentLine,cursorPosition);
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
                     RedrawAllLines(lines,currentLine,cursorPosition,ref linesDisplayed,ref lastCursorLine);
                  }
               } else if (keyInfo.Key == ConsoleKey.Y && keyInfo.Modifiers == ConsoleModifiers.Control) {
                  // Ctrl-Y: Redo
                  if (redoStack.Count > 0) {
                     SaveState(undoStack,lines,currentLine,cursorPosition,maxUndoLevels);
                     EditState nextState = redoStack.Pop();
                     lines = nextState.Lines.Select(s => s).ToList(); // Deep copy
                     currentLine = nextState.CurrentLine;
                     cursorPosition = nextState.CursorPosition;
                     RedrawAllLines(lines,currentLine,cursorPosition,ref linesDisplayed,ref lastCursorLine);
                  }
               } else if (keyInfo.Key == ConsoleKey.Enter && keyInfo.Modifiers == ConsoleModifiers.Control) {
                  // Ctrl+Enter: Submit regardless of cursor position if text ends with period
                  string fullText = string.Join("\n",lines).TrimEnd();
                  if (fullText.Length > 0 && fullText[^1] == '.') {
                     ClearEditAreaWithHeader(linesDisplayed,lastCursorLine,savedForeground,savedBackground);
                     return string.Join("\n",lines);
                  }
               } else if (keyInfo.Key == ConsoleKey.Enter) {
                  // Only terminate if on last line, at end, and ends with period
                  if (IsAtTerminationPoint(lines,currentLine,cursorPosition)) {
                     // Clear the edit area and move to next line
                     ClearEditAreaWithHeader(linesDisplayed,lastCursorLine,savedForeground,savedBackground);
                     return string.Join("\n",lines);
                  } else {
                     // Insert new line
                     string currentLineText = lines[currentLine];
                     lines[currentLine] = currentLineText[..cursorPosition];
                     lines.Insert(currentLine + 1,currentLineText[cursorPosition..]);
                     currentLine++;
                     cursorPosition = 0;
                     SaveState(undoStack,lines,currentLine,cursorPosition,maxUndoLevels);
                     redoStack.Clear(); // Clear redo stack on new change
                     RedrawAllLines(lines,currentLine,cursorPosition,ref linesDisplayed,ref lastCursorLine);
                  }
               } else if (keyInfo.Key == ConsoleKey.Escape) {
                  // Clear the edit area and show cancellation message
                  ClearEditAreaWithHeader(linesDisplayed,lastCursorLine,savedForeground,savedBackground);
                  Console.WriteLine("[Editing cancelled]");
                  return null;
               } else if (keyInfo.Key == ConsoleKey.UpArrow) {
                  if (currentLine > 0) {
                     currentLine--;
                     cursorPosition = Math.Min(cursorPosition,lines[currentLine].Length);
                     RedrawAllLines(lines,currentLine,cursorPosition,ref linesDisplayed,ref lastCursorLine);
                  }
               } else if (keyInfo.Key == ConsoleKey.DownArrow) {
                  if (currentLine < lines.Count - 1) {
                     currentLine++;
                     cursorPosition = Math.Min(cursorPosition,lines[currentLine].Length);
                     RedrawAllLines(lines,currentLine,cursorPosition,ref linesDisplayed,ref lastCursorLine);
                  }
               } else if (keyInfo.Key == ConsoleKey.LeftArrow) {
                  if (cursorPosition > 0) {
                     cursorPosition--;
                     RedrawAllLines(lines,currentLine,cursorPosition,ref linesDisplayed,ref lastCursorLine);
                  } else if (currentLine > 0) {
                     // Merge with previous line
                     currentLine--;
                     cursorPosition = lines[currentLine].Length;
                     string mergedLine = lines[currentLine] + lines[currentLine + 1];
                     lines[currentLine] = mergedLine;
                     lines.RemoveAt(currentLine + 1);
                     SaveState(undoStack,lines,currentLine,cursorPosition,maxUndoLevels);
                     redoStack.Clear();
                     RedrawAllLines(lines,currentLine,cursorPosition,ref linesDisplayed,ref lastCursorLine);
                  }
               } else if (keyInfo.Key == ConsoleKey.RightArrow) {
                  if (cursorPosition < lines[currentLine].Length) {
                     cursorPosition++;
                     RedrawAllLines(lines,currentLine,cursorPosition,ref linesDisplayed,ref lastCursorLine);
                  } else if (currentLine < lines.Count - 1) {
                     currentLine++;
                     cursorPosition = 0;
                     RedrawAllLines(lines,currentLine,cursorPosition,ref linesDisplayed,ref lastCursorLine);
                  }
               } else if (keyInfo.Key == ConsoleKey.Home) {
                  cursorPosition = 0;
                  RedrawAllLines(lines,currentLine,cursorPosition,ref linesDisplayed,ref lastCursorLine);
               } else if (keyInfo.Key == ConsoleKey.End) {
                  cursorPosition = lines[currentLine].Length;
                  RedrawAllLines(lines,currentLine,cursorPosition,ref linesDisplayed,ref lastCursorLine);
               } else if (keyInfo.Key == ConsoleKey.Backspace) {
                  if (cursorPosition > 0) {
                     lines[currentLine] = lines[currentLine].Remove(cursorPosition - 1,1);
                     cursorPosition--;
                     SaveState(undoStack,lines,currentLine,cursorPosition,maxUndoLevels);
                     redoStack.Clear();
                     RedrawAllLines(lines,currentLine,cursorPosition,ref linesDisplayed,ref lastCursorLine);
                  } else if (currentLine > 0) {
                     // Merge with previous line
                     string mergedLine = lines[currentLine - 1] + lines[currentLine];
                     cursorPosition = lines[currentLine - 1].Length;
                     lines.RemoveAt(currentLine);
                     currentLine--;
                     lines[currentLine] = mergedLine;
                     SaveState(undoStack,lines,currentLine,cursorPosition,maxUndoLevels);
                     redoStack.Clear();
                     RedrawAllLines(lines,currentLine,cursorPosition,ref linesDisplayed,ref lastCursorLine);
                  }
               } else if (keyInfo.Key == ConsoleKey.Delete) {
                  if (cursorPosition < lines[currentLine].Length) {
                     lines[currentLine] = lines[currentLine].Remove(cursorPosition,1);
                     SaveState(undoStack,lines,currentLine,cursorPosition,maxUndoLevels);
                     redoStack.Clear();
                     RedrawAllLines(lines,currentLine,cursorPosition,ref linesDisplayed,ref lastCursorLine);
                  } else if (currentLine < lines.Count - 1) {
                     // Merge with next line
                     lines[currentLine] += lines[currentLine + 1];
                     lines.RemoveAt(currentLine + 1);
                     SaveState(undoStack,lines,currentLine,cursorPosition,maxUndoLevels);
                     redoStack.Clear();
                     RedrawAllLines(lines,currentLine,cursorPosition,ref linesDisplayed,ref lastCursorLine);
                  }
               } else if (!char.IsControl(keyInfo.KeyChar)) {
                  lines[currentLine] = lines[currentLine].Insert(cursorPosition,keyInfo.KeyChar.ToString());
                  cursorPosition++;
                  SaveState(undoStack,lines,currentLine,cursorPosition,maxUndoLevels);
                  redoStack.Clear();
                  RedrawAllLines(lines,currentLine,cursorPosition,ref linesDisplayed,ref lastCursorLine);
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
            // Clear scrollback buffer
            _scrollbackBuffer.Clear();
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
         // Display help directly to alternate screen
         Console.ForegroundColor = ConsoleColor.Cyan;
         Console.WriteLine("\n" + _editModeHelp);
         Console.WriteLine("\nPress any key to continue editing...");
         Console.ResetColor();
         // Wait for key press
         Console.ReadKey(intercept: true);
         // Switch back to main screen buffer
         Console.Write("\x1b[?1049l");
      }

      /// <summary>
      /// Clear the edit area including header line
      /// </summary>
      private static void ClearEditAreaWithHeader(int linesDisplayed,int lastCursorLine,ConsoleColor foreground,ConsoleColor background) {
         Console.ForegroundColor = foreground;
         Console.BackgroundColor = background;
         if (lastCursorLine > 0) Console.Write($"\x1b[{lastCursorLine}A");
         Console.Write("\r");
         for (int i = 0 ; i < linesDisplayed ; i++) {
            Console.Write("\x1b[2K");
            if (i < linesDisplayed - 1) {
               Console.WriteLine();
               Console.Write("\r");
            }
         }
         if (linesDisplayed > 1) Console.Write($"\x1b[{linesDisplayed - 1}A");
         Console.Write("\r");
         Console.Write("\x1b[A\r\x1b[2K");
      }

      /// <summary>
      /// Check if cursor is at termination point
      /// </summary>
      private static bool IsAtTerminationPoint(List<string> lines,int currentLine,int cursorPosition) {
         if (currentLine != lines.Count - 1) return false;
         if (cursorPosition != lines[currentLine].Length) return false;
         string fullText = string.Join("\n",lines).TrimEnd();
         return fullText.Length > 0 && fullText[^1] == '.';
      }

      /// <summary>
      /// Verify the syntax of the current text
      /// </summary>
      private static bool VerifySyntax(List<string> lines) {
         string text = string.Join("\n",lines);
         return Database.Instance.CLI?.VerifySyntax(text) ?? false;
      }

      /// <summary>
      /// Redraw all lines in the multi-line editor
      /// </summary>
      private static void RedrawAllLines(List<string> lines,int currentLine,int cursorPosition,ref int linesDisplayed,ref int lastCursorLine) {
         bool syntaxValid = VerifySyntax(lines);
         Console.BackgroundColor = syntaxValid ? ConsoleColor.White : ConsoleColor.Yellow;
         Console.ForegroundColor = ConsoleColor.Black;
         if (linesDisplayed > 0 && lastCursorLine > 0) Console.Write($"\x1b[{lastCursorLine}A");
         Console.Write("\r");
         int linesToDisplay = Math.Max(lines.Count,linesDisplayed);
         for (int i = 0 ; i < linesToDisplay ; i++) {
            Console.Write("\x1b[2K");
            if (i < lines.Count) Console.Write(": " + lines[i]);
            if (i < linesToDisplay - 1) {
               Console.WriteLine();
               Console.Write("\r");
            }
         }
         linesDisplayed = lines.Count;
         if (currentLine < linesToDisplay - 1) {
            int linesToMoveUp = linesToDisplay - 1 - currentLine;
            Console.Write($"\x1b[{linesToMoveUp}A");
         }
         Console.Write($"\r\x1b[{2 + cursorPosition}C");
         lastCursorLine = currentLine;
      }

      /// <summary>
      /// Replace the current buffer with history content
      /// </summary>
      private static void ReplaceBuffer(List<char> buffer,string text,ref int cursorPosition,string prompt,int promptVisualLength) {
         buffer.Clear();
         buffer.AddRange(text);
         cursorPosition = buffer.Count;
         RedrawLine(buffer,cursorPosition,prompt,promptVisualLength);
      }

      /// <summary>
      /// Redraw the current input line
      /// </summary>
      private static void RedrawLine(List<char> buffer,int cursorPosition,string prompt,int promptVisualLength) {
         int currentTop = Console.CursorTop;
         int windowWidth = Console.WindowWidth;
         Console.SetCursorPosition(0,currentTop);
         Console.Write(new string(' ',windowWidth - 1));
         Console.SetCursorPosition(0,currentTop);
         Console.Write(prompt + new string(buffer.ToArray()));
         Console.SetCursorPosition(promptVisualLength + cursorPosition,currentTop);
      }

      /// <summary>
      /// Save the current state for undo/redo functionality
      /// </summary>
      private record EditState(List<string> Lines,int CurrentLine,int CursorPosition) {
         public List<string> Lines { get; init; } = Lines.Select(s => s).ToList();
      }

      /// <summary>
      /// Save the current state to the undo stack
      /// </summary>
      private static void SaveState(Stack<EditState> stack,List<string> lines,int currentLine,int cursorPosition,int maxLevels) {
         EditState state = new(lines,currentLine,cursorPosition);
         stack.Push(state);
         if (stack.Count > maxLevels) {
            Stack<EditState> temp = new(stack.Reverse().Skip(1));
            stack.Clear();
            foreach (EditState s in temp.Reverse()) stack.Push(s);
         }
      }

      /// <summary>
      /// Check if console has been resized and update display if needed
      /// </summary>
      private void HandleConsoleResize() {
         if (!_statusLineEnabled) return;

         int currentHeight = Console.WindowHeight;
         int currentWidth = Console.WindowWidth;
         
         bool heightChanged = currentHeight != _terminalHeight;
         bool widthChanged = currentWidth != _terminalWidth;
         
         if (heightChanged || widthChanged) {
            _terminalHeight = currentHeight;
            _terminalWidth = currentWidth;
            
            try {
               if (heightChanged) {
                  // Full reconfigure needed when height changes
                  ReconfigureScrollingRegion();
                  RedisplayRecentOutput();
               } else if (widthChanged) {
                  // Only update status line when just width changes
                  string currentStatus = Focus.Current.Object?.FQDN() ?? "Nothing";
                  SetStatus(currentStatus);
               }
            } catch {
               _statusLineEnabled = false;
            }
         }
      }

      /// <summary>
      /// Configure the scrolling region and status line
      /// </summary>
      private void ReconfigureScrollingRegion() {
         Console.Write("\x1b[2J"); // Clear screen
         Console.Write("\x1b[1;1H"); // Move to line 1
         Console.ForegroundColor = ConsoleColor.Black;
         Console.BackgroundColor = ConsoleColor.Gray;
         Console.Write(new string(' ',Console.WindowWidth));
         Console.ResetColor();
         Console.Write($"\x1b[2;{_terminalHeight}r"); // Set scrolling region
         Console.Write("\x1b[2;1H"); // Move to line 2
      }

      /// <summary>
      /// Redisplay recent output from scrollback buffer
      /// </summary>
      private void RedisplayRecentOutput() {
         int startLine = Math.Max(0,_scrollbackBuffer.Count - (_terminalHeight - 2));
         for (int i = startLine ; i < _scrollbackBuffer.Count ; i++) {
            Console.WriteLine(_scrollbackBuffer[i].Text);
         }
         
         string currentStatus = Focus.Current.Object?.FQDN() ?? "Nothing";
         SetStatus(currentStatus);
      }

      /// <summary>
      /// Show a completion menu and return the selected item, or null if cancelled
      /// </summary>
      private string? ShowCompletionMenu(string[] completions,List<char> buffer,int cursorPosition,string prompt,int promptVisualLength) {
         // Save cursor position
         int savedCursorLeft = Console.CursorLeft;
         int savedCursorTop = Console.CursorTop;

         // Calculate menu dimensions
         int maxWidth = completions.Max(c => c.Length) + 4; // Add padding
         int menuWidth = Math.Max(maxWidth,20);
         int menuHeight = Math.Min(completions.Length + 2,15); // +2 for borders, max 15 lines
         bool needsScroll = completions.Length > (menuHeight - 2);

         int selectedIndex = 0;
         int scrollOffset = 0;
         int visibleItems = menuHeight - 2;

         // Save original colors
         ConsoleColor savedForeground = Console.ForegroundColor;
         ConsoleColor savedBackground = Console.BackgroundColor;

         try {
            while (true) {
               // Calculate menu position (below current line, or above if not enough space)
               int menuTop = savedCursorTop + 1;
               if (menuTop + menuHeight > Console.WindowHeight) {
                  menuTop = savedCursorTop - menuHeight;
                  if (menuTop < 0) menuTop = 0;
               }

               int menuLeft = Math.Max(0,Math.Min(savedCursorLeft,Console.WindowWidth - menuWidth));

               // Draw menu
               DrawCompletionMenu(completions,selectedIndex,scrollOffset,visibleItems,menuLeft,menuTop,menuWidth,menuHeight,needsScroll);

               // Wait for key input
               ConsoleKeyInfo key = Console.ReadKey(intercept: true);

               if (key.Key == ConsoleKey.Escape) {
                  // Cancel - clear menu and return null
                  ClearCompletionMenu(menuLeft,menuTop,menuWidth,menuHeight);
                  return null;
               } else if (key.Key == ConsoleKey.Enter || key.Key == ConsoleKey.Spacebar) {
                  // Select current item
                  ClearCompletionMenu(menuLeft,menuTop,menuWidth,menuHeight);
                  return completions[selectedIndex];
               } else if (key.Key == ConsoleKey.UpArrow) {
                  if (selectedIndex > 0) {
                     selectedIndex--;
                     if (selectedIndex < scrollOffset) scrollOffset = selectedIndex;
                  }
               } else if (key.Key == ConsoleKey.DownArrow || key.Key == ConsoleKey.Tab) {
                  if (selectedIndex < completions.Length - 1) {
                     selectedIndex++;
                     if (selectedIndex >= scrollOffset + visibleItems) scrollOffset = selectedIndex - visibleItems + 1;
                  }
               } else if (key.Key == ConsoleKey.PageUp) {
                  selectedIndex = Math.Max(0,selectedIndex - visibleItems);
                  scrollOffset = Math.Max(0,scrollOffset - visibleItems);
               } else if (key.Key == ConsoleKey.PageDown) {
                  selectedIndex = Math.Min(completions.Length - 1,selectedIndex + visibleItems);
                  scrollOffset = Math.Min(Math.Max(0,completions.Length - visibleItems),scrollOffset + visibleItems);
               } else if (key.Key == ConsoleKey.Home) {
                  selectedIndex = 0;
                  scrollOffset = 0;
               } else if (key.Key == ConsoleKey.End) {
                  selectedIndex = completions.Length - 1;
                  scrollOffset = Math.Max(0,completions.Length - visibleItems);
               }
            }
         } finally {
            // Restore cursor and colors
            Console.SetCursorPosition(savedCursorLeft,savedCursorTop);
            Console.ForegroundColor = savedForeground;
            Console.BackgroundColor = savedBackground;
         }
      }

      /// <summary>
      /// Draw the completion menu using box drawing characters
      /// </summary>
      private static void DrawCompletionMenu(string[] completions,int selectedIndex,int scrollOffset,int visibleItems,int menuLeft,int menuTop,int menuWidth,int menuHeight,bool needsScroll) {
         Console.ForegroundColor = ConsoleColor.White;
         Console.BackgroundColor = ConsoleColor.DarkGray;

         // Draw top border
         Console.SetCursorPosition(menuLeft,menuTop);
         Console.Write("┌" + new string('─',menuWidth - 2) + "┐");

         // Draw menu items
         int displayCount = Math.Min(visibleItems,completions.Length - scrollOffset);
         for (int i = 0 ; i < visibleItems ; i++) {
            Console.SetCursorPosition(menuLeft,menuTop + 1 + i);
            Console.Write("│");

            if (i < displayCount) {
               int itemIndex = scrollOffset + i;
               string item = completions[itemIndex];
               bool isSelected = itemIndex == selectedIndex;

               if (isSelected) {
                  Console.ForegroundColor = ConsoleColor.Black;
                  Console.BackgroundColor = ConsoleColor.Cyan;
               } else {
                  Console.ForegroundColor = ConsoleColor.White;
                  Console.BackgroundColor = ConsoleColor.DarkGray;
               }

               // Truncate or pad item to fit
               string displayItem = item.Length > menuWidth - 4 ? item[..(menuWidth - 7)] + "..." : item.PadRight(menuWidth - 4);
               Console.Write($" {displayItem} ");

               Console.ForegroundColor = ConsoleColor.White;
               Console.BackgroundColor = ConsoleColor.DarkGray;
            } else {
               Console.Write(new string(' ',menuWidth - 2));
            }

            Console.Write("│");
         }

         // Draw bottom border
         Console.SetCursorPosition(menuLeft,menuTop + menuHeight - 1);
         Console.Write("└" + new string('─',menuWidth - 2) + "┘");

         // Draw scroll indicators if needed
         if (needsScroll) {
            if (scrollOffset > 0) {
               Console.SetCursorPosition(menuLeft + menuWidth - 2,menuTop);
               Console.Write("▲");
            }
            if (scrollOffset + visibleItems < completions.Length) {
               Console.SetCursorPosition(menuLeft + menuWidth - 2,menuTop + menuHeight - 1);
               Console.Write("▼");
            }
         }
      }

      /// <summary>
      /// Clear the completion menu from the screen
      /// </summary>
      private static void ClearCompletionMenu(int menuLeft,int menuTop,int menuWidth,int menuHeight) {
         Console.ForegroundColor = ConsoleColor.Gray;
         Console.BackgroundColor = ConsoleColor.Black;

         for (int i = 0 ; i < menuHeight ; i++) {
            Console.SetCursorPosition(menuLeft,menuTop + i);
            Console.Write(new string(' ',menuWidth));
         }

         Console.ResetColor();
      }
   }
}
