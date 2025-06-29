// <auto-gen>
//=======================================================================
// <copyright file="EmitterString.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-03-25</creation-date>
// 
// <summary>
//   Writes output to a string. Used by the code generators to write formatted code comments to the output and by the command interpreter
//   to write code output to the command window.
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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal class EmitterString : Emitter {

      private string prefix = "";
      private string suffix = "";
      public EmitterString(string prefix = "", string suffix = "") {
         this.prefix = prefix;
         this.suffix = suffix;
         SupressDebug = true;
      }

      private readonly StringBuilder sb = new();
      //private string buffer = "";

      protected override void WriteLine(string line) => sb.Append(prefix).Append(line).AppendLine(suffix);

      public override string Content {
         get {
            try {
               return sb.ToString();
            }
            finally {
               sb.Clear();
            }
         }
      }
      //protected override void WriteLine(string item) => buffer += prefix + item + suffix + "\n";
      //public override string Content { get {
      //      string s = buffer;
      //      buffer = "";
      //      return s;
      //   }
      //}
   }
}

