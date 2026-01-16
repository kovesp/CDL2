// <auto-gen>
//=======================================================================
// <copyright file="ToastConsole.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-01-14</creation-date>
// 
// <summary>
//   Console implementation of IToaster interface.
//   Displays toast messages directly to the console output.
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


namespace CDL2v1 {
   /// <summary>
   /// Console-based toast notification implementation.
   /// Displays messages to standard output, suitable for non-GUI environments.
   /// </summary>
   public class ToastConsole : IToaster {
      private readonly object lockObject = new();

      /// <summary>
      /// Shows a toast notification by writing to console output.
      /// </summary>
      /// <param name="message">The message to display.</param>
      /// <param name="timeoutMs">Timeout in milliseconds (ignored in console mode).</param>
      /// <param name="delay">If true, adds a brief pause after displaying.</param>
      public void ShowToast(string message,int timeoutMs = 0,bool delay = false,bool setOwner = false) {
         lock (lockObject) {
            ConsoleColor previousColor = Console.ForegroundColor;
            try {
               Console.ForegroundColor = ConsoleColor.Cyan;
               Console.WriteLine($"[TOAST] {message}");
               if (delay && timeoutMs > 0) Thread.Sleep(Math.Min(timeoutMs,2000));
            } finally {
               Console.ForegroundColor = previousColor;
            }
         }
      }

      /// <summary>
      /// Shows a toast notification and executes an action.
      /// </summary>
      /// <param name="message">The message to display.</param>
      /// <param name="action">Action to execute while toast is displayed.</param>
      /// <param name="minShowInterval">Minimum show interval in milliseconds (ignored in console mode).</param>
      public void ShowToast(string message,Action action,int _) {
         lock (lockObject) {
            ConsoleColor previousColor = Console.ForegroundColor;
            try {
               Console.ForegroundColor = ConsoleColor.Cyan;
               Console.WriteLine($"[TOAST] {message}");
               Console.ForegroundColor = previousColor;

               action?.Invoke();
            } catch (Exception ex) {
               Console.ForegroundColor = ConsoleColor.Red;
               Console.WriteLine($"[ERROR] Toast action failed: {ex.Message}");
            } finally {
               Console.ForegroundColor = previousColor;
            }
         }
      }
   }
}