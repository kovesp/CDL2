using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   /// <summary>
   /// Formatted printing of the parse tree.
   /// </summary>
   internal class PrettyPrinter {
      private const int DEFAULT_WIDTH = 100;
      private const int DEFAULT_INDENT = 3;

      private readonly int LineWidth = DEFAULT_WIDTH;        // TODO: Implement line wrapping in PrettyPrinter.
      private readonly int IndentWidth = DEFAULT_INDENT;
      private readonly CodeEmitterBase emitter;

      /// <summary>
      /// Construct a pretty printer with a maximum line length and an indentation width using the specified emitter.
      /// </summary>
      /// <param name="width"></param>
      /// <param name="indent"></param>
      /// <param name="emitter"></param>
      /// <example>
      ///   Construct a pretty printer that outputs to a file.
      ///   
      ///   PrettyPrinter pp = new PrettyPrinter(100,3,new FileCodeEmitter("output.txt"));
      ///   or simpler
      ///    PrettyPrinter pp = new("output.txt");
      /// </example>
      public PrettyPrinter(int width,int indent,CodeEmitterBase emitter) {
         this.LineWidth = width;
         this.IndentWidth = indent;
         this.emitter = emitter;
         emitter.IndentWidth = this.IndentWidth;
         emitter.LineWidth = this.LineWidth;
      }

      /// <summary>
      /// Construct a pretty printer with a default maximum line length of <see cref="DEFAULT_WIDTH"/> and an indentation width of <see cref="DEFAULT_INDENT"/> using the specified emitter.
      /// </summary>
      /// <param name="emitter"></param>
      public PrettyPrinter(CodeEmitterBase emitter) : this (DEFAULT_WIDTH,DEFAULT_INDENT, emitter) { }
      /// <summary>
      /// Construct a pretty printer with a default maximum line length of <see cref="DEFAULT_WIDTH"/> and an indentation width of <see cref="DEFAULT_INDENT"/> using the specified file name.
      /// </summary>
      /// <param name="fileName">If this is null, use the <see cref="DebugCodeEmitter"/> instead.</param>
      public PrettyPrinter(string? fileName) : this(DEFAULT_WIDTH,DEFAULT_INDENT,fileName.IsValidFileName() ? new FileCodeEmitter(fileName ?? "") : new DebugCodeEmitter()) { }

      private static readonly Dictionary<Type,(RW, RW)> units = new() {
         { typeof(Program),(RW.PROGRAM, RW.ENDPROG)},
         { typeof(Module),(RW.MODULE, RW.ENDMOD)},
         { typeof(Layer),(RW.LAYER, RW.ENDLAY)},
         { typeof(Section),(RW.SECTION, RW.ENDSEC)},
      };


      public void Print(Program program,Set<Module> modules) {
         Print(program);
         Print(modules);
      }

      public void Print(Program program) {
         PrintUnitStart(program);
         PrintList(RW.PART,program.children.Select(part=>part.name));
         PrintLudes(program);
         PrintUnitEnd(program);
      }

      private void PrintLudes(Container container) {
         PrintLude(RW.PRELUDE,container);
         PrintLude(RW.ROOT,container);
         PrintLude(RW.POSTLUDE,container);
      }

      private void PrintLude(RW ludeType,Container container) {
         if (container is Section) {
            if (container.ludes[ludeType].Any()) {
               emitter.Emit(ludeType," ");
               // Section ludes are stored as ids of a generated Code item.
               if (container.Symbols[container.ludes[ludeType].First()] is Code code) { // This should always be the case
                  Print(code.alternatives.First());
                  emitter.Emitnl(TT.END);
               } else {
                  Logger.ReportError($"Internal error: {ludeType} lude is not a Code item.");
               }
            }
         } else { 
            PrintList(ludeType,container.ludes[ludeType]);
         }
      }


      private void Print(Alternative alternative) => Print(alternative,0);
      private void Print(Alternative alternative,int indentLevel) {
         foreach (Call call in alternative.calls.Skip(1)) {
            emitter.Emit(", ");
            Print(call);
         }
         switch (alternative.lastCall.type) {
            case LastCallType.Standard:
               Debug.Assert(alternative.lastCall.call != null);
               Print(alternative.lastCall.call);
               break;
            case LastCallType.Succeed:
               emitter.Emit(TT.SUCCEED);
               break;
            case LastCallType.Fail:
               emitter.Emit(TT.FAIL);
               break;
            case LastCallType.Abort:
               emitter.Emit(TT.ABORT);
               break;
            case LastCallType.Repeat:
               emitter.Emit(TT.REPEAT);
               Debug.Assert(alternative.lastCall.label is not null);
               if (alternative.lastCall.label != TokenList.AnonID) {
                  emitter.Emit(alternative.lastCall.label.token.tokenString);
               }
               break;
            case LastCallType.Group:
               Debug.Assert(alternative.lastCall.label is not null && alternative.lastCall.group is not null);
               Print(alternative.lastCall.label,alternative.lastCall.group);
               break;
         }
      }

      private void Print(ID label,Group group) {
         emitter.Emit(TT.GRPOPEN);
         if (label != TokenList.AnonID) emitter.Emit(label.token.tokenString,TT.LABELSEP);
         Print(group.alternatives);
         emitter.Emit(TT.GRPCLOSE);
      }

      private void Print(Group group) => Print(TokenList.AnonID,group);

      private void Print(List<Alternative> alternatives) {
         Debug.Assert(alternatives.Any());
         Print(alternatives.First());
         foreach (Alternative alternative in alternatives.Skip(1)) {
            emitter.Emitnl(TT.ALTSEP);
            Print(alternative);
         }
      }

      public void Print(Call call) { }

      private void PrintList(RW rw,IEnumerable<ID> ids) {
         if (ids.Any()) {
            emitter.Emit(1,rw," ",ids.First().token.tokenString);
            foreach (ID id in ids.Skip(1)) {
               emitter.Emit(", ");
               emitter.Emit(1,id.token.tokenString);
            }
            emitter.Emitnl(TT.END.TT2String());
         }
      }

      public void Print(Set<Module> modules) {
         foreach (Module module in modules) Print(module);
      }

      public void Print(Module module) {
         PrintUnitStart(module);
         foreach (Layer layer in module.children) Print(layer);
         PrintUnitEnd(module);
         PrintLudes(module);
      }

      public void Print(Layer layer) {
         PrintUnitStart(layer);
         foreach (Section section in layer.children) Print(section);
         PrintUnitEnd(layer);
         PrintLudes(layer);
      }

      public void Print(Section section) {
         PrintUnitStart(section);
         PrintList(RW.EXPORT,section.export);
         PrintList(RW.IMPORT,section.import);
         PrintList(RW.ABSTR,section.abstr);
         PrintList(RW.EXT,section.ext);
         PrintList(RW.INV,section.inv);

         emitter.Emitnl("# CONST definitions #");
         emitter.Emitnl("# VAR definitions #");
         emitter.Emitnl("# LIST definitions #");
         emitter.Emitnl("# MACRO definitions #");
         emitter.Emitnl("# CODE definitions #");

         PrintUnitEnd(section);
         PrintLudes(section);
      }

      public void Print(Proc proc) {
      }

      public void Print(Code code) {
      }

      public void Print(Macro macro) {
      }

      public void Print(Var variable) {
      }

      public void Print(Const expression) {
      }

      public void Print(LIST lIST) {
      }

      private void PrintUnitStart(NamedElement unit) => emitter.Emitnl(units[unit.GetType()].Item1.ToString()," ",unit.name.token.tokenString,TT.END.TT2String());
      private void PrintUnitEnd(NamedElement unit)   => emitter.Emitnl(units[unit.GetType()].Item2.ToString()," ",unit.name.token.tokenString,TT.END.TT2String());

   }
}
