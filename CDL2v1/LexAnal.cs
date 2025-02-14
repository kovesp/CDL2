using System;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;

namespace CDL2v1 {
   internal class LexicalAnalyzer {
      public static TokenList Tokenize(string filePath) {
         TokenList tokens = new();

         string input = File.ReadAllText(filePath);

         while (!string.IsNullOrEmpty(input)) {
            if (Token.TryCreateToken(ref input,out Token token)) {
               tokens.Add(token);
            } else {
               // Handle error or invalid token
               break;
            }
         }
         return tokens;
      }
   }
}
