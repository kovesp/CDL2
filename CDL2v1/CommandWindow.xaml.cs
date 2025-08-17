// <auto-gen>
//=======================================================================
// <copyright file="CommandWindow.xaml.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-06-10</creation-date>
// 
// <summary>
//   Implements the GUI logic (code-behind) for the CommandWindow.
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
//    This project is not affiliated with the original CDL2 project.
// </attribution>
//=======================================================================
// </auto-gen>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;


namespace CDL2v1 {
   /// <summary>
   /// Interaction logic for CommandWindow.xaml
   /// </summary>
   public partial class CommandPromptWindow : Window {
      private readonly History _commandHistory = new();
      private readonly FontFamily _textFont = new("Cascadia Mono");

      // Store the last height of the output area for restore functionality
      private double _lastOutputHeight = 0;

      // Event raised when a command is entered
      public event EventHandler<string>? CommandEntered;

      private bool _multilineMode = false;
      private class UndoEntry(TextBox input) {
         public string Text = input.Text;
         public int CaretIndex = input.CaretIndex;

         public void SetTextBox(TextBox input) {
            input.Text = Text;
            input.CaretIndex = CaretIndex;
         }
      }

      private const string _singleLineTooltip = "Enter a Lab command and press Enter. F1 for help.";
      private const string _editTooltip = "Edit the item and press Ctrl-Enter to submit. F1 for help.";
      private const string _multilineTooltip = "Enter CDL2 construct. Submit occurs when a line ends with a period ('.').";

      private const string _multilineModeHelp = """
Editing keys in multi-line mode.
Pressing Esc or clicking in this window dismisses it.

Key | Action
---
Enter       | Insert a new line.
Ctrl-Enter  | Submit the CDL2 construct (last line must end with '.').
Esc         | Cancel editing.
Ctrl-Z      | Undo last edit.
Ctrl-Y      | Redo last edit.
Ctrl-C      | Copy selection to clipboard.
Ctrl-X      | Cut selection to clipboard.
Ctrl-V      | Paste from clipboard.
Left-Dbl-Click | Select CDL2 identifier or reserved word.
Tab         | Insert indentation (based on indent width).
F1          | Show this help message.
""";
      private const string _singleLineModeHelp = """
Editing keys in single-line mode.
Pressing Esc or clicking in this window dismisses it.

Key | Action
---
Enter | Execute the command or
      | Enter a single line CDL2 object terminated by a period.
↑     | Previous command in history.
↓     | Next command in history.
F1    | Show this help message.
""";

      // Background brushes for input field
      private readonly Brush _standardInputBackground;
      private readonly Brush _standardInputForeground;
      private readonly Brush _multilineInputBackground = Brushes.White;
      private readonly Brush _multilineInputForeground = Brushes.Black;

      public Emitter? Emitter;   // Used to get the indent width

      public CommandPromptWindow() {
         InitializeComponent();

         // Set initial prompt
         WriteLine($"CDL2 Laboratory v{CDL2.Version} - Type 'help' for available commands");
         DisplayPrompt();

         // Apply saved window position and size
         ApplySavedWindowSettings();

         // Subscribe to events to save changes
         this.Closing += CommandPromptWindow_Closing;
         this.LocationChanged += CommandPromptWindow_LocationChanged;
         this.SizeChanged += CommandPromptWindow_SizeChanged;

         // Initialize the last output height
         _lastOutputHeight = OutputRow.ActualHeight;

         // Handle grid size changes to save last height
         MainGrid.SizeChanged += (s,e) => {
            if (OutputRow.ActualHeight > 0) {
               _lastOutputHeight = OutputRow.ActualHeight;
            }
         };

         // Focus on the window so it can receive keyboard input
         Loaded += (s,e) => {
            Keyboard.Focus(InputTextBox);
         };

         _standardInputBackground = InputTextBox.Background;
         _standardInputForeground = InputTextBox.Foreground;
         InputTextBox.ToolTip = _singleLineTooltip;
         // Store the original background at startup

         // Get the Info color from PrettyPrinter.Decorators
         var infoColorHex = PrettyPrinter.Decorators[SE.NoteInfo].FG;
         StatusBarTextBox.Foreground = (Brush)new BrushConverter().ConvertFromString(infoColorHex)!;
      }

