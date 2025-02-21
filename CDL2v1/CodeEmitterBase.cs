using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal abstract class CodeEmitterBase {
      public virtual string Target { get; set; } = "";
      public int IndentWidth { get; set; } = 3;
      public int LineWidth { get; set; } = 100;
      public int IndentLevel { get; set; } = 0;

      /// <summary>
      /// This is the prefix that is added to each line of output.
      /// It is up to the concrete subclass to use or ignore this
      /// </summary>
      public string LinePrefix { get; set; } = "";

      /// <summary>
      /// Write the item to the target. Must be supplied by concrete sublcasses.
      /// </summary>
      /// <param name="item"></param>
      protected abstract void  WriteLine(string item);

      /// <summary>
      /// The current line being built up.
      /// </summary>
      private string CurrentLine = "";

      /// <summary>
      /// Emit code to the target.
      /// ToString is used on the objects.
      /// </summary>
      /// <param name="code"></param>
      public bool Emit(params object[] code) => WriteWithIndent(false,false,true,code);
      /// <summary>
      /// Like <see cref="Emitnl(object[])"/> with a new line added.
      /// </summary>
      /// <param name="code"></param>
      /// <returns>True if a new line was written.</returns>
      public bool Emitnl(params object[] code) => WriteWithIndent(false,true,true,code);

      /// <summary>
      /// Like <see cref="Emit(int, object[])"/> with a new line added at the begining.
      /// </summary>
      /// <param name="indentLevel"></param>
      /// <param name="code"></param>
      /// <returns>True if a new line was written.</returns>
      public bool NlEmit(params object[] code) => WriteWithIndent(true,false,true,code);
      /// <summary>
      /// Like <see cref="Emit(int, object[])"/> with a new line added at the begining and end.
      /// </summary>
      /// <param name="indentLevel"></param>
      /// <param name="code"></param>
      /// <returns>True if a new line was written.</returns>
      public bool NlEmitnl(params object[] code) => WriteWithIndent(true,true,true,code);

      /// <summary>
      /// Emit a string to the target without a new line.
      /// </summary>
      /// <param name="s"></param>
      internal void EmitIgnoreLineLength(string s,bool NL=false) => WriteWithIndent(false,NL,false,s);

      /// <summary>
      /// This is the method that actually writes the code to the target using the <see cref="Write(string)"/> method.
      /// </summary>
      /// <param name="nlbefore"></param>
      /// <param name="nlafter"></param>
      /// <param name="honorLineLength"></param>
      /// 
      /// <returns>True if a new line was written.</returns>
      /// <param name="items"></param>
      protected bool WriteWithIndent(bool nlbefore,bool nlafter,bool honorLineLength = true,params object[] items) {
         bool wasNewline = WriteNewLine(nlbefore && CurrentLine.Trim().Length > 0);
         foreach (object item in items) {
            string[] currentItems = (item?.ToString() ?? "").Split('\n');
            if (honorLineLength && CurrentLine.Length + currentItems[0].Length > LineWidth) wasNewline = wasNewline || WriteNewLine(true);
            AddToCurrentLine(currentItems[0]);
            foreach (string currentItem in currentItems.Skip(1)) {
               wasNewline = wasNewline || WriteNewLine(true);
               AddToCurrentLine(currentItem);
            }
         }
         return wasNewline || WriteNewLine(nlafter);

         // Initialize CurrentLine with the indent if empty.
         void AddToCurrentLine(string str) {
            if (CurrentLine.Length == 0) CurrentLine = new string(' ',IndentLevel * IndentWidth);
            CurrentLine += str;
         }

         // Write the current line to the target. Add a newline if requested and return the request.
         bool WriteNewLine(bool nl) {
            if (nl) {
               WriteLine(CurrentLine);
               CurrentLine = "";               
            }
            return nl;
         }
      }
   }
}
