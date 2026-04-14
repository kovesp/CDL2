// <auto-gen>
//=======================================================================
// <copyright file="PrettyPrinter.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-02-17</creation-date>
// 
// <summary>
//   Formats the parse tree in a human-readable way, using the specified Emitter.
// </summary>
// <attribution>
//   This file is part of the clean room reimplementation of the
//      CDL2 Compiler
//      CDL2 Laboratory
//      CDL2 Target Code Generators
//
//    Based on original work on CDL and CDL2 led by C. H. A. Koster
//    and the CDL2 team at the Universities of Berlin, Germany and
//    Nijmegen, The Netherlands.
//
//    The CDL2 Laboratory was the work of Epsilon GmbH, Berlin.
//    H. M. Stahl, H. Feuerhahn, JP. Dehotay, B. Böhringer
//    (and others I don't remember ... sorry).
//
//    This project is not affiliated with the original CDL2 project.
// </attribution>
//=======================================================================
// </auto-gen>

// Ignore Spelling: CDL

using System.Diagnostics;
using System.Text.RegularExpressions;
#if WINDOWS
using System.Windows.Input;
#endif

using static CDL2v1.Logger;

namespace CDL2v1 {
   /// <summary>
   /// Formatted printing of the parse tree.
   /// </summary>
   public partial class PrettyPrinter {
      private const int DEFAULT_LINE_LENGTH = 100;
      private const int DEFAULT_INDENT_MULTIPLIER = 3;
      private const int DEFAULT_MAX_INDENT_INCREMENT = 3;

      private int LineLength { get; set; } = DEFAULT_LINE_LENGTH;              // Line length for wrapping        
      private int IndentMultiplier { get; set; } = DEFAULT_INDENT_MULTIPLIER;        // The indent multiplier
      private int MaxIndentIncrement { get; set; } = DEFAULT_MAX_INDENT_INCREMENT;     // The maximum number of times the indent can be incremented for wrapping.
      public bool SupressNotes { get; internal set; }

      public readonly Emitter Emitter;

      private readonly bool IncludeComments = true;


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

      // Color constants - Windows color names mapped to hex codes
      // Color names <see cref="System.Windows.Media.Colors"/> and https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.colors?view=windowsdesktop-9.0
      public const string White = "#FFFFFF";
      public const string DarkSlateGray = "#2F4F4F";
      public const string DarkOrange = "#FF8C00";
      public const string LightGray = "#D3D3D3";
      public const string Olive = "#808000";
      public const string OliveDrab = "#6B8E23";
      public const string DarkOliveGreen = "#556B2F";
      public const string Red = "#FF0000";
      public const string Orange = "#FFA500";
      public const string LightSkyBlue = "#87CEFA";
      public const string MediumSpringGreen = "#00FA9A";
      public const string DarkGray = "#A9A9A9";

      public const string BackgroundColor = DarkSlateGray;

      public partial record Decoration {
         private static readonly Regex HexColorRegex = ColourRE();
         public string FG { get; init; }
         public string BG { get; init; }
         public DS Style { get; init; }

         public Decoration(string FG = White,string BG = BackgroundColor,DS Style = DS.Normal) {
            if (!HexColorRegex.IsMatch(FG))
               throw new ArgumentException($"Foreground color in Decoration must be in #RRGGBB format, got: {FG}",nameof(FG));
            if (!HexColorRegex.IsMatch(BG))
               throw new ArgumentException($"Background color in Decoration must be in #RRGGBB format, got: {BG}",nameof(BG));

            this.FG = FG;
            this.BG = BG;
            this.Style = Style;
         }

         [GeneratedRegex(@"^#[0-9A-Fa-f]{6}$",RegexOptions.Compiled)]
         private static partial Regex ColourRE();
      }

      public static readonly Decoration DefaultDecoration = new();

      private static readonly string AffixColor = "#9cdcfe";
      /// <summary>
      /// Decorators for all syntax elements.
      /// Colors may be specified as hex values of the form #rrggbb only. Use the color constants defined above if you want names.
      /// </summary>
      public readonly static Dictionary<SE,Decoration> Decorators = new() {
         { SE.Id                       ,DefaultDecoration },
         { SE.Unit                     ,new(FG:"#569cd6",Style:DS.Bold) },
         { SE.Builtin                  ,new(FG:"#569cd6",Style:DS.Italic)},
         { SE.ReservedWord             ,new(FG:"#569cd6",Style:DS.Bold) },
         { SE.InputAffix               ,new(FG:AffixColor) },
         { SE.OutputAffix              ,new(FG:AffixColor.IntensifyColor(1.25)) },  // #51c0fd
         { SE.TransputAffix            ,new(FG:AffixColor.IntensifyColor(1.50)) },  // #26b1fd
         { SE.StringAffix              ,new(FG:"#d69d85") },
         { SE.Local                    ,new(FG:DarkOrange) },
         { SE.Label                    ,new(FG:LightGray) },
         { SE.Const                    ,new(FG:Olive) },
         { SE.Var                      ,new(FG:OliveDrab) },
         { SE.List                     ,new(FG:DarkOliveGreen) },
         { SE.Number                   ,new(FG:"#b5cea8") },
         { SE.String                   ,new(FG:"#d69d85") },
         { SE.Comment                  ,new(FG:"#57a64a") },
         { SE.NoteError                ,new(FG:Red) },
         { SE.NoteWarning              ,new(FG:Orange) },
         { SE.NoteInfo                 ,new(FG:LightSkyBlue) },
         { SE.ConditionalCompilationOn ,new(FG:MediumSpringGreen,Style:DS.Italic) },
         { SE.ConditionalCompilationOff,new(FG:DarkGray,Style:DS.Italic) },
         { SE.UNDEFINED                ,new(FG:Red)},                             // Undefined identifiers.
         { SE.Other                    ,DefaultDecoration },                        // Will be used to obtain the overall background
         { SE.AlgorithmName            ,DefaultDecoration},                         // Not used, but required entry
       };

      public readonly static Dictionary<AlgorithmNameType,Decoration> AlgorithmNameDecorators = new() {
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
      public PrettyPrinter(int width,int indent,int maxIndentIncrement,Emitter emitter,bool includeComments = true) {
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
      public PrettyPrinter(Emitter emitter,bool includeComments = true) : this(DEFAULT_LINE_LENGTH,DEFAULT_INDENT_MULTIPLIER,DEFAULT_MAX_INDENT_INCREMENT,emitter,includeComments) { }
      /// <summary>
      /// Construct a pretty printer with a default maximum line length of <see cref="DEFAULT_LINE_LENGTH"/> and an indentation width of <see cref="DEFAULT_INDENT_MULTIPLIER"/> using the specified file Id.
      /// </summary>
      /// <param Id="fileName">If this is null, use the <see cref="EmitterDebug"/> instead.</param>
      public PrettyPrinter(string? fileName) 
         : this(DEFAULT_LINE_LENGTH,DEFAULT_INDENT_MULTIPLIER,DEFAULT_MAX_INDENT_INCREMENT,fileName!.IsValidFileName ? new EmitterFile(fileName ?? "") 
                                                                                                                     : throw new ArgumentException("Invalid file name", nameof(fileName))) { }

      private record struct UnitDelimiter(RW Start,RW End);
      private static readonly Dictionary<Type,UnitDelimiter> units = new() {
         { typeof(Program),new (RW.PROGRAM, RW.ENDPROG)},
         { typeof(Module),new (RW.MODULE, RW.ENDMOD)},
         { typeof(Layer),new (RW.LAYER, RW.ENDLAY)},
         { typeof(Section),new (RW.SECTION, RW.ENDSEC)},
      };

      public void Print(IEnumerable<Program> programs,IEnumerable<Module> modules) {
         Emitter.BeginUpdate();
         foreach (Program program in programs) Print(program);
         foreach (Module module in modules) Print(module);
         Emitter.EndUpdate();
      }

      internal string Print(SingleSelection sel,bool withComment = false) {
         if (sel.ListType != SelectorType.INVALID) {
            Container? container = sel.Object as Container;
            Debug.Assert(container is not null,$"Selection with ListType {sel.ListType} has no container.");
            if (sel.ListType == SelectorType.LUDE) {
               return PrintLudes(container,asString: true);
            } else if (Container.LudeTypeBySelector.TryGetValue(sel.ListType,out ReservedWord rw)) {
               return PrintLude(rw,container,asString: true);
            } else if (sel.ListType == SelectorType.FACE) {
               // TODO print interface lists. Also add entry for individual interface lists.
               switch (sel.Object) {
                  case Section sec:
                     return PrintInterfaces(sec,asString: true);
                  case Layer lay:
                     foreach (Section sec in lay.Sections) PrintInterfaces(sec,withSectionComment: true);
                     return Emitter.Content;
                  case Module mod:
                     foreach (Section sec in mod.Sections) PrintInterfaces(sec,withSectionComment: true);
                     return Emitter.Content;
                  default:
                     return "";
               }
            } else if (Container.InterfaceTypeBySelector.TryGetValue(sel.ListType,out rw)) {
               switch (sel.Object) {
                  case Section sec:
                     PrintList(rw,sec.Interfaces[Container.InterfaceEnumBySelector[sel.ListType]]);
                     return Emitter.Content;
                  case Layer lay:
                     foreach (Section sec in lay.Sections) PrintList(rw,sec.Interfaces[Container.InterfaceEnumBySelector[sel.ListType]],sec,withSectionComment: true);
                     return Emitter.Content;
                  case Module mod:
                     foreach (Section sec in mod.Sections) PrintList(rw,sec.Interfaces[Container.InterfaceEnumBySelector[sel.ListType]],sec,withSectionComment: true);
                     return Emitter.Content;
                  default:
                     return "";
               }
            } else {
               throw new NotImplementedException($"Cannot print selection of type {sel.ListType}");
            }
         } else {
            return Print(sel.Object!,withComment);
         }
      }

      internal string Print(NamedElement namedElement,bool withComment = false) {
         if (withComment) PrintComment($"# {namedElement.FQDN()}");
         switch (namedElement) {
            // Units
            case Program program:
               Print(program);
               break;
            case Module module:
               Print(module);
               break;
            case Layer layer:
               Print(layer);
               break;
            case Section section:
               Print(section);
               break;
            // CDL2Objects
            case Algorithm algorithm:
               Print(algorithm);
               break;
            case Const constant:
               PrintDataDefinition(RW.CONST,Print,constant);
               break;
            case Var variable:
               PrintDataDefinition(RW.VAR,Print,variable);
               Print(variable);
               break;
            case LIST list:
               PrintDataDefinition(RW.LIST,l => Print(l,l.Section!),list);
               break;

            // Smaller elements
            case Group group:
               Print(group,group.Section!);
               break;
            case Alternative alternative:
               Print(alternative,alternative.Section!);
               break;
            case Call call:
               Print(call,call.Section!,extraSpace: false,firstInAlternative: true);
               break;
            case LastCall lastCall:
               Print(lastCall,lastCall.Section!);
               break;

            default:
               if (namedElement is not null) throw new NotImplementedException($"Cannot print {namedElement.GetType()}");
               break;
         }
         return Emitter.Content; // Return the content of the emitter. Only non-empty for EmitterString
      }
      internal void PauseUpdate(Action action) {
         if (Emitter.CanPauseUpdate) {
#if WINDOWS
            // Save the current cursor
            Cursor? previousCursor = Mouse.OverrideCursor;

            try {
               // Set the cursor to "Wait" (hourglass)
               Mouse.OverrideCursor = Cursors.Wait;

               // Begin update operation (stops UI updates until complete)
               Emitter.BeginUpdate();

               // Execute the action
               action();
            } finally {
               // Always restore the cursor and end update, even if an exception occurs
               Mouse.OverrideCursor = previousCursor;
               Emitter.EndUpdate();
            }
#else
            action();
#endif
         } else {
            // If updates cannot be paused, just execute the action directly
            action();
         }
      }

      public void Print(Program program) => PrintContainer(program,() => {
         PrintList(RW.PART,program.Parts,decorate: false);
         PrintLudes(program);
      },Newline: true,updateUI: true);

      public void Print(Module module) => PrintContainer(module,() => { foreach (Layer layer in module.Layers) Print(layer); },Newline: true,updateUI: true);

      public void Print(Layer layer) => PrintContainer(layer,() => { foreach (Section section in layer.Sections) Print(section); },updateUI: false);

      public void Print(Section section) => PrintContainer(section,() => {
         // Always print the interfaces at the top
         PrintInterfaces(section);

         if (Settings.SettingValue<bool>("PrettyPrintSorted")) {
            // Sort the section contents by their type
            int EmitCount<T>(IEnumerable<T> list,string type) {
               int count = list.Count();
               if (count > 0) { Emitnl(); NlEmitnl($"# {count} {type} definition{(count == 1 ? "" : "s")} #".Decorate(Emitter,SE.Comment)); }
               return count;
            }
            void PrintDataDefinitions<T>(RW type,IEnumerable<T> items,Action<T> print) where T : CDL2Object {
               if (EmitCount(items,type.ToString()) > 0) {
                  foreach (T item in items) {
                     PrintDataDefinition<T>(type,print,item);
                  }
               }
            }
            void PrintAlgorithms<T>(string type,IEnumerable<T> list,Action<T> print) where T : Algorithm {
               if (EmitCount(list,type) > 0) foreach (T algorithm in list) print(algorithm);
            }

            PrintDataDefinitions(RW.CONST,section.Constants,Print);
            PrintDataDefinitions(RW.VAR,section.Variables,Print);
            PrintDataDefinitions(RW.LIST,section.Lists,l => Print(l,section));
            PrintAlgorithms("Imported Algorithm",section.ImportedAlgorithms,alg=>Print(alg));
            PrintAlgorithms("Macro",section.Macros,Print);
            PrintAlgorithms("Procedure",section.NonSyntheticProcedures,a => Print(a,section));
         } else {
            // Print in the sequence they were added to the section or rearranged.
            foreach (CDL2Object cdl2obj in section.ChildElements().Cast<CDL2Object>()) {
               if (cdl2obj is Algorithm algorithm) {
                  Print(algorithm);
               } else if (cdl2obj is Const constant) {
                  PrintDataDefinition(RW.CONST,Print,constant);
               } else if (cdl2obj is Var variable) {
                  PrintDataDefinition(RW.VAR,Print,variable);
               } else if (cdl2obj is LIST list) {
                  PrintDataDefinition(RW.LIST,l => Print(l,section),list);
               } else {
                  ReportError($"Unknown CDL2Object type {cdl2obj.GetType()} in section {section.Id}");
               }
            }
         }
      },updateUI: true);
      /// <summary>
      /// Prints the interface lists contained within the specified section, including exports, imports, abstractions,
      /// extensions, and invariants.  
      /// </summary>
      /// <param name="section">The section whose interface lists will be printed. Must not be null.</param>
      /// <param name="asString"></param>
      private string PrintInterfaces(Section section,bool asString = false,bool withSectionComment = false) {
         if (withSectionComment) PrintLineComment(section);
         EmitOptNl(
                 PrintList(RW.EXPORT,section.Interfaces[InterfaceTypes.Export]) |
                 PrintList(RW.IMPORT,section.Interfaces[InterfaceTypes.Import]) |
                 PrintList(RW.ABSTR,section.Interfaces[InterfaceTypes.Abstr]) |
                 PrintList(RW.EXT,section.Interfaces[InterfaceTypes.Ext]) |
                 PrintList(RW.INV,section.Interfaces[InterfaceTypes.Inv])
               );
         return asString ? Emitter.Content : "";
      }

      /// <summary>
      /// Print a single data definition.
      /// </summary>
      /// <typeparam name="T">In Actual use T is one of Const, Var, or List. Since CDL2Object is a class, I'm too lazy
      /// to use a generic constraint on CDL2Object, so we can use this method for any CDL2Object type.</typeparam>
      /// <param name="type"></param>
      /// <param name="print"></param>
      /// <param name="item"></param>
      private void PrintDataDefinition<T>(RW type,Action<T> print,T item) where T : CDL2Object {
         EmitReservedwordForObject(type,item);
         Emit(" ");
         print(item);
         EmitSeparatorWithNL(TT.END);
      }

      /// <summary>
      /// Print all 3 ludes of the caintainer if any are present.
      /// </summary>
      /// <param name="container"></param>
      private string PrintLudes(Container container,bool asString = false) {
         PrintLude(RW.PRELUDE,container);
         PrintLude(RW.ROOT,container);
         PrintLude(RW.POSTLUDE,container);
         return asString ? Emitter.Content : "";
      }

      /// <summary>
      /// Print the specified lude type for the given container.
      /// </summary>
      /// <param name="ludeType"></param>
      /// <param name="container"></param>
      /// <param name="asString">Returns the lude as a string if true, otherwise prints it directly.</param>
      public string PrintLude(RW ludeType,Container container,bool asString = false) {
         if (container is Section section) {
            if (section.Ludes[ludeType].Count != 0) {
               // Section Ludes are stored as ids of a generated Procedure item and this is used as a indicator that the lude Procedure is present.
               if (section.LudeProcs[ludeType] is Guid guid) { // This should always be the case
                  EmitReservedwordForObject(ludeType);
                  Emit(" ");
                  Print(guid.ToCDL2Object<Procedure>()!.group.Alternatives.First(),section);
                  EmitSeparatorWithNL(TT.END);
               } else {
                  ReportError($"Internal error: {ludeType} lude Procedure is missing.");
               }
            }
         } else {
            // The Program and Module ludes are lists of module respectively section Ids.
            PrintList(ludeType,container.Ludes[ludeType],decorate: false);
         }
         return asString ? Emitter.Content : "";
      }
      //private class Boxed<T> {
      //   public T? Value { get; set; }
      //   public Boxed() => Value = default;
      //}

      /// <summary>
      /// Print an alternative: all calls and the last call.
      /// </summary>
      /// <param name="alternative"></param>
      /// <param name="section"></param>
      /// <param name="extraSpace"></param>
      private void Print(Alternative alternative,Section section,bool extraSpace = false) {
         Emitter.ExtraIndent = 0;
         if (alternative.Calls.Count > 0) {
            PrintComment(alternative,needsEnd: false);
            Print(alternative.Calls.First(),section,extraSpace: extraSpace,firstInAlternative: true);
            foreach (Call call in alternative.Calls.Skip(1)) {
               EmitSeparator(TT.CALLSEP);
               Print(call,section);
            }
            if (alternative.LastCall.type != LCT.None) EmitSeparator(TT.CALLSEP);
         }

         if (alternative.LastCall.type != LCT.None) Print(alternative.LastCall,section,extraSpace: extraSpace);
      }

      /// <summary>
      /// Print the last call of an alternative.
      /// </summary>
      /// <param name="lastCall"></param>
      /// <param name="section"></param>
      /// <param name="extraSpace"></param>
      private void Print(LastCall lastCall,Section section,bool extraSpace = false) {
         Debug.Assert(lastCall.type != LCT.None,"lastCall.type is None in call of Print(LastCall,...)");
         switch (lastCall.type) {
            case LastCallType.Standard:
               Debug.Assert(lastCall.call is not null,"lastCall.call is null");
               Print(lastCall.call,section,extraSpace: extraSpace,firstInAlternative: false);
               break;
            case LastCallType.Succeed:
               EmitWithExtraSpace(extraSpace,TT.SUCCEED);
               break;
            case LastCallType.Fail:
               EmitWithExtraSpace(extraSpace,TT.FAIL);
               break;
            case LastCallType.Abort:
               EmitWithExtraSpace(extraSpace,TT.ABORT);
               break;
            case LastCallType.Repeat:
               EmitWithExtraSpace(extraSpace,TT.REPEAT);
               if (!lastCall.label?.IsAnonymous ?? true) {
                  Emit(lastCall.label!.Name.Decorate(Emitter,SE.Label));
               }
               break;
            case LastCallType.Group:
               Debug.Assert(lastCall.group is not null,"lastCall.group is null");
               Print(lastCall.group!,section);
               break;
         }
      }

      /// <summary>
      /// Emits the formatted representation of the specified group and its alternatives to the output, using
      /// indentation to reflect structure.
      /// </summary>
      /// <param name="group">The group to be printed. Must not be null.</param>
      /// <param name="section">The section context in which the group is being printed. Determines formatting and emission rules.</param>
      private void Print(Group group,Section section) => Indented(() => {
         NlEmit(TT.GRPOPEN);
         if (!group.IsSynthetic) Emit(group.Id.Name.Decorate(Emitter,SE.Label),TT.LABELSEP);
         Print(group.Alternatives,section);
         Emit(TT.GRPCLOSE);
      });

      /// <summary>
      /// Prints a list of alternatives to the output, separating each alternative appropriately within the specified
      /// section.
      /// </summary>
      /// <param name="alternatives">The list of alternatives to print. Must contain at least one element.</param>
      /// <param name="section">The section in which the alternatives are to be printed.</param>
      private void Print(List<Alternative> alternatives,Section section) {
         Debug.Assert(alternatives.Count != 0,"alternatives list is empty");
         Print(alternatives.First(),section);
         foreach (Alternative alternative in alternatives.Skip(1)) {
            EmitSeparatorWithNL(TT.ALTSEP);
            Print(alternative,section,extraSpace: true);
         }
      }

      /// <summary>
      /// Print a call within an alternative. Decortions are applied based on type.
      /// </summary>
      /// <param name="call"></param>
      /// <param name="section"></param>
      /// <param name="extraSpace"></param>
      /// <param name="firstInAlternative"></param>
      public void Print(Call call,Section? section,bool extraSpace = false,bool firstInAlternative = false) => KeepTogether(() => {
         AlgorithmNameType callDecorator = AlgorithmNameType.None;
         Algorithm? called = null;
         section ??= call.ContainingProc.Section;
         if (section?.TryGetDeclaration(call.id,out Algorithm? algorithm)??false) {
            called = algorithm;
            callDecorator = algorithm!.NameType;
            //} else {
            //   ReportError($"Internal error: {call.id} has no container. Something wrong with semantic analysis?");
         }
         if (call.IsBuiltin) {
            EmitWithExtraSpace(extraSpace,RW.BUILTIN.Decorate(Emitter,SE.Builtin)," ",call.id.Decorate(Emitter,SE.Builtin));
         } else if (called is null) {
            EmitWithExtraSpace(extraSpace,call.id.Decorate(Emitter,SE.UNDEFINED));
         } else if (called.IsConditionalCompilationOn) {
            EmitWithExtraSpace(extraSpace,call.id.Decorate(Emitter,SE.ConditionalCompilationOn));
         } else if (called.IsConditionalCompilationOff) {
            EmitWithExtraSpace(extraSpace,call.id.Decorate(Emitter,SE.ConditionalCompilationOff));
         } else {
            EmitWithExtraSpace(extraSpace,call.id.Decorate(Emitter,AlgorithmNameDecorators[callDecorator]));
         }
         foreach (IActualArg arg in call.Args) {
            Emit(TT.AFFIXSEP);
            switch (arg) {
               case STRING s:
                  Emit(s.AsDecoratedCDL2String(Emitter));
                  break;
               case Const c:
                  Emit(c.Id.Decorate(Emitter,SE.Const));
                  break;
               case Var v:
                  Emit(v.Id.Decorate(Emitter,SE.Var));
                  break;
               case Affix affix:
                  Emit(affix.Id.Decorate(Emitter,affix.SyntaxElement));
                  break;
               case Local local:
                  Emit(local.Id.Decorate(Emitter,SE.Local));
                  break;
               case ID id:
                  if (section?.TryGetDeclaration(id,out CDL2Object? cdl2obj)??false) {
                     switch (cdl2obj) {
                        case Const constant:
                           Emit(id.Decorate(Emitter,SE.Const));
                           break;
                        case LIST list:
                           Emit(id.Decorate(Emitter,SE.List));
                           break;
                        case Var var:
                           Emit(id.Decorate(Emitter,SE.Var));
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

      /// <summary>
      /// Emits a formatted list of identifiers to the specified writer, optionally applying decoration and section
      /// context.
      /// </summary>
      /// <param name="rw">The writer to which the formatted list will be emitted.</param>
      /// <param name="ids">The collection of identifiers to include in the output list. If the collection is empty, no output is
      /// produced.</param>
      /// <param name="section">An optional section context used to influence the formatting or decoration of each identifier. If null,
      /// default formatting is applied.</param>
      /// <param name="decorate">true to apply decoration to each identifier; otherwise, false.</param>
      /// <returns>true if the list was emitted; otherwise, false if the collection of identifiers was empty.</returns>
      private bool PrintList(RW rw,IEnumerable<ID> ids,Section? section = null,bool decorate = true,bool withSectionComment = false) {
         if (ids.Any()) {
            if (withSectionComment && section is not null) PrintLineComment(section);
            EmitReservedwordForObject(rw);
            Emit(" ",DecoratedID(ids.First(),section,decorate));
            foreach (ID id in ids.Skip(1)) {
               EmitSeparator(TT.LISTSEP);
               EmitWithExtraSpace(true,DecoratedID(id,section,decorate));
            }
            EmitSeparatorWithNL(TT.END);
            return true;
         }
         return false;
      }

      /// <summary>
      /// Print a list of ids. If decorate is true, then decorate the ids.
      /// </summary>
      /// <param name="id"></param>
      /// <param name="decorate"></param>
      /// <returns></returns>
      private string DecoratedID(ID id,Section? section,bool decorate = true) {
         if (decorate && (section?.TryGetDeclaration(id,out CDL2Object? obj) ?? false)) {
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
      public void Print(Algorithm algorithm,bool synthetics = false) {
         if (algorithm is Macro macro) {
            Print(macro);
         } else if (algorithm is ImportedAlgorithm impProc) {
            Print(impProc);
         } else if (synthetics && algorithm.IsLude) {
            PrintLude(algorithm.LudeTpe,algorithm.Section!);
         } else {
            Print((Procedure)algorithm,algorithm.Section!);
         }
      }

      private static IEnumerable<RW> LudeProcRW = [RW.PRELUDE,RW.ROOT,RW.POSTLUDE];
      /// <summary>
      /// Print a ContainingProc unless it is IsSynthetic.
      /// </summary>
      /// <param name="proc"></param>
      public void Print(Procedure proc,Section section) {
         if (proc.IsSynthetic) {
            return;
         } else {
            PrintAlgorithmHeader(proc);
            Indented(() => {
               Debug.Assert(proc.group.Alternatives.Count != 0,"alternatives list is empty");
               Print(proc.group.Alternatives.First(),section);
               foreach (Alternative alt in proc.group.Alternatives.Skip(1)) {
                  EmitSeparatorWithNL(TT.ALTSEP);
                  Print(alt,section);
               }
               EmitSeparatorWithNL(TT.END);
            });
         }
      }
      /// <summary>
      /// Imported algorithms have their header printed.
      /// </summary>
      /// <param name="alg"></param>
      public void Print(ImportedAlgorithm alg) {
         PrintAlgorithmHeader(alg);
         EmitSeparatorWithNL(TT.END);
      }

      /// <summary>
      /// Print a macro.
      /// </summary>
      /// <param name="macro"></param>
      public void Print(Macro macro) {
         PrintAlgorithmHeader(macro);
         Indented(() => {
            Debug.Assert(macro.Elements.Count != 0,"macro elements list is empty");

            IElement elem1 = macro.Elements.First();
            bool wasID = PrintMacroElement(elem1,withNl: false);
            foreach (IElement elem in macro.Elements.Skip(1)) {
               wasID = PrintMacroElement(elem,withSpace: true,wasID: wasID);
            }
            EmitSeparatorWithNL(TT.END);
         });
      }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="elem"></param>
      /// <param name="withSpace"></param>
      /// <param name="withNl"></param>
      /// <returns>True if the element printed was an ID.</returns>
      /// <exception cref="NotImplementedException"></exception>
      private bool PrintMacroElement(IElement elem,bool withSpace = false,bool withNl = true,bool wasID = false) {
         if (withSpace) Emit(" ");
         switch (elem) {
            case STRING s:
               Emit((withNl && s.value.Contains('\n') ? "\n" : ""),s.AsDecoratedCDL2String(Emitter));
               break;
            case INT n:
               Emit(n.value.Decorate(Emitter));
               break;
            case FLOAT f:
               Emit(f.value.Decorate(Emitter));
               break;
            case ID id:
               if (wasID) Emit(TT.ELEMSEP," "); // If the previous element was an ID, add a separator.
               Emit(id.Name);
               break;
            case Affix affix:
               if (wasID) Emit(TT.ELEMSEP," ");
               Emit(affix.Id.Decorate(Emitter,affix.SyntaxElement));
               break;
            case Local local:
               if (wasID) Emit(TT.ELEMSEP," ");
               Emit(local.Id.Decorate(Emitter,SE.Local));
               break;
            default:
               throw new NotImplementedException();
         }
         return elem is ID || elem is Affix || elem is Local; // Return true if the element printed was an ID.
      }

      /// <summary>
      /// Print the algorithm header: type, name, affixes, locals, body type.
      /// </summary>
      /// <param name="algorithm"></param>
      private void PrintAlgorithmHeader(Algorithm algorithm) {
         EmitReservedwordForObject(algorithm.AlgorithmType,algorithm);
         Emit(" ",algorithm.Id.Decorate(Emitter,AlgorithmNameDecorator(algorithm)));
         foreach (Affix affix in algorithm.Affixes.Cast<Affix>()) {
            Emit(affix.affixType == AffixType.std ? TT.AFFIXSEP : TT.STRINGAFFIXSEP);
            if (affix.IsInput) Emit(TT.AFFIXDIR);
            Emit(affix.Id.Decorate(Emitter,affix.SyntaxElement));
            if (affix.IsOutput) Emit(TT.AFFIXDIR);
         }
         if (algorithm is not ImportedAlgorithm) {
            if (algorithm.Locals.Any()) {
               foreach (Local local in algorithm.Locals) {
                  Emit(" ",TT.LOCALSEP,local.Id.Decorate(Emitter,SE.Local));
               }
            }
            Emitnl(" ",algorithm.BodyType);
         }
      }
      /// <summary>
      /// Supply hte correct decorator for the algorithm name based on its conditional compilation status.
      /// </summary>
      /// <param name="alg"></param>
      /// <returns></returns>
      private static Decoration AlgorithmNameDecorator(Algorithm alg)
         => alg.IsConditionalCompilationOn ? Decorators[SE.ConditionalCompilationOn] :
            alg.IsConditionalCompilationOff ? Decorators[SE.ConditionalCompilationOff] :
            AlgorithmNameDecorators[alg.NameType];
      /// <summary>
      /// Emits the representation of the specified constant, including its identifier and value, to the output stream.
      /// </summary>
      /// <remarks>If the constant is marked as imported, only its identifier is emitted and its value is
      /// not printed. Elements within the constant that are not recognized types will result in an exception.</remarks>
      /// <param name="constant">The constant to be printed. Must not be null.</param>
      /// <exception cref="NotImplementedException">Thrown if the constant contains an element of an unsupported type.</exception>
      public void Print(Const constant) {
         Emit(constant.Id.Decorate(Emitter,SE.Const));
         if (constant.IsImported) return;
         Emit(" ",TT.EQUALS," ");
         bool wasID;
         foreach (IElement element in constant.elements) {
            wasID = false;
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
                  if (wasID) Emit(TT.ELEMSEP," "); // If the previous element was an ID, add a separator.
                  Emit(id.Name);
                  wasID = true;
                  break;
               default:
                  throw new NotImplementedException();
            }
         }
      }

      /// <summary>
      /// Print a variable.
      /// </summary>
      /// <param name="var"></param>
      public void Print(Var var) => Emit(var.Id.Decorate(Emitter,SE.Var));

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

      /// <summary>
      /// Print a List.
      /// </summary>
      /// <param name="list"></param>
      /// <param name="section"></param>
      public void Print(LIST list,Section section) {
         Emit(list.Id.Decorate(Emitter,SE.List));
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
      /// 
      /// </summary>
      /// <param name="section"></param>
      /// <exception cref="NotImplementedException"></exception>
      private void PrintLineComment(Section section) => Emitnl(("# " + section.FQDN()).Decorate(Emitter,SE.Comment));


      /// <summary>
      /// Print the comments for the element.
      /// </summary>
      /// <param name="element"></param>
      private void PrintComment(NamedElement element,bool nl = true) => PrintComment(element.Comments,element.Notes,nl);
      /// <summary>
      /// Print an alternative comment.
      /// </summary>
      /// <param name="element"></param>
      /// <param name="needsEnd"></param>
      private void PrintComment(Alternative element,bool needsEnd = true) => PrintComment(string.Empty,element.Notes,needsEnd: false);

      /// <summary>
      /// Print an inline comment, i.e., # comment #
      /// </summary>
      /// <param name="comment"></param>
      private void PrintInlineComment(string comment) {
         if (IncludeComments) {
            Emit($"{Token.TokenType2Glyph[TT.COMMENT]}{comment}{Token.TokenType2Glyph[TT.COMMENT]} ".Decorate(Emitter,SE.Comment));
         }
      }
      /// <summary>
      /// Adds # Imported # if the object is imported.
      /// </summary>
      /// <param name="obj"></param>
      /// <returns></returns>
      private bool PrintImportedComment(CDL2Object obj) { if (obj.IsImported) PrintInlineComment("Imported"); return obj.IsImported; }

      /// <summary>
      /// Emits the specified comment text and associated notes to the output, applying formatting and optional line
      /// breaks as specified.
      /// </summary>
      /// <remarks>
      /// Comments and notes are only emitted if the IncludeComments property is set to true. Notes
      /// with different severities are formatted distinctly to indicate their type.
      /// When the SuppressNotes flag is set, then only comments and user notes are emitted.
      /// </remarks>
      /// <param name="comments">The comment text to emit. If null, empty, or whitespace, no comment is emitted.</param>
      /// <param name="notes">A collection of notes to emit after the comment. Each note is formatted according to its severity.</param>
      /// <param name="nl">true to insert a new line before emitting the comment; otherwise, false. The default is true.</param>
      /// <param name="needsEnd">true to append an end marker after notes of type Note; otherwise, false. The default is true.</param>
      private void PrintComment(string comments,Notes? notes = null,bool nl = true,bool needsEnd = true) {
         if (IncludeComments) {
            if (comments.IsNotNullEmptyOrWhitespace) EmitOptNl(nl,NormalizeDividers(comments).Decorate(Emitter,SE.Comment));

            foreach (Note note in notes ?? Notes.Empty) {
               if (note.NoteType == Severity.Note) {
                  NlEmitnl(note.Text.TrimEnd().Decorate(Emitter,SE.Comment));
                  if (needsEnd) {
                     Emitnl(RW.NOTE,Token.TokenType2Glyph[TT.END]);
                  } else {
                     Emitnl(RW.NOTE);
                  }
               } else if (!SupressNotes) {
                  Emitnl(string.Concat("#",Note.Marker,(note.NoteType.ToString().ToUpper().PadRight(7)[..7] + " " + note.Number.ToString("D3") + ": "),note.Text)
                     .Decorate(Emitter,note.NoteType switch {
                        Severity.Error => SE.NoteError,
                        Severity.Warning => SE.NoteWarning,
                        Severity.Info => SE.NoteInfo,
                        _ => SE.Comment
                     }));
               }
            }
         }
      }
      /// <summary>
      /// Normalizes divider lines in the specified comment text to a consistent format.
      /// </summary>
      /// <remarks>This method is intended to ensure that divider lines within comment blocks use a uniform
      /// style and length. The normalization may affect the appearance of comments in generated documentation or code
      /// output.</remarks>
      /// <param name="comments">The comment text in which divider lines will be normalized. Cannot be null.</param>
      /// <returns>A string containing the comment text with all divider lines replaced by a standardized divider format.</returns>
      private string NormalizeDividers(string comments)
         => string.Join("\n",comments.Split("\r\n").Select(l
            => DividerLineRegex().Replace(l,m => $"\n#{new string(m.Groups[1].Value[0],Emitter.LineLength - 4)}#"))).Trim();

      /// <summary>
      /// Translate all objects to strings using their to ToString, unless it is a TokenType, then use the glyph.
      /// </summary>
      /// <param Id="items"></param>
      /// <returns></returns>
      private static string[] TranslateTokens(params object[] items) => [.. items.Select(item => TranslateToken(item))];
      private static string TranslateToken(object item) => item is TT tt ? Token.ToGlyph(tt) : item.ToString() ?? "";

      /// <summary>
      /// Emit a reserved word. Together with any comment the object might have.
      /// If the object is imported, add an inline comment "Imported".
      /// Ensure it is on a new line.
      /// </summary>
      /// <param name="type"></param>
      private void EmitReservedwordForObject(RW type,CDL2Object? obj = null) {
         if (obj is not null) {
            PrintComment(obj,nl: false);
            if (obj.IsImported) {
               PrintInlineComment("Imported");
               Emit(type.Decorate(Emitter,SE.ReservedWord));
               return;
            }
         }
         NlEmit(type.Decorate(Emitter,SE.ReservedWord));
      }

      /// <summary>
      /// Emit the specified items at the current indent level.
      /// The methods with nl will add a new line at the beginning or end.
      /// </summary>
      /// <param Id="items"></param>
      private void Emit(params object[] items) => Emitter.Emit(TranslateTokens(items));
      private void EmitWithExtraSpace(bool extraSpace,params object[] items) => Emitter.EmitWithExtraSpace(extraSpace,TranslateTokens(items));
      private void EmitSeparator(TT sep,bool space = true) => Emitter.EmitIgnoreLineLength(TranslateToken(sep) + (space ? " " : ""));
      private void EmitSeparatorWithNL(TT sep) => Emitter.EmitIgnoreLineLength(TranslateToken(sep),NL: true);
      private void Emitnl(params object[] items) => Emitter.Emitnl(TranslateTokens(items));
      private void EmitOptNl(bool nl,params object[] items) {
         if (nl) Emitnl(items); else Emit(items);
      }
      private void NlEmit(params object[] items) => Emitter.NlEmit(TranslateTokens(items));
      private void NlEmitnl(params object[] items) => Emitter.NlEmitnl(TranslateTokens(items));
      [GeneratedRegex(@"^#?\s*([=~#-])+\s*#?$")]
      private static partial Regex DividerLineRegex();
   }
}

