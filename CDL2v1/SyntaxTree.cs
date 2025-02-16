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
      protected virtual string ItemTypeShortName => GetType().Name.ToUpper()[..3];

      public readonly Container? parent;        // null for the Program and Modules.
   }

   // Marker interfaces to allow lists to be composed of permissable elements.
   public interface MacroElement { }
   public interface ConstElement { }
   public interface InterfaceElement { }
   public interface ProvidedElement : InterfaceElement { }
   public interface RequiredElement : InterfaceElement { }
   public interface ActualArg { }
   public interface LudeElement { }

   /// <summary>
   /// Base class for all elements that can contain other elements, i.e., the program and modules, layers, sections.
   /// </summary>
   /// <param name="id"></param>
   internal abstract class Container(ID id) : NamedElement(id) {
      public Container(ID id,Container? parent) : this(id) => (this.parent = parent)?.children.Add(this); 

        
      public List<Container> children = [];     // The children of the container. Layers are ordered, hence the list.

      public SymbolTable symbolTable = [];      // TODO: The symbol table for the container. Placeholder for now.

      public readonly Dictionary<Token.ReservedWord,List<LudeElement>> ludes = new() {
         { Token.ReservedWord.PRELUDE,[] },
         { Token.ReservedWord.ROOT,[] },
         { Token.ReservedWord.POSTLUDE,[] }
      };
      public Action<Parser,Token.ReservedWord,Container> ParseLude = (parser,ludeType,container) => {};

      public string FullName() => parent is null ? $"{ToString()}" : $"{parent.FullName()} {ToString()}";
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

      public Module(ID id) : base(id,null) => ParseLude = Parser.ParseLudeOfIDs;
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
   }

   // ---------------------------------------------------------------------------------------------------

   /// <summary>
   /// Represents a procedure in the syntax tree. Concretely it is either a Macro or Code. 
   /// </summary>
   /// <param name="id">The procedure name.</param>
   /// <param name="args">The argument list.</param>
   /// <param name="locals">The locals.</param>
   /// <param name="procType">The procedure type.</param>
   /// <param name="bodyType">The type of body.</param>
   /// <param name="section">The containing section.</param>
   internal abstract class Proc(ID id,List<Arg> args,Set<ID> locals,Token procType,Token.TokenType bodyType,Section section) : NamedElement(id), ProvidedElement {
      public readonly Section section = section;
      public readonly Token.ReservedWord procType = procType.rval??Token.ReservedWord.FUNCTION;    // one of FUNCTION, ACTION, TEST or PREDICATE (rval will never be null)
      public readonly Token.TokenType bodyType = bodyType;                                         // one of : or := (for CODE only) and = or =: (for MACRO only)
      public readonly List<Arg> args = args;
      public readonly Set<ID> locals = locals;

      override protected string ItemTypeShortName => $"{procType}";
   }
   internal class Macro(ID id,List<Arg> args,Set<ID> locals,Token procType,Token.TokenType bodyType,Section section) : Proc(id,args,locals,procType,bodyType,section) {
      public List<MacroElement> elements = [];
   }
   internal class Code(ID id,List<Arg> args,Set<ID> locals,Token procType,Token.TokenType bodyType,Section section) : Proc(id,args,locals,procType,bodyType,section) {
      public List<Alternative> alternatives = [];
   }

   internal class Call(ID id) : LudeElement {
      public readonly ID id = id;
      public readonly List<ActualArg> args = [];
      override public string ToString() => $"{id.name}+{string.Join("+",args)}";
   }
   // The last element (in an alternative) can be:
   //    Standard - a normal procedure call which is the last item in the alternatives' procs list.
   //    Success, Fail, Abort - i.e., +, -, or ?.
   //    Repeat - * with a reference to the group that is repeated possibly using the label
   //    Group - a nested group.
   internal class LastCall(LastCall.CallType type) {
      public enum CallType { Standard, Success, Fail, Abort, Repeat, Group }
      public readonly CallType type = type;
      public readonly Group? group;
      public readonly Call? call;
      public readonly ID? label;

      public LastCall(Call call) : this(CallType.Standard) => this.call = call;
      public LastCall(Group group) : this(CallType.Group) => this.group = group;
      public LastCall(ID? label) : this(CallType.Repeat) => this.label = label;
      public override string ToString() {
         switch (type) {
            case CallType.Standard: return call?.ToString() ?? "";
            case CallType.Success: return "+";
            case CallType.Fail: return "-";
            case CallType.Abort: return "?";
            case CallType.Repeat: return $"*{(label is null || label == TokenList.AnonID ? "" : label.name)}";
            case CallType.Group: return group?.ToString() ?? "";
            default: return "ERROR";
         }
      }
   }
   internal class Alternative(List<Call> calls,LastCall lastCall) {
      public readonly List<Call> calls = calls;
      public readonly LastCall lastCall = lastCall;
   }
   // Note that the name in this case is the label.
   internal class Group(ID id,List<Alternative> alternatives) : NamedElement(id) {
      public readonly List<Alternative> alternatives = alternatives;
   }

   internal class INT : ConstElement, MacroElement {
      public readonly long value;
      public INT(Token intToken) {
         Debug.Assert(intToken.type == Token.TokenType.INT && intToken.ival != null);
         value = (long)intToken.ival;
      }
      override public string ToString() => value.ToString();
   }
   internal class FLOAT : ConstElement, MacroElement {
      public readonly double value;
      public FLOAT(Token floatToken) {
         Debug.Assert(floatToken.type == Token.TokenType.FLOAT && floatToken.fval != null);
         value = (double)floatToken.fval;
      }
      override public string ToString() => value.ToString();
   }
   internal class STRING : MacroElement, ConstElement, ActualArg {
      public readonly string value;
      public STRING(Token str) {
         Debug.Assert(str.type == Token.TokenType.STRING && str.sval != null);
         value = str.sval;
      }
      override public string ToString() => $"\"{value}\"";
   }
   internal class LIST(ID id,Token lwb,Token upb) : NamedElement(id), MacroElement, ConstElement, ProvidedElement {
      // Stored as tokens to allow for the possibility of a const reference or an intgeger. If a const reference, the reference will be resolved during the semantic analysis.
      public readonly Token lwb = lwb;
      public readonly Token upb = upb;
      // override public string ToString() => $"LIST {name.name}({lwb.sval}:{upb.sval})";
   }
   internal class Var(ID id) : NamedElement(id), MacroElement, ProvidedElement {
      override public string ToString() => $"VAR {name.name}";
   }
   internal class Const(ID id) : NamedElement(id), MacroElement, ConstElement, ProvidedElement {
      public readonly List<ConstElement> elements = [];  // Will contain ids (const, var, list) and strings, ints, floats
      // override public string ToString() => $"CONST {name.name}={string.Join("",elements)}";
   }

   internal class Arg(ID id,Arg.ArgDir dir,Arg.ArgType type) : NamedElement(id), MacroElement {
      public enum ArgDir { input, output, transput, NONE }
      public enum ArgType { std, str }

      public readonly ArgDir argDir = dir;
      public readonly ArgType argType = type;

      public Boolean IsInput => argDir == ArgDir.input || argDir == ArgDir.transput;
      public Boolean IsOutput => argDir == ArgDir.output || argDir == ArgDir.transput;


      override public string ToString() => argType == ArgType.std ? $"+{(IsInput ? ">" : "")}{name}{(IsOutput ? ">" : "")}" : $"*{name.name}";
   }

   internal class Local : NamedElement, MacroElement {
      public Local(ID id) : base(id) { }
      override public string ToString() => $"-{name.name}";
   }

   internal class ID : ConstElement, MacroElement, ActualArg, LudeElement {
      public readonly string name = "ERROR";
      public readonly Token token = Token.ErrorToken;
      public ID(Token token) {
         Debug.Assert(token.type == Token.TokenType.ID && token.sval != null,"Program constructor: id not TokenType.ID or sval is null");
         this.token = token;
         name = token.tokenString;
      }
      public ID() { }
      public ID(string name) => this.name = name;

      public override bool Equals(object? obj) => obj is ID iD && name == iD.name;
      public override int GetHashCode() => HashCode.Combine(name);
      public override string ToString() => name;
      public static bool operator ==(ID left,ID right) => left is null ? right is null : left.Equals(right);
      public static bool operator !=(ID left,ID right) => !(left == right);
   }

   internal class Undeclared(ID id) : NamedElement(id) {}

}
