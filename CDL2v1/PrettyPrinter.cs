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
      private void Indented(Action action) => emitter.Indented(action);
      /// <summary>
      /// Perform action keeping produced output together on one line.
      /// </summary>
      /// <param id="action"></param>
      private void KeepTogether(Action action) => emitter.KeepTogether(action);

      public static readonly FontWeight Bold = FontWeights.Bold;
      public static readonly FontStyle Italic = FontStyles.Oblique;
      public static TextDecorationCollection? Underline { get; internal set; } = TextDecorations.Underline;

      public record Decoration (string FG = "White", string BG = "#1E1E1E", DS Style = DS.Normal);
      public static readonly Decoration DefaultDecoration = new();

      private static string AffixColor = "#9cdcfe"; 
      public static Dictionary<SE,Decoration> Decorators = new() {
         { SE.Id                 ,DefaultDecoration },
         { SE.Unit               ,new Decoration(FG:"#569cd6",Style:DS.Bold) },
         { SE.ReservedWord       ,new Decoration(FG:"#569cd6",Style:DS.Bold) },
         { SE.InputAffix         ,new Decoration(FG:AffixColor) },
         { SE.OutputAffix        ,new Decoration(FG:AffixColor.IntensifyColor(1.25)) }, // #51c0fd
         { SE.TransputAffix      ,new Decoration(FG:AffixColor.IntensifyColor(1.50)) }, // #26b1fd
         { SE.StringAffix        ,new Decoration(FG:"#d69d85") },
         { SE.Local              ,new Decoration(FG:"DarkOrange") },
         { SE.Label              ,new Decoration(FG:"LightGray") },
         { SE.Const              ,new Decoration(FG:"Olive") },
         { SE.Var                ,new Decoration(FG:"OliveDrab") },
         { SE.List               ,new Decoration(FG:"DarkOliveGreen") },
         { SE.Number             ,new Decoration(FG:"#b5cea8") },
         { SE.String             ,new Decoration(FG:"#d69d85") },
         { SE.Comment            ,new Decoration(FG:"#57a64a") },
         { SE.Other              ,DefaultDecoration },                             // Will be used to obtain the overall background
         { SE.AlgorithmName      ,DefaultDecoration},                              // Not used, but required entry
       };

      public static Dictionary<AlgorithmNameType,Decoration> AlgorithmNameDecorators = new() {
         { AlgorithmNameType.None,new Decoration(FG:"#dcdcaa") },
       };

      static PrettyPrinter() {
         // Base decorator style for algorithms
         var baseDecorator = new Decoration(FG: "#dcdcaa");

         // Create decorators for all possible combinations of flags
         bool[] falseTrue = new[] { false,true };
         foreach (bool canFail in falseTrue) {
            foreach (bool isMacro in falseTrue) {
               foreach (bool hasEffect in falseTrue) {
                  // Skip the case where all are false - we already have "None" defined
                  if (!canFail && !isMacro && !hasEffect)
                     continue;

                  // Calculate combined flags
                  AlgorithmNameType flags = AlgorithmNameType.None;
                  if (canFail) flags |= AlgorithmNameType.CanFail;
                  if (isMacro) flags |= AlgorithmNameType.Macro;
                  if (hasEffect) flags |= AlgorithmNameType.HasEffect;

                  // Calculate decoration style
                  DS style = DS.Normal;
                  if (canFail) style |= DS.Italic;
                  if (isMacro) style |= DS.Underline;
                  if (hasEffect) style |= DS.Bold;

                  // Create and add the decorator
                  AlgorithmNameDecorators[flags] = new Decoration(
                      FG: baseDecorator.FG,
                      BG: baseDecorator.BG,
                      Style: style
                  );
               }
            }
         }
      }


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

      public void Print(Program program) => PrintContainer(program,() => {
         PrintList(RW.PART,program.Parts,decorate:false);
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
            Print(section.Lists.First(),section);
            foreach (LIST list in section.Lists.Skip(1)) {
               EmitSeparator(TT.LISTSEP);
               Print(list,section);
            }
            EmitSeparatorWithNL(TT.END);
         }

         if (EmitCount(section.Macros,"MACRO") > 0) foreach (Macro macro in section.Macros) Print(macro);

         if (EmitCount(section.NonSyntheticProcedures,"PROC ") > 0) foreach (Procedure proc in section.NonSyntheticProcedures) Print(proc,section);

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
               if (section.TryGetLocalDeclaration(section.Ludes[ludeType].First(),out Procedure? proc)) { // This should always be the case
                  Print(proc!.group.alternatives.First(),section);
                  EmitSeparatorWithNL(TT.END);
               } else {
                  ReportError($"Internal error: {ludeType} lude is not a Procedure item.");
               }
            }
         } else { 
            PrintList(ludeType,container.Ludes[ludeType],decorate:false);
         }
      }

      private void Print(Alternative alternative,Section section,bool extraSpace=false) {
         emitter.ExtraIndent = 0;
         if (alternative.calls.Count > 0) { 
            Print(alternative.calls.First(),section,extraSpace:extraSpace,firstInAlternative:true);
            foreach (Call call in alternative.calls.Skip(1)) {
               EmitSeparator(TT.CALLSEP);
               Print(call,section);
            }
            if (alternative.lastCall.type != LCT.None) EmitSeparator(TT.CALLSEP);
         }

         if (alternative.lastCall.type != LCT.None) {            
            switch (alternative.lastCall.type) {
               case LastCallType.Standard:
                  Debug.Assert(alternative.lastCall.call != null,"alternative.lastCall.call is null");
                  Print(alternative.lastCall.call,section,firstInAlternative:alternative.calls.Count==0);
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
                     Emit(alternative.lastCall.label.Name);
                  }
                  break;
               case LastCallType.Group:
                  Debug.Assert(alternative.lastCall.group is not null,"alternative.group is null");
                  Print(alternative.lastCall.group,section);
                  break;
            }
         }
      }

      private void Print(Group group,Section section) => Indented(() => {
         NlEmit(TT.GRPOPEN);
         if (group.id != ID.AnonID) Emit(group.id.Name,TT.LABELSEP);
         Print(group.alternatives,section);
         Emit(TT.GRPCLOSE);
      });

      private void Print(List<Alternative> alternatives,Section section) {
         Debug.Assert(alternatives.Any(),"alternatives list is empty");
         Print(alternatives.First(),section);
         foreach (Alternative alternative in alternatives.Skip(1)) {
            EmitSeparatorWithNL(TT.ALTSEP);
            Print(alternative,section,extraSpace:true);
         }
      }

      public void Print(Call call,Section section,bool extraSpace = false,bool firstInAlternative=false) => KeepTogether(() => {
         AlgorithmNameType callDecorator = AlgorithmNameType.None;
         Algorithm? called = null;
         if (section.TryGetDeclaration(call.id,out Algorithm? algorithm)) {
            called = algorithm;
            callDecorator = algorithm!.NameType;
         } else {
            ReportError($"Internal error: {call.id} has no container. Something wrong with semantic analysis?");
         }
         EmitWithExtraSpace(extraSpace,call.id.Decorate(emitter,AlgorithmNameDecorators[callDecorator]));

         foreach (IActualArg arg in call.args) {
            Emit(TT.PARAMSEP);
            if (arg is STRING s) {
               Emit(s.AsDecoratedCDL2String(emitter));
            } else if (arg is ID id) {
               if (call.TryGetAffix(id,out Affix affix)) {
                  Emit(id.Decorate(emitter,affix.SyntaxElement));
               } else if (call.TryGetLocal(id,out Local _)) {
                  Emit(id.Decorate(emitter,SE.Local));
               } else if (section.TryGetDeclaration(id,out ICDL2Object? cdl2obj)) {
                  switch (cdl2obj) {
                     case Const constant:
                        Emit(id.Decorate(emitter,SE.Const));
                        break;
                     case LIST list:
                        Emit(id.Decorate(emitter,SE.List));
                        break;
                     case Var var:
                        Emit(id.Decorate(emitter,SE.Var));
                        break;
                     default:
                        Emit(id);
                        break;
                  }
               } else {
                  // Should not be possible
                  Debug.WriteLine($"Internal error: Algorithm {call.id} not found.");
                  Emit(id);
               }
            }
         }
         // This is safe, because the MaxIndentIncrement limits the extra indent.
         if (!firstInAlternative && emitter.WillKeepTogetherNotFitOnCurrentLine()) emitter.ExtraIndent++;
         //static bool TryFindInvocationType(ID id,ref AlgorithmNameType callDecorator,AlgorithmNameType callAttribute,Layer layer) {
         //   foreach (Section container in layer.Children.Cast<Section>()) {
         //      if (container.import.Contains(id)) {
         //         callDecorator |= AlgorithmNameType.Imported;
         //         return true;
         //      } else if ((callAttribute == AlgorithmNameType.Ext ? container.ext : container.abstr).Contains(id)) {
         //         callDecorator |= callAttribute;
         //         return true;
         //      }
         //   }
         //   return false;
         //}
      });

      private void PrintList(RW rw,IEnumerable<ID> ids,Section? section=null,bool decorate = true) {
         if (ids.Any()) {
            Emit(rw.Decorate(emitter,SE.ReservedWord)," ",DecoratedID(ids.First(),section,decorate));
            foreach (ID id in ids.Skip(1)) {
               EmitSeparator(TT.LISTSEP);
               Emit(DecoratedID(id,section,decorate));
            }
            EmitSeparatorWithNL(TT.END);
         }
      }

      /// <summary>
      /// Print a list of ids. If decorate is true, then decorate the ids.
      /// </summary>
      /// <param name="id"></param>
      /// <param name="decorate"></param>
      /// <returns></returns>
      private string DecoratedID(ID id,Section? section,bool decorate=true) {
         if (decorate && (section?.TryGetDeclaration(id,out ICDL2Object? obj)??false)) {
            if (obj!.SE == SE.AlgorithmName) {
               return id.Decorate(emitter,AlgorithmNameDecorators[((Algorithm)obj).NameType]);
            } else {
               return id.Decorate(emitter,obj.SE);
            }
         }
         return id.Name;
      }

      /// <summary>
      /// Print a procedure unless it is IsSynthetic.
      /// </summary>
      /// <param name="proc"></param>
      public void Print(Procedure proc,Section section) {
         Debug.Assert(!proc.IsSynthetic,"Synthetic procedures should not be printed");
         PrintProcHead(proc);
         Indented(() => {
            Debug.Assert(proc.group.alternatives.Count != 0,"alternatives list is empty");
            Print(proc.group.alternatives.First(),section);
            foreach (Alternative alt in proc.group.alternatives.Skip(1)) {
               EmitSeparatorWithNL(TT.ALTSEP);
               Print(alt,section);
            }
            EmitSeparatorWithNL(TT.END);
         });
      }

      /// <summary>
      /// Print a macro.
      /// </summary>
      /// <param name="macro"></param>
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
               Emit((withNl && s.value.Contains('\n')?"\n":""),s.AsDecoratedCDL2String(emitter));
               break;
            case INT n:
               Emit(n.value.Decorate(emitter));
               break;
            case FLOAT f:
               Emit(f.value.Decorate(emitter));
               break;
            case ID id:
               Emit(id.Name);
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

      private void PrintProcHead(Algorithm algorithm) {
         Emit(algorithm.algorithmType.Decorate(emitter,SE.ReservedWord)," ",
            algorithm.id.Decorate(emitter,AlgorithmNameDecorators[algorithm.NameType]));
         foreach (Affix affix in algorithm.affixes.Cast<Affix>()) {
            Emit(affix.affixType == AffixType.std ? TT.PARAMSEP : TT.STRINGPARAMSEP);
            if (affix.IsInput) Emit(TT.AFFIXDIR);
            Emit(affix.id.Decorate(emitter,affix.SyntaxElement));
            if (affix.IsOutput) Emit(TT.AFFIXDIR);
         }
         if (algorithm.locals.Any()) {
            foreach (Local local in algorithm.locals) {
               Emit(" ",TT.LOCALSEP,local.id.Decorate(emitter,SE.Local));
            }
         }
         Emitnl(algorithm.bodyType);
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
                  Emit(id.Name);
                  break;
               default:
                  throw new NotImplementedException();
            }
         }
      }

      public void Print(LIST list,Section section) 
         => Emit(list.id.Decorate(emitter,SE.List),TT.LISTBOUNDSTART,DecoratedID(list.lwb,section),TT.LISTBOUNDSEP,DecoratedID(list.upb,section),TT.LISTBOUNDEND);

      /// <summary>
      /// Print the start and end of a container unit, and then the contents.
      /// Print the Ludes for the container if it can have any at the correct place.
      /// (Why they couldn't position the Ludes in the same place for a PROGRAM as the other items is a mystery).
      /// </summary>
      /// <param Name="unit"></param>
      /// <param Name="action"></param>
      private void PrintContainer(Container unit,Action action,bool Newline = false,bool updateUI = false) {
         if (unit.Comments != null) Emitnl(unit.Comments.Decorate(emitter,SE.Comment));
         Emitnl(units[unit.GetType()].Start.Decorate(emitter,SE.Unit)," ",unit.id.Decorate(emitter,SE.Id),TT.END);
         Indented(() => action());
         Emitnl(units[unit.GetType()].End.Decorate(emitter,SE.Unit)," ",unit.id.Name,TT.END);
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
