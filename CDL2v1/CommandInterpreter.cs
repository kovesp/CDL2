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
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

namespace CDL2v1 {
   public class CommandInterpreter {
      private readonly CommandPromptWindow? commandWindow;
      private readonly PrettyPrinter pp;
      private readonly PrettyPrinter ppEdit; // For use in the edit command
      private readonly Parser parser;
      private bool IsEditing = false; // Used to determine if we are currently in edit mode

      public CommandInterpreter(CommandPromptWindow? window = null) {
         commandWindow = window;
         // Create a CommandWindowEmitter that integrates with our window
         // Initialize the parser with the compiler and a callback for error messages
         if (commandWindow is not null) {
            pp = new(new EmitterCommandWindow(commandWindow),includeComments: true);
            parser = new Parser(CDL2.Compiler,(severity,msg,_) => commandWindow.WriteLine($"{severity}: {msg}",severity));
         } else {
            pp = new(new EmitterDebug(),includeComments: true);
            parser = new Parser(CDL2.Compiler,(severity,msg,_) => Debug.WriteLine($"{severity}: {msg}"));
         }
         ppEdit = new(new EmitterString(),includeComments: true);
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

      /// <summary>
      /// Uses the Windows message box for now
      /// </summary>
      /// <param name="message"></param>
      /// <param name="buttons"></param>
      /// <param name="icon"></param>
      /// <returns></returns>
      public bool QueryBox(string message,MessageBoxButton buttons = MessageBoxButton.OKCancel,MessageBoxImage icon = MessageBoxImage.Question) {
         if (commandWindow is not null) { // Must be in interactive mode
            return MessageBox.Show(commandWindow,message,"CDL2 Laboratory",buttons,icon) == MessageBoxResult.OK;
         }
         return false;
      }

      public bool CanReplace() {
         if (IsEditing) return true; // We are in edit mode, so we can replace
         return QueryBox("The current object will be replaced. Continue?");
      }
      public void EnterCode(string input) {
         parser.Tokenize(input);
         Debug.Assert(parser.tokens.Count > 0,"Lexical Analysis found no usable tokens in input.");

         if (parser.Parse(ParsingContext ?? Focus.Current,out NamedElement? element,CanReplace,input)) Focus.SetFocus(element!);
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

      private void WriteLine(string message,Severity severity = Severity.NONE) {
         if (commandWindow is not null) {
            commandWindow.WriteLine(message,severity);
         } else {
            Debug.WriteLine(message);
         }
      }
      private void WriteError(string message) => WriteLine("Error:" + message,Severity.Error);
      private void WriteInfo(string message) => WriteLine("Info: " + message,Severity.Info);
      private void WriteWarning(string message) => WriteLine("Warning: " + message,Severity.Warning);

      /// <summary>
      /// For use by unit tests and other non-interactive uses.
      /// </summary>
      /// <param name="command"></param>
      public void InterpretCommand(string command) {
         if (command.Trim() == "") return; // Ignore empty commands
         string firstWord = Regex.Split(command.Trim(),@"\s+")[0].Trim();
         CommandType commandType = Abbreviation<CommandType>.Identify(firstWord.ToLower());
         string args = command[firstWord.Length..].Trim();
         InterpretCommand(command,commandType,"",args);
      }
      public void InterpretCommand(string command,CommandType commandType,string settings,string args) {
         IsEditing = false;
         IEnumerable<string> arguments = Regex.Split(command.Trim(),@"\s+").Skip(1).Select(s => s.Trim());
         //commandWindow.WriteLine($"> {commandType} {string.Join(" ",arguments)}");
         string[] parts;

         try {
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
                  if (!Focus.Current.Move(args,FocusMoveDirection.Forward))
                     WriteWarning("Invalid command");
                  break;
               case CommandType.previous:
                  if (!Focus.Current.Move(args,FocusMoveDirection.Backward))
                     WriteWarning("Invalid command");
                  break;
               case CommandType.first:
                  if (!Focus.Current.Move(args,FocusMoveDirection.First))
                     WriteWarning("Invalid command");
                  break;
               case CommandType.last:
                  if (!Focus.Current.Move(args,FocusMoveDirection.Last))
                     WriteWarning("Invalid command");
                  break;
               case CommandType.list:
                  if (args == "") {
                     if (Focus.Current.Object is not null) {
                        //TODO: Ignore Focus sub object for now
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
                        WriteError(selection.ErrorMessage);
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
                        //TODO: Ignore Focus sub object for now
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
                  // If it is a Section, then remove all declarations and remove the synthetic procedures generated for ludes.
                  // In each case, update the ABSTR and EXT lists of the containing LAYER and the EXPORTS and IMPORTS of the containing module.
                  // Rerun Semantic analysis to update the database.
                  //
                  // For now only handle the case when a program is being deleted: It has no children and is not contained in anything.
                  SingleSelection? context = GetContext(args);
                  if (context is null)
                     return;
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
                  if (commandWindow is null) return; // Ignore the command if there is no command window
                  context = GetContext(args);
                  if (context is null || context.Object is null || !context.IsFocusable) {
                     WriteError("Can't edit.");
                     return;
                  }
                  if (context.Object is Container) { // Container is a base class for Module, Layer, Section, Program, etc.
                     // The idea is
                     // 1. PrettyPrint the container into a file.
                     // 2. Launch an external editor (VS Code by default).
                     // 3. Detect the end of editing (like git does with an external edtor).
                     // 4. Read the file back and parse it.
                     WriteError("Editing of containers not yet implemented.");
                     return;
                  }

                  // Display the object in the command window for editing.
                  IsEditing = true; // Set the editing flag so that we can handle the edited text later. Can be used to supress a prompt for object being replaced.
                  ppEdit.Emitter.Clear();
                  commandWindow.EditText(ppEdit.Print(context.Object));
                  // Nothing else. When editing is done the command window will call EnterCode with the edited text.
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
                  Settings.SettingValue("NoSave",false);
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
                  // TODO: Pass the program derivable from the focus or settings. Same for the target code generator.
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
         } catch (Exception ex) {
            WriteError($"Exception in command: {ex.Message}");
         }
      }

      /// <summary>
      /// Return the context for the command. This either the selection specified in args, or the current focus.
      /// Note that null is returned if the selector was invalid.
      /// If there is no selector then the current focus is returned which may be SingleSelector.Empty which is a valid case.
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

