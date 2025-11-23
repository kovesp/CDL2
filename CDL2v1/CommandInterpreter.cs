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
//   It is used to interpret lab commands and display results in the CommandWindow;
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

using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;


namespace CDL2v1 {
   public class CommandInterpreter {
      private readonly CommandPromptWindow? commandWindow;
      private readonly PrettyPrinter pp;
      private readonly PrettyPrinter ppEdit; // For use in the edit command
      private readonly Parser parser;
      private bool IsEditing = false; // Used to determine if we are currently in edit mode
      private InsertLocation insertionLocation = InsertLocation.After; // Used to determine where to insert the object being added

      public const char CommandComment = '!';
      public const char CommandSeparator = ';'; // Not currently used. Difficult to split commands correctly when there are quoted strings.

      public CommandInterpreter(CommandPromptWindow? window = null) {
         commandWindow = window;
         // Create a CommandWindowEmitter that integrates with our window
         // Initialize the parser with the compiler and a callback for error messages
         if (commandWindow is not null) {
            pp = new(commandWindow.Emitter = new EmitterCommandWindow(commandWindow) { SuppressDebug = !Settings.SettingValue<bool>("PrettyPrintDebug") },includeComments: true);
            // pp = new(commandWindow.Emitter = new EmitterMulticast(new EmitterDebug(),new EmitterCommandWindow(commandWindow)),includeComments: true);

            parser = new Parser(CDL2.Compiler,(severity,msg,_) => commandWindow.WriteLine($"{severity}: {msg}",severity));
         } else {
            pp = new(new EmitterDebug(),includeComments: true);
            parser = new Parser(CDL2.Compiler,(severity,msg,_) => Debug.WriteLine($"{severity}: {msg}"));
         }
         ppEdit = new(new EmitterString(),includeComments: true);
      }

      public void SetStatus(NamedElement? element=null) {
         if (commandWindow is not null) {
            if (element is null) {
               commandWindow.SetStatus("Nothing");
            } else {
               string marker = (element is ITopLevelContainer t ? t : element.Module)?.Modified == true ? "*" : "";
               commandWindow.SetStatus(marker+element.FQDN(WithInterface:true));
            }
         }
      }
      public void SetStatus() => SetStatus(Focus.Current.Object);

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

      /// <summary>
      /// Called whenever code is entered. It needs to take into consideretion
      /// <list type="bullet">
      /// <item>isEditing: if false, then no prompt is given when the object entered exists</item>
      /// <item>parsingContext</item>
      /// <item>insertionLocation: before, after and replace</item>
      /// </list>
      /// </summary>
      /// <param name="input"></param>
      /// <param name="setFocus"></param>
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

                  // Parses the input and adds it to the DB. If an element with the same name exists, it will ask for confirmation to replace it.
                  // Must also take care of adding an undo record if something is replaced.
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
      private void WriteError(string message) => WriteLine("Error: " + message,Severity.Error);
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
         StringBuilder result = new(length);
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
         public readonly string Name = Name.ToLower();
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

      /// <summary>
      /// Parse a setting of the form -name[+|-] or -name[:|=]value.
      /// Note that the leading - is removed and is optional for the set command.
      /// This works becasue the call ensures that the - is present when required.
      /// </summary>
      /// <param name="setting"></param>
      /// <returns></returns>
      private ParsedSetting ParseSetting(string setting) {
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
         if ((command = command.Trim()) == "" || command.StartsWith(CommandComment)) return; // Ignore empty commands and command comments
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
         if (commandType == CommandType.set) {
            args = "";
            settings = [.. commandParts.Skip(1).Select(ParseSetting)];
         } else {
            args = string.Join(' ',commandParts.Skip(1).Where(part => !part.StartsWith('-')));
            settings = [.. commandParts.Skip(1).Where(part => part.StartsWith('-')).Select(ParseSetting)];
         }
      }

      private static readonly ImmutableDictionary<CommandType,FocusMoveDirection> commandAsFocusMoveDirection =
          new Dictionary<CommandType,FocusMoveDirection> {
             [CommandType.next]     = FocusMoveDirection.Forward,
             [CommandType.previous] = FocusMoveDirection.Backward,
             [CommandType.first]    = FocusMoveDirection.First,
             [CommandType.last]     = FocusMoveDirection.Last,
          }.ToImmutableDictionary();

