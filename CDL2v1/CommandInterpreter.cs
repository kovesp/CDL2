using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CDL2v1 {
   public class Command(string name, int minLength) {
      public readonly string Name = name;
      public readonly int MinLength = minLength;
      public readonly Type CommandType = (Type)Enum.Parse(typeof(Type), name, true);
      public enum Type {
         INVALID,
         focus,
         next,
         prev,
         list,
         print,
         set,
         replace,
         append,
         insert,
         edit,
         undo,
         generate,
         quit,
         help,
      }
      private readonly static Set<Command> Commands = [
         new ("focus"   , 1),
            new ("next"    , 1),
            new ("prev"    , 1),
            new ("list"    , 1),
            new ("print"   , 2),
            new ("set"     , 3),
            new ("replace" , 1),
            new ("append"  , 1),
            new ("insert"  , 1),
            new ("edit"    , 1),
            new ("undo"    , 1),
            new ("generate", 1),
            new ("quit"    , 4),
            new ("help"    , 1),
         ];

      public static Type Identify(string command) {
         if (string.IsNullOrWhiteSpace(command)) return Type.INVALID;
         command = command.Trim().ToLowerInvariant();
         foreach (Command cmd in Commands) {
            if (command.Length >= cmd.MinLength && command.StartsWith(cmd.Name)) {
               return cmd.CommandType;
            }
         }
         return Type.INVALID;
      }
   }
   internal class CommandInterpreter {
      internal void IntepretCommand(string command, Command.Type commandType, CommandPromptWindow commandWindow) {
         IEnumerable<string> arguments = Regex.Split(command, @"\s+").Skip(1).Select(s=>s.Trim());
         //commandWindow.WriteLine($"> {commandType} {string.Join(" ",arguments)}");
         switch (commandType) {
            case Command.Type.INVALID:
               commandWindow.WriteLine($"Invalid command: {command}");
               return;
            case Command.Type.focus:
               // Handle focus command
               commandWindow.WriteLine("Focus command executed");
               break;
            case Command.Type.next:
               // Handle next command
               commandWindow.WriteLine("Next command executed");
               break;
            case Command.Type.prev:
               // Handle previous command
               commandWindow.WriteLine("Previous command executed");
               break;
            case Command.Type.list:
               // Trial command to list modules
               Database.Instance.Modules.ForEach(modGuid => commandWindow.WriteLine(NamedElement.From<Module>(modGuid).FQDN()));
               break;
            case Command.Type.print:
               // Handle print command
               string[] parts = command.Split(' ', 2);
               if (parts.Length < 2) {
                  commandWindow.WriteLine("Usage: print <message>");
                  return;
               }
               string message = parts[1];
               commandWindow.WriteLine($"Print: {message}");
               break;
            case Command.Type.set:
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
            case Command.Type.replace:
            case Command.Type.append:
            case Command.Type.insert:
            case Command.Type.edit:
            case Command.Type.undo:
               break;
            case Command.Type.quit:
               commandWindow.Close();
               return;
            case Command.Type.help:
               commandWindow.WriteLine("Available commands:");
               commandWindow.WriteLine("  generate        - Generate code");
               commandWindow.WriteLine("  quit            - Close this window");
               break;
            case Command.Type.generate:

               break;

            default:
                  // Handle other commands as needed
               break;
         }
      }
   }
}
