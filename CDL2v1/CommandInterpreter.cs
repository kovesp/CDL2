using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal class CommandInterpreter {
      internal void IntepretCommand(string command, CommandType commandType, string settings, string args,CommandPromptWindow commandWindow) {
         IEnumerable<string> arguments = Regex.Split(command, @"\s+").Skip(1).Select(s=>s.Trim());
         //commandWindow.WriteLine($"> {commandType} {string.Join(" ",arguments)}");
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
               // Trial command to list modules
               Database.Instance.Modules.ForEach(modGuid => commandWindow.WriteLine(NamedElement.From<Module>(modGuid)?.FQDN()??""));
               break;
            case CommandType.print:
               // Handle print command
               string[] parts = command.Split(' ', 2);
               if (parts.Length < 2) {
                  commandWindow.WriteLine("Usage: print <message>");
                  return;
               }
               string message = parts[1];
               commandWindow.WriteLine($"Print: {message}");
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
            case CommandType.quit:
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