      #region Window Settings
      private void ApplySavedWindowSettings() {
         // Get window settings
         double left = Settings.SettingValue<double>("WindowLeft");
         double top = Settings.SettingValue<double>("WindowTop");
         double width = Settings.SettingValue<double>("WindowWidth");
         double height = Settings.SettingValue<double>("WindowHeight");

         // Set window position
         if (left >= 0 && top >= 0) {
            this.WindowStartupLocation = WindowStartupLocation.Manual;
            this.Left = left;
            this.Top = top;
         } else {
            // Only center if we don't have saved position
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
         }

         // Apply size if valid
         if (width > 200 && height > 200) {
            this.Width = width;
            this.Height = height;
         }
      }

      private void SaveWindowSettings() {
         if (WindowState == WindowState.Normal) {
            Setting<double>? leftSetting = Settings.Setting<double>("WindowLeft");
            Setting<double>? topSetting = Settings.Setting<double>("WindowTop");
            Setting<double>? widthSetting = Settings.Setting<double>("WindowWidth");
            Setting<double>? heightSetting = Settings.Setting<double>("WindowHeight");

            leftSetting?.Value = this.Left;
            topSetting?.Value = this.Top;
            widthSetting?.Value = this.Width;
            heightSetting?.Value = this.Height;

            // Save to persistent storage
            Settings.SaveSettings();
         }
      }

      private void CommandPromptWindow_Closing(object? sender,System.ComponentModel.CancelEventArgs e) => SaveWindowSettings();

      private void CommandPromptWindow_LocationChanged(object? sender,EventArgs e) {
         if (IsLoaded && WindowState == WindowState.Normal)
            SaveWindowSettings();
      }

      private void CommandPromptWindow_SizeChanged(object? sender,SizeChangedEventArgs e) {
         if (IsLoaded && WindowState == WindowState.Normal)
            SaveWindowSettings();
      }
      #endregion

      #region Formatted Output
      /// <summary>
      /// Configure the output area for formatted text (called by CommandWindowEmitter)
      /// </summary>
      public void ConfigureFormattedOutput() {
         // TextBlock doesn't need special configuration
         OutputScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
      }

      // Cache to improve scrolling performance
      private bool _autoScrollEnabled = true;
      private bool _scrollInProgress = false;

      /// <summary>
      /// Add formatted text to the output area
      /// </summary>
      public void AddFormattedText(
          string text,
          Brush foreground,
          Brush background,
          FontWeight fontWeight = default,
          FontStyle fontStyle = default,
          TextDecorationCollection? textDecorations = null,
          bool lineBreak = false) {

         // Create a styled run
         Run run = new(text) {
            Foreground = foreground,
            Background = background,
            FontWeight = fontWeight != default ? fontWeight : FontWeights.Normal,
            FontStyle = fontStyle != default ? fontStyle : FontStyles.Normal,
            TextDecorations = textDecorations,
            FontFamily = _textFont
         };

         // Add the run to the TextBlock
         OutputTextBlock.Inlines.Add(run);

         if (lineBreak) {
            OutputTextBlock.Inlines.Add(new LineBreak());
         }

         // Ensure auto-scroll to bottom, but only if not in batch mode
         if (_autoScrollEnabled && !_scrollInProgress) {
            ScrollToEnd();
         }
      }

      private void ScrollToEnd() {
         if (_scrollInProgress)
            return;

         _scrollInProgress = true;

         // Use low priority dispatcher to avoid blocking the UI
         Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(() => {
            OutputScrollViewer.ScrollToBottom();
            _scrollInProgress = false;
         }));
      }

      /// <summary>
      /// Begin batch updating of formatted text
      /// </summary>
      public void BeginFormattedUpdate() {
         _autoScrollEnabled = false;
      }

      /// <summary>
      /// End batch updating of formatted text
      /// </summary>
      public void EndFormattedUpdate() {
         _autoScrollEnabled = true;
         ScrollToEnd();
      }

      /// <summary>
      /// Force UI update during batch operations
      /// </summary>
      public void UpdateFormattedUI() {
         OutputTextBlock.UpdateLayout();
      }
      #endregion

      #region Simple Text Output

