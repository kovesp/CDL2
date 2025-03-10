// Ignore Spelling: CDL

using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CDL2v1 {
   internal class EmitterWindow : EmitterBase {
      private Window? window;
      private TextBlock? outputTextBlock;
      private Dictionary<string,Brush> colorMap = [];
      private FontFamily? symbolFont;

      private bool isRenderingSuspended = false;
      private int batchDepth = 0;
      private readonly List<FormattedTextSegment> textSegmentBuffer = [];

      // Structure to hold formatted text segments for batching
      private record FormattedTextSegment(
         string Text,
         Brush Foreground,
         Brush Background,
         FontWeight Weight = default,
         FontStyle Style = default,
         TextDecorationCollection? Decorations = null,
         bool LineBreak = false
      );

      public EmitterWindow() {
         SupportsDecoration = true;

         // Create and start a new STA thread for the WPF window
         Thread thread = new(() => {
            // Initialize the WPF application context
            Application app = new();

            // Create brushes for each color used by the pretty printer
            foreach (string color in PrettyPrinter.UsedColors()) {
               colorMap[color] = new BrushConverter().ConvertFromString(color) as Brush ?? Brushes.Black;
            }
            FontFamily symbolFont = new("Wingdings 3");

            colorMap["Foreground"] = colorMap[PrettyPrinter.Decorators[SE.Other].FG]; // Use SE.Other background
            colorMap["Background"] = colorMap[PrettyPrinter.Decorators[SE.Other].BG]; // Use SE.Other foreground

            window = new Window {
               Title = "Pretty Print Window",
               Width = 900,
               Height = (int)(SystemParameters.WorkArea.Height * 0.95),
               Top = 10,
               Foreground = colorMap["Foreground"],
               Background = colorMap["Background"],
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
                                    Margin = new Thickness(10),
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
         });

         thread.SetApartmentState(ApartmentState.STA);
         thread.Start();
         // Wait for the window to stabilize
         while (window == null) Thread.Sleep(100);
         while (outputTextBlock == null) Thread.Sleep(100);
      }

      public override void Close() {
         //window.Dispatcher.Invoke(() => window.Close());
      }

      /// <summary>
      /// Write the item to the window.
      /// The text may contain sequences of <spam fg="colorName" style="Normal|Bold|Italic|BoldItalic">text</spam>.
      /// </summary>
      /// <param id="item"></param>
      protected override void WriteLine(string item) {
         int lastIndex = 0;

         foreach (Match match in spanRegex.Matches(item)) {
            if (match.Index > lastIndex) {
               AppendText(item[lastIndex..match.Index],colorMap["Foreground"],colorMap["Background"]);
            }

            string fg = match.Groups["fg"].Value;
            string bg = match.Groups["bg"].Value;
            string style = match.Groups["style"].Value;
            string text = match.Groups["text"].Value;

            Brush fgBrush = string.IsNullOrEmpty(fg) ? colorMap["Foreground"] : colorMap[fg];
            Brush bgBrush = string.IsNullOrEmpty(bg) ? colorMap["Background"] : colorMap[bg];
            FontWeight fontWeight = FontWeights.Normal;
            FontStyle fontStyle = FontStyles.Normal;
            TextDecorationCollection? textDecorations = null;


            if (!string.IsNullOrEmpty(style)) {
               switch (style.ToLower()) {
                  case "normal":
                     break;
                  case "bold":
                     fontWeight = PrettyPrinter.Bold;
                     break;
                  case "italic":
                     fontStyle = PrettyPrinter.Italic;
                     break;
                  case "underline":
                     textDecorations = TextDecorations.Underline;
                     break;
                  case "bold, italic":
                     fontWeight = PrettyPrinter.Bold;
                     fontStyle = PrettyPrinter.Italic;
                     break;
                  case "bold, underline": {
                        fontWeight = PrettyPrinter.Bold;
                        textDecorations = TextDecorations.Underline;
                        break;
                     }
                  case "italic, underline": {
                        fontStyle = PrettyPrinter.Italic;
                        textDecorations = TextDecorations.Underline;
                        break;
                     }
                  case "bold, italic, underline": {
                        fontWeight = PrettyPrinter.Bold;
                        fontStyle = PrettyPrinter.Italic;
                        textDecorations = TextDecorations.Underline;
                        break;
                     }
               }
            }

            AppendText(text,fgBrush,bgBrush,fontWeight,fontStyle,textDecorations);
            lastIndex = match.Index + match.Length;
         }

         if (lastIndex < item.Length) AppendText(item[lastIndex..],colorMap["Foreground"],colorMap["Background"]);
         AppendText("",colorMap["Foreground"],colorMap["Background"],lineBreak: true);
      }

      private const char ThinSpace = '\u2009';
      private void AppendText(string text,Brush fg,Brush bg,
                              FontWeight fontWeight = default,FontStyle fontStyle = default,
                              TextDecorationCollection? textDecorations = null,
                              bool lineBreak = false) {
         // Ensure the window exists
         Debug.Assert(window != null,"Window is null");

         if (isRenderingSuspended) {
            // When rendering is suspended, just add to the buffer
            textSegmentBuffer.Add(new FormattedTextSegment(
               text,fg,bg,fontWeight,fontStyle,textDecorations,lineBreak
            ));
            return;
         }

         // Normal (non-buffered) rendering
         window.Dispatcher.Invoke(() => {
            text = Regex.Replace(text,@"( := | =: | = | : )\s*$",$"{ThinSpace}$1",
                 RegexOptions.IgnorePatternWhitespace);

            outputTextBlock?.Inlines.Add(new System.Windows.Documents.Run(text) {
               Foreground = fg,
               Background = bg,
               FontWeight = fontWeight,
               FontStyle = fontStyle,
               FontFamily = new FontFamily("Cascadia Code"),
               TextDecorations = textDecorations
            });

            if (lineBreak)
               outputTextBlock?.Inlines.Add(new System.Windows.Documents.LineBreak());
         },DispatcherPriority.Background);
      }

      // Call this before making multiple updates
      public override void BeginUpdate() {
         if (window == null) return;

         batchDepth++;
         if (batchDepth == 1) {
            isRenderingSuspended = true;
            textSegmentBuffer.Clear();

            window.Dispatcher.Invoke(() => {
               // Pause layout and rendering
               if (outputTextBlock != null) {
                  var scrollViewer = FindVisualParent<ScrollViewer>(outputTextBlock);
                  scrollViewer?.SetValue(ScrollViewer.CanContentScrollProperty,false);
               }
            },DispatcherPriority.Send);
         }
      }

      // Call this after completing updates
      public override void EndUpdate() {
         if (window == null) return;

         if (batchDepth > 0) {
            batchDepth--;
            if (batchDepth == 0) {
               isRenderingSuspended = false;

               window.Dispatcher.Invoke(() => {
                  // Apply all buffered content in one UI update
                  FlushTextSegmentBuffer();

                  // Re-enable scrolling and update layout
                  if (outputTextBlock != null) {
                     var scrollViewer = FindVisualParent<ScrollViewer>(outputTextBlock);
                     scrollViewer?.SetValue(ScrollViewer.CanContentScrollProperty,true);
                     outputTextBlock.UpdateLayout();
                  }
               },DispatcherPriority.Send);
            }
         }
      }

      // For very large documents, add a method to add intermediate UI updates
      public override void UpdateUI() {
         if (window == null || !isRenderingSuspended) return;
         window.Dispatcher.Invoke(() => {
            FlushTextSegmentBuffer();
            // Force a layout update
            outputTextBlock?.UpdateLayout();
         },DispatcherPriority.Normal);
      }

      // Method to flush all buffered text segments to the UI
      private void FlushTextSegmentBuffer() {
         if (outputTextBlock == null || textSegmentBuffer.Count == 0) return;

         // Apply all text segments in the buffer to the textblock
         foreach (FormattedTextSegment segment in textSegmentBuffer) {
            // Apply the regular expression transformation
            string text = Regex.Replace(segment.Text,
                @"( := | =: | = | : )\s*$",
                $"{ThinSpace}$1",
                RegexOptions.IgnorePatternWhitespace);

            // Add the formatted segment to the TextBlock
            outputTextBlock.Inlines.Add(new System.Windows.Documents.Run(text) {
               Foreground = segment.Foreground,
               Background = segment.Background,
               FontWeight = segment.Weight != default ? segment.Weight : FontWeights.Normal,
               FontStyle = segment.Style != default ? segment.Style : FontStyles.Normal,
               FontFamily = new FontFamily("Cascadia Code"),
               TextDecorations = segment.Decorations
            });

            if (segment.LineBreak)
               outputTextBlock.Inlines.Add(new System.Windows.Documents.LineBreak());
         }

         // Clear the buffer after applying
         textSegmentBuffer.Clear();
      }

      // Helper to find parent element of specific type
      private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject => VisualTreeHelper.GetParent(child) switch {
         null => null,
         T parent => parent,
         var parentObject => FindVisualParent<T>(parentObject),
      };
   }
}

