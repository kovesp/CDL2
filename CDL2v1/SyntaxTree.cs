using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal class NamedElement {
      public readonly ID name;
      public NamedElement(Token id) {
         Debug.Assert(id.type == Token.TokenType.ID && id.sval != null,"Program constructor: id not TokenType.ID or sval is null");
         name = new ID(id);
      }
   }
   // Marker interfaces to allow lists to be composed of permissable elements.
   public interface MacroElement { }
   public interface ConstElement { }
   public interface InterfaceElement { }
   public interface ProvidedElement : InterfaceElement { }
   public interface RequiredElement : InterfaceElement { }

   internal class Module(Token id) : NamedElement(id) {
      public readonly List<Layer> layers = [];
      public readonly HashSet<ID> import = [];        // Imports are specified in sections, but are propagated up the module level.

      public override string ToString() => $"MODULE {name.name}";
   }
   internal class Layer : NamedElement {
      public readonly Module module;
      public readonly HashSet<Section> sections = [];
      public Layer(Token id,Module module) : base(id) {
         this.module = module;
         module.layers.Add(this);
      }
      public override string ToString() => $"LAYER {name.name}";
   }
   internal class Section : NamedElement {
      public readonly Layer layer;
      public readonly HashSet<ID> ext = [];
      public readonly HashSet<ID> abstr = [];
      public readonly HashSet<ID> inv = [];
      public readonly HashSet<ID> export = [];
      public readonly HashSet<ID> import = [];

      // These sets contain the names of the elements in the section. The actual elements are in the symbol table.
      public readonly HashSet<ID> routines = [];  // Both code and macros
      public readonly HashSet<ID> lists = [];
      public readonly HashSet<ID> vars = [];
      public readonly HashSet<ID> consts = [];

      public readonly List<ID> prelude = [];
      public readonly List<ID> root = [];
      public readonly List<ID> postlude = [];

      public Section(Token id,Layer layer) : base(id) {
         this.layer = layer;
         layer.sections.Add(this);
      }
      public override string ToString() => $"SECTION {name.name}";
   }
   internal class Proc(Token id,List<Arg> args,List<ID> locals,Token procType,Token.TokenType bodyType,Section section) : NamedElement(id), ProvidedElement {
      public readonly Section section = section;
      public readonly Token.ReservedWord procType = procType.rval??Token.ReservedWord.FUNCTION;   // one of FUNCTION, ACTION, TEST or PREDICATE (rval will never be null)
      public readonly Token.TokenType bodyType = bodyType;      // one of : or := (for CODE only) and = or =: (for MACRO only)
      public readonly List<Arg> args = args;
      public readonly List<ID> locals = locals;
   }
   internal class Macro(Token id,List<Arg> args,List<ID> locals,Token procType,Token.TokenType bodyType,Section section) : Proc(id,args,locals,procType,bodyType,section) {
      List<MacroElement> elements = [];
   }
   internal class Code(Token id,List<Arg> args,List<ID> locals,Token procType,Token.TokenType bodyType,Section section) : Proc(id,args,locals,procType,bodyType,section) {
      List<Alternative> alternatives = [];
   }

   // The last element (in an alternative) can be:
   //    Standard - a normal procedure call which is the last item in the alternatives' procs list.
   //    Success, Fail, Abort - i.e., +, -, or ?.
   //    Repeat - * with a reference to the group that is repeated possibly using the label
   //    Group - a nested group.
   internal class LastCall {
      public enum CallType { Standard, Success, Fail, Abort, Repeat, Group }
      public readonly CallType type;
      public readonly Group? group;
      public LastCall(CallType type,Group? group) {
         this.type = type;
         this.group = group;
      }
      public LastCall(CallType type) : this(type,null) { }
   }
   internal class Alternative {
      public readonly List<Proc> procs = new();
      public readonly LastCall lastCall;

      public Alternative(LastCall lastCall) => this.lastCall = lastCall;
   }
   // Note that the name in this case is the label.
   internal class Group(Token id) : NamedElement(id) {
      public readonly List<Alternative> alternatives = new();
   }

   internal class INT : ConstElement {
      public readonly long value;
      public INT(Token intToken) {
         Debug.Assert(intToken.type == Token.TokenType.INT && intToken.ival != null);
         value = (long)intToken.ival;
      }
   }
   internal class FLOAT : ConstElement {
      public readonly double value;
      public FLOAT(Token floatToken) {
         Debug.Assert(floatToken.type == Token.TokenType.FLOAT && floatToken.fval != null);
         value = (double)floatToken.fval;
      }
   }
   internal class STRING : MacroElement, ConstElement {
      public readonly string value;
      public STRING(Token str) {
         Debug.Assert(str.type == Token.TokenType.STRING && str.sval != null);
         value = str.sval;
      }
   }
   internal class LIST(Token id,Token lwb,Token upb) : NamedElement(id), MacroElement, ConstElement, ProvidedElement {
      // Stored as tokens to allow for the possibility of a const reference or an intgeger. If a const reference, the reference will be resolved during the semantic analysis.
      public readonly Token lwb = lwb;
      public readonly Token upb = upb;
   }
   internal class Var(Token id) : NamedElement(id), MacroElement, ProvidedElement { }
   internal class Const(Token id) : NamedElement(id), MacroElement, ConstElement, ProvidedElement {
      public readonly List<ConstElement> elements = [];  // Will contain ids (const, var, list) and strings, ints, floats
   }

   internal class Arg(Token id,Arg.ArgDir dir,Arg.ArgType type) : NamedElement(id), MacroElement {
      public enum ArgDir { input, output, transput }
      public enum ArgType { std, str }

      public readonly ArgDir argDir = dir;
      public readonly ArgType argType = type;
   }

   internal class Local : NamedElement, MacroElement {
      public Local(Token id) : base(id) { }
   }

   internal class ID : ConstElement {
      public readonly string name;
      public readonly Token token;
      public ID(Token id) {
         Debug.Assert(id.type == Token.TokenType.ID && id.sval != null,"Program constructor: id not TokenType.ID or sval is null");
         token = id;
         name = id.sval;
      }

      public override bool Equals(object? obj) => obj is ID iD && name == iD.name;
      public override int GetHashCode() => HashCode.Combine(name);
      public override string ToString() => name;
   }

   internal class Undeclared(Token id) : NamedElement(id) {}

   internal class Program : NamedElement {
      public List<Module> parts = new();
      public List<Proc> prelude = new();
      public List<Proc> root = new();
      public List<Proc> postlude = new();

      public Program(Token id) : base(id) { }
   }
}