      /// <summary>
      /// Writes a line of text to the output with optional note type formatting
      /// </summary>
      /// <param name="text">The text to write</param>
      /// <param name="severity">Optional note type that determines text color (default None)</param>
      public void WriteLine(string text,Severity severity = Severity.NONE) {
         Application.Current.Dispatcher.Invoke(() => {
            // Get the appropriate brush based on note type from PrettyPrinter.Decorators
            Brush foreground = GetNoteTypeBrush(severity);

            // Add formatted text with the appropriate color
            AddFormattedText(text,foreground,Brushes.Transparent,lineBreak: true);
         });
      }

      public void WriteError(string text) => WriteLine(text,Severity.Error);
      public void WriteWarning(string text) => WriteLine(text,Severity.Warning);
      public void WriteInfo(string text) => WriteLine(text,Severity.Info);

      /// <summary>
      /// Gets the brush color for a specific note type using colors from PrettyPrinter.Decorators
      /// </summary>
      /// <param name="noteType">The note type</param>
      /// <returns>Brush corresponding to the note type</returns>
      private static Brush GetNoteTypeBrush(Severity noteType) {
         // Map NoteType to corresponding SyntacticElement
         SE element = noteType switch {
            Severity.Error => SE.NoteError,
            Severity.Warning => SE.NoteWarning,
            Severity.Info => SE.NoteInfo,
            Severity.Note => SE.Comment,
            _ => SE.Other  // Default
         };

         // Get color from PrettyPrinter.Decorators
         string colorHex = PrettyPrinter.Decorators[element].FG;

         // Convert hex color to brush
         try {
            return (Brush)new BrushConverter().ConvertFromString(colorHex)!;
         } catch {
            return Brushes.LightGray; // Fallback if conversion fails
         }
      }
      #endregion

      #region Input Handling
      /// <summary>
      /// Displays the command prompt
      /// </summary>
      private void DisplayPrompt() {
         Application.Current.Dispatcher.Invoke(() => {
            if (!IsEditing) {
               PromptTextBlock.Text = "> ";
               InputTextBox.Clear();
               InputTextBox.ToolTip = _singleLineTooltip;
               InputTextBox.IsUndoEnabled = false;
               InputTextBox.Focus();
            }
         });
         IsEditing = false;
      }

      private bool IsEditing = false;
      public void EditText(string text) {
         Application.Current.Dispatcher.Invoke(() => {
            PromptTextBlock.Text = ": ";
            InputTextBox.Text = text.Trim();
            InputTextBox.Focus();
            IsEditing = true;
            SwitchToMultiline(_editTooltip);
         });
      }

      /// <summary>
      /// Clear the output area
      /// </summary>
      private void ClearOutput_Click(object sender,RoutedEventArgs e) {
         Application.Current.Dispatcher.Invoke(() => {
            // Clear all content from the TextBlock
            OutputTextBlock.Inlines.Clear();

            // Add initial message
            WriteLine($"CDL2 Laboratory v{CDL2.Version} - Output cleared");

            // Focus back on input box
            InputTextBox.Focus();
         });
      }