      /// <summary>
      /// When the given setting is encountered in a set command, or on another command this handler is called to process it.
      /// The handler is called when the settings is to be set with first parameter true, and when it is to be reset with false.
      /// The second parameter is the setting name. Specify in all lowercase.
      /// The third parameter is the value to be set.
      /// The return value is whether the set was successful.
      /// </summary>
      private static readonly ImmutableDictionary<string,Func<bool,string,object?,bool>> SetHandlers =
          new Dictionary<string,Func<bool,string,object?,bool>> {
             ["programname"]      = SetProgram,
             ["autosaveinterval"] = SetAutoSaveInterval,
          }.ToImmutableDictionary();

      /// <summary>
      /// Handles changes to the AutosaveInterval setting by configuring the database auto-save timer.
      /// </summary>
      /// <param name="isSet">True when setting the value, false when resetting.</param>
      /// <param name="_">The name of the setting (not used).</param>
      /// <param name="intervalSeconds">The auto-save interval in seconds.</param>
      /// <returns>Always true as any integer value is valid.</returns>
      private static bool SetAutoSaveInterval(bool isSet,string _,object? intervalSeconds) {
         if (intervalSeconds is int interval) {
            Database.Instance.ConfigureAutoSave(interval);
         }
         return true;
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
         bool SettingsValid = true;
         foreach (ParsedSetting setting in settings) {
            if (Settings.IsValidSetting(setting.Name)) {
               setting.PreviousValue = setting.Type switch {
                  SettingType.Boolean => Settings.SettingValue<bool>(setting.Name),
                  SettingType.Integer => Settings.SettingValue<int>(setting.Name),
                  SettingType.String => Settings.SettingValue<string>(setting.Name),
                  _ => throw new NotImplementedException($"Setting type {setting.Type} not implemented."),
               };
               if (SetHandlers.TryGetValue(setting.Name,out Func<bool,string,object?,bool>? handler) && ! handler(true,setting.Name,setting.Value)) SettingsValid = false;
               if (SettingsValid) {
                  Settings.SettingValue(setting.Name,setting.Type,setting.Value,CommandOverride: true);
               } else {
                  WriteError($"Invalid setting value: {setting.Name}={setting.Value}. Command aborted.");
                  break;
               }
            } else {
               WriteError($"Invalid setting: {setting.Name} ignored");
            }
         }

         if (Settings.SettingValue<bool>("DebugCommands")) {
            WriteInfo($"Command: {verb} {string.Join(" ",settings.Select(s=>s.ToString()))} {args}");
         }

         bool ResetSettings = true; // Whether to reset settings after the command. Some commands may want to keep the settings.
         bool RequiresSemanticAnalysis = false; // Whether the command requires semantic analysis after execution.

         try {
            if (SettingsValid) switch (commandType) { // skip command if settings are invalid. Must do the undo in that case
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
               case CommandType.previous:
               case CommandType.first:
               case CommandType.last:
                  if (!Focus.Current.Move(args,commandAsFocusMoveDirection[commandType],out string msg,out Severity severity)) WriteLine(msg,severity); break;

               case CommandType.list:
                  InterpretCommandList(args); break;

               case CommandType.print:
               case CommandType.type:
                  InterpretCommandPrint(args); break;

               case CommandType.set:
                  // Modify settings so that the reset actually sets the new values
                  if (settings.Length == 0) {
                     // List the current settings
                     DisplaySettings();
                  } else {
                     ResetSettings = false;
                  }
                  break;
               case CommandType.status:
                  InterpretCommandStatus(); break;

               case CommandType.rename:
                  InterpretCommandRename(args); RequiresSemanticAnalysis = true; break;
               case CommandType.add:
                  InterpretCommandAdd(args); RequiresSemanticAnalysis = true; break;
               case CommandType.edit:
                  InterpretCommandEdit(args); RequiresSemanticAnalysis = true; break;
               case CommandType.delete:
               case CommandType.remove:
                  InterpretCommandDelete(args); RequiresSemanticAnalysis = true; break;
               case CommandType.consult:
                  InterpretCommandConsult(args); RequiresSemanticAnalysis = true; break;

               case CommandType.undo:
                  InterpretCommandUndoRedo(args,undo: true); RequiresSemanticAnalysis = true; break;
               case CommandType.redo:
                  InterpretCommandUndoRedo(args,undo: false); RequiresSemanticAnalysis = true; break;

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

               case CommandType.analyze:
                  InterpretCommandAnalyze(args); break;
               case CommandType.generate:
                  // TODO: Pass the program derivable from the focus or settings. Same for the target code generator.
                  Program? program = CDL2.GetMainProgram();
                  if (program is not null) {
                     CDL2.GenerateCode(out string targetFileName,program);
                     WriteInfo($"{Settings.SettingValue<string>("Target")} code generated for {program.FQDN()} into {targetFileName}");
                  }
                  break;

               default:
                  // Handle other commands as needed
                  break;
            }
            if (RequiresSemanticAnalysis && Settings.SettingValue<bool>("AutoAnalyze")) {
               // TODO: Perform semantic analysis after commands that could have made a change if AutoAnalyze is set.
            }
         } catch (Exception ex) {
            WriteError($"Exception in command: {ex.Message}");
         } finally {
            // Restore previous settings (unless it is a set command
            if (ResetSettings) {
               foreach (ParsedSetting setting in settings) {
                  if (Settings.IsValidSetting(setting.Name)) {
                     if (SetHandlers.TryGetValue(setting.Name,out Func<bool,string,object?,bool>? handler)) handler(false,setting.Name,setting.PreviousValue!);
                     Settings.SettingValue(setting.Name,setting.Type,setting.PreviousValue!,CommandOverride: false);
                  }
               }
            }
         }
         SetStatus();
      }

      /// <summary>
      /// Verifies that the given program name existis in the database.
      /// </summary>
      /// <param name="_1">True when setting the value, false when reseting. Since the previous value is passed on reset, no need to check.</param>
      /// <param name="_2">The name of the setting == "ProgramName"</param>
      /// <param name="programName"></param>
      /// <returns></returns>
      private static bool SetProgram(bool _1,string _2,object? programName) {
         Program? prog = Database.Instance.ProgramByName((programName as string) ?? "");
         if (prog is null) return false;
         //TODO Set the mainprogram variable here
         return true;
      }

      private void InterpretCommandAnalyze(string args) => throw new NotImplementedException();

      private static readonly Regex ModuleOrProgramStart = new(@"(?m)^\s*(?:#.*?(?:#|$)\s*)*\s*(?:MODULE|PROGRAM)(?=\s)",RegexOptions.Compiled);
      /// <summary>
      /// Read, parse and execute coomands from a file.
      /// </summary>
      /// <param name="fileName">The file name.</param>
      /// <remarks>
      /// Special case: if the file starts with a MODULE or PROGRAM reserved word, then the file is parsed as in non-Lab mode.
      /// </remarks>
      private void InterpretCommandConsult(string fileName) {
         if (fileName.TryGetFile(out string fullFileName,["labc","cdl2"])) {
            string fileContent = File.ReadAllText(fullFileName).TrimStart();
            if (ModuleOrProgramStart.IsMatch(fileContent)) {
               // Non-Lab mode parsing.
               List<ITopLevelContainer> parsedContainers = parser.ParseString(fileContent);
               Debug.Assert(parsedContainers.All(c => c is Program || c is Module),"Expected programs or modules in consulted file");
               Debug.Assert(CDL2.Compiler.SemanticAnalyzer != null,"SemanticAnalyzer is null");
               foreach (ITopLevelContainer container in parsedContainers) {
                  container.Modified = true;
               }
               WriteInfo($"Consulted => {string.Join(", ",parsedContainers.Select(c => c.FQDN()))}");
               parsedContainers.LastOrDefault().SetFocus();  
            } else {
               // Lab mode: interpret each line as a command
               string[] lines = fileContent.Split(new[] { "\r\n", "\r", "\n" },StringSplitOptions.RemoveEmptyEntries);
               foreach (string line in lines) {
                  WriteInfo("   " + line);
                  InterpretCommand(line);
               }
               WriteInfo($"Consulted");
            }
         } else {
            WriteError("File not found");
         }
      }

