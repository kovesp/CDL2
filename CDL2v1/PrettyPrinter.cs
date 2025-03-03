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
      /// <affix name="action"></affix>
      void Indented(Action action) {
         emitter.IndentLevel++;
         action();
         emitter.IndentLevel--;
      }
      /// <summary>
      /// Perform action keeping produced output together on one line.
      /// </summary>
      /// <affix name="action"></affix>
      void KeepTogether(Action action) {
         bool keepTogether = emitter.KeepTogether;
         emitter.KeepTogether = true;
         action();
         emitter.KeepTogether = keepTogether;
      }

      public record Decoration (string Color,DS Style);

      public static Dictionary<SE,Decoration> Decorators = new() {
         { SE.Unit          ,new Decoration("DarkBlue",DS.Bold) },
         { SE.ReservedWord  ,new Decoration("DarkBlue",DS.Bold) },
         { SE.InputAffix    ,new Decoration("Gold",DS.Normal) },
         { SE.OutputAffix   ,new Decoration("Red",DS.Normal) },
         { SE.TransputAffix ,new Decoration("DarkRed",DS.Normal) },
         { SE.Local         ,new Decoration("DarkOrange",DS.Normal) },
         { SE.Const         ,new Decoration("Olive",DS.Normal) },
         { SE.Var           ,new Decoration("OliveDrab",DS.Normal) },
         { SE.List          ,new Decoration("DarkOliveGreen",DS.Normal) },
       };
      public static Dictionary<AIT,Decoration> InvocationDecorators = new() {
         { AIT.None                                             ,new Decoration("DarkGreen"       ,DS.Normal) },
         { AIT.CanFail                                          ,new Decoration("DarkGreen"       ,DS.Italic) },
         { AIT.Macro                                            ,new Decoration("DarkGreen"       ,DS.Underline) },

         { AIT.Ext                                              ,new Decoration("MediumAquamarine",DS.Normal) },
         { AIT.Abstr                                            ,new Decoration("LightGreen"      ,DS.Normal) },
         { AIT.Abstr|AIT.Ext                                    ,new Decoration("LightGreen"      ,DS.Normal) },
         { AIT.Imported                                         ,new Decoration("Blue"            ,DS.Normal) },
         { AIT.Imported|AIT.Abstr                               ,new Decoration("Blue"            ,DS.Normal) },
         { AIT.Imported|AIT.Abstr|AIT.Ext                       ,new Decoration("Blue"            ,DS.Normal) },

         { AIT.CanFail|AIT.Ext                                  ,new Decoration("MediumAquamarine",DS.Italic) },
         { AIT.CanFail|AIT.Abstr                                ,new Decoration("LightGreen"      ,DS.Italic) },
         { AIT.CanFail|AIT.Abstr|AIT.Ext                        ,new Decoration("LightGreen"      ,DS.Italic) },
         { AIT.CanFail|AIT.Imported                             ,new Decoration("Blue"            ,DS.Italic) },
         { AIT.CanFail|AIT.Imported|AIT.Abstr                   ,new Decoration("LightGreen"      ,DS.Italic) },
         { AIT.CanFail|AIT.Imported|AIT.Abstr|AIT.Ext           ,new Decoration("LightGreen"      ,DS.Italic) },

         { AIT.Macro|AIT.Ext                                    ,new Decoration("MediumAquamarine",DS.Underline) },
         { AIT.Macro|AIT.Abstr                                  ,new Decoration("LightGreen"      ,DS.Underline) },
         { AIT.Macro|AIT.Abstr|AIT.Ext                          ,new Decoration("LightGreen"      ,DS.Underline) },
         { AIT.Macro|AIT.Imported                               ,new Decoration("Blue"            ,DS.Underline) },
         { AIT.Macro|AIT.Imported|AIT.Abstr                     ,new Decoration("LightGreen"      ,DS.Underline) },
         { AIT.Macro|AIT.Imported|AIT.Abstr|AIT.Ext             ,new Decoration("LightGreen"      ,DS.Underline) },

         { AIT.CanFail|AIT.Macro|AIT.Ext                        ,new Decoration("MediumAquamarine",DS.Italic|DS.Underline) },
         { AIT.CanFail|AIT.Macro|AIT.Abstr                      ,new Decoration("LightGreen"      ,DS.Italic|DS.Underline) },
         { AIT.CanFail|AIT.Macro|AIT.Abstr|AIT.Ext              ,new Decoration("LightGreen"      ,DS.Italic|DS.Underline) },
         { AIT.CanFail|AIT.Macro|AIT.Imported                   ,new Decoration("Blue"            ,DS.Italic|DS.Underline) },
         { AIT.CanFail|AIT.Macro|AIT.Imported|AIT.Abstr         ,new Decoration("Blue"            ,DS.Italic|DS.Underline) },
         { AIT.CanFail|AIT.Macro|AIT.Imported|AIT.Abstr|AIT.Ext ,new Decoration("Blue"            ,DS.Italic|DS.Underline) },
       };


      /// <summary>
      /// Construct a pretty printer with a maximum line length and an indentation width using the specified emitter.
      /// </summary>
      /// <affix name="width"></affix>
      /// <affix name="indent"></affix>
      /// <affix name="maxIndentIncrement"></affix>
      /// <affix name="emitter"></affix>
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
      }

      /// <summary>
      /// Construct a pretty printer with a default maximum line length of <see cref="DEFAULT_LINE_LENGTH"/> and an indentation width of <see cref="DEFAULT_INDENT_MULTIPLIER"/> using the specified emitter.
      /// </summary>
      /// <affix name="emitter"></affix>
      public PrettyPrinter(EmitterBase emitter) : this (DEFAULT_LINE_LENGTH,DEFAULT_INDENT_MULTIPLIER,DEFAULT_MAX_INDENT_INCREMENT, emitter) { }
      /// <summary>
      /// Construct a pretty printer with a default maximum line length of <see cref="DEFAULT_LINE_LENGTH"/> and an indentation width of <see cref="DEFAULT_INDENT_MULTIPLIER"/> using the specified file name.
      /// </summary>
      /// <affix name="fileName">If this is null, use the <see cref="EmitterDebug"/> instead.</affix>
      public PrettyPrinter(string? fileName) : this(DEFAULT_LINE_LENGTH,DEFAULT_INDENT_MULTIPLIER,DEFAULT_MAX_INDENT_INCREMENT, fileName.IsValidFileName() ? new EmitterFile(fileName ?? "") : new EmitterWindow()) { }

      private record struct UnitDelimiter(RW Start, RW End);
      private static readonly Dictionary<Type,UnitDelimiter> units = new() {
         { typeof(Program),new (RW.PROGRAM, RW.ENDPROG)},
         { typeof(Module),new (RW.MODULE, RW.ENDMOD)},
         { typeof(Layer),new (RW.LAYER, RW.ENDLAY)},
         { typeof(Section),new (RW.SECTION, RW.ENDSEC)},
      };

      public void Print(Program? program,Set<Module> modules) {
         if (program != null) Print(program);
         foreach (Module module in modules) Print(module);
      }

      public void Print(Program program) => PrintContainer(program,() => {
         PrintList(RW.PART,program.Children.Select(part => part.name));
         PrintLudes(program);
      });

      public void Print(Module module) => PrintContainer(module,() => { foreach (Layer layer in module.Children) Print(layer); }); 

      public void Print(Layer layer)   => PrintContainer(layer,()  => { foreach (Section section in layer.Children) Print(section); });

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

         if (EmitCount(section.constants,"CONST") > 0) {
            Emit(RW.CONST.Decorate(emitter,SE.ReservedWord)," ");
            Print((Const)section.Symbols[section.constants.First()]);
            foreach (ID constId in section.constants.Skip(1)) {
               EmitSeparator(TT.LISTSEP);
               Print((Const)section.Symbols[constId]);
            }
            EmitSeparatorWithNL(TT.END);
         }

         if (EmitCount(section.variables,"VAR  ") > 0) {
            PrintList(RW.VAR,section.variables);
         }

         if (EmitCount(section.lists,"LIST ") > 0) {
            Emit(RW.LIST.Decorate(emitter,SE.ReservedWord)," ");
            Print((LIST)section.Symbols[section.lists.First()]);
            foreach (ID listId in section.lists.Skip(1)) {
               EmitSeparator(TT.LISTSEP);
               Print((LIST)section.Symbols[listId]);
            }
            EmitSeparatorWithNL(TT.END);
         }

         IEnumerable<ID> macros = section.routines.Where(r => section.Symbols[r] is Macro);
         if (EmitCount(macros,"MACRO") > 0) foreach (ID macroId in macros) Print((Macro)section.Symbols[macroId]);

         IEnumerable<ID> codes = section.routines.Where(r => section.Symbols[r] is Procedure);
         if (EmitCount(codes,"CODE ") > 0) foreach (ID codeId in codes) Print((Procedure)section.Symbols[codeId]);

      });     

      private void PrintLudes(Container container) {
         PrintLude(RW.PRELUDE,container);
         PrintLude(RW.ROOT,container);
         PrintLude(RW.POSTLUDE,container);
      }

      private void PrintLude(RW ludeType,Container container) {
         if (container is Section) {
            if (container.Ludes[ludeType].Count != 0) {
               Emit(ludeType.Decorate(emitter,SE.ReservedWord)," ");
               // Section Ludes are stored as ids of a generated Procedure item.
               if (container.Symbols[container.Ludes[ludeType].First()] is Procedure code) { // This should always be the case
                  Print(code.alternatives.First());
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
         if (group.name != ID.AnonID) Emit(group.name.token.tokenString,TT.LABELSEP);
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
         AIT callDecorator = AIT.None;
         if (call.id.owner != null) {
            if (call.id.owner.TryGetValue(call.id,out NamedElement? ne) && ne is Algorithm algorithm) {
               if (algorithm is Macro) callDecorator |= AIT.Macro;
               if (algorithm.algType == RW.TEST || algorithm.algType == RW.PREDICATE) callDecorator |= AIT.CanFail;
            }
            if (call.id.owner.Owner is Section section) {
               // This covers local usages
               if (section.abstr.Contains(call.id)) callDecorator |= AIT.Abstr;
               if (section.ext.Contains(call.id)) callDecorator |= AIT.Ext;
               if (section.import.Contains(call.id)) callDecorator |= AIT.Imported;

               if (section.inv.Contains(call.id)) {
                  if (section.Parent is Layer currentLayer) {
                     if (!TryFindInvocationType(call.id,ref callDecorator,AIT.Ext,currentLayer)) {
                        if (currentLayer.Parent is Module module) {
                           int layerIndex = module.Children.IndexOf(currentLayer);
                           if (layerIndex > 1) {
                              Layer previousLayer = (Layer)module.Children[layerIndex - 1];
                              TryFindInvocationType(call.id,ref callDecorator,AIT.Abstr,previousLayer);
                           }
                        }
                     }
                  }
               }
            }
         } else {
            ReportError($"Internal error: {call.id} has no owner.");
         }

            EmitWithExtraSpace(extraSpace,call.id.token.tokenString.Decorate(emitter,SE.AlgorithmInvocation,callDecorator));
         foreach (IActualArg arg in call.args) {
            Emit(TT.PARAMSEP);
            if (arg is STRING s) {
               Emit("\"",EscapedCDL2(s.value),"\"");
            } else if (arg is ID id) {
               Emit(id.token.tokenString);
            }
         }
         // This is safe, because the MaxIndentIncrement limits the extra indent.
         if (!firstInAlternative && emitter.WillKeepTogetherNotFitOnCurrentLine()) emitter.ExtraIndent++;

         static bool TryFindInvocationType(ID id,ref AIT callDecorator,AIT callAttribute,Layer layer) {
            foreach (Section section in layer.Children.Cast<Section>()) {
               if (section.import.Contains(id)) {
                  callDecorator |= AIT.Imported;
                  return true;
               } else if ((callAttribute == AIT.Ext ? section.ext : section.abstr).Contains(id)) {
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
         if (ids.Any()) {
            Emit(rw.Decorate(emitter,SE.ReservedWord)," ",ids.First().token.tokenString);
            foreach (ID id in ids.Skip(1)) {
               EmitSeparator(TT.LISTSEP);
               Emit(id.token.tokenString);
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
            PrintMacroElement(macro.elements.First(),withNl: false);
            foreach (IMacroElement elem in macro.elements.Skip(1)) {
               PrintMacroElement(elem,withSpace: true);
            }
            EmitSeparatorWithNL(TT.END);
         });
      }

      private void PrintMacroElement(IMacroElement elem,bool withSpace = false,bool withNl = true) {
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
            case Affix affix:
               Emit(affix.name.token.tokenString);
               break;
            case Local local:
               Emit(local.name.token.tokenString);
               break;
            default:
               throw new NotImplementedException();
         }
      }

      private void PrintProcHead(Algorithm code) {
         Emit(code.algType," ",code.name);
         foreach (Affix affix in code.formals.Cast<Affix>()) {
            Emit(affix.affixType == AffixType.std ? TT.PARAMSEP : TT.STRINGPARAMSEP);
            if (affix.IsInput) Emit(TT.AFFIXDIR);
            Emit(affix.name.token.tokenString);
            if (affix.IsOutput) Emit(TT.AFFIXDIR);
         }
         if (code.locals.Any()) {
            foreach (Local local in code.locals) {
               Emit(" ",TT.LOCALSEP,local.name.token.tokenString);
            }
         }
         Emitnl(code.bodyType);
      }

      public void Print(Const constant) {
         Emit(constant.name,TT.EQUALS);
         foreach (IConstElement element in constant.elements) {
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
         Emit(list.name.token.tokenString,TT.LISTBOUNDSTART,list.lwb?.tokenString??"???",TT.LISTBOUNDSEP,list.upb?.tokenString??"???",TT.LISTBOUNDEND);
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
      /// Print the Ludes for the container if it can have any at the correct place.
      /// (Why they couldn't position the Ludes in the same place for a PROGRAM as the other items is a mystery).
      /// </summary>
      /// <affix name="unit"></affix>
      /// <affix name="action"></affix>
      private void PrintContainer(Container unit,Action action) {
         Emitnl(units[unit.GetType()].Start.Decorate(emitter,SE.Unit)," ",unit.name.token.tokenString,TT.END);
         Indented(() => action());
         if (unit is Program) PrintLudes(unit);
         Emitnl(units[unit.GetType()].End.Decorate(emitter,SE.Unit)," ",unit.name.token.tokenString,TT.END);
         if (unit is Module || unit is Section) PrintLudes(unit);
      }

      /// <summary>
      /// Translate all objects to strings using their to ToString, unless it is a TokenType, then use the glyph.
      /// </summary>
      /// <affix name="items"></affix>
      /// <returns></returns>
      private static string[] TranslateTokens(params object[] items) => items.Select(item => TranslateToken(item)).ToArray();
      private static string TranslateToken(object item) => item is TT tt ? Token.ToGlyph(tt) : item.ToString() ?? "";

      /// <summary>
      /// Emit the specified items at the current indent level.
      /// The methods with nl will add a new line at the beginning or end.
      /// </summary>
      /// <affix name="items"></affix>
      private void Emit(params object[] items) => emitter.Emit(TranslateTokens(items));
      private void EmitWithExtraSpace(bool extraSpace,params object[] items) => emitter.EmitWithExtraSpace(extraSpace,TranslateTokens(items));
      private void EmitSeparator(TT sep,bool space=true) => emitter.EmitIgnoreLineLength(TranslateToken(sep)+(space?" ":""));
      private void EmitSeparatorWithNL(TT sep) => emitter.EmitIgnoreLineLength(TranslateToken(sep),NL:true);
      private void Emitnl(params object[] items) => emitter.Emitnl(TranslateTokens(items));
      private void NlEmit(params object[] items) => emitter.NlEmit(TranslateTokens(items));
      private void NlEmitnl(params object[] items) => emitter.NlEmitnl(TranslateTokens(items));


   }
}
