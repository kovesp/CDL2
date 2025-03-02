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
   /// <param name="id"></param>
   internal class NamedElement(ID id) {
      public readonly ID name = id;

      override public string ToString() => $"{ItemTypeShortName} {name.token.tokenString}";
      public string AsName(string replacement="") => name.AsIdentifier(replacement);
      protected virtual string ItemTypeShortName => GetType().Name.ToUpper()[..3];

      public Container? Parent;      // null for the Program and Modules.
   }

   // Marker interfaces to allow lists to be composed of permissable elements.
   internal interface IMacroElement { }
   internal interface IConstElement { }
   internal interface IInterfaceElement { }
   internal interface IProvidedElement : IInterfaceElement { }
   internal interface IRequiredElement : IInterfaceElement { }
   internal interface IActualArg { }

   /// <summary>
   /// Base class for all elements that can contain other elements, i.e., the program and modules, layers, sections.
   /// </summary>
   /// <param name="id"></param>
   internal abstract class Container(ID id) : NamedElement(id) {
      public readonly SymbolTable Symbols = new SymbolTable();
      public Container(ID id,Container? parent) : this(id) {
         this.Parent = parent;
         this.Parent?.Children.Add(this);
         Symbols.Owner = this;
      }

      /// <summary>
      /// The Children of the container. Layers are ordered, hence the list.
      /// </summary>
      public List<Container> Children = [];

      // The Ludes are stored in a dictionary with the reserved word as the key. The values are lists of IDs.
      // Section Ludes will be generated as Procedure items and given the name of the lude type (which are not legal as a CDL2 name).
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
      /// The short name of the container with its type. Used in the ToString method.
      /// </summary>
      public string ContainerName => $"{Parent?.ContainerName ?? ""} {ItemTypeShortName} {name.token.tokenString}";
   }

   /// <summary>
   /// Represents a program in the syntax tree.
   /// </summary>
   internal class Program : Container {
      override protected string ItemTypeShortName => "PROG";

      /// <summary>
      /// Program Ludes are a list of module IDs.
      /// </summary>
      /// <param name="id"></param>
      public Program(ID id) : base(id,null) => ParseLude = Parser.ParseLudeOfIDs;

      /// <summary>
      /// Get the modules that have the given lude type.
      /// </summary>
      /// <param name="ludeType"></param>
      /// <returns>A collection of modules that are in the lude of the given type.</returns>
      public IEnumerable<Module> Lude(RW ludeType) => this.Ludes[ludeType].Select(id => (Module)Symbols[id]);
   }

   /// <summary>
   /// Represents a module in the syntax tree.
   /// </summary>
   /// <param name="id"></param>
   internal class Module : Container {
      public readonly Set<ID> import = [];         // Imports are specified in sections, but are propagated up the module level.
      public readonly Set <ID> export = [];        // Exports are specified in sections, but are propagated up the module level.

      /// <summary>
      /// Moduel Ludes are a list of section IDs.
      /// </summary>
      /// <param name="id"></param>
      public Module(ID id) : base(id) => ParseLude = Parser.ParseLudeOfIDs;
   }

   /// <summary>
   /// Represents a layer in the syntax tree.
   /// Notice that layers don't have Ludes.
   /// </summary>
   /// <param name="id"></param>
   /// <param name="module"></param>
   internal class Layer(ID id,Module module) : Container(id,module) {  }

   /// <summary>
   /// Represents a section in the syntax tree.
   /// </summary>
   /// <param name="id"></param>
   /// <param name="layer"></param>
   internal class Section : Container {
      /// <summary>
      /// The interfaces.
      /// </summary>
      public readonly Set<ID> ext = [];
      public readonly Set<ID> abstr = [];
      public readonly Set<ID> inv = [];
      public readonly Set<ID> export = [];
      public readonly Set<ID> import = [];      

      // These sets contain the names of the elements in the section. The actual elements are in the symbol table.
      public readonly Set<ID> routines = [];  // Both code and macros
      public readonly Set<ID> lists = [];
      public readonly Set<ID> vars = [];
      public readonly Set<ID> consts = [];

      /// <summary>
      /// Sections have Ludes each of which contains the ID of an internally generated CODE FUNCTION or ACTION which consist of a single alternative.
      /// TODO: Ensure that the generated CODE is correctly typed and that only ACTIONs and/or FUNCTIONs are called.
      /// </summary>
      /// <param name="id"></param>
      /// <param name="layer"></param>
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
   /// <param name="id">The algorithm name.</param>
   /// <param name="formals">The argument list.</param>
   /// <param name="locals">The locals.</param>
   /// <param name="algType">The algorithm type.</param>
   /// <param name="bodyType">The type of body.</param>
   /// <param name="section">The containing section.</param>
   internal abstract class Algorithm : NamedElement, IProvidedElement {
     // public readonly Section section = section;
      public readonly RW algType;            // one of FUNCTION, ACTION, TEST or PREDICATE (rval will never be null)
      public readonly TT bodyType;           // one of : or := (for CODE only) and = or =: (for MACRO only)
      public readonly List<Affix> formals;
      public readonly Set<Local> locals;

      public Algorithm(ID id,List<Affix> formals,Set<Local> locals,Token algType,TT bodyType,Section section) : base(id) {
         this.formals = formals;
         this.locals = locals;
         this.algType = algType.rval ?? RW.FUNCTION;
         this.bodyType = bodyType;
         this.Parent = section;
      }

      public bool TryGetAffix(ID id,out Affix affix) => (affix = formals.FirstOrDefault(affix => affix.name == id,Affix.Default)) != Affix.Default;
      public bool TryGetLocal(ID id,out Local local) => (local = locals.FirstOrDefault(local => local.name == id,Local.Default)) != Local.Default;


      override protected string ItemTypeShortName => $"{algType}";
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
   /// <param name="id"></param>
   /// <param name="formals"></param>
   /// <param name="locals"></param>
   /// <param name="algType"></param>
   /// <param name="bodyType"></param>
   /// <param name="section"></param>
   internal class Macro(ID id,List<Affix> formals,Set<Local> locals,Token algType,TT bodyType,Section section) : Algorithm(id,formals,locals,algType,bodyType,section) {
      public List<IMacroElement> elements = [];
   }
   /// <summary>
   /// Represents a code in the syntax tree.
   /// </summary>
   /// <param name="id"></param>
   /// <param name="formals"></param>
   /// <param name="locals"></param>
   /// <param name="algType"></param>
   /// <param name="bodyType"></param>
   /// <param name="section"></param>
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
   /// <param name="type"></param>   
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
   // Note that the name in this case is the label.
   internal class Group(ID label,List<Alternative> alternatives) : NamedElement(label) {
      public readonly List<Alternative> alternatives = alternatives;
   }

   internal class INT : IConstElement, IMacroElement {
      public readonly long value;
      public INT(Token intToken) {
         Debug.Assert(intToken.type == TT.INT && intToken.ival != null);
         value = (long)intToken.ival;
      }
      override public string ToString() => value.ToString();
   }
   internal class FLOAT : IConstElement, IMacroElement {
      public readonly double value;
      public FLOAT(Token floatToken) {
         Debug.Assert(floatToken.type == TT.FLOAT && floatToken.fval != null);
         value = (double)floatToken.fval;
      }
      override public string ToString() => value.ToString();
   }
   internal class STRING : IMacroElement, IConstElement, IActualArg {
      public readonly string value;
      public STRING(Token str) {
         Debug.Assert(str.type == TT.STRING && str.sval != null);
         value = str.sval;
      }
      override public string ToString() => $"\"{value}\"";
   }
   internal class LIST : NamedElement, IMacroElement, IConstElement {
      // Stored as tokens to allow for the possibility of a const reference or an intgeger. If a const reference, the reference will be resolved during the semantic analysis.
      public readonly Token? lwb;
      public readonly Token? upb;

      public LIST(ID id,Token lwb,Token upb) : base(id) {
         this.lwb = lwb;
         this.upb = upb;
      }

      public LIST(ID id) : base(id) { }
      // override public string ToString() => $"LIST {name.name}({lwb.sval}:{upb.sval})";
   }
   internal class Var(ID id) : NamedElement(id), IMacroElement {
      override public string ToString() => $"VAR {name.name}";
   }
   internal class Const(ID id) : NamedElement(id), IConstElement, IMacroElement, IProvidedElement {
      public readonly List<IConstElement> elements = [];  // Will contain ids (const, var, list) and strings, ints, floats
      // override public string ToString() => $"CONST {name.name}={string.Join("",elements)}";
   }

   /// <summary>
   /// Represents a formal argument in an algirithm.
   /// It is just an ID with anotations. An arg is considered to be euqal to another arg or ID if the names are the same.
   /// </summary>
   /// <param name="id"></param>
   /// <param name="dir"></param>
   /// <param name="type"></param>
   internal class Affix(ID id,AffixDir dir,AffixType type) : NamedElement(id), IMacroElement {
      internal static readonly Affix Default = new (ID.AnonID,AffixDir.NONE,AffixType.std);
      public readonly AffixDir paramDir = dir;
      public readonly AffixType paramType = type;

      public Boolean IsInput => paramDir == AffixDir.input || paramDir == AffixDir.transput;
      public Boolean IsOutput => paramDir == AffixDir.output || paramDir == AffixDir.transput;

      public override bool Equals(object? obj) => obj is Affix param && EqualityComparer<ID>.Default.Equals(name,param.name);
      public override int GetHashCode() => HashCode.Combine(name);

      override public string ToString() => paramType == AffixType.std ? $"+{(IsInput ? ">" : "")}{name}{(IsOutput ? ">" : "")}" : $"*{name}";

      public static bool operator ==(Affix? left,Affix? right) => EqualityComparer<Affix>.Default.Equals(left,right);
      public static bool operator !=(Affix? left,Affix? right) => !(left == right);
   }

   internal class Local : NamedElement, IMacroElement {
      internal static readonly Local Default = new(ID.AnonID);
      public Local(ID id) : base(id) { }
      override public string ToString() => $"-{name.name}";
   }

   internal class Undeclared(ID id) : NamedElement(id) {}

}