      private void DisplaySettings() {
         WriteLine(Settings.AllSettings.First().ToTabularString(title: true));
         foreach (ISetting setting in Settings.AllSettings.OrderBy(s => s.Name)) {
            WriteLine(setting.ToTabularString());
         }
      }

      private void InterpretCommandHelp(string args) {
         if (args == "") {
            WriteInfo("Capital letters denote the minimum abbreviation of the command.");
            foreach (Abbreviation<CommandType> cmd in Abbreviation<CommandType>.Commands) {
               WriteLine(Regex.Replace(cmd.HelpText,@"^[a-z]+","   " + cmd.NameWithAbbreviation,RegexOptions.Compiled));
            }
            WriteInfo("Type 'help selector' to list the valid selectors.");
            WriteInfo("Type 'help setting' to list the valid settings.");
         } else if (args == "selector") {
            WriteInfo("Capital letters denote the minimum abbreviation of the selector.");
            WriteInfo("Only the first letter of the selector must be capitalized.\n");
            foreach (Abbreviation<SelectorType> sel in Abbreviation<SelectorType>.FocusTypes) {
               WriteLine($"   {sel.NameWithAbbreviation}");
            }
         } else if (args == "setting") {
            foreach (ISetting setting in Settings.AllSettings.OrderBy(s => s.Name)) {
               string[] desc = setting.Option.Description?.Split("\n") ?? [""];
               WriteLine($"{setting.Name.PadRight(Settings.Instance.MaxNameLength)} : {desc[0]}");
               foreach (string line in desc.Skip(1)) {
                  WriteLine($"{new string(' ',Settings.Instance.MaxNameLength)}   {line}");
               }
            }
         }
      }

      /// <summary>
      /// Perform undo or redo. List the appropriate stack if requested.
      /// If an argument is given the operation is performed on the element at that index in the stack.
      /// </summary>
      /// <param name="args"></param>
      /// <param name="undo"></param>
      private void InterpretCommandUndoRedo(string args,bool undo) {
         BoundedStack<Database.UndoRecord> stack = undo ? Database.Instance.UndoStack : Database.Instance.RedoStack;
         BoundedStack<Database.UndoRecord> otherStack = undo ? Database.Instance.RedoStack : Database.Instance.UndoStack;
         string stackName = undo ? "undo" : "redo";
         if (Settings.SettingValue<bool>("list")) {
            int n = 0;
            WriteLine($"{stackName} stack ({stack.Count}/{stack.Capacity})");
            foreach (Database.UndoRecord record in stack) {
               CDL2Object? obj = record.CDL2Object;
               if (obj is not null) {
                  WriteLine($"{++n,3}:{(record.Tag.IsNotEmptyOrWhitespace ? $" [{record.Tag}]:" : "")} {record.ChangeType,8} :: {obj.FQDN(WithInterface: true)}");
               } else {
                  WriteLine($"{++n,3}: Undo record contains {record.ObjectGuid} which is not in NamedElements");
               }
            }
         } else if (stack.Count == 0) {
            WriteError($"{stackName} stack is empty.");
         } else {
            int index = int.TryParse(args,out int i) ? i-1 : 0;
            if (i < 0 || i >= stack.Count) {
               WriteError($"argument {index + 1} for {stackName} stack out of range: must be {(stack.Count==1?"1 if given":$"between 1 and {stack.Count}")}.");
            } else {
               string tag = Settings.SettingValue<string>("settag")!;
               if (tag.IsNotEmptyOrWhitespace) {
                  stack[index]?.Tag = tag == "-" ? "" : tag;
               } else {
                  tag = Settings.SettingValue<string>("tag")!;
                  // Move the requested record to the top of the stack
                  if (tag.IsNotEmptyOrWhitespace) {
                     stack.Surface(record => record.Tag == tag);
                  } else {
                     stack.Surface(index);
                  }
                  SingleUndoRedo(undo,stack,otherStack);
               }
               SetStatus();
            }
         }
      }

