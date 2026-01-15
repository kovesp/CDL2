// <auto-gen>
//=======================================================================
// <copyright file="EmitterAnsi.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-01-15</creation-date>
// 
// <summary>
//   ANSI-based emitter for CDL2 output.
//   Writes formatted text to console using ANSI escape codes with full RGB color support.
// </summary>
//=======================================================================
// </auto-gen>

using System;
using System.Text.RegularExpressions;

namespace CDL2v1 {
   /// <summary>
   /// Emitter that writes to console using ANSI escape codes for full RGB color support
   /// </summary>
   public class EmitterAnsi : Emitter {
      private const string ESC = "\x1b";
      private const string RESET = $"{ESC}[0m";

      public bool SuppressBackgroundColors { get; set; } = true;

      /// <summary>
      /// Initialize the ANSI emitter
      /// </summary>
      public EmitterAnsi() : base() {
         SupportsDecoration = true;
         EnableAnsiSupport();
      }

      /// <summary>
      /// Enable ANSI escape sequence support in Windows console
      /// </summary>
      private static void EnableAnsiSupport() {
         if (!Settings.OnWindows) return;

         try {
            IntPtr handle = GetStdHandle(-11); // STD_OUTPUT_HANDLE
            if (GetConsoleMode(handle, out uint mode)) {
               mode |= 0x0004; // ENABLE_VIRTUAL_TERMINAL_PROCESSING
               SetConsoleMode(handle, mode);
            }
         } catch {
            // Ignore if ANSI support cannot be enabled
         }
      }

      /// <summary>
      /// Write a line with span formatting to console using ANSI codes
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

            string ansiCodes = BuildAnsiCodes(fg, bg, style);
            Console.Write($"{ansiCodes}{text}{RESET}");

            lastIndex = match.Index + match.Length;
         }

         if (lastIndex < item.Length) Console.Write(item[lastIndex..]);
         Console.WriteLine();
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

      #region Windows Console API for ANSI support
      [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
      private static extern IntPtr GetStdHandle(int nStdHandle);

      [System.Runtime.InteropServices.DllImport("kernel32.dll")]
      private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

      [System.Runtime.InteropServices.DllImport("kernel32.dll")]
      private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
      #endregion
   }
}