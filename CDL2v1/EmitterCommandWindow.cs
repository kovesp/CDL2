// <auto-gen>
//=======================================================================
// <copyright file="EmitterCommandWindow.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-06-29</creation-date>
// 
// <summary>
//   The emitter used to produced output in the command windows. Supports colring and formatting of the output.
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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Diagnostics;

namespace CDL2v1 {
   /// <summary>
   /// An Emitter that outputs to the CommandPromptWindow output area
   /// </summary>
   internal class EmitterCommandWindow : Emitter {
      private readonly CommandPromptWindow commandWindow;
      private readonly Dictionary<string, Brush> colorMap = new();
      private FontFamily? textFont;
      private bool isRenderingSuspended = false;
      private int batchDepth = 0;
      private readonly List<FormattedTextSegment> textSegmentBuffer = new();

      // Background brush for all output
      private readonly Brush windowBackground = new SolidColorBrush(Colors.DarkSlateGray);

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

      public EmitterCommandWindow(CommandPromptWindow commandWindow) {
         this.commandWindow = commandWindow;
         SupportsDecoration = true;
         
         // Create brushes for each color used by the pretty printer
         foreach (string color in PrettyPrinter.UsedColors()) {
            colorMap[color] = new BrushConverter().ConvertFromString(color) as Brush ?? Brushes.Black;
         }
         textFont = new("Cascadia Mono");

         // Override default background to use window background
         colorMap["Background"] = windowBackground;
         
         // Use the Other foreground but our window background
         colorMap["Foreground"] = colorMap[PrettyPrinter.Decorators[SE.Other].FG];

         // Configure the output area
         commandWindow.ConfigureFormattedOutput();
      }

      /// <summary>
      /// Write the item to the window using formatted text with spans
      /// </summary>
      protected override void WriteLine(string item) {
         int lastIndex = 0;

         foreach (Match match in spanRegex.Matches(item)) {
            if (match.Index > lastIndex) {
               AppendText(item[lastIndex..match.Index], colorMap["Foreground"], windowBackground);
            }

            string fg = match.Groups["fg"].Value;
            string bg = match.Groups["bg"].Value;
            string style = match.Groups["style"].Value;
            string text = match.Groups["text"].Value;

            Brush fgBrush = string.IsNullOrEmpty(fg) ? colorMap["Foreground"] : colorMap[fg];
            
            // Always use windowBackground unless explicitly overridden
            Brush bgBrush = string.IsNullOrEmpty(bg) ? windowBackground : 
                            (bg == "Background" ? windowBackground : colorMap[bg]);
            
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

            AppendText(text, fgBrush, bgBrush, fontWeight, fontStyle, textDecorations);
            lastIndex = match.Index + match.Length;
         }

         if (lastIndex < item.Length) AppendText(item[lastIndex..], colorMap["Foreground"], windowBackground);
         AppendText("", colorMap["Foreground"], windowBackground, lineBreak: true);
      }

      private void AppendText(string text, Brush fg, Brush bg,
                           FontWeight fontWeight = default, FontStyle fontStyle = default,
                           TextDecorationCollection? textDecorations = null,
                           bool lineBreak = false) {
         if (isRenderingSuspended) {
            // When rendering is suspended, just add to the buffer
            textSegmentBuffer.Add(new FormattedTextSegment(
               text, fg, bg, fontWeight, fontStyle, textDecorations, lineBreak
            ));
            return;
         }

         // Normal (non-buffered) rendering
         Application.Current.Dispatcher.Invoke(() => {
            commandWindow.AddFormattedText(text, fg, bg, fontWeight, fontStyle, textDecorations, lineBreak);
         });
      }

      // Call this before making multiple updates
      public override void BeginUpdate() {
         batchDepth++;
         if (batchDepth == 1) {
            isRenderingSuspended = true;
            textSegmentBuffer.Clear();

            Application.Current.Dispatcher.Invoke(() => {
               commandWindow.BeginFormattedUpdate();
            });
         }
      }

      // Call this after completing updates
      public override void EndUpdate() {
         if (batchDepth > 0) {
            batchDepth--;
            if (batchDepth == 0) {
               isRenderingSuspended = false;

               Application.Current.Dispatcher.Invoke(() => {
                  // Apply all buffered content in one UI update
                  FlushTextSegmentBuffer();
                  commandWindow.EndFormattedUpdate();
               });
            }
         }
      }

      // For very large documents, add a method to add intermediate UI updates
      public override void UpdateUI() {
         if (!isRenderingSuspended) return;
         Application.Current.Dispatcher.Invoke(() => {
            FlushTextSegmentBuffer();
            commandWindow.UpdateFormattedUI();
         });
      }

      // Method to flush all buffered text segments to the UI
      private void FlushTextSegmentBuffer() {
         if (textSegmentBuffer.Count == 0) return;

         // Apply all text segments in the buffer to the TextBlock
         foreach (FormattedTextSegment segment in textSegmentBuffer) {
            commandWindow.AddFormattedText(
               segment.Text,
               segment.Foreground,
               segment.Background,
               segment.Weight != default ? segment.Weight : FontWeights.Normal,
               segment.Style != default ? segment.Style : FontStyles.Normal,
               segment.Decorations,
               segment.LineBreak
            );
         }

         // Clear the buffer after applying
         textSegmentBuffer.Clear();
      }
   }
}

