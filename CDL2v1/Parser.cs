using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using static CDL2v1.Logger;

namespace CDL2v1 {
   internal class Parser {
      /// <summary>
      /// The object being compiled. Used mainly for error reporting.
      /// </summary>
      /// <affix name="parser"></affix>
      public class CompilationObject(Parser parser) {
         private enum OT {ABSTR, EXT, INV, IMPORT, EXPORT, VAR, CONSTANT, LIST, FUNCTION, ACTION, TEST, PREDICATE, PRELUDE, ROOT, POSTLUDE, MODULE, LAYER, SECTION, PROGRAM }
         private static readonly OT[] AlgTypes = [OT.FUNCTION, OT.ACTION, OT.TEST, OT.PREDICATE];
         
         public string Program => (parser?.currentProgram?.ToString()+" ") ?? "";
         public string Module  => (parser?.currentModule?.ToString() + " ") ?? "";
         public string Layer   => (parser?.currentLayer?.ToString() + " ") ?? "";
         public string Section => (parser?.currentSection?.ToString() + " ") ?? "";
         public string Obj     => $"{(AlgTypes.Contains(type) ? $"{type} {name}" : "")}";

         private readonly Parser parser = parser;
         private OT type;
         private ID name = ID.ErrorID;
         public bool IsValid { get; set; } = true;

         public (RW,ID) Object {
            set {
               type = (OT)Enum.Parse(typeof(OT),value.Item1.ToString());
               name = value.Item2;
            }
         }

         override public string ToString() => $"{Program}{Module}{Layer}{Section}{Obj}".TrimStart();
      }

      public TokenList tokens = new();
      public SymbolTable modules = new();             // This symbol table will contain modules only.

      public Set<Module> Modules => modules.AsSet<Module>();

      public Program?          currentProgram;    // The current program being parsed. Should be the same as currentObject.Program.
      private Module?           currentModule;     // The current module being parsed. Should be the same as currentObject.Module.
      private Layer?            currentLayer;      // The current layer being parsed. Should be the same as currentObject.Layer.
      private Section?          currentSection;    // The current section being parsed. Should be the same as currentObject.Section.
      public  CompilationObject currentObject;     // The object being compiled. Used mainly for error reporting.

      public Parser() => currentObject = new CompilationObject(this);

      /// <summary>
      /// Recursive descent parser for CDL2.
      /// </summary>
      /// <affix name="tokens"></affix>
      /// <exception cref="Exception"></exception>
      internal void Parse(TokenList tokens) {
         this.tokens = tokens;
         // The list of tokens should contain a set of modules and possibly a program
         tokens.SetOptions(TokenList.Options.SkipComments | TokenList.Options.ThrowOnUnexpectedToken); // Remove comments from the token list and throw on unexpected tokens
                                                                                                       // The first token should be a module or program
         Logger.logger.ErrorAction = SkipToNextEnd;
         Logger.logger.CurrentObject = currentObject;

         while (tokens.IsNonEmpty()) {
            ID unitId = ID.ErrorID;
            if (tokens.CanConsumeContainerDelimiter(RW.MODULE,ref unitId)) {
               ParseModule(unitId);
            } else if (tokens.CanConsumeContainerDelimiter(RW.PROGRAM,ref unitId)) {
               if (currentProgram != null) {
                  throw new Exception("Only one PROGRAM is allowed");
               } else {
                  ParseProgram(unitId);
               }
            } else {
               throw new Exception("Expected MODULE or PROGRAM");
            }
         }

         Logger.logger.CurrentObject = null;
         Logger.logger.ErrorAction = null;
      }

