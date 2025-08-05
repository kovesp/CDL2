// <auto-gen>
//=======================================================================
// <copyright file="CommandInterpreter.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-06-10</creation-date>
// 
// <summary>
//   This is the laboratory command interpreter.
//   It is used to interpret lab commands and display results in the CommandWindow.
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CDL2v1 {
   public class CommandInterpreter {
      private readonly CommandPromptWindow? commandWindow;
      private readonly PrettyPrinter pp;
      private readonly Parser parser;

      public CommandInterpreter(CommandPromptWindow window) {
         commandWindow = window;
         // Create a CommandWindowEmitter that integrates with our window
         pp = new(new EmitterCommandWindow(commandWindow), includeComments: true);
         // Initialize the parser with the compiler and a callback for error messages
         parser = new Parser(CDL2.Compiler,(severity,msg,_)=>commandWindow.WriteLine($"{severity}: {msg}",severity));
      }

      public CommandInterpreter() {
         commandWindow = null;
         // Create a CommandWindowEmitter that integrates with our window
         pp = new(new EmitterDebug(),includeComments: true);
         // Initialize the parser with the compiler and a callback for error messages
         parser = new Parser(CDL2.Compiler,(severity,msg,_) => Debug.WriteLine($"{severity}: {msg}",severity));
      }

      public void SetStatus(string? status = null) {
         if (commandWindow is not null) {
            if (status is null) {
               commandWindow!.SetStatus("Nothing");
            } else {
               commandWindow.SetStatus(status);
            }
         }
      }
      public void SetStatus(NamedElement? element=null) {
         if (commandWindow is not null) {
            if (element is null) {
               commandWindow.SetStatus("Nothing");
            } else {
               commandWindow.SetStatus(element.FQDN());
            }
         }
      }

      public void EnterCode(string input) {
         parser.Tokenize(input);
         Debug.Assert(parser.tokens.Count > 0,"Lexical Analysis found no usable tokens in input.");

         if (parser.Parse(ParsingContext ?? Focus.Current,out NamedElement? element)) Focus.SetFocus(element!);
         ParsingContext = null; // Reset the parsing context after a parse
      }
      public bool EnterRawCode(string input) {
         string trimmed = input.Trim();
         if (char.IsAsciiLetterUpper(trimmed[0])) {
            string firstWord = trimmed.Split(' ','\t','\r','\n')[0];
            SelectorType type = Abbreviation<SelectorType>.Identify(firstWord.ToUpper());
            if (type != SelectorType.INVALID) {
               EnterCode($"{type} {input[firstWord.Length..]}");
               return true;
            }
         }
         return false;
      }

      Focus? ParsingContext = null;

      private void WriteLine(string message) {
         if (commandWindow is not null) {
            commandWindow.WriteLine(message);
         } else {
            Debug.WriteLine(message);
         }
      }
      private void WriteError(string message) => WriteLine("Error:" + message);
      private void WriteInfo(string message) => WriteLine("Info: " + message);
      private void WriteWarning(string message) => WriteLine("Warning: " + message);

      public void InterpretCommand(string command, CommandType commandType, string settings, string args) {
         IEnumerable<string> arguments = Regex.Split(command, @"\s+").Skip(1).Select(s=>s.Trim());
         //commandWindow.WriteLine($"> {commandType} {string.Join(" ",arguments)}");
         string[] parts;

         switch (commandType) {
#if DEBUG
            case CommandType.vsdebug:
               Debugger.Break();
               break;
#endif
            case CommandType.INVALID:
               WriteError($"Invalid command: {command}");
               return;
            case CommandType.focus:
               if (!Focus.SetFocus(args,out string errorMessage)) {
                  WriteError(errorMessage);
               }
               break;
            case CommandType.next:
               // Handle next command
               WriteLine("Next command executed");
               break;
            case CommandType.previous:
               // Handle previous command
               WriteLine("Previous command executed");
               break;
            case CommandType.list:
               if (args == "") {
                  if (Focus.Current.Object is not null) {
                     //TODO: Ignore Focus subojbest for now
                     WriteLine(Focus.Current.Object.FQDN());
                  } else {
                     WriteInfo($"Nothing");
                  }
               } else {
                  Selection selection = new(args);
                  if (selection.IsInvalid) {
                     WriteError(selection.ErrorMessage);
                     return;
                  }
                  if (selection.Count == 0) {
                     WriteLine(selection.ErrorMessage);
                  } else {
                     foreach (SingleSelection sel in selection) {
                        WriteLine(sel.Object!.FQDN());
                     }
                  }
               }
               break;
            case CommandType.print:
            case CommandType.type:
               if (args == "") {
                  if (Focus.Current.Object is not null) {
                     //TODO: Ignore Focus subobject for now
                     pp.PauseUpdate(() => pp.Print(Focus.Current.Object));
                  }
                  return;
               } else {
                  Selection selection = new(args);
                  if (selection.IsInvalid) {
                     WriteError(selection.ErrorMessage);
                     return;
                  }
                  pp.PauseUpdate(() => {
                     foreach (SingleSelection sel in selection) {
                        pp.Print(sel.Object!);
                     }
                  });
               }
               break;
            case CommandType.set:
               // Handle set command
               parts = command.Split(' ',3);
               if (parts.Length < 3) {
                  WriteInfo("Usage: set <key> <value>");
                  return;
               }
               string key = parts[1];
               string value = parts[2];
               // Set logic here
               WriteLine($"Set {key} to {value}");
               break;
            case CommandType.status:
               Reachable.LogObjectCount(CDL2.Compiler.Reachable.AllObjects,$"in {Database.Instance.Modules.Count.Plural("module")}",WriteInfo);
               break;
            case CommandType.rename:
               break;
            case CommandType.add:
            case CommandType.append:
               break;
            case CommandType.delete:
            case CommandType.remove:
               // Remove the NamedElement from NamedElements.
               // If it is a program or a module, remove it from the appropriate database list
               // If it is a Module, Layer or a Section, remove it from its container, and also remove all children.
               // If it is a Section, then remove all declarations and remove the synthetic procs generated for ludes.
               // In each case, update the ABSTR and EXT lists of the containig LAYER and the EXPORTS and IMPORTS of the containing module.
               // Rerun Semantic analysis to update the database.
               //
               // For now only handle the case when a program is being deleted: It has no children and is not contained in anything.
               SingleSelection? context = GetContext(args);
               if (context is null) return;
               switch (context.Object) {
                  case Program p:
                     Debug.Assert(p.Siblings.Contains(p.GUID),"{p} is not among its siblings.");
                     Focus.MoveFocusFrom(p); // Must move the focus first because it relies on p still being among the siblings.
                     Database.Instance.NamedElements.Remove(p.GUID);
                     Database.Instance.ElementsWithNotes.Remove(p.GUID);
                     // The above is what needs to be done for a single element. It then needs to be repeated for all children.
                     // ... Program doesn't have any, since Parts are not exactly children.
                     // CDL2.Compiler.SemanticAnalyzer!.Analyze(p);
                     WriteInfo($"{p.FQDN()} removed");
                     break;
                  case Module m:
                     WriteInfo("Not implemented.");
                     break;
                  case Layer l:
                     WriteInfo("Not implemented.");
                     break;
                  case Section s:
                     WriteInfo("Not implemented.");
                     break;
                  case CDL2Object c:
                     WriteInfo("Not implemented.");
                     break;
                  default:
                     WriteError($"Cannot delete {Focus.Current.Object?.FQDN() ?? "<unknown>"}");
                     return;
               }
               break;
            case CommandType.edit:
               break;
            case CommandType.insert:
               break;
            case CommandType.replace:
               break;
            case CommandType.undo:
               break;
            case CommandType.save:
               WriteInfo($"Saved: {Database.Save()}");
               break;
            case CommandType.abort:
               Settings.SettingValue("NoSave",true);
               commandWindow?.Close();
               return;
            case CommandType.bye:
            case CommandType.quit:
            case CommandType.exit:
               commandWindow?.Close();
               return;
            case CommandType.help:
               if (args == "") {
                  WriteInfo("Capital letters denote the minimum abbreviation of the command.\n");
                  foreach (Abbreviation<CommandType> cmd in Abbreviation<CommandType>.Commands) {
                     WriteInfo(Regex.Replace(cmd.HelpText,@"^[a-z]+","   " + cmd.NameWithAbbreviation,RegexOptions.Compiled));
                  }
                  WriteInfo("\nType 'help selector' to list the valid selectors");
               } else if (args == "selector") {
                  WriteInfo("Capital letters denote the minimum abbreviation of the selector.");
                  WriteInfo("Only the first letter of the selector must be capitalized.\n");
                  foreach (Abbreviation<SelectorType> sel in Abbreviation<SelectorType>.FocusTypes) {
                     WriteInfo($"   {sel.NameWithAbbreviation}");
                  }
               }
               break;
            case CommandType.generate:
               // TDOD: Pass the program derivable from the focus or settings. Same for the target code generator.
               Program? program = CDL2.GetMainProgram();
               if (program is not null) {
                  CDL2.GenerateCode(out string targetFileName,program);
                  WriteInfo($"{Settings.SettingValue<string>("Target")} code generated for {program.FQDN()} into {targetFileName}");
               }
               break;
            case CommandType.consult:
               break;
            default:
               // Handle other commands as needed
               break;
         }
      }

      /// <summary>
      /// Return the context for the command. This either the selection specified in args, or the current focus.
      /// Note that null is returned if the selector was invalid.
      /// If there is no selector then the current focus is returned which may be SingleSeletor.Empty which is a valid case.
      /// </summary>
      /// <param name="args"></param>
      /// <param name="commandWindow"></param>
      /// <returns></returns>
      private SingleSelection? GetContext(string args) {
         if (args != "") {
            Selection selection = new(args);
            if (selection.IsInvalid) {
               WriteError(selection.ErrorMessage);
               return null;
            } else {
               return selection.FirstOrDefault()!;
            }
         } else {
            return Focus.Current.Selection;
         }
      }
   }
}

