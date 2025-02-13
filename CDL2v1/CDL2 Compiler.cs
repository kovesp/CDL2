using CDL2v1;

using System;
using System.IO;
using System.CommandLine;
using System.CommandLine.Invocation;

internal class CDL2 {
   public static string Version = "1.0.0";

   public static CDL2 Compiler;

   public int VerbosityLevel { get; set; }

   static CDL2() {
      Compiler = new CDL2();
   }

   private static void Main(string[] args) {
      Console.WriteLine($"CDL2 Compiler v{Version}");

      // Define the root command with options
      var rootCommand = new RootCommand {
            new Option<string[]>(
                "--sources",
                description: "The source files to compile"),
            new Option<int>(
                new string[] { "-v", "--verbose" },
                getDefaultValue: () => 0,
                description: "Set the verbosity level (0-3)")
      };

      rootCommand.Description = "CDL2 Compiler";


      // Set the handler for the root command
      rootCommand.SetHandler((string[] sources) => Compiler.CompileSources(sources),new Option<string[]>("--sources"));

      // Invoke the command handler
      rootCommand.Invoke(args);
   }

   public Parser? Parser;
   public SemanticAnalyzer? semanticAnalyzer;
   public CodeGenerator? codeGenerator;
   public void CompileSources(string[] args) {
      Parser = new Parser();
      foreach (var arg in args) {
         string source = Path.GetFullPath(arg);
         if (File.Exists(source)) {
            Console.WriteLine($"   Compiling {source} ... Lexical analysis");
            TokenList sourceTokens = LexicalAnalyzer.Tokenize(source);

            // Add the tokens comprising the file to the syntax tree
            Parser.Parse(sourceTokens);
         }
      }

      // Perform semantic checks
      semanticAnalyzer = new SemanticAnalyzer();
      if (Parser.program != null) {
         semanticAnalyzer.Analyze(Parser.program);
      }

      // Later choose from options which code generator to use, for now there is only one
      codeGenerator = new CodeGeneratorPowerShell();

      if (Parser.program != null) {
         codeGenerator.GenerateCode(Parser.program);
      } else {
         Console.WriteLine("No program found in the source files");
      }
   }

   /// <summary>
   /// Called by <see cref="Logger.ReportError"/> to skip to the next END token."/>
   /// </summary>
   internal void SkipToNextEnd() {
      if (Parser != null) {
         Parser.SkipToNextEnd();
      }
   }
}
