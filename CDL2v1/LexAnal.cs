using System;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;

namespace CDL2v1 {
   internal partial class LexicalAnalyzer(CDL2 compiler, TokenList tokens) : CompilationPhase(compiler) {
      public void Tokenize(string filePath) {
         string input = File.ReadAllText(filePath);

         while (!string.IsNullOrEmpty(input)) {
            if (Token.TryCreateToken(ref input,out Token token)) {
               tokens.Add(token);
            } else {
               Match match = InvalidTokenSkipRE().Match(input);
               if (match.Success) {
                  AddNote(Note.InvalidToken, match.Value);
                  input = input[match.Length..];
               }
            }
         }
      }

      [GeneratedRegex(@"^((?:[^"".]+|""(?:[^""$]|(\$.))*"")*?)\.", RegexOptions.Compiled)]
      private static partial Regex InvalidTokenSkipRE();
   }
}
