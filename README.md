# CDL2 Compiler \& Laboratory V1.1

1. In compiler mode parses a CDL2 program file into a syntax tree and generates code for PowerShell.
2. In Lab mode shows a GUI window where Lab commands may be entered and executed. Under development.

Note: I don't have any official documentation for CDL2 itself or the Lab apart from the monograph of
Maik Stahl and Kees Koster *Implementing Portable and Efficient Software in and Open-Ended Language*.
The rest is from memory and for the Lab, parallels from the MProlog PDSS subsystem which was based on
the Lab.

Version 1.1 is the first release to be made public.
* Implmentations for Windows and Linux.
  * Full GUI on Windows. 
  * Character-based GUI running in a terminal on Linux.
  * Both support pretty-printing with syntax-colouring. The Windows GUI implementation
    is noticably very slow on large amounts of output.
* A more or less completely functional CDL2 Laboratory. Exceptions:
  * None-working or broken commands: move, rename.
  * Some selectors will not work as expected, eg., list Sec sec Call should list all calls in all procedures int he section.
* Code Generators
  * Code generation is implemented only for Programs (i.e., not possible to generate code for individual modules).
  * PowerShell
    * Just run the generated *.ps1 file with pwsh.
  * C#
    * On Windows, use dotnet run *.cs
    * On Linux -- I did it once, need to document
  * C
    * On Windows use the included cc.cmd or cco.cmd tol generate an excutable from te generated C code.
    * On Linux use gcc.
    * The release includes CDL2Trace.h which is the CDL2 debugger for C. With appropriate code generation options
      the debugger will be invoked. For details see the LAB UG, generate command.
