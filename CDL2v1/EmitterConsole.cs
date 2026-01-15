// <auto-gen>
//=======================================================================
// <copyright file="EmitterConsole.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-01-15</creation-date>
// 
// <summary>
//   Console-based emitter for CDL2 output.
//   Writes formatted text to the console with color support.
// </summary>
//=======================================================================
// </auto-gen>

using System;
using System.Collections.Generic;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace CDL2v1 {
   /// <summary>
   /// Emitter that writes to the console with color support
   /// </summary>
   public class EmitterConsole : Emitter {
      private readonly ConsoleColor _defaultForeground = Console.ForegroundColor;
      private readonly ConsoleColor _defaultBackground = Console.BackgroundColor;
      private readonly Dictionary<string, ConsoleColor> _colorMap = [];

      public readonly bool SupressBackgroundColors = true;

      /// <summary>
      /// Initialize the console emitter
      /// </summary>
      public EmitterConsole() : base() {
         SupportsDecoration = true;
         
         // Build color map for all colors used by pretty printer
         foreach (string hexColor in PrettyPrinter.UsedColors()) {
            _colorMap[hexColor] = ParseHexToConsoleColor(hexColor, _defaultForeground);
         }
      }

      /// <summary>
      /// Write a line with span formatting to console
      /// </summary>
      protected override void WriteLine(string item) {
         int lastIndex = 0;

         foreach (Match match in spanRegex.Matches(item)) {
            if (match.Index > lastIndex) {
               Console.Write(item[lastIndex..match.Index]);
            }

            string fg = match.Groups["fg"].Value;
            string bg = match.Groups["bg"].Value;
            string style = match.Groups["style"].Value;
            string text = match.Groups["text"].Value;

            ApplyConsoleFormatting(fg, bg, style);
            Console.Write(text);
            ResetConsoleFormatting();

            lastIndex = match.Index + match.Length;
         }

         if (lastIndex < item.Length) Console.Write(item[lastIndex..]);
         Console.WriteLine();
      }

      /// <summary>
      /// Apply console color and style formatting
      /// </summary>
      private void ApplyConsoleFormatting(string fg, string bg, string style) {
         if (!string.IsNullOrEmpty(fg) && _colorMap.TryGetValue(fg, out ConsoleColor fgColor)) {
            Console.ForegroundColor = fgColor;
         }

         if (!SupressBackgroundColors && !string.IsNullOrEmpty(bg) && _colorMap.TryGetValue(bg, out ConsoleColor bgColor)) {
            Console.BackgroundColor = bgColor;
         }

         // Approximate bold with bright colors
         if (!string.IsNullOrEmpty(style) && style.Contains("bold", StringComparison.OrdinalIgnoreCase)) {
            if (Console.ForegroundColor < ConsoleColor.DarkGray) {
               Console.ForegroundColor = Console.ForegroundColor + 8;
            }
         }
      }

      /// <summary>
      /// Reset console formatting to defaults
      /// </summary>
      private void ResetConsoleFormatting() {
         Console.ForegroundColor = _defaultForeground;
         Console.BackgroundColor = _defaultBackground;
      }

      /// <summary>
      /// Parse hex color string to nearest ConsoleColor
      /// </summary>
      private static ConsoleColor ParseHexToConsoleColor(string hexColor, ConsoleColor defaultColor) {
         if (string.IsNullOrEmpty(hexColor) || !hexColor.StartsWith("#")) return defaultColor;

         try {
            string hex = hexColor.TrimStart('#');
            int r = Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = Convert.ToInt32(hex.Substring(4, 2), 16);
            
            return GetNearestConsoleColor(r, g, b);
         } catch {
            return defaultColor;
         }
      }

      /// <summary>
      /// Get the nearest ConsoleColor for RGB values
      /// </summary>
      private static ConsoleColor GetNearestConsoleColor(int r, int g, int b) {
         ConsoleColor result = ConsoleColor.Gray;
         double minDistance = double.MaxValue;

         foreach (ConsoleColor color in Enum.GetValues<ConsoleColor>()) {
            System.Drawing.Color systemColor = System.Drawing.Color.FromName(color.ToString());
            double distance = Math.Sqrt(
                Math.Pow(r - systemColor.R, 2) +
                Math.Pow(g - systemColor.G, 2) +
                Math.Pow(b - systemColor.B, 2));

            if (distance < minDistance) {
               minDistance = distance;
               result = color;
            }
         }

         return result;
      }
   }
}