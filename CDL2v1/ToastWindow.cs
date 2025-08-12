using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CDL2v1 {
   public class ToastWindow : Window {
      private readonly DispatcherTimer? _timer;

      public ToastWindow(string message,int timeoutMs = 3000) {
         WindowStyle = WindowStyle.None;
         AllowsTransparency = true;
         Background = Brushes.Transparent;
         ShowInTaskbar = false;
         Topmost = true;
         ResizeMode = ResizeMode.NoResize;
         SizeToContent = SizeToContent.WidthAndHeight;

         var border = new Border {
            Background = new SolidColorBrush(Color.FromArgb(220,30,30,30)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16)
         };

         var lines = message.Split(new[] { "\r\n","\n" },StringSplitOptions.None);

         // Determine max number of columns by counting '|' in each line
         int maxColumns = lines.Where(l => l.Trim() != "---")
                               .Select(l => l.Count(c => c == '|') + 1)
                               .DefaultIfEmpty(1)
                               .Max();

         var grid = new Grid();
         for (int c = 0 ; c < maxColumns ; c++) {
            grid.ColumnDefinitions.Add(new ColumnDefinition {
               Width = (c == maxColumns - 1) ? new GridLength(1,GridUnitType.Star) : GridLength.Auto
            });
         }

         int row = 0;
         foreach (var line in lines) {
            if (line.Trim() == "---") {
               grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
               var rule = new Border {
                  BorderBrush = Brushes.Gray,
                  BorderThickness = new Thickness(0,1,0,0),
                  Margin = new Thickness(0,6,0,6),
                  HorizontalAlignment = HorizontalAlignment.Stretch
               };
               Grid.SetRow(rule,row);
               Grid.SetColumn(rule,0);
               Grid.SetColumnSpan(rule,maxColumns);
               grid.Children.Add(rule);
               row++;
               continue;
            }
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var parts = line.Split('|');
            if (parts.Length > 1) {
               for (int col = 0 ; col < maxColumns ; col++) {
                  string text = col < parts.Length ? parts[col].Trim() : "";
                  var tb = new TextBlock {
                     Text = text,
                     Foreground = Brushes.White,
                     FontSize = 16,
                     // Add extra horizontal space between columns
                     Margin = new Thickness(col == 0 ? 4 : 16,0,col == maxColumns - 1 ? 4 : 0,0),
                     TextWrapping = TextWrapping.Wrap
                  };
                  Grid.SetRow(tb,row);
                  Grid.SetColumn(tb,col);
                  grid.Children.Add(tb);
               }
            } else {
               var span = new TextBlock {
                  Text = line.Trim(),
                  Foreground = Brushes.White,
                  FontSize = 16,
                  Margin = new Thickness(4,0,4,0),
                  TextWrapping = TextWrapping.Wrap
               };
               Grid.SetRow(span,row);
               Grid.SetColumn(span,0);
               Grid.SetColumnSpan(span,maxColumns);
               grid.Children.Add(span);
            }
            row++;
         }

         border.Child = grid;
         Content = border;
         MouseLeftButtonDown += (s,e) => Close();
         PreviewKeyDown += ToastWindow_PreviewKeyDown;
         if (timeoutMs > 0) {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(timeoutMs) };
            _timer.Tick += (s,e) => { _timer.Stop(); Close(); };
         }
      }

      private void ToastWindow_PreviewKeyDown(object sender,KeyEventArgs e) {
         if (e.Key == Key.Escape) {
            Close();
            e.Handled = true;
         }
      }

      protected override void OnContentRendered(EventArgs e) {
         base.OnContentRendered(e);
         _timer?.Start();
         Focus(); // Ensure window can receive key events
      }

      public static void ShowToast(string message,int timeoutMs = 0) {
         ToastWindow toast = new(message,timeoutMs) {
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
         };
         toast.Show();
      }

      /// <summary>
      /// Show the toast, run the action. Keep the toast up as long as the action runs, but eat leat the specified minimum show interval.
      /// Can't be shown relative to the main window, so it will be centered on the screen.
      /// </summary>
      /// <param name="message"></param>
      /// <param name="action"></param>
      /// <param name="minShowInterval"></param>
      public static void ShowToast(string message, Action action, int minShowInterval = 0) {
         var actionDone = new ManualResetEvent(false);
         Thread toastThread = new(() => {
            ToastWindow toast = new ToastWindow(message, minShowInterval) {
               WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            toast.Show();

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(minShowInterval) };
            timer.Tick += (s, e) => {
               timer.Stop();
               actionDone.WaitOne();
               toast.Close();
               Dispatcher.CurrentDispatcher.InvokeShutdown();
            };
            timer.Start();

            if (minShowInterval == 0) {
               actionDone.WaitOne();
               toast.Close();
               Dispatcher.CurrentDispatcher.InvokeShutdown();
            }

            Dispatcher.Run();
         });

         toastThread.SetApartmentState(ApartmentState.STA);
         toastThread.Start();

         action();
         actionDone.Set();
      }
   }
}