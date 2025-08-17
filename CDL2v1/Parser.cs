// <auto-gen>
//=======================================================================
// <copyright file="Parser.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-02-10</creation-date>
// 
// <summary>
//   Responsible for parsing tokenized input into a CDL2 syntax tree.
// </summary>
// <attribution>
//   This file is part of the clean room reimplementation of the
//      CDL2 Compiler
//      CDL2 Laboratory
//      CDL2 Target Code Generators
//
//    Based on original work on CDL and CDL2 led by C. H. A. Koster
//    and the CDL2 team at the Universities of Berlin, Germany and
//    Nijmegen, The Netherlands.
//
//    The CDL2 Laboratory was the work of Epsilon GmbH, Berlin.
//    H. M. Stahl, H. Feuerhahn, JP. Dehotay, B. Böhringer
//    (and others I don't remember ... sorry).
//
//    This project is not affiliated with the original CDL2 project.
// </attribution>
//=======================================================================
// </auto-gen>

// Ignore Spelling: CDL

using System;
using System.Collections;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Controls;

using static CDL2v1.Logger;

namespace CDL2v1 {
   public class Parser : CompilationPhase {
      /// <summary>
      /// The object being compiled. Used mainly for error reporting.
      /// </summary>
      /// <param Id="parser"></param>
      public class CompilationObject(Parser parser) {
         private enum OT { ABSTR, EXT, INV, IMPORT, EXPORT, VAR, CONSTANT, LIST, FUNCTION, ACTION, TEST, PREDICATE, PRELUDE, ROOT, POSTLUDE, MODULE, LAYER, SECTION, PROGRAM }
         private static readonly OT[] AlgTypes = [OT.FUNCTION,OT.ACTION,OT.TEST,OT.PREDICATE];

         public string Program => (parser?.currentProgram?.ToString() + " ") ?? "";
         public string Module => (parser?.currentModule?.ToString() + " ") ?? "";
         public string Layer => (parser?.currentLayer?.ToString() + " ") ?? "";
         public string Section => (parser?.currentSection?.ToString() + " ") ?? "";
         public string Obj => $"{(AlgTypes.Contains(type) ? $"{type} {name}" : "")}";

         private readonly Parser parser = parser;
         private OT type;
         private ID name = ID.ErrorID;
         public bool IsValid { get; set; } = true;

