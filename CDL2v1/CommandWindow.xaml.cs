using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CDL2v1 {
   /// <summary>
   /// Interaction logic for CommandWindow.xaml
   /// </summary>
   public partial class CommandPromptWindow : Window {
      private readonly ObservableCollection<string> _outputLines = [];
      private readonly History _commandHistory = new();
      // private bool _isInitializing = true;

      // Event raised when a command is entered
      public event EventHandler<string>? CommandEntered;

      // Store the last height of the output area for restore functionality
      private double _lastOutputHeight = 0;

      public CommandPromptWindow() {
         InitializeComponent();
         OutputListBox.ItemsSource = _outputLines;

         // Set initial prompt
         WriteLine($"CDL2 Laboratory v{CDL2.Version} - Type 'help' for available commands");
         DisplayPrompt();

         // ApplyED saved window position and size
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
            Keyboard.Focus(this);
            //_isInitializing = false;
         };
      }

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

            leftSetting?.Value   = this.Left;
            topSetting?.Value    = this.Top;
            widthSetting?.Value  = this.Width;
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

      /// <summary>
      /// Writes a line of text to the output
      /// </summary>
      public void WriteLine(string text) {
         Application.Current.Dispatcher.Invoke(() => {
            _outputLines.Add(text);
            OutputListBox.ScrollIntoView(_outputLines[^1]);
         });
      }

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
      /// This works, but KeyDown does not receive Up and Down.
      /// </summary>
      /// <remarks>
      /// Note that Ctrl-C, Ctrl-X, and Ctrl-V are handled by the TextBox control itself.
      /// </remarks>
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
         }
      }

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
         
         // Ensure consistent background colors
         MainGrid.Background = new SolidColorBrush(Colors.DarkSlateGray);
         OutputListBox.Background = new SolidColorBrush(Colors.DarkSlateGray);
         InputTextBox.Background = new SolidColorBrush(Colors.DarkSlateGray);
         PromptTextBlock.Background = new SolidColorBrush(Colors.DarkSlateGray);
         
         // Focus back on the input box
         InputTextBox.Focus();
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
         _outputLines.Add($"> {command}");

         // Raise event to handle the command
         CommandEntered?.Invoke(this, command);

         // Display new prompt
         DisplayPrompt();
      }

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
   }
}