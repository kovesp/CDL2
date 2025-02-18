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

      protected abstract void  Write(string items);

      private string CurrentLine = "";

      /// <summary>
      /// Emit code to the target.
      /// ToString is used on the objects.
      /// </summary>
      /// <param name="code"></param>
      public void Emit(params object[] code) => WriteWithIndent(0,false,false,code);
      /// <summary>
      /// Like <see cref="Emitnl(object[])"/> with a new line added.
      /// </summary>
      /// <param name="code"></param>
      public void Emitnl(params object[] code) => WriteWithIndent(0,false,true,code);

      /// <summary>
      /// Like <see cref="Emit( object[])"/> with an indentation of indentLeverl*IndentWidth.
      /// </summary>
      /// <param name="indentLevel"></param>
      /// <param name="code"></param>
      public void Emit(int indentLevel,params object[] code) => WriteWithIndent(indentLevel,false,false,code);
      /// <summary>
      /// Like <see cref="Emit(int, object[])"/> with a new line added at the end.
      /// This is multipl
      /// </summary>
      /// <param name="indentLevel"></param>
      /// <param name="code"></param>
      public void Emitnl(int indentLevel,params object[] code)=> WriteWithIndent(indentLevel,false,true,code);
      /// <summary>
      /// Like <see cref="Emit(int, object[])"/> with a new line added at the begining.
      /// </summary>
      /// <param name="indentLevel"></param>
      /// <param name="code"></param>
      public void NlEmit(int indentLevel,params object[] code) => WriteWithIndent(indentLevel,true,false,code);
      /// <summary>
      /// Like <see cref="Emit(int, object[])"/> with a new line added at the begining and end.
      /// </summary>
      /// <param name="indentLevel"></param>
      /// <param name="code"></param>
      public void NlEmitnl(int indentLevel,params object[] code) => WriteWithIndent(indentLevel,true,true,code);

      protected void WriteWithIndent(int level,bool nlbefore,bool nlafter,params object[] items) {
         WriteNewLine(nlbefore);
         foreach (object item in items) {
            string currentItem = item?.ToString() ?? "";
            if (CurrentLine.Length + currentItem.Length > LineWidth) WriteNewLine(true);
            CurrentLine += currentItem;
         }
         WriteNewLine(nlafter);

         void WriteNewLine(bool nl) {
            if (nl) {
               Write(CurrentLine);
               Write("\n");
               CurrentLine = new string(' ',level * IndentWidth);               
            }
         }
      }

   }
}
