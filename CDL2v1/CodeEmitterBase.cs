using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal abstract class CodeEmitterBase {
      public virtual string Target { get; set; } = "";
      /// <summary>
      /// The number of spaces to use for each level of indentation.
      /// </summary>
      public int IndentWidth { get; set; } = 3;
      /// <summary>
      /// The number of characters to use for the line width.
      /// </summary>
      public int LineLength { get; set; } = 100;
      /// <summary>
      /// The current level of indentation.
      /// Note that whyen it decreases, ExtraIndent is reset to 0.
      /// </summary>
      public int IndentLevel { 
         get => indentLevel;
         set {
            if (value < indentLevel) ExtraIndent = 0;
            indentLevel = value;
         } 
      }
      private int indentLevel = 0;
      /// <summary>
      /// The maximum ExtraIndent that can be added to the current line.
      /// </summary>
      public int MaxExtraIndent { get; set; } = 3;
      /// <summary>
      /// The number of extra indent levels to add to the current line.
      /// This is reset to 0 when IndentLevel is decremented.
      /// </summary>
      public int ExtraIndent { 
         get => extraIndent; 
         set { if (value >= 0 && value <= MaxExtraIndent) extraIndent = value; } 
      }
      private int extraIndent = 0;
      /// <summary>
      /// While set to true, all output is buffered until set to false.
      /// This results in keeping all of that on one line.
      /// </summary>
      public bool KeepTogether {
         get => KeepTogetherState;
         set {
            KeepTogetherState = value;
            if (KeepTogetherState) {
               KeepTogetherBuffer = "";
            } else {
               WriteWithIndent(nlbefore: false,nlafter: false,honorLineLength: true,extraSpace: false,KeepTogetherBuffer);
               KeepTogetherBuffer = "";
            }
         }
      }
      /// <summary>
      /// If true, the LineLength is ignored.
      /// </summary>
      public bool IgnoreLineLength { get; set; } = false;

      private string KeepTogetherBuffer = "";
      private bool KeepTogetherState = false;

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
      public bool Emit(params object[] code) => WriteWithIndent(nlbefore:false,nlafter:false,honorLineLength:true,extraSpace:false,code);
      /// <summary>
      /// Like <see cref="Emitnl(object[])"/> with a new line added.
      /// </summary>
      /// <param name="code"></param>
      /// <returns>True if a new line was written.</returns>
      public bool Emitnl(params object[] code) => WriteWithIndent(nlbefore:false,nlafter:true,honorLineLength:true,extraSpace:false,code);

      /// <summary>
      /// Like <see cref="Emit(int, object[])"/> with a new line added at the begining.
      /// </summary>
      /// <param name="indentLevel"></param>
      /// <param name="code"></param>
      /// <returns>True if a new line was written.</returns>
      public bool NlEmit(params object[] code) => WriteWithIndent(nlbefore:true,nlafter:false,honorLineLength:true,extraSpace:false,code);
      /// <summary>
      /// Like <see cref="Emit(int, object[])"/> with a new line added at the begining and end.
      /// </summary>
      /// <param name="indentLevel"></param>
      /// <param name="code"></param>
      /// <returns>True if a new line was written.</returns>
      public bool NlEmitnl(params object[] code) => WriteWithIndent(nlbefore:true,nlafter:true,honorLineLength:true,extraSpace:false,code);

      /// <summary>
      /// Emit a string to the target without a new line.
      /// </summary>
      /// <param name="s"></param>
      internal void EmitIgnoreLineLength(string s,bool NL=false) => WriteWithIndent(nlbefore:false,nlafter:NL,honorLineLength:false,extraSpace:false,s);

      internal void EmitWithExtraSpace(bool extraSpace,object[] items) => WriteWithIndent(nlbefore:false,nlafter:false,honorLineLength:true,extraSpace:extraSpace,items);

      /// <summary>
      /// This is the method that actually writes the code to the target using the <see cref="Write(string)"/> method.
      /// </summary>
      /// <param name="nlbefore"></param>
      /// <param name="nlafter"></param> 
      /// <param name="honorLineLength"></param> 
      /// <param name="items"></param>
      /// <returns>True if a new line was written.</returns>
      protected bool WriteWithIndent(bool nlbefore,bool nlafter,bool honorLineLength = true,bool extraSpace = false,params object[] items) {
         if (KeepTogether) {
            KeepTogetherBuffer += (extraSpace?" ":"")+string.Join("",items.Select(i => i?.ToString() ?? ""));
            return false;
         } else { 
            bool wasNewline = WriteNewLine(nlbefore && CurrentLine.Trim().Length > 0);
            // Split the items into lines.
            string[] lines = Regex.Split(string.Join("",items.Select(i => i?.ToString() ?? "")),@"\r\n|\r|\n");
            // Write the pervious line if it would be too long with the first new item AND if line length is being honoured.
            if (!IgnoreLineLength && honorLineLength && WillNotFitOnCurrentLine(lines[0])) wasNewline = wasNewline || WriteNewLine(true);
            AddToCurrentLine(lines[0],extraSpace);
            foreach (string line in lines.Skip(1)) {
               wasNewline = WriteNewLine(true) || wasNewline; // Write the previous line
               AddToCurrentLine(line);
            }         
            return wasNewline || WriteNewLine(nlafter);
         }
      }

      public bool WillNotFitOnCurrentLine(string s) => CurrentLine.Length + s.Length > LineLength;
      public bool WillKeepTogetherNotFitOnCurrentLine() => WillNotFitOnCurrentLine(KeepTogetherBuffer);

      // Initialize CurrentLine with the indent if empty.
      void AddToCurrentLine(string str,bool extraSpace=false) {
         if (CurrentLine.Length == 0) CurrentLine = new string(' ',(IndentLevel + extraIndent) * IndentWidth + (extraSpace?1:0));
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
