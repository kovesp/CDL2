// Ignore Spelling: CDL

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
      /// <param id="parser"></param>
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
               switch (type) {
                  case OT.PROGRAM:
                     (parser.currentModule, parser.currentLayer, parser.currentSection) = (null, null, null);
                     break;
                  case OT.MODULE:
                     (parser.currentProgram,parser.currentLayer, parser.currentSection) = (null, null,null);
                     break;
                  case OT.LAYER:
                     parser.currentSection = null;
                     break;
               }
            }
         }

         override public string ToString() => $"{Program}{Module}{Layer}{Section}{Obj}".TrimStart();
      }

      public TokenList tokens = new();


      public  Program?          currentProgram;    // The current program being parsed. Should be the same as currentObject.Program.
      private Module?           currentModule;     // The current module being parsed. Should be the same as currentObject.Module.
      private Layer?            currentLayer;      // The current layer being parsed. Should be the same as currentObject.Layer.
      private Section?          currentSection;    // The current section being parsed. Should be the same as currentObject.Section.
      public  CompilationObject currentObject;     // The object being compiled. Used mainly for error reporting.

      public Parser() => currentObject = new CompilationObject(this);

      /// <summary>
      /// Recursive descent parser for CDL2.
      /// </summary>
      /// <param id="tokens"></param>
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
               ParseProgram(unitId);
            } else {
               throw new Exception("Expected MODULE or PROGRAM");
            }
         }

         Logger.logger.CurrentObject = null;
         Logger.logger.ErrorAction = null;
      }

      /// <summary>
      /// Parse a program. 
      /// PROGRAM program Name.
      ///   PART module1, module2, ... .
      ///   PRELUDE id1, id2, ... .
      ///   ROOT id1, id2, ... .
      ///   POSTLUDE id1, id2, ... .
      /// ENDPROG program Name.
      /// </summary>
      /// <param Name="programId"></param>
      private void ParseProgram(ID programId) {
         if (Program.Programs.ContainsKey(programId)) {
            ReportError($"Program {programId} already exists");
            return;
         } else {
            currentObject.Object = (RW.PROGRAM, programId);
            Program.Programs[programId] = currentProgram = new Program(programId);
            Log(1,$"Parsing {currentProgram}"); 
         }

         // Now should see parts
         List<ID> parts = [];

         if (tokens.CanConsume(RW.PART)) {
            ParseIDList(RW.PART,currentProgram.Parts);
            // TODO: Semantic Analysis to verify that the parts are modules.
         }
         ParseLudes(currentProgram);
         // Consume the ENDPROG token
         tokens.CanConsumeContainerDelimiter(RW.ENDPROG,ref programId); 
      }


      /// <summary>
      /// Parse a module.
      /// This implementation uses the implementation favoured by the CDL2 lab, i.e., that a PROGRAM is required to specify the participating modules.
      /// The PROGRAM Ludes specify modules, such modules must have corresponding Ludes which specify sections, and the sections have Ludes specifying the algorithms to call.
      /// 
      /// The CDL2 compiler required that only a single module have Ludes.
      /// 
      /// This implementation therefore will:
      /// * If there is a PROGRAM, follow the CDL2 lab convention.
      /// * Otherwise it will follow the CD2 compiler convention.
      /// 
      /// THis is irrelevant for parsing, it will be handled in the semantic analysis.
      /// TODO: Semantic analysis to enforce CDL2 lab or compiler convention.
      /// </summary>
      /// <param id="moduleId">The ID (id) of the module.</param>
      private void ParseModule(ID moduleId) {
         if (Program.Modules.ContainsKey(moduleId)) {
            ReportError($"Program {moduleId} already exists");
            return;
         } else {
            Program.Modules[moduleId] = currentModule = new Module(moduleId);
            currentObject.Object = (RW.MODULE, moduleId);
            Log(1,$"Parsing {currentObject}");
         }
         
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
         currentLayer= new Layer(layerId,currentModule,currentLayer);
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
         Log(1,$"Parsing {currentObject}");

         // Now should see section parts
         // Interfaces first
         ParseInterfaces();

         // Now could see algorithms, lists, variables, constants in any order.
         // Parse each type and return its ID.
         while (!tokens.IsNext(RW.ENDSEC)) {
            if (tokens.IsNext(AlgTypes)) {
               ParseAlgorithm();
            } else if (tokens.IsNext(RW.LIST)) {
               ParseList();
            } else if (tokens.IsNext(RW.VAR)) {
               ParseVar();
            } else if (tokens.IsNext(RW.CONST)) {
               ParseConstants();
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
            currentObject.Object = (algType.reservedWordValue ?? RW.FUNCTION, id);
            if (currentSection.local.ContainsKey(id)) {
               ReportError($"Algorithm {id} already declared in section {currentSection.id} as {currentSection.local[id].GetType().Name}");
               return;
            }
            List<Affix>? formals = ParseFormals();
            if (formals == null) return;
            Algorithm? algorithm = null;
            if (tokens.Optional(TT.END)) {
               // IMPORT declaration. Check if it is in the imports list.
               if (currentSection.import.Contains(id)) {
                  algorithm = new ImportedAlgorithm(id,formals,algType,currentSection);
               } else {
                  ReportError($"{algType} {id} is not exported but has no body.");
                  return;
               }
            } else if (currentSection.import.Contains(id)) {
               ReportError($"{algType} {id} is imported but has locals or a body.");
            } else {
               Set<Local>? locals = ParseLocals();
               if (locals == null) return;
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
            currentSection.local[id] = algorithm;
            id.section = currentSection;
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
         algorithm.alternatives = ParseAlternatives(algorithm);
         if (!tokens.CanConsume(TT.END)) ReportError("Expected .");
      }
      private List<Alternative> ParseAlternatives(Procedure proc) {
         List<Alternative> alternatives = [];
         do {
            alternatives.Add(ParseAlternative(proc));
         } while (tokens.Optional(TT.ALTSEP)) ;
         return alternatives;
      }

      private Alternative ParseAlternative(Procedure proc) {
         List<Call> calls = [];
         LastCall? lastCall =null;
         do {
            if (lastCall != null) {
               // If we have a last call, then we should NOT have see a separator
               ReportError("Unexpected ,");
            } else if (tokens.Optional(out ID id)) {
               calls.Add(ParseCall(id,proc));
            } else if (tokens.Optional(TT.SUCCEED)) {
               lastCall = new LastCall(LCT.Succeed);
            } else if (tokens.Optional(TT.FAIL)) {
               lastCall = new LastCall(LCT.Fail);
            } else if (tokens.Optional(TT.ABORT)) {
               lastCall = new LastCall(LCT.Abort);
            } else if (tokens.Optional(TT.REPEAT)) {
               lastCall = tokens.Optional(out id) ? new LastCall(id) : new LastCall(LCT.Repeat);
            } else if (tokens.Optional(TT.GRPOPEN)) {
               lastCall = ParseGroup(proc);
            } else {
               ReportError("Expected ID, +, -, ?, or *");
            }
         } while (tokens.Optional(TT.CALLSEP));
         if (lastCall == null) {
            // The last all position contained an actual call so convert it to a last call
            lastCall = new LastCall(calls.Last());
            calls.RemoveAt(calls.Count - 1);
         }
         return new Alternative(calls,lastCall);
      }

      private Call ParseCall(ID id,Procedure proc) => ParseCall(this,id,proc);
      private static Call ParseCall(Parser parser,ID id,Procedure proc) {
         Debug.Assert(parser.currentSection != null);
         Call call = new(id,proc);
         ParseActualArgs(parser,call);
         return call;
      }

      private LastCall ParseGroup(Procedure proc) {
         LastCall? lastCall;
         ID label = ParseOptionalLabel();
         Group group = new(label,ParseAlternatives(proc));
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
      /// <param id="call"></param>
      // private void ParseActualArgs(Call call) => ParseActualArgs(this,call);
      private static void ParseActualArgs(Parser parser,Call call) {
         Debug.Assert(parser.currentSection != null);
         while (parser.tokens.Optional(TT.PARAMSEP)) {
            if (parser.tokens.Optional(out ID id)) {
               call.args.Add(id);
            } else if (parser.tokens.CanConsume(TT.STRING,out Token str)) {
               call.args.Add(new STRING(str));
            } else {
               parser.ReportError("Expected ID or STRING");
            }
         }
      }

      private Set<Local>? ParseLocals() {
         Set<Local> locals = [];
         while (tokens.Optional(TT.LOCALSEP) && tokens.CanConsume(TT.ID,out Token token)) {
            Local local = new(ID.From(token));
            if (locals.Contains(local)) {
               ReportError($"Duplicate local {local}");
               return null;
            } else {
               locals.Add(local);
            }
         }
         return locals;
      }
      private static readonly List<TT> formalTypes = [TT.PARAMSEP,TT.STRINGPARAMSEP];
      private List<Affix>? ParseFormals() {
         List<Affix> args = [];
         while (tokens.Optional(formalTypes,out Token affixTypeInd)) {
            bool isIn = tokens.Optional(TT.AFFIXDIR);
            if (tokens.CanConsume(out ID id)) {
               bool isOut = tokens.Optional(TT.AFFIXDIR);
               AffixDir affixDir = isIn ? (isOut ? AffixDir.transput : AffixDir.input) : (isOut ? AffixDir.output : AffixDir.NONE);
               AffixType affixType = affixTypeInd.type == TT.PARAMSEP ? AffixType.std : AffixType.str;
               if (affixType == AffixType.str && affixDir != AffixDir.NONE) ReportError("String arguments cannot have a direction");
               if (affixType == AffixType.std && affixDir == AffixDir.NONE) ReportError("Standard arguments must be input, output, or transput");
               Affix affix = new(id,affixDir,affixType);
               if (args.Contains(affix)) {
                  ReportError($"Duplicate formal parameter {id}");
                  return null;
               } else {
                  args.Add(affix);
               }
            }
         }
         return args;
      }

      private void ParseList() {
         if (tokens.CanConsume(RW.LIST)) {
            Debug.Assert(currentSection != null);
            ParseIDDeclarationList(currentSection.local,id => ParseListBody(id));
         }
      }

      private static readonly List<TT> boundTypes = [TT.ID,TT.INT];
      /// <summary>
      /// Parse the body of a list declaration. Format is list-id(lwb:upb).
      /// <param id="token"></param>
      /// <exception cref="Exception"></exception>
      private LIST? ParseListBody(ID id) {
         Debug.Assert(currentSection != null);
         LIST? list = null;
         if (  tokens.Optional(TT.LISTBOUNDSTART) &&
               tokens.CanConsume(boundTypes,out Token lwb) &&
               tokens.CanConsume(TT.LISTBOUNDSEP) &&
               (tokens.CanConsume(TT.ID,out Token upb) || tokens.CanConsume(TT.INT,out upb)) &&
               tokens.CanConsume(TT.LISTBOUNDEND)) {
            list = new(id,lwb,upb);
         } else {
            ReportError($"LIST {id} with has invalid bounds in section {currentSection.id}");
         }
         return list;
      }

      /// <summary>
      /// Parse a var declaration.
      /// </summary>
      private void ParseVar() {
         Debug.Assert(currentSection != null);
         if (tokens.CanConsume(RW.VAR)) {
            ParseIDDeclarationList(currentSection.local,id => new Var(id));
         }
      }

      /// <summary>
      /// Parse a constant declaration.
      /// </summary>
      private void ParseConstants() {
         Debug.Assert(currentSection != null);
         if (tokens.CanConsume(RW.CONST)) {
            Debug.Assert(currentSection != null);
            ParseIDDeclarationList(currentSection.local,id => ParseConstBody(id));
         }
      }

      /// <summary>
      /// Parse the body of a constant declaration. At this point the ID has been consumed.
      /// We should see an '=' followed by a sequence of constant elements (e.g., numbers, strings, etc.) terminated by a period or a comma.
      /// The terminator will be consumed by <see cref="ParseIDList(ICollection{ID}, ICollection{ID}?, Action{ID}?)
      /// </summary>
      /// <param id="token">The token of the constant.</param>
      private Const? ParseConstBody(ID id) {
         Debug.Assert(currentSection != null);
         Const? c = new(id);
         if (tokens.Optional(TT.EQUALS)) {
            if (currentSection.import.Contains(id)) {
               LogError($"CONST {id} with definition is imported in section {currentSection.id}");
            } else {
               ParseConstElements(c);
            }
         } else if (!currentSection.import.Contains(id)) {
            LogError($"CONST {id} with no definition is not imported in section {currentSection.id}");
            c = null;
         }
         return c;
      }

      /// <summary>
      /// Parse the elements of a constant declaration.
      /// </summary>
      /// <param id="c"></param>
      /// <exception cref="Exception"></exception>
      private void ParseConstElements(Const c) {
         Debug.Assert(currentSection != null);
         while (!tokens.IsNext(TT.END) && !tokens.IsNext(TT.SEP)) {
            if (tokens.Optional(TT.ID,out Token elemId)) {
               ID id = ID.From(elemId);
               if (currentSection.local.ContainsKey(id)) {
                  // The ID is already declared in this section. It can only be a constant or undeclared.
                  // That will be true even if it is invoked or imported.
                  Debug.Assert(currentSection.local[id] is Const || currentSection.local[id] is Undeclared);
                  c.elements.Add(id);
               } else if (currentSection.import.Contains(id)) {
                  currentSection.local[id] = Undeclared.Instance;
                  c.elements.Add(id);
               }
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
         ParseInterfaceList(RW.IMPORT,currentSection.import);
      }

      /// <summary>
      /// Parse a simple list of IDs occurring in interfaces.
      /// TODO: Verify that the IDs are unique within BOTH interface lists.
      /// </summary>
      /// <param id="interfaceType"></param>
      /// <param id="idList">The section interface list.</param>
      /// <returns></returns>
      private bool ParseInterfaceList(RW interfaceType,ICollection<ID> idList) {
         if (tokens.Consume(interfaceType)) {
            ParseIDList(interfaceType,idList);
            return true;
         } else {
            return false;
         }
      }

      private void ParseLudes(Container container) {
         container.LudeParser(this,RW.PRELUDE,container);
         container.LudeParser(this,RW.ROOT,container);
         container.LudeParser(this,RW.POSTLUDE,container);
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
      /// <param id="parser"></param>
      /// <param id="type"></param>
      /// <param id="container"></param>
      internal static void ParseLudeOfCalls(Parser parser,RW type,Container container) {
         if (parser.tokens.Optional(type)) {
            //Debug.Assert(container != null);
            Section section = (Section)container;
            Procedure lude = new(type,section);
            List<Call> callList =[];
            while (parser.tokens.Optional(TT.ID,out Token id)) {
               callList.Add(ParseCall(parser,ID.From(id),lude));
               if (!parser.tokens.CanConsumeSep()) break;
            }
            parser.tokens.CanConsumeEnd();

            lude.alternatives.Add(new Alternative(callList,new LastCall(LCT.None)));
            section.Ludes[type].Add(lude.id);
            section.local[lude.id] = lude;
         }
      }

      /// <summary>
      /// Parse a list of IDs. The list is terminated by a period. The lists are normally sets.
      /// The lists cannot contain duplicates
      /// TODO: Can an imports be in more than one section in a module? Let's assume no.
      /// </summary>
      /// <param id="idList"></param>
      /// <param id="idList2"></param>
      /// <param id="processID"></param>
      private void ParseIDDeclarationList(Dictionary<ID,ICDL2Object> idList,Func<ID,ICDL2Object?> processID) {
         while (tokens.IsNext(TT.ID)) {
            ID id = ID.From(tokens.Next());
            ICDL2Object? cDL2Object = processID(id);
            if (cDL2Object != null) {
               if (!idList.ContainsKey(id)) {
                  idList[id] = cDL2Object;
                  id.section = currentSection;
               }
               // TODO: need error reporting for duplicate entries
            }

            if (!tokens.CanConsumeSep()) break;
         }
         tokens.CanConsumeEnd();
      }

      /// <summary>
      /// Parse plain list of IDs. Interface lists, PARTs and VARs.
      /// </summary>
      /// <param Name="idList"></param>
      /// <param Name="idList2"></param>
      private void ParseIDList(RW type,ICollection<ID> idList1) {
         while (tokens.IsNext(TT.ID)) {
            ID id = ID.From(tokens.Next());
            if (!idList1.Contains(id)) {
               idList1.Add(id);
            } else {
               ReportError($"Duplicate ID {id} in {type}");
            }
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
