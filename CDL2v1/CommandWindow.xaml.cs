using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

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
         MainGrid.SizeChanged += (s, e) => {
            if (OutputRow.ActualHeight > 0) {
               _lastOutputHeight = OutputRow.ActualHeight;
            }
         };

         // Focus on the window so it can receive keyboard input
         Loaded += (s, e) => {
            Keyboard.Focus(InputTextBox);
         };
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

      private void CommandPromptWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e) => SaveWindowSettings();

      private void CommandPromptWindow_LocationChanged(object? sender, EventArgs e) {
         if (IsLoaded && WindowState == WindowState.Normal)
            SaveWindowSettings();
      }

      private void CommandPromptWindow_SizeChanged(object? sender, SizeChangedEventArgs e) {
         if (IsLoaded && WindowState == WindowState.Normal)
            SaveWindowSettings();
      }
      #endregion

      #region Formatted Output
      /// <summary>
      /// Configure the output area for formatted text (called by CommandWindowEmitter)
      /// </summary>
      public void ConfigureFormattedOutput() {
         OutputTextBlock.Document.PageWidth = 2000; // Wide page to avoid wrapping
      }

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

         // Add the run to the last paragraph
         FlowDocument document = OutputTextBlock.Document;
         Paragraph lastParagraph;
         
         if (document.Blocks.Count == 0) {
            lastParagraph = new Paragraph();
            document.Blocks.Add(lastParagraph);
         } else {
            lastParagraph = document.Blocks.LastBlock as Paragraph;
         }

         lastParagraph?.Inlines.Add(run);
         
         if (lineBreak) {
            lastParagraph?.Inlines.Add(new LineBreak());
         }

         // Ensure auto-scroll to bottom
         OutputTextBlock.ScrollToEnd();
      }

      /// <summary>
      /// Begin batch updating of formatted text
      /// </summary>
      public void BeginFormattedUpdate() {
         OutputTextBlock.BeginChange();
         OutputScrollViewer.SetValue(ScrollViewer.CanContentScrollProperty, false);
      }

      /// <summary>
      /// End batch updating of formatted text
      /// </summary>
      public void EndFormattedUpdate() {
         OutputTextBlock.EndChange();
         OutputScrollViewer.SetValue(ScrollViewer.CanContentScrollProperty, true);
         OutputTextBlock.ScrollToEnd();
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
      /// Writes a line of text to the output
      /// </summary>
      public void WriteLine(string text) {
         Application.Current.Dispatcher.Invoke(() => {
            AddFormattedText(text, Brushes.LightGray, Brushes.Transparent, lineBreak: true);
         });
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
            InputTextBox.Focus();
         });
      }

      /// <summary>
      /// Handle input text box key down events
      /// </summary>
      private void InputTextBox_PreviewKeyDown(object sender, KeyEventArgs e) {
         switch (e.Key) {
            case Key.Enter:
               ExecuteCommand();
               e.Handled = true;
               break;
            case Key.Up:
               InputTextBox.Text = _commandHistory.Previous();
               InputTextBox.CaretIndex = InputTextBox.Text.Length;
               e.Handled = true;
               break;
            case Key.Down:
               InputTextBox.Text = _commandHistory.Next();
               InputTextBox.CaretIndex = InputTextBox.Text.Length;
               e.Handled = true;
               break;
            // Let standard Ctrl+C, Ctrl+V, etc. be handled by the TextBox control
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
         CommandEntered?.Invoke(this, command);

         // Display new prompt
         DisplayPrompt();
      }
      #endregion

      #region UI Controls
      /// <summary>
      /// Maximize/minimize the output area
      /// </summary>
      private void ToggleOutputArea_Click(object sender, RoutedEventArgs e) {
         if (OutputRow.Height.Value > 0) {
            // Collapse output area
            _lastOutputHeight = OutputRow.Height.Value;
            OutputRow.Height = new GridLength(0);
         } else {
            // Restore output area
            OutputRow.Height = new GridLength(_lastOutputHeight > 0 ? _lastOutputHeight : 1, GridUnitType.Star);
         }

         InputTextBox.Focus();
      }

      /// <summary>
      /// Handle mouse wheel zoom with Ctrl key
      /// </summary>
      private void OutputTextBlock_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
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
      private void ZoomIn_Click(object sender, RoutedEventArgs e) {
         ZoomIn(20);
      }

      /// <summary>
      /// Zoom out button click handler
      /// </summary>
      private void ZoomOut_Click(object sender, RoutedEventArgs e) {
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
      #endregion

      #region Command History
      /// <summary>
      /// Command history manager
      /// </summary>
      private class History {
         private readonly List<string> _history = [];
         private int _currentIndex = -1;

         public void Add(string command) {
            _history.Add(command);
            _currentIndex = _history.Count;
         }

         public string Previous() {
            if (_history.Count == 0) return "";

            _currentIndex = Math.Max(0, _currentIndex - 1);
            return _currentIndex < _history.Count ? _history[_currentIndex] : "";
         }

         public string Next() {
            if (_history.Count == 0) return "";

            _currentIndex = Math.Min(_history.Count, _currentIndex + 1);
            return _currentIndex < _history.Count ? _history[_currentIndex] : "";
         }
      }
      #endregion
   }
}