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
//   Base class of taget code generators providing some simple support methods.
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
   internal abstract partial class TargetCodeGenerator {

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

      /// <summary>
      /// Return true if the macro contains multiple statements separated by the given separator.
      /// </summary>
      /// <param name="macro"></param>
      /// <param name="separator"></param>
      /// <returns></returns>
      protected static bool HasMultipleStatments(Macro macro, string separator = ";") {
         Regex regex = BuildSeparatorRegex(separator);
         return macro.elements.OfType<STRING>().Any(str => regex.IsMatch(str.value));
      }

      /// <summary>
      /// Splits the elements of a macro into two groups based on the last occurrence of a specified separator.
      /// </summary>
      /// <remarks>The separator is matched against string elements using a regular expression. This method
      /// does not modify the original macro or its elements.</remarks>
      /// <param name="macro">The macro whose elements are to be split. Must not be null.</param>
      /// <param name="separator">The string value used to identify the separator element. Defaults to ";" if not specified.</param>
      /// <returns>A tuple containing two lists: the first list includes all elements up to and including the last separator, and
      /// the second list contains all elements following the last separator. If no separator is found, the first list
      /// is empty and the second contains all elements.</returns>
      public static (List<IElement> beforeLast, List<IElement> lastExpression) SplitMacroBody(Macro macro, string separator = ";") {
         if (macro.elements.Count == 0) return ([], []);
         
         Regex regex = BuildSeparatorRegex(separator);
         
         int lastSeparatorIndex = -1;
         for (int i = macro.elements.Count - 1; i >= 0; i--) {
            if (macro.elements[i] is STRING str && regex.IsMatch(str.value)) {
               lastSeparatorIndex = i;
               break;
            }
         }
         
         List<IElement> beforeLast = [];
         List<IElement> lastExpression = [];
         
         if (lastSeparatorIndex == -1) {
            lastExpression.AddRange(macro.elements);
         } else {
            for (int i = 0; i <= lastSeparatorIndex; i++) {
               beforeLast.Add(macro.elements[i]);
            }
            
            for (int i = lastSeparatorIndex + 1; i < macro.elements.Count; i++) {
               lastExpression.Add(macro.elements[i]);
            }
         }
         
         return (beforeLast, lastExpression);
      }

      private static Regex BuildSeparatorRegex(string separator) {
         string pattern = $@"(?<!['""])(?:\n|{Regex.Escape(separator)})(?![^'""]*['""])";
         return new Regex(pattern, RegexOptions.Compiled);
      }

      protected static readonly Random Random =  new();
      protected static string RandomInitialValue => Random.Next(0, int.MaxValue).ToString() + "  <# Random value to catch uninitialized VARs, LOCALs, and output AFFIXes #>";
      #endregion Helpers

   }
}

