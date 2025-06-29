// <auto-gen>
//=======================================================================
// <copyright file="TargetCodeGenerator.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-05-09</creation-date>
// 
// <summary>
//   Content description goes here.
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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal partial class TargetCodeGenerator {

      protected Emitter emitter = new EmitterSink();
      #region Helpers
      protected void Newline(bool optional = false) {
         if (optional) emitter.EmitnlOption(); else emitter.Emitnl();
      }
      protected void EmitUnitStartComment(Container unit) => emitter.Emitnl($"# Begin {unit.ContainerName}");
      protected void EmitUnitEndComment(Container unit) => emitter.Emitnl($"# End {unit.ContainerName}");
      protected void GenerateComment(string comment) {
         foreach (string line in comment.Split('\n')) emitter.Emitnl("# ", line);
      }

      public void IncrementIndent() => emitter.IndentLevel++;
      public void DecrementIndent() => emitter.IndentLevel--;

      protected static bool HasMultipleStatments(Macro macro) => macro.elements.OfType<STRING>().Any(str => MatchMultipleStatementsRegex().IsMatch(str.value));

      protected static readonly Random Random =  new();
      protected static string RandomInitialValue => Random.Next(0, int.MaxValue).ToString() + "  <# Random value to catch uninitialized VARs, LOCALs, and output AFFIXes #>";

      [GeneratedRegex(@"(?<!['""])(?:\n|;)(?![^'""]*['""])", RegexOptions.Compiled)]
      private static partial Regex MatchMultipleStatementsRegex();
      #endregion Helpers

   }
}

