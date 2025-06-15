using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   /// <summary>
   /// A simple logger class that can be used to log messages at different levels.
   /// </summary>
   internal class Logger {
      /// <summary>
      /// The logger instance. This is a singleton.
      /// </summary>
      public static Logger logger { get; private set; }
      /// <summary>
      /// Initialize the singleton.
      /// </summary>
      static Logger() => logger = new Logger();

      /// <summary>
      /// A replaceable action called by ReportError.
      /// </summary>
      public Action? ErrorAction { get; set; }
      /// <summary>
      /// A replaceable action to actually write the message. The default is _WriteLine defined here.
      /// </summary>
      /// <param Id="level">The level of the message. In order for the line to bw written the level must be less or equal
      ///         to the Compilerr.VerbosityLevel for the main output and Compiler.DebugVerbosityLevel for the debug output.
      /// </param>
      /// <param Id="message">The message to write.</param>"
      public Action<int,string> WriteLine { get; set; }

      public object? CurrentObject = null;

      /// <summary>
      /// Create a logger with the given actions.
      /// </summary>
      /// <param Id="errorAction"></param>
      /// <param Id="writeLine"></param>
      public Logger(Action? errorAction,Action<int,string> writeLine) {
         ErrorAction = errorAction;
         WriteLine = writeLine;
      }
      /// <summary>
      /// 
      /// </summary>
      /// <param Id="errorAction"></param>
      public  Logger(Action? errorAction) : this(errorAction,_WriteLine) { }
      /// <summary>
      /// Create a logger with the default actions.
      /// </summary>
      public Logger() : this(null,_WriteLine) { }

      /// <summary>
      /// Log a message at the given level.
      /// </summary>
      /// <param Id="level"></param>
      /// <param Id="message"></param>
      public void _Log(int level,string message) {
         string prefix = "CDL2: "+ new string(' ',3 * level);
         message = prefix+message.Replace("\n","\n" + prefix);
         WriteLine(level,message);
      }

      /// <summary>
      /// Log a message at level 0.
      /// </summary>
      /// <param Id="message"></param>
      public void _Log(string message) => _Log(0,message);

      /// <summary>
      /// Log an error message.
      /// </summary>
      /// <param Id="message"></param>
      private void _LogError(string message) => WriteLine(-1, string.Join('\n', message.Split('\n').Select(line => $"CDL2 Error: {line}")));

      /// <summary>
      /// Report an error message.
      /// If the ErrorAction is not null, it is called.
      /// </summary>
      /// <param Id="message"></param>
      public void _ReportError(string message, bool suppressErrorAction = false) {
         string? currentObj = CurrentObject?.ToString();
         _LogError($"{(currentObj is null?"":": ")}{message}");
         if (!suppressErrorAction && ErrorAction is not null) ErrorAction();
      }

      /// <summary>
      /// Static versions of the logger methods used with the singleton instance.
      /// </summary>
      /// <param Id="level"></param>
      /// <param Id="message"></param>
      public static void Log(int level,string message) => logger._Log(level,message);
      public static void Log(string message) => logger._Log(0,message);
      public static void LogError(string message) => logger._LogError(message);
      public static void ReportError(string message,bool suppressErrorAction=false) => logger._ReportError(message, suppressErrorAction: suppressErrorAction);

      /// <summary>
      /// Writes a message to the console and debug output if the verbosity level is high enough.
      /// Can be replaced via the WriteLine property.
      /// </summary>
      /// <param Id="level"></param>
      /// <param Id="message"></param>
      private static void _WriteLine(int level,string message) {
         if (Settings.Verbosity(level)) Console.WriteLine(message);
         if (Settings.DebugVerbosity(level)) Debug.WriteLine(message);
      }
   }
}
