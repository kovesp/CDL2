// Ignore Spelling: CDL Emitnl Nl nlafter nlbefore

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CDL2v1 {
   public abstract class EmitterBase {
      protected EmitterBase() {
         if (GetType() != typeof(EmitterDebug)) WriteDebug = (s) => {
           
            EmitterDebug.WriteDebug(s);
         };
      }
      private readonly Action<string> WriteDebug = (s) => { };

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
      /// Note that when it decreases, ExtraIndent is reset to 0.
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
      public bool AggregateOutput {
         get => IsAggregatingOutput;
         set {
            IsAggregatingOutput = value;
            if (IsAggregatingOutput) {
               AggregateBuffer = "";
            } else {
               WriteWithIndent(nlbefore: false,nlafter: false,honorLineLength: true,extraSpace: false,AggregateBuffer);
               AggregateBuffer = "";
            }
         }
      }
      /// <summary>
      /// If true, the LineLength is ignored.
      /// </summary>
      public bool IgnoreLineLength { get; set; } = false;

      private string AggregateBuffer = "";
      private bool IsAggregatingOutput = false;

      /// <summary>
      /// This is the prefix that is added to each line of output.
      /// It is up to the concrete subclass to use or ignore this
      /// </summary>
      public string LinePrefix { get; set; } = "";

      public bool SupportsDecoration { get; set; } = false;
      public Regex spanRegex = new(@"<span\s+(fg='(?<fg>[^']*)')?\s+(bg='(?<bg>[^']*)')?\s+(style='(?<style>[^']*)')?\s*>(?<text>.*?)<\/span>",
                                 RegexOptions.IgnoreCase | RegexOptions.Compiled);
      protected string RemoveSpans(string text) => spanRegex.Replace(text, "${text}");

      public void Indented(Action action) {
         IndentLevel++;
         action();
         IndentLevel--;
      }
      /// <summary>
      /// Perform action keeping produced output together on one line.
      /// </summary>
      /// <param id="action"></param>
      public void KeepTogether(Action action) {
         bool keepTogether = AggregateOutput;
         AggregateOutput = true;
         action();
         AggregateOutput = keepTogether;
      }

      /// <summary>
      /// Override this to close the target.
      /// </summary>
      public virtual void Close() { }

      /// <summary>
      /// Write the item to the target. Must be supplied by concrete subclasses.
      /// </summary>
      /// <param id="item"></param>
      protected abstract void WriteLine(string item);

      /// <summary>
      /// The current line being built up.
      /// </summary>
      private string CurrentLine = "";

      /// <summary>
      /// Emit code to the target.
      /// ToString is used on the objects.
      /// </summary>
      /// <param id="code"></param>
      public bool Emit(params object[] code) => WriteWithIndent(nlbefore: false,nlafter: false,honorLineLength: true,extraSpace: false,code);
      /// <summary>
      /// Like <see cref="Emitnl(object[])"/> with a new line added.
      /// </summary>
      /// <param id="code"></param>
      /// <returns>True if a new line was written.</returns>
      public bool Emitnl(params object[] code) => WriteWithIndent(nlbefore: false,nlafter: true,honorLineLength: true,extraSpace: false,code);

      /// <summary>
      /// Like <see cref="Emit(int, object[])"/> with a new line added at the beginning.
      /// </summary>
      /// <param id="indentLevel"></param>
      /// <param id="code"></param>
      /// <returns>True if a new line was written.</returns>
      public bool NlEmit(params object[] code) => WriteWithIndent(nlbefore: true,nlafter: false,honorLineLength: true,extraSpace: false,code);
      /// <summary>
      /// Like <see cref="Emit(int, object[])"/> with a new line added at the beginning and end.
      /// </summary>
      /// <param id="indentLevel"></param>
      /// <param id="code"></param>
      /// <returns>True if a new line was written.</returns>
      public bool NlEmitnl(params object[] code) => WriteWithIndent(nlbefore: true,nlafter: true,honorLineLength: true,extraSpace: false,code);

      /// <summary>
      /// Emit a string to the target without a new line.
      /// </summary>
      /// <param id="s"></param>
      internal void EmitIgnoreLineLength(string s,bool NL = false) => WriteWithIndent(nlbefore: false,nlafter: NL,honorLineLength: false,extraSpace: false,s);

      internal void EmitWithExtraSpace(bool extraSpace,object[] items) => WriteWithIndent(nlbefore: false,nlafter: false,honorLineLength: true,extraSpace: extraSpace,items);

      /// <summary>
      /// This is the method that actually writes the code to the target using the <see cref="Write(string)"/> method.
      /// </summary>
      /// <param id="nlbefore"></param>
      /// <param id="nlafter"></param> 
      /// <param id="honorLineLength"></param> 
      /// <param id="items"></param>
      /// <returns>True if a new line was written.</returns>
      protected bool WriteWithIndent(bool nlbefore,bool nlafter,bool honorLineLength = true,bool extraSpace = false,params object[] items) {
         if (AggregateOutput) {
            AggregateBuffer += (extraSpace ? " " : "") + string.Join("",items.Select(i => i?.ToString() ?? ""));
            return false;
         } else {
            bool wasNewline = WriteNewLine(nlbefore && CurrentLine.Trim().Length > 0);
            // Split the items into lines.
            string[] lines = Regex.Split(string.Join("",items.Select(i => i?.ToString() ?? "")),@"\r\n|\r|\n",RegexOptions.Compiled);
            // Write the previous line if it would be too long with the first new item AND if line length is being honoured.
            if (!IgnoreLineLength && honorLineLength && WillNotFitOnCurrentLine(lines[0])) wasNewline = wasNewline || WriteNewLine(true);
            AddToCurrentLine(lines[0],extraSpace);
            foreach (string line in lines.Skip(1)) {
               wasNewline = WriteNewLine(true) || wasNewline; // Write the previous line
               AddToCurrentLine(line);
            }
            return wasNewline || WriteNewLine(nlafter);
         }
      }

      public bool WillNotFitOnCurrentLine(string s) => GetLengthWithoutDecorations(CurrentLine) + GetLengthWithoutDecorations(s)> LineLength;
      private int GetLengthWithoutDecorations(string str) => spanRegex.Replace(str,match => match.Groups["text"].Value).Length;
      public bool WillKeepTogetherNotFitOnCurrentLine() => WillNotFitOnCurrentLine(AggregateBuffer);

      // Initialize CurrentLine with the indent if empty.
      private void AddToCurrentLine(string str,bool extraSpace = false) {
         if (CurrentLine.Length == 0) CurrentLine = new string(' ',(IndentLevel + extraIndent) * IndentWidth + (extraSpace ? 1 : 0));
         CurrentLine += str;
      }

      // Write the current line to the target. Add a newline if requested and return the request.
      private bool WriteNewLine(bool nl) {
         if (nl) {
            WriteDebug(CurrentLine);            
            WriteLine(CurrentLine);
            CurrentLine = "";
         }
         return nl;
      }

      public virtual void BeginUpdate() { }
      public virtual void EndUpdate() { }
      public virtual void UpdateUI() { }
   }
}