      /// <summary>
      /// Perform a single undo or redo operation.
      /// The idea is that the operations are symetric, so which one is done depends on which stack is undo or redo and which is the other stack.
      /// </summary>
      /// <param name="stack">undo stack for undo, redo stack for redo</param>
      /// <param name="otherStack">redo stack for undo, undo stack for redo</param>
      /// <exception cref="NotImplementedException"></exception>
      private static void SingleUndoRedo(bool undo,BoundedStack<Database.UndoRecord> stack,BoundedStack<Database.UndoRecord> otherStack) {
         Database.UndoRecord record = stack.Pop();
         switch (record.ChangeType) {
            case ChangeType.Added:
               break;
            case ChangeType.Removed:
               CDL2Object obj = record.CDL2Object!;
               if (undo) {
                  // Revive the removed object
                  int objectPos = -1;
                  // If the focus is currently in the same section as the object being revived, insert the revived object after the current focus.
                  if (Focus.Current.Section == obj.Section) objectPos = Focus.Current.IndexFor();
                  obj.Revive(null,ChangeType.Removed,record.InterfaceStatus,objectPos);
               } else {
                  // Remove the object again
                  Focus.MoveFocusFrom(obj);
                  obj.Remove();
               }
               break;
            case ChangeType.InterfaceChanged:
               InterfaceTypes currentInterfaceType = record.CDL2Object!.GetInterfaces();
               record.CDL2Object!.SetInterfaces(record.InterfaceStatus);
               record.InterfaceStatus = currentInterfaceType; // Swap the interface status for the symetric operation  
               break;
            case ChangeType.Replaced: 
               break;
            case ChangeType.Renamed: 
               break;
            default:
               throw new NotImplementedException($"Undo of change type {record.ChangeType} not implemented.");
         }
         otherStack.Push(record);
      }


      private void InterpretCommandDelete(string args) {
         // Remove the NamedElement from NamedElements.
         // If it is a program or a module, remove it from the appropriate database list
         // If it is a Module, Layer or a Section, remove it from its container, and also remove all children.
         // If it is a Section, then remove all declarations and remove the synthetic procedures generated for ludes.
         //
         // In each case, update the ABSTR and EXT lists of the containing LAYER and the EXPORTS and IMPORTS of the containing module.
         // Rerun Semantic analysis to update the database.
         Selection? multiContext = GetMultiContext(args);
         if (multiContext is not null && multiContext.IsValid) {
            foreach (SingleSelection context in multiContext) {
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
                     InterfaceTypes interfaceTypes = InterfaceTypeFromSetting();
                     if (interfaceTypes != InterfaceTypes.None) { // Interface removal(s) were requested
                        if (obj is not Algorithm && obj is not Const) {
                           WriteError("Interfaces can only be removed from Algorithms or Constants");
                           // All objects in the selection are of the same type, so this will happen on the first one rather than in a loop
                           return;
                        }
                        InterfaceTypes currentInterfaceTypes = obj.GetInterfaces();
                        if (currentInterfaceTypes != interfaceTypes) {
                           Database.Instance.RecordUndo(obj,ChangeType.InterfaceChanged);
                           obj.ClearInterfaces(interfaceTypes);
                        }
                     } else {
                        Focus.MoveFocusFrom(obj);
                        obj.Remove();
                     }
                     obj.Module?.Modified = true;
                     break;
                  default:
                     WriteError($"Cannot delete {Focus.Current.Object?.FQDN() ?? "<unknown>"}");
                     break;
               }
            }
            SetStatus();
         }
      }

      private void InterpretCommandEdit(string args) {
         if (commandWindow == null) return; // Ignore the command if there is no command window
         SingleSelection? context = GetContext(args);
         if (context == null || context.Object == null || !context.IsFocusable) {
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
         } else {
            // Display the object in the command window for editing.
            IsEditing = true; // Set the editing flag so that we can handle the edited text later. Can be used to supress a prompt for object being replaced.
            ppEdit.Emitter.Clear();
            ParsingContext = new Focus(context); // Set the parsing context to the current focus, so that the parser can use it.
            commandWindow.EditText(ppEdit.Print(context.Object));
            insertionLocation = InsertLocation.Replace;
            // Nothing else. When editing is done the command window will call EnterCode with the edited text.
         }
      }

