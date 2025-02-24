using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

using static CDL2v1.Logger;

namespace CDL2v1 {
   /// <summary>
   /// Formatted printing of the parse tree.
   /// </summary>
   internal class PrettyPrinter {
      private const int DEFAULT_LINE_LENGTH          = 100;
      private const int DEFAULT_INDENT_MULTIPLIER    = 3;
      private const int DEFAULT_MAX_INDENT_INCREMENT = 3;

      private int LineLength { get; set; }              = DEFAULT_LINE_LENGTH;              // Line length for wrapping        
      private int IndentMultiplier { get; set; }        = DEFAULT_INDENT_MULTIPLIER;        // The indent multiplier
      private int MaxIndentIncrement { get; set; }      = DEFAULT_MAX_INDENT_INCREMENT;     // The maximum number of times the indent can be incremented for wrapping.

      private readonly CodeEmitterBase emitter;


      /// <summary>
      /// Perform action with an increased indent level.
      /// </summary>
      /// <param name="action"></param>
      void Indented(Action action) {
         emitter.IndentLevel++;
         action();
         emitter.IndentLevel--;
      }
      /// <summary>
      /// Perform action keeping produced output together on one line.
      /// </summary>
      /// <param name="action"></param>
      void KeepTogether(Action action) {
         bool keepTogether = emitter.KeepTogether;
         emitter.KeepTogether = true;
         action();
         emitter.KeepTogether = keepTogether;
      }


      /// <summary>
      /// Construct a pretty printer with a maximum line length and an indentation width using the specified emitter.
      /// </summary>
      /// <param name="width"></param>
      /// <param name="indent"></param>
      /// <param name="maxIndentIncr"></param>
      /// <param name="emitter"></param>
      /// <example>
      ///   Construct a pretty printer that outputs to a file.
      ///   
      ///   PrettyPrinter pp = new PrettyPrinter(100,3,new FileCodeEmitter("output.txt"));
      ///   or simpler
      ///    PrettyPrinter pp = new("output.txt");
      /// </example>
      public PrettyPrinter(int width,int indent,int maxIndentIncr,CodeEmitterBase emitter) {
         this.LineLength = width;
         this.IndentMultiplier = indent;
         this.MaxIndentIncrement = maxIndentIncr;
         this.emitter = emitter;
         emitter.IndentWidth = this.IndentMultiplier;
         emitter.LineWidth = this.LineLength;
         emitter.IndentLevel = 0;
         emitter.LinePrefix = "CDL2PP: ";
      }

      /// <summary>
      /// Construct a pretty printer with a default maximum line length of <see cref="DEFAULT_LINE_LENGTH"/> and an indentation width of <see cref="DEFAULT_INDENT_MULTIPLIER"/> using the specified emitter.
      /// </summary>
      /// <param name="emitter"></param>
      public PrettyPrinter(CodeEmitterBase emitter) : this (DEFAULT_LINE_LENGTH,DEFAULT_INDENT_MULTIPLIER,DEFAULT_MAX_INDENT_INCREMENT, emitter) { }
      /// <summary>
      /// Construct a pretty printer with a default maximum line length of <see cref="DEFAULT_LINE_LENGTH"/> and an indentation width of <see cref="DEFAULT_INDENT_MULTIPLIER"/> using the specified file name.
      /// </summary>
      /// <param name="fileName">If this is null, use the <see cref="CodeEmitterDebug"/> instead.</param>
      public PrettyPrinter(string? fileName) : this(DEFAULT_LINE_LENGTH,DEFAULT_INDENT_MULTIPLIER,DEFAULT_MAX_INDENT_INCREMENT, fileName.IsValidFileName() ? new CodeEmitterFile(fileName ?? "") : new CodeEmitterDebug()) { }

      private record struct UnitDelim(RW Start, RW End);
      private static readonly Dictionary<Type,UnitDelim> units = new() {
         { typeof(Program),new (RW.PROGRAM, RW.ENDPROG)},
         { typeof(Module),new (RW.MODULE, RW.ENDMOD)},
         { typeof(Layer),new (RW.LAYER, RW.ENDLAY)},
         { typeof(Section),new (RW.SECTION, RW.ENDSEC)},
      };

