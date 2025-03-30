// Ignore Spelling: CDL

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using static CDL2v1.Logger;

namespace CDL2v1 {
   public class Notes : List<Note> { }
   public class Parser : CompilationPhase {
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
      private Section?          currentSection;    // The current container being parsed. Should be the same as currentObject.Section.
      public  CompilationObject currentObject;     // The object being compiled. Used mainly for error reporting.

      public Parser(CDL2 compiler) : base(compiler) => currentObject = new CompilationObject(this);

      /// <summary>
      /// Recursive descent parser for CDL2.
      /// </summary>
      /// <param id="tokens"></param>
      /// <exception cref="Exception"></exception>
      internal void Parse(TokenList tokens) {
         this.tokens = tokens;
         // The list of tokens should contain a set of modules and possibly a program
         tokens.SetOptions(TokenList.Options.ThrowOnUnexpectedToken); 
                                                                                                       
         Logger.logger.ErrorAction = SkipToNextEnd;
         Logger.logger.CurrentObject = currentObject;

         while (tokens.IsNonEmpty()) {
            Notes notes = ParseNotes();
            ID unitId = ID.ErrorID;
            if (tokens.CanConsumeContainerDelimiter(RW.MODULE,ref unitId,out string? comments)) {
               ParseModule(unitId,comments,notes);
            } else if (tokens.CanConsumeContainerDelimiter(RW.PROGRAM,ref unitId,out comments)) {
               ParseProgram(unitId,comments,notes);
            } else {
               throw new Exception("Expected MODULE or PROGRAM");
            }
         }

         Logger.logger.CurrentObject = null;
         Logger.logger.ErrorAction = null;
      }

