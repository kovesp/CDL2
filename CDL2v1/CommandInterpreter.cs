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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CDL2v1 {
   public class CommandInterpreter {
      private readonly CommandPromptWindow commandWindow;
      private readonly PrettyPrinter pp;
      private readonly Parser parser;

      public CommandInterpreter(CommandPromptWindow window) {
         commandWindow = window;
         // Create a CommandWindowEmitter that integrates with our window
         pp = new(new EmitterCommandWindow(commandWindow), includeComments: true);
         parser = new Parser(CDL2.Compiler);
      }


      internal void EnterCode(string input) {
         commandWindow.WriteInfo($"Entering code: {input}");
         context ??= Focus.Current;
         Logger.Log($"Context: {context}");
         parser.Tokenize(input);
         Debug.Assert(parser.tokens.Count > 0,"Lexical Analysis found to usuable tokens in input.");
         Logger.Log($"Tokenized input: {parser.tokens.Count} tokens, first token is {parser.tokens.Peek()}");

         parser.Parse(context);

         Logger.Log($"Done");
      }

      Focus? context = null;

      internal void IntepretCommand(string command, CommandType commandType, string settings, string args, CommandPromptWindow commandWindow) {
         IEnumerable<string> arguments = Regex.Split(command, @"\s+").Skip(1).Select(s=>s.Trim());
         //commandWindow.WriteLine($"> {commandType} {string.Join(" ",arguments)}");
         string[] parts;

         switch (commandType) {
            case CommandType.INVALID:
               commandWindow.WriteError($"Invalid command: {command}");
               return;
            case CommandType.focus:
               if (Focus.SetFocus(args, out string errorMessage)) {
                  commandWindow.WriteInfo(Focus.Current.ToString());
               } else {
                  commandWindow.WriteError(errorMessage);
               }
               break;
            case CommandType.next:
               // Handle next command
               commandWindow.WriteLine("Next command executed");
               break;
            case  CommandType.previous:
               // Handle previous command
               commandWindow.WriteLine("Previous command executed");
               break;
            case CommandType.list:
               if (args == "") {
                  if (Focus.Current.Object is not null) {
                     //TODO: Ignore Focus subojbest for now
                     commandWindow.WriteLine(Focus.Current.Object.FQDN());
                  } else {
                     commandWindow.WriteInfo($"Nothing");
                  }
               } else {
                  Selection selection = new(args);
                  if (selection.IsInvalid) {
                     commandWindow.WriteError(selection.ErrorMessage);
                     return;
                  }
                  if (selection.Count == 0) {
                     commandWindow.WriteLine(selection.ErrorMessage);
                  } else {
                     foreach (SingleSelection sel in selection) {
                        commandWindow.WriteLine(sel.Object!.FQDN());
                     }
                  }
               }
               break;
            case CommandType.print:
               if (args == "") {
                  if (Focus.Current.Object is not null) {
                     //TODO: Ignore Focus subobject for now
                     pp.PauseUpdate(() => pp.Print(Focus.Current.Object));
                  }
                  return;
               } else {
                  Selection selection = new(args);
                  if (selection.IsInvalid) {
                     commandWindow.WriteError(selection.ErrorMessage);
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
               parts = command.Split(' ', 3);
               if (parts.Length < 3) {
                  commandWindow.WriteInfo("Usage: set <key> <value>");
                  return;
               }
               string key = parts[1];
               string value = parts[2];
               // Set logic here
               commandWindow.WriteLine($"Set {key} to {value}");
               break;
            case CommandType.status:
               Reachable.LogObjectCount(CDL2.Compiler.Reachable.AllObjects,$"in {Database.Instance.Modules.Count.Plural("module")}", commandWindow.WriteInfo);
               break;
            case CommandType.rename:
               break;
            case CommandType.replace:
            case CommandType.append:
            case CommandType.insert:
            case CommandType.edit:
            case CommandType.undo:
               break;
            case CommandType.save:
               commandWindow.WriteInfo($"Saved: {Database.Save()}");               
               break;
            case CommandType.quit:
            case CommandType.exit:
               commandWindow.Close();
               return;
            case CommandType.help:
               if (args == "") {
                  commandWindow.WriteInfo("Capital letters denote the minimum abbreviation of the command.\n");
                  foreach (Abbreviation<CommandType> cmd in Abbreviation<CommandType>.Commands) {
                     commandWindow.WriteInfo(Regex.Replace(cmd.HelpText,@"^[a-z]+","   "+cmd.NameWithAbbreviation,RegexOptions.Compiled));
                  }
                  commandWindow.WriteInfo("\nType 'help selector' to list the valid selectors");
               } else if (args == "selector") {
                  commandWindow.WriteInfo("Capital letters denote the minimum abbreviation of the selector.");
                  commandWindow.WriteInfo("Only the first letter of the selector must be capitalized.\n");
                  foreach (Abbreviation<SelectorType> sel in Abbreviation<SelectorType>.FocusTypes) {
                     commandWindow.WriteInfo($"   {sel.NameWithAbbreviation}");
                  }
               }
               break;
            case CommandType.generate:
               // TDOD: Pass the program derivable from the focus or settings. Same for the target code generator.
               Program? program = CDL2.GetMainProgram();
               if (program is not null) {
                  CDL2.GenerateCode(out string targetFileName,program);
                  commandWindow.WriteInfo($"{Settings.SettingValue<string>("Target")} code generated for {program.FQDN()} into {targetFileName}");
               }
               break;
            default:
                  // Handle other commands as needed
               break;
         }
      }
   }
}

