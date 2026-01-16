// <auto-gen>
//=======================================================================
// <copyright file="EmitterWindow.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-03-02</creation-date>
// 
// <summary>
//   This is a stand-alone version of the pretty printer (invoked by the --pretty-print option) that displays output in a WPF window.
//   Mostly deprecated.
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

// Ignore Spelling: CDL
#if WINDOWS
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CDL2v1 {
   internal partial class EmitterWindow : Emitter {
      private Window? window;
      private TextBlock? outputTextBlock;
      private readonly Dictionary<string,Brush> colorMap = [];
      private FontFamily? symbolFont;
      private FontFamily? textFont;

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
            symbolFont = new("Wingdings 3");
            textFont = new("Cascadia Mono");

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
                            new StackPanel {
                                Orientation = Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Right,
                                VerticalAlignment = VerticalAlignment.Bottom,
                                Margin = new Thickness(10),
                                Children = {
                                    new Button {
                                        Content = "+",
                                        Width = 30,
                                        Height = 30,
                                        Margin = new Thickness(5)
                                    },
                                    new Button {
                                        Content = "-",
                                        Width = 30,
                                        Height = 30,
                                        Margin = new Thickness(5)
                                    },
                                    new Button {
                                        Content = "Close",
                                        Margin = new Thickness(5)
                                    }
                                }
                            }
                        }
               }
            };

            // Set the button click event handlers
            var buttonPanel = (StackPanel)((Grid)window.Content).Children[1];
            ((Button)buttonPanel.Children[0]).Click += (s,e) => ZoomIn();
            ((Button)buttonPanel.Children[1]).Click += (s,e) => ZoomOut();
            ((Button)buttonPanel.Children[2]).Click += (s,e) => window.Close();

            // Handle the window closed event to exit the application
            window.Closed += (s,e) => app.Shutdown();

            // Add mouse wheel event handler for zooming
            window.PreviewMouseWheel += Window_PreviewMouseWheel;

            // Show the window and start the dispatcher
            app.Run(window);
         });

         thread.SetApartmentState(ApartmentState.STA);
         thread.Start();
         // Wait for the window to stabilize
         while (window == null) Thread.Sleep(100);
         while (outputTextBlock == null) Thread.Sleep(100);
      }

      private void Window_PreviewMouseWheel(object sender,MouseWheelEventArgs e) {
         if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) {
            if (e.Delta > 0) {
               ZoomIn(10);
            } else {
               ZoomOut(10);
            }
            e.Handled = true;
         }
      }

      public override void Close() {
         //window.Dispatcher.Invoke(() => window.Close());
      }

      public override bool CanPauseUpdate => true;

      /// <summary>
      /// Write the item to the window.
      /// The text may contain sequences of <spam fg="colorName" style="Normal|Bold|Italic|BoldItalic">text</span>.
      /// </summary>
      /// <param Id="item"></param>
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
                     textDecorations = PrettyPrinter.Underline;
                     break;
                  case "bold, italic":
                     fontWeight = PrettyPrinter.Bold;
                     fontStyle = PrettyPrinter.Italic;
                     break;
                  case "bold, underline": {
                        fontWeight = PrettyPrinter.Bold;
                        textDecorations = PrettyPrinter.Underline;
                        break;
                     }
                  case "italic, underline": {
                        fontStyle = PrettyPrinter.Italic;
                        textDecorations = PrettyPrinter.Underline;
                        break;
                     }
                  case "bold, italic, underline": {
                        fontWeight = PrettyPrinter.Bold;
                        fontStyle = PrettyPrinter.Italic;
                        textDecorations = PrettyPrinter.Underline;
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
            AddRun(new(FormatAlgorithmBodySeparators(text)) {
               Foreground = fg,
               Background = bg,
               FontWeight = fontWeight,
               FontStyle = fontStyle,
               TextDecorations = textDecorations
            });

         },DispatcherPriority.Background);
      }

      private void AddRun(Run run,bool lineBreak = false) {
         run.FontFamily = textFont; ;
         outputTextBlock?.Inlines.Add(run);
         if (lineBreak) outputTextBlock?.Inlines.Add(new System.Windows.Documents.LineBreak());
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
                  ScrollViewer? scrollViewer = FindVisualParent<ScrollViewer>(outputTextBlock);
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
                     ScrollViewer? scrollViewer = FindVisualParent<ScrollViewer>(outputTextBlock);
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

         // Apply all text segments in the buffer to the text block
         foreach (FormattedTextSegment segment in textSegmentBuffer) {
            // Add the formatted segment to the TextBlock
            AddRun(new Run(FormatAlgorithmBodySeparators(segment.Text)) {
               Foreground = segment.Foreground,
               Background = segment.Background,
               FontWeight = segment.Weight != default ? segment.Weight : FontWeights.Normal,
               FontStyle = segment.Style != default ? segment.Style : FontStyles.Normal,
               TextDecorations = segment.Decorations
            });

            if (segment.LineBreak)
               outputTextBlock.Inlines.Add(new System.Windows.Documents.LineBreak());
         }

         // Clear the buffer after applying
         textSegmentBuffer.Clear();
      }

      private const char SeparatorSpace = (char)SpaceCharacters.ThreePerEm;

      private static string FormatAlgorithmBodySeparators(string text) => BodySeparatorRE().Replace(text,$"{SeparatorSpace}$1{SeparatorSpace}");

      // Helper to find parent element of specific type
      private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject => VisualTreeHelper.GetParent(child) switch {
         null => null,
         T parent => parent,
         var parentObject => FindVisualParent<T>(parentObject),
      };

      // Zoom in by increasing the font size by 20%
      private void ZoomIn(int pct = 20) {
         if (outputTextBlock != null) {
            outputTextBlock.FontSize *= (100 + pct) / 100.0;
         }
      }

      // Zoom out by decreasing the font size by 20%
      private void ZoomOut(int pct = 20) {
         if (outputTextBlock != null) {
            outputTextBlock.FontSize /= (100 + pct) / 100.0;
         }
      }

      [GeneratedRegex(@"\s*( := | =: | = | : )\s*$",RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace)]
      private static partial Regex BodySeparatorRE();
   }
}
#endif