      /// <summary>
      /// Handle input text box key down events
      /// </summary>
      private void InputTextBox_PreviewKeyDown(object sender,KeyEventArgs e) {
         /// <summary>
         /// Insert text at the current caret position and move the caret after the inserted text.
         /// </summary>
         void Insert(string chars) {
            int index = InputTextBox.CaretIndex;
            InputTextBox.Text = InputTextBox.Text.Insert(index,chars);
            InputTextBox.CaretIndex = index + chars.Length;
         }
         /// <summary>
         /// Inserts a newline with the same indentation as the current line.
         /// If the first non-whitespace character is an open parenthesis, add an extra space for that.
         /// </summary>
         void InsertNewlineWithIndent() {
            string caretLine = InputTextBox.GetLineText(InputTextBox.GetLineIndexFromCharacterIndex(InputTextBox.CaretIndex));
            int leadingSpaces = Math.Max(0,caretLine.FindIndex(c => !char.IsWhiteSpace(c)));
            if (caretLine[leadingSpaces] == '(') leadingSpaces++;
            Insert(Environment.NewLine + new string(' ',leadingSpaces));
         }

         e.Handled = false; // Default is to let non handled and other keys pass through
         if (e.Key == Key.Enter) {
            string input = InputTextBox.Text;
            bool enterModifierPressed = e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control);

            if (_multilineMode) {
               // Terminat multiline mode either when Ctrl-enter is pressed or
               // when enter is pressed with the caret at the end of input just after a period.
               if (input.EndsWith('.') && (input.Length == InputTextBox.CaretIndex || enterModifierPressed)) {
                  SwitchToSingleLine();
                  ExecuteCommand();
                  e.Handled = true;
               } else {
                  InsertNewlineWithIndent();
                  e.Handled = true;
               }
            } else {
               // Check whether we have a command or CDL2 object
               string trimmed = input.Trim();
               bool isCommand = char.IsAsciiLetterLower(trimmed[0]);
               string firstWord = trimmed.Split(' ','\t','\r','\n')[0];
               if (!isCommand) {
                  SelectorType type = Abbreviation<SelectorType>.Identify(firstWord.ToUpper());
                  if (type != SelectorType.INVALID) {
                     InputTextBox.Text = $"{type} {input[firstWord.Length..]}";
                     InputTextBox.CaretIndex = InputTextBox.Text.Length;
                     if (trimmed.EndsWith('.')) { // Valid single line construct (e.g., Var v.)
                        ExecuteCommand();
                        e.Handled = true;
                     } else { // CDL2 object that was not completed on a single line
                        SwitchToMultiline();
                        InsertNewlineWithIndent();
                        e.Handled = true;
                     }
                  } else { // Invalid selector, so supply error message.
                     WriteError($"Attempt to enter a CDL2 construct with non-existent reserved word: {firstWord}");
                     DisplayPrompt();
                     e.Handled = true;
                  }
               } else { // Must be a command
                  ExecuteCommand();
                  e.Handled = true;
               }
            }
         } else if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None) {
            string toastMessage = _multilineModeHelp;
            if (!_multilineMode) {
               toastMessage = _singleLineModeHelp;
               string trimmed = InputTextBox.Text.Trim();
               if (trimmed.Length == 0 || char.IsAsciiLetterLower(trimmed[0])) {
                  string firstWord = trimmed.Split(' ','\t','\r','\n')[0];
                  string commandHelp = Abbreviation<CommandType>.LongHelp(firstWord,toastFormat: true);
                  if (commandHelp.IsNotEmptyOrWhitespace()) toastMessage += $"\n\nCommand|Parameters|Description\n---\n{commandHelp}";
               }
            }
            ToastWindow.ShowToast(toastMessage);
            e.Handled = true;
         } else if (e.Key == Key.Escape) {
            SwitchToSingleLine();
            DisplayPrompt();
            IsEditing = false;
            e.Handled = true;
         } else if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.None) {
            // 0 1 2 -> 3 2 1
            int spacesToInsert = Emitter!.IndentWidth - (Math.Min(InputTextBox.CaretIndex - 1,0)) % Emitter!.IndentWidth;
            Insert(new string(' ',spacesToInsert));
            e.Handled = true;
         } else if (e.Key == Key.Up) {
            if (!_multilineMode) {
               string? history = _commandHistory.Previous();
               if (history is null) {
                  FlashInputError();
               } else {
                  InputTextBox.Text = history;
                  InputTextBox.CaretIndex = history.Length;
               }
               e.Handled = true;
            }
         } else if (e.Key == Key.Down) {
            if (!_multilineMode) {
               string? history = _commandHistory.Next();
               if (history is null) {
                  FlashInputError();
               } else {
                  InputTextBox.Text = history;
                  InputTextBox.CaretIndex = history.Length;
               }
               e.Handled = true;
            }
         } else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control) {
            if (_multilineMode && !InputTextBox.CanUndo) {
               FlashInputError();
               e.Handled = true;
            }
         } else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control) {
            if (_multilineMode && !InputTextBox.CanRedo) {
               FlashInputError();
               e.Handled = true;
            }
         }

      }

      private void ClearUndoRedo(TextBox box) {
         bool undoState = box.IsUndoEnabled;
         box.IsUndoEnabled = false;
         box.IsUndoEnabled = undoState;
      }

      private void SwitchToSingleLine() {
         _multilineMode = false;
         InputTextBox.ToolTip = _singleLineTooltip;
         InputTextBox.Background = _standardInputBackground;
         InputTextBox.Foreground = InputTextBox.CaretBrush = _standardInputForeground;
         InputTextBox.IsUndoEnabled = false;
      }

      private void SwitchToMultiline(string tip = _multilineTooltip) {
         _multilineMode = true;
         InputTextBox.ToolTip = tip;
         InputTextBox.Background = _multilineInputBackground;
         InputTextBox.Foreground = InputTextBox.CaretBrush = _multilineInputForeground;
         ClearUndoRedo(InputTextBox);
         InputTextBox.IsUndoEnabled = true;
      }

      /// <summary>
      /// Executes the current command
      /// </summary>
      private void ExecuteCommand() {
         string command = InputTextBox.Text.Trim();
         // Add command to history
         if (char.IsAsciiLetterLower(command[0])) {
            // It's a command, so add it to history and echo it.
            _commandHistory.Add(command);
            // Echo command
            WriteLine($"> {command}");
         }

         if (command.IsNotEmptyOrWhitespace()) {
            // Raise event to handle the command
            CommandEntered?.Invoke(this,command);
         }

         // Display new prompt
         DisplayPrompt();
      }
      #endregion

      #region UI Controls
      /// <summary>
      /// Maximize/minimize the output area
      /// </summary>
      private void ToggleOutputArea_Click(object sender,RoutedEventArgs e) {
         if (OutputRow.Height.Value > 0) {
            // Collapse output area
            _lastOutputHeight = OutputRow.Height.Value;
            OutputRow.Height = new GridLength(0);
         } else {
            // Restore output area
            OutputRow.Height = new GridLength(_lastOutputHeight > 0 ? _lastOutputHeight : 1,GridUnitType.Star);
         }

         InputTextBox.Focus();
      }

      /// <summary>
      /// Handle mouse wheel zoom with Ctrl key
      /// </summary>
      private void OutputTextBlock_PreviewMouseWheel(object sender,MouseWheelEventArgs e) {
         if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) {
            if (e.Delta > 0) {
               ZoomIn(10);
            } else {
               ZoomOut(10);
            }
            e.Handled = true;
         }
      }

      /// <summary>
      /// Zoom in button click handler
      /// </summary>
      private void ZoomIn_Click(object sender,RoutedEventArgs e) {
         ZoomIn(20);
      }

      /// <summary>
      /// Zoom out button click handler
      /// </summary>
      private void ZoomOut_Click(object sender,RoutedEventArgs e) {
         ZoomOut(20);
      }

      /// <summary>
      /// Increase font size
      /// </summary>
      private void ZoomIn(int percentage = 20) {
         OutputTextBlock.FontSize *= (100 + percentage) / 100.0;
      }

      /// <summary>
      /// Decrease font size
      /// </summary>
      private void ZoomOut(int percentage = 20) {
         OutputTextBlock.FontSize /= (100 + percentage) / 100.0;
      }

      /// <summary>
      /// Handle mouse wheel zoom with Ctrl key on the ScrollViewer
      /// </summary>
      private void OutputScrollViewer_PreviewMouseWheel(object sender,MouseWheelEventArgs e) {
         if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) {
            if (e.Delta > 0) {
               ZoomIn(10);
            } else {
               ZoomOut(10);
            }
            e.Handled = true;
         }
      }

      /// <summary>
      /// In multiline mode, handl;e double-click.
      /// </summary>
      /// <param name="sender"></param>
      /// <param name="e"></param>
      private void InputTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
      {
         e.Handled = false;
         if (e.ClickCount != 2 && e.ClickCount != 3) return;
         if (sender is not TextBox textBox) return;
         if (!_multilineMode) return;

         // Get mouse position and character index
         Point mousePos = e.GetPosition(textBox);
         int charIndex = textBox.GetCharacterIndexFromPoint(mousePos, true);
         if (charIndex < 0 || charIndex >= textBox.Text.Length) return;

         string text = textBox.Text;
         string[] lines = text.Split('\n');
         int lineIndex = textBox.GetLineIndexFromCharacterIndex(charIndex);
         string line = textBox.GetLineText(lineIndex);
         int lineStart = textBox.GetCharacterIndexFromLineIndex(lineIndex);
         int posInLine = charIndex - lineStart;

         // --- BLOCK SELECTION LOGIC: comments/whitespace/Note ---
         static bool IsCommentLine(string l) => l.TrimStart().StartsWith('#');
         static bool IsNoteLine(string l)
         {
            var trimmed = l.TrimStart();
            if (trimmed.Length < 4) return false;
            if (!char.IsUpper(trimmed[0])) return false;
            if (trimmed.StartsWith("Note", StringComparison.OrdinalIgnoreCase))
            {
               if (trimmed.Length == 4 ||
                   char.IsWhiteSpace(trimmed[4]) ||
                   char.IsPunctuation(trimmed[4]))
                  return true;
            }
            return false;
         }

         if (IsCommentLine(lines[lineIndex]) || string.IsNullOrWhiteSpace(lines[lineIndex]) || IsNoteLine(lines[lineIndex]))
         {
            int first = lineIndex, last = lineIndex;
            int totalLines = lines.Length;

            // Expand upwards: include all contiguous comment or whitespace lines
            for (int i = lineIndex - 1; i >= 0; i--)
            {
               if (string.IsNullOrWhiteSpace(lines[i]) || IsCommentLine(lines[i]))
                  first = i;
               else
                  break;
            }

            // Expand downwards: include all contiguous comment or whitespace lines
            for (int i = lineIndex + 1; i < totalLines; i++)
            {
               if (string.IsNullOrWhiteSpace(lines[i]) || IsCommentLine(lines[i]))
                  last = i;
               else
                  break;
            }

            // If the next line after the block is a NOTE line, include it
            if (last + 1 < totalLines && IsNoteLine(lines[last + 1]))
               last = last + 1;

            int selStart = textBox.GetCharacterIndexFromLineIndex(first);
            int selEnd = textBox.GetCharacterIndexFromLineIndex(last) + lines[last].Length;
            textBox.Select(selStart, selEnd - selStart);
            e.Handled = true;
            return;
         }

         // --- Existing selection logic below ---

         int start = charIndex;
         int end = charIndex;

         // Robust comment detection: any region between odd/even # is a comment
         int hashCount = 0;
         for (int i = 0; i < posInLine; i++)
         {
            if (line[i] == '#') hashCount++;
         }
         if ((hashCount % 2) == 1)
         {
            // Click is inside a comment, do not handle
            return;
         }

         switch (e.ClickCount)
         {
            case 2:
               if (!char.IsAsciiLetter(text[start])) return;
               ExpandSelection(GetValidCharPredicate(text, start));
               break;
            case 3:
               // Find the first ':' or ':=' in the line
               int colonIdx = line.IndexOf(':');
               int assignIdx = line.IndexOf(":=");
               int delimiterIdx = -1;
               if (assignIdx >= 0)
               {
                  delimiterIdx = assignIdx;
               }
               else if (colonIdx >= 0)
               {
                  delimiterIdx = colonIdx;
               }

               // If triple-click is in the header (before ':' or ':='), act like double-click
               if (delimiterIdx >= 0 && posInLine < delimiterIdx)
               {
                  // Double-click logic: select identifier/word
                  if (!char.IsAsciiLetter(text[start])) return;
                  ExpandSelection(GetValidCharPredicate(text, start));
                  break;
               }

               // --- Otherwise, use call selection logic as before ---
               int i = charIndex;
               bool inString = false;
               while (i > 0)
               {
                  char c = text[i - 1];
                  if (c == '"')
                  {
                     int dollarCount = 0;
                     int j = i - 2;
                     while (j >= 0 && text[j] == '$') { dollarCount++; j--; }
                     if (dollarCount % 2 == 0)
                        inString = !inString;
                  }
                  if (!inString)
                  {
                     int k = i - 1;
                     // Only skip spaces, not newlines
                     while (k >= 0 && text[k] == ' ') k--;

                     // Accept delimiter, open paren, or newline as valid call start
                     if (k < 0) { i = 0; break; }
                     if (k > 0 && text[k - 1] == ':' && text[k] == '=')
                     {
                        i = k + 1;
                        break;
                     }
                     if (text[k] == ':' || text[k] == ';' || text[k] == ',' || text[k] == '(' ||
                         text[k] == '\n' || text[k] == '\r')
                     {
                        i = k + 1;
                        break;
                     }

                     // Accept capitalized word as call boundary
                     int wordEnd = k;
                     while (wordEnd >= 0 && char.IsLetter(text[wordEnd])) wordEnd--;
                     int wordStart = wordEnd + 1;
                     if (wordStart <= k && char.IsUpper(text[wordStart]))
                     {
                        i = k + 1;
                        break;
                     }
                  }
                  i--;
               }
               start = i;
               // Skip spaces after delimiter, paren, or start of line/text
               while (start < text.Length && text[start] == ' ') start++;
               // If open paren, skip it and any spaces after it
               if (start < text.Length && text[start] == '(')
               {
                  start++;
                  while (start < text.Length && text[start] == ' ') start++;
               }
               // Require lowercase letter at start
               if (start >= text.Length || !char.IsAsciiLetterLower(text[start])) return;

               // --- Find call end ---
               i = charIndex;
               inString = false;
               while (i < text.Length)
               {
                  char c = text[i];
                  if (c == '"')
                  {
                     int dollarCount = 0;
                     int j = i - 1;
                     while (j >= 0 && text[j] == '$') { dollarCount++; j--; }
                     if (dollarCount % 2 == 0)
                        inString = !inString;
                  }
                  if (!inString)
                  {
                     if (c == ',' || c == ';' || c == '.')
                        break;
                  }
                  i++;
               }
               end = i - 1;
               // Trim trailing whitespace and all close parens
               while (end > start && char.IsWhiteSpace(text[end])) end--;
               while (end >= start && text[end] == ')') end--;
               while (end > start && char.IsWhiteSpace(text[end])) end--;
               break;
            default:
               return;
         }

         textBox.Select(start, end - start + 1);
         e.Handled = true;

         // Helper: get valid char predicate for identifier
         static Predicate<char> GetValidCharPredicate(string text, int idx) =>
             char.IsAsciiLetterUpper(text[idx])
                 ? char.IsAsciiLetterUpper
                 : c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == ' ';

         // Helper: expand selection left/right for identifier
         bool ExpandSelection(Predicate<char> validChar)
         {
            int startStart = start;
            int endStart = end;
            // Expand to the left
            while (start > 0 && validChar(text[start - 1])) start--;
            while (!char.IsAsciiLetter(text[start]) && start < end) start++; // Skip leading non-letter characters
            // Expand to right
            while (end < text.Length - 1 && validChar(text[end + 1])) end++;
            while (end > start && text[end] == ' ') end--; // Skip trailing spaces
            return start != startStart || end != endStart;
         }
      }
      #endregion

      #region Command History
      /// <summary>
      /// </summary>
      /// Command history manager
      private class History {
         private readonly List<string> _history = [];
         private int _currentIndex = -1;

         public void Add(string command) {
            _history.Add(command);
            _currentIndex = _history.Count;
         }

         public string? Previous() {
            if (_history.Count == 0 || _currentIndex == 0) return null;

            _currentIndex = Math.Max(0,_currentIndex - 1);
            return _currentIndex < _history.Count ? _history[_currentIndex] : "";
         }

         public string? Next() {
            if (_history.Count == 0 || _currentIndex >= _history.Count) return null;

            _currentIndex = Math.Min(_history.Count,_currentIndex + 1);
            return _currentIndex < _history.Count ? _history[_currentIndex] : "";
         }
      }
      #endregion

      public void SetStatus(string message) => StatusBarTextBox.Text = message;

      // Call this method to flash the InputTextBox as an error
      private void FlashInputError() {
         if (InputTextBox.Background is SolidColorBrush solidBrush) {
            SolidColorBrush animBrush;
            if (solidBrush.IsFrozen) {
               animBrush = solidBrush.Clone();
               InputTextBox.Background = animBrush;
            } else {
               animBrush = solidBrush;
            }

            Color originalColor = animBrush.Color;

            ColorAnimation animation = new ColorAnimation {
               To = Colors.Red,
               Duration = TimeSpan.FromMilliseconds(100),
               AutoReverse = true,
               RepeatBehavior = new RepeatBehavior(2)
            };

            animation.Completed += (s,e) => {
               animBrush.Color = originalColor;
            };

            animBrush.BeginAnimation(SolidColorBrush.ColorProperty,animation);
         } else {
            Brush originalBrush = InputTextBox.Background;
            InputTextBox.Background = new SolidColorBrush(Colors.Red);

            DispatcherTimer timer = new DispatcherTimer {
               Interval = TimeSpan.FromMilliseconds(400)
            };
            timer.Tick += (s,e) => {
               InputTextBox.Background = originalBrush;
               ((DispatcherTimer)s).Stop();
            };
            timer.Start();
         }
      }
   }
}
