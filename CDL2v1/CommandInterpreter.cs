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
using System.Security.Cryptography;
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
            pp = new(commandWindow.Emitter = new EmitterCommandWindow(commandWindow),includeComments: true);
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

      public void EnterCode(string input,bool setFocus = true) {
         if (input.Contains('.')) {
            IEnumerable<string> lines = input.Split('.',StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines.SkipLast(1)) EnterCode(line,setFocus = false);
            EnterCode(lines.Last(),setFocus:true);
         } else {
            input = input.Trim();
            if (input[^1] != '.') input += '.';
            string firstWord = input.Split(' ','\t','\r','\n')[0];
            SelectorType type = Abbreviation<SelectorType>.Identify(firstWord.ToUpper());
            if (type != SelectorType.INVALID) {
               input = type + input[firstWord.Length..];
               if (parser.Tokenize(input,ParseMode.Full)) {
                  Debug.Assert(parser.tokens.Count > 0,"Lexical Analysis found no usable tokens in input.");

                  if (parser.Parse(ParsingContext ?? Focus.Current,out NamedElement? element,CanReplace,input) & setFocus)
                     Focus.SetFocus(element!);
               }
            }
            ParsingContext = null; // Reset the parsing context after a parse
         }
      }
      /// <summary>
      /// Parse the input and verify whther it is syntactically correct.
      /// DO not add anything to the database, just check the syntax.
      /// </summary>
      /// <param name="input"></param>
      /// <returns></returns>
      public bool VerifySyntax(string input) {
         input = input.Trim();
         if (input.Length > 0 && input[^1] != '.') input += '.';
         if (parser.Tokenize(input,ParseMode.Check)) {
            return Database.WithSuspendedNamedElementRegistration(true,
               () => parser.Parse(ParsingContext ?? Focus.Current,out _,() => false,input,ParseMode.Check));
         }
         return false;
      }

      public bool EnterRawCode(string input) {
         string trimmed = input.Trim();
         if (char.IsAsciiLetterUpper(trimmed[0])) {
            EnterCode(trimmed);
            return true;
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
      /// Replaces all spaces in quoted strings with $S, to allow splitting the command line into arguments and settings.
      /// </summary>
      /// <param name="input"></param>
      /// <returns></returns>
      private static string ReplaceSpacesInQuotedStrings(string input) {
         bool inQuotes = false;
         int length = input.Length;
         StringBuilder result = new StringBuilder(length);
         for (int i = 0 ; i < length ; i++) {
            char c = input[i];
            if (inQuotes) {
               if (c == '$' && i + 1 < length && (input[i + 1] == '"' || input[i + 1] == '$')) {
                  result.Append(c);
                  result.Append(input[i + 1]);
                  i++;
               } else if (c == '"') {
                  inQuotes = false;
                  result.Append(c);
               } else if (c == ' ') {
                  result.Append("$S");
               } else {
                  result.Append(c);
               }
            } else {
               if (c == '"') {
                  inQuotes = true;
               }
               result.Append(c);
            }
         }
         return result.ToString();
      }
      public record class ParsedSetting(string Name,SettingType Type,object? Value,object? currentValue) {
         public readonly string Name = Name;
         public readonly SettingType Type = Type;
         public readonly object? Value = Value;
         public object? PreviousValue = currentValue;

         public override string ToString() => Type switch {
            SettingType.Boolean => $"-{Name}{(Value is null ? "" : (bool)Value ? "+" : "-")}",
            SettingType.Integer => $"-{Name}={Value}",
            SettingType.String  => $"-{Name}=\"{Value}\"",
            _                   => $"-{Name}=<unknown type>"
         };
      }

      private ParsedSetting SplitSetting(string setting) {
         string[] parts = setting.TrimStart('-').Split([':','='],2);
         if (parts.Length > 1) {
            // A numeric or string setting.
            if (int.TryParse(parts[1],out int intValue)) {
               return new ParsedSetting(parts[0],SettingType.Integer,intValue,null);
            } else {
               if (parts[1].StartsWith('"')) {
                  parts[1] = Regex.Replace(parts[1],@"$(.)",m => m.Groups[1].Value switch {
                     "S" or "s" => " ",
                     "$" => "$",
                     "\"" => "\"",
                     "L" or "l" => "\n",
                     "T" or "t" => "\t",
                     _ => m.Value
                  });
               }

               return new ParsedSetting(parts[0],SettingType.String,parts[1],null);
            }
         } else {
            // A boolean setting, possibly with + or - suffix.
            return new ParsedSetting(parts[0].TrimEnd('-','+'),SettingType.Boolean,parts[0][^1] == '+' ? true : parts[0][^1] == '-' ? false : null,null);
         }
      }

      /// <summary>
      /// Pasrse the command into verb, argumensts and settings and then interprete it.
      /// </summary>
      /// <param name="command"></param>
      public void InterpretCommand(string command) {
         if ((command = command.Trim()) == "") return; // Ignore empty commands
         ParseCommand(command,out string verb,out CommandType commandType,out string args,out ParsedSetting[] settings);
         InterpretCommand(verb,commandType,settings,args);
      }

      /// <summary>
      /// Parse the command into verb, arguments and settings.
      /// </summary>
      /// <param name="command"></param>
      /// <param name="verb"></param>
      /// <param name="commandType"></param>
      /// <param name="args"></param>
      /// <param name="settings"></param>
      private void ParseCommand(string command,out string verb,out CommandType commandType,out string args,out ParsedSetting[] settings) {
         string input = ReplaceSpacesInQuotedStrings(command); // Replace spaces in quoted strings with $S to allow splitting the command line into arguments and settings.
         string[] commandParts = Regex.Split(input,@"\s+");
         verb = commandParts[0].ToLower();
         commandType = Abbreviation<CommandType>.Identify(verb.ToLower());
         args = string.Join(' ',commandParts.Skip(1).Where(part => !part.StartsWith('-')));
         settings = [.. commandParts.Skip(1).Where(part => part.StartsWith('-')).Select(SplitSetting)];
      }

      /// <summary>
      /// Interpret the command with the given verb, arguments and settings.
      /// </summary>
      /// <param name="command"></param>
      /// <param name="commandType"></param>
      /// <param name="settings"></param>
      /// <param name="args"></param>
      /// <exception cref="NotImplementedException"></exception>
      public void InterpretCommand(string verb,CommandType commandType,ParsedSetting[] settings,string args) {
         IsEditing = false;
         // Use settings to change global settings. Save previous values so they can be restored later.
         foreach (ParsedSetting setting in settings) {
            if (Settings.IsValidSetting(setting.Name)) {
               setting.PreviousValue = setting.Type switch {
                  SettingType.Boolean => Settings.SettingValue<bool>(setting.Name),
                  SettingType.Integer => Settings.SettingValue<int>(setting.Name),
                  SettingType.String => Settings.SettingValue<string>(setting.Name),
                  _ => throw new NotImplementedException($"Setting type {setting.Type} not implemented."),
               };
               Settings.SettingValue(setting.Name,setting.Type,setting.Value);
            } else {
               WriteError($"Invalid setting: {setting.Name} ignored");
            }
         }

         if (Settings.SettingValue<bool>("DebugCommands")) {
            WriteInfo($"Command: {verb} {string.Join(" ",settings.Select(s=>s.ToString()))} {args}");
         }

         bool ResetSettings = true; // Whether to reset settings after the command. Some commands may want to keep the settings.

         try {
            switch (commandType) {
#if DEBUG
               case CommandType.vsdebug:
                  Debugger.Break();
                  break;
#endif
               case CommandType.INVALID:
                  WriteError($"Invalid command: {verb}");
                  return;
               case CommandType.focus:
                  if (!Focus.SetFocus(args,out string errorMessage)) WriteError(errorMessage); break;
               case CommandType.next:
                  if (!Focus.Current.Move(args,FocusMoveDirection.Forward)) WriteWarning("Invalid command"); break;
               case CommandType.previous:
                  if (!Focus.Current.Move(args,FocusMoveDirection.Backward)) WriteWarning("Invalid command"); break;
               case CommandType.first:
                  if (!Focus.Current.Move(args,FocusMoveDirection.First)) WriteWarning("Invalid command"); break;
               case CommandType.last:
                  if (!Focus.Current.Move(args,FocusMoveDirection.Last)) WriteWarning("Invalid command"); break;
               case CommandType.list:
                  InterpretCommandList(args); break;
               case CommandType.print:
               case CommandType.type:
                  InterpretCommandPrint(args); break;
               case CommandType.set:
                  // Modify settings so that the reset actually sets the new values
                  if (settings.Length == 0) {
                     // List the current settings
                     WriteLine(Settings.AllSettings.First().ToTabularString(title:true));
                     foreach (ISetting setting in Settings.AllSettings.OrderBy(s => s.Name)) {
                        WriteLine(setting.ToTabularString());
                     }
                  } else {
                     ResetSettings = false;
                  }
                  break;
               case CommandType.status:
                  InterpretCommandStatus(); break;
               case CommandType.rename:
                  InterpretCommandRename(args); break;
               case CommandType.add:
               case CommandType.append:
                  InterpretCommandAdd(args,InsertLocation.After); break;
               case CommandType.insert:
                  InterpretCommandAdd(args,InsertLocation.Before); break;
               case CommandType.delete:
               case CommandType.remove:
                  InterpretCommandDelete(args); break;
               case CommandType.edit:
                  InterpretCommandEdit(args); break;
               case CommandType.replace:
                  InterpretCommandReplace(args); break;
               case CommandType.undo:
                  InterpretCommandUndoRedo(args,undo: true); break;
               case CommandType.redo:
                  InterpretCommandUndoRedo(args,undo: false); break;
               case CommandType.save:
                  WriteInfo($"Saved: {Database.Save()}"); break;
               case CommandType.abort:
                  ToastWindow.ShowToast("abort command used, not saving the database.",2000);
                  commandWindow?.Close();
                  return;
               case CommandType.bye:
               case CommandType.quit:
               case CommandType.exit:
                  ToastWindow.ShowToast($"Saving ${Settings.LabDBPath}",() => Database.Save(),2000);
                  commandWindow?.Close();
                  return;
               case CommandType.help:
                  InterpretCommandHelp(args); break;
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
         } finally {
            // Restore previous settings (unless it is a set command
            if (ResetSettings) foreach (ParsedSetting setting in settings) if (Settings.IsValidSetting(setting.Name)) Settings.SettingValue(setting.Name,setting.Type,setting.PreviousValue!);  
         }
      }

      private void InterpretCommandHelp(string args) {
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
      }

      private void InterpretCommandUndoRedo(string args,bool undo) {
         if (Settings.SettingValue<bool>("list")) {
            WriteWarning("Undo/Redo with list option.");
         }
         //SettingValue<int>("VerbosityLevel") >= level
         //int count = 1;
         //if (args != "") {
         //   if (!int.TryParse(args,out count) || count < 1) {
         //      WriteError("Invalid argument for undo/redo command.");
         //      return;
         //   }
         //}
         //if (undo) {
         //   if (Database.Undo(count,out int actual)) {
         //      WriteInfo($"Undid {actual} step{(actual == 1 ? "" : "s")}");
         //   } else {
         //      WriteWarning("Nothing to undo.");
         //   }
         //} else {
         //   if (Database.Redo(count,out int actual)) {
         //      WriteInfo($"Redid {actual} step{(actual == 1 ? "" : "s")}");
         //   } else {
         //      WriteWarning("Nothing to redo.");
         //   }
         //}
      }

      private void InterpretCommandReplace(string args) => throw new NotImplementedException();

      private void InterpretCommandEdit(string args) {
         if (commandWindow is null) return; // Ignore the command if there is no command window
         SingleSelection context = GetContext(args);
         if (context is null || context.Object is null || !context.IsFocusable) {
            WriteError("Can't edit.");
         } else if (context.Object is Container) { 
            // Container is a base class for Module, Layer, Section, Program, etc.
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
         ParsingContext = new Focus(context); // Set the parsing context to the current focus, so that the parser can use it.
         commandWindow.EditText(ppEdit.Print(context.Object));
         // Nothing else. When editing is done the command window will call EnterCode with the edited text.
      }

      private SingleSelection? InterpretCommandDelete(string args) {
         // Remove the NamedElement from NamedElements.
         // If it is a program or a module, remove it from the appropriate database list
         // If it is a Module, Layer or a Section, remove it from its container, and also remove all children.
         // If it is a Section, then remove all declarations and remove the synthetic procedures generated for ludes.
         // In each case, update the ABSTR and EXT lists of the containing LAYER and the EXPORTS and IMPORTS of the containing module.
         // Rerun Semantic analysis to update the database.
         SingleSelection? context = GetContext(args);
         if (context is not null) {
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
               case CDL2Object obj:
                  Focus.MoveFocusFrom(obj);
                  Database.Instance.RecordUndo(obj);
                  obj.Section?.Declarations.Remove(obj.Id);
                  obj.Siblings.Remove(obj.GUID);
                  obj.ClearInterfaceStatus();
                  // WriteInfo($"{obj.FQDN()} removed");
                  break;
               default:
                  WriteError($"Cannot delete {Focus.Current.Object?.FQDN() ?? "<unknown>"}");
                  break;
            }
         }

         return context;
      }

      private void InterpretCommandAdd(string args,InsertLocation after) => throw new NotImplementedException();
      private void InterpretCommandRename(string args) => throw new NotImplementedException();

      private void InterpretCommandStatus() {
         WriteInfo($"CDL2 Lab Version {CDL2.Version} with database {Settings.LabDBPath}");
         Reachable.LogObjectCount(CDL2.Compiler.Reachable.AllObjects,$"in {Database.Instance.Modules.Count.Plural("module")}",WriteInfo);
      }

      private string[] InterpretCommandSet(string command) {
         // Handle set command
         string[] parts = command.Split(' ',3);
         if (parts.Length < 3) {
            WriteInfo("Usage: set <key> <value>");
         } else {
            string key = parts[1];
            string value = parts[2];
            // Set logic here
            WriteLine($"Set {key} to {value}");
         }

         return parts;
      }

      private void InterpretCommandPrint(string args) {
         if (args == "") {
            if (Focus.Current.Object is not null) {
               //TODO: Ignore Focus sub object for now
               pp.PauseUpdate(() => pp.Print(Focus.Current.Object));
            }
         } else {
            Selection selection = new(args);
            if (selection.IsInvalid) {
               WriteError(selection.ErrorMessage);
            } else {
               pp.PauseUpdate(() => {
                  foreach (SingleSelection sel in selection) {
                     pp.Print(sel.Object!);
                  }
               });
            }
         }
      }

      private void InterpretCommandList(string args) {
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
            } else if (selection.Count == 0) {
               WriteError(selection.ErrorMessage);
            } else {
               foreach (SingleSelection sel in selection) {
                  WriteLine(sel.Object!.FQDN());
               }
            }
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