      /// <summary>
      /// Parse a program. 
      /// PROGRAM program PhaseName.
      ///   PART module1, module2, ... .
      ///   PRELUDE id1, id2, ... .
      ///   ROOT id1, id2, ... .
      ///   POSTLUDE id1, id2, ... .
      /// ENDPROG program PhaseName.
      /// </summary>
      /// <param PhaseName="programId"></param>
      private void ParseProgram(ID programId,string? comments,Notes notes) {
         if (Database.Instance.Programs.ContainsKey(programId)) {
            ReportError($"Program {programId} already exists");
            return;
         } else {
            currentObject.Object = (RW.PROGRAM, programId);
            Database.Instance.Programs[programId] = currentProgram = new Program(programId,comments,notes);
            Log(1,$"Parsing {currentProgram}");
         }

         if (tokens.CanConsume(RW.PART)) {
            ParseIDList(RW.PART,currentProgram.Parts);
            // TODO: Semantic Analysis to verify that the parts are modules.
         }

         ParseLudes(currentProgram);
         // Consume the ENDPROG token
         tokens.CanConsumeContainerDelimiter(RW.ENDPROG,ref programId,out _);
      }
      /// <summary>
      /// Parse (and skip) NOTEs.
      /// </summary>
      private Notes ParseNotes() {
         Notes notes = [];
         while (tokens.CanConsumeNote(out Note? note)) {
            notes.Add(note!);
         }
         return notes;
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
      private void ParseModule(ID moduleId,string? comments,Notes notes) {
         if (Database.Instance.Modules.ContainsKey(moduleId)) {
            ReportError($"Program {moduleId} already exists");
            return;
         } else {
            Database.Instance.Modules[moduleId] = currentModule = new Module(moduleId,comments,notes);
            currentObject.Object = (RW.MODULE, moduleId);
            Log(1,$"Parsing {currentObject}");
         }
         
         // Now should see layers
         ID layerId = ID.ErrorID;
         Notes internalNotes = ParseNotes();
         while (tokens.CanConsumeContainerDelimiter(RW.LAYER,ref layerId,out comments)) {
            ParseLayer(layerId,comments,internalNotes);
            internalNotes = ParseNotes();
         }
         // Consume the ENDMOD
         tokens.CanConsumeContainerDelimiter(RW.ENDMOD,ref moduleId,out _);
         ParseLudes(currentModule);
      }

      private void ParseLayer(ID layerId,string? comments,Notes notes) {
         Debug.Assert(currentModule != null);
         currentObject.Object = (RW.LAYER, layerId);
         currentLayer= new Layer(layerId,currentModule,currentLayer,comments,notes);
         Log(1,$"Parsing {currentObject}");

         // Now should see sections
         ID sectionId = ID.ErrorID;
         Notes internalNotes = ParseNotes();
         while (tokens.CanConsumeContainerDelimiter(RW.SECTION,ref sectionId,out comments)) {
            ParseSection(sectionId,comments,internalNotes);
            internalNotes = ParseNotes();
         }
         // Consume the ENDLAY
         tokens.CanConsumeContainerDelimiter(RW.ENDLAY,ref layerId,out _);
         // Layers don't have Ludes.
      }

      private static readonly List<RW> AlgTypes = [RW.FUNCTION,RW.ACTION,RW.TEST,RW.PREDICATE];
      private void ParseSection(ID sectionId,string? comments,Notes notes) {
         Debug.Assert(currentLayer != null);
         currentObject.Object = (RW.SECTION, sectionId);
         currentSection = new Section(sectionId,currentLayer,comments,notes);
         Log(1,$"Parsing {currentObject}");

         // Now should see container parts
         // Interfaces first
         ParseInterfaces();

         // Now could see algorithms, lists, variables, constants in any order.
         // Parse each LudeType and return its ID.
         while (!tokens.IsNext(RW.ENDSEC)) {
            Notes internalNotes = ParseNotes();
            if (tokens.IsNext(AlgTypes)) {
               ParseAlgorithm(internalNotes);
            } else if (tokens.IsNext(RW.LIST)) {
               ParseList(internalNotes);
            } else if (tokens.IsNext(RW.VAR)) {
               ParseVar(internalNotes);
            } else if (tokens.IsNext(RW.CONST)) {
               ParseConstants(internalNotes);
            } else {
               ReportError("Expected FUNCTION, ACTION, TEST, PREDICATE, LIST, VAR, or CONST");
            }
         }

         // Consume the ENDSEC
         tokens.CanConsumeContainerDelimiter(RW.ENDSEC,ref sectionId,out _);
         // Now could see prelude, root, postlude in that order.
         ParseLudes(currentSection);
      }

      private static readonly List<TT> bodyTypes = [TT.INLINECODEBODY,TT.MACROPROCBODY,TT.MACROBODY,TT.CODEBODY];
      private void ParseAlgorithm(Notes notes) {
         Debug.Assert(currentSection != null);
         if (tokens.CanConsume(AlgTypes,out Token algType) && tokens.CanConsume(out ID id)) {
            Logger.Log(3,$"Parsing {algType} {id}");
            currentObject.Object = (algType.reservedWordValue ?? RW.FUNCTION, id);
            if (currentSection.declarations.TryGetValue(id,out ICDL2Object? value)) {
               ReportError($"Algorithm {id} already declared in container {currentSection.id} as {value.GetType().Name}");
               return;
            }
            List<Affix>? formals = ParseAffixes();
            if (formals == null) return;
            Algorithm? algorithm = null;
            if (tokens.Optional(TT.END)) {
               // IMPORT declaration. Check if it is in the imports list.
               algorithm = new ImportedAlgorithm(id,formals,algType,currentSection);
               if (!currentSection.import.Contains(id)) {
                  AddNote(algorithm,Note.AlgorithmStubNotImported,algorithm.id.Name,currentSection.id.Name);
                  ReportError($"{algType} {id} is not exported but has no body.");
               }
            } else {
               Set<Local>? locals = ParseLocals();
               if (locals == null) return;
               if (tokens.CanConsume(bodyTypes,out Token bodyType)) {                  
                  if (bodyType.type == TT.CODEBODY || bodyType.type == TT.INLINECODEBODY) {
                     // Parse the code body
                     algorithm = new Procedure(id,formals,locals,algType,bodyType.type,currentSection);
                     algorithm.AddNotes(PhaseName, notes);
                     ParseProcedureBody((Procedure)algorithm);
                  } else {
                     // Parse the macro body
                     algorithm = new Macro(id,formals,locals,algType,bodyType.type,currentSection);
                     algorithm.AddNotes(PhaseName, notes);
                     ParseMacroBody((Macro)algorithm);
                  }
               }
            }
            Debug.Assert(algorithm != null);
            if (currentSection.import.Contains(id)) {
               AddNote(algorithm,Note.ImportedAlgorithmHasBody,algorithm.id.Name,currentSection.id.Name);
               ReportError($"{algType} {id} is imported but has locals or a body.");
            }
            currentSection.declarations[id] = algorithm;
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
               ReportError("Expected ID, Affix, Local, STRING, INT, or FLOAT");
            }
         }
      }
      private void ParseProcedureBody(Procedure proc) {
         proc.group.alternatives = ParseAlternatives(proc,group:null);
         if (!tokens.CanConsume(TT.END)) ReportError("Expected .");
      }
      private List<Alternative> ParseAlternatives(Procedure proc,Group? group) {
         List<Alternative> alternatives = [];
         do {
            Notes notes = ParseNotes();
            alternatives.Add(ParseAlternative(proc,group,notes));
         } while (tokens.Optional(TT.ALTSEP)) ;
         return alternatives;
      }

      private Alternative ParseAlternative(Procedure proc,Group? group,Notes notes) {
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
               if (proc.CanFail) {
                  lastCall = new LastCall(LCT.Fail);
               } else {
                  AddNote(proc, Note.IllegalFailOperator, proc.algorithmType);
                  ReportError($"{proc} contains fail operator",supressErrorAction:true);
               }
            } else if (tokens.Optional(TT.ABORT)) {
               lastCall = new LastCall(LCT.Abort);
            } else if (tokens.Optional(TT.REPEAT)) {
               if (tokens.Optional(out ID label)) {
                  // Go up the group hierarchy to see if the label can be use
                  Group? g = group;
                  bool found = false;
                  while (g != null) {
                     if (g.id == label) {
                        found = true;
                        break;
                     }
                     g = g.Parent;
                  }
                  if (!found && label != proc.id) { // The label can be the ContainingProc id
                     AddNote(proc,Note.LabelNotFound,label.Name);
                     ReportError($"Label {label} not found in group hierarchy");
                  }
                  lastCall = new LastCall(label);
                  if (id == proc.id) proc.repeatsProcedure = true;
               } else {
                  lastCall = new LastCall(LCT.Repeat);
               }
            } else if (tokens.Optional(TT.GRPOPEN)) {
               lastCall = ParseGroup(proc,containingGroup:group);
            } else {
               ReportError("Expected ID, +, -, ?, or *");
            }
         } while (tokens.Optional(TT.CALLSEP));
         if (lastCall == null) {
            // The last all position contained an actual call so convert it to a last call
            lastCall = new LastCall(calls.Last());
            calls.RemoveAt(calls.Count - 1);
         }
         return new Alternative(calls,lastCall,notes);
      }

      private Call ParseCall(ID id,Procedure proc) => ParseCall(this,id,proc);
      private static Call ParseCall(Parser parser,ID id,Procedure containingProc) {
         Debug.Assert(parser.currentSection != null);
         Call call = new(id,containingProc);
         ParseActualArgs(parser,call);
         return call;
      }

      private LastCall ParseGroup(Procedure proc,Group? containingGroup) {
         LastCall? lastCall;
         ID? label = ParseOptionalLabel(containingGroup,proc);
         Group group = new(label,[],containingGroup,synthetic:label is null);
         group.alternatives = ParseAlternatives(proc,group);
         if (!tokens.CanConsume(TT.GRPCLOSE)) ReportError("Expected )");
         lastCall = new LastCall(group);
         return lastCall;
      }

      private ID? ParseOptionalLabel(Group? group,Procedure proc) {
         if (tokens.Peek().type == TT.ID && tokens.Peek(1).type == TT.LABELSEP) {
            // Consume the label and the colon
            ID label = ID.From(tokens.Next());
            tokens.Next();
            // Go up the group hierarchy to see if the label is already defined.
            Group? g = group;
            while (g != null) {
               if (g.id == label) {
                  AddNote(proc,Note.DuplicateLabel,label.Name);
                  ReportError($"Duplicate label {label}");
                  return ID.AnonID;
               }
               g = g.Parent;
            }
            return label;
         } else {
            return null;
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
            if (!locals.Add(local)) {
               ReportError($"Duplicate declarations {local}");
               return null;
            }
         }
         return locals;
      }
      private static readonly List<TT> formalTypes = [TT.PARAMSEP,TT.STRINGPARAMSEP];
      private List<Affix>? ParseAffixes() {
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

      private void ParseList(Notes notes) {
         if (tokens.CanConsume(RW.LIST,out string? comments)) {
            Debug.Assert(currentSection != null);
            ParseIDDeclarationList(currentSection.declarations,comments!,ParseListBody,notes);
         }
      }

      /// <summary>
      /// Parse the body of a list declaration. Format is list-id(lwb:upb).
      /// <param id="token"></param>
      /// <exception cref="Exception"></exception>
      private LIST? ParseListBody(ID id) {
         Debug.Assert(currentSection != null);
         LIST? list = null;
         if (  tokens.Optional(TT.LISTBOUNDSTART) &&
               tokens.CanConsume(TT.ID,out Token lwbToken) &&
               tokens.CanConsume(TT.LISTBOUNDSEP) &&
               tokens.CanConsume(TT.ID,out Token upbToken) &&
               tokens.CanConsume(TT.LISTBOUNDEND)) {
            list = new(id,currentSection,ID.From(lwbToken),ID.From(upbToken));
         } else {
            ReportError($"LIST {id} with has invalid bounds in container {currentSection.id}");
         }
         return list;
      }

      /// <summary>
      /// Parse a var declaration.
      /// </summary>
      private void ParseVar(Notes notes) {
         Debug.Assert(currentSection != null);
         if (tokens.CanConsume(RW.VAR,out string? comments)) {
            ParseIDDeclarationList(currentSection.declarations,comments!,id => new Var(id,currentSection),notes);
         }
      }

      /// <summary>
      /// Parse a constant declaration.
      /// </summary>
      private void ParseConstants(Notes notes) {
         Debug.Assert(currentSection != null);
         if (tokens.CanConsume(RW.CONST,out string? comments)) {
            Debug.Assert(currentSection != null);
            ParseIDDeclarationList(currentSection.declarations,comments!,ParseConstBody,notes);
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
         if (tokens.Optional(TT.EQUALS)) {
            if (currentSection.import.Contains(id)) {
               LogError($"CONST {id} with definition is imported in container {currentSection.id}");
               return null;
            } else {
               Const c = new(id,currentSection);
               ParseConstElements(c);
               return c;
            }
         } else if (!currentSection.import.Contains(id)) {
            LogError($"CONST {id} with no definition is not imported in container {currentSection.id}");
            return null;
         } else {
            return new ImportedConst(id,currentSection);
         }
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
               if (currentSection.declarations.TryGetValue(id,out ICDL2Object? value)) {
                  // The ID is already declared in this container. It can only be a constant or undeclared.
                  // That will be true even if it is invoked or imported.
                  Debug.Assert(value is Const || value is Undeclared);
                  c.elements.Add(id);
               } else if (currentSection.import.Contains(id)) {
                  currentSection.declarations[id] = Undeclared.Instance;
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
      /// Parse the interfaces of a container.
      /// </summary>
      private void ParseInterfaces() {
         Debug.Assert(currentSection != null && currentLayer != null && currentModule != null);
         // Provided interfaces
         ParseInterfaceList(RW.ABSTR,currentSection.abstr,currentLayer.abstr);
         ParseInterfaceList(RW.EXT,currentSection.ext,currentLayer.ext);
         ParseInterfaceList(RW.EXPORT,currentSection.export,currentModule.exports);
         // Required interfaces
         ParseInterfaceList(RW.INV,currentSection.inv);
         ParseInterfaceList(RW.IMPORT,currentSection.import);
      }

      /// <summary>
      /// Parse a simple list of IDs occurring in interfaces.
      /// TODO: Verify that the IDs are unique within BOTH interface lists.
      /// </summary>
      /// <param id="interfaceType"></param>
      /// <param id="idList">The container interface list.</param>
      /// <returns></returns>
      private bool ParseInterfaceList(RW interfaceType,ICollection<ID> idList,Dictionary<ID,Section>? propagationDictionary = null) {
         if (tokens.Consume(interfaceType)) {
            ParseIDList(interfaceType,idList,propagationDictionary);
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
      /// Parse a Section lude. This is an alternative (i.e., a sequence of calls, without the other serializationOptions for the last call) terminated by a period.
      /// It will be stored as a Procedure. The ID will be SectionName_LudeType. 
      /// </summary>
      /// <param id="parser"></param>
      /// <param id="LudeType"></param>
      /// <param id="container"></param>
      internal static void ParseLudeOfCalls(Parser parser,RW ludeType,Container container) {
         if (parser.tokens.Optional(ludeType)) {
            //Debug.Assert(container != null);
            Section section = (Section)container;
            Procedure lude = new(ludeType,section);

            List<Call> callList =[];
            while (parser.tokens.Optional(TT.ID,out Token id)) {
               callList.Add(ParseCall(parser,ID.From(id),lude));
               if (!parser.tokens.CanConsumeSep()) break;
            }
            parser.tokens.CanConsumeEnd();

            lude.algorithmType = callList.All(call=>call.HasEffect) ? RW.ACTION : RW.FUNCTION;
            lude.group.alternatives.Add(new Alternative(callList,new LastCall(LCT.None),[]));
            section.Ludes[ludeType].Add(lude.id);
            section.declarations[lude.id] = lude;
         }
      }

      /// <summary>
      /// Parse a list of IDs. The list is terminated by a period. The lists are normally sets.
      /// The lists cannot contain duplicates
      /// TODO: Can an imports be in more than one container in a module? Let's assume no.
      /// </summary>
      /// <param id="idList"></param>
      /// <param id="idList2"></param>
      /// <param id="processID"></param>
      private void ParseIDDeclarationList(Dictionary<ID,ICDL2Object> idList,string comments,Func<ID,ICDL2Object?> processID,Notes notes) {
         NamedElement? firstObject = null;
         while (tokens.IsNext(TT.ID)) {
            ID id = ID.From(tokens.Next());
            ICDL2Object? CDL2Object = processID(id);            
            if (CDL2Object != null) {
               if (!idList.ContainsKey(id)) {
                  idList[id] = CDL2Object;
                  firstObject ??= (NamedElement)CDL2Object;
               }
               // TODO: need error reporting for duplicate entries
            }

            if (!tokens.CanConsumeSep()) break;
         }
         tokens.CanConsumeEnd();
         firstObject!.Comments = comments;
         firstObject!.AddNotes(PhaseName, notes);
      }

      /// <summary>
      /// Parse plain list of IDs. Interface lists, PARTs and VARs.
      /// </summary>
      /// <param PhaseName="idList"></param>
      /// <param PhaseName="idList2"></param>
      private void ParseIDList(RW type,ICollection<ID> idList,Dictionary<ID,Section>? propagationDictionary=null) {
         while (tokens.IsNext(TT.ID)) {
            ID id = ID.From(tokens.Next());
            if (! idList.Contains(id)) {
               idList.Add(id);
            } else {
               ReportError($"Duplicate ID {id} in {type}");
            }
            if (propagationDictionary != null) {
               if (! propagationDictionary.ContainsKey(id)) {
                  propagationDictionary![id] = currentSection!;
               } else {
                  ReportError($"Duplicate ID {id} in {type} for parent or propagation dictionary is null");
               }
            }
            if (!tokens.CanConsumeSep()) break;
         }
         tokens.CanConsumeEnd();
      }

      private void ReportError(string v,bool supressErrorAction=false) => Logger.ReportError($"{currentModule} {currentLayer} {currentSection}: {v}", supressErrorAction);
      internal void SkipToNextEnd() {
         while (!tokens.IsNext(TT.END)) tokens.Skip();
         tokens.Skip(); // The end itself
      }
   }
}
