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

using System.Text.RegularExpressions;

namespace CDL2v1 {
   internal partial class LexicalAnalyzer(CDL2 compiler,TokenList tokens) : CompilationPhase(compiler) {

      /// <summary>
      /// Break the input string into tokens.
      /// If the input does not start with a valid token, report the error and skip
      /// <list type="bullet">
      /// <item>a single special character</item>
      /// <item>a run of lower case or upper case characters</item>
      /// <item>a run of digits</item>
      /// </list>
      /// </summary>
      /// <param name="input"></param>
      /// <returns></returns>
      public bool Tokenize(string input,ParseMode mode) {
         while (!string.IsNullOrEmpty(input)) {
            if (Token.TryCreateToken(ref input,out Token token)) {
               tokens.Add(token);
            } else {
               Match match = InvalidTokenSkipRE().Match(input);
               if (match.Success) {
                  input = input[match.Length..];
                  if (mode == ParseMode.Full) {
                     AddNote(Note.InvalidToken,match.Value);
                  } else {
                     return false; // In non-full mode, stop at the first error.
                  }
               }
            }
         }
         return true;
      }

      [GeneratedRegex(@"^\s*(?:\p{Ll}+)|(?:\p{Lu}+)|(?:\d+)|(?:[\p{P}\p{S}])",RegexOptions.Compiled)]
      private static partial Regex InvalidTokenSkipRE();
   }
}

