// <auto-gen>
//=======================================================================
// <copyright file="EmitterFile.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-03-02</creation-date>
// 
// <summary>
//   This emitter writes content to a file. Used by the target specific code generators to write the target code to a file.
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

using System.Diagnostics;
using System.IO;

namespace CDL2v1 {
   internal class EmitterFile : Emitter {
      private StreamWriter? writer = null;
      private string? target = null;

      /// <summary>
      /// The target file Id. Setting this will close the current file and open a new one.
      /// The new one is opened only if the target is not null or empty.
      /// This will throw an exception if the file cannot be opened.
      /// </summary>
      public override string Target {
         get => target??"";
         set {
            writer?.Close();
            writer = null;
            target = value;
            if (target is not null && target != "") writer = new StreamWriter(value);            
         }
      }

      public EmitterFile() => Target = "";
      public EmitterFile(string targetFileName) => Target = targetFileName;
      ~EmitterFile() => Target = "";

      /// <summary>
      /// Write the item to the target file.
      /// </summary>
      /// <param Id="item"></param>
      protected override void WriteLine(string item) => writer?.WriteLine(item);

      public override void Close() {
         writer?.Flush();
         writer?.Close();
         writer = null;
      }

      /// <summary>
      /// Determines whether the underlying writer is currently open and available for writing.
      /// </summary>
      /// <returns>true if the writer is open; otherwise, false.</returns>
      public bool IsOpen() => writer is not null;
   }
}
