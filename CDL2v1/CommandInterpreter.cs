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
using System.Text;
using System.Text.RegularExpressions;


namespace CDL2v1 {
   public class CommandInterpreter {
      private readonly ICLIREPL? REPL;
      private readonly IToaster? toaster;
      private readonly PrettyPrinter pp;
      private readonly PrettyPrinter ppFile; // For use in the print command with file setting
      private readonly PrettyPrinter ppEdit; // For use in the edit command
      private readonly Parser parser;
      private bool IsEditing = false; // Used to determine if we are currently in edit mode

      public const char CommandComment = '!';
      public const char CommandSeparator = ';'; // Not currently used. Difficult to split commands correctly when there are quoted strings.

      public CommandInterpreter(ICLIREPL? repl = null,Emitter? emitter = null,IToaster? toaster = null) {
         REPL = repl;
         this.toaster = toaster;
         // Create a CommandWindowEmitter that integrates with our window
         // Initialize the parser with the compiler and a callback for error messages
         if (REPL is not null) {
            pp = new(REPL.Emitter = emitter!,includeComments: true);

            parser = new Parser(CDL2.Compiler,(severity,msg,_) => ErrorReporter(severity,msg,_));
         } else {
            pp = new(new EmitterDebug(),includeComments: true);
            parser = new Parser(CDL2.Compiler,(severity,msg,_) => Debug.WriteLine($"{severity}: {msg}"));
         }
         ppEdit = new(new EmitterString(),includeComments: true);
         ppFile = new(new EmitterFile(),includeComments: true);
      }

      public void ErrorReporter(Severity severity,string msg,bool _) => REPL?.WriteLine($"{severity}: {msg}",severity);
      private void ReportProblem(Note note,params object[] args) => ErrorReporter(note.NoteType,note.FormattedText(args),false);


      public void SetStatus(NamedElement? element = null) {
         if (REPL is not null) {
            if (element is null) {
               REPL.SetStatus("Nothing");
            } else {
               string marker = (element is ITopLevelContainer t ? t : element.Module)?.Modified == true ? "*" : "";
               REPL.SetStatus(marker + element.FQDN(WithInterface: true));
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
      
      public bool QueryBox(string message) => REPL?.QueryBox(message) ?? false;

      /// <summary>
      /// Called to determine whether an existing object can be replaced.
      /// </summary>
      /// <param name="element"></param>
      /// <returns></returns>
      public bool CanReplace(NamedElement element) {
         if (IsEditing) return true; // We are in edit mode, so we can replace
         string objName = element is null ? "object" : element.FQDN();
         return QueryBox($"The current {objName} will be replaced. Continue?");
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
         try {
            if (input.Contains('.')) {
               IEnumerable<string> lines = SplitOnPeriods(input);
               if (lines.Count() > 1) {
                  foreach (string line in lines.SkipLast(1)) EnterSourceSentence(line,setFocus: false);
                  input = lines.Last();
               }
            }
            EnterSourceSentence(input,setFocus);
            SetStatus();
         } finally {
            IsEditing = false; // Reset editing mode after a parse
            ParsingContext = null; // Reset the parsing context after a parse
         }
      }

      /// <summary>
      /// Process a single code entry without splitting on periods.
      /// </summary>
      /// <param name="input"></param>
      /// <param name="setFocus"></param>
      private void EnterSourceSentence(string input,bool setFocus = false) {
         input = input.Trim();
         if (input[^1] != '.') input += '.';
         string inputBody = RemoveLeadingComments(input);
         string inputComment = input.Length > inputBody.Length ? input[..(input.Length - inputBody.Length)] : "";
         string firstWord = inputBody.Split('.',' ','\t','\r','\n')[0];
         SelectorType type = Abbreviation<SelectorType>.Identify(firstWord.ToUpper());
         if (type != SelectorType.INVALID) {
            input = inputComment + type + inputBody[firstWord.Length..];
            if (parser.Tokenize(input,ParseMode.Full) && parser.tokens.Count > 0) {
               if (parser.VerifyIdentity(ParsingContext)) { // Considered verified if there is no context, or the type and id match
                  // Parses the input and adds it to the DB. If an element with the same name exists, it will ask for confirmation to replace it.
                  // Must also take care of adding an undo record if something is replaced.
                  // Notice that it the edited element had notes attached, these will be lost becasue we will have a new object.
                  // When the element was inserted into the edit buffer, it will only have had the user notes, Lab generated ones are not output.
                  // This is the desired behaviour, later semantic analysis will regenerate those notes that still applied to the modified object.
                  ParsingContext parsingContext = ParsingContext.AsParsingContext;
                  if (parser.Parse(parsingContext,out NamedElement? element,CanReplace,input)) {
                     if (setFocus && element is not null) Focus.SetFocus(element);
                     element?.Module?.Modified = true;
                     if (Container.LudeSelectors.Contains(type)) {
                        Container container = parsingContext.Focus.FocusType switch {
                           ST.PROGRAM => (parsingContext.Focus.Object as Program)!,
                           ST.MODULE => (parsingContext.Focus.Object as Module)!,
                           _ => parsingContext.Focus.Object!.Section!,
                        };
                        WriteInfo($"{container} {type} added");
                     }
                  }
               } else {
                  WriteError(Note.CannotChangeIdentity.Text);
               }
            } else {
               WriteError("Lexical Analysis found no usable tokens in input.");
            }
         } else {
            WriteError($"Unknown object type: {firstWord}");
         }
      }

      /// <summary>
      /// Split input on periods that are not inside comments or quoted strings.
      /// CDL2 comments are delimited by # and can span to end of line or be closed with another #.
      /// Quoted strings use " and support $ escaping.
      /// </summary>
      /// <param name="input"></param>
      /// <returns></returns>
      private static IEnumerable<string> SplitOnPeriods(string input) {
         List<string> result = [];
         StringBuilder current = new();
         bool inQuotes = false;
         bool inComment = false;
         int length = input.Length;

         for (int i = 0 ; i < length ; i++) {
            char c = input[i];

            if (inQuotes) {
               current.Append(c);
               if (c == '$' && i + 1 < length) {
                  current.Append(input[++i]); // Escaped character
               } else if (c == '"') {
                  inQuotes = false;
               }
            } else if (inComment) {
               current.Append(c);
               if (c == '#' || c == '\n') {
                  inComment = false;
               }
            } else {
               if (c == '"') {
                  inQuotes = true;
                  current.Append(c);
               } else if (c == '#') {
                  inComment = true;
                  current.Append(c);
               } else if (c == '.') {
                  string trimmed = current.ToString().Trim();
                  if (trimmed.Length > 0) result.Add(trimmed);
                  current.Clear();
               } else {
                  current.Append(c);
               }
            }
         }

         // Add remaining content
         string final = current.ToString().Trim();
         if (final.Length > 0) result.Add(final);

         return result.Count > 0 ? result : [input.Trim()];
      }

      /// <summary>
      /// Remove leading comments from the input.
      /// </summary>
      /// <param name="input"></param>
      /// <returns></returns>
      private static string RemoveLeadingComments(string input) => Regex.Replace(input,@"^(\s*#.*?#\s*|\n)+","",RegexOptions.Singleline);

      /// <summary>
      /// Parse the input and verify whether it is syntactically correct.
      /// DO not add anything to the database, just check the syntax.
      /// </summary>
      /// <param name="input"></param>
      /// <returns></returns>
      public bool VerifySyntax(string input) {
         input = input.Trim();
         if (input.Length > 0 && input[^1] != '.') input += '.';
         if (parser.Tokenize(input, ParseMode.Check)) {
            // Use null context for syntax verification - independent of any editing session
            ParsingContext? context = null;
            return Database.WithSuspendedNamedElementRegistration(true,
                () => parser.Parse(context.AsParsingContext, out _, _ => false, input, ParseMode.Check));
         }
         return false;
      }

      /// <summary>
      /// A version of EnterCode for use by unit tests.
      /// </summary>
      /// <param name="input"></param>
      /// <returns></returns>
      public bool EnterRawCode(string input) {
         string trimmed = input.Trim();
         if (char.IsAsciiLetterUpper(trimmed[0])) {
            EnterCode(trimmed);
            return true;
         }
         return false;
      }

      ParsingContext? ParsingContext = null;

      private void WriteLine(string message,Severity severity = Severity.NONE) {
         if (REPL is not null) {
            REPL.WriteLine(message,severity);
         } else {
            Debug.WriteLine(message);
         }
      }
      private void WriteLineParsed(string message) {
         if (REPL is not null) {
            REPL.WriteLineParsed(message);
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
            SettingType.Boolean => $"-{Name}{(Value is null ? "" : (bool)Value ? "+" : "-")} ",
            SettingType.Integer => $"-{Name}={Value} ",
            SettingType.String => $"-{Name}=\"{Value}\" ",
            _ => $"-{Name}=<unknown type> "
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
         if (Settings.Abbreviations.TryGetValue(parts[0],out string? fullName)) parts[0] = fullName;
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
      /// Must be called by 
      /// </summary>
      /// <param name="input"></param>
      public void ProcessInput(string input) {
         input = input.Trim();

         if (char.IsAsciiLetterLower(input[0])) {
            // Commands start with a lowercase letter
            InterpretCommand(input);
         } else if (!input.StartsWith(CommandInterpreter.CommandComment)) { // A command comment. Can't be the CDL2 comment delimiter # becasue that is valid in CDL2 source
            EnterCode(input); // Assume it is a cdl2 construct that must be parsed
         }
      }

      /// <summary>
      /// Pasrse the command into verb, argumensts and settings and then interprete it.
      /// </summary>
      /// <param name="command"></param>
      /// <remarks>For use by unit tests and the consult command.</remarks>
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

      private static readonly ImmutableDictionary<CommandType,MoveDirection> commandAsDirection =
          new Dictionary<CommandType,MoveDirection> {
             [CommandType.next]     = MoveDirection.Forward,
             [CommandType.previous] = MoveDirection.Backward,
             [CommandType.first]    = MoveDirection.First,
             [CommandType.last]     = MoveDirection.Last,
             [CommandType.down]     = MoveDirection.Forward,
             [CommandType.up]       = MoveDirection.Backward,
             [CommandType.top]      = MoveDirection.First,
             [CommandType.bottom]   = MoveDirection.Last,
          }.ToImmutableDictionary();

      /// <summary>
      /// When the given setting is encountered in a set command, or on another command this handler is called to processor it.
      /// The handler is called when the settings is to be set with first parameter true, and when it is to be reset with false.
      /// The second parameter is the setting name. Specify in all lowercase.
      /// The third parameter is the value to be set.
      /// The return value is whether the set was successful.
      /// </summary>
      private static readonly ImmutableDictionary<string,Func<bool,string,object?,bool>> SetHandlers =
          new Dictionary<string,Func<bool,string,object?,bool>> {
             ["programname"] = SetProgram,
             ["autosaveinterval"] = SetAutoSaveInterval,
             ["target"] = SetTarget,
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
      /// Determines whether the specified target represents a valid code generator name.
      /// </summary>
      /// <remarks>This method checks whether the provided target corresponds to a registered code
      /// generator. The isSet and _ parameters do not influence the outcome.</remarks>
      /// <param name="isSet">Indicates whether the target is intended to be set. This parameter does not affect the result.</param>
      /// <param name="_">Reserved for future use. This parameter is ignored.</param>
      /// <param name="target">An object expected to be a string representing the code generator name to validate. Can be null.</param>
      /// <returns>true if the target is a string and matches a known code generator name; otherwise, false.</returns>
      private static bool SetTarget(bool isSet,string _,object? target) => target is string cg && CDL2.AvailableCodeGenerators.ContainsKey(cg);

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
               if (SetHandlers.TryGetValue(setting.Name,out Func<bool,string,object?,bool>? handler) && !handler(true,setting.Name,setting.Value)) SettingsValid = false;
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
            WriteInfo($"Command: {verb} {string.Join(" ",settings.Select(s => s.ToString()))} {args}");
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
                     if (!Focus.SetFocus(args,out string errorMessage)) {
                        WriteLineParsed(errorMessage);
                     } else if (args.IsEmptyOrWhitespace || !Settings.SettingValue<bool>("LongConsolePrompt")) {
                        WriteLine(Focus.Current.ToString());
                     }
                     break;
                  case CommandType.next:
                  case CommandType.previous:
                  case CommandType.first:
                  case CommandType.last:
                     if (!Focus.Current.MoveFocus(args,commandAsDirection[commandType],out string msg,out Severity severity)) WriteLine(msg,severity); break;

                  case CommandType.down:
                  case CommandType.up:
                  case CommandType.top:
                  case CommandType.bottom:
                     if (!InterpretCommandMoveObject(args,commandAsDirection[commandType],out msg,out severity)) WriteLine(msg,severity); break;
                  case CommandType.move:
                     if (!InterpretCommandMoveObjectTo(args,out msg,out severity)) WriteLine(msg,severity); break;

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
                     //Task.Delay(2000).ContinueWith(_ => {
                     //   ((Window)commandWindow!).Dispatcher.Invoke(() => commandWindow.Close());
                     //});
                     toaster!.ShowToast("abort command used, not saving the database.",2000,delay: true,setOwner: false);
                     REPL?.Close();
                     break;
                  case CommandType.bye:
                  case CommandType.quit:
                  case CommandType.exit:
                     toaster!.ShowToast($"Saving ${Settings.LabDBPath}",() => Database.Save(),2000);
                     REPL?.Close();
                     return;

                  case CommandType.shell:
                     InterpretCommandShell(args); break;

                  case CommandType.help:
                     InterpretCommandHelp(args); break;

                  case CommandType.analyze:
                     InterpretCommandAnalyze(args); break;
                  case CommandType.generate:
                     InterpretCommandGenerate(args);
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
      /// Return the platform dependent name of the progam to invoke for the supported shells
      /// </summary>
      /// <param name="shellName"></param>
      /// <returns></returns>
      private static string? SupportedShellCommand(string shellName) {
         if (Settings.OnWindows) {
            return shellName switch {
               "cmd" => "cmd.exe",
               "pwsh" => "pwsh.exe",
               "powershell" => "pwsh.exe",
               "bash" => "bash.exe",
               _ => null
            };
         } else if (Settings.OnLinux || Settings.OnMacOS) {
            return shellName switch {
               "bash" => "bash",
               "pwsh" => "pwsh",
               "powershell" => "pwsh",
               _ => null
            };
         } else {
            return null;
         }
      }
      private static readonly string[] SupportedShells = [ "cmd", "pwsh", "bash" ];

      private void InterpretCommandShell(string args) {
         string shell = Settings.SettingValue<string>("Shell")!.ToLower();
         string? shellCommand = SupportedShellCommand(shell)!;
         if (shellCommand is null) {
            WriteError($"Unsupported shell: {shell}. Valid values are: {string.Join(", ",SupportedShells)}");
            return;
         }
         ProcessStartInfo startInfo = shell switch {
            "cmd" => new ProcessStartInfo {
               FileName = shellCommand,
               Arguments = "/c " + args,
               RedirectStandardOutput = true,
               RedirectStandardError = true,
               UseShellExecute = false,
               CreateNoWindow = true
            },
            "pwsh" or "powershell" => new ProcessStartInfo {
               FileName = shellCommand,
               Arguments = "-NoLogo -NoProfile -Command \"$PSStyle.OutputRendering = 'PlainText'; " + args.Replace("\"","`\"") + "\"",
               RedirectStandardOutput = true,
               RedirectStandardError = true,
               UseShellExecute = false,
               CreateNoWindow = true
            },
            "bash" => new ProcessStartInfo {
               FileName = shellCommand,
               Arguments = "-c \"" + args.Replace("\"","\\\"") + "\"",
               RedirectStandardOutput = true,
               RedirectStandardError = true,
               UseShellExecute = false,
               CreateNoWindow = true
            },
            _ => throw new InvalidOperationException($"Unsupported shell: {shell}. Valid values are: cmd, pwsh, bash")
         };

         try {
            using Process process = new() { StartInfo = startInfo };
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (!string.IsNullOrEmpty(output)) WriteLine(output.TrimEnd());

            if (!string.IsNullOrEmpty(error)) WriteError(error.TrimEnd());

            if (process.ExitCode != 0) WriteWarning($"Process exited with code {process.ExitCode}");
         } catch (Exception ex) {
            WriteError($"Error executing shell command: {ex.Message}");
         }
      }

      private void InterpretCommandGenerate(string args) {
         // TODO: Pass the program derivable from the focus or settings. Same for the target code generator.
         SingleSelection? context = GetContext(args);
         Program? program = context?.Object is not null && context?.Object is Program prog ? prog : CDL2.GetMainProgram();
         if (program is not null) {
            Match targetMatch = Regex.Match(program.Comments,@"PRAGMA\s+Target\s*[=:]\s*(\w+)",RegexOptions.Compiled);
            string target = targetMatch.Success ? targetMatch.Groups[1].Value : Settings.SettingValue<string>("Target")!;
            if (CDL2.AvailableCodeGenerators.ContainsKey(target)) {
               string targetFileName = Settings.SettingValue<string>("file")!;
               CDL2.GenerateCode(ref targetFileName,target: target,program);
               WriteInfo($"{target} code generated for {program.FQDN()} into {targetFileName}");
            } else {
               WriteError($"Unknown code generator {target} specified in {(targetMatch.Success ? "program PRAGMA" : "setting")}");
            }
         }
      }

      /// <summary>
      /// Verifies that the given program name existis in the database.
      /// </summary>
      /// <param name="_1">True when setting the value, false when reseting. Since the previous value is passed on reset, no need to check.</param>
      /// <param name="_2">The name of the setting (not used).</param>
      /// <param name="programName"></param>
      /// <returns></returns>
      private static bool SetProgram(bool _1,string _2,object? programName) {
         Program? prog = Database.Instance.ProgramByName((programName as string) ?? "");
         if (prog is null) return false;
         //TODO Set the mainprogram variable here
         return true;
      }

      /// <summary>
      /// Attempts to move the currently selected object by the specified amount and direction based on the provided
      /// arguments.
      /// </summary>
      /// <remarks>The move operation is only performed if the current selection is a movable object of the
      /// appropriate type and the selection context is valid. If the operation cannot be performed, an error message
      /// and severity are set accordingly.</remarks>
      /// <param name="args">A string representing the number of positions to move the object. If not a valid integer, a default value of 1
      /// is used.</param>
      /// <param name="focusMoveDirection">The direction in which to move the selected object.</param>
      /// <param name="msg">When this method returns, contains an error message if the move operation could not be performed; otherwise,
      /// its value is undefined.</param>
      /// <param name="severity">When this method returns, contains the severity level associated with the result of the operation.</param>
      /// <returns>true if the object was successfully moved; otherwise, false.</returns>
      internal bool InterpretCommandMoveObject(string args,MoveDirection focusMoveDirection,out string msg,out Severity severity) {
         (msg,severity) = ("Immovable",Severity.Error);
         SingleSelection context = Focus.Current.Selection;
         if (context.Object is not null && context.Object is NamedElement obj && obj is Container or CDL2Object && context.ListType == SelectorType.INVALID) {
            int n = int.TryParse(args,out int a) ? a: 1;
            if (obj is not Layer || REPL!.QueryBox("Moving a layer will almost certainly mess up interfaces. Continue?")) {
               ((ISibling)obj).MoveSiblingBy(n,focusMoveDirection,recordUndo: true);
            }
            return true;
         }
         return false;
      }
      internal bool InterpretCommandMoveObjectTo(string args,out string msg,out Severity severity) {
         (msg, severity) = ("Immovable", Severity.Error);
         SingleSelection context = Focus.Current.Selection;
         SingleSelection? target = GetContext(args);
         if (!context.IsFocusable || target?.IsFocusable!=true || context.Object is null || target.Object is null || context.ObjectGuid == target.ObjectGuid) return false;
         ISibling src = context.Object;
         ISibling dst = target.Object;
         switch (dst) {
            case Program:
               if (src is not Program) return false;
               src.MoveSibling(dst);
               break;
            case Module:
               if (src is Module) {
                  src.MoveSibling(dst);
               } else if (src is Layer) {
                  msg = "Moving a Layer to another Module not implemented";
                  return false;
               } else {
                  return false;
               }
               break;
            case Layer dstLayer:
               if (src is Layer srcLayer && srcLayer.Module == dstLayer.Module) {
                  src.MoveSibling(dst);
               } else if (src is Section) {
                  msg = "Moving a Section to another Layer in the current or another Module not implemented";
               } else {
                  return false;
               }
               break;
            case Section dstSection:
               if (src is Section srcSection && srcSection.Layer == dstSection.Layer) {
                  src.MoveSibling(dst);
               } else if (src is Section) {
                  msg = "Moving a Section to another Layer in the current or another Module not implemented";
               } else {
                  return false;
               }
               break;
            case CDL2Object dstObj:
               msg = "Moving object is not yert implemented";

               break;
            default:
               return false;
         }
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
               parsedContainers.LastOrDefault()!.SetFocus();
            } else {
               // Lab mode: interpret each line as a command
               string[] lines = fileContent.Split(new[] { "\r\n","\r","\n" },StringSplitOptions.RemoveEmptyEntries);
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
         WriteLine(Settings.AllSettings.First().ToTabularString(title: true)!);
         foreach (ISetting setting in Settings.AllSettings.OrderBy(s => s.Name)) {
            WriteLine(setting.ToTabularString()!);
         }
      }

      private void InterpretCommandHelp(string args) {
         if (Settings.SettingValue<bool>("selectors")!) {
            WriteInfo("Capital letters denote the minimum abbreviation of the selector.");
            WriteInfo("Only the first letter of the selector must be capitalized.\n");
            foreach (Abbreviation<SelectorType> sel in Abbreviation<SelectorType>.FocusTypes) {
               WriteLine($"   {sel.NameWithAbbreviation,-10}   {sel.HelpText}");
            }
         } else if (Settings.SettingValue<bool>("settings")!) {
            WriteInfo("The short form of a setting is in parentheses.");
            WriteInfo("Single letter versions CANNOT be combined (e.g., '-ur' must be written '-u -r'.");
            WriteInfo("Settings marked with * are not avilable on the Lab command line.");
            static string blanks(int n) => new(' ',n);
            foreach (ISetting setting in Settings.AllSettings.OrderBy(s => s.Name)) {
               string[] desc = setting.Option.Description?.Split("\n") ?? [""];
               string abbrev = Settings.ReverseAbbreviations.TryGetValue(setting.Name,out string? abbr)
                                 ? $"({abbr})".PadRight(Settings.MaxAbbreviationLength + 2)
                                 : $"{blanks(Settings.MaxAbbreviationLength + 2)}";
               string labOnly = setting.LongOption.StartsWith("--NA") ? "*" : " ";
               WriteLine($"{(labOnly+setting.Name).PadRight(Settings.Instance.MaxNameLength+1)} {abbrev} : {desc[0]}");
               foreach (string line in desc.Skip(1)) {
                  WriteLine($"{blanks(Settings.Instance.MaxNameLength + Settings.MaxAbbreviationLength + 4)}   {line}");
               }
            }
         } else {
            WriteInfo("Commands must start with a lower case letter.");
            WriteInfo("Capital letters in the following only denote the minimum abbreviation of the command.");
            foreach (Abbreviation<CommandType> cmd in Abbreviation<CommandType>.Commands) {
               WriteLine(Regex.Replace(cmd.HelpText,@"^[a-z]+","   " + cmd.NameWithAbbreviation,RegexOptions.Compiled));
            }
            WriteInfo("Type 'help -s[electors]' to list the valid selectors.");
            WriteInfo("Type 'help -settings' or 'help -o' to list the valid settings.");
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
         string tag = "";
         if (Settings.SettingValue<bool>("list")) {
            ListUndoRedoStack(stack,stackName);
         } else if (stack.Count == 0) {
            WriteWarning($"{stackName} stack is empty.");
         } else if ((tag = Settings.SettingValue<string>("tag")!).IsNotEmptyOrWhitespace) {
            // Find the most recent record with the given tag
            int index = stack.FindIndex(r => r.Tag == tag);
            if (index < 0) {
               WriteError($"No record with tag '{tag}' found in {stackName} stack.");
               return;
            }
            // Move the found record to the top of the stack then do the operation
            stack.Surface(index);
            SingleUndoRedo(undo,stack,otherStack);
            SetStatus();
         } else {
            int n = 1;
            bool isIndex = false;
            Match m = Regex.Match(args.Trim(),@"^(:?)\s*(\d+)\s*$",RegexOptions.Compiled);
            if (m.Success) {
               isIndex = m.Groups[1].Value == ":";
               n = int.Parse(m.Groups[2].Value);
               if (n <= 0 || n > stack.Count) {
                  WriteError($"Argument {n} for {stackName} stack out of range: must be {(stack.Count == 1 ? "1 if given" : $"between 1 and {stack.Count}")}.");
                  return;
               }
            }
            if (isIndex) {
               tag = Settings.SettingValue<string>("settag")!;
               if (tag.IsNotEmptyOrWhitespace) {
                  stack[n - 1]?.Tag = tag == "-" ? "" : tag;
               } else {
                  // Move the requested record to the top of the stack and perform the operation
                  stack.Surface(n - 1);
                  SingleUndoRedo(undo,stack,otherStack);
               }
            } else {
               // Perform n operations
               for (int i = 0 ; i < n ; i++) SingleUndoRedo(undo,stack,otherStack);
            }
            SetStatus();
         }
      }

      /// <summary>
      /// List the undo or redo stack if the given setting is true or if no setting is given.
      /// </summary>
      /// <param name="stack"></param>
      /// <param name="stackName"></param>
      /// <param name="ifSetting"></param>
      /// <returns>true if listing was done</returns>
      private bool ListUndoRedoStack(BoundedStack<Database.UndoRecord> stack,string stackName,string? ifSetting = null) {
         if (ifSetting is null || Settings.SettingValue<bool>(ifSetting)) {
            int n = 0;
            WriteLine($"{stackName} stack ({stack.Count}/{stack.Capacity})");
            foreach (Database.UndoRecord record in stack) WriteLine($"{++n,3}:{record.Description()}");
            return true;
         }
         return false;
      }

      /// <summary>
      /// Perform a single undo or redo operation.
      /// The idea is that the operations are symetric, so which one is done depends on which stack is undo or redo and which is the other stack.
      /// </summary>
      /// <param name="stack">undo stack for undo, redo stack for redo</param>
      /// <param name="otherStack">redo stack for undo, undo stack for redo</param>
      /// <exception cref="NotImplementedException"></exception>
      private void SingleUndoRedo(bool undo,BoundedStack<Database.UndoRecord> stack,BoundedStack<Database.UndoRecord> otherStack) {
         Database.UndoRecord record = stack.Peek();
         CDL2Object obj = record.CDL2Object!;
         bool done = true;
         switch (record.ChangeType) {
            case ChangeType.Added:
               if (undo) {
                  Focus.MoveFocusFrom(obj);
                  obj.Section!.Declarations.Remove(obj.Id);
               } else {
                  obj.Section!.Declarations.Add(obj.Id,obj.GUID);
                  (obj as ISibling)?.MoveSiblingTo(record.Position);
                  Focus.SetFocus(obj);
               }
               break;
            case ChangeType.Removed:
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
            case ChangeType.Replaced:
               // Sufficient to swap the objects and their guids in the record
               ((CDL2Object)record.ReplacementObject!).Replace((CDL2Object)record.Object!,record:false);
               (record.ObjectGuid,record.ReplacementGuid) = (record.ReplacementGuid,record.ObjectGuid);
               break;
            case ChangeType.Renamed:
               done = false; break;
            case ChangeType.InterfaceChanged:
               InterfaceTypes currentInterfaceType = record.CDL2Object!.GetInterfaces();
               record.CDL2Object!.SetInterfaces(record.InterfaceStatus);
               record.InterfaceStatus = currentInterfaceType; // Swap the interface status for the symetric operation  
               break;
            case ChangeType.InterfaceAdded:
               if (undo) {
                  (record.Object as Section)!.Interfaces[Container.InterfaceEnumBySelector[record.InterfaceType]].Remove(record.Id);
               } else {
                  (record.Object as Section)!.Interfaces[Container.InterfaceEnumBySelector[record.InterfaceType]].Add(record.Id);
               }
               break;
            case ChangeType.InterfaceRemoved:
               if (undo) {
                  (record.Object as Section)!.Interfaces[Container.InterfaceEnumBySelector[record.InterfaceType]].Add(record.Id);
               } else {
                  (record.Object as Section)!.Interfaces[Container.InterfaceEnumBySelector[record.InterfaceType]].Remove(record.Id);
               }
               break;
            case ChangeType.LudeAdded:
               List<ID> ludes = (record.Object as Container)!.Ludes[record.LudeType];
               // For programs and modules it is alist of IDs, for sections there is only one lude per type
               if (undo) {
                  ludes.Remove(record.Id);
               } else {
                  ludes.Insert(Math.Max(0,record.Position),record.Id);
               }
               if (record.Object is Section s1) {
                  // If it is a section lude, then there is also a ludeProc Guid
                  s1.LudeProcs[record.LudeType] = undo ? null : record.LudeProcGuid;
               }
               break;
            case ChangeType.LudeRemoved:
               ludes = (record.Object as Container)!.Ludes[record.LudeType];
               // For programs and modules it is alist of IDs, for sections there is only one lude per type
               if (undo) {
                  ludes.Insert(Math.Max(0,record.Position),record.Id);
               } else {
                  ludes.Remove(record.Id);
               }
               if (record.Object is Section s2) {
                  // If it is a section lude, then there is also a ludeProc Guid
                  s2.LudeProcs[record.LudeType] = undo ? record.LudeProcGuid : null;
               }
               break;
            case ChangeType.LudeReplaced:
               done = false; break;
            default:
               throw new NotImplementedException($"Undo/Redo of change type {record.ChangeType} is unknown to SingleUndoRedo.");
         }
         if (done) {
            otherStack.Push(stack.Pop());
         } else {
            ReportProblem(Note.NotImplemented,$"Undo/Redo of change type {record.ChangeType}.");
         }
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
                     if (context.ListType != ST.INVALID) {
                        DeleteContainerLude(context,p);
                        p!.Modified = true;
                     } else {
                        Debug.Assert(p.Siblings.Contains(p.GUID),"{p} is not among its siblings.");
                        Focus.MoveFocusFrom(p); // Must move the focus first because it relies on p still being among the siblings.
                        Database.Instance.NamedElements.Remove(p.GUID);
                        Database.Instance.ElementsWithNotes.Remove(p.GUID);
                        // The above is what needs to be done for a single element. It then needs to be repeated for all children.
                        // ... Program doesn't have any, since Parts are not exactly children.
                        // CDL2.Compiler.SemanticAnalyzer!.Analyze(p);
                        WriteInfo($"{p.FQDN()} removed");
                     }
                     break;
                  case Module m:
                     if (context.ListType != ST.INVALID) {
                        DeleteContainerLude(context,m);
                        m!.Modified = true;
                     } else {
                        ReportProblem(Note.NotImplemented,$"Module delete");
                     }
                     break;
                  case Layer l:
                     ReportProblem(Note.NotImplemented,$"Layer delete");
                     break;
                  case Section s:
                     if (context.ListType != ST.INVALID) {
                        switch (context.ListType) {
                           case ST.PRELUDE:
                           case ST.ROOT:
                           case ST.POSTLUDE:
                              RW ludeType = Container.LudeTypeBySelector[context.ListType];
                              List<ID> ludes = s.Ludes[ludeType];
                              ludes.Clear();  // there is only one
                              // If it is a section lude, then there is also a ludeProc Guid
                              Database.Instance.RecordUndo(s,ludeType,context.Id,s.LudeProcs[ludeType]!.Value,ChangeType.LudeRemoved);
                              s.LudeProcs[ludeType] = null;
                              s.Module!.Modified = true;
                              break;
                           case ST.EXPORT:
                           case ST.IMPORT:
                           case ST.EXT:
                           case ST.ABSTR:
                           case ST.INV:
                              if (context.Id.IsAnonymous) {
                                 ReportProblem(Note.CannotDelete,$"All {context.ListType}",s.FQDN());
                              } else {
                                 // Single interface element removal
                                 SortedSet<ID> interfaceList = s.Interfaces[Container.InterfaceEnumBySelector[context.ListType]];
                                 if (interfaceList.Remove(context.Id)) {
                                    Database.Instance.RecordUndo(s,Container.InterfaceTypeBySelector[context.ListType],context.Id,ChangeType.InterfaceRemoved);
                                    s.Module!.Modified = true;
                                 } else {
                                    ReportProblem(Note.CannotDelete,$"Non-existent {context.ListType} {context.Id}",s.FQDN());
                                 }
                              }
                              break;
                           default:
                              ReportProblem(Note.CannotDelete,context.ListType,s.FQDN());
                              break;
                        }
                     } else {
                        ReportProblem(Note.NotImplemented,$"Section delete");
                     }
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
                           obj.Module!.Modified = true;
                        }
                     } else {
                        Focus.MoveFocusFrom(obj);
                        obj.Module!.Modified = true;
                        obj.Remove();
                     }
                     break;
                  default:
                     ReportProblem(Note.CannotDelete,Focus.Current.Object?.FQDN() ?? "<unknown>");
                     break;
               }
            }
            SetStatus();
         }
      }

      /// <summary>
      /// Delete a single or all ludes of a given type of a Program or a Module
      /// </summary>
      /// <param name="context"></param>
      /// <param name="container"></param>
      private void DeleteContainerLude(SingleSelection context,Container container) {
         switch (context.ListType) {
            case ST.PRELUDE:
            case ST.ROOT:
            case ST.POSTLUDE:
               RW ludeType = Container.LudeTypeBySelector[context.ListType];
               List<ID> ludes = container.Ludes[ludeType];
               if (context.Id.IsAnonymous) {
                  foreach (ID id in ludes) Database.Instance.RecordUndo(container,ludeType,id,ChangeType.LudeRemoved);
                  ludes.Clear();
               } else {
                  Database.Instance.RecordUndo(container,ludeType,context.Id,ChangeType.LudeRemoved);
                  ludes.Remove(context.Id);
               }
               break;
            default:
               ReportProblem(Note.CannotDelete,context.ListType,container.FQDN());
               break;
         }
      }

      /// <summary>
      /// Selects the single available lude from the specified section if no lude is currently selected; otherwise,
      /// returns the provided lude value.
      /// </summary>
      /// <remarks>If the section contains exactly one lude with a count greater than zero and the current
      /// selection is RW.NONE, this method returns that lude. In all other cases, it returns the original
      /// selection.</remarks>
      /// <param name="rw">The current lude selection. If set to RW.NONE, the method may select a lude from the section.</param>
      /// <param name="sec">The section from which to select a lude if only one is available.</param>
      /// <returns>The selected lude if exactly one lude is available in the section and no lude is currently selected;
      /// otherwise, returns the original lude value.</returns>
      private static RW SelectSingleSectionLude(RW rw,Section sec) => rw == RW.NONE && sec.Ludes.Values.Sum(v => v.Count) == 1 ? sec.Ludes.Keys.Where(v => sec.Ludes[v].Count > 0).First() : rw;

      /// <summary>
      /// Edit the selected object.
      /// Currently only a single section, algorithm or constant can be edited.
      /// </summary>
      /// <param name="args"></param>
      private void InterpretCommandEdit(string args) {
         if (REPL == null) return; // Ignore the command if there is no REPL
         Selection? selection = GetMultiContext(args);
         if (selection is null || selection.Count != 1) {
            WriteError("Only a single object can be edited.");
            return;
         }
         SingleSelection? context = selection.First();
         if (context == null || context.Object == null || !context.IsFocusable) {
            WriteError("Can't edit.");
         } else if (context.Object is Container && context.ListType == ST.INVALID) {
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
            ParsingContext = new(new Focus(context),InsertLocation.Replace); // Set the parsing context to the current focus, so that the parser can use it.
            if (context.Object is Const or LIST or Algorithm) {
               // Only Vars cannot be edited
               ppEdit.SupressNotes = true;   // Klude to omit printing of notes.
               REPL.EditText(ppEdit.Print(context.Object));
               ppEdit.SupressNotes = true;
            } else if (context.Object is Section sec && context.ListType != ST.INVALID) {
               // Special case: editing the prelude of a section.
               RW ludeType = SelectSingleSectionLude(context.ListType switch {
                  ST.PRELUDE => RW.PRELUDE,
                  ST.ROOT => RW.ROOT,
                  ST.POSTLUDE => RW.POSTLUDE,
                  ST.LUDE => RW.NONE,
                  _ => RW.NONE
               },sec);
               if (context.ListType == ST.LUDE && sec.Ludes.Values.Sum(v=>v.Count) == 1) {
                  // There is only one lude, so select that
                  ludeType = sec.Ludes.Keys.Where(v => sec.Ludes[v].Count > 0).First();
               }
               if (ludeType != RW.NONE) {
                  if (sec.Ludes[ludeType].Count == 0) {
                     WriteError($"{sec} does not have a {ludeType}.");
                     IsEditing = false;
                     ParsingContext = null;
                     return;
                  } else {
                     ParsingContext.LudeType = ludeType;
                     ppEdit.SupressNotes = true;
                     REPL.EditText(ppEdit.PrintLude(ludeType,sec,asString: true)!);
                     ppEdit.SupressNotes = false;
                  }
               } else if (context.ListType == ST.LUDE) {
                  WriteError("Can't edit. Specify the specific lude instead of LUDE");
               } else { 
                  WriteError("Can't edit.");
               }
            } else {
               WriteError("Can't edit");
               IsEditing = false;
               return;
            }
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
         if (REPL == null) return; // Ignore the command if there is no command window
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
            InsertLocation insertLocation = Settings.Before ? InsertLocation.Before : InsertLocation.After; // This will of coruse be ignored if the object exists and is replaced.
            ppEdit.Emitter.Clear();
            ParsingContext = new(new Focus(context),insertLocation);
            REPL.EditText();
            // Nothing else. When editing is done the command window will call EnterCode with the edited text. What to do with it is determined by the insertionLocation and IsEditing flags.
         }
      }

      private static readonly Dictionary<string,InterfaceTypes> interfaceTypeMap = new() {
         ["abstr"] = InterfaceTypes.Abstr,
         ["ext"] = InterfaceTypes.Ext,
         ["inv"] = InterfaceTypes.Inv,
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

      private void InterpretCommandRename(string args) {
         if (string.IsNullOrEmpty(args)) return;
         string[] parts = Regex.Split(args,@"\s*=\s*",RegexOptions.Compiled);
         if (parts.Length != 2) {
            WriteError("Usage: rename [selector] = <newName>.");
            return;
         }
         string sel = parts[0].Trim();
         string newName = parts[1].Trim();
         if (string.IsNullOrEmpty(newName) || !Token.IdRE().IsMatch(newName)) {
            WriteError($"'{newName}' is not a valid CDL2 identifier.");
            return;
         }
         string newCannonicalName = newName.Replace(" ","");

         // Perform the rename operation
         SingleSelection? context = GetContext(sel);
         if (context is not null) {
            NamedElement? obj = context.Object;
            if (obj is null) {
               WriteError("No object to rename");
               return;
            }
            bool differentNameName = newCannonicalName != obj.Id.CanonicalName;
            ITopLevelContainer modifiedContainer;
            bool mainProgramBeingRenamed = false;
            bool refs = Settings.SettingValue<bool>("refs");
            switch (obj) {
               case Program prog:
                  if (differentNameName && Database.Instance.ProgramByName(newName) is not null) {
                     WriteError($"A program named {newName} already exists.");
                     return;
                  }
                  modifiedContainer = prog;
                  mainProgramBeingRenamed = Settings.SettingValue<string>("ProgramName")!.Replace(" ","") == prog.Id.CanonicalName;
                  break;
               case Module mod:
                  if (differentNameName && Database.Instance.ModuleByName(newName) is not null) {
                     WriteError($"A module named {newName} already exists.");
                     return;
                  }
                  modifiedContainer = mod;
                  break;
               case Layer layer:
                  if (differentNameName && !layer.Module!.Layers.All(layer => layer.Id.CanonicalName != newCannonicalName)) {
                     WriteError($"A layer named {newName} already exists in the module.");
                     return;
                  }
                  modifiedContainer = layer.Module!;
                  break;
               case Section sec:
                  if (differentNameName && !sec.Layer!.Sections.All(section => section.Id.CanonicalName != newCannonicalName)) {
                     WriteError($"A section named {newName} already exists in the layer.");
                     return;
                  }
                  modifiedContainer = sec.Module!;
                  break;
               case CDL2Object cDL2Object:
                  Section section = cDL2Object.Section!;
                  if (differentNameName && !section!.Declarations.Keys.All(id => id.CanonicalName != newCannonicalName)) {
                     WriteError($"An object named {newName} already exists in the section.");
                     return;
                  }
                  if (!section.Interfaces[InterfaceTypes.Inv].All(id => id.CanonicalName != newCannonicalName)) {
                     WriteError($"{newName} is in the INV list of this section.");
                     return;
                  }
                  if (!section.Interfaces[InterfaceTypes.Import].All(id => id.CanonicalName != newCannonicalName)) {
                     WriteError($"{newName} is in the IMPORT list of this section.");
                     return;
                  }
                  modifiedContainer = cDL2Object.Module!;
                  break;
               default:
                  WriteError($"Cannot rename {obj.FQDN()}");
                  return;
            }

            modifiedContainer.Modified = true;
            Database.Instance.RecordUndo(obj.Id,Database.Instance.DisplayName(obj.Id.CanonicalName),newName,updateReferences: refs,changeType: ChangeType.Renamed);
            obj.Rename(newName,updateReferences: refs);
            if (mainProgramBeingRenamed) Settings.SettingValue<string>("ProgramName",newName);
         }
      }
      private void InterpretCommandStatus() {
         WriteInfo($"CDL2 Lab Version {CDL2.Version} with database {Settings.LabDBPath}");
         Reachable.LogObjectCount(CDL2.Compiler.Reachable.AllObjects,$"in {Database.Instance.Modules.Count.Plural("module")}",WriteInfo,0);
         WriteInfo($" Available code generators: {string.Join(", ",CDL2.AvailableCodeGenerators.Keys)}; Target={Settings.SettingValue<string>("Target")}");
      }

      /// <summary>
      /// Run the print command.
      /// The -file setting must be of the form <filename> or <filename>::append. Filename may be empty to refer to the previous file.
      /// </summary>
      /// <param name="args"></param>
      private void InterpretCommandPrint(string args) {
         string fileName = Settings.SettingValue<string>("file")?.Trim('"') ?? "";
         PrettyPrinter ppTarget;
         bool withComment;

         if (fileName != "") {
            try {
               ppFile.Emitter.Target = fileName;
               WriteInfo(ppFile.Emitter.TargetInfo);
            } catch (IOException e) {
               WriteError(e.Message);
               return;
            }
            ppTarget = ppFile;
            withComment = true;
         } else {
            // Print to the regular place.
            ppTarget = pp;
            withComment = false;
         }

         if (args.IsEmptyOrWhitespace) {
            if (Focus.Current.Object is not null) {
               ppTarget.PauseUpdate(() => ppTarget.Print(Focus.Current.Object,withComment));
               ppTarget.Emitter.Close();
            }
         } else {
            Selection selection = new(args);
            if (selection.IsInvalid) {
               WriteError(selection.ErrorMessage);
            } else if (selection.Count == 0) {
               WriteInfo("No matches for selector");
            } else {
               ppTarget.PauseUpdate(() => {
                  foreach (SingleSelection sel in selection) {
                     ppTarget.Print(sel,withComment);
                  }
               });
               ppTarget.Emitter.Close();
            }
         }
      }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="args"></param>
      private void InterpretCommandList(string args) {
         if (args.IsEmptyOrWhitespace) {
            if (ListUndoRedoStack(Database.Instance.UndoStack,"Undo",ifSetting: "undo") | ListUndoRedoStack(Database.Instance.RedoStack,"Redo",ifSetting: "redo")) return;

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
               WriteInfo("No matches for selector");
            } else {
               foreach (SingleSelection sel in selection) {
                  switch (sel.ListType) {
                     case SelectorType.PRELUDE or SelectorType.ROOT or SelectorType.POSTLUDE:
                        ListLude(sel,sel.ListType);
                        break;
                     case SelectorType.LUDE:
                        ListLude(sel,SelectorType.PRELUDE);
                        ListLude(sel,SelectorType.ROOT);
                        ListLude(sel,SelectorType.POSTLUDE);                        
                        break;
                     default:
                        WriteWithInterface(sel.Object!);
                     break;
                  }
               }
            }
         }

         /// <summary>
         /// Writes the fully qualified name (FQDN) of the given element, including its interface types if specified.
         /// </summary>
         /// <param name="elem">The named element to write.</param>
         void WriteWithInterface(NamedElement elem) => WriteLine(elem.FQDN(WithInterface: true));

         void ListLude(SingleSelection sel,SelectorType listType) {
            if (sel.Object is Container c1) {
               if (c1.Ludes[Container.LudeTypeBySelector[listType]].Count > 0) WriteLine($"{c1.FQDN()} {listType}");
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