      public void Print(Program program,Set<Module> modules) {
         Print(program);
         foreach (Module module in modules) Print(module);
      }

      public void Print(Program program) => PrintContainer(program,() => {
         PrintList(RW.PART,program.children.Select(part => part.name));
         PrintLudes(program);
      });

      public void Print(Module module) => PrintContainer(module,() => { foreach (Layer layer in module.children) Print(layer); }); 

      public void Print(Layer layer)   => PrintContainer(layer,()  => { foreach (Section section in layer.children) Print(section); });

      public void Print(Section section) => PrintContainer(section,() => {
         PrintList(RW.EXPORT,section.export);
         PrintList(RW.IMPORT,section.import);
         PrintList(RW.ABSTR,section.abstr);
         PrintList(RW.EXT,section.ext);
         PrintList(RW.INV,section.inv);

         int EmitCount(IEnumerable<ID> list,string type) { 
            int count = list.Count();
            if (count > 0) { Emitnl(); NlEmitnl($"# {count} {type} definition{(count == 1 ? "" : "s")} #"); }
            return count;
         }

         if (EmitCount(section.consts,"CONST") > 0) {
            Emit(RW.CONST," ");
            Print((Const)section.Symbols[section.consts.First()]);
            foreach (ID constId in section.consts.Skip(1)) {
               EmitSeparator(TT.LISTSEP);
               Print((Const)section.Symbols[constId]);
            }
            EmitSeparatorWithNL(TT.END);
         }

         if (EmitCount(section.vars,"VAR  ") > 0) {
            PrintList(RW.VAR,section.vars);
         }

         if (EmitCount(section.lists,"LIST ") > 0) {
            Emit(RW.LIST," ");
            Print((LIST)section.Symbols[section.lists.First()]);
            foreach (ID listId in section.lists.Skip(1)) {
               EmitSeparator(TT.LISTSEP);
               Print((LIST)section.Symbols[listId]);
            }
            EmitSeparatorWithNL(TT.END);
         }

         IEnumerable<ID> macros = section.routines.Where(r => section.Symbols[r] is Macro);
         if (EmitCount(macros,"MACRO") > 0) foreach (ID macroId in macros) Print((Macro)section.Symbols[macroId]);

         IEnumerable<ID> codes = section.routines.Where(r => section.Symbols[r] is Code);
         if (EmitCount(codes,"CODE ") > 0) foreach (ID codeId in codes) Print((Code)section.Symbols[codeId]);

      });     

      private void PrintLudes(Container container) {
         PrintLude(RW.PRELUDE,container);
         PrintLude(RW.ROOT,container);
         PrintLude(RW.POSTLUDE,container);
      }

      private void PrintLude(RW ludeType,Container container) {
         if (container is Section) {
            if (container.ludes[ludeType].Count != 0) {
               Emit(ludeType," ");
               // Section ludes are stored as ids of a generated Code item.
               if (container.Symbols[container.ludes[ludeType].First()] is Code code) { // This should always be the case
                  Print(code.alternatives.First());
                  EmitSeparatorWithNL(TT.END);
               } else {
                  ReportError($"Internal error: {ludeType} lude is not a Code item.");
               }
            }
         } else { 
            PrintList(ludeType,container.ludes[ludeType]);
         }
      }

      private void Print(Alternative alternative,bool extraSpace=false) {
         emitter.ExtraIndent = 0;
         if (alternative.calls.Count > 0) { 
            Print(alternative.calls.First(),extraSpace:extraSpace,firstInAlternative:true);
            foreach (Call call in alternative.calls.Skip(1)) {
               EmitSeparator(TT.CALLSEP);
               Print(call);
            }
            if (alternative.lastCall.type != LCT.None) EmitSeparator(TT.CALLSEP);
         }

         if (alternative.lastCall.type != LCT.None) {            
            switch (alternative.lastCall.type) {
               case LastCallType.Standard:
                  Debug.Assert(alternative.lastCall.call != null,"alternative.lastCall.call is null");
                  Print(alternative.lastCall.call,firstInAlternative:alternative.calls.Count==0);
                  break;
               case LastCallType.Succeed:
                  Emit(TT.SUCCEED);
                  break;
               case LastCallType.Fail:
                  Emit(TT.FAIL);
                  break;
               case LastCallType.Abort:
                  Emit(TT.ABORT);
                  break;
               case LastCallType.Repeat:
                  Emit(TT.REPEAT);
                  Debug.Assert(alternative.lastCall.label is not null,"alternative.lastcall.label is null");
                  if (alternative.lastCall.label != TokenList.AnonID) {
                     Emit(alternative.lastCall.label.token.tokenString);
                  }
                  break;
               case LastCallType.Group:
                  Debug.Assert(alternative.lastCall.group is not null,"alternative.group is null");
                  Print(alternative.lastCall.group);
                  break;
            }
         }
      }

      private void Print(Group group) => Indented(() => {
         NlEmit(TT.GRPOPEN);
         if (group.name != TokenList.AnonID) Emit(group.name.token.tokenString,TT.LABELSEP);
         Print(group.alternatives);
         Emit(TT.GRPCLOSE);
      });

      private void Print(List<Alternative> alternatives) {
         Debug.Assert(alternatives.Any(),"alternatives list is empty");
         Print(alternatives.First());
         foreach (Alternative alternative in alternatives.Skip(1)) {
            EmitSeparatorWithNL(TT.ALTSEP);
            Print(alternative,extraSpace:true);
         }
      }

      public void Print(Call call,bool extraSpace = false,bool firstInAlternative=false) => KeepTogether(() => {
         EmitWithExtraSpace(extraSpace,call.id.token.tokenString);
         foreach (ActualArg arg in call.args) {
            Emit(TT.PARAMSEP);
            if (arg is STRING s) {
               Emit("\"",EscapedCDL2(s.value),"\"");
            } else if (arg is ID id) {
               Emit(id.token.tokenString);
            }
         }
         // This is safe, because the MaxIndentIncrement limits the extra indent.
         if (!firstInAlternative && emitter.WillKeepTogetherNotFitOnCurrentLine()) emitter.ExtraIndent++;
      });

      private static string EscapedCDL2(string str) {
         StringBuilder sb = new();
         foreach (char c in str) {
            if (Token.Char2Escape.TryGetValue(c.ToString(),out string? escape)) {
               sb.Append($"${escape}");
            } else {
               sb.Append(c);
            }
         }
         return sb.ToString();
      }

      private void PrintList(RW rw,IEnumerable<ID> ids) {
         if (ids.Any()) {
            Emit(rw," ",ids.First().token.tokenString);
            foreach (ID id in ids.Skip(1)) {
               EmitSeparator(TT.LISTSEP);
               Emit(id.token.tokenString);
            }
            EmitSeparatorWithNL(TT.END);
         }
      }

      public void Print(Code code) {
         PrintProcHead(code);
         Indented(() => {
            Debug.Assert(code.alternatives.Count != 0,"alternatives list is empty");
            Print(code.alternatives.First());
            foreach (Alternative alt in code.alternatives.Skip(1)) {
               EmitSeparatorWithNL(TT.ALTSEP);
               Print(alt);
            }
            EmitSeparatorWithNL(TT.END);
         });
      }

      public void Print(Macro macro) {
         PrintProcHead(macro);
         Indented(() => {
            Debug.Assert(macro.elements.Count != 0,"macro elements list is empty");
            PrintMacroElement(macro.elements.First(),withNl: false);
            foreach (MacroElement elem in macro.elements.Skip(1)) {
               PrintMacroElement(elem,withSpace: true);
            }
            EmitSeparatorWithNL(TT.END);
         });
      }

      private void PrintMacroElement(MacroElement elem,bool withSpace = false,bool withNl = true) {
         if (withSpace) Emit(" ");
         switch (elem) {
            case STRING s:
               Emit((withNl && s.value.Contains('\n')?"\n":""),"\"",EscapedCDL2(s.value),"\"");
               break;
            case INT n:
               Emit(n.value);
               break;
            case FLOAT f:
               Emit(f.value);
               break;
            case ID id:
               Emit(id.token.tokenString);
               break;
            default:
               throw new NotImplementedException();
         }
      }

