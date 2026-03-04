// <auto-gen>
//=======================================================================
// <copyright file="EmitterAnsiString.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-01-20</creation-date>
// 
// <summary>
//   ANSI-based emitter that writes to a string with full RGB color support.
//   Combines the string accumulation of EmitterString with the ANSI color
//   support of EmitterAnsi.
// </summary>
//=======================================================================
// </auto-gen>

using System.Text;
using System.Text.RegularExpressions;

namespace CDL2v1 {
   /// <summary>
   /// Emitter that writes to a string using ANSI escape codes for full RGB color support.
   /// The accumulated output can be retrieved via the Content property.
   /// </summary>
   internal class EmitterAnsiString : Emitter {
      private const string ESC = "\x1b";
      private const string RESET = $"{ESC}[0m";

      private readonly string prefix = "";
      private readonly string suffix = "";
      private readonly StringBuilder sb = new();

      public bool SuppressBackgroundColors { get; set; } = true;

      /// <summary>
      /// Initialize the ANSI string emitter with optional prefix and suffix for each line
      /// </summary>
      /// <param name="prefix">Prefix to add to each line (e.g., "// " for comments)</param>
      /// <param name="suffix">Suffix to add to each line</param>
      public EmitterAnsiString(string prefix = "", string suffix = "") : base() {
         this.prefix = prefix;
         this.suffix = suffix;
         SupportsDecoration = true;
         SuppressDebug = true;
      }

      /// <summary>
      /// Clear the accumulated content
      /// </summary>
      public override void Clear() => sb.Clear();

      /// <summary>
      /// Write a line with span formatting to the string builder using ANSI codes
      /// </summary>
      protected override void WriteLine(string item) {
         // Build ANSI-styled output
         StringBuilder output = new();
         int lastIndex = 0;

         foreach (Match match in spanRegex.Matches(item)) {
            if (match.Index > lastIndex) {
               output.Append(item[lastIndex..match.Index]);
            }

            string fg = match.Groups["fg"].Value;
            string bg = match.Groups["bg"].Value;
            string style = match.Groups["style"].Value;
            string text = match.Groups["text"].Value;

            string ansiCodes = BuildAnsiCodes(fg, bg, style);
            output.Append($"{ansiCodes}{text}{RESET}");

            lastIndex = match.Index + match.Length;
         }

         if (lastIndex < item.Length) output.Append(item[lastIndex..]);

         // Append to string builder with prefix and suffix
         sb.Append(prefix).Append(output).AppendLine(suffix);
      }

      /// <summary>
      /// Build ANSI escape codes for the given formatting
      /// </summary>
      private string BuildAnsiCodes(string fg, string bg, string style) {
         string codes = "";

         if (!string.IsNullOrEmpty(fg)) {
            (int r, int g, int b) = ParseHexColor(fg);
            codes += $"{ESC}[38;2;{r};{g};{b}m";
         }

         if (!SuppressBackgroundColors && !string.IsNullOrEmpty(bg)) {
            (int r, int g, int b) = ParseHexColor(bg);
            codes += $"{ESC}[48;2;{r};{g};{b}m";
         }

         if (!string.IsNullOrEmpty(style)) {
            if (style.Contains("bold", StringComparison.OrdinalIgnoreCase)) codes += $"{ESC}[1m";
            if (style.Contains("italic", StringComparison.OrdinalIgnoreCase)) codes += $"{ESC}[3m";
            if (style.Contains("underline", StringComparison.OrdinalIgnoreCase)) codes += $"{ESC}[4m";
         }

         return codes;
      }

      /// <summary>
      /// Parse hex color string to RGB values
      /// </summary>
      private static (int r, int g, int b) ParseHexColor(string hexColor) {
         if (string.IsNullOrEmpty(hexColor) || !hexColor.StartsWith("#")) return (255, 255, 255);

         try {
            string hex = hexColor.TrimStart('#');
            int r = Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = Convert.ToInt32(hex.Substring(4, 2), 16);
            return (r, g, b);
         } catch {
            return (255, 255, 255);
         }
      }

      /// <summary>
      /// Get the accumulated content with ANSI codes and clear the buffer.
      /// This follows the pattern of EmitterString.
      /// </summary>
      public override string Content {
         get {
            try {
               return sb.ToString();
            } finally {
               Clear();
            }
         }
      }
   }
}
