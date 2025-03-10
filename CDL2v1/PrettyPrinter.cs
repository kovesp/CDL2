// Ignore Spelling: CDL

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Xps;

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

      private readonly EmitterBase emitter;


      /// <summary>
      /// Perform action with an increased indent level.
      /// </summary>
      /// <param id="action"></param>
      void Indented(Action action) {
         emitter.IndentLevel++;
         action();
         emitter.IndentLevel--;
      }
      /// <summary>
      /// Perform action keeping produced output together on one line.
      /// </summary>
      /// <param id="action"></param>
      void KeepTogether(Action action) {
         bool keepTogether = emitter.KeepTogether;
         emitter.KeepTogether = true;
         action();
         emitter.KeepTogether = keepTogether;
      }

      public static readonly FontWeight Bold = FontWeights.DemiBold;
      public static readonly FontStyle Italic = FontStyles.Oblique;

      public record Decoration (string FG = "White", string BG = "#1E1E1E", DS Style = DS.Normal);

      public static Dictionary<SE,Decoration> Decorators = new() {
         { SE.Id                 ,new Decoration() },
         { SE.Unit               ,new Decoration(FG:"#569cd6",Style:DS.Bold) },
         { SE.ReservedWord       ,new Decoration(FG:"#569cd6",Style:DS.Bold) },
         { SE.InputAffix         ,new Decoration(FG:"#9cdcfe") },
         { SE.OutputAffix        ,new Decoration(FG:"#51c0fd") },
         { SE.TransputAffix      ,new Decoration(FG:"#26b1fd") },
         { SE.StringAffix        ,new Decoration(FG:"#d69d85") },
         { SE.Local              ,new Decoration(FG:"DarkOrange") },
         { SE.Label              ,new Decoration(FG:"LightGray") },
         { SE.Const              ,new Decoration(FG:"Olive") },
         { SE.Var                ,new Decoration(FG:"OliveDrab") },
         { SE.List               ,new Decoration(FG:"DarkOliveGreen") },
         { SE.Number             ,new Decoration(FG:"#b5cea8") },
         { SE.String             ,new Decoration(FG:"#d69d85") },
         { SE.Comment            ,new Decoration(FG:"#57a64a") },
         { SE.Other              ,new Decoration() },                              // Will be used to obtain the overall background
         { SE.AlgorithmName      ,new Decoration() },                              // Not used, but required entry
       };

      public static Dictionary<AlgorithmNameType,Decoration> AlgorithmNameDecorators = new() {
         { AlgorithmNameType.None,   new Decoration(FG:"#dcdcaa") },
         { AlgorithmNameType.CanFail,new Decoration(FG:"#dcdcaa",Style:DS.Italic) },
         { AlgorithmNameType.Macro,  new Decoration(FG:"#dcdcaa",Style:DS.Underline) },
       };


      /// <summary>
      /// Returns a set of all used colors in the above tables.
      /// </summary>
      /// <returns></returns>
      public static Set<string> UsedColors() {
         Set<string> colors = [];
         foreach (Decoration decoration in Decorators.Values.Concat(AlgorithmNameDecorators.Values)) {
            colors.Add(decoration.FG);
            colors.Add(decoration.BG);
         }
         return colors;
      }

      /// <summary>
      /// Construct a pretty printer with a maximum line length and an indentation width using the specified emitter.
      /// </summary>
      /// <param id="width"></param>
      /// <param id="indent"></param>
      /// <param id="maxIndentIncrement"></param>
      /// <param id="emitter"></param>
      /// <example>
      ///   Construct a pretty printer that outputs to a file.
      ///   
      ///   PrettyPrinter pp = new PrettyPrinter(100,3,new FileCodeEmitter("output.txt"));
      ///   or simpler
      ///    PrettyPrinter pp = new("output.txt");
      /// </example>
      public PrettyPrinter(int width,int indent,int maxIndentIncrement,EmitterBase emitter) {
         this.LineLength = width;
         this.IndentMultiplier = indent;
         this.MaxIndentIncrement = maxIndentIncrement;
         this.emitter = emitter;
         emitter.IndentWidth = this.IndentMultiplier;
         emitter.LineLength = this.LineLength;
         emitter.IndentLevel = 0;
         emitter.LinePrefix = "CDL2PP: ";

         // Ensure that all elements have a decoration entry
         foreach (SE se in Enum.GetValues(typeof(SE))) {
            Debug.Assert(Decorators.ContainsKey(se),$"Missing decorator for {se}");
         }
      }

      /// <summary>
      /// Construct a pretty printer with a default maximum line length of <see cref="DEFAULT_LINE_LENGTH"/> and an indentation width of <see cref="DEFAULT_INDENT_MULTIPLIER"/> using the specified emitter.
      /// </summary>
      /// <param id="emitter"></param>
      public PrettyPrinter(EmitterBase emitter) : this (DEFAULT_LINE_LENGTH,DEFAULT_INDENT_MULTIPLIER,DEFAULT_MAX_INDENT_INCREMENT, emitter) { }
      /// <summary>
      /// Construct a pretty printer with a default maximum line length of <see cref="DEFAULT_LINE_LENGTH"/> and an indentation width of <see cref="DEFAULT_INDENT_MULTIPLIER"/> using the specified file id.
      /// </summary>
      /// <param id="fileName">If this is null, use the <see cref="EmitterDebug"/> instead.</param>
      public PrettyPrinter(string? fileName) : this(DEFAULT_LINE_LENGTH,DEFAULT_INDENT_MULTIPLIER,DEFAULT_MAX_INDENT_INCREMENT, fileName.IsValidFileName() ? new EmitterFile(fileName ?? "") : new EmitterWindow()) { }

      private record struct UnitDelimiter(RW Start, RW End);
      private static readonly Dictionary<Type,UnitDelimiter> units = new() {
         { typeof(Program),new (RW.PROGRAM, RW.ENDPROG)},
         { typeof(Module),new (RW.MODULE, RW.ENDMOD)},
         { typeof(Layer),new (RW.LAYER, RW.ENDLAY)},
         { typeof(Section),new (RW.SECTION, RW.ENDSEC)},
      };

      public void Print(Dictionary<ID,Program> programs,Dictionary<ID,Module> modules) {
         emitter.BeginUpdate();
         foreach (Program program in programs.Values) Print(program);
         foreach (Module module in modules.Values) Print(module);
         emitter.EndUpdate();
      }

      public void Print(Program program) =>PrintContainer(program,() => {
         PrintList(RW.PART,program.Parts);
         PrintLudes(program);
      },Newline: true,updateUI: true);

      public void Print(Module module) => PrintContainer(module,() => { foreach (Layer layer in module.Children.Cast<Layer>()) Print(layer); },Newline: true,updateUI: true); 

      public void Print(Layer layer)   => PrintContainer(layer,() => { foreach (Section section in layer.Children.Cast<Section>()) Print(section); },updateUI: false);

      public void Print(Section section) => PrintContainer(section,() => {
         PrintList(RW.EXPORT,section.export);
         PrintList(RW.IMPORT,section.import);
         PrintList(RW.ABSTR,section.abstr);
         PrintList(RW.EXT,section.ext);
         PrintList(RW.INV,section.inv);

         int EmitCount(IEnumerable<ICDL2Object> list,string type) {
            int count = list.Count();
            if (count > 0) { Emitnl(); NlEmitnl($"# {count} {type} definition{(count == 1 ? "" : "s")} #".Decorate(emitter,SE.Comment)); }
            return count;
         }

         if (EmitCount(section.Constants,"CONST") > 0) {
            Emit(RW.CONST.Decorate(emitter,SE.ReservedWord)," ");
            Print(section.Constants.First());
            foreach (Const constant in section.Constants.Skip(1)) {
               EmitSeparator(TT.LISTSEP);
               Print(constant);
            }
            EmitSeparatorWithNL(TT.END);
         }

         if (EmitCount(section.Variables,"VAR  ") > 0) {
            PrintList(RW.VAR,section.Variables.Select(variable => variable.id));
         }

         if (EmitCount(section.Lists,"LIST ") > 0) {
            Emit(RW.LIST.Decorate(emitter,SE.ReservedWord)," ");
            Print(section.Lists.First());
            foreach (LIST list in section.Lists.Skip(1)) {
               EmitSeparator(TT.LISTSEP);
               Print(list);
            }
            EmitSeparatorWithNL(TT.END);
         }

         if (EmitCount(section.Macros,"MACRO") > 0) foreach (Macro macro in section.Macros) Print(macro);

         if (EmitCount(section.Procedures,"PROC ") > 0) foreach (Procedure proc in section.Procedures) Print(proc);

      },updateUI: true);     

      private void PrintLudes(Container container) {
         PrintLude(RW.PRELUDE,container);
         PrintLude(RW.ROOT,container);
         PrintLude(RW.POSTLUDE,container);
      }

      private void PrintLude(RW ludeType,Container container) {
         if (container is Section section) {
            if (section.Ludes[ludeType].Count != 0) {
               Emit(ludeType.Decorate(emitter,SE.ReservedWord)," ");
               // Section Ludes are stored as ids of a generated Procedure item.
               if (section.local[section.Ludes[ludeType].First()] is Procedure proc) { // This should always be the case
                  Print(proc.alternatives.First());
                  EmitSeparatorWithNL(TT.END);
               } else {
                  ReportError($"Internal error: {ludeType} lude is not a Procedure item.");
               }
            }
         } else { 
            PrintList(ludeType,container.Ludes[ludeType]);
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
                  Debug.Assert(alternative.lastCall.label is not null,"alternative.lastCall.label is null");
                  if (alternative.lastCall.label != ID.AnonID) {
                     Emit(alternative.lastCall.label.token.TokenString);
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
         if (group.id != ID.AnonID) Emit(group.id.token.TokenString,TT.LABELSEP);
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
         AlgorithmNameType callDecorator = AlgorithmNameType.None;
         if (call.id.section != null) {
            if (call.id.section.local.TryGetValue(call.id,out ICDL2Object? obj) && obj is Algorithm algorithm) {
               if (algorithm is Macro) callDecorator |= AlgorithmNameType.Macro;
               if (algorithm.algorithmType == RW.TEST || algorithm.algorithmType == RW.PREDICATE) callDecorator |= AlgorithmNameType.CanFail;
            }

            //if (call.id.owner.Owner is Section section) {
            //   // This covers local usages
            //   if (section.abstr.Contains(call.id)) callDecorator |= AlgorithmNameType.Abstr;
            //   if (section.ext.Contains(call.id)) callDecorator |= AlgorithmNameType.Ext;
            //   if (section.import.Contains(call.id)) callDecorator |= AlgorithmNameType.Imported;

            //   if (section.inv.Contains(call.id)) {
            //      if (section.Parent is Layer currentLayer) {
            //         if (!TryFindInvocationType(call.id,ref callDecorator,AlgorithmNameType.Ext,currentLayer)) {
            //            if (currentLayer.Parent is Module module) {
            //               int layerIndex = module.Children.IndexOf(currentLayer);
            //               if (layerIndex > 1) {
            //                  Layer previousLayer = (Layer)module.Children[layerIndex - 1];
            //                  TryFindInvocationType(call.id,ref callDecorator,AlgorithmNameType.Abstr,previousLayer);
            //               }
            //            }
            //         }
            //      }
            //   }
            //}
         } else {
            ReportError($"Internal error: {call.id} has no owner.");
         }

         EmitWithExtraSpace(extraSpace,call.id.token.TokenString.Decorate(emitter,SE.AlgorithmName,callDecorator));
         foreach (IActualArg arg in call.args) {
            Emit(TT.PARAMSEP);
            if (arg is STRING s) {
               Emit("\"",EscapedCDL2(s.value),"\"");
            } else if (arg is ID id) {
               Emit(id.token.TokenString);
            }
         }
         // This is safe, because the MaxIndentIncrement limits the extra indent.
         if (!firstInAlternative && emitter.WillKeepTogetherNotFitOnCurrentLine()) emitter.ExtraIndent++;

         static bool TryFindInvocationType(ID id,ref AlgorithmNameType callDecorator,AlgorithmNameType callAttribute,Layer layer) {
            foreach (Section section in layer.Children.Cast<Section>()) {
               if (section.import.Contains(id)) {
                  callDecorator |= AlgorithmNameType.Imported;
                  return true;
               } else if ((callAttribute == AlgorithmNameType.Ext ? section.ext : section.abstr).Contains(id)) {
                  callDecorator |= callAttribute;
                  return true;
               }
            }
            return false;
         }
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
         string DecoratedID(ID id) {
            //if (id.section == null) {

            //} else {
            //   if (id.owner.TryGetValue(id,out NamedElement obj)) {
            //      return obj switch {
            //         Const c => c.id.Decorate(emitter,SE.Const),
            //         LIST l => l.id.Decorate(emitter,SE.List),
            //         Algorithm a => a.id.Decorate(emitter,SE.InputAffix),
            //         _ => id.token.TokenString,
            //      };
            //   }
              return id.token.TokenString;
            }
         if (ids.Any()) {
            Emit(rw.Decorate(emitter,SE.ReservedWord)," ",DecoratedID(ids.First()));
            foreach (ID id in ids.Skip(1)) {
               EmitSeparator(TT.LISTSEP);
               Emit(DecoratedID(id));
            }
            EmitSeparatorWithNL(TT.END);
         }
      }


      public void Print(Procedure code) {
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
            PrintMacroElement(macro,macro.elements.First(),withNl: false);
            foreach (IMacroElement elem in macro.elements.Skip(1)) {
               PrintMacroElement(macro,elem,withSpace: true);
            }
            EmitSeparatorWithNL(TT.END);
         });
      }

      private void PrintMacroElement(Macro macro,IMacroElement elem,bool withSpace = false,bool withNl = true) {
         if (withSpace) Emit(" ");
         switch (elem) {
            case STRING s:
               Emit((withNl && s.value.Contains('\n')?"\n":""),("\""+EscapedCDL2(s.value)+"\"").Decorate(emitter,SE.String));
               break;
            case INT n:
               Emit(n.value.Decorate(emitter));
               break;
            case FLOAT f:
               Emit(f.value.Decorate(emitter));
               break;
            case ID id:
               Emit(id.token.TokenString);
               break;
            case Affix affix:
               Emit(affix.id.Decorate(emitter,affix.SyntaxElement));
               break;
            case Local local:
               Emit(local.id.Decorate(emitter,SE.Local));
               break;
            default:
               throw new NotImplementedException();
         }
      }

      private void PrintProcHead(Algorithm code) {
         Emit(code.algorithmType.Decorate(emitter,SE.ReservedWord)," ",code.id);
         foreach (Affix affix in code.formals.Cast<Affix>()) {
            Emit(affix.affixType == AffixType.std ? TT.PARAMSEP : TT.STRINGPARAMSEP);
            if (affix.IsInput) Emit(TT.AFFIXDIR);
            Emit(affix.id.Decorate(emitter,affix.SyntaxElement));
            if (affix.IsOutput) Emit(TT.AFFIXDIR);
         }
         if (code.locals.Any()) {
            foreach (Local local in code.locals) {
               Emit(" ",TT.LOCALSEP,local.id.Decorate(emitter,SE.Local));
            }
         }
         Emitnl(code.bodyType);
      }

      public void Print(Const constant) {
         Emit(constant.id.Decorate(emitter,SE.Const),TT.EQUALS);
         foreach (IConstElement element in constant.elements) {
            switch (element) {
               case STRING s:
                  Emit(s.value.Decorate(emitter,SE.String));
                  break;
               case INT n:
                  Emit(n.value.Decorate(emitter));
                  break;
               case FLOAT f:
                  Emit(f.value.Decorate(emitter));
                  break;
               case Const c:
                  Emit(c.id.Decorate(emitter,SE.Const));
                  break;
               case ID id:
                  Emit(id.token.TokenString);
                  break;
               default:
                  throw new NotImplementedException();
            }
         }
      }

      public void Print(LIST list) {
         //TODO: Look at how to print list bounds.
         Emit(list.id.Decorate(emitter,SE.List),TT.LISTBOUNDSTART,list.lwb?.TokenString??"???",TT.LISTBOUNDSEP,list.upb?.TokenString??"???",TT.LISTBOUNDEND);
      }

      /// <summary>
      /// Print the start and end of a container unit, and then the contents.
      /// Print the Ludes for the container if it can have any at the correct place.
      /// (Why they couldn't position the Ludes in the same place for a PROGRAM as the other items is a mystery).
      /// </summary>
      /// <param Name="unit"></param>
      /// <param Name="action"></param>
      private void PrintContainer(Container unit,Action action,bool Newline = false,bool updateUI = false) {
         Emitnl(units[unit.GetType()].Start.Decorate(emitter,SE.Unit)," ",unit.id.token.TokenString,TT.END);
         Indented(() => action());
         Emitnl(units[unit.GetType()].End.Decorate(emitter,SE.Unit)," ",unit.id.token.TokenString,TT.END);
         if (unit is Module || unit is Section) PrintLudes(unit);
         if (Newline) Emitnl();
         if (updateUI) emitter.UpdateUI();
      }

      /// <summary>
      /// Translate all objects to strings using their to ToString, unless it is a TokenType, then use the glyph.
      /// </summary>
      /// <param id="items"></param>
      /// <returns></returns>
      private static string[] TranslateTokens(params object[] items) => items.Select(item => TranslateToken(item)).ToArray();
      private static string TranslateToken(object item) => item is TT tt ? Token.ToGlyph(tt) : item.ToString() ?? "";

      /// <summary>
      /// Emit the specified items at the current indent level.
      /// The methods with nl will add a new line at the beginning or end.
      /// </summary>
      /// <param id="items"></param>
      private void Emit(params object[] items) => emitter.Emit(TranslateTokens(items));
      private void EmitWithExtraSpace(bool extraSpace,params object[] items) => emitter.EmitWithExtraSpace(extraSpace,TranslateTokens(items));
      private void EmitSeparator(TT sep,bool space=true) => emitter.EmitIgnoreLineLength(TranslateToken(sep)+(space?" ":""));
      private void EmitSeparatorWithNL(TT sep) => emitter.EmitIgnoreLineLength(TranslateToken(sep),NL:true);
      private void Emitnl(params object[] items) => emitter.Emitnl(TranslateTokens(items));
      private void NlEmit(params object[] items) => emitter.NlEmit(TranslateTokens(items));
      private void NlEmitnl(params object[] items) => emitter.NlEmitnl(TranslateTokens(items));
   }
}
