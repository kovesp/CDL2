// <auto-gen>
//=======================================================================
// <copyright file="LexAnal.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-02-10</creation-date>
// 
// <summary>
//   The lexical Analyzer. Just the wrapper for invokeing the Tokenizer over an input stream.
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
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;

namespace CDL2v1 {
   internal partial class LexicalAnalyzer(CDL2 compiler, TokenList tokens) : CompilationPhase(compiler) {

      public string Tokenize(string input) {
         while (!string.IsNullOrEmpty(input)) {
            if (Token.TryCreateToken(ref input,out Token token)) {
               tokens.Add(token);
            } else {
               Match match = InvalidTokenSkipRE().Match(input);
               if (match.Success) {
                  AddNote(Note.InvalidToken,match.Value);
                  input = input[match.Length..];
               }
            }
         }

         return input;
      }

      [GeneratedRegex(@"^((?:[^"".]+|""(?:[^""$]|(\$.))*"")*?)\.", RegexOptions.Compiled)]
      private static partial Regex InvalidTokenSkipRE();
   }
}