      /// <summary>
      /// Implements the add, replace, append and insert commands.
      /// </summary>
      /// <param name="args"></param>
      /// <param name="after"></param>
      /// <param name="add"></param>
      private void InterpretCommandAdd(string args) {
         if (commandWindow == null) return; // Ignore the command if there is no command window
         Selection? context = GetMultiContext(args);
         if (context == null || context.Count == 0) return;

         InterfaceTypes interfaceTypes = InterfaceTypeFromSetting();
         if (interfaceTypes != InterfaceTypes.None) {
            if (context.First().Object is not Algorithm && context.First().Object is not Const) {
               // It is enough to check the first because all must be of the same type.
               WriteError("Interfaces can only be added to Algorithms or Constants");
               return;
            }
            foreach (SingleSelection sel in context) {
               if (sel.Object is CDL2Object obj) {
                  InterfaceTypes currentInterfaceTypes = obj.GetInterfaces();
                  if (currentInterfaceTypes != interfaceTypes) {
                     Database.Instance.RecordUndo(obj,ChangeType.InterfaceChanged);
                     obj.AddInterfaces(interfaceTypes);
                     obj.Module!.Modified = true;
                  }
               }
            }
            SetStatus();
            return;
         } else {
            // Swich to edit mode in the input field with empty content.
            IsEditing = false; // Ensure a prompt is given if the object exists.
            insertionLocation = Settings.SettingValue<bool>("before") ? InsertLocation.Before : InsertLocation.After; // This will ofcoruse be ignore if the object exists and is replaced.
            ppEdit.Emitter.Clear();
            ParsingContext = new Focus(context); // Set the parsing context to the current focus, so that the parser can use it.
            commandWindow.EditText();
            // Nothing else. When editing is done the command window will call EnterCode with the edited text. What to do with it is determined by the insertionLocation and IsEditing flags.
         }
      }

      private static readonly Dictionary<string,InterfaceTypes> interfaceTypeMap = new() {
         ["abstr"]  = InterfaceTypes.Abstr,
         ["ext"]    = InterfaceTypes.Ext,
         ["inv"]    = InterfaceTypes.Inv,
         ["export"] = InterfaceTypes.Export,
         ["import"] = InterfaceTypes.Import,
      };
      private static InterfaceTypes InterfaceTypeFromSetting() {
         InterfaceTypes interfaceType = InterfaceTypes.None;
         foreach (string interfaceSetting in interfaceTypeMap.Keys) {
            if (Settings.SettingValue<bool>(interfaceSetting)) {
               interfaceType |= interfaceTypeMap[interfaceSetting];
            }
         }
         return interfaceType;
      }

      private void InterpretCommandRename(string args) => throw new NotImplementedException();

      private void InterpretCommandStatus() {
         WriteInfo($"CDL2 Lab Version {CDL2.Version} with database {Settings.LabDBPath}");
         Reachable.LogObjectCount(CDL2.Compiler.Reachable.AllObjects,$"in {Database.Instance.Modules.Count.Plural("module")}",WriteInfo);
      }

      private void InterpretCommandPrint(string args) {
         if (args.IsEmptyOrWhitespace) {
            if (Focus.Current.Object is not null) {
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
         if (args.IsEmptyOrWhitespace) {
            if (Focus.Current.Object is not null) {
               WriteWithInterface(Focus.Current.Object);
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
                  WriteWithInterface(sel.Object!);
               }
            }
         }

         /// <summary>
         /// Writes the fully qualified name (FQDN) of the given element, including its interface types if specified.
         /// </summary>
         /// <param name="elem">The named element to write.</param>
         void WriteWithInterface(NamedElement elem) => WriteLine(elem.FQDN(WithInterface:true));
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
      private Selection? GetMultiContext(string args) {
         if (args != "") {
            Selection selection = new(args);
            if (selection.IsInvalid) {
               WriteError(selection.ErrorMessage);
               return null;
            } else {
               return selection;
            }
         } else {
            return new Selection(Focus.Current.Selection);
         }
      }
   }
}

