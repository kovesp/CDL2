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
      private ObservableCollection<string> _outputLines = new ObservableCollection<string>();
      private readonly History _commandHistory = new();

      // Event raised when a command is entered
      public event EventHandler<string> CommandEntered;

      public CommandPromptWindow() {
         InitializeComponent();
         OutputListBox.ItemsSource = _outputLines;

         // Set initial prompt
         WriteLine("CDL2 Laboratory - Type 'help' for available commands");
         DisplayPrompt();

         // Focus on the window so it can receive keyboard input
         Loaded += (s, e) => {
            Keyboard.Focus(this);
         };
      }

      /// <summary>
      /// Writes a line of text to the output
      /// </summary>
      public void WriteLine(string text) {
         Application.Current.Dispatcher.Invoke(() => {
            _outputLines.Add(text);
            OutputListBox.ScrollIntoView(_outputLines[_outputLines.Count - 1]);
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
      /// </summary>
      private void InputTextBox_KeyDown(object sender, KeyEventArgs e) {
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
      //private void InputTextBox_KeyDown(object sender, KeyEventArgs e) {
      //   if (e.Key == Key.Enter) {
      //      // This will call the OnKeyDown method which handles the command execution
      //      e.Handled = true;
      //   }
      //}

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
         private readonly List<string> _history = new List<string>();
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