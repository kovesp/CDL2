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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CDL2v1 {
   /// <summary>
   /// Interaction logic for CommandWindow.xaml
   /// </summary>
   public partial class CommandPromptWindow : Window {
      private readonly History _commandHistory = new();
      private FontFamily _textFont = new("Cascadia Mono");

      // Store the last height of the output area for restore functionality
      private double _lastOutputHeight = 0;

      // Event raised when a command is entered
      public event EventHandler<string>? CommandEntered;

      private bool _multilineMode = false;
      private string _singleLineTooltip = "Enter a Lab command and press Enter";
      private string _multilineTooltip = "Enter CDL2 construct. Submit occurs when a line ends with a period ('.').";

      // Background brushes for input field
      private readonly Brush _standardInputBackground;
      private readonly Brush _multilineInputBackground = Brushes.White;
      private readonly Brush _standardInputForeground;
      private readonly Brush _multilineInputForeground = Brushes.Black;

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

         // Store the original background at startup
         _standardInputBackground = InputTextBox.Background;
         _standardInputForeground = InputTextBox.Foreground;
         InputTextBox.ToolTip = _singleLineTooltip;

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
      /// <param name="noteType">Optional note type that determines text color (default None)</param>
      public void WriteLine(string text,NoteType noteType = NoteType.NONE) {
         Application.Current.Dispatcher.Invoke(() => {
            // Get the appropriate brush based on note type from PrettyPrinter.Decorators
            Brush foreground = GetNoteTypeBrush(noteType);

            // Add formatted text with the appropriate color
            AddFormattedText(text,foreground,Brushes.Transparent,lineBreak: true);
         });
      }

      public void WriteError(string text) => WriteLine(text,NoteType.Error);
      public void WriteWarning(string text) => WriteLine(text,NoteType.Warning);
      public void WriteInfo(string text) => WriteLine(text,NoteType.Info);

      /// <summary>
      /// Gets the brush color for a specific note type using colors from PrettyPrinter.Decorators
      /// </summary>
      /// <param name="noteType">The note type</param>
      /// <returns>Brush corresponding to the note type</returns>
      private static Brush GetNoteTypeBrush(NoteType noteType) {
         // Map NoteType to corresponding SyntacticElement
         SE element = noteType switch {
            NoteType.Error => SE.NoteError,
            NoteType.Warning => SE.NoteWarning,
            NoteType.Info => SE.NoteInfo,
            NoteType.Note => SE.Comment,
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
            PromptTextBlock.Text = "> ";
            InputTextBox.Text = "";
            InputTextBox.ToolTip = _singleLineTooltip;
            InputTextBox.Focus();
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
         if (e.Key == Key.Enter) {
            string input = InputTextBox.Text;

            if (_multilineMode) {
               var lines = input.Split(new[] { "\r\n","\n" },StringSplitOptions.None);
               string lastLine = lines.Length > 0 ? lines[^1].TrimEnd() : "";
               if (lastLine.EndsWith('.')) {
                  _multilineMode = false;
                  InputTextBox.AcceptsReturn = false;
                  InputTextBox.ToolTip = _singleLineTooltip;
                  InputTextBox.Background = _standardInputBackground;
                  InputTextBox.Foreground = InputTextBox.CaretBrush = _standardInputForeground;
                  ExecuteCommand();
                  e.Handled = true;
               } else {
                  // Stay in multiline mode, allow Enter to insert a new line
                  e.Handled = false;
               }
               return;
            } else {
               // Check wheter we have a command or CDL2 object
               string trimmed = input.Trim();
               string firstWord = trimmed.Split(' ','\t','\r','\n')[0];
               bool isCommand = char.IsAsciiLetterLower(firstWord[0]);
               if (!isCommand) {
                  SelectorType type = Abbreviation<SelectorType>.Identify(firstWord.ToUpper());
                  if (type != SelectorType.INVALID) {
                     InputTextBox.Text = $"{type} {input[firstWord.Length..]}";
                     InputTextBox.CaretIndex = InputTextBox.Text.Length;
                  } else {                      // If not a command, treat as CDL2 object
                     WriteError($"Attempt to enter a CDL2 construct with non-existent reserved word: {firstWord}");
                     DisplayPrompt();
                     e.Handled = true;
                     return;
                  }
               }
               if (isCommand || trimmed.EndsWith('.')) { // Command (lc word) or single line code.
                  ExecuteCommand();
                  e.Handled = true;
                  return;
               } else {
                  _multilineMode = true;
                  InputTextBox.AcceptsReturn = true;
                  InputTextBox.ToolTip = _multilineTooltip;
                  InputTextBox.Background = _multilineInputBackground;
                  InputTextBox.Foreground = InputTextBox.CaretBrush = _multilineInputForeground;

                  e.Handled = false; // Let Enter insert a new line
                  return;
               }
            }
         } else if (e.Key == Key.Up) {
            InputTextBox.Text = _commandHistory.Previous();
            InputTextBox.CaretIndex = InputTextBox.Text.Length;
            e.Handled = true;
         } else if (e.Key == Key.Down) {
            InputTextBox.Text = _commandHistory.Next();
            InputTextBox.CaretIndex = InputTextBox.Text.Length;
            e.Handled = true;
         }
      }

      /// <summary>
      /// Executes the current command
      /// </summary>
      private void ExecuteCommand() {
         string command = InputTextBox.Text.Trim();
         if (string.IsNullOrEmpty(command)) {
            DisplayPrompt();
            return;
         }

         // Add command to history
         _commandHistory.Add(command);

         // Echo command
         WriteLine($"> {command}");

         // Raise event to handle the command
         CommandEntered?.Invoke(this,command);

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
      #endregion

      #region Command History
      /// <summary>
      /// Command history manager
      /// </summary>
      private class History {
         private readonly List<string> _history = new();
         private int _currentIndex = -1;

         public void Add(string command) {
            _history.Add(command);
            _currentIndex = _history.Count;
         }

         public string Previous() {
            if (_history.Count == 0)
               return "";

            _currentIndex = Math.Max(0,_currentIndex - 1);
            return _currentIndex < _history.Count ? _history[_currentIndex] : "";
         }

         public string Next() {
            if (_history.Count == 0)
               return "";

            _currentIndex = Math.Min(_history.Count,_currentIndex + 1);
            return _currentIndex < _history.Count ? _history[_currentIndex] : "";
         }
      }
      #endregion

      public void SetStatus(string message) => StatusBarTextBox.Text = message;
   }
}