      private void ParseProgram(ID programId) {
         // The next token should be an ID
         currentObject.Object = (RW.PROGRAM, programId);
         currentProgram = new Program(programId);
         currentProgram.Symbols[currentProgram.name] = currentProgram;
         Log(1,$"Parsing {currentProgram}");

         // Now should see parts
         List<ID> parts = [];

         if (tokens.CanConsume(RW.PART)) {
            ParseIDList(parts,null,null);
            foreach (ID part in parts) {
               if (modules.IsDeclared(part,out NamedElement? e) && e is Module mod) {
                  currentProgram.Children.Add(mod);
                  currentProgram.Symbols[part] = mod;
               } else {
                  ReportError($"Expected MODULE, for the name {part} but found {e}");
               }
            }
         }
         ParseLudes(currentProgram);
         // Consume the ENDPROG token
         tokens.CanConsumeContainerDelimiter(RW.ENDPROG,ref programId); 
      }


      /// <summary>
      /// Parse a module.
      /// This implementation uses the implementation favoured by the CDL2 lab, i.e., that a PROGRAM is required to specify the participating modules.
      /// The PROGRAM Ludes specify modules, such modules must have corresponding Ludes which specify sections, and the sections have Ludes specifying the routines to call.
      /// 
      /// The CDL2 compiler required that only a single module have Ludes.
      /// 
      /// This implementation therefore will:
      /// * If there is a PROGRAM, follow the CDL2 lab convention.
      /// * Otherwise it will follow the CD2 compiler convention.
      /// 
      /// THis is irrlevant for parsing, it will be handled in the semantic analysis.
      /// TODO: Semantic analysis to enforce CDL2 lab or compiler convention.
      /// </summary>
      /// <affix name="moduleId">The ID (name) of the module.</affix>
      private void ParseModule(ID moduleId) {
         currentObject.Object = (RW.MODULE, moduleId);
         currentModule = new Module(moduleId);
         modules[currentModule.name] = currentModule;
         Log(1,$"Parsing {currentObject}");

         // Now should see layers
         ID layerId = ID.ErrorID;
         while (tokens.CanConsumeContainerDelimiter(RW.LAYER,ref layerId)) {
            ParseLayer(layerId);
         }
         // Consume the ENDMOD
         tokens.CanConsumeContainerDelimiter(RW.ENDMOD,ref moduleId);
         ParseLudes(currentModule);
      }

      private void ParseLayer(ID layerId) {
         Debug.Assert(currentModule != null);
         currentObject.Object = (RW.LAYER, layerId);
         currentLayer = new Layer(layerId,currentModule);
         currentModule.Symbols[currentLayer.name] = currentLayer;
         Log(1,$"Parsing {currentObject}");

         // Now should see sections
         ID sectionId = ID.ErrorID;
         while (tokens.CanConsumeContainerDelimiter(RW.SECTION,ref sectionId)) {
            ParseSection(sectionId);
         }
         // Consume the ENDLAY
         tokens.CanConsumeContainerDelimiter(RW.ENDLAY,ref layerId);
         // Layers don't have Ludes.
      }

      private static readonly List<RW> AlgTypes = [RW.FUNCTION,RW.ACTION,RW.TEST,RW.PREDICATE];
      private void ParseSection(ID sectionId) {
         Debug.Assert(currentLayer != null);
         currentObject.Object = (RW.SECTION, sectionId);
         currentSection = new Section(sectionId,currentLayer);
         currentLayer.Symbols[currentSection.name] = currentSection;
         Log(1,$"Parsing {currentObject}");

         // Now should see section parts
         // Interfaces first
         ParseInterfaces();

         // Now could see routines, lists, vars, consts in any order.
         // Parse each type and return its ID.
         while (!tokens.IsNext(RW.ENDSEC)) {
            if (tokens.IsNext(AlgTypes)) {
               ParseAlgorithm();
            } else if (tokens.IsNext(RW.LIST)) {
               ParseList();
            } else if (tokens.IsNext(RW.VAR)) {
               ParseVar();
            } else if (tokens.IsNext(RW.CONST)) {
               ParseConsts();
            } else {
               ReportError("Expected FUNCTION, ACTION, TEST, PREDICATE, LIST, VAR, or CONST");
            }
         }

         // Consume the ENDSEC
         tokens.CanConsumeContainerDelimiter(RW.ENDSEC,ref sectionId);
         // Now could see prelude, root, postlude in that order.
         ParseLudes(currentSection);
      }

