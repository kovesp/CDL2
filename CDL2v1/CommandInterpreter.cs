using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CDL2v1 {
   public class CommandInterpreter {
      private readonly CommandPromptWindow commandWindow;
      private readonly PrettyPrinter pp;

      public CommandInterpreter(CommandPromptWindow window) {
         commandWindow = window;
         pp = new(new CommandWindowEmitter(commandWindow), includeComments: true);
      }

      internal void IntepretCommand(string command, CommandType commandType, string settings, string args,CommandPromptWindow commandWindow) {
         IEnumerable<string> arguments = Regex.Split(command, @"\s+").Skip(1).Select(s=>s.Trim());
         //commandWindow.WriteLine($"> {commandType} {string.Join(" ",arguments)}");
         string[] parts;

         switch (commandType) {
            case CommandType.INVALID:
               commandWindow.WriteLine($"Invalid command: {command}");
               return;
            case CommandType.focus:
               Focus.SetFocus(args);
               commandWindow.WriteLine(Focus.Current.ToString());
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
                     commandWindow.WriteLine($"Nothing");
                  }
                  return;
               } else {
                  Selection selection = new(args);
                  foreach (SingleSelection sel in selection) {
                     commandWindow.WriteLine(sel.Object!.FQDN());
                  }
               }
               break;
            case CommandType.print:
               if (args == "") {
                  if (Focus.Current.Object is not null) {
                     //TODO: Ignore Focus subojbest for now
                     commandWindow.WriteLine(Focus.Current.Object.FQDN());
                  } else {
                     commandWindow.WriteLine($"Nothing");
                  }
                  return;
               } else {
                  Selection selection = new(args);
                  foreach (SingleSelection sel in selection) {
                     pp.Print(sel.Object!);
                  }
               }

               break;
            case CommandType.set:
               // Handle set command
               parts = command.Split(' ', 3);
               if (parts.Length < 3) {
                  commandWindow.WriteLine("Usage: set <key> <value>");
                  return;
               }
               string key = parts[1];
               string value = parts[2];
               // Set logic here
               commandWindow.WriteLine($"Set {key} to {value}");
               break;
            case CommandType.status:
               Reachable.LogObjectCount(CDL2.Compiler.Reachable.AllObjects,$"in {Database.Instance.Modules.Count.Plural("module")}", commandWindow.WriteLine);
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
               commandWindow.WriteLine($"Saved: {Database.Save()}");               
               break;
            case CommandType.quit:
            case CommandType.exit:
               commandWindow.Close();
               return;
            case CommandType.help:
               if (args == "") {
                  commandWindow.WriteLine("Capital letters denote the minimum abbreviation of the command.\n");
                  foreach (Abbreviation<CommandType> cmd in Abbreviation<CommandType>.Commands) {
                     commandWindow.WriteLine(Regex.Replace(cmd.HelpText,@"^[a-z]+","   "+cmd.NameWithAbbreviation,RegexOptions.Compiled));
                  }
               }
               break;
            case CommandType.generate:
               // TDOD: Pass the program derivable from the focus or settings. Same for the target code generator.
               Program? program = CDL2.GetMainProgram();
               if (program is not null) {
                  CDL2.GenerateCode(out string targetFileName,program);
                  commandWindow.WriteLine($"{Settings.SettingValue<string>("Target")} code generated for {program.FQDN()} into {targetFileName}");
               }
               break;
            default:
                  // Handle other commands as needed
               break;
         }
      }
   }
}