      private void PrintProcHead(Algorithm code) {
         Emit(code.algType," ",code.name);
         foreach (Param param in code.formals.Cast<Param>()) {
            Emit(param.paramType == PT.std ? TT.PARAMSEP : TT.STRINGPARAMSEP);
            if (param.IsInput) Emit(TT.PARAMDIR);
            Emit(param.token.tokenString);
            if (param.IsOutput) Emit(TT.PARAMDIR);
         }
         if (code.locals.Any()) {
            foreach (ID local in code.locals) {
               Emit(" ",TT.LOCALSEP,local.token.tokenString);
            }
         }
         Emitnl(code.bodyType);
      }

      public void Print(Const constant) {
         Emit(constant.name,TT.EQUALS);
         foreach (ConstElement element in constant.elements) {
            switch (element) {
               case STRING s:
                  Emit(s.value);
                  break;
               case INT n:
                  Emit(n.value);
                  break;
               case FLOAT f:
                  Emit(f.value);
                  break;
               case Const c:
                  Print(c);
                  break;
               case ID id:
                  Emit(id.token.tokenString);
                  break;
               default:
                  throw new NotImplementedException();
            }
         }
      }

      public void Print(LIST list) {
         Emit(list.name.token.tokenString,TT.LISTBOUNDSTART,list.lwb.tokenString,TT.LISTBOUNDSEP,list.upb.tokenString,TT.LISTBOUNDEND);
      }

      //private void PrintUnitStart(NamedElement unit) {
      //   Emitnl(units[unit.GetType()].Start.ToString()," ",unit.name.token.tokenString,TT.END);
      //   IndentLevel++;
      //}

      //private void PrintUnitEnd(NamedElement unit) {
      //   IndentLevel--;
      //   Emitnl(units[unit.GetType()].End.ToString()," ",unit.name.token.tokenString,TT.END);
      //}

      /// <summary>
      /// Print the start and end of a container unit, and then the contents.
      /// Print the ludes for the containier if it can have any at the corect place.
      /// (Why they couldn't position the ludes in the same place for a PROGRAM as the other items is a mystery).
      /// </summary>
      /// <param name="unit"></param>
      /// <param name="action"></param>
      private void PrintContainer(Container unit,Action action) {
         Emitnl(units[unit.GetType()].Start.ToString()," ",unit.name.token.tokenString,TT.END);
         Indented(() => action());
         if (unit is Program) PrintLudes(unit);
         Emitnl(units[unit.GetType()].End.ToString()," ",unit.name.token.tokenString,TT.END);
         if (unit is Module || unit is Section) PrintLudes(unit);
      }

      /// <summary>
      /// Translate all objects to strings using their to ToString, unless it is a TokenType, then use the glyph.
      /// </summary>
      /// <param name="items"></param>
      /// <returns></returns>
      private static string[] TranslateTokens(params object[] items) => items.Select(item => TranslateToken(item)).ToArray();
      private static string TranslateToken(object item) => item is TT tt ? Token.ToGlyph(tt) : item.ToString() ?? "";

      /// <summary>
      /// Emit the specified items at the current indent level.
      /// The methods with nl will add a new line at the begining or end.
      /// </summary>
      /// <param name="items"></param>
      private void Emit(params object[] items) => emitter.Emit(TranslateTokens(items));
      private void EmitWithExtraSpace(bool extraSpace,params object[] items) => emitter.EmitWithExtraSpace(extraSpace,TranslateTokens(items));
      private void EmitSeparator(TT sep,bool space=true) => emitter.EmitIgnoreLineLength(TranslateToken(sep)+(space?" ":""));
      private void EmitSeparatorWithNL(TT sep) => emitter.EmitIgnoreLineLength(TranslateToken(sep),NL:true);
      private void Emitnl(params object[] items) => emitter.Emitnl(TranslateTokens(items));
      private void NlEmit(params object[] items) => emitter.NlEmit(TranslateTokens(items));
      private void NlEmitnl(params object[] items) => emitter.NlEmitnl(TranslateTokens(items));
   }
}
