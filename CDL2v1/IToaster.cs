// <auto-gen>
//=======================================================================
// <copyright file="IToaster.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-01-14</creation-date>
// 
// <summary>
//   Interface for displaying toast notifications.
//   Abstracts toast notification functionality for GUI and console implementations.
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
   /// Interface for displaying toast notifications.
   /// Provides methods for showing temporary messages to the user.
   /// </summary>
   public interface IToaster {
      /// <summary>
      /// Shows a toast notification with the specified message.
      /// </summary>
      /// <param name="message">The message to display.</param>
      /// <param name="timeoutMs">Timeout in milliseconds to display the toast. Default is 0 (no timeout).</param>
      /// <param name="delay">If true, delays showing the toast. Default is false.</param>
      void ShowToast(string message,int timeoutMs = 0,bool delay = false,bool setOwner = true);

      /// <summary>
      /// Shows a toast notification with the specified message and executes an action.
      /// </summary>
      /// <param name="message">The message to display.</param>
      /// <param name="action">Action to execute while showing the toast.</param>
      /// <param name="minShowInterval">Minimum time in milliseconds to keep toast visible. Default is 0.</param>
      /// <param name="noShow">If true, the toast will not be shown. Default is false.</param>
      void ShowToast(string message,Action action,int minShowInterval = 0,bool noShow = false);
   }
}