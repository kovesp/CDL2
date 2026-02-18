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
#if WINDOWS
using System.Configuration;
using System.Text.RegularExpressions;
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
   public partial class CommandWindow : Window, ICLIREPL {
      private readonly FontFamily _textFont = new("Cascadia Mono");

      // Store the last height of the output area for restore functionality
      private double _lastOutputHeight = 0;
      private double _initialStatusBarHeight = 0;  // Add this

      // Event raised when a command is entered
      public event EventHandler<string>? CommandEntered;

      private readonly History _commandHistory = new();
      public IEnumerable<string> CommandHistory {
         get => _commandHistory.Commands;
         set => _commandHistory.Commands = value;
      }

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
Enter       | Insert a new line, but submit when caret
            | is just after the terminating period.
Ctrl-Enter  | Submit the CDL2 construct (last line must end with '.').
Esc         | Cancel editing.
Ctrl-Z      | Undo last edit.
Ctrl-Y      | Redo last edit.
Ctrl-C      | Copy selection to clipboard.
Ctrl-X      | Cut selection to clipboard.
Ctrl-V      | Paste from clipboard.
L-D-Click   | Select CDL2 identifier, reserved word
            | or block of comments and notes.
L-T-Click   | Select the call clicked in a procedure body.
Tab         | Insert indentation (based on indent width).
F1          | Show this help message.

(L: Left, R: Right, D: Double, T: Triple)
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
Tab   | Complete command, setting, or selector name (if possible).
F1    | Show this help message.

Editing keys in completion menu (when visible):
Key   | Action
---
↑/↓   | Navigate items
Tab   | Cycle through items
Enter | Select current item
Space | Select current item
Esc   | Cancel menu
""";

      // Background brushes for input field
      private readonly Brush _standardInputBackground;
      private readonly Brush _standardInputForeground;
      private readonly Brush _multilineInputBackground = Brushes.White;
      private readonly Brush _multilineInputForeground = Brushes.Black;
      private readonly Brush _multilineInputErrorBackground = Brushes.Moccasin;

      public Emitter? Emitter { get; set; }
      public IToaster Toaster { get; init; }

      public CommandWindow(IToaster toaster) {
         this.Toaster = toaster;

         Settings.LoadCommandHistory(this);

         InitializeComponent();

         Title = CDL2.LabName;

         // Set initial prompt
         WriteLine($"{CDL2.LabName} v{CDL2.Version} - Type 'help' for available commands");
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
            // Initialize zoom restrictions AFTER layout is complete
            InitializeZoomRestrictions();

            // Store initial status bar height
            _initialStatusBarHeight = StatusBarGrid.ActualHeight;

            Keyboard.Focus(InputTextBox);
         };

         _standardInputBackground = InputTextBox.Background;
         _standardInputForeground = InputTextBox.Foreground;
         InputTextBox.ToolTip = _singleLineTooltip;

         // Get the Info color from PrettyPrinter.Decorators
         var infoColorHex = PrettyPrinter.Decorators[SE.NoteInfo].FG;
         StatusBarLeftTextBlock.Foreground = (Brush)new BrushConverter().ConvertFromString(infoColorHex)!;
         StatusBarRightTextBlock.Foreground = (Brush)new BrushConverter().ConvertFromString(infoColorHex)!;
      }

      #region Open and Close
      /// <summary>
      /// Start the REPL loop. This is automatic in WPF, so this is a no-op.
      /// </summary>
      public void Open() { }

      public void SetInputProcessor(Action<string> processor) => CommandEntered += (sender,input) => processor(input);

      /// <summary>
      /// The underlying Window already has a Close() method, no need to override it.
      /// </summary>

      #endregion Open and Close

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
            Settings.SaveSettings(this);
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
      public void ConfigureFormattedOutput() => OutputScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;

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
      public void BeginFormattedUpdate() => _autoScrollEnabled = false;

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
      public void UpdateFormattedUI() => OutputTextBlock.UpdateLayout();
      #endregion

      #region Simple Text Output

      /// <summary>
      /// Writes a line of text to the output with optional note type formatting
      /// </summary>
      /// <param name="text">The text to write</param>
      /// <param name="severity">Optional note type that determines text color (default None)</param>
      public void WriteLine(string text,Severity severity = Severity.NONE) => Application.Current.Dispatcher.Invoke(() => {
         // Get the appropriate brush based on note type from PrettyPrinter.Decorators
         Brush foreground = GetNoteTypeBrush(severity);

         // Add formatted text with the appropriate color
         AddFormattedText(text,foreground,Brushes.Transparent,lineBreak: true);
      });

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
      public void EditText(string text = "") => Application.Current.Dispatcher.Invoke(() => {
         PromptTextBlock.Text = ": ";
         InputTextBox.Text = text.Trim();
         InputTextBox.Focus();
         IsEditing = true;
         SwitchToMultiline(_editTooltip);
      });

      /// <summary>
      /// Clear the output area
      /// </summary>
      private void ClearOutput_Click(object sender,RoutedEventArgs e) => Application.Current.Dispatcher.Invoke(() => {
         // Clear all content from the TextBlock
         OutputTextBlock.Inlines.Clear();

         // Add initial message
         WriteLine($"{CDL2.LabName} v{CDL2.Version} - Output cleared");

         // Focus back on input box
         InputTextBox.Focus();
      });

      // Completion tracking
      private int _completionStartPos = 0;
      private ContextMenu? _activeCompletionMenu = null;
      private int _selectedCompletionIndex = 0;
      private string[]? _currentCompletions = null;

      /// <summary>
      /// Shows a menu of completions at the current caret position.
      /// When a completion is selected, it replaces the text from the start position to the caret with the selected completion.
      /// </summary>
      /// <param name="completions"></param>
      /// <param name="startPos"></param>
      private void ShowCompletionMenu(IEnumerable<string> completions,int startPos) {
         _completionStartPos = startPos;
         _currentCompletions = completions.OrderBy(c => c).ToArray();
         _selectedCompletionIndex = 0;

         // Get colors from PrettyPrinter
         string bgColorHex = PrettyPrinter.Decorators[SE.Other].BG;
         string fgColorHex = PrettyPrinter.Decorators[SE.Other].FG;
         string highlightBgHex = PrettyPrinter.Decorators[SE.NoteInfo].BG;
         string highlightFgHex = PrettyPrinter.Decorators[SE.NoteInfo].FG;

         Brush bgBrush = (Brush)new BrushConverter().ConvertFromString(bgColorHex)!;
         Brush fgBrush = (Brush)new BrushConverter().ConvertFromString(fgColorHex)!;
         Brush highlightBgBrush = (Brush)new BrushConverter().ConvertFromString(highlightBgHex)!;
         Brush highlightFgBrush = (Brush)new BrushConverter().ConvertFromString(highlightFgHex)!;

         _activeCompletionMenu = new ContextMenu {
            PlacementTarget = this,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Absolute,
            Background = bgBrush,
            Foreground = fgBrush,
            BorderBrush = fgBrush,
            BorderThickness = new Thickness(1),
            StaysOpen = false
         };

         // Handle keyboard navigation on the menu itself
         _activeCompletionMenu.PreviewKeyDown += (s,e) => {
            if (e.Key == Key.Down || e.Key == Key.Tab) {
               _selectedCompletionIndex = (_selectedCompletionIndex + 1) % _currentCompletions.Length;
               UpdateCompletionSelection();
               e.Handled = true;
            } else if (e.Key == Key.Up) {
               _selectedCompletionIndex = (_selectedCompletionIndex - 1 + _currentCompletions.Length) % _currentCompletions.Length;
               UpdateCompletionSelection();
               e.Handled = true;
            } else if (e.Key == Key.Enter || e.Key == Key.Space) {
               ApplyCompletion(_currentCompletions[_selectedCompletionIndex]);
               e.Handled = true;
            } else if (e.Key == Key.Escape) {
               _activeCompletionMenu.IsOpen = false;
               e.Handled = true;
            }
         };

         // Create a simplified template for menu items without icon column
         ControlTemplate itemTemplate = new(typeof(MenuItem));
         FrameworkElementFactory border = new(typeof(Border));
         border.Name = "Border";
         border.SetValue(Border.BackgroundProperty,bgBrush);
         border.SetValue(Border.PaddingProperty,new Thickness(8,4,8,4));

         FrameworkElementFactory contentPresenter = new(typeof(ContentPresenter));
         contentPresenter.SetValue(ContentPresenter.ContentSourceProperty,"Header");
         border.AppendChild(contentPresenter);

         itemTemplate.VisualTree = border;

         Trigger mouseOverTrigger = new() { Property = MenuItem.IsMouseOverProperty,Value = true };
         mouseOverTrigger.Setters.Add(new Setter(Border.BackgroundProperty,highlightBgBrush,border.Name));
         itemTemplate.Triggers.Add(mouseOverTrigger);

         // Style for menu items
         Style menuItemStyle = new(typeof(MenuItem));
         menuItemStyle.Setters.Add(new Setter(MenuItem.TemplateProperty,itemTemplate));
         menuItemStyle.Setters.Add(new Setter(MenuItem.ForegroundProperty,fgBrush));

         Trigger itemMouseOverTrigger = new() { Property = MenuItem.IsMouseOverProperty,Value = true };
         itemMouseOverTrigger.Setters.Add(new Setter(MenuItem.ForegroundProperty,highlightFgBrush));
         menuItemStyle.Triggers.Add(itemMouseOverTrigger);

         int index = 0;
         foreach (string completion in _currentCompletions) {
            MenuItem menuItem = new() {
               Header = completion,
               Style = menuItemStyle,
               Tag = index
            };
            menuItem.Click += (s,e) => ApplyCompletion(completion);
            _activeCompletionMenu.Items.Add(menuItem);
            index++;
         }

         // Highlight the first item
         UpdateCompletionSelection();

         // Simple positioning: 30px right of InputTextBox, bottom of menu above InputTextBox
         Point inputPosition = InputTextBox.TransformToAncestor(this).Transform(new Point(0,0));

         _activeCompletionMenu.HorizontalOffset = this.Left + 20;
         _activeCompletionMenu.VerticalOffset = this.Top + this.Height - (InputArea.ActualHeight + InputAreaSeparator.ActualHeight + _currentCompletions.Length * 26);

         _activeCompletionMenu.Closed += (s,e) => {
            _activeCompletionMenu = null;
            _currentCompletions = null;
            InputTextBox.Focus();
         };
         _activeCompletionMenu.IsOpen = true;
      }

      private void UpdateCompletionSelection() {
         if (_activeCompletionMenu == null || _currentCompletions == null) return;

         // Get colors
         string bgColorHex = PrettyPrinter.Decorators[SE.Other].BG;
         string fgColorHex = PrettyPrinter.Decorators[SE.Other].FG;
         string highlightBgHex = PrettyPrinter.Decorators[SE.NoteInfo].BG;
         string highlightFgHex = PrettyPrinter.Decorators[SE.NoteInfo].FG;

         Brush bgBrush = (Brush)new BrushConverter().ConvertFromString(bgColorHex)!;
         Brush fgBrush = (Brush)new BrushConverter().ConvertFromString(fgColorHex)!;
         Brush highlightBgBrush = (Brush)new BrushConverter().ConvertFromString(highlightBgHex)!;
         Brush highlightFgBrush = (Brush)new BrushConverter().ConvertFromString(highlightFgHex)!;

         for (int i = 0 ; i < _activeCompletionMenu.Items.Count ; i++) {
            MenuItem item = (MenuItem)_activeCompletionMenu.Items[i];
            if (i == _selectedCompletionIndex) {
               item.Background = highlightBgBrush;
               item.Foreground = highlightFgBrush;
            } else {
               item.Background = bgBrush;
               item.Foreground = fgBrush;
            }
         }
      }

      private void ApplyCompletion(string completion) {
         string currentText = InputTextBox.Text;
         InputTextBox.Text = currentText[.._completionStartPos] + completion;
         InputTextBox.CaretIndex = InputTextBox.Text.Length;
         if (_activeCompletionMenu != null) _activeCompletionMenu.IsOpen = false;
      }

      /// <summary>
      /// Handle input text box key down events
      /// </summary>
      private void InputTextBox_PreviewKeyDown(object sender,KeyEventArgs e) {
         e.Handled = false;
         if (e.Key == Key.Enter) {
            HandleEnterKey();
         } else if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None) {
            ShowHelpToast();
         } else if (e.Key == Key.Escape) {
            e.Handled = true;
            SwitchToSingleLine();
            DisplayPrompt();
            IsEditing = false;
            e.Handled = true;
         } else if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.None) {
            if (_multilineMode) {
               InsertIndentation();
            } else {
               if (NameCompletion.GetCommandCompletions(InputTextBox.Text,out CompletionResult? result)) {
                  ApplyCompletions(result);
               } else if (NameCompletion.GetSettingCompletions(InputTextBox.Text,out result)) {
                  ApplyCompletions(result);
               } else if (NameCompletion.GetSelectorCompletions(InputTextBox.Text,out result)) {
                  ApplyCompletions(result);
               }
            }
            e.Handled = true;
         } else if (e.Key == Key.Up && !_multilineMode) {
            HandleHistoryNavigation(true);
            e.Handled = true;
         } else if (e.Key == Key.Down && !_multilineMode) {
            HandleHistoryNavigation(false);
            e.Handled = true;
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
         } else if (e.Key == Key.Down && Keyboard.Modifiers == ModifierKeys.Alt) {
            // Select next completion
            if (_activeCompletionMenu != null) {
               e.Handled = true;

               _selectedCompletionIndex++;
               if (_selectedCompletionIndex >= _currentCompletions!.Length) {
                  _selectedCompletionIndex = 0;
               }
               UpdateCompletionSelection();
            }
         } else if (e.Key == Key.Up && Keyboard.Modifiers == ModifierKeys.Alt) {
            // Select previous completion
            if (_activeCompletionMenu != null) {
               e.Handled = true;

               _selectedCompletionIndex--;
               if (_selectedCompletionIndex < 0) {
                  _selectedCompletionIndex = _currentCompletions!.Length - 1;
               }
               UpdateCompletionSelection();
            }
         } else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Alt) {
            // Confirm selected completion
            if (_activeCompletionMenu != null && _currentCompletions != null) {
               e.Handled = true;

               // Get the selected completion
               string selectedCompletion = _currentCompletions[_selectedCompletionIndex];
               ApplyCompletion(selectedCompletion);
            }
         }

         /////////////////////
         // Local functions //
         /////////////////////
         void Insert(string chars) {
            int index = InputTextBox.CaretIndex;
            InputTextBox.Text = InputTextBox.Text.Insert(index,chars);
            InputTextBox.CaretIndex = index + chars.Length;
         }
         void InsertNewlineWithIndent() {
            string caretLine = InputTextBox.GetLineText(InputTextBox.GetLineIndexFromCharacterIndex(InputTextBox.CaretIndex));
            int leadingSpaces = Math.Max(0,caretLine.FindIndex(c => !char.IsWhiteSpace(c)));
            if (caretLine.Length > leadingSpaces && caretLine[leadingSpaces] == '(') leadingSpaces++;
            Insert(Environment.NewLine + new string(' ',leadingSpaces));
         }

         void HandleEnterKey() {
            string input = InputTextBox.Text;
            bool enterModifierPressed = e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control);

            if (_multilineMode) {
               if (input.EndsWith('.') && (input.Length == InputTextBox.CaretIndex || enterModifierPressed)) {
                  SwitchToSingleLine();
                  ExecuteCommand();
                  e.Handled = true;
               } else {
                  InsertNewlineWithIndent();
                  e.Handled = true;
               }
            } else {
               InputType inputType = TokenList.ClassifyInput(input, out string trimmed, out string firstWord);
                
                switch (inputType) {
                    case InputType.Empty:
                        ExecuteCommand();
                        e.Handled = true;
                        break;
                        
                    case InputType.CommandComment:
                        // Command comments are ignored - just show new prompt
                        DisplayPrompt();
                        e.Handled = true;
                        break;
                        
                    case InputType.CDL2Comment:
                        // CDL2 comments are passed through as-is
                        ExecuteCommand();
                        e.Handled = true;
                        break;
                        
                    case InputType.Command:
                        ExecuteCommand();
                        e.Handled = true;
                        break;
                        
                    case InputType.CDL2Construct:
                        // Expand abbreviation and switch to multiline if needed
                        SelectorType type = Abbreviation<SelectorType>.Identify(firstWord.ToUpper());
                        InputTextBox.Text = $"{type} {input[firstWord.Length..]}";
                        InputTextBox.CaretIndex = InputTextBox.Text.Length;
                        
                        if (trimmed.EndsWith('.')) {
                            ExecuteCommand();
                            e.Handled = true;
                        } else {
                            SwitchToMultiline();
                            InsertNewlineWithIndent();
                            e.Handled = true;
                        }
                        break;
                        
                    case InputType.Invalid:
                        WriteError($"Invalid input: '{firstWord}' is not a recognized command or CDL2 reserved word.");
                        DisplayPrompt();
                        e.Handled = true;
                        break;
                }
            }
         }

         void ShowHelpToast() {
            string toastMessage = _multilineModeHelp;
            if (!_multilineMode) {
               toastMessage = _singleLineModeHelp;
               string trimmed = InputTextBox.Text.Trim();
               if (trimmed.Length == 0 || char.IsAsciiLetterLower(trimmed[0])) {
                  string firstWord = trimmed.Split(' ','\t','\r','\n')[0];
                  string commandHelp = Abbreviation<CommandType>.LongHelp(firstWord,toastFormat: true);
                  if (commandHelp.IsNotNullEmptyOrWhitespace) toastMessage += $"\n\nCommand|Parameters|Description\n---\n{commandHelp}";
               }
            }
            Toaster.ShowToast(toastMessage);
         }

         void InsertIndentation() {
            int spacesToInsert = this.Emitter!.IndentWidth - (Math.Min(InputTextBox.CaretIndex - 1,0)) % Emitter!.IndentWidth;
            Insert(new string(' ',spacesToInsert));
         }

         void HandleHistoryNavigation(bool previous) {
            string? history = previous ? _commandHistory.Previous() : _commandHistory.Next();
            if (history is null) {
               FlashInputError();
            } else {
               InputTextBox.Text = history;
               InputTextBox.CaretIndex = history.Length;
            }
         }

         void ApplyCompletions(CompletionResult result) {
            if (result.Completions.Length == 1) {
               // Single match: use the full object name
               string replacement = result.Completions[0];
               InputTextBox.Text = InputTextBox.Text[..result.StartPosition] + replacement;
               InputTextBox.CaretIndex = InputTextBox.Text.Length;
            } else {
               ShowCompletionMenu(result.Completions,result.StartPosition);
            }
         }
      }

      private static void ClearUndoRedo(TextBox box) {
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
         if (!string.IsNullOrEmpty(command)) {
            if (char.IsAsciiLetterLower(command[0])) {
               // It's a command, so add it to history and echo it.
               _commandHistory.Add(command);
               // Echo command
               WriteLine($"> {command}");
            }

            if (command.IsNotNullEmptyOrWhitespace) {
               // Raise event to handle the command
               CommandEntered?.Invoke(this,command);
            }
         }

         // Display new prompt
         DisplayPrompt();
      }

      // Implement ICommandInterface.QueryBox with simplified signature
      bool ICLIREPL.QueryBox(string message) =>
         MessageBox.Show(this,message,CDL2.LabName,
                              MessageBoxButton.YesNo,
                              MessageBoxImage.Question) == MessageBoxResult.Yes;
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
      private void ZoomIn_Click(object sender,RoutedEventArgs e) => ZoomIn(20);

      /// <summary>
      /// Zoom out button click handler
      /// </summary>
      private void ZoomOut_Click(object sender,RoutedEventArgs e) => ZoomOut(20);

      /// <summary>
      /// Increase font size
      /// </summary>
      private void ZoomIn(int percentage = 20) {
         double multiplier = (100 + percentage) / 100.0;
         ApplyZoomToChildren(this,multiplier);
         AdjustStatusBarHeight(multiplier);
      }

      /// <summary>
      /// Decrease font size
      /// </summary>
      private void ZoomOut(int percentage = 20) {
         double divisor = (100 + percentage) / 100.0;
         ApplyZoomToChildren(this,1.0 / divisor);
         AdjustStatusBarHeight(1.0 / divisor);
      }

      /// <summary>
      /// Adjust status bar height based on zoom multiplier
      /// </summary>
      private void AdjustStatusBarHeight(double multiplier) {
         if (_initialStatusBarHeight == 0) {
            _initialStatusBarHeight = StatusBarGrid.ActualHeight;
         }

         if (_initialStatusBarHeight > 0) {
            double currentHeight = StatusBarGrid.ActualHeight;
            double newHeight = currentHeight * multiplier;
            StatusBarGrid.Height = newHeight;
         }
      }

      /// <summary>
      /// Reset all font sizes to their initial values
      /// </summary>
      private void ResetZoom() {
         foreach (KeyValuePair<string,ZoomParameters> entry in ZoomRestrictions) {
            ZoomRestrictions[entry.Key] = entry.Value with { SkippedZooms = 0 };
            RestoreFontSize(this,entry.Key,entry.Value.InitialFontSize);
         }

         // Reset status bar to initial height
         if (_initialStatusBarHeight > 0) {
            StatusBarGrid.Height = _initialStatusBarHeight;
         } else {
            StatusBarGrid.Height = double.NaN;  // Auto-size
         }
      }

      private record ZoomParameters(int MinPct = 0,int MaxPct = 0,double InitialFontSize = 0.0,int SkippedZooms = 0);

      private Dictionary<string,ZoomParameters> ZoomRestrictions = new() {
         { "PromptTextBlock", new ZoomParameters(0, -1, 0.0, 0) },
         { "InputTextBox",    new ZoomParameters(0, -1, 0.0, 0) },
      };

      /// <summary>
      /// Recursively apply zoom to all text-displaying controls in the visual tree
      /// </summary>
      private void ApplyZoomToChildren(DependencyObject parent,double multiplier) {
         int childCount = VisualTreeHelper.GetChildrenCount(parent);

         for (int i = 0 ; i < childCount ; i++) {
            DependencyObject child = VisualTreeHelper.GetChild(parent,i);

            // Apply zoom to TextBoxes and TextBlocks only, and recursively to other controls
            if (child is TextBlock textBlock) {
               if (ShouldApplyZoom(textBlock.Name,textBlock.FontSize,multiplier,out double blockNewSize)) textBlock.FontSize = blockNewSize;
            } else if (child is TextBox textBox) {
               if (ShouldApplyZoom(textBox.Name,textBox.FontSize,multiplier,out double boxNewSize)) textBox.FontSize = boxNewSize;
            } else {
               // Recursively process children
               ApplyZoomToChildren(child,multiplier);
            }
         }
      }

      /// <summary>
      /// Determines if zoom should be applied based on restrictions
      /// </summary>
      private bool ShouldApplyZoom(string? controlName,double currentFontSize,double multiplier,out double newSize) {
         newSize = currentFontSize * multiplier;
         bool isZoomingIn = multiplier > 1.0;

         // Only zoom named controls - skip unnamed controls (like TextBlocks inside buttons)
         if (string.IsNullOrEmpty(controlName)) {
            newSize = currentFontSize;
            return false;
         }

         // No restrictions - allow zoom
         if (!ZoomRestrictions.TryGetValue(controlName,out ZoomParameters? restrictions)) return true;

         // Initialize the initial font size if needed
         if (restrictions.InitialFontSize == 0.0) {
            ZoomRestrictions[controlName] = restrictions with { InitialFontSize = currentFontSize };
            restrictions = ZoomRestrictions[controlName];
         }

         // Calculate limits based on initial font size
         double minAllowed = restrictions.MinPct == 0 ? restrictions.InitialFontSize
                           : restrictions.MinPct == -1 ? 0
                           : restrictions.InitialFontSize * restrictions.MinPct / 100.0;

         double maxAllowed = restrictions.MaxPct == 0 ? restrictions.InitialFontSize
                           : restrictions.MaxPct == -1 ? double.MaxValue
                           : restrictions.InitialFontSize * restrictions.MaxPct / 100.0;

         // Check if we have skipped zooms in the opposite direction to consume first
         if ((isZoomingIn && restrictions.SkippedZooms < 0) || (!isZoomingIn && restrictions.SkippedZooms > 0)) {
            // Consume one skipped zoom instead of actually zooming
            int adjustment = isZoomingIn ? 1 : -1;
            ZoomRestrictions[controlName] = restrictions with { SkippedZooms = restrictions.SkippedZooms + adjustment };
            newSize = currentFontSize;
            return false;
         }

         // Check if new size would violate restrictions
         if (newSize < minAllowed || newSize > maxAllowed) {
            // Track this skipped zoom
            int skipAdjustment = isZoomingIn ? 1 : -1;
            ZoomRestrictions[controlName] = restrictions with { SkippedZooms = restrictions.SkippedZooms + skipAdjustment };
            newSize = currentFontSize;
            return false;
         }

         return true;
      }
      /// <summary>
      /// Initialize zoom restrictions for all named TextBox and TextBlock controls
      /// </summary>
      private void InitializeZoomRestrictions() {
         PopulateZoomRestrictions(this);
      }

      /// <summary>
      /// Recursively populate zoom restrictions for all text controls
      /// </summary>
      private void PopulateZoomRestrictions(DependencyObject parent) {
         int childCount = VisualTreeHelper.GetChildrenCount(parent);

         for (int i = 0 ; i < childCount ; i++) {
            DependencyObject child = VisualTreeHelper.GetChild(parent,i);

            // Process TextBlock and TextBox controls - these are leaf nodes, don't recurse
            if (child is TextBlock textBlock) {
               string? controlName = textBlock.Name;
               if (!string.IsNullOrEmpty(controlName)) {
                  if (!ZoomRestrictions.ContainsKey(controlName)) {
                     ZoomRestrictions[controlName] = new ZoomParameters(-1,-1,textBlock.FontSize,0);
                  } else if (ZoomRestrictions[controlName].InitialFontSize == 0.0) {
                     // Update existing entry with actual font size
                     ZoomRestrictions[controlName] = ZoomRestrictions[controlName] with { InitialFontSize = textBlock.FontSize };
                  }
               }
            } else if (child is TextBox textBox) {
               string? controlName = textBox.Name;
               if (!string.IsNullOrEmpty(controlName)) {
                  if (!ZoomRestrictions.ContainsKey(controlName)) {
                     ZoomRestrictions[controlName] = new ZoomParameters(-1,-1,textBox.FontSize,0);
                  } else if (ZoomRestrictions[controlName].InitialFontSize == 0.0) {
                     // Update existing entry with actual font size
                     ZoomRestrictions[controlName] = ZoomRestrictions[controlName] with { InitialFontSize = textBox.FontSize };
                  }
               }
            } else {
               // Only recurse into containers (Grid, StackPanel, etc.), not into TextBlock/TextBox
               PopulateZoomRestrictions(child);
            }
         }
      }

      /// <summary>
      /// Recursively find and restore font size for a named control
      /// </summary>
      private void RestoreFontSize(DependencyObject parent,string controlName,double fontSize) {
         int childCount = VisualTreeHelper.GetChildrenCount(parent);

         for (int i = 0 ; i < childCount ; i++) {
            DependencyObject child = VisualTreeHelper.GetChild(parent,i);

            if ((child as FrameworkElement)?.Name == controlName) {
               if (child is TextBlock textBlock) textBlock.FontSize = fontSize;
               else if (child is TextBox textBox) textBox.FontSize = fontSize;
               return;
            }

            RestoreFontSize(child,controlName,fontSize);
         }
      }

      /// <summary>
      /// Reset zoom button click handler
      /// </summary>
      private void ResetZoom_Click(object sender,RoutedEventArgs e) => ResetZoom();

      /// <summary>
      /// In multiline mode, handle double-click.
      /// </summary>
      private void InputTextBox_PreviewMouseLeftButtonDown(object sender,MouseButtonEventArgs e) {
         e.Handled = false;
         if (e.ClickCount != 2 && e.ClickCount != 3) return;
         if (sender is not TextBox textBox) return;
         if (!_multilineMode) return;

         Point mousePos = e.GetPosition(textBox);
         int charIndex = textBox.GetCharacterIndexFromPoint(mousePos,true);
         if (charIndex < 0 || charIndex >= textBox.Text.Length) return;

         string text = textBox.Text;
         string[] lines = text.Split('\n');
         int lineIndex = textBox.GetLineIndexFromCharacterIndex(charIndex);
         string line = textBox.GetLineText(lineIndex);
         int lineStart = textBox.GetCharacterIndexFromLineIndex(lineIndex);
         int posInLine = charIndex - lineStart;
         int startSel = charIndex;
         int endSel = charIndex;

         if (IsCommentOrNoteOrBlank(line)) {
            SelectCommentOrNoteBlock();
            e.Handled = true;
            return;
         }

         int algoLineIndex = FindAlgorithmHeaderLine();
         if (algoLineIndex < 0) return;
         string headerLine = lines[algoLineIndex];
         int headerLineStart = textBox.GetCharacterIndexFromLineIndex(algoLineIndex);

         GetHeaderSeparator(headerLine,out int sepIdx,out int sepLen,out string sepType);

         bool inHeader = (lineIndex == algoLineIndex) && (sepIdx >= 0) && (posInLine <= sepIdx + sepLen - 1);
         bool inBody = (lineIndex > algoLineIndex) ||
                       (lineIndex == algoLineIndex && sepIdx >= 0 && posInLine > sepIdx + sepLen - 1);

         if (inHeader) {
            if (e.ClickCount == 3) { e.Handled = true; return; }
            if (e.ClickCount == 2) {
               ExpandSelection(GetValidCharPredicate(text,startSel));
               textBox.Select(startSel,endSel - startSel + 1);
               e.Handled = true;
               return;
            }
         } else if (inBody) {
            bool isProcedure = sepType == ":" || sepType == ":=";
            bool isMacro = sepType == "=" || sepType == "=:";
            if (isMacro && e.ClickCount == 3) { e.Handled = true; return; }

            int hashCount = 0;
            for (int i = 0 ; i < posInLine ; i++) if (line[i] == '#') hashCount++;
            if ((hashCount % 2) == 1) { e.Handled = true; return; }

            if (e.ClickCount == 2) {
               if (!char.IsAsciiLetter(text[startSel])) { e.Handled = true; return; }
               ExpandSelection(GetValidCharPredicate(text,startSel));
               textBox.Select(startSel,endSel - startSel + 1);
               e.Handled = true;
               return;
            } else if (e.ClickCount == 3) {
               if (!isProcedure) { e.Handled = true; return; }
               startSel = FindProcedureCallStart();
               if (startSel >= text.Length || !char.IsAsciiLetterLower(text[startSel])) { e.Handled = true; return; }
               endSel = FindProcedureCallEnd();
               textBox.Select(startSel,endSel - startSel + 1);
               e.Handled = true;
               return;
            }
         }

         /////////////////////
         // Local functions //
         /////////////////////
         bool IsCommentOrNoteOrBlank(string l) {
            if (string.IsNullOrWhiteSpace(l)) return true;
            string trimmed = l.TrimStart();
            if (trimmed.StartsWith('#')) return true;
            return trimmed.Length >= 4 && char.IsUpper(trimmed[0]) &&
                trimmed.StartsWith("Note",StringComparison.OrdinalIgnoreCase) &&
                (trimmed.Length == 4 || char.IsWhiteSpace(trimmed[4]) || char.IsPunctuation(trimmed[4]));
         }

         void SelectCommentOrNoteBlock() {
            int first = lineIndex, last = lineIndex, totalLines = lines.Length;
            for (int i = lineIndex - 1 ; i >= 0 ; i--) {
               if (IsCommentOrNoteOrBlank(lines[i])) first = i;
               else break;
            }
            for (int i = lineIndex + 1 ; i < totalLines ; i++) {
               if (IsCommentOrNoteOrBlank(lines[i])) last = i;
               else break;
            }
            int selStart = textBox.GetCharacterIndexFromLineIndex(first);
            int selEnd = textBox.GetCharacterIndexFromLineIndex(last) + lines[last].Length;
            textBox.Select(selStart,selEnd - selStart);
         }

         int FindAlgorithmHeaderLine() {
            Regex algoHeaderRegex = AlgorithmHeaderRE();
            int idx = lineIndex;
            while (idx >= 0) {
               string candidate = lines[idx].TrimStart();
               if (algoHeaderRegex.IsMatch(candidate)) return idx;
               idx--;
            }
            return -1;
         }

         void GetHeaderSeparator(string headerLine,out int sepIdx,out int sepLen,out string sepType) {
            int idxColonEq = headerLine.IndexOf(":=");
            int idxEqColon = headerLine.IndexOf("=:");
            int idxColon = headerLine.IndexOf(':');
            int idxEq = headerLine.IndexOf('=');
            sepIdx = -1; sepLen = 0; sepType = "";
            if (idxColonEq >= 0 && (sepIdx == -1 || idxColonEq < sepIdx)) { sepIdx = idxColonEq; sepLen = 2; sepType = ":="; }
            if (idxEqColon >= 0 && (sepIdx == -1 || idxEqColon < sepIdx)) { sepIdx = idxEqColon; sepLen = 2; sepType = "=:"; }
            if (idxColon >= 0 && (sepIdx == -1 || idxColon < sepIdx)) { sepIdx = idxColon; sepLen = 1; sepType = ":"; }
            if (idxEq >= 0 && (sepIdx == -1 || idxEq < sepIdx)) { sepIdx = idxEq; sepLen = 1; sepType = "="; }
         }

         Predicate<char> GetValidCharPredicate(string t,int idx) =>
             char.IsAsciiLetterUpper(t[idx])
                 ? char.IsAsciiLetterUpper
                 : c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == ' ';

         void ExpandSelection(Predicate<char> validChar) {
            while (startSel > 0 && validChar(text[startSel - 1])) startSel--;
            while (!char.IsAsciiLetter(text[startSel]) && startSel < endSel) startSel++;
            while (endSel < text.Length - 1 && validChar(text[endSel + 1])) endSel++;
            while (endSel > startSel && text[endSel] == ' ') endSel--;
         }

         int FindProcedureCallStart() {
            int i = charIndex;
            bool inString = false;
            while (i > 0) {
               char c = text[i - 1];
               if (c == '"') {
                  int dollarCount = 0, j = i - 2;
                  while (j >= 0 && text[j] == '$') { dollarCount++; j--; }
                  if (dollarCount % 2 == 0) inString = !inString;
               }
               if (!inString) {
                  int k = i - 1;
                  while (k >= 0 && text[k] == ' ') k--;
                  if (k < 0) { i = 0; break; }
                  if (k > 0 && text[k - 1] == ':' && text[k] == '=') { i = k + 1; break; }
                  if (text[k] == ':' || text[k] == ';' || text[k] == ',' || text[k] == '(' ||
                      text[k] == '\n' || text[k] == '\r') { i = k + 1; break; }
                  int wordEnd = k;
                  while (wordEnd >= 0 && char.IsLetter(text[wordEnd])) wordEnd--;
                  int wordStart = wordEnd + 1;
                  if (wordStart <= k && char.IsUpper(text[wordStart])) { i = k + 1; break; }
               }
               i--;
            }
            while (i < text.Length && text[i] == ' ') i++;
            if (i < text.Length && text[i] == '(') {
               i++;
               while (i < text.Length && text[i] == ' ') i++;
            }
            return i;
         }

         int FindProcedureCallEnd() {
            int i = charIndex;
            bool inString = false;
            while (i < text.Length) {
               char c = text[i];
               if (c == '"') {
                  int dollarCount = 0, j = i - 1;
                  while (j >= 0 && text[j] == '$') { dollarCount++; j--; }
                  if (dollarCount % 2 == 0) inString = !inString;
               }
               if (!inString) {
                  if (c == ',' || c == ';' || c == '.') break;
               }
               i++;
            }
            int endSelLocal = i - 1;
            while (endSelLocal > startSel && char.IsWhiteSpace(text[endSelLocal])) endSelLocal--;
            while (endSelLocal >= startSel && text[endSelLocal] == ')') endSelLocal--;
            while (endSelLocal > startSel && text[endSelLocal] == ' ') endSelLocal--;
            return endSelLocal;
         }
      }

      private void InputTextBox_TextChanged(object sender,TextChangedEventArgs e) {
         if (_multilineMode && sender is TextBox box) {
            box.Background = Database.Instance.CLI.VerifySyntax(box.Text) ? _multilineInputBackground : _multilineInputErrorBackground;
         }
      }

      #endregion
      public void SetStatus(string message) {
         StatusBarLeftTextBlock.Text = message;
         string programName = Settings.SettingValue<string>("ProgramName")!;
         string marker = programName.IsNotNullEmptyOrWhitespace && Database.Instance.ProgramByName(programName)!.Modified ? "*" : "";
         StatusBarRightTextBlock.Text = $"[{Database.Instance.GetModificationCount()}/{Settings.SettingValue<int>("AutosaveCount")}] {marker}Prog {programName}";
      }

      private double MeasureTextWidth(string text,FontFamily fontFamily,double fontSize) {
         FormattedText formattedText = new(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(fontFamily,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal),
            fontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
         return formattedText.Width;
      }

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

            ColorAnimation animation = new() {
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

            DispatcherTimer timer = new() {
               Interval = TimeSpan.FromMilliseconds(400)
            };
            timer.Tick += (s,e) => {
               InputTextBox.Background = originalBrush;
               if (s is not null) ((DispatcherTimer)s).Stop();
            };
            timer.Start();
         }
      }

      [GeneratedRegex(@"^(?:A(?i:CTION)|F(?i:UNCTION)|T(?i:EST)|P(?i:REDICATE))\s",RegexOptions.Compiled,"en-US")]
      private static partial Regex AlgorithmHeaderRE();
   }
}
#endif