using CDL2v1;

using System;
using System.IO;

internal class CDL2 {
   private static void Main(string[] args) {
      Console.WriteLine("CDL2 Compiler v1");
      Parser parser = new Parser();

      foreach (var arg in args) {
         string source = Path.GetFullPath(arg);
         if (File.Exists(source)) {
            Console.WriteLine($"   Compiling {source} ... Lexical analysis");
            List<Token> sourceTokens = LexicalAnalyzer.Tokenize(source);

            // Add the tokens comprising the file to the syntax tree
            parser.Parse(sourceTokens);
         }
      }

   }
}