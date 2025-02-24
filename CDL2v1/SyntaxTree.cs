using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
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
      public string AsName(string replacement="") => name.AsName(replacement);
      protected virtual string ItemTypeShortName => GetType().Name.ToUpper()[..3];

      public Container? parent;        // null for the Program and Modules.
   }

   // Marker interfaces to allow lists to be composed of permissable elements.
   public interface MacroElement { }
   public interface ConstElement { }
   public interface InterfaceElement { }
   public interface ProvidedElement : InterfaceElement { }
   public interface RequiredElement : InterfaceElement { }
   public interface ActualArg { }

   /// <summary>
   /// Base class for all elements that can contain other elements, i.e., the program and modules, layers, sections.
   /// </summary>
   /// <param name="id"></param>
   internal abstract class Container(ID id) : NamedElement(id) {
      public readonly SymbolTable Symbols = [];
      public Container(ID id,Container? parent) : this(id) {
         (this.parent = parent)?.children.Add(this);
         Symbols.parent = this;
      }

      public List<Container> children = [];     // The children of the container. Layers are ordered, hence the list.



      // The ludes are stored in a dictionary with the reserved word as the key. The values are lists of IDs.
      // Section ludes will be generated as Code items and given the name of the lude type (which are not legal as a CDL2 name).
      public readonly Dictionary<RW,List<ID>> ludes = new() {
         { RW.PRELUDE,[] },
         { RW.ROOT,[] },
         { RW.POSTLUDE,[] }
      };
      public Action<Parser,RW,Container> ParseLude = (parser,ludeType,container) => { };

      public string FullName() => parent is null ? $"{ToString()}" : $"{parent.FullName()} {ToString()}";

      public string ContainerName() => this switch {
         Program => $"PROG {name.token.tokenString}",
         Module  => $"MOD  {name.token.tokenString}",
         Layer   => $"MOD  {parent?.name.token.tokenString} LAY {name.token.tokenString}",
         Section => $"MOD  {parent?.parent?.name.token.tokenString} LAY {parent?.name.token.tokenString} SEC {name.token.tokenString}",
         _      => "Container"
      };
   }

   internal class Program : Container {
      override protected string ItemTypeShortName => "PROG";

      public Program(ID id) : base(id,null) => ParseLude = Parser.ParseLudeOfIDs;
   }

   /// <summary>
   /// Represents a module in the syntax tree.
   /// </summary>
   /// <param name="id"></param>
   internal class Module : Container {
      public readonly Set<ID> import = [];         // Imports are specified in sections, but are propagated up the module level.
      public readonly Set <ID> export = [];        // Exports are specified in sections, but are propagated up the module level.

      public Module(ID id) : base(id) => ParseLude = Parser.ParseLudeOfIDs;
   }

   /// <summary>
   /// Represents a layer in the syntax tree.
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

      public Section(ID id,Layer layer) : base(id,layer) => ParseLude = Parser.ParseLudeOfCalls;

      public static Type[] ProvidedElementImplementors;
      static Section() {
         ProvidedElementImplementors = Extensions.GetImplementorsOfInterface<ProvidedElement>().ToArray<Type>();
      }
   }

   // ---------------------------------------------------------------------------------------------------

   /// <summary>
   /// Represents an algorithm in the syntax tree. Concretely it is either a Macro or Code. 
   /// </summary>
   /// <param name="id">The algorithm name.</param>
   /// <param name="formals">The argument list.</param>
   /// <param name="locals">The locals.</param>
   /// <param name="algType">The algorithm type.</param>
   /// <param name="bodyType">The type of body.</param>
   /// <param name="section">The containing section.</param>
   internal class Algorithm(ID id,List<ID> formals,Set<ID> locals,Token algType,TT bodyType,Section section) : NamedElement(id), ProvidedElement {
      public readonly Section section = section;
      public readonly RW algType = algType.rval??RW.FUNCTION;    // one of FUNCTION, ACTION, TEST or PREDICATE (rval will never be null)
      public readonly TT bodyType = bodyType;                      // one of : or := (for CODE only) and = or =: (for MACRO only)
      public readonly List<ID> formals = formals;                  // These will actually be Param-s which are IDs with annotations.
      public readonly Set<ID> locals = locals;

      /// <summary>
      /// Used to declare an imported algorithm.
      /// </summary>
      /// <param name="id"></param>
      /// <param name="formals"></param>
      /// <param name="algType"></param>
      /// <param name="section"></param>
      public Algorithm(ID id,List<ID> formals,Token algType,Section section) : this(id,formals,[],algType,TT.NOBODY,section) { }

      override protected string ItemTypeShortName => $"{algType}";
   }
   internal class Macro(ID id,List<ID> args,Set<ID> locals,Token algType,TT bodyType,Section section) : Algorithm(id,args,locals,algType,bodyType,section) {
      public List<MacroElement> elements = [];
   }
   internal class Code(ID id,List<ID> args,Set<ID> locals,Token algType,TT bodyType,Section section) : Algorithm(id,args,locals,algType,bodyType,section) {
      public List<Alternative> alternatives = [];
      public Code(RW ludeType,Section section) : this(new ID(section,ludeType),[],[],Token.ACTIONToken,TT.CODEBODY,section) { } // Used for section ludes which are parameterless actions with no locals.
   }

   internal class Call(ID id)  {
      public readonly ID id = id;
      public readonly List<ActualArg> args = [];
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
      public readonly ID? label = TokenList.AnonID;

      public LastCall(Call call) : this(LCT.Standard) => this.call = call;
      public LastCall(Group group) : this(LCT.Group) => this.group = group;
      public LastCall(ID? label) : this(LCT.Repeat) => this.label = label;
      public override string ToString() {
         switch (type) {
            case LCT.Standard: return call?.ToString() ?? "";
            case LCT.Succeed: return "+";
            case LCT.Fail: return "-";
            case LCT.Abort: return "?";
            case LCT.Repeat: return $"*{(label is null || label == TokenList.AnonID ? "" : label.name)}";
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

   internal class INT : ConstElement, MacroElement {
      public readonly long value;
      public INT(Token intToken) {
         Debug.Assert(intToken.type == TT.INT && intToken.ival != null);
         value = (long)intToken.ival;
      }
      override public string ToString() => value.ToString();
   }
   internal class FLOAT : ConstElement, MacroElement {
      public readonly double value;
      public FLOAT(Token floatToken) {
         Debug.Assert(floatToken.type == TT.FLOAT && floatToken.fval != null);
         value = (double)floatToken.fval;
      }
      override public string ToString() => value.ToString();
   }
   internal class STRING : MacroElement, ConstElement, ActualArg {
      public readonly string value;
      public STRING(Token str) {
         Debug.Assert(str.type == TT.STRING && str.sval != null);
         value = str.sval;
      }
      override public string ToString() => $"\"{value}\"";
   }
   internal class LIST(ID id,Token lwb,Token upb) : NamedElement(id), MacroElement, ConstElement {
      // Stored as tokens to allow for the possibility of a const reference or an intgeger. If a const reference, the reference will be resolved during the semantic analysis.
      public readonly Token lwb = lwb;
      public readonly Token upb = upb;
      // override public string ToString() => $"LIST {name.name}({lwb.sval}:{upb.sval})";
   }
   internal class Var(ID id) : NamedElement(id), MacroElement {
      override public string ToString() => $"VAR {name.name}";
   }
   internal class Const(ID id) : NamedElement(id), MacroElement, ConstElement, ProvidedElement {
      public readonly List<ConstElement> elements = [];  // Will contain ids (const, var, list) and strings, ints, floats
      // override public string ToString() => $"CONST {name.name}={string.Join("",elements)}";
   }

   /// <summary>
   /// Represents a formal argument in an algirithm.
   /// It is just an ID with anotations. An arg is considered to be euqal to another arg or ID if the names are the same.
   /// </summary>
   /// <param name="id"></param>
   /// <param name="dir"></param>
   /// <param name="type"></param>
   internal class Param(ID id,ParamDir dir,ParamType type) : ID(id), MacroElement {
      public readonly ParamDir paramDir = dir;
      public readonly ParamType paramType = type;

      public Boolean IsInput => paramDir == ParamDir.input || paramDir == ParamDir.transput;
      public Boolean IsOutput => paramDir == ParamDir.output || paramDir == ParamDir.transput;


      override public string ToString() => paramType == ParamType.std ? $"+{(IsInput ? ">" : "")}{name}{(IsOutput ? ">" : "")}" : $"*{name}";
      //public override bool Equals(object? obj) => obj is ID arg && base.Equals(obj) && EqualityComparer<Token>.Default.Equals(token,arg.token);
      //public override int GetHashCode() => HashCode.Combine(base.GetHashCode(),token);

      //public static bool operator ==(Arg? left,Arg? right) => EqualityComparer<Arg>.Default.Equals(left,right);
      //public static bool operator !=(Arg? left,Arg? right) => !(left == right);
   }

   internal class Local : NamedElement, MacroElement {
      public Local(ID id) : base(id) { }
      override public string ToString() => $"-{name.name}";
   }

   /// <summary>
   /// Represents a reference to a named element, Arg or Local in the syntax tree.
   /// It contains the token it was created from.
   /// </summary>
   internal class ID : ConstElement, MacroElement, ActualArg {
      public readonly Token token = Token.ErrorToken;
      public readonly string name = Token.ErrorToken.tokenString;
      public Container? parent = null;

      public ID(Token token) {
         Debug.Assert(token.type == TT.ID && token.sval != null,"Program constructor: id not TokenType.ID or sval is null");
         this.token = token;
         name = token.tokenString;
      }
      public ID(ID id) : this(id.token) { }
      public ID() { }
      public ID(string name) : this(new Token(name)) { }

      public ID(Container container,RW rw) : this($"{container.name.token.tokenString}_{rw}") { }

      public override bool Equals(object? obj) => obj is ID iD && token == iD.token;
      public override int GetHashCode() => HashCode.Combine(token);
      public override string ToString() => token.tokenString;
      public string AsName(string separator="_",string spaceReplacement="") {
         string parentPrefix = parent is null ? "" : $"{parent.AsName(spaceReplacement)}{separator}";
         return $"{parentPrefix}{token.AsName(spaceReplacement)}";
      }
 
      public static bool operator ==(ID left,ID right) => left is null ? right is null : left.Equals(right);
      public static bool operator !=(ID left,ID right) => !(left == right);
   }

   internal class Undeclared(ID id) : NamedElement(id) {}

}
