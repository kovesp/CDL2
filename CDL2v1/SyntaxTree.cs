using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
   internal class Module : NamedElement {
      public readonly List<Layer> layers = new();
      public Module(Token id) : base(id) { }
      public override string ToString() => $"MODULE {name.name}";
   }
   internal class Layer : NamedElement {
      public readonly Module module;
      public readonly List<Section> sections = new();
      public Layer(Token id,Module module) : base(id) {
         this.module = module;
      }
      public override string ToString() => $"LAYER {name.name}";
   }
   internal class Section : NamedElement {
      public readonly Layer layer;
      public readonly List<ID> ext = new();
      public readonly List<ID> abstr = new();
      public readonly List<ID> inv = new();
      public readonly List<ID> export = new();
      public readonly List<ID> import = new();

      public readonly List<Proc> routines = new();  // Both code and macros
      public readonly List<LIST> lists = new();
      public readonly List<Var> vars = new();
      public readonly List<Const> consts = new();

      public readonly List<ID> prelude = new();
      public readonly List<ID> root = new();
      public readonly List<ID> postlude = new();

      public Section(Token id,Layer layer) : base(id) {
         this.layer = layer;
      }
      public override string ToString() => $"SECTION {name.name}";
   }
   internal class Proc : NamedElement {
      public readonly Section section;
      public enum ProcType { TEST, PREDICATE, FUNCTION, ACTION }
      public readonly ProcType type;
      public readonly List<Arg> args = new();
      public readonly List<ID> locals = new();
      public Proc(Token id,ProcType type,Section section) : base(id) {
         this.type = type;
         this.section = section;
      }
   }
   internal class Macro : Proc {
      public readonly List<MacroElement> elements = new();
      public Macro(Token id,ProcType type,Section section) : base(id,type,section) { }
   }
   internal class Code : Proc {
      public readonly List<Alternative> alternatives = new();
      public Code(Token id,ProcType type,Section section) : base(id,type,section) { }
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
   internal class Group : NamedElement {
      public readonly List<Alternative> alternatives = new();
      public Group(Token id) : base(id) {
      }
   }

   // Marker interfaces to allow lists to be composed of permissable elements.
   public interface MacroElement { }
   public interface ConstElement { }

   internal class INT : ConstElement {
      public readonly long value;
      public INT(long value) => this.value = value;
   }
   internal class FLOAT : ConstElement {
      public readonly double value;
      public FLOAT(double value) => this.value = value;
   }
   internal class STRING : MacroElement, ConstElement {
      public readonly string value;
      public STRING(Token str) {
         Debug.Assert(str.type == Token.TokenType.STRING && str.sval != null,"STRING constructor: str not TokenType.STRING or sval is null");
         value = str.sval;
      }
   }
   internal class LIST : NamedElement, MacroElement, ConstElement {
      public readonly int lwb;
      public readonly int upb;
      public LIST(Token id,int lwb,int upb) : base(id) {
         this.lwb = lwb;
         this.upb = upb;
      }
   }
   internal class Var : NamedElement, MacroElement {
      public Var(Token id) : base(id) { }
   }
   internal class Const : NamedElement, MacroElement, ConstElement {
      public readonly List<ConstElement> elements = new();  // Will contain consts, strings, ints, floats, vars, lists, locals and args
      public Const(Token id) : base(id) { }
   }

   internal class ID {
      public readonly string name;
      public ID(Token id) {
         Debug.Assert(id.type == Token.TokenType.ID && id.sval != null,"Program constructor: id not TokenType.ID or sval is null");
         name = id.sval;
      }

      public override bool Equals(object? obj) => obj is ID iD && name == iD.name;
      public override int GetHashCode() => HashCode.Combine(name);
      public override string ToString() => name;
   }
   internal class Arg : NamedElement, MacroElement {
      public enum ArgDir { input, output, transput }
      public enum ArgType { std, str }

      public readonly ArgDir argDir;
      public readonly ArgType argType;

      public Arg(Token id,ArgDir dir,ArgType type) : base(id) {
         argDir = dir;
         argType = type;
      }
   }
   internal class Local : NamedElement, MacroElement {
      public Local(Token id) : base(id) { }
   }
   internal class Program : NamedElement {
      public List<Module> parts = new();
      public List<ID> prelude = new();
      public List<ID> root = new();
      public List<ID> postlude = new();

      public Program(Token id) : base(id) { }
   }
}
