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
            TokenList sourceTokens = LexicalAnalyzer.Tokenize(source);

            // Add the tokens comprising the file to the syntax tree
            parser.Parse(sourceTokens);
         }
      }
      
      // Later choose from options which code generator to use, for now there is only one
      CodeGenerator codeGenerator = new CodeGeneratorPowerShell();

      if (parser.program != null) {
         codeGenerator.GenerateCode(parser.program);
      } else {
         Console.WriteLine("No program found in the source files");
      }

   }
}
