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
      private const int DEFAULT_LINE_LENGTH          = 100;
      private const int DEFAULT_INDENT_MULTIPLIER    = 3;
      private const int DEFAULT_MAX_INDENT_INCREMENT = 3;

      private int LineLength { get; set; }              = DEFAULT_LINE_LENGTH;              // Line length for wrapping        
      private int IndentMultiplier { get; set; }        = DEFAULT_INDENT_MULTIPLIER;                   // The indent multiplier
      private int MaxIndentIncrement { get; set; }      = DEFAULT_MAX_INDENT_INCREMENT;     // The maximum number of times the indent can be incremented for wrapping.

      private readonly CodeEmitterBase emitter;

      private int indentLevel = 0;

      /// <summary>
      /// Construct a pretty printer with a maximum line length and an indentation width using the specified emitter.
      /// </summary>
      /// <param name="width"></param>
      /// <param name="indent"></param>
      /// <param name="maxIndentIncr"></param>
      /// <param name="emitter"></param>
      /// <example>
      ///   Construct a pretty printer that outputs to a file.
      ///   
      ///   PrettyPrinter pp = new PrettyPrinter(100,3,new FileCodeEmitter("output.txt"));
      ///   or simpler
      ///    PrettyPrinter pp = new("output.txt");
      /// </example>
      public PrettyPrinter(int width,int indent,int maxIndentIncr,CodeEmitterBase emitter) {
         this.LineLength = width;
         this.IndentMultiplier = indent;
         this.MaxIndentIncrement = maxIndentIncr;
         this.emitter = emitter;
         emitter.IndentWidth = this.IndentMultiplier;
         emitter.LineWidth = this.LineLength;
      }

      /// <summary>
      /// Construct a pretty printer with a default maximum line length of <see cref="DEFAULT_LINE_LENGTH"/> and an indentation width of <see cref="DEFAULT_INDENT_MULTIPLIER"/> using the specified emitter.
      /// </summary>
      /// <param name="emitter"></param>
      public PrettyPrinter(CodeEmitterBase emitter) : this (DEFAULT_LINE_LENGTH,DEFAULT_INDENT_MULTIPLIER,DEFAULT_MAX_INDENT_INCREMENT, emitter) { }
      /// <summary>
      /// Construct a pretty printer with a default maximum line length of <see cref="DEFAULT_LINE_LENGTH"/> and an indentation width of <see cref="DEFAULT_INDENT_MULTIPLIER"/> using the specified file name.
      /// </summary>
      /// <param name="fileName">If this is null, use the <see cref="DebugCodeEmitter"/> instead.</param>
      public PrettyPrinter(string? fileName) : this(DEFAULT_LINE_LENGTH,DEFAULT_INDENT_MULTIPLIER,DEFAULT_MAX_INDENT_INCREMENT, fileName.IsValidFileName() ? new FileCodeEmitter(fileName ?? "") : new DebugCodeEmitter()) { }

      private record struct UnitDelim(RW Start, RW End);
      private static readonly Dictionary<Type,UnitDelim> units = new() {
         { typeof(Program),new (RW.PROGRAM, RW.ENDPROG)},
         { typeof(Module),new (RW.MODULE, RW.ENDMOD)},
         { typeof(Layer),new (RW.LAYER, RW.ENDLAY)},
         { typeof(Section),new (RW.SECTION, RW.ENDSEC)},
      };

      public void Print(Program program,Set<Module> modules) {
         Print(program);
         foreach (Module module in modules) Print(module);
      }

      public void Print(Program program) {
         PrintUnitStart(program);
         PrintList(RW.PART,program.children.Select(part=>part.name));
         PrintLudes(program);
         PrintUnitEnd(program);
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
      }

      public void Print(Section section) {
         PrintUnitStart(section);
         PrintList(RW.EXPORT,section.export);
         PrintList(RW.IMPORT,section.import);
         PrintList(RW.ABSTR,section.abstr);
         PrintList(RW.EXT,section.ext);
         PrintList(RW.INV,section.inv);

         int EmitCount(IEnumerable<ID> list,string type) { int count = list.Count(); if (count > 0) emitter.Emitnl(indentLevel,$"# {count} {type} definition{(count==1 ? "" : "s")} #"); return count; }

         if (EmitCount(section.consts,"CONST") > 0) {
            emitter.Emit(indentLevel,RW.CONST," ");
            Print((Const)section.Symbols[section.consts.First()]);
            foreach (ID constId in section.consts.Skip(1)) {
               emitter.Emit(indentLevel,TT.LISTSEP.TT2String()," ");
               Print((Const)section.Symbols[constId]);
            }
            emitter.Emitnl(indentLevel,TT.END.TT2String());
         }

         if (EmitCount(section.vars,"VAR  ") > 0) {
            PrintList(RW.VAR,section.vars);
         }

         if (EmitCount(section.lists,"LIST ") > 0) {
            emitter.Emit(indentLevel,RW.LIST," ");
            Print((LIST)section.Symbols[section.lists.First()]);
            foreach (ID listId in section.lists.Skip(1)) {
               emitter.Emit(indentLevel,TT.LISTSEP.TT2String()," ");
               Print((LIST)section.Symbols[listId]);
            }
            emitter.Emitnl(indentLevel,TT.END.TT2String());
         }

         IEnumerable<ID> macros = section.routines.Where(r => section.Symbols[r] is Macro);
         if (EmitCount(macros,"MACRO") > 0) foreach (ID macroId in macros) Print((Macro)section.Symbols[macroId]);

         IEnumerable<ID> codes = section.routines.Where(r => section.Symbols[r] is Code);
         if (EmitCount(codes,"CODE ") > 0) foreach (ID codeId in codes) Print((Code)section.Symbols[codeId]);

         PrintUnitEnd(section);
         PrintLudes(section);
      }

      private void PrintLudes(Container container) {
         PrintLude(RW.PRELUDE,container);
         PrintLude(RW.ROOT,container);
         PrintLude(RW.POSTLUDE,container);
      }

      private void PrintLude(RW ludeType,Container container) {
         if (container is Section) {
            if (container.ludes[ludeType].Count != 0) {
               emitter.Emit(indentLevel,ludeType," ");
               // Section ludes are stored as ids of a generated Code item.
               if (container.Symbols[container.ludes[ludeType].First()] is Code code) { // This should always be the case
                  Print(code.alternatives.First());
                  emitter.Emitnl(indentLevel,TT.END);
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
            emitter.Emit(indentLevel,rw," ",ids.First().token.tokenString);
            foreach (ID id in ids.Skip(1)) {
               emitter.Emit(indentLevel,", ");
               emitter.Emit(indentLevel,id.token.tokenString);
            }
            emitter.Emitnl(indentLevel,TT.END.TT2String());
         }
      }

      public void Print(Code code) {
         PrintProcHead(code);
         indentLevel++;
         emitter.Emit(indentLevel,"# Code body#"); // emit the body
         emitter.Emitnl(indentLevel,TT.END.TT2String());
         indentLevel--;
      }

      public void Print(Macro macro) {
         PrintProcHead(macro);
         indentLevel++;
         emitter.Emit(indentLevel,"# Macro body#");   // emit the body
         emitter.Emitnl(indentLevel,TT.END.TT2String());
         indentLevel--;
      }

      private void PrintProcHead(Proc code) {
         emitter.Emit(indentLevel,code.procType," ",code.name);
         foreach (Param param in code.formals.Cast<Param>()) {
            emitter.Emit(indentLevel,param.paramType == PT.std ? TT.PARAMSEP.TT2String() : TT.STRINGPARAMSEP.TT2String());
            if (param.IsInput) emitter.Emit(indentLevel,TT.PARAMDIR.TT2String());
            emitter.Emit(indentLevel,param.token.tokenString);
            if (param.IsOutput) emitter.Emit(indentLevel,TT.PARAMDIR.TT2String());
         }
         if (code.locals.Any()) {
            emitter.Emit(indentLevel," ");
            foreach (ID local in code.locals) {
               emitter.Emit(indentLevel,TT.LOCALSEP.TT2String(),local.token.tokenString);
            }
         }
         emitter.Emitnl(indentLevel,code.bodyType.TT2String());
      }

      public void Print(Const constant) {
         emitter.Emit(constant.name,TT.EQUALS.TT2String());
         foreach (ConstElement element in constant.elements) {
            switch (element) {
               case STRING s:
                  emitter.Emit(indentLevel,s.value);
                  break;
               case INT n:
                  emitter.Emit(indentLevel,n.value);
                  break;
               case FLOAT f:
                  emitter.Emit(indentLevel,f.value);
                  break;
               case Const c:
                  Print(c);
                  break;
               case ID id:
                  emitter.Emit(indentLevel,id.token.tokenString);
                  break;
               default:
                  throw new NotImplementedException();
            }
         }
      }

      public void Print(LIST list) {
         emitter.Emit(indentLevel,list.name.token.tokenString,TT.LISTBOUNDSTART.TT2String(),list.lwb.tokenString,TT.LISTBOUNDSEP.TT2String(),list.upb.tokenString,TT.LISTBOUNDEND.TT2String());
      }

      private void PrintUnitStart(NamedElement unit) {
         emitter.Emitnl(indentLevel,units[unit.GetType()].Start.ToString()," ",unit.name.token.tokenString,TT.END.TT2String());
         indentLevel++;
      }

      private void PrintUnitEnd(NamedElement unit) {
         indentLevel--;
         emitter.Emitnl(indentLevel,units[unit.GetType()].End.ToString()," ",unit.name.token.tokenString,TT.END.TT2String());
      }
   }
}