      private static readonly List<TT> bodyTypes = [TT.INLINECODEBODY,TT.MACROPROCBODY,TT.MACROBODY,TT.CODEBODY];
      private void ParseAlgorithm() {
         Debug.Assert(currentSection != null);
         if (tokens.CanConsume(AlgTypes,out Token algType) && tokens.CanConsume(out ID id)) {
            currentObject.Object = (algType.rval ?? RW.FUNCTION, id);
            List<Affix> formals = ParseParams();
            Algorithm? algorithm = null;
            if (tokens.Optional(TT.END)) {
               // IMPORT declaration. Check if it is in the import list.
               if (currentSection.import.Contains(id)) {
                  algorithm = new ImportedAlgorithm(id,formals,algType,currentSection);
               } else {
                  ReportError($"{algType} {id} is not exported but has no body.");
                  return;
               }
            } else if (currentSection.import.Contains(id)) {
               ReportError($"{algType} {id} is imported but has locals or a body.");
            } else {
               Set<Local> locals = ParseLocals();
               if (tokens.CanConsume(bodyTypes,out Token bodyType)) {                  
                  if (bodyType.type == TT.CODEBODY || bodyType.type == TT.INLINECODEBODY) {
                     // Parse the code body
                     algorithm = new Procedure(id,formals,locals,algType,bodyType.type,currentSection);
                     ParseProcedureBody((Procedure)algorithm);
                  } else {
                     // Parse the macro body
                     algorithm = new Macro(id,formals,locals,algType,bodyType.type,currentSection);
                     ParseMacroBody((Macro)algorithm);
                  }
               }
            }
            Debug.Assert(algorithm != null);
            currentSection.Symbols[algorithm.name] = algorithm;
            currentSection.routines.Add(algorithm.name);
         } else {
            ReportError("Expected FUNCTION, ACTION, TEST, or PREDICATE (this should be impossible");
         }
      }

      private void ParseMacroBody(Macro macro) {
         Debug.Assert(currentSection != null);
         while (!tokens.Optional(TT.END)) {
            if (tokens.Optional(TT.ID,out Token idToken)) {
               ID id = ID.From(idToken);
               if (macro.TryGetAffix(id,out Affix? affix)) {
                  macro.elements.Add(affix);
               } else if (macro.TryGetLocal(id,out Local local)) {
                  macro.elements.Add(local);
               } else {
                  // Can be a Var, Const, or List. TODO: Semantic Analysis to verify.
                  macro.elements.Add(id);
               }
            } else if (tokens.Optional(TT.STRING,out Token str)) {
               macro.elements.Add(new STRING(str));
            } else if (tokens.Optional(TT.INT,out Token i)) {
               macro.elements.Add(new INT(i));
            } else if (tokens.Optional(TT.FLOAT,out Token f)) {
               macro.elements.Add(new FLOAT(f));
            } else {
               ReportError("Expected ID, STRING, INT, or FLOAT");
            }
         }
      }
      private void ParseProcedureBody(Procedure algorithm) {
         algorithm.alternatives = ParseAlternatives();
         if (!tokens.CanConsume(TT.END)) ReportError("Expected .");
      }
      private List<Alternative> ParseAlternatives() {
         List<Alternative> alternatives = [];
         do {
            alternatives.Add(ParseAlternative());
         } while (tokens.Optional(TT.ALTSEP)) ;
         return alternatives;
      }

