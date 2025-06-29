// <auto-gen>
//=======================================================================
// <copyright file="EmitterDebug.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-03-02</creation-date>
// 
// <summary>
//   This emitter writes output to the Visusal Studio Debug Console. Can be used in conjuction with another emitter.
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
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal class EmitterDebug : Emitter {
      private static readonly EmitterDebug Instance = new();
      public EmitterDebug() {
         Target = "Debug";
         SupressDebug = true;
      }
      protected override void WriteLine(string line) => Debug.WriteLine(LinePrefix+line.Replace("\n","\n"+LinePrefix));
      public static void WriteDebug(string line) => Instance.WriteLine(Instance.RemoveSpans(line));
   }
}

