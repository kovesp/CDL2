using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CDL2v1 {
   internal class EmitterWindow : EmitterBase {
      private Window window;
      private TextBlock outputTextBlock;

      public EmitterWindow() {
         SupportsDecoration = true;

         // Create and start a new STA thread for the WPF window
         Thread thread = new Thread(() => {
            // Initialize the WPF application context
            Application app = new Application();

            window = new Window {
               Title = "Pretty Print Window",
               Width = 800,
               Height = 900,
               Content = new Grid {
                  RowDefinitions = {
                            new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                            new RowDefinition { Height = GridLength.Auto }
                        },
                  Children = {
                            new ScrollViewer {
                                Content = outputTextBlock = new TextBlock {
                                    FontSize = 14,
                                    TextWrapping = TextWrapping.Wrap,
                                    Margin = new Thickness(10)
                                }
                            },
                            new Button {
                                Content = "Close",
                                Margin = new Thickness(10),
                                HorizontalAlignment = HorizontalAlignment.Right,
                                VerticalAlignment = VerticalAlignment.Bottom
                            }
                        }
               }
            };

            // Set the button click event handler
            ((Button)((Grid)window.Content).Children[1]).Click += (s,e) => window.Close();

            // Handle the window closed event to exit the application
            window.Closed += (s,e) => app.Shutdown();

            // Show the window and start the dispatcher
            
            app.Run(window);
            //System.Windows.Threading.Dispatcher.Run();
         });

         thread.SetApartmentState(ApartmentState.STA);
         thread.Start();
      }

      public override void Close() {
         //window.Dispatcher.Invoke(() => window.Close());
      }

      /// <summary>
      /// Write the item to the window.
      /// The text may contain sequences of <spam color="colorName" style="Normal|Bold|Italic|BoldItalic">text</spam>.
      /// </summary>
      /// <param name="item"></param>
      protected override void WriteLine(string item) {
         Regex spanRegex = new Regex(@"<span\s*(color='(?<color>[^']*)')?\s*(style='(?<style>[^']*)')?\s*>(?<text>.*?)<\/span>",RegexOptions.IgnoreCase);
         int lastIndex = 0;

         foreach (Match match in spanRegex.Matches(item)) {
            if (match.Index > lastIndex) {
               AppendText(item.Substring(lastIndex,match.Index - lastIndex),Brushes.Black);
            }

            var color = match.Groups["color"].Value;
            var style = match.Groups["style"].Value;
            var text = match.Groups["text"].Value;

            Brush brush = Brushes.Black;
            if (!string.IsNullOrEmpty(color)) {
               brush = (Brush)new BrushConverter().ConvertFromString(color);
            }

            FontWeight fontWeight = FontWeights.Normal;
            FontStyle fontStyle = FontStyles.Normal;
            TextDecorationCollection textDecorations = null;

            if (!string.IsNullOrEmpty(style)) {

               switch (style.ToLower()) {
                  case "Normal":
                     break;
                  case "Bold":
                     fontWeight = FontWeights.Bold;
                     break;
                  case "Italic":
                     fontStyle = FontStyles.Italic;
                     break;
                  case "Underline":
                     textDecorations = TextDecorations.Underline;
                     break;
                  case "Bold, Italic":
                     fontWeight = FontWeights.Bold;
                     fontStyle = FontStyles.Italic;
                     break;
                  case "Bold, Underline": {
                        fontWeight = FontWeights.Bold;
                        textDecorations = TextDecorations.Underline;
                        break;
                     }
                  case "Italic, Underline": {
                        fontStyle = FontStyles.Italic;
                        textDecorations = TextDecorations.Underline;
                        break;
                     }
                  case "Bold, Italic, Underline": {
                        fontWeight = FontWeights.Bold;
                        fontStyle = FontStyles.Italic;
                        textDecorations = TextDecorations.Underline;
                        break;
                     }
               }
            }

            AppendText(text,brush,fontWeight,fontStyle,textDecorations);
            lastIndex = match.Index + match.Length;
         }

         if (lastIndex < item.Length) {
            AppendText(item.Substring(lastIndex),Brushes.Black);
         }
         AppendText("",Brushes.Black,lineBreak: true);
      }

      private void AppendText(string text,Brush color,FontWeight fontWeight = default,FontStyle fontStyle = default,TextDecorationCollection textDecorations = null,bool lineBreak = false) {
         while (window == null) Thread.Sleep(100); // Wait for the window to be created
                                                   // Ensure the operation is performed on the UI thread
         window.Dispatcher.Invoke(() => {
            var run = new System.Windows.Documents.Run(text) { Foreground = color,FontWeight = fontWeight,FontStyle = fontStyle,TextDecorations = textDecorations };
            outputTextBlock.Inlines.Add(run);
            if (lineBreak) outputTextBlock.Inlines.Add(new System.Windows.Documents.LineBreak());
         });
      }
   }
}

