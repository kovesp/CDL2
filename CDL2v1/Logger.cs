using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal class Logger {
      public static void Log(int level,string message) {
         if (level >= CDL2.Compiler.VerbosityLevel) Console.WriteLine(message);
      }

      public static void Log(string message) => Log(0,message);

      public static void LogError(string message) => Console.WriteLine($"Error: {message}");

      public static void ReportError(string message) {
         LogError($"{CDL2.Compiler.Parser?.currentObject.ToString()}: {message}");
         CDL2.Compiler.SkipToNextEnd();
      }
   }
}
