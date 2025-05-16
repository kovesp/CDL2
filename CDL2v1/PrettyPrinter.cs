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
using System.Text.RegularExpressions;
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
   public class PrettyPrinter {
      private const int DEFAULT_LINE_LENGTH          = 100;
      private const int DEFAULT_INDENT_MULTIPLIER    = 3;
      private const int DEFAULT_MAX_INDENT_INCREMENT = 3;

      private int LineLength { get; set; }              = DEFAULT_LINE_LENGTH;              // Line length for wrapping        
      private int IndentMultiplier { get; set; }        = DEFAULT_INDENT_MULTIPLIER;        // The indent multiplier
      private int MaxIndentIncrement { get; set; }      = DEFAULT_MAX_INDENT_INCREMENT;     // The maximum number of times the indent can be incremented for wrapping.
   
      public readonly EmitterBase Emitter;

      private bool IncludeComments = true;


      /// <summary>
      /// Perform action with an increased indent level.
      /// </summary>
      /// <param Id="action"></param>
      private void Indented(Action action) => Emitter.Indented(action);
      /// <summary>
      /// Perform action keeping produced output together on one line.
      /// </summary>
      /// <param Id="action"></param>
      private void KeepTogether(Action action) => Emitter.KeepTogether(action);

      public static readonly FontWeight Bold = FontWeights.Bold;
      public static readonly FontStyle Italic = FontStyles.Oblique;
      public static TextDecorationCollection? Underline { get; internal set; } = TextDecorations.Underline;

      public record Decoration (string FG = "White", string BG = "#1E1E1E", DS Style = DS.Normal);
      public static readonly Decoration DefaultDecoration = new();

      private static readonly string AffixColor = "#9cdcfe";
      /// <summary>
      /// Decorators for all syntax elements.
      /// Colors may be specified as hex values of the form #rrggbb or
      /// as a color name, <see cref="System.Windows.Media.Colors"/> and https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.colors?view=windowsdesktop-9.0
      /// </summary>
      public static Dictionary<SE,Decoration> Decorators = new() {
         { SE.Id                       ,DefaultDecoration },
         { SE.Unit                     ,new(FG:"#569cd6",Style:DS.Bold) },
         { SE.Builtin                  ,new(FG:"#569cd6",Style:DS.Italic)},
         { SE.ReservedWord             ,new(FG:"#569cd6",Style:DS.Bold) },
         { SE.InputAffix               ,new(FG:AffixColor) },
         { SE.OutputAffix              ,new(FG:AffixColor.IntensifyColor(1.25)) },  // #51c0fd
         { SE.TransputAffix            ,new(FG:AffixColor.IntensifyColor(1.50)) },  // #26b1fd
         { SE.StringAffix              ,new(FG:"#d69d85") },
         { SE.Local                    ,new(FG:"DarkOrange") },
         { SE.Label                    ,new(FG:"LightGray") },
         { SE.Const                    ,new(FG:"Olive") },
         { SE.Var                      ,new(FG:"OliveDrab") },
         { SE.List                     ,new(FG:"DarkOliveGreen") },
         { SE.Number                   ,new(FG:"#b5cea8") },
         { SE.String                   ,new(FG:"#d69d85") },
         { SE.Comment                  ,new(FG:"#57a64a") },
         { SE.NoteError                ,new(FG:"Red") },
         { SE.NoteWarning              ,new(FG:"Orange") },
         { SE.NoteInfo                 ,new(FG:"LightSkyBlue") },
         { SE.ConditionalCompilationOn ,new(FG:"MediumSpringGreen",Style:DS.Italic) },
         { SE.ConditionalCompilationOff,new(FG:"DarkGray",Style:DS.Italic) },
         { SE.UNDEFINED                ,new(FG:"Red")},                             // Undefined identifiers.
         { SE.Other                    ,DefaultDecoration },                        // Will be used to obtain the overall background
         { SE.AlgorithmName            ,DefaultDecoration},                         // Not used, but required entry
       };

      public static Dictionary<AlgorithmNameType,Decoration> AlgorithmNameDecorators = new() {
         { AlgorithmNameType.None,new Decoration(FG:"#dcdcaa") },
       };

      static PrettyPrinter() {
         // Base decorator style for algorithms
         var baseDecorator = new Decoration(FG: "#dcdcaa");

         // Create decorators for all possible combinations of flags
         bool[] falseTrue = [false,true];
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
      /// Construct a pretty printer with a maximum line length and an indentation width using the specified Emitter.
      /// </summary>
      /// <param Id="width"></param>
      /// <param Id="indent"></param>
      /// <param Id="maxIndentIncrement"></param>
      /// <param Id="Emitter"></param>
      /// <example>
      ///   Construct a pretty printer that outputs to a file.
      ///   
      ///   PrettyPrinter pp = new PrettyPrinter(100,3,new FileCodeEmitter("output.txt"));
      ///   or simpler
      ///    PrettyPrinter pp = new("output.txt");
      /// </example>
      public PrettyPrinter(int width,int indent,int maxIndentIncrement,EmitterBase emitter,bool includeComments=true) {
         this.LineLength = width;
         this.IndentMultiplier = indent;
         this.MaxIndentIncrement = maxIndentIncrement;
         this.Emitter = emitter;
         this.IncludeComments = includeComments;
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
      /// Construct a pretty printer with a default maximum line length of <see cref="DEFAULT_LINE_LENGTH"/> and an indentation width of <see cref="DEFAULT_INDENT_MULTIPLIER"/> using the specified Emitter.
      /// </summary>
      /// <param Id="Emitter"></param>
      public PrettyPrinter(EmitterBase emitter,bool includeComments=true) : this (DEFAULT_LINE_LENGTH,DEFAULT_INDENT_MULTIPLIER,DEFAULT_MAX_INDENT_INCREMENT, emitter,includeComments) { }
      /// <summary>
      /// Construct a pretty printer with a default maximum line length of <see cref="DEFAULT_LINE_LENGTH"/> and an indentation width of <see cref="DEFAULT_INDENT_MULTIPLIER"/> using the specified file Id.
      /// </summary>
      /// <param Id="fileName">If this is null, use the <see cref="EmitterDebug"/> instead.</param>
      public PrettyPrinter(string? fileName) : this(DEFAULT_LINE_LENGTH,DEFAULT_INDENT_MULTIPLIER,DEFAULT_MAX_INDENT_INCREMENT, fileName.IsValidFileName() ? new EmitterFile(fileName ?? "") : new EmitterWindow()) { }

      private record struct UnitDelimiter(RW Start, RW End);
      private static readonly Dictionary<Type,UnitDelimiter> units = new() {
         { typeof(Program),new (RW.PROGRAM, RW.ENDPROG)},
         { typeof(Module),new (RW.MODULE, RW.ENDMOD)},
         { typeof(Layer),new (RW.LAYER, RW.ENDLAY)},
         { typeof(Section),new (RW.SECTION, RW.ENDSEC)},
      };

      public void Print(IDDictionary<Program> programs,IDDictionary<Module> modules) {
         Emitter.BeginUpdate();
         foreach (Program program in programs.Values) Print(program);
         foreach (Module module in modules.Values) Print(module);
         Emitter.EndUpdate();
      }

      public void Print(Program program) => PrintContainer(program,() => {
         PrintList(RW.PART,program.Parts,decorate:false);
         PrintLudes(program);
      },Newline: true,updateUI: true);

      public void Print(Module module) => PrintContainer(module,() => { foreach (Layer layer in module.Layers) Print(layer); },Newline: true,updateUI: true); 

      public void Print(Layer layer)   => PrintContainer(layer,() => { foreach (Section section in layer.Sections) Print(section); },updateUI: false);

      public void Print(Section section) => PrintContainer(section,() => {
         PrintList(RW.EXPORT,section.export);
         PrintList(RW.IMPORT,section.import);
         PrintList(RW.ABSTR,section.abstr);
         PrintList(RW.EXT,section.ext);
         PrintList(RW.INV,section.inv);

         int EmitCount<T>(IEnumerable<T> list,string type) {
            int count = list.Count();
            if (count > 0) { Emitnl(); NlEmitnl($"# {count} {type} definition{(count == 1 ? "" : "s")} #".Decorate(Emitter,SE.Comment)); }
            return count;
         }
         void PrintDataDefinitions<T>(RW type,IEnumerable<T> items,Action<T> print) where T : CDL2Object {
            if (EmitCount(items, type.ToString()) > 0) {
               foreach (T item in items) {
                  if (item.HasCommentOrNote) PrintComment(item);
                  Emit(type.Decorate(Emitter, SE.ReservedWord), " ");
                  print(item);
                  EmitSeparatorWithNL(TT.END);
               }
            }
         }
         void PrintAlgorithms<T>(string type,IEnumerable<T> list,Action<T> print) where T : Algorithm {
            if (EmitCount(list, type) > 0) foreach (T algorithm in list) print(algorithm);
         }

         PrintDataDefinitions(RW.CONST, section.Constants, Print);
         PrintDataDefinitions(RW.VAR, section.Variables, Print);
         PrintDataDefinitions(RW.LIST, section.Lists, l=>Print(l,section));
         PrintAlgorithms("Macro", section.Macros, Print);
         PrintAlgorithms("Procedure", section.NonSyntheticProcedures, a=>Print(a,section));

      },updateUI: true);     

      private void PrintLudes(Container container) {
         PrintLude(RW.PRELUDE,container);
         PrintLude(RW.ROOT,container);
         PrintLude(RW.POSTLUDE,container);
      }

      private void PrintLude(RW ludeType,Container container) {
         if (container is Section section) {
            if (section.Ludes[ludeType].Count != 0) {
               Emit(ludeType.Decorate(Emitter,SE.ReservedWord)," ");
               // SectionById Ludes are stored as ids of a generated Procedure item.
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
      private class Boxed<T> {
         public T? Value { get; set; }
         public Boxed() => Value = default;
      }

      private void Print(Alternative alternative, Section section,bool extraSpace=false) {
         Emitter.ExtraIndent = 0;
         if (alternative.calls.Count > 0) {
            PrintComment(alternative);
            Print(alternative.calls.First(), section, extraSpace: extraSpace, firstInAlternative: true);
            foreach (Call call in alternative.calls.Skip(1)) {
               EmitSeparator(TT.CALLSEP);
               Print(call, section);
            }
            if (alternative.lastCall.type != LCT.None) EmitSeparator(TT.CALLSEP);
         }

         if (alternative.lastCall.type != LCT.None) {            
            switch (alternative.lastCall.type) {
               case LastCallType.Standard:
                  Debug.Assert(alternative.lastCall.call != null,"alternative.lastCall.call is null");
                  Print(alternative.lastCall.call, section, firstInAlternative: alternative.calls.Count == 0);
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
                     Emit(alternative.lastCall.label.Name.Decorate(Emitter,SE.Label));
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
         if (! group.IsSynthetic) Emit(group.Id.Name.Decorate(Emitter,SE.Label),TT.LABELSEP);
         Print(group.alternatives,section);
         Emit(TT.GRPCLOSE);
      });

      private void Print(List<Alternative> alternatives,Section section) {
         Debug.Assert(alternatives.Count != 0,"alternatives list is empty");
         Print(alternatives.First(),section);
         foreach (Alternative alternative in alternatives.Skip(1)) {
            EmitSeparatorWithNL(TT.ALTSEP);
            Print(alternative,section,extraSpace:true);
         }
      }

      public void Print(Call call, Section section, bool extraSpace = false, bool firstInAlternative = false) => KeepTogether(() => {
         AlgorithmNameType callDecorator = AlgorithmNameType.None;
         Algorithm? called = null;
         if (section.TryGetDeclaration(call.id,out Algorithm? algorithm)) {
            called = algorithm;
            callDecorator = algorithm!.NameType;
         //} else {
         //   ReportError($"Internal error: {call.id} has no container. Something wrong with semantic analysis?");
         }
         if (call.IsBuiltin) {
            EmitWithExtraSpace(extraSpace, RW.BUILTIN.Decorate(Emitter, SE.Builtin), " ", call.id.Decorate(Emitter, SE.Builtin));
         } else if (called is null) {
            EmitWithExtraSpace(extraSpace, call.id.Decorate(Emitter, SE.UNDEFINED));
         } else if (called.IsConditionalCompilationOn) {
            EmitWithExtraSpace(extraSpace, call.id.Decorate(Emitter, SE.ConditionalCompilationOn));
         } else if (called.IsConditionalCompilationOff) {
            EmitWithExtraSpace(extraSpace, call.id.Decorate(Emitter, SE.ConditionalCompilationOff));
         } else { 
            EmitWithExtraSpace(extraSpace, call.id.Decorate(Emitter, AlgorithmNameDecorators[callDecorator]));
         }
         foreach (IActualArg arg in call.args) {
            Emit(TT.AFFIXSEP);
            switch (arg) {
               case STRING s:
                  Emit(s.AsDecoratedCDL2String(Emitter));
                  break;
               case Const c:
                  Emit(c.Id.Decorate(Emitter,SE.Const));
                  break;
               case Var v:
                  Emit(v.Id.Decorate(Emitter, SE.Var));
                  break;
               case Affix affix:
                  Emit(affix.Id.Decorate(Emitter, affix.SyntaxElement));
                  break;
               case Local local:
                  Emit(local.Id.Decorate(Emitter, SE.Local));
                  break;
               case ID id:
                  if (section.TryGetDeclaration(id, out CDL2Object? cdl2obj)) {
                     switch (cdl2obj) {
                        case Const constant:
                           Emit(id.Decorate(Emitter, SE.Const));
                           break;
                        case LIST list:
                           Emit(id.Decorate(Emitter, SE.List));
                           break;
                        case Var var:
                           Emit(id.Decorate(Emitter, SE.Var));
                           break;
                        default:
                           Emit(id);
                           break;
                     }
                  } else {
                     Emit(id);
                  }
                  break;
            }
            //if (arg is STRING s1) {
            //   Emit(s.AsDecoratedCDL2String(Emitter));
            //} else if (arg is ID Id) {
            //   if (call.TryGetAffix(Id,out Affix affix)) {
            //      Emit(Id.Decorate(Emitter,affix.SyntaxElement));
            //   } else if (call.TryGetLocal(Id,out Local _)) {
            //      Emit(Id.Decorate(Emitter,SE.Local));
            //   } else if (section.TryGetDeclaration(Id,out ICDL2Object? cdl2obj)) {
            //      switch (cdl2obj) {
            //         case Const constant:
            //            Emit(Id.Decorate(Emitter,SE.Const));
            //            break;
            //         case LIST list:
            //            Emit(Id.Decorate(Emitter,SE.List));
            //            break;
            //         case Var var:
            //            Emit(Id.Decorate(Emitter,SE.Var));
            //            break;
            //         default:
            //            Emit(Id);
            //            break;
            //      }
            //   } else {
            //      // Should not be possible
            //      Debug.WriteLine($"Internal error: Algorithm {call.Id} not found.");
            //      Emit(Id);
            //   }
            //}
         }
         // This is safe, because the MaxIndentIncrement limits the extra indent.
         if (!firstInAlternative && Emitter.WillKeepTogetherNotFitOnCurrentLine()) Emitter.ExtraIndent++;
         //static bool TryFindInvocationType(ID Id,ref AlgorithmNameType callDecorator,AlgorithmNameType callAttribute,Layer layer) {
         //   foreach (SectionById container in layer.Children.Cast<SectionById>()) {
         //      if (container.import.Contains(Id)) {
         //         callDecorator |= AlgorithmNameType.Imported;
         //         return true;
         //      } else if ((callAttribute == AlgorithmNameType.Ext ? container.ext : container.abstr).Contains(Id)) {
         //         callDecorator |= callAttribute;
         //         return true;
         //      }
         //   }
         //   return false;
         //}
      });

      private void PrintList(RW rw,IEnumerable<ID> ids,Section? section=null,bool decorate = true) {
         if (ids.Any()) {
            Emit(rw.Decorate(Emitter,SE.ReservedWord)," ",DecoratedID(ids.First(),section,decorate));
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
         if (decorate && (section?.TryGetDeclaration(id,out CDL2Object? obj)??false)) {
            if (obj!.SE == SE.AlgorithmName) {
               return id.Decorate(Emitter,AlgorithmNameDecorators[((Algorithm)obj).NameType]);
            } else {
               return id.Decorate(Emitter,obj.SE);
            }
         }
         return id.Name;
      }

      /// <summary>
      /// Print an algorithm which (of course) is either a Procedure or a Macro.
      /// </summary>
      /// <param name="algorithm"></param>
      public void Print(Algorithm algorithm) {
         if (algorithm is Procedure proc) {
            Print(proc, proc.Section!);
         } else {
            Print((Macro)algorithm);
         }
      }

      /// <summary>
      /// Print a ContainingProc unless it is IsSynthetic.
      /// </summary>
      /// <param name="proc"></param>
      public void Print(Procedure proc,Section section) {
         Debug.Assert(!proc.IsSynthetic,"Synthetic procedures should not be printed");
         PrintAlgorithmHeader(proc);
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
         PrintAlgorithmHeader(macro);
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
               Emit((withNl && s.value.Contains('\n')?"\n":""),s.AsDecoratedCDL2String(Emitter));
               break;
            case INT n:
               Emit(n.value.Decorate(Emitter));
               break;
            case FLOAT f:
               Emit(f.value.Decorate(Emitter));
               break;
            case ID id:
               Emit(id.Name);
               break;
            case Affix affix:
               Emit(affix.Id.Decorate(Emitter,affix.SyntaxElement));
               break;
            case Local local:
               Emit(local.Id.Decorate(Emitter,SE.Local));
               break;
            default:
               throw new NotImplementedException();
         }
      }

      private void PrintAlgorithmHeader(Algorithm algorithm) {
         PrintComment(algorithm);
         Emit(algorithm.algorithmType.Decorate(Emitter,SE.ReservedWord)," ",
            algorithm.Id.Decorate(Emitter,AlgorithmNameDecorator(algorithm)));
         foreach (Affix affix in algorithm.affixes.Cast<Affix>()) {
            Emit(affix.affixType == AffixType.std ? TT.AFFIXSEP : TT.STRINGAFFIXSEP);
            if (affix.IsInput) Emit(TT.AFFIXDIR);
            Emit(affix.Id.Decorate(Emitter,affix.SyntaxElement));
            if (affix.IsOutput) Emit(TT.AFFIXDIR);
         }
         if (algorithm.locals.Any()) {
            foreach (Local local in algorithm.locals) {
               Emit(" ",TT.LOCALSEP,local.Id.Decorate(Emitter,SE.Local));
            }
         }
         Emitnl(" ",algorithm.bodyType);
      }
      private Decoration AlgorithmNameDecorator(Algorithm alg) 
         => alg.IsConditionalCompilationOn ? Decorators[SE.ConditionalCompilationOn] : 
            alg.IsConditionalCompilationOff ? Decorators[SE.ConditionalCompilationOff] : 
            AlgorithmNameDecorators[alg.NameType];

      public void Print(Const constant) {
         //PrintIDComment(item,SE.Const);
         Emit(constant.Id.Decorate(Emitter, SE.Const));
         Emit(" ",TT.EQUALS," ");
         foreach (IConstElement element in constant.elements) {
            switch (element) {
               case STRING s:
                  Emit(s.value.Decorate(Emitter,SE.String));
                  break;
               case INT n:
                  Emit(n.value.Decorate(Emitter));
                  break;
               case FLOAT f:
                  Emit(f.value.Decorate(Emitter));
                  break;
               case Const c:
                  Emit(c.Id.Decorate(Emitter,SE.Const));
                  break;
               case ID id:
                  Emit(id.Name);
                  break;
               default:
                  throw new NotImplementedException();
            }
         }
      }

      public void Print(Var var) => Emit(var.Id.Decorate(Emitter, SE.Var));

      // TODO Fix printing of comments and notes
      private void PrintIDComment(CDL2Object obj,SE type) {
         if (obj.Comments != null || obj.Notes.Count > 0) {
            Emitter.Indented(
               () => {
                  //NlEmitnl(obj.Comments.Decorate(Emitter,SE.Comment));
                  PrintComment(obj);
                  Emit(obj.Id.Decorate(Emitter,type));
               }
            );
         } else {
            Emit(obj.Id.Decorate(Emitter,type));
         }
      }

      public void Print(LIST list,Section section) {
         Emit(list.Id.Decorate(Emitter, SE.List));
         Emit(TT.LISTBOUNDSTART,DecoratedID(list.lwb,section),TT.LISTBOUNDSEP,DecoratedID(list.upb,section),TT.LISTBOUNDEND);
      }

      /// <summary>
      /// Print the start and end of a container element, and then the contents.
      /// Print the Ludes for the container if it can have any at the correct place.
      /// (Why they couldn't position the Ludes in the same place for a PROGRAM as the other items is a mystery).
      /// </summary>
      /// <param PhaseName="element"></param>
      /// <param PhaseName="action"></param>
      private void PrintContainer(Container unit,Action action,bool Newline = false,bool updateUI = false) {
         PrintComment(unit);
         Emitnl(units[unit.GetType()].Start.Decorate(Emitter,SE.Unit)," ",unit.Id.Decorate(Emitter,SE.Id),TT.END);
         Indented(() => action());
         Emitnl(units[unit.GetType()].End.Decorate(Emitter,SE.Unit)," ",unit.Id.Name,TT.END);
         if (unit is Module || unit is Section) PrintLudes(unit);
         if (Newline) Emitnl();
         if (updateUI) Emitter.UpdateUI();
      }

      /// <summary>
      /// Print the comments for the element.
      /// </summary>
      /// <param name="element"></param>
      private void PrintComment(NamedElement element) => PrintComment(element.Comments,element.Notes);
      private void PrintComment(Alternative element) => PrintComment(null,element.Notes);

      private void PrintComment(string? comments,Notes notes) {
         if (IncludeComments) {
            if (comments != null) Emitnl(NormalizeDividers(comments).Decorate(Emitter, SE.Comment));
            foreach (Note note in notes) {
               if (note.Type == NoteType.Note) {
                  NlEmitnl(note.Text.Decorate(Emitter, SE.Comment));
                  Emitnl(RW.NOTE, Token.TokenType2Glyph[TT.END]);
               } else {
                  Emitnl(string.Concat("#", Note.Marker, (note.Type.ToString().ToUpper().PadRight(7)[..7] + " " + note.Number.ToString("D3") + ": "), note.Text)
                     .Decorate(Emitter, note.Type switch {
                        NoteType.Error => SE.NoteError,
                        NoteType.Warning => SE.NoteWarning,
                        NoteType.Info => SE.NoteInfo,
                        NoteType.Note => SE.Comment,
                        _ => SE.Comment
                     }));
               }
            }
         }
      }
      private string NormalizeDividers(string comments) 
         => string.Join("\n", comments.Split("\r\n").Select(l 
            => Regex.Replace(l, @"^#?\s*([=~#-])+\s*#?$",m => $"\n#{new string(m.Groups[1].Value[0], Emitter.LineLength-4)}#"))).TrimStart();

      /// <summary>
      /// Translate all objects to strings using their to ToString, unless it is a TokenType, then use the glyph.
      /// </summary>
      /// <param Id="items"></param>
      /// <returns></returns>
      private static string[] TranslateTokens(params object[] items) => [.. items.Select(item => TranslateToken(item))];
      private static string TranslateToken(object item) => item is TT tt ? Token.ToGlyph(tt) : item.ToString() ?? "";

      /// <summary>
      /// Emit the specified items at the current indent level.
      /// The methods with nl will add a new line at the beginning or end.
      /// </summary>
      /// <param Id="items"></param>
      private void Emit(params object[] items) => Emitter.Emit(TranslateTokens(items));
      private void EmitWithExtraSpace(bool extraSpace,params object[] items) => Emitter.EmitWithExtraSpace(extraSpace,TranslateTokens(items));
      private void EmitSeparator(TT sep,bool space=true) => Emitter.EmitIgnoreLineLength(TranslateToken(sep)+(space?" ":""));
      private void EmitSeparatorWithNL(TT sep) => Emitter.EmitIgnoreLineLength(TranslateToken(sep),NL:true);
      private void Emitnl(params object[] items) => Emitter.Emitnl(TranslateTokens(items));
      private void NlEmit(params object[] items) => Emitter.NlEmit(TranslateTokens(items));
      private void NlEmitnl(params object[] items) => Emitter.NlEmitnl(TranslateTokens(items));
   }
}