      private Alternative ParseAlternative() {
         List<Call> calls = [];
         LastCall? lastCall =null;
         do {
            if (lastCall != null) {
               // If we have a last call, then we should NOT have see a separator
               ReportError("Unexpected ,");
            } else if (tokens.Optional(out ID id)) {
               calls.Add(ParseCall(id));
            } else if (tokens.Optional(TT.SUCCEED)) {
               lastCall = new LastCall(LCT.Succeed);
            } else if (tokens.Optional(TT.FAIL)) {
               lastCall = new LastCall(LCT.Fail);
            } else if (tokens.Optional(TT.ABORT)) {
               lastCall = new LastCall(LCT.Abort);
            } else if (tokens.Optional(TT.REPEAT)) {
               lastCall = tokens.Optional(out id) ? new LastCall(id) : new LastCall(LCT.Repeat);
            } else if (tokens.Optional(TT.GRPOPEN)) {
               lastCall = ParseGroup();
            } else {
               ReportError("Expected ID, +, -, ?, or *");
            }
         } while (tokens.Optional(TT.CALLSEP));
         if (lastCall == null) {
            // The last all postion contained an actual call so convert it to a last call
            lastCall = new LastCall(calls.Last());
            calls.RemoveAt(calls.Count - 1);
         }
         return new Alternative(calls,lastCall);
      }

      private Call ParseCall(ID id) => ParseCall(this,id);
      private static Call ParseCall(Parser parser,ID id) {
         Debug.Assert(parser.currentSection != null);
         Call call = new(parser.currentSection.Symbols.Reference(id));
         ParseActualArgs(parser,call);
         return call;
      }

      private LastCall ParseGroup() {
         LastCall? lastCall;
         ID label = ParseOptionalLabel();
         Group group = new(label,ParseAlternatives());
         if (!tokens.CanConsume(TT.GRPCLOSE)) ReportError("Expected )");
         lastCall = new LastCall(group);
         return lastCall;
      }

      private ID ParseOptionalLabel() {
         if (tokens.Peek().type == TT.ID && tokens.Peek(1).type == TT.LABELSEP) {
            // Consume the label and the colon
            ID label = ID.From(tokens.Next());
            tokens.Next();
            return label;
         } else {
            return ID.AnonID;
         }
      }

      /// <summary>
      /// Parse the actual arguments of a call.
      /// Actual arguments are a sequence of IDs or strings separated by '+'.
      /// </summary>
      /// <affix name="call"></affix>
      // private void ParseActualArgs(Call call) => ParseActualArgs(this,call);
      private static void ParseActualArgs(Parser parser,Call call) {
         Debug.Assert(parser.currentSection != null);
         while (parser.tokens.Optional(TT.PARAMSEP)) {
            if (parser.tokens.Optional(out ID id)) {
               call.args.Add(parser.currentSection.Symbols.Reference(id));
            } else if (parser.tokens.CanConsume(TT.STRING,out Token str)) {
               call.args.Add(new STRING(str));
            } else {
               parser.ReportError("Expected ID or STRING");
            }
         }
      }

      private Set<Local> ParseLocals() {
         Set<Local> locals = [];
         while (tokens.Optional(TT.LOCALSEP) && tokens.CanConsume(TT.ID,out Token token)) locals.Add(new Local(ID.From(token)));
         return locals;
      }
      private static readonly List<TT> formalTypes = [TT.PARAMSEP,TT.STRINGPARAMSEP];
      private List<Affix> ParseParams() {
         List<Affix> args = [];
         while (tokens.Optional(formalTypes,out Token paramTypeInd)) {
            bool isIn = tokens.Optional(TT.PARAMDIR);
            if (tokens.CanConsume(out ID id)) {
               bool isOut = tokens.Optional(TT.PARAMDIR);
               AffixDir paramDir = isIn ? (isOut ? AffixDir.transput : AffixDir.input) : (isOut ? AffixDir.output : AffixDir.NONE);
               AffixType paramType = paramTypeInd.type == TT.PARAMSEP ? AffixType.std : AffixType.str;
               if (paramType == AffixType.str && paramDir != AffixDir.NONE) ReportError("String arguments cannot have a direction");
               if (paramType == AffixType.std && paramDir == AffixDir.NONE) ReportError("Standard arguments must be input, output, or transput");
               args.Add(new Affix(id,paramDir,paramType));
            }
         }
         return args;
      }

