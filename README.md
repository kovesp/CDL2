# CDL2 Compiler V1

1. Tokenizes the CDL2 program (possibly contained in multiple files) into a list of tokens.
   - Tokens support retaining file and line # info, but not yet implemented.
   - Comments are retained as tokens, but are currently discarded before parsing.
2. Parses a CDL2 program into a syntax tree.
   - Error handling very primitive currently.
3. Semantic analysis framework created but does nothing now.
4. Syntax tree can be printed with the Pretty Printer.
5. Code Generation framework started.
   - Target code generator selectable via command line.
   - First codegenerator (for PowerShell) started.