         public (RW, ID) Object {
            set {
               type = (OT)Enum.Parse(typeof(OT),value.Item1.ToString());
               name = value.Item2;
               switch (type) {
                  case OT.PROGRAM:
                     (parser.currentModule, parser.currentLayer, parser.currentSection) = (null, null, null);
                     break;
                  case OT.MODULE:
                     (parser.currentProgram, parser.currentLayer, parser.currentSection) = (null, null, null);
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

      public Program? currentProgram;    // The current program being parsed. Should be the same as currentObject.Program.
      private Module? currentModule;     // The current module being parsed. Should be the same as currentObject.Module.
      private Layer? currentLayer;      // The current layer being parsed. Should be the same as currentObject.Layer.
      private Section? currentSection;    // The current container being parsed. Should be the same as currentObject.SectionById.
      public CompilationObject currentObject;     // The object being compiled. Used mainly for error reporting.

      private LexicalAnalyzer? Lexer = null;       // The lexical analyzer used to parse the input file.

      public static readonly Dictionary<RW,Type> RW2Type = new() {
         [RW.PROGRAM]   = typeof(Program),
         [RW.MODULE]    = typeof(Module),
         [RW.LAYER]     = typeof(Layer),
         [RW.SECTION]   = typeof(Section),
         [RW.CONST]     = typeof(Const),
         [RW.LIST]      = typeof(LIST),
         [RW.VAR]       = typeof(Var),
         [RW.TEST]      = typeof(Algorithm),
         [RW.PREDICATE] = typeof(Algorithm),
         [RW.FUNCTION]  = typeof(Algorithm),
         [RW.ACTION]    = typeof(Algorithm),
      };

      private readonly Action<Severity,string,bool> ErrorReporter;

      public Parser(CDL2 compiler,Action<Severity,string,bool>? reporter = null) : base(compiler) {
         currentObject = new CompilationObject(this);
         ErrorReporter = reporter ?? ((severity,message,suppressErrorAction) => Logger.ReportError($"{currentModule} {currentLayer} {currentSection}: {message}",suppressErrorAction));
      }

      private void ReportInvalidToken(TokenType[] expected,Token actual,RW[] rw) {
         Container subject = currentSection != null ? currentSection : currentLayer != null ? currentLayer : currentModule != null ? currentModule : currentProgram!;
         string expectedTypes;
         if (expected.Length == 1 && expected[0] == TT.RESWORD) {
            expectedTypes = rw.Length == 1 ? rw[0].ToString() : $"one of {string.Join(",",rw)}";
         } else {
            expectedTypes = expected.Length == 1 ? expected[0].ToString() : $"one of {string.Join(",",expected)}";
         }
         string actualType = actual.type == TT.RESWORD ? actual.reservedWordValue?.ToString()! : actual.type.ToString();
         AddNote(subject,Note.UnexpectedToken,expectedTypes,actualType);
      }

      public override void ReportNoteCounts(Reachable? reachable,string? message = null) {
         Lexer!.ReportNoteCounts(reachable,message);
         base.ReportNoteCounts(reachable,message);
      }


      /// <summary>
      /// Recursive descent parser for CDL2.
      /// </summary>
      /// <param Id="tokens"></param>
      /// <exception cref="Exception"></exception>
      internal void Parse(string filePath) => ParseString(File.ReadAllText(filePath));
      private void ParseString(string input) {
         Tokenize(input);
         ParseTokens();
      }

      public void Tokenize(string input) {
         tokens = new TokenList(ReportInvalidToken);
         (Lexer = new LexicalAnalyzer(Compiler,tokens)).Tokenize(input);
      }

      public void ParseTokens() {
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
               //throw new Exception("Expected MODULE or PROGRAM");
               break;
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
      private void ParseProgram(ID programId,string comments,Notes notes) {
         if (Database.Instance.IsNamedElement<Program>(programId)) {
            AddNote(Note.DuplicateContainer,programId.Name);
            return;
         } else {
            currentObject.Object = (RW.PROGRAM, programId);
            currentProgram = new Program(programId,comments,notes);
            Log(1,$"Parsing {currentProgram}");
         }

         if (tokens.CanConsume(RW.PART)) {
            ParseIDList(RW.PART,currentProgram.Parts);
         }

         ParseLudes(currentProgram);
         // Consume the ENDPROG token
         tokens.CanConsumeContainerDelimiter(RW.ENDPROG,ref programId,out _);
      }
      /// <summary>
      /// Parse (and skip) NOTEs.
      /// </summary>
      private Notes ParseNotes(bool needsEnd = true) {
         Notes notes = [];
         while (tokens.CanConsumeNote(out Note? note,needsEnd:needsEnd)) {
            notes.Add(note!);
         }
         return notes;
      }

      /// <summary>
      /// Parse a module.
      /// This implementation uses the implementation favored by the CDL2 lab, i.e., that a PROGRAM is required to specify the participating modules.
      /// The PROGRAM Ludes specify modules, such modules must have corresponding Ludes which specify sections, and the sections have Ludes specifying the algorithms to call.
      /// 
      /// The CDL2 Compiler required that only a single module have Ludes.
      /// 
      /// This implementation therefore will:
      /// * If there is a PROGRAM, follow the CDL2 lab convention.
      /// * Otherwise it will follow the CD2 Compiler convention.
      /// 
      /// THis is irrelevant for parsing, it will be handled in the semantic analysis.
      /// TODO: Semantic analysis to enforce CDL2 lab or Compiler convention.
      /// </summary>
      /// <param Id="moduleId">The ID (Id) of the module.</param>
      private void ParseModule(ID moduleId,string comments,Notes notes) {
         if (Database.Instance.IsNamedElement<Module>(moduleId)) {
            AddNote(Note.DuplicateContainer,moduleId.Name);
            return;
         } else {
            currentObject.Object = (RW.MODULE, moduleId);
            currentModule = new Module(moduleId,comments,notes);
            Log(1,$"Parsing {currentObject}");
         }

         // Now should see layers
         ID layerId = ID.ErrorID;
         Notes internalNotes = ParseNotes();
         while (tokens.CanConsumeContainerDelimiter(RW.LAYER,ref layerId,out string? layerComments)) {
            ParseLayer(layerId,layerComments,internalNotes);
            internalNotes = ParseNotes();
         }
         // Consume the ENDMOD
         tokens.CanConsumeContainerDelimiter(RW.ENDMOD,ref moduleId,out _);
         ParseLudes(currentModule);
      }

      private void ParseLayer(ID layerId,string comments,Notes notes) {
         Debug.Assert(currentModule != null);
         currentObject.Object = (RW.LAYER, layerId);
         currentLayer = new Layer(layerId,currentModule,currentLayer,comments,notes);
         Log(1,$"Parsing {currentObject}");

         // Now should see sections
         ID sectionId = ID.ErrorID;
         Notes internalNotes = ParseNotes();
         while (tokens.CanConsumeContainerDelimiter(RW.SECTION,ref sectionId,out string? sectionComments)) {
            ParseSection(sectionId,sectionComments,internalNotes);
            internalNotes = ParseNotes();
         }
         // Consume the ENDLAY
         tokens.CanConsumeContainerDelimiter(RW.ENDLAY,ref layerId,out _);
         // Layers don't have Ludes.
      }

      private static readonly List<RW> AlgTypes = [RW.FUNCTION,RW.ACTION,RW.TEST,RW.PREDICATE];
      private void ParseSection(ID sectionId,string comments,Notes notes) {
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
               ParseAlgorithm(internalNotes,out _);
            } else if (tokens.IsNext(RW.LIST)) {
               ParseList(internalNotes);
            } else if (tokens.IsNext(RW.VAR)) {
               ParseVar(internalNotes);
            } else if (tokens.IsNext(RW.CONST)) {
               ParseConstants(internalNotes);
            } else {
               ReportError($"Expected FUNCTION, ACTION, TEST, PREDICATE, LIST, VAR, or CONST. Seeing {tokens.Peek()}");
            }
         }

         // Consume the ENDSEC
         tokens.CanConsumeContainerDelimiter(RW.ENDSEC,ref sectionId,out _);
         // Now could see prelude, root, postlude in that order.
         ParseLudes(currentSection);
      }

      private static readonly List<TT> bodyTypes = [TT.INLINEPROCBODY,TT.MACROPROCBODY,TT.MACROBODY,TT.PROCBODY];
      private void ParseAlgorithm(Notes notes,out Algorithm? algorithm) {
         Debug.Assert(currentSection != null);
         algorithm = null;
         if (tokens.CanConsume(AlgTypes,out Token algType) && tokens.CanConsume(out ID id)) {
            Logger.Log(4,$"Parsing {algType} {id}");
            RW algTypeRW = algType.reservedWordValue ?? RW.FUNCTION;
            currentObject.Object = (algTypeRW, id);
            if (DuplicateDeclaration(id,algTypeRW)) return;
            List<Affix>? affixes = ParseAffixes();
            if (affixes == null) return;
            if (tokens.Optional(TT.END)) {
               // IMPORT declaration. Check if it is in the imports list.
               algorithm = new ImportedAlgorithm(id,affixes,algType,currentSection);
               algorithm.AddNotes(PhaseName,notes);
               if (!currentSection.import.Contains(id)) {
                  AddNote(currentSection,Note.ObjectNotImported,algorithm); return;
               }
            } else {
               Set<Local>? locals = ParseLocals();
               if (locals == null) return;
               if (tokens.CanConsume(bodyTypes,out Token bodyType)) {
                  if (bodyType.type == TT.PROCBODY || bodyType.type == TT.INLINEPROCBODY) {
                     // Parse the code body
                     algorithm = new Procedure(id,affixes,locals,algType,bodyType.type,currentSection);
                     algorithm.AddNotes(PhaseName,notes);
                     ParseProcedureBody((Procedure)algorithm);
                  } else {
                     // Parse the macro body
                     algorithm = new Macro(id,affixes,locals,algType,bodyType.type,currentSection);
                     algorithm.AddNotes(PhaseName,notes);
                     ParseMacroBody((Macro)algorithm);
                  }
               }
               Debug.Assert(algorithm != null);
               if (currentSection.import.Contains(id)) {
                  AddNote(currentSection,Note.ObjectImportedButHasBody,algorithm);
                  return;
               }
            }
            currentSection.Declarations[id] = algorithm.GUID;
         } else {
            ReportError("Expected FUNCTION, ACTION, TEST, or PREDICATE (this should be impossible");
         }
      }

      private bool DuplicateDeclaration(ID id,RW type) {
         if (currentSection!.Declarations.TryGetValue(id,out CDL2Object? value)) {
            AddNote(currentSection,Note.DuplicateDeclaration,$"{type} {id}",value!);
            return true;
         }
         return false;
      }

      private void ParseMacroBody(Macro macro) {
         ParseElementList(macro,macro.elements,"ID, Affix, Local, STRING, INT, or FLOAT");
         if (!tokens.CanConsume(TT.END))
            ReportError("Expected .");
      }

      private void ParseProcedureBody(Procedure proc) {
         proc.group.Alternatives = ParseAlternatives(proc,group: proc.group);
         if (!tokens.CanConsume(TT.END))
            ReportError("Expected .");
      }
      private List<Alternative> ParseAlternatives(Procedure proc,Group group) {
         List<Alternative> alternatives = [];
         do {
            Notes notes = ParseNotes(needsEnd: false);
            alternatives.Add(ParseAlternative(proc,group,notes));
         } while (tokens.Optional(TT.ALTSEP));
         return alternatives;
      }

      private Alternative ParseAlternative(Procedure proc,Group group,Notes notes) {
         Alternative alternative = new(notes,group);
         do {
            if (alternative.lastCall.type != LCT.None) {
               // If we have a last call, then we should NOT have see a separator
               ReportError("Unexpected ,");
            } else if (tokens.Optional(RW.BUILTIN) && tokens.Optional(out ID id)) {
               alternative.calls.Add(ParseCall(id,proc,alternative,builtin: true));
            } else if (tokens.Optional(out id)) {
               alternative.calls.Add(ParseCall(id,proc,alternative));
            } else if (tokens.Optional(TT.SUCCEED)) {
               alternative.lastCall = new LastCall(LCT.Succeed,alternative);
            } else if (tokens.Optional(TT.FAIL)) {
               alternative.lastCall = new LastCall(LCT.Fail,alternative);
               if (!proc.CanFail) {
                  AddNote(proc,Note.IllegalFailOperator,proc.AlgorithmType);
                  ReportError($"{proc} contains fail operator",suppressErrorAction: true);
               }

            } else if (tokens.Optional(TT.ABORT)) {
               alternative.lastCall = new LastCall(LCT.Abort,alternative);
            } else if (tokens.Optional(TT.REPEAT)) {
               if (tokens.Optional(out ID label)) {
                  if (group.HasLabeledAncestorGroup(label)) {
                     AddNote(proc,Note.LabelNotFound,label.Name);

                  }


                  alternative.lastCall = new LastCall(label,alternative);
                  if (id == proc.Id)
                     proc.repeatsProcedure = true;
               } else {
                  alternative.lastCall = new LastCall(LCT.Repeat,alternative);
               }
            } else if (tokens.Optional(TT.GRPOPEN)) {
               alternative.lastCall = ParseGroup(proc,containingGroup: group,containingAlternative: alternative);
            } else if (tokens.IsNext(TT.END) || tokens.IsNext(TT.ALTSEP)) {
               // The last item in an alternative can be empty which is equivalent to a succeed and is represented as such.
               alternative.lastCall = new LastCall(LCT.Succeed,alternative);
            } else {
               ReportError("Expected ID, +, -, ?, or *");
            }
         } while (tokens.Optional(TT.CALLSEP));
         alternative.NormalizeCalls();
         return alternative;
      }

      private Call ParseCall(ID id,Procedure proc,Alternative containingAlternative,bool builtin = false) => ParseCall(this,id,proc,containingAlternative,builtin);
      private static Call ParseCall(Parser parser,ID id,Procedure containingProc,Alternative containingAlternative,bool builtin = false) {
         Debug.Assert(parser.currentSection != null);
         Call call = new(id,containingProc,containingAlternative,builtin);
         ParseActualArgs(parser,call,containingProc);
         return call;
      }

      private LastCall ParseGroup(Procedure proc,Group containingGroup,Alternative containingAlternative) {
         LastCall? lastCall;
         ID? label = ParseOptionalLabel(containingGroup,proc);
         Group group = new(label,[],containingAlternative.GUID,synthetic: label is null);
         group.Alternatives = ParseAlternatives(proc,group);
         if (!tokens.CanConsume(TT.GRPCLOSE))
            ReportError("Expected )");
         lastCall = new LastCall(group,containingAlternative);
         return lastCall;
      }

      private ID? ParseOptionalLabel(Group group,Procedure proc) {
         if (tokens.Peek().type == TT.ID && tokens.Peek(1).type == TT.LABELSEP) {
            // Consume the label and the colon
            ID label = ID.From(tokens.Next());
            tokens.Next();
            // Go up the group hierarchy to see if the label is already defined.
            if (group.HasLabeledAncestorGroup(label)) {
               AddNote(proc,Note.DuplicateLabel,label.Name);
               return ID.AnonID; // Return a dummy ID to indicate the error
            } else {
               return label;
            }
         }
         return null;
      }

      /// <summary>
      /// Parse the actual arguments of a call.
      /// Actual arguments are a sequence of IDs or strings separated by '+'.
      /// </summary>
      /// <param Id="call"></param>
      private static void ParseActualArgs(Parser parser,Call call,Procedure proc) {
         Debug.Assert(parser.currentSection != null);
         while (parser.tokens.Optional(TT.AFFIXSEP)) {
            if (parser.tokens.Optional(out ID id)) {
               call.argRefs.Add(id);
            } else if (parser.tokens.CanConsume(TT.STRING,out Token str)) {
               call.argRefs.Add(new STRING(str));
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
               ReportError($"Duplicate Declarations {local}");
               return null;
            }
         }
         return locals;
      }
      private static readonly List<TT> formalTypes = [TT.AFFIXSEP,TT.STRINGAFFIXSEP];
      private List<Affix>? ParseAffixes() {
         List<Affix> args = [];
         while (tokens.Optional(formalTypes,out Token affixTypeInd)) {
            bool isIn = tokens.Optional(TT.AFFIXDIR);
            if (tokens.CanConsume(out ID id)) {
               bool isOut = tokens.Optional(TT.AFFIXDIR);
               AffixDir affixDir = isIn ? (isOut ? AffixDir.transput : AffixDir.input) : (isOut ? AffixDir.output : AffixDir.NONE);
               AffixType affixType = affixTypeInd.type == TT.AFFIXSEP ? AffixType.std : AffixType.str;
               if (affixType == AffixType.str && affixDir != AffixDir.NONE)
                  ReportError("String arguments cannot have a direction");
               if (affixType == AffixType.std && affixDir == AffixDir.NONE)
                  ReportError("Standard arguments must be input, output, or transput");
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
            ParseIDDeclarationList(currentSection.Declarations,comments!,ParseListBody,notes);
         }
      }

      /// <summary>
      /// Parse the body of a list declaration. Format is list-Id(lwb:upb).
      /// <param Id="token"></param>
      /// <exception cref="Exception"></exception>
      private LIST? ParseListBody(ID id) {
         Debug.Assert(currentSection != null);
         if (DuplicateDeclaration(id,RW.LIST))
            return null;
         LIST? list = null;
         if (tokens.Optional(TT.LISTBOUNDSTART) &&
               tokens.CanConsume(TT.ID,out Token lwbToken) &&
               tokens.CanConsume(TT.LISTBOUNDSEP) &&
               tokens.CanConsume(TT.ID,out Token upbToken) &&
               tokens.CanConsume(TT.LISTBOUNDEND)) {
            list = new(id,currentSection,ID.From(lwbToken),ID.From(upbToken));
         } else {
            AddNote(currentSection,Note.InvalidListBounds,id);
         }
         return list;
      }

      /// <summary>
      /// Parse a var declaration.
      /// </summary>
      private void ParseVar(Notes notes) {
         Debug.Assert(currentSection != null);
         if (tokens.CanConsume(RW.VAR,out string? comments)) {
            ParseIDDeclarationList(currentSection.Declarations,comments!,id => {
               if (DuplicateDeclaration(id,RW.VAR))
                  return null;
               else
                  return new Var(id,currentSection);
            },notes);
         }
      }

      /// <summary>
      /// Parse a constant declaration.
      /// </summary>
      private void ParseConstants(Notes notes) {
         Debug.Assert(currentSection != null);
         if (tokens.CanConsume(RW.CONST,out string? comments)) {
            Debug.Assert(currentSection != null);
            ParseIDDeclarationList(currentSection.Declarations,comments!,ParseConstBody,notes);
         }
      }

      /// <summary>
      /// Parse the body of a constant declaration. At this point the ID has been consumed.
      /// We should see an '=' followed by a sequence of constant elements (e.g., numbers, strings, etc.) terminated by a period or a comma.
      /// The terminator will be consumed by <see cref="ParseIDList(ICollection{ID}, ICollection{ID}?, Action{ID}?)
      /// </summary>
      /// <param Id="token">The token of the constant.</param>
      private Const? ParseConstBody(ID id) {
         Debug.Assert(currentSection != null);
         if (DuplicateDeclaration(id,RW.CONST))
            return null;
         if (tokens.Optional(TT.EQUALS)) {
            if (currentSection.import.Contains(id)) {
               LogError($"CONST {id} with definition is imported in container {currentSection.Id}");
               return null;
            } else {
               Const c = new(id,currentSection);
               ParseElementList(c,c.elements,"ID, STRING, INT, or FLOAT",secondaryTerminator: TT.SEP);
               return c;
            }
         } else if (!currentSection.import.Contains(id)) {
            LogError($"CONST {id} with no definition is not imported in container {currentSection.Id}");
            return null;
         } else {
            return new ImportedConst(id,currentSection);
         }
      }

      /// <summary>
      /// Parse the elements of a constant declaration.
      /// </summary>
      /// <param Id="c"></param>
      /// <exception cref="Exception"></exception>
      private void ParseElementList(NamedElement parent,List<IElement> elements,string expected,TT secondaryTerminator = TokenType.END) {
         Debug.Assert(currentSection != null);
         while (!tokens.IsNext(TT.END) && !tokens.IsNext(secondaryTerminator)) {
            if (tokens.Optional(TT.ELEMSEP,out Token _)) {
               continue;
            } else if (tokens.Optional(TT.ID,out Token elemId)) {
               elements.Add(ID.From(elemId));
            } else if (tokens.Optional(TT.STRING,out Token str)) {
               elements.Add(new STRING(str));
            } else if (tokens.Optional(TT.INT,out Token i)) {
               elements.Add(new INT(i));
            } else if (tokens.Optional(TT.FLOAT,out Token f)) {
               elements.Add(new FLOAT(f));
            } else {
               AddNote(parent,Note.UnexpectedToken,expected,tokens.Peek().ToString());
            }
         }
      }

      /// <summary>
      /// Parse the interfaces of a container.
      /// </summary>
      private void ParseInterfaces() {
         Debug.Assert(currentSection != null && currentLayer != null && currentModule != null);
         // The interface can be in any order.
         while (tokens.IsNext([RW.ABSTR,RW.EXT,RW.INV,RW.IMPORT,RW.EXPORT])) {
            // Provided interfaces
            ParseInterfaceList(RW.ABSTR,currentSection.abstr);
            ParseInterfaceList(RW.EXT,currentSection.ext);
            ParseInterfaceList(RW.EXPORT,currentSection.export);
            // Required interfaces
            ParseInterfaceList(RW.INV,currentSection.inv);
            ParseInterfaceList(RW.IMPORT,currentSection.import);
         }
      }

      /// <summary>
      /// Parse a simple list of IDs occurring in interfaces.
      /// It is OK for the list to be completely absent
      /// </summary>
      /// <param Id="interfaceType"></param>
      /// <param Id="idList">The container interface list.</param>
      /// <returns></returns>
      private void ParseInterfaceList(RW interfaceType,ICollection<ID> idList) {
         if (tokens.Consume(interfaceType)) {
            ParseIDList(interfaceType,idList);
         }
      }

      private void ParseLudes(Container container) {
         container.LudeParser(this,RW.PRELUDE,container);
         container.LudeParser(this,RW.ROOT,container);
         container.LudeParser(this,RW.POSTLUDE,container);
      }

      internal static void ParseLudeOfIDs(Parser parser,RW type,Container container) {
         if (parser.tokens.Optional(type)) {
            while (parser.tokens.Optional(TT.ID,out Token idToken)) {
               ID id = ID.From(idToken);
               if (container.Ludes[type].Contains(id)) {
                  parser.ReportWarning($"Duplicate {type} {id} ignored");
               } else { 
                  container.Ludes[type].Add(id);
               }
               if (!parser.tokens.CanConsumeSep()) break;
            }
            parser.tokens.CanConsumeEnd();
         }
      }

      /// <summary>
      /// Parse a SectionById lude. This is an alternative (i.e., a sequence of calls, without the other serializationOptions for the last call) terminated by a period.
      /// It will be stored as a Procedure. The ID will be SectionName_LudeType. 
      /// </summary>
      /// <param Id="parser"></param>
      /// <param Id="LudeType"></param>
      /// <param Id="container"></param>
      internal static void ParseLudeOfCalls(Parser parser,RW ludeType,Container container) {
         if (parser.tokens.Optional(ludeType)) {
            //Debug.Assert(container != null);
            Section section = (Section)container;
            Procedure lude = new(ludeType,section);

            Alternative alternative = new(parser.ParseNotes(),lude.group);

            while (parser.tokens.Optional(TT.ID,out Token id)) {
               alternative.calls.Add(ParseCall(parser,ID.From(id),lude,alternative));
               if (!parser.tokens.CanConsumeSep())
                  break;
            }
            parser.tokens.CanConsumeEnd();
            if (alternative.calls.Count >= 1) {
               alternative.NormalizeCalls();
            } else {
               parser.AddNote(container,Note.EmptyLude,ludeType);
            }

            lude.AlgorithmType = alternative.calls.All(call => call.HasEffect) ? RW.ACTION : RW.FUNCTION;
            lude.group.Alternatives.Add(alternative);
            section.Ludes[ludeType].Add(lude.Id);
            section.Declarations[lude.Id] = lude.GUID;
         }
      }


      /// <summary>
      /// Parse a list of IDs. The list is terminated by a period. The lists are normally sets.
      /// The lists cannot contain duplicates
      /// </summary>
      /// <param Id="idList"></param>
      /// <param Id="idList2"></param>
      /// <param Id="processID"></param>
      private void ParseIDDeclarationList(Section.DeclarationDictionary declarations,string comments,Func<ID,CDL2Object?> getObject,Notes notes) {
         NamedElement? firstObject = null;
         while (tokens.IsNext(TT.ID)) {
            ID id = ID.From(tokens.Next());
            CDL2Object? CDL2Object = getObject(id);
            if (CDL2Object != null && declarations.TryAdd(id,CDL2Object)) {
               firstObject ??= (NamedElement)CDL2Object;
            }

            if (!tokens.CanConsumeSep())
               break;
         }
         tokens.CanConsumeEnd();
         if (firstObject != null) {
            firstObject.Comments = comments;
            firstObject.AddNotes(PhaseName,notes);
         }
      }

      /// <summary>
      /// Parse plain list of IDs. Interface lists, PARTs and VARs.
      /// </summary>
      /// <param PhaseName="idList"></param>
      /// <param PhaseName="idList2"></param>
      private void ParseIDList(RW type,ICollection<ID> idList) {
         while (tokens.IsNext(TT.ID)) {
            ID id = ID.From(tokens.Next());
            if (!idList.Contains(id)) {
               idList.Add(id);
            } else {
               ReportWarning($"Duplicate {type} {id} ignored");
            }
            if (!tokens.CanConsumeSep())
               break;
         }
         tokens.CanConsumeEnd();
      }

      private void ReportError(string message,bool suppressErrorAction = false)    => ErrorReporter(Severity.Error,message,suppressErrorAction);
      private void ReportError(string message) => ErrorReporter(Severity.Error,message,false);
      private void ReportWarning(string message) => ErrorReporter(Severity.Warning,message,false);
      private void ReportInfo(string message) => ErrorReporter(Severity.Info,message,false); 

      internal void SkipToNextEnd() {
         while (!tokens.IsNext(TT.END))
            tokens.Skip();
         tokens.Skip(); // The end itself
      }

      /// <summary>
      /// Parse the tokens stream. Add it to the parse tree in the context of the focus.
      /// Return the resulting element or null if there was an error.
      /// </summary>
      /// <param name="context"></param>
      /// <param name="element"></param>
      /// <param name="canReplace">A function which returns true if the element can be replaced.</param>
      /// <param>The original input string. Used only as debug aid dureing development.</param>
      /// <returns></returns>
      /// <param name="replace"></param>
      internal bool Parse(Focus context,out NamedElement? element,Func<bool> canReplace,string input) {
         element = null;
         if (tokens.Peek().type != TokenType.RESWORD) { 
            ReportError($"Expected a reserved word at the start of input, not \"{tokens.Peek()}\".");
            return false;
         }


         Token initialToken = tokens.Peek();
         RW objectType = initialToken.reservedWordValue ?? RW.NONE;
         string comments = initialToken.Comments ?? string.Empty;
         ID id;
         int after;
         switch (objectType) {
            case RW.PROGRAM:
               tokens.Skip(); // Consume the reserved word
               if (tokens.CanConsume(out id) && tokens.CanConsumeEnd()) {
                  // We have a correct Module declaration. These are valid irrespective of the context.
                  after = context.IndexFor(objectType);
                  element = new Program(id,comments,after: after);
                  Focus.SetFocus(element);
               } else {
                  ReportError($"Expected ID and . after {RW.PROGRAM} reserved word.");
               }
               break;
            case RW.PART:
               tokens.Skip(); // Consume the reserved word
               // TODO: It must be possible to add the part(s) at an arbitrary position in the parts list.
               if (context.FocusType == SelectorType.PROGRAM) {
                  ParseIDList(RW.PART,(context.Object as Program)!.Parts);
                  element = context.Object;
               } else {
                  ReportError($"{RW.PART} declaration outside of {RW.PROGRAM} context");
               }
               break;
            case RW.MODULE:
               tokens.Skip(); // Consume the reserved word
               if (tokens.CanConsume(out id) && tokens.CanConsumeEnd()) {
                  // We have a correct Module or Program declaration. These are valid irrespective of the context.
                  after = context.IndexFor(objectType);
                  element = new Module(id,comments,after: after);
                  Focus.SetFocus(element);
               } else {
                  ReportError($"Expected ID and . after {RW.MODULE} reserved word.");
               }
               break;
            case RW.LAYER:
               tokens.Skip(); // Consume the reserved word
               if (context.FocusType == SelectorType.PROGRAM || context.FocusType == SelectorType.INVALID) {
                  ReportError($"Cannot add a {RW.LAYER} because I don't know which {RW.MODULE} to add it to.");
               } else if (tokens.CanConsume(out id) && tokens.CanConsumeEnd()) {
                  after = context.IndexFor(objectType);
                  Module module = context.Module!;
                  Layer? ancestor = Focus.Current.ObjectFor(RW.LAYER,module.Layers);
                  element = new Layer(id,module,ancestor,comments,after: after);
                  Focus.SetFocus(element);
               } else {
                  ReportError($"Expected ID and . after {RW.LAYER} reserved word.");
               }
               break;
            case RW.SECTION:
               tokens.Skip(); // Consume the reserved word
               if (context.FocusType == SelectorType.PROGRAM || context.FocusType == SelectorType.MODULE || context.FocusType == SelectorType.INVALID) {
                  ReportError($"Cannot add a {RW.SECTION} because I don't know which {RW.SECTION} to add it to.");
               } else if (tokens.CanConsume(out id) && tokens.CanConsumeEnd()) {
                  after = context.IndexFor(objectType);
                  Module module = context.Module!;
                  Layer layer = Focus.Current.ObjectFor(RW.LAYER,module.Layers)!;
                  element = new Section(id,layer,comments,after: after);
                  Focus.SetFocus(element);
               } else {
                  ReportError($"Expected ID and . after {RW.SECTION} reserved word.");
               }
               break;
            case RW.FUNCTION:
            case RW.ACTION:
            case RW.TEST:
            case RW.PREDICATE:
               ReportInfo($"Parsing algorithm with input: {input}");
               // Verify that the section can be determined from the context.
               Section? section = context.Section;
               if (section is not null) {
                  currentSection = section;
                  ParseAlgorithm(Notes.Empty,out Algorithm? alg); element = alg;
               } else {
                  ReportError($"Cannot add an algorithm because I don't know which {RW.SECTION} to add it to. Context: {context}");
               }
               break;
            case RW.CONST:
               ReportInfo($"Parsing const with input: {input}");
               break;
            case RW.VAR:
               ReportInfo($"Parsing var with input: {input}");
               break;
            case RW.LIST:
               ReportInfo($"Parsing list with input: {input}");
               break;
            case RW.ABSTR:
            case RW.EXT:
            case RW.INV:
            case RW.EXPORT:
            case RW.IMPORT:
               break;
            case RW.ROOT:
            case RW.PRELUDE:
            case RW.POSTLUDE:
               if (context.FocusType == SelectorType.LAYER) {
                  ReportError($"Layers don't have {objectType}s");
               } else {
                  switch (context.FocusType) {
                     case SelectorType.MODULE:
                        ParseLudeOfIDs(this,objectType,(context.Object as Module)!);
                        // TODO: Produce warnings for ludes that don't have corresponding sections?
                        break;
                     case SelectorType.PROGRAM:
                        ParseLudeOfIDs(this,objectType,(context.Object as Program)!);
                        // TODO: Produce warnings for ludes that don't have corresponding modules?
                        break;
                     default:
                        // Either the context is a Section or it is inside a section.
                        section = context.Object as Section ?? (context.Object as CDL2Object)?.Section;
                        Debug.Assert(section != null,"Expected a section context for ROOT, PRELUDE, or POSTLUDE.");
                        ParseLudeOfCalls(this,objectType,section);
                        break;
                  }
               }
               break;
            case RW.NOTE:
               break;

            case RW.NONE:
               break;
         }

         return element != null;
      }
   }
}