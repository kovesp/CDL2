using System;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;

namespace CDL2v1 {
   internal class LexicalAnalyzer {
      public static List<Token> Tokenize(string filePath) {
         List<Token> tokens = new List<Token>();
         string remainingContent = File.ReadAllText(filePath);

         while (!string.IsNullOrEmpty(remainingContent)) {
            if (Token.TryCreateToken(ref remainingContent,out Token token)) {
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
