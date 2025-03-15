// Ignore Spelling: Transput CDL abstr ext inv ludes lude lwb upb FQN

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Xml.Linq;

namespace CDL2v1 {
   internal class Set<T> : HashSet<T> {
      public Set() { }
      public Set(IEnumerable<T> collection) : base(collection) { }
   }
   // Marker interfaces to allow lists to be composed of permissible elements.
   internal interface IMacroElement { }
   internal interface IConstElement { }
   internal interface IInterfaceElement { }
   internal interface IProvidedElement : IInterfaceElement { }
   internal interface IRequiredElement : IInterfaceElement { }
   internal interface IActualArg { }
   /// <summary>
   /// Any CDL2 object: Algorithm, Const, Var, LIST.
   /// </summary>
   internal interface ICDL2Object {
      public SE SE { get; }
   }
   /// <summary>
   /// Any CDL2 data object: Const, Var, LIST.
   /// </summary>
   internal interface ICDL2DataObject : ICDL2Object { }
   /// <summary>
   /// Any CDL2 object that is local to a section: Algorithm, Var, LIST.
   /// </summary>
   internal interface ILocalCDL2Object : ICDL2Object { }
   /// <summary>
   /// Any CDL2 data object that is local to a section: Var, LIST.
   /// </summary>
   internal interface ILocalCDL2DataObject : ILocalCDL2Object, ICDL2DataObject { }
   /// <summary>
   /// Represents a failure protected objects: output and transput affixes and variables. This means that if used in an algorithm that fails,
   /// any changes to the object is undone.
   /// </summary>
   /// <param name="id"></param>
   internal interface IFailureProtected : IActualArg { }
   internal interface IScope { }

   /// <summary>
   /// Base class for all elements that have names in the syntax tree.
   /// </summary>
   /// <param id="id"></param>
   internal class NamedElement(ID id) {
      public readonly ID id = id;
      public Container? Parent;      // null for the Program and Modules.

      override public string ToString() => $"{ItemTypeShortName} {id.Name}";
      protected virtual string ItemTypeShortName => GetType().Name.ToUpper()[..3];
      public string? Comments;
   }

   /// <summary>
   /// Base class for all elements that can contain other elements, i.e., the program and modules, layers, sections.
   /// </summary>
   internal abstract class Container : NamedElement, IScope {
      /// <summary>
      /// The Children of the container. Layers are ordered, hence the list.
      /// </summary>
      public List<Container> Children = [];
      /// <param id="id"></param>
      public Container(ID id,string? comments) : base(id) => Comments = comments;

      public Container(ID id,Container? parent,string? comments = null) : this(id,comments) { 
         Parent = parent;
         if (Parent != null && (bool)(Parent.Children.Contains(this))) {
            Logger.ReportError($"{ContainerName} is already a child of {Parent.ContainerName}");
         } else {
            this.Parent?.Children.Add(this);
         }
      }

      // The Ludes are stored in a dictionary with the reserved word as the key. The values are lists of IDs.
      // Section Ludes will be generated as Procedure items and given the id of the lude type (which are not legal as a CDL2 id).
      public readonly Dictionary<RW,List<ID>> Ludes = new() {
         { RW.PRELUDE,[] },
         { RW.ROOT,[] },
         { RW.POSTLUDE,[] }
      };

      /// <summary>
      /// Sets the LudeParser action for the container. The default is to do nothing.
      /// </summary>
      public Action<Parser,RW,Container> LudeParser = (parser,ludeType,container) => { };

      /// <summary>
      /// The short id of the container with its type. Used in the ToString method.
      /// </summary>
      public string ContainerName => $"{Parent?.ContainerName ?? ""} {ItemTypeShortName} {id.Name}".Trim();
   }

   /// <summary>
   /// Represents a program in the syntax tree.
   /// </summary>
   internal class Program : Container {
      override protected string ItemTypeShortName => "PROG";

      public static readonly Dictionary<ID,Program> Programs = [];   // Contains all the programs in the syntax tree.
      public static Program? FirstProgram = null;                    // The first program in the syntax tree.
      internal Set<ID> Parts = [];
      public static readonly Dictionary<ID,Module> Modules = [];     // Contains all the modules in the syntax tree.

      /// <summary>
      /// Program Ludes are a list of module IDs.
      /// </summary>
      /// <param id="id"></param>
      public Program(ID id,string? comments) : base(id,null,comments) {
         LudeParser = Parser.ParseLudeOfIDs;
         FirstProgram ??= this;
      }

      /// <summary>
      /// Get the modules that have the given lude type.
      /// </summary>
      /// <param id="ludeType"></param>
      /// <returns>A collection of modules that are in the lude of the given type.</returns>
      public IEnumerable<Module> Lude(RW ludeType) => this.Ludes[ludeType].Select(id => Program.Modules[id]);
      internal static Program? FindProgramByName(string programName) => Programs.TryGetValue(ID.From(new Token(programName),typeof(Program)),out Program? program) ? program : null;
   }

   /// <summary>
   /// Represents a module in the syntax tree.
   /// </summary>
   /// <param id="id"></param>
   internal class Module : Container {
      public readonly Set<ID> imports = [];                       // Imports are specified in sections, but are propagated up the module level.
      public readonly Dictionary<ID,Section> exports = [];        // Exports are specified in sections, but are propagated up the module level.

      /// <summary>
      /// Module Ludes are a list of container IDs.
      /// </summary>
      /// <param id="id"></param>
      public Module(ID id,string? comments) : base(id,null,comments) {
         LudeParser = Parser.ParseLudeOfIDs;
         Comments = comments;
      }
   }

   /// <summary>
   /// Represents a layer in the syntax tree.
   /// Notice that layers don't have Ludes.
   /// </summary>
   /// <param id="id"></param>
   /// <param id="module"></param>
   /// <param Name="ancestor">The layer from which this layer is extended. Null for the lowest layer.</param>
   internal class Layer(ID id,Module module,Layer? ancestor,string? comments = null) : Container(id,module,comments) {
      public readonly Layer? Ancestor = ancestor;
      public readonly Dictionary<ID,Section> ext = [];
      public readonly Dictionary<ID,Section> abstr = [];

   }

   /// <summary>
   /// Represents a container in the syntax tree.
   /// </summary>
   /// <param id="id"></param>
   /// <param id="layer"></param>
   internal class Section : Container {
      /// <summary>
      /// The interfaces.
      /// </summary>
      public readonly Set<ID> ext = [];
      public readonly Set<ID> abstr = [];
      public readonly Set<ID> inv = [];
      public readonly Set<ID> export = [];
      public readonly Set<ID> import = [];

      /// <summary>
      /// Hold the declarations of the section. The key is the ID of the declaration.
      /// </summary>
      public readonly Dictionary<ID,ICDL2Object> declarations = [];
      /// <summary>
      /// Acts a cache for <see cref="TryGetDeclaration{T}(ID,out T)"/>."/>
      /// </summary>
      public readonly Dictionary<ID,ICDL2Object> resolvedDeclarations = [];
      public IEnumerable<Const> Constants => declarations.Values.OfType<Const>();
      public IEnumerable<Var> Variables => declarations.Values.OfType<Var>();
      public IEnumerable<LIST> Lists => declarations.Values.OfType<LIST>();
      public IEnumerable<Macro> Macros => declarations.Values.OfType<Macro>();
      public IEnumerable<Procedure> Procedures => declarations.Values.OfType<Procedure>();
      /// <summary>
      /// The non-synthetic procedures.
      /// Synthetic procedures are generated by the parser for container ludes.
      /// </summary>
      public IEnumerable<Procedure> NonSyntheticProcedures => Procedures.Where(proc => !proc.IsSynthetic);

      /// <summary>
      /// Sections have Ludes each of which contains the ID of an internally generated CODE FUNCTION or ACTION which consist of a single alternative.
      /// TODO: Ensure that the generated CODE is correctly typed and that only ACTIONs and/or FUNCTIONs are called.
      /// </summary>
      /// <param id="id"></param>
      /// <param id="layer"></param>
      public Section(ID id,Layer layer,string? comments = null) : base(id,layer,comments) => LudeParser = Parser.ParseLudeOfCalls;

      public static Type[] ProvidedElementImplementors;
      static Section() => ProvidedElementImplementors = [.. Extensions.GetImplementorsOfInterface<IProvidedElement>()];

      /// <summary>
      /// Get the declaration with the given ID. If the declaration is not found in this section, it must be an inv and is looked for in the
      /// containing layer's extensions and the previous layer's abstractions.
      /// Assumes that semantic analysis has been done and that the declaration is found.
      /// </summary>
      /// <param name="id"></param>
      /// <typeparam name="T">The type of the requested object which must be an ICDL2Object.</typeparam>
      /// <returns>The declaration if found. </returns>
      /// 
      public bool TryGetDeclaration<T>(ID id,out T? declaration) where T : ICDL2Object {
         if (resolvedDeclarations.TryGetValue(id,out ICDL2Object? cached) && cached is T resolved) {
            declaration = resolved;
            return true; // Found in the cache
         } else if (TryGetLocalDeclaration(id,out ILocalCDL2Object? obj) && obj is T local) {
            declaration = local;
            resolvedDeclarations[id] = local; // Cache the result to avoid checking in both declaration and resolvedDeclaration.
            return true; // Found locally
         } else if (inv.Contains(id)) {
            Debug.Assert(Parent != null && Parent is Layer,$"Parent of {this} is null or not a Layer");
            Layer layer = (Layer)Parent;
            if (layer.ext.TryGetValue(id,out Section? declaringSection) && declaringSection.declarations[id] is T extended) {
               declaration = extended;
               resolvedDeclarations[id] = extended;
               return true;
            } else if (layer.Ancestor != null && layer.Ancestor.abstr.TryGetValue(id,out declaringSection) && declaringSection.declarations[id] is T abstracted) {
               declaration = abstracted;
               resolvedDeclarations[id] = abstracted;
               return true;
            }
         }
         declaration = default;
         return false;
      }
      public bool TryGetLocalDeclaration<T>(ID id,out T? declaration) where T : ILocalCDL2Object {
         if (declarations.TryGetValue(id,out ICDL2Object? obj) && obj is T local) {
            declaration = local;
            return true;
         }
         declaration = default;
         return false;
      }
   }

   // ---------------------------------------------------------------------------------------------------

   /// <summary>
   /// Represents an algorithm in the syntax tree. Concretely it is either a Macro or Procedure. 
   /// </summary>

   class DeclaredCDL2Object : NamedElement {
      /// <summary>
      /// True if the object is synthetic, i.e., generated by the parser. Currently only for Procedures, but perhaps needed for constants later.
      /// </summary>
      public readonly bool IsSynthetic;

      public DeclaredCDL2Object(ID id,Section section,string? comments,bool synthetic = false) : base(id) {
         Parent = section;
         IsSynthetic = synthetic;
         Comments = comments;
      }

      /// <summary>
      /// Fully qualified name as Module_Layer_Section_Object.
      /// Separator can be specified. Default is "_".
      /// </summary>
      /// <param name="separator"></param>
      /// <returns></returns>
      public string FQN(string separator = "_",string prefix = "",string replacement = "",bool camelCase = false) {
         string sectionName = Parent!.id.Name.AsIdentifier(prefix,replacement,camelCase);
         string layerName = Parent!.Parent!.id.Name.AsIdentifier(prefix,replacement,camelCase);
         string moduleName = Parent!.Parent!.Parent!.id.Name.AsIdentifier(prefix,replacement,camelCase);
         string objectName = id.Name.AsIdentifier(prefix,replacement,camelCase);
         return $"{moduleName}{separator}{layerName}{separator}{sectionName}{separator}{objectName}";
      }
   }
   /// <summary>
   /// Represents the common properties of Algorithms (Macros and Procedures).
   /// </summary>
   internal abstract class Algorithm : DeclaredCDL2Object, IProvidedElement, ILocalCDL2Object, IScope {
      // public readonly Section container = container;
      public readonly RW algorithmType;            // One of FUNCTION, ACTION, TEST or PREDICATE (reservedWordValue will never be null)
      public readonly TT bodyType;                 // One of : or := (for CODE only) and = or =: (for MACRO only)
      public readonly List<Affix> affixes;         // The affixes of this algorithm. A List because they are ordered.       
      public readonly Set<Local> locals;           // The declarations variables of this algorithm.

      public SE SE => SE.AlgorithmName;
      public Algorithm(ID id,List<Affix> formals,Set<Local> locals,Token algorithmType,TT bodyType,Section section,bool synthetic = false) 
            : base(id,section,algorithmType.Comments,synthetic) {
         this.affixes = formals;
         this.locals = locals;
         this.algorithmType = algorithmType.reservedWordValue ?? RW.FUNCTION;
         this.bodyType = bodyType;
      }

      public Section section => (Section)Parent!;

      public AlgorithmNameType NameType {
         get {
            AlgorithmNameType ait = AlgorithmNameType.None;
            if (algorithmType == RW.TEST || algorithmType == RW.PREDICATE) ait |= AlgorithmNameType.CanFail;
            if (algorithmType == RW.ACTION || algorithmType == RW.PREDICATE) ait |= AlgorithmNameType.HasEffect;
            if (bodyType == TT.MACROBODY || bodyType == TT.MACROPROCBODY) ait |= AlgorithmNameType.Macro;
            return ait;
         }
      }

      public DecorationStyle NameStyle {
         get {
            AlgorithmNameType ait = NameType;
            DecorationStyle ds = DecorationStyle.Normal;
            if (ait.HasFlag(ANT.CanFail)) ds |= DecorationStyle.Italic;
            if (ait.HasFlag(ANT.Macro)) ds |= DecorationStyle.Underline;
            if (ait.HasFlag(ANT.HasEffect)) ds |= DecorationStyle.Bold;
            return ds;
         }
      }
      public string AlgorithmName => $"{algorithmType} {id}";

      public bool CanFail => algorithmType == RW.TEST || algorithmType == RW.PREDICATE;
      public bool AlwaysSucceeds => !CanFail;
      public bool HasEffect => algorithmType == RW.PREDICATE || algorithmType == RW.ACTION;
      public Boolean NeedsFinalization => affixes.Any(affix => affix.IsOutput) || GetReferencedVariables().Any();

      public bool TryGetAffix(ID id,out Affix affix) => (affix = this.affixes.FirstOrDefault(affix => affix.id == id,Affix.Default)) != Affix.Default;
      public bool TryGetLocal(ID id,out Local local) => (local = locals.FirstOrDefault(local => local.id == id,Local.Default)) != Local.Default;

      /// <summary>
      /// Get the annotation symbols for the ID of this algorithm. Computed on first use.
      /// Note that the failure conditions should have been ruled out by the semantic analyzer.
      /// TODO: The above will be true for a full run, but not necessarily for a lab like environment
      /// </summary>
      //public SA NameAnnotation {
      //   get {
      //      SA getSA() {
      //         Section section = Parent as Section;
      //         Debug.Assert(section != null);

      //         if (section.inv.Contains(id)) { // More complicated then declarations. Need to find the container the algorithm is abstracted or extended from
      //                                         // Examine siblings to find the container the algorithm is extended from.
      //            Layer currentLayer = section.Parent as Layer;
      //            Debug.Assert(currentLayer != null);
      //            foreach (Section sibling in currentLayer.Children.Where(sec => sec != section).Cast<Section>()) {
      //               if (sibling.ext.Contains(id)) {
      //                  if (sibling.import.Contains(id)) return new SA(Prefix1: AS.Ext,Prefix2: AS.ImportExport);
      //                  return new SA(Prefix1: AS.Ext);
      //               }
      //            }
      //            // If still here, examine the layer below if any.
      //            List<Container> moduleLayers = currentLayer.Parent?.Children;
      //            Debug.Assert(moduleLayers != null);
      //            if (moduleLayers.Count > 1) {
      //               int currentLayerPosition = moduleLayers.IndexOf(currentLayer);
      //               if (currentLayerPosition > 0) {
      //                  Container layerBelow = moduleLayers[currentLayerPosition - 1];
      //                  foreach (Section ancestor in layerBelow.Children.Cast<Section>()) {
      //                     if (ancestor.abstr.Contains(id)) {
      //                        if (ancestor.import.Contains(id)) return new SA(Prefix1: AS.Abstr,Prefix2: AS.ImportExport);
      //                        return new SA(Prefix1: AS.Abstr);
      //                     }
      //                  }
      //               }
      //               return new SA(Prefix1: AS.Inv);   // Only possible in a partially analyzed context.
      //            } else { // declarations
      //               bool exported = section.export.Contains(id);
      //               bool imported = section.import.Contains(id);
      //               bool abstr = section.abstr.Contains(id);
      //               bool ext = section.ext.Contains(id);
      //               if (imported) {
      //                  if (abstr && ext) return new SA(Prefix1: AS.ImportExport,Suffix1: AS.AbstrExt);
      //                  if (abstr) return new SA(Prefix1: AS.ImportExport,Suffix1: AS.Abstr);
      //                  if (ext) return new SA(Prefix1: AS.ImportExport,Suffix1: AS.Ext);
      //               } else if (exported) {
      //                  if (abstr && ext) return new SA(Suffix1: AS.ImportExport,Suffix2: AS.AbstrExt);
      //                  if (abstr) return new SA(Suffix1: AS.ImportExport,Suffix2: AS.Abstr);
      //                  if (ext) return new SA(Suffix1: AS.ImportExport,Suffix2: AS.Ext);
      //               } else {
      //                  if (abstr && ext) return new SA(Suffix1: AS.AbstrExt);
      //                  if (abstr) return new SA(Suffix1: AS.Abstr);
      //                  if (ext) return new SA(Suffix1: AS.Ext);
      //               }
      //            }
      //         }
      //         return new SA();  // Should be impossible to get here.
      //      }
      //      return sa ??= getSA();
      //   }
      //}
      //private SA? sa = null;
      /// <summary>
      /// Used to force the re-computation of the Name annotations.
      /// TODO: figure out when to re-compute Name annotations.
      /// </summary>
      //public void ResetNameAnnotations() => sa = null;
      public abstract IEnumerable<Var> GetReferencedVariables();


      override protected string ItemTypeShortName => $"{algorithmType}";
   }

   /// <summary>
   /// An imported algorithm is a reference to an algorithm in another module. Thus it has only a header and no body.
   /// </summary>
   internal class ImportedAlgorithm(ID id,List<Affix> formals,Token algorithmType,Section section) : Algorithm(id,formals,[],algorithmType,TT.NOBODY,section) {
      public override IEnumerable<Var> GetReferencedVariables() => [];
   }

   /// <summary>
   /// Represents a macro in the syntax tree.
   /// </summary>
   /// <param id="id"></param>
   /// <param id="affixes"></param>
   /// <param id="locals"></param>
   /// <param id="algorithmType"></param>
   /// <param id="bodyType"></param>
   /// <param id="container"></param>
   internal class Macro(ID id,List<Affix> formals,Set<Local> locals,Token algorithmType,TT bodyType,Section section) : Algorithm(id,formals,locals,algorithmType,bodyType,section) {
      public List<IMacroElement> elements = [];

      public override IEnumerable<Var> GetReferencedVariables() => elements.OfType<Var>();
   }
   /// <summary>
   /// Represents a code in the syntax tree.
   /// </summary>
   /// <param id="id"></param>
   /// <param id="affixes"></param>
   /// <param id="locals"></param>
   /// <param id="algorithmType"></param>
   /// <param id="bodyType"></param>
   /// <param id="section"></param>
   internal class Procedure(ID id,List<Affix> formals,Set<Local> locals,Token algorithmType,TT bodyType,Section section,bool synthetic = false) 
         : Algorithm(id,formals,locals,algorithmType,bodyType,section,synthetic) {
      public Group group = new(id,[],null);
      /// <summary>
      /// True if the procedure is an Action or Function that has only a single alternative which is a sequence of calls none of which can fail.
      /// </summary>
      public bool IsVerySimple {
         get {
            if (isVerySimple.notset) {
               isVerySimple.notset = false;
               if (AlwaysSucceeds && group.alternatives.Count == 1) {
                  List<Call> calls = group.alternatives[0].calls;
                  LastCall lastCall = group.alternatives[0].lastCall;
                  if ((lastCall.type == LCT.Standard || lastCall.type == LCT.Succeed)
                        && calls.All(call => call.TryGetCalled(out Algorithm? called1) && ! called1!.AlwaysSucceeds)) { 
                     isVerySimple.value = lastCall.type == LCT.Succeed || lastCall.TryGetCalled(out Algorithm? called2) && called2!.AlwaysSucceeds;
                  }
               }
            }
            return isVerySimple.value;
         }
      }
      private (bool notset, bool value) isVerySimple = (true, false);
      /// <summary>
      /// Can have alternatives, but there are no repeats.
      /// </summary>
      public bool IsSimple => !CanFail && HasNoRepeat(group);

      private static bool HasNoRepeat(Group group) {
         foreach (Alternative alternative in group.alternatives) {
            if (alternative.lastCall.type == LCT.Repeat) return false;
            if (alternative.lastCall.type == LCT.Group && !HasNoRepeat(alternative.lastCall.group!)) return false;
         }
         return true;
      }

      public Procedure(RW ludeType,Section section) : this(ID.From(section,ludeType),[],[],Token.ACTIONToken,TT.CODEBODY,section,true) { } // Used for container Ludes which are parameterless actions with no locals.
      public override IEnumerable<Var> GetReferencedVariables() {
         Set<Var> variables = [];
         CollectReferencedVariables(group,variables);
         return variables;
      }
      private static void CollectReferencedVariables(Group group,Set<Var> variables) {
         foreach (Alternative alternative in group.alternatives) {
            foreach (Call call in alternative.calls) foreach (Var variable in call.args.OfType<Var>()) variables.Add(variable);
            if (alternative.lastCall.type == LCT.Standard) {
               foreach (Var variable in alternative.lastCall.call!.args.OfType<Var>()) variables.Add(variable);
            } else if (alternative.lastCall.type == LCT.Group) {
               CollectReferencedVariables(alternative.lastCall.group!,variables);
            }
         }
      }
   }

   internal class Call(ID id,Procedure proc) {
      public readonly ID id = id;
      public readonly List<IActualArg> args = [];
      public readonly Procedure procedure = proc;
      override public string ToString() => $"{id.Name}+{string.Join("+",args)}";
      internal bool TryGetAffix(ID id,out Affix affix) => procedure.TryGetAffix(id,out affix);
      internal bool TryGetLocal(ID id,out Local local) => procedure.TryGetLocal(id,out local);
      internal bool TryGetCalled(out Algorithm? called) {
         Debug.Assert(procedure != null,$"{this} has null procedure or its Parent is not a Section ");
         if (procedure.section.TryGetDeclaration(id,out called)) return true;
         called = null;
         return false;
      }
      public bool CanFail => procedure.CanFail;
      public bool AlwaysSucceeds => procedure.AlwaysSucceeds;
   }
   /// <summary>
   /// The last element(in an alternative) can be:
   /// Standard - a normal algorithm call which is the last item in the alternative's call list.
   /// Success, Fail, Abort - i.e., +, -, or?.
   /// Repeat - * with a reference to the group that is repeated possibly using the label
   /// Group - a nested group.
   /// </summary>
   /// <param id="type"></param>   
   internal class LastCall(LCT type) {

      public readonly LCT type = type;
      public readonly Group? group;
      public readonly Call? call;
      public readonly ID? label = ID.AnonID;

      public LastCall(Call call) : this(LCT.Standard) => this.call = call;
      public LastCall(Group group) : this(LCT.Group) => this.group = group;
      public LastCall(ID? label) : this(LCT.Repeat) => this.label = label;

      internal bool TryGetCalled(out Algorithm? called) {
         if (type == LCT.Standard && call!.procedure.section.TryGetDeclaration(call.id,out called)) return true;
         called = null;
         return false;
      }

      public override string ToString() => type switch {
         LCT.Standard => call?.ToString() ?? "",
         LCT.Succeed => "+",
         LCT.Fail => "-",
         LCT.Abort => "?",
         LCT.Repeat => $"*{(label is null || label == ID.AnonID ? "" : label.Name)}",
         LCT.Group => group?.ToString() ?? "",
         _ => "ERROR",
      };
   }
   internal class Alternative(List<Call> calls,LastCall lastCall) {
      public readonly List<Call> calls = calls;
      public readonly LastCall lastCall = lastCall;
   }
   // Note that the id in this case is the label.
   internal class Group(ID label,List<Alternative> alternatives,Group? parent) : NamedElement(label) {
      public List<Alternative> alternatives = alternatives;
      public new readonly Group? Parent = parent;
   }


   internal class INT : IConstElement, IMacroElement {
      public readonly long value;
      public INT(Token intToken) {
         Debug.Assert(intToken.type == TT.INT && intToken.intValue != null);
         value = (long)intToken.intValue;
      }
      override public string ToString() => value.ToString();
   }
   internal class FLOAT : IConstElement, IMacroElement {
      public readonly double value;
      public FLOAT(Token floatToken) {
         Debug.Assert(floatToken.type == TT.FLOAT && floatToken.floatValue != null);
         value = (double)floatToken.floatValue;
      }
      override public string ToString() => value.ToString();
   }
   internal class STRING : IMacroElement, IConstElement, IActualArg {
      public readonly string value;
      public STRING(Token str) {
         Debug.Assert(str.type == TT.STRING && str.StringValue != null);
         value = str.StringValue;
      }

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
      public string AsDecoratedCDL2String(EmitterBase emitter) => $"\"{EscapedCDL2(value)}\"".Decorate(emitter,SE.String);
      override public string ToString() => $"\"{value}\"";
   }
   internal class LIST(ID id,Section section,ID lwb,ID upb) : DeclaredCDL2Object(id,section,null), IMacroElement, ILocalCDL2DataObject {
      public readonly ID lwb = lwb;
      public readonly ID upb = upb;

      public SE SE => SE.List;

      override public string ToString() => $"LIST {id}({lwb}:{upb})";
   }
   internal class Var(ID id,Section section) : DeclaredCDL2Object(id,section,null), IFailureProtected, IMacroElement, ILocalCDL2DataObject, IActualArg {
      public SE SE => SE.Var;

      override public string ToString() => $"VAR {id.Name}";
   }
   internal class Const(ID id,Section section) : DeclaredCDL2Object(id,section,null), IConstElement, IMacroElement, IProvidedElement, ICDL2DataObject, ILocalCDL2Object, IActualArg {
      public SE SE => SE.Const;
      public readonly List<IConstElement> elements = [];  // Will contain ids (const, var, list) and strings, integers, floats
      // override public string ToString() => $"CONST {id.id}={string.Join("",elements)}";
   }

   internal class ImportedConst(ID id,Section section) : Const(id,section) { }



   /// <summary>
   /// Represents a formal argument in an algorithm.
   /// It is just an ID with annotations. An arg is considered to be equal to another arg or ID if the names are the same.
   /// </summary>
   /// <param id="id"></param>
   /// <param id="dir"></param>
   /// <param id="type"></param>
   internal class Affix(ID id,AffixDir dir,AffixType type) : NamedElement(id), IFailureProtected, IMacroElement  {
      internal static readonly Affix Default = new (ID.AnonID,AffixDir.NONE,AffixType.std);
      public readonly AffixDir affixDir = dir;
      public readonly AffixType affixType = type;

      public Boolean IsInput => affixDir == AffixDir.input || affixDir == AffixDir.transput;
      public Boolean IsOutput => affixDir == AffixDir.output || affixDir == AffixDir.transput;
      public Boolean IsTransput => affixDir == AffixDir.transput;
      public Boolean IsString => affixType == AffixType.str;

     public SE SyntaxElement => IsString ? SE.StringAffix : IsTransput ? SE.TransputAffix : IsInput ? SE.InputAffix : SE.OutputAffix;

      public override bool Equals(object? obj) => obj is Affix affix && EqualityComparer<ID>.Default.Equals(id,affix.id);
      public override int GetHashCode() => HashCode.Combine(id);

      override public string ToString() => affixType == AffixType.std ? $"+{(IsInput ? ">" : "")}{id}{(IsOutput ? ">" : "")}" : $"*{id}";

      public static bool operator ==(Affix? left,Affix? right) => EqualityComparer<Affix>.Default.Equals(left,right);
      public static bool operator !=(Affix? left,Affix? right) => !(left == right);
   }

   internal class Local(ID id) : NamedElement(id), IMacroElement, IActualArg {
      internal static readonly Local Default = new(ID.AnonID);

      override public string ToString() => $"-{id.Name}";
   }

   internal class Undeclared() : NamedElement(ID.AnonID), ICDL2Object {
      public SE SE => SE.Other;
      internal readonly static Undeclared Instance = new();
   }

}