      private void ParseList() {
         if (tokens.CanConsume(RW.LIST)) {
            Debug.Assert(currentSection != null);
            ParseIDList(currentSection.lists,null,id => ParseListBody(id));
         }
      }

      private static readonly List<TT> boundTypes = [TT.ID,TT.INT];
      /// <summary>
      /// Parse the body of a list declaration. Format is lname(lwb:upb).
      /// <affix name="token"></affix>
      /// <exception cref="Exception"></exception>
      private void ParseListBody(ID id) {
         Debug.Assert(currentSection != null);
         if (  tokens.Optional(TT.LISTBOUNDSTART) &&
               tokens.CanConsume(boundTypes,out Token lwb) &&
               tokens.CanConsume(TT.LISTBOUNDSEP) &&
               (tokens.CanConsume(TT.ID,out Token upb) || tokens.CanConsume(TT.INT,out upb)) &&
               tokens.CanConsume(TT.LISTBOUNDEND)) {
            currentSection.Symbols[id] = new LIST(id,lwb,upb);
         } else if (!currentSection.import.Contains(id)) {
            LogError($"LIST {id} with has invalid bounds in section {currentSection.name}");
         }
      }

      /// <summary>
      /// Parse a var declaration.
      /// </summary>
      private void ParseVar() {
         Debug.Assert(currentSection != null);
         if (tokens.CanConsume(RW.VAR)) {
            Debug.Assert(currentSection != null);
            ParseIDList(currentSection.vars,null,id => currentSection.Symbols[id] = new Var(id));
         }
      }

      /// <summary>
      /// Parse a constant declaration.
      /// </summary>
      private void ParseConsts() {
         Debug.Assert(currentSection != null);
         if (tokens.CanConsume(RW.CONST)) {
            Debug.Assert(currentSection != null);
            ParseIDList(currentSection.consts,null,id => ParseConstBody(id));
         }
      }

      /// <summary>
      /// Parse the body of a constant declaration. At this point the ID has been consumed.
      /// We should see an '=' followed by a sequence of constant elements (e.g., numbers, strings, etc.) terminated by a period or a comma.
      /// The terminator will be consumed by <see cref="ParseIDList(ICollection{ID}, ICollection{ID}?, Action{ID}?)
      /// </summary>
      /// <affix name="token">The token of the constant.</affix>
      private void ParseConstBody(ID id) {
         Debug.Assert(currentSection != null);
         Const c = new(id);
         currentSection.Symbols[id] = c;
         if (tokens.Optional(TT.EQUALS)) {
            if (currentSection.import.Contains(id)) {
               LogError($"CONST {id} with definition is imported in section {currentSection.name}");
            } else {
               ParseConstElements(c);
            }
         } else if (!currentSection.import.Contains(id)) {
            LogError($"CONST {id} with no definition is not imported in section {currentSection.name}");
         }
      }

      /// <summary>
      /// Parse the elements of a constant declaration.
      /// </summary>
      /// <affix name="c"></affix>
      /// <exception cref="Exception"></exception>
      private void ParseConstElements(Const c) {
         Debug.Assert(currentSection != null);
         while (!tokens.IsNext(TT.END) && !tokens.IsNext(TT.SEP)) {
            if (tokens.Optional(TT.ID,out Token elemId)) {
               c.elements.Add(currentSection.Symbols.Reference(ID.From(elemId)));
            } else if (tokens.Optional(TT.STRING,out Token str)) {
               c.elements.Add(new STRING(str));
            } else if (tokens.Optional(TT.INT,out Token i)) {
               c.elements.Add(new INT(i));
            } else if (tokens.Optional(TT.FLOAT,out Token f)) {
               c.elements.Add(new FLOAT(f));
            } else {
               throw new Exception("Expected ID, STRING, INT, or FLOAT");
            }
         }
      }

