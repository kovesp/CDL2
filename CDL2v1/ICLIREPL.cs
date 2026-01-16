// <auto-gen>
//=======================================================================
// <copyright file="ICommandInterface.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-01-14</creation-date>
// 
// <summary>
//   Interface defining the contract for command interfaces.
//   Used by CommandInterpreter to abstract GUI vs console implementations.
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
   /// Interface for command-line interfaces (GUI or text-based).
   /// Abstracts the interaction between CommandInterpreter and the user interface.
   /// </summary>
   public interface ICLIREPL {
      /// <summary>
      /// Gets or sets the emitter used for formatted output.
      /// The emitter handles the actual formatting (GUI-based or text-based).
      /// </summary>
      Emitter? Emitter { get; set; }

      /// <summary>
      /// Writes a line of text with optional severity indication.
      /// </summary>
      /// <param name="text">The text to write.</param>
      /// <param name="severity">Severity level for formatting/coloring.</param>
      void WriteLine(string text,Severity severity = Severity.NONE);

      /// <summary>
      /// Updates the status display with the given message.
      /// In console mode, this might just write to a status line or do nothing.
      /// </summary>
      /// <param name="message">The status message to display.</param>
      void SetStatus(string message);

      /// <summary>
      /// Enters edit mode with the specified text.
      /// In GUI mode, switches to multi-line editing.
      /// In console mode, prompts for multi-line input.
      /// </summary>
      /// <param name="text">The initial text to edit (empty for new entry).</param>
      void EditText(string text = "");

      /// <summary>
      /// Prompts the user with a yes/no question.
      /// </summary>
      /// <param name="message">The question to ask.</param>
      /// <returns>True if user confirms, false otherwise.</returns>
      bool QueryBox(string message);

      /// <summary>
      /// Sets the input line processor for this REPL
      /// </summary>
      /// <param name="inputProcessor"></param>
      void SetInputProcessor(Action<string> inputProcessor);

      /// <summary>
      /// Opens the command interface starting the REPL or GUI loop.
      /// </summary>
      void Open();

      /// <summary>
      /// Closes/exits the command interface.
      /// </summary>
      void Close();
   }
}