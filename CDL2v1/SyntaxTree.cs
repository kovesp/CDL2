// Ignore Spelling: Transput

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace CDL2v1 {
   internal class Set<T> : HashSet<T> {
      public Set() { }
      public Set(IEnumerable<T> collection) : base(collection) { }
   }

   /// <summary>
   /// Base class for all elements that have names in the syntax tree.
   /// </summary>
   /// <param id="id"></param>
   internal class NamedElement(ID id) {
      public readonly ID id = id;

      override public string ToString() => $"{ItemTypeShortName} {id.token.TokenString}";
      public string AsName(string replacement="") => id.AsIdentifier(replacement);
      protected virtual string ItemTypeShortName => GetType().Name.ToUpper()[..3];

      public Container? Parent;      // null for the Program and Modules.
   }

   // Marker interfaces to allow lists to be composed of permissible elements.
   internal interface IMacroElement { }
   internal interface IConstElement { }
   internal interface IInterfaceElement { }
   internal interface IProvidedElement : IInterfaceElement { }
   internal interface IRequiredElement : IInterfaceElement { }
   internal interface IActualArg { }
   internal interface ICDL2Object { }

   /// <summary>
   /// Base class for all elements that can contain other elements, i.e., the program and modules, layers, sections.
   /// </summary>
   /// <param id="id"></param>
   internal abstract class Container(ID id) : NamedElement(id) {
      public Container(ID id,Container? parent) : this(id) {
         this.Parent = parent;
         this.Parent?.Children.Add(this);
      }

      /// <summary>
      /// The Children of the container. Layers are ordered, hence the list.
      /// </summary>
      public List<Container> Children = [];

      // The Ludes are stored in a dictionary with the reserved word as the key. The values are lists of IDs.
      // Section Ludes will be generated as Procedure items and given the id of the lude type (which are not legal as a CDL2 id).
      public readonly Dictionary<RW,List<ID>> Ludes = new() {
         { RW.PRELUDE,[] },
         { RW.ROOT,[] },
         { RW.POSTLUDE,[] }
      };

      /// <summary>
      /// Sets the ParseLude action for the container. The default is to do nothing.
      /// </summary>
      public Action<Parser,RW,Container> ParseLude = (parser,ludeType,container) => { };

      /// <summary>
      /// The short id of the container with its type. Used in the ToString method.
      /// </summary>
      public string ContainerName => $"{Parent?.ContainerName ?? ""} {ItemTypeShortName} {id.token.TokenString}";
   }

   /// <summary>
   /// Represents a program in the syntax tree.
   /// </summary>
   internal class Program : Container {
      override protected string ItemTypeShortName => "PROG";

      public static readonly Dictionary<ID,Program> Programs = [];   // Contains all the programs in the syntax tree.
      public static readonly Dictionary<ID,Module> Modules = [];     // Contains all the modules in the syntax tree.

      /// <summary>
      /// Program Ludes are a list of module IDs.
      /// </summary>
      /// <param id="id"></param>
      public Program(ID id) : base(id,null) => ParseLude = Parser.ParseLudeOfIDs;

      /// <summary>
      /// Get the modules that have the given lude type.
      /// </summary>
      /// <param id="ludeType"></param>
      /// <returns>A collection of modules that are in the lude of the given type.</returns>
      public IEnumerable<Module> Lude(RW ludeType) => this.Ludes[ludeType].Select(id => (Module)Symbols[id]);
   }

   /// <summary>
   /// Represents a module in the syntax tree.
   /// </summary>
   /// <param id="id"></param>
   internal class Module : Container {
      public readonly Set<ID> imports = [];         // Imports are specified in sections, but are propagated up the module level.
      public readonly Set <ID> exports = [];        // Exports are specified in sections, but are propagated up the module level.

      /// <summary>
      /// Module Ludes are a list of section IDs.
      /// </summary>
      /// <param id="id"></param>
      public Module(ID id) : base(id) => ParseLude = Parser.ParseLudeOfIDs;
   }

   /// <summary>
   /// Represents a layer in the syntax tree.
   /// Notice that layers don't have Ludes.
   /// </summary>
   /// <param id="id"></param>
   /// <param id="module"></param>
   /// <param name="ancestor">The layer from which this layer is extended. Null for the lowest layer.</param>
   internal class Layer(ID id,Module module,Layer? ancestor) : Container(id,module) {
      public readonly Layer? Ancestor = ancestor;
      public readonly Dictionary<ID,Section> ext = [];
      public readonly Dictionary<ID,Section> abstr = [];
   }

   /// <summary>
   /// Represents a section in the syntax tree.
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

      public readonly Dictionary<ID,ICDL2Object> local = []; 

      /// <summary>
      /// Sections have Ludes each of which contains the ID of an internally generated CODE FUNCTION or ACTION which consist of a single alternative.
      /// TODO: Ensure that the generated CODE is correctly typed and that only ACTIONs and/or FUNCTIONs are called.
      /// </summary>
      /// <param id="id"></param>
      /// <param id="layer"></param>
      public Section(ID id,Layer layer) : base(id,layer) => ParseLude = Parser.ParseLudeOfCalls;

      public static Type[] ProvidedElementImplementors;
      static Section() {
         ProvidedElementImplementors = Extensions.GetImplementorsOfInterface<IProvidedElement>().ToArray<Type>();
      }
   }

   // ---------------------------------------------------------------------------------------------------

   /// <summary>
   /// Represents an algorithm in the syntax tree. Concretely it is either a Macro or Procedure. 
   /// </summary>
   /// <param id="id">The algorithm id.</param>
   /// <param id="formals">The argument list.</param>
   /// <param id="locals">The locals.</param>
   /// <param id="algorithmType">The algorithm type.</param>
   /// <param id="bodyType">The type of body.</param>
   /// <param id="section">The containing section.</param>
   internal abstract class Algorithm : NamedElement, IProvidedElement, ICDL2Object {
     // public readonly Section section = section;
      public readonly RW algorithmType;            // one of FUNCTION, ACTION, TEST or PREDICATE (reservedWordValue will never be null)
      public readonly TT bodyType;           // one of : or := (for CODE only) and = or =: (for MACRO only)
      public readonly List<Affix> formals;
      public readonly Set<Local> locals;

      public Algorithm(ID id,List<Affix> formals,Set<Local> locals,Token algorithmType,TT bodyType,Section section) : base(id) {
         this.formals = formals;
         this.locals = locals;
         this.algorithmType = algorithmType.reservedWordValue ?? RW.FUNCTION;
         this.bodyType = bodyType;
         this.Parent = section;
      }

      public AlgorithmNameType NameType {
         get {
            AlgorithmNameType ait = AlgorithmNameType.None;
            if (algorithmType == RW.TEST || algorithmType == RW.PREDICATE) ait |= AlgorithmNameType.CanFail;
            if (bodyType == TT.MACROBODY || bodyType == TT.MACROPROCBODY) ait |= AlgorithmNameType.Macro;

            return ait;
         }
      }

      public bool TryGetAffix(ID id,out Affix affix) => (affix = formals.FirstOrDefault(affix => affix.id == id,Affix.Default)) != Affix.Default;
      public bool TryGetLocal(ID id,out Local local) => (local = locals.FirstOrDefault(local => local.id == id,Local.Default)) != Local.Default;

      /// <summary>
      /// Get the annotation symbols for the ID of this algorithm. Computed on first use.
      /// Note that the failure conditions should have been ruled out by the semantic analyzer.
      /// TODO: The above will be true for a full run, but not necessarily for a lab like environment
      /// </summary>
      public SA NameAnnotation {
         get {
            SA getSA() {
               Section section = Parent as Section;
               Debug.Assert(section != null);

               if (section.inv.Contains(id)) { // More complicated then local. Need to find the section the algorithm is abstracted or extended from
                                               // Examine siblings to find the section the algorithm is extended from.
                  Layer currentLayer = section.Parent as Layer;
                  Debug.Assert(currentLayer != null);
                  foreach (Section sibling in currentLayer.Children.Where(sec => sec != section).Cast<Section>()) {
                     if (sibling.ext.Contains(id)) {
                        if (sibling.import.Contains(id)) return new SA(Prefix1: AS.Ext,Prefix2: AS.ImportExport);
                        return new SA(Prefix1: AS.Ext);
                     }
                  }
                  // If still here, examine the layer below if any.
                  List<Container> moduleLayers = currentLayer.Parent?.Children;
                  Debug.Assert(moduleLayers != null);
                  if (moduleLayers.Count > 1) {
                     int currentLayerPosition = moduleLayers.IndexOf(currentLayer);
                     if (currentLayerPosition > 0) {
                        Container layerBelow = moduleLayers[currentLayerPosition - 1];
                        foreach (Section ancestor in layerBelow.Children.Cast<Section>()) {
                           if (ancestor.abstr.Contains(id)) {
                              if (ancestor.import.Contains(id)) return new SA(Prefix1: AS.Abstr,Prefix2: AS.ImportExport);
                              return new SA(Prefix1: AS.Abstr);
                           }
                        }
                     }
                     return new SA(Prefix1:AS.Inv);   // Only possible in a partially analyzed context.
                  } else { // local
                     bool exported = section.export.Contains(id);
                     bool imported = section.import.Contains(id);
                     bool abstr = section.abstr.Contains(id);
                     bool ext = section.ext.Contains(id);
                     if (imported) {
                        if (abstr && ext) return new SA(Prefix1: AS.ImportExport,Suffix1: AS.AbstrExt);
                        if (abstr) return new SA(Prefix1: AS.ImportExport,Suffix1: AS.Abstr);
                        if (ext) return new SA(Prefix1: AS.ImportExport,Suffix1: AS.Ext);
                     } else if (exported) {
                        if (abstr && ext) return new SA(Suffix1: AS.ImportExport,Suffix2: AS.AbstrExt);
                        if (abstr) return new SA(Suffix1: AS.ImportExport,Suffix2: AS.Abstr);
                        if (ext) return new SA(Suffix1: AS.ImportExport,Suffix2: AS.Ext);
                     } else {
                        if (abstr && ext) return new SA(Suffix1: AS.AbstrExt);
                        if (abstr) return new SA(Suffix1: AS.Abstr);
                        if (ext) return new SA(Suffix1: AS.Ext);
                     }
                  }
               }
               return new SA();  // Should be impossible to get here.
            }
            return sa ??= getSA();
         }
      }
      private SA? sa = null;
      /// <summary>
      /// Used to force the re-computation of the name annotations.
      /// TODO: figure out when to re-compute name annotations.
      /// </summary>
      public void ResetNameAnnotations() => sa = null;

      override protected string ItemTypeShortName => $"{algorithmType}";
   }

   /// <summary>
   /// An imported algorithm is a reference to an algorithm in another module. Thus it has only a header and no body.
   /// </summary>
   internal class ImportedAlgorithm : Algorithm {
      public ImportedAlgorithm(ID id,List<Affix> formals,Token algType,Section section) : base(id,formals,[],algType,TT.NOBODY,section) => Parent = section;
   }

   /// <summary>
   /// Represents a macro in the syntax tree.
   /// </summary>
   /// <param id="id"></param>
   /// <param id="formals"></param>
   /// <param id="locals"></param>
   /// <param id="algorithmType"></param>
   /// <param id="bodyType"></param>
   /// <param id="section"></param>
   internal class Macro(ID id,List<Affix> formals,Set<Local> locals,Token algType,TT bodyType,Section section) : Algorithm(id,formals,locals,algType,bodyType,section) {
      public List<IMacroElement> elements = [];
   }
   /// <summary>
   /// Represents a code in the syntax tree.
   /// </summary>
   /// <param id="id"></param>
   /// <param id="formals"></param>
   /// <param id="locals"></param>
   /// <param id="algorithmType"></param>
   /// <param id="bodyType"></param>
   /// <param id="section"></param>
   internal class Procedure(ID id,List<Affix> formals,Set<Local> locals,Token algType,TT bodyType,Section section) : Algorithm(id,formals,locals,algType,bodyType,section) {
      public List<Alternative> alternatives = [];
      public Procedure(RW ludeType,Section section) : this(ID.From(section,ludeType),[],[],Token.ACTIONToken,TT.CODEBODY,section) { } // Used for section Ludes which are parameterless actions with no locals.
   }

   internal class Call(ID id)  {
      public readonly ID id = id;
      public readonly List<IActualArg> args = [];
      override public string ToString() => $"{id.name}+{string.Join("+",args)}";
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
      public override string ToString() {
         switch (type) {
            case LCT.Standard: return call?.ToString() ?? "";
            case LCT.Succeed: return "+";
            case LCT.Fail: return "-";
            case LCT.Abort: return "?";
            case LCT.Repeat: return $"*{(label is null || label == ID.AnonID ? "" : label.name)}";
            case LCT.Group: return group?.ToString() ?? "";
            default: return "ERROR";
         }
      }
   }
   internal class Alternative(List<Call> calls,LastCall lastCall) {
      public readonly List<Call> calls = calls;
      public readonly LastCall lastCall = lastCall;
   }
   // Note that the id in this case is the label.
   internal class Group(ID label,List<Alternative> alternatives) : NamedElement(label) {
      public readonly List<Alternative> alternatives = alternatives;
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
      override public string ToString() => $"\"{value}\"";
   }
   internal class LIST : NamedElement, IMacroElement, IConstElement, ,ICDL2Object {
      // Stored as tokens to allow for the possibility of a const reference or an integer. If a const reference, the reference will be resolved during the semantic analysis.
      public readonly Token? lwb;
      public readonly Token? upb;

      public LIST(ID id,Token lwb,Token upb) : base(id) {
         this.lwb = lwb;
         this.upb = upb;
      }

      public LIST(ID id) : base(id) { }
      // override public string ToString() => $"LIST {id.id}({lwb.StringValue}:{upb.StringValue})";
   }
   internal class Var(ID id) : NamedElement(id), IMacroElement, ICDL2Object {
      override public string ToString() => $"VAR {id.name}";
   }
   internal class Const(ID id) : NamedElement(id), IConstElement, IMacroElement, IProvidedElement, ICDL2Object {
      public readonly List<IConstElement> elements = [];  // Will contain ids (const, var, list) and strings, integers, floats
      // override public string ToString() => $"CONST {id.id}={string.Join("",elements)}";
   }

   /// <summary>
   /// Represents a formal argument in an algorithm.
   /// It is just an ID with annotations. An arg is considered to be equal to another arg or ID if the names are the same.
   /// </summary>
   /// <param id="id"></param>
   /// <param id="dir"></param>
   /// <param id="type"></param>
   internal class Affix(ID id,AffixDir dir,AffixType type) : NamedElement(id), IMacroElement {
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

   internal class Local : NamedElement, IMacroElement {
      internal static readonly Local Default = new(ID.AnonID);
      public Local(ID id) : base(id) { }
      override public string ToString() => $"-{id.name}";
   }

   internal class Undeclared(ID id) : NamedElement(id) {}

}