      /// <summary>
      /// Parse the interfaces of a section.
      /// </summary>
      private void ParseInterfaces() {
         Debug.Assert(currentSection != null && currentModule != null);
         // Provided interfaces
         ParseInterfaceList(RW.ABSTR,currentSection.abstr);
         ParseInterfaceList(RW.EXT,currentSection.ext);
         ParseInterfaceList(RW.EXPORT,currentSection.export);
         // Required interfaces
         ParseInterfaceList(RW.INV,currentSection.inv);
         ParseInterfaceList(RW.IMPORT,currentSection.import,currentModule.import);
      }

      /// <summary>
      /// Parse a simple list of IDs occuring in interfaces.
      /// TODO: Verify that the IDs are uniqe within BOTH interface lists.
      /// </summary>
      /// <affix name="interfaceType"></affix>
      /// <affix name="idList1">The section intrface list.</affix>
      /// <affix name="idList2">The module interface list for imports.</affix>
      /// <returns></returns>
      private bool ParseInterfaceList(RW interfaceType,ICollection<ID> idList1,ICollection<ID>? idList2 = null) {
         Debug.Assert(currentSection != null);
         if (tokens.Consume(interfaceType)) {
            ParseIDList(idList1,idList2,container:currentSection);
            return true;
         } else {
            return false;
         }
      }

      private void ParseLudes(Container container) {
         container.ParseLude(this,RW.PRELUDE,container);
         container.ParseLude(this,RW.ROOT,container);
         container.ParseLude(this,RW.POSTLUDE,container);
      }

      internal static void ParseLudeOfIDs(Parser parser,RW type,Container container) {
         if (parser.tokens.Optional(type)) {
            while (parser.tokens.Optional(TT.ID,out Token  id)) {
               container.Ludes[type].Add(ID.From(id));
               if (!parser.tokens.CanConsumeSep()) break;
            }
            parser.tokens.CanConsumeEnd();
         }
      }

      /// <summary>
      /// Parse a section lude. This is an alternative (i.e., a sequence of calls, without the other options for the last call) terminated by a period.
      /// It will be stored as a Procedure item in the section's symbols table. The ID will be SectionName_LudeType. 
      /// </summary>
      /// <affix name="parser"></affix>
      /// <affix name="type"></affix>
      /// <affix name="container"></affix>
      internal static void ParseLudeOfCalls(Parser parser,RW type,Container container) {
         if (parser.tokens.Optional(type)) {
            //Debug.Assert(container != null);
            List<Call> callList =[];
            while (parser.tokens.Optional(TT.ID,out Token id)) {
               callList.Add(ParseCall(parser,ID.From(id)));
               if (!parser.tokens.CanConsumeSep()) break;
            }
            parser.tokens.CanConsumeEnd();
            Procedure lude = new(type,(Section)container);
            lude.alternatives.Add(new Alternative(callList,new LastCall(LCT.None)));
            container.Symbols[lude.name] = lude;
            container.Ludes[type].Add(lude.name);
         }
      }

      /// <summary>
      /// Parse a list of IDs. The list is terminated by a period. The lists are normally sets.
      /// The lists cannot contain duplicates
      /// TODO: Can an import be in more than one section in a module? Let's assume no.
      /// </summary>
      /// <affix name="idList1"></affix>
      /// <affix name="idList2"></affix>
      /// <affix name="processID"></affix>
      private void ParseIDList(ICollection<ID> idList1,ICollection<ID>? idList2 = null,Action<ID>? processID = null,Container? container=null) {
         while (tokens.IsNext(TT.ID)) {
            ID id = ID.From(tokens.Next());
            if (idList2 != null && !idList2.Contains(id)) idList2.Add(id);
            if (!idList1.Contains(id)) idList1.Add(id);
            processID?.Invoke(id);
            container?.Symbols.Reference(id);
            if (!tokens.CanConsumeSep()) break;
         }
         tokens.CanConsumeEnd();
      }

      private void ReportError(string v) => ReportError($"MOD {currentModule} LAY {currentLayer} SEC {currentSection}: {v}");
      internal void SkipToNextEnd() {
         while (!tokens.IsNext(TT.END)) tokens.Skip();
         tokens.Skip(); // The end itself
      }
   }
}
