using CDL2v1;

using System;
using System.IO;
using System.CommandLine;
using System.CommandLine.Invocation;

internal class CDL2 {
   public static string Version = "1.0.0";

   public static CDL2 Compiler;

   public int VerbosityLevel { get; set; }
   public bool LineNumbers { get; set; }
   public string Target { get; set; } = "";

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
                ["-v", "--verbose"],
                getDefaultValue: () => 0,
                description: "Set the verbosity level (0-3)"),
            new Option<bool>(
                "--lineNumbers",
                getDefaultValue: () => true,
                description: "Add line numbers to the token stream"),
            new Option<string>(
                ["-t","--target"],
                getDefaultValue: () => "PowerShell",
                description: "Generate code for the specified target language. Default is PowerShell.")
      };

      rootCommand.Description = "CDL2 Compiler";

      // Set the handler for the root command
      rootCommand.SetHandler((string[] sources) => Compiler.CompileSources(sources),new Option<string[]>("--sources"));
      rootCommand.SetHandler((int verbosity) => Compiler.VerbosityLevel = verbosity,new Option<int>(["-v","--verbose"]));
      rootCommand.SetHandler((bool lineNumbers) => Compiler.LineNumbers = lineNumbers,new Option<bool>("--lineNumbers"));
      rootCommand.SetHandler((string target) => Compiler.Target = target,new Option<string>(["-t","--target"]));

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
         // TODO: If errors are found, null out the program object.
         semanticAnalyzer.Analyze(Parser.program);
      }

      ICodeGenerator? cg = CreateCodeGenerator(Target);
      if (Parser.program != null && cg != null) {
         codeGenerator = new CodeGenerator(cg);
         codeGenerator.GenerateCode(Parser.program);
      } else {
         Console.WriteLine("No program found in the source files");
      }
   }

   private ICodeGenerator? CreateCodeGenerator(string target) {
      try {
         string className = $"CDL2v1.CodeGenerator{target}";
         Type? type = Type.GetType(className);
         if (type != null && typeof(ICodeGenerator).IsAssignableFrom(type)) {
            return Activator.CreateInstance(type) as ICodeGenerator;
         }
      } catch (Exception ex) {
         Console.WriteLine($"Error creating code generator for target {target}: {ex.Message}");
      }
      return null;
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
