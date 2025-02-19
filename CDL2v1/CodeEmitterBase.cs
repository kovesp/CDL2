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

      /// <summary>
      /// Write the item to the target. Must be supplied by concrete sublcasses.
      /// </summary>
      /// <param name="item"></param>
      protected abstract void  Write(string item);

      /// <summary>
      /// The current line being built up.
      /// </summary>
      private string CurrentLine = "";

      /// <summary>
      /// Emit code to the target.
      /// ToString is used on the objects.
      /// </summary>
      /// <param name="code"></param>
      public bool Emit(params object[] code) => WriteWithIndent(0,false,false,code);
      /// <summary>
      /// Like <see cref="Emitnl(object[])"/> with a new line added.
      /// </summary>
      /// <param name="code"></param>
      /// <returns>True if a new line was written.</returns>
      public bool Emitnl(params object[] code) => WriteWithIndent(0,false,true,code);

      /// <summary>
      /// Like <see cref="Emit( object[])"/> with an indentation of indentLeverl*IndentWidth.
      /// </summary>
      /// <param name="indentLevel"></param>
      /// <param name="code"></param>
      /// <returns>True if a new line was written.</returns>
      public bool Emit(int indentLevel,params object[] code) => WriteWithIndent(indentLevel,false,false,code);
      /// <summary>
      /// Like <see cref="Emit(int, object[])"/> with a new line added at the end.
      /// This is multipl
      /// </summary>
      /// <param name="indentLevel"></param>
      /// <param name="code"></param>
      /// <returns>True if a new line was written.</returns>
      public bool Emitnl(int indentLevel,params object[] code)=> WriteWithIndent(indentLevel,false,true,code);
      /// <summary>
      /// Like <see cref="Emit(int, object[])"/> with a new line added at the begining.
      /// </summary>
      /// <param name="indentLevel"></param>
      /// <param name="code"></param>
      /// <returns>True if a new line was written.</returns>
      public bool NlEmit(int indentLevel,params object[] code) => WriteWithIndent(indentLevel,true,false,code);
      /// <summary>
      /// Like <see cref="Emit(int, object[])"/> with a new line added at the begining and end.
      /// </summary>
      /// <param name="indentLevel"></param>
      /// <param name="code"></param>
      /// <returns>True if a new line was written.</returns>
      public bool NlEmitnl(int indentLevel,params object[] code) => WriteWithIndent(indentLevel,true,true,code);

      /// <summary>
      /// This is the method that actually writes the code to the target using the <see cref="Write(string)"/> method.
      /// </summary>
      /// <param name="level"></param>
      /// <param name="nlbefore"></param>
      /// <param name="nlafter"></param>
      /// <param name="items"></param>
      /// <returns>True if a new line was written.</returns>
      protected bool WriteWithIndent(int level,bool nlbefore,bool nlafter,params object[] items) {
         bool wasNewline = WriteNewLine(nlbefore);
         if (CurrentLine.Length == 0) CurrentLine = new string(' ',level * IndentWidth);
         foreach (object item in items) {
            string[] currentItems = (item?.ToString() ?? "").Split('\n');
            if (CurrentLine.Length + currentItems[0].Length > LineWidth) wasNewline = wasNewline || WriteNewLine(true);
            CurrentLine += currentItems[0];
            foreach (string currentItem in currentItems.Skip(1)) {
               wasNewline = wasNewline || WriteNewLine(true);
               CurrentLine += currentItem;
            }
         }
         return wasNewline || WriteNewLine(nlafter);

         // Write the current line to the target. Add a newline if requested and return the request.
         bool WriteNewLine(bool nl) {
            if (nl) {
               Write(CurrentLine);
               Write("\n");
               CurrentLine = "";               
            }
            return nl;
         }
      }

   }
}
