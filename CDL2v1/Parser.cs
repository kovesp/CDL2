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

using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using static CDL2v1.Logger;

namespace CDL2v1 {
   public partial class Parser : CompilationPhase {
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
         [RW.PROGRAM] = typeof(Program),
         [RW.MODULE] = typeof(Module),
         [RW.LAYER] = typeof(Layer),
         [RW.SECTION] = typeof(Section),
         [RW.CONST] = typeof(Const),
         [RW.LIST] = typeof(LIST),
         [RW.VAR] = typeof(Var),
         [RW.TEST] = typeof(Algorithm),
         [RW.PREDICATE] = typeof(Algorithm),
         [RW.FUNCTION] = typeof(Algorithm),
         [RW.ACTION] = typeof(Algorithm),
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

      public override void ReportNoteCounts(Reachable? reachable,string? message = null,Action<string>? reporter = null,bool summaryOnly=false) {
         Lexer!.ReportNoteCounts(reachable,message,reporter,summaryOnly);
         base.ReportNoteCounts(reachable,message,reporter,summaryOnly);
      }


      /// <summary>
      /// Recursive descent parser for CDL2.
      /// </summary>
      /// <param Id="tokens"></param>
      /// <exception cref="Exception"></exception>
      internal void Parse(string filePath) => ParseString(File.ReadAllText(filePath));
      public List<ITopLevelContainer> ParseString(string input) {
         input = NormalizeLineEndRE().Replace(input,"\n");
         return Tokenize(input,ParseMode.Full) ? ParseTokens() : [];
      }

      public bool Tokenize(string input,ParseMode mode) {
         tokens = new TokenList(mode == ParseMode.Full ? ReportInvalidToken : (_,_,_) => { });
         return (Lexer = new LexicalAnalyzer(Compiler,tokens)).Tokenize(input,mode);
      }

      public List<ITopLevelContainer> ParseTokens() {
         Logger.logger.ErrorAction = SkipToNextEnd;
         Logger.logger.CurrentObject = currentObject;

         List<ITopLevelContainer> parsedContainers = [];

         while (tokens.IsNonEmpty()) {
            Notes notes = ParseNotes();
            ID unitId = ID.ErrorID;
            if (tokens.CanConsumeContainerDelimiter(RW.MODULE,ref unitId,out string? comments)) {
               parsedContainers.AddNonNull(ParseModule(unitId,comments,notes));
            } else if (tokens.CanConsumeContainerDelimiter(RW.PROGRAM,ref unitId,out comments)) {
               parsedContainers.AddNonNull(ParseProgram(unitId,comments,notes));
            } else {
               //throw new Exception("Expected MODULE or PROGRAM");
               break;
            }
         }

         Logger.logger.CurrentObject = null;
         Logger.logger.ErrorAction = null;
         return parsedContainers!;
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
      private Program? ParseProgram(ID programId,string comments,Notes notes) {
         if (Database.Instance.IsNamedElement<Program>(programId)) {
            AddNote(Note.DuplicateContainer,programId.Name);
            return null;
         } else {
            currentObject.Object = (RW.PROGRAM, programId);
            currentProgram = new Program(programId,comments,notes);
            LogParseObject(1,ParseMode.Full,currentProgram);
         }

         if (tokens.CanConsume(RW.PART)) {
            ParseIDList(RW.PART,currentProgram.Parts);
         }

         ParseLudes(currentProgram);
         // Consume the ENDPROG token
         tokens.CanConsumeContainerDelimiter(RW.ENDPROG,ref programId,out _);
         return currentProgram;
      }
      /// <summary>
      /// Parse (and skip) NOTEs.
      /// </summary>
      private Notes ParseNotes(bool needsEnd = true) {
         Notes notes = [];
         while (tokens.CanConsumeNote(out Note? note,needsEnd: needsEnd)) {
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
      private Module? ParseModule(ID moduleId,string comments,Notes notes) {
         if (Database.Instance.IsNamedElement<Module>(moduleId)) {
            AddNote(Note.DuplicateContainer,moduleId.Name);
            return null;
         } else {
            currentObject.Object = (RW.MODULE,moduleId);
            currentModule = new Module(moduleId,comments,notes);
            LogParseObject(1);
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
         return currentModule;
      }

      private void LogParseObject(int level,ParseMode mode=ParseMode.Full,params object[] insertions) {
         if (mode == ParseMode.Full) {
            if (insertions.Length == 0) {
               Log(level,$"Parsing {currentObject}");
            } else {
               Log(level,$"Parsing {string.Join(" ",insertions)}");
            }
         }
      }
      private void ParseLayer(ID layerId,string comments,Notes notes) {
         Debug.Assert(currentModule != null);
         currentObject.Object = (RW.LAYER, layerId);
         currentLayer = new Layer(layerId,currentModule,currentLayer,comments,notes);
         LogParseObject(2);

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
      private void ParseSection(ID sectionId,string comments,Notes notes,ParseMode mode = ParseMode.Full) {
         Debug.Assert(currentLayer != null);
         currentObject.Object = (RW.SECTION, sectionId);
         currentSection = new Section(sectionId,currentLayer,comments,notes);
         LogParseObject(3,mode);

         // Now should see container parts
         // Interfaces first
         ParseInterfaces();

         // Now could see algorithms, lists, variables, constants in any order.
         // Parse each LudeType and return its ID.
         while (!tokens.IsNext(RW.ENDSEC)) {
            Notes internalNotes = ParseNotes();
            bool cont;
            if (tokens.IsNext(AlgTypes)) {
               cont = ParseAlgorithm(internalNotes,out _,mode);
            } else if (tokens.IsNext(RW.LIST)) {
               cont = ParseList(internalNotes,out _,mode);
            } else if (tokens.IsNext(RW.VAR)) {
               cont = ParseVariables(internalNotes,mode,out _,null);
            } else if (tokens.IsNext(RW.CONST)) {
               cont = ParseConstants(internalNotes,mode,out _,null,null);
            } else {
               if (mode == ParseMode.Full) ReportError($"Expected FUNCTION, ACTION, TEST, PREDICATE, LIST, VAR, or CONST. Seeing {tokens.Peek()}");
               cont = false;
            }
            if (!cont) return;
         }

         // Consume the ENDSEC
         tokens.CanConsumeContainerDelimiter(RW.ENDSEC,ref sectionId,out _);
         // Now could see prelude, root, postlude in that order.
         ParseLudes(currentSection);
      }

      private static readonly List<TT> bodyTypes = [TT.PROCINLINEBODY,TT.MACROPROCBODY,TT.MACROBODY,TT.PROCBODY];
      private bool ParseAlgorithm(Notes notes,[NotNullWhen(true)] out Algorithm? algorithm,ParseMode mode = ParseMode.Full,
            Func<NamedElement,bool>? canReplace = null,NamedElement? contextObj = null) {
         Debug.Assert(currentSection != null);
         algorithm = null;
         if (tokens.CanConsume(AlgTypes,out Token algType) && tokens.CanConsume(out ID id)) {
            LogParseObject(4,mode,algType,id);
            RW algTypeRW = algType.reservedWordValue ?? RW.FUNCTION;
            currentObject.Object = (algTypeRW, id);
            if (!ParseAffixes(mode,out List<Affix> affixes)) return false;
            bool isImported = currentSection.Interfaces[InterfaceTypes.Import].Contains(id);
            bool importAdded = false;
            if (tokens.Optional(TT.END)) {
               // IMPORT declaration. Check if it is in the imports list.
               if (!isImported && mode == ParseMode.Full) {
                  if (contextObj is not null) {
                     // We are in Lab mode and the object is not imported but this is an import declaration
                     importAdded = Database.Instance.CLI.QueryBox($"You have entered an import declaration for {algType} {id}, but it is not imported. Add to IMPORT list?");
                     if (!importAdded) {
                        // this OK, user can add it later, or semantic analyzer will catch it.
                        if (mode == ParseMode.Full) AddNote(currentSection,Note.ObjectNotImported,$"{algType} {id}");
                     }
                  } else {
                     // We are in compiler mode, so just report the error
                     if (mode == ParseMode.Full) AddNote(currentSection,Note.ObjectNotImported,id);
                     return false;
                  }
               }
               algorithm = new ImportedAlgorithm(id,affixes,algType,currentSection);
               algorithm.AddNotes(PhaseName,notes);
               if (importAdded) {
                  Database.Instance.RecordUndo(algorithm,ChangeType.InterfaceChanged); // Record unimported state
                  currentSection.Interfaces[InterfaceTypes.Import].Add(id);
                  Database.Instance.RecordUndoSetSwap(); // Because the import must be undone first.
               }
            } else {
               // Full declaration with body
               if (!ParseLocals(mode,out Set<Local> locals)) return false;
               if (tokens.CanConsume(bodyTypes,out Token bodyType)) {
                  if (bodyType.type == TT.PROCBODY || bodyType.type == TT.PROCINLINEBODY) {
                     // Parse the code body
                     algorithm = new Procedure(id,affixes,locals,algType,bodyType.type,currentSection);
                     algorithm.AddNotes(PhaseName,notes);
                     if (!ParseProcedureBody((Procedure)algorithm,mode)) {
                        if (mode == ParseMode.Full) {
                           if (mode == ParseMode.Full) ReportProblem(Note.ParseExpectedProcBody);
                        }
                        return false;
                     }
                  } else {
                     // Parse the macro body
                     algorithm = new Macro(id,affixes,locals,algType,bodyType.type,currentSection);
                     algorithm.AddNotes(PhaseName,notes);
                     if (!ParseMacroBody((Macro)algorithm,mode)) {
                        if (mode == ParseMode.Full) {
                           if (mode == ParseMode.Full) ReportProblem(Note.ParseExpectedMacroBody);
                        }
                        return false;
                     }
                  }
               }
               Debug.Assert(algorithm != null);
               if (mode == ParseMode.Full && isImported) {
                  if (contextObj is not null) {
                     // We are in Lab mode and the object is imported but has a body ... silently remove the import
                     Database.Instance.RecordUndo(algorithm,ChangeType.InterfaceChanged);
                     currentSection.Interfaces[InterfaceTypes.Import].Remove(id);
                  } else {
                     // We are in compiler mode, so just report the error
                     if (mode == ParseMode.Full) AddNote(currentSection,Note.ObjectImportedButHasBody,algorithm);
                     if (mode == ParseMode.Full) ReportProblem(Note.ObjectImportedButHasBody,algorithm);
                     return false;
                  }
               }
            }
            if (mode == ParseMode.Full) {
               bool duplicate = DuplicateDeclaration(id,algTypeRW,report: contextObj is null); // Do not report the problem in Lab mode
               if (duplicate) {
                  Guid currentGuid = currentSection.Declarations[id];
                  CDL2Object currentObject = currentGuid.ToNamedElement<CDL2Object>()!;
                  if (canReplace?.Invoke(currentObject) == false) return false;
                  // Replace the existing declaration, but record an undo
                  if (contextObj is not null) currentObject.Replace(algorithm);
               } else {
                  // Record an add
                  Database.Instance.RecordUndo(algorithm,contextObj,ChangeType.Added);
                  currentSection.Declarations[id] = algorithm.GUID;
               }
            }
         } else {
            if (mode == ParseMode.Full) ReportError("Expected FUNCTION, ACTION, TEST, or PREDICATE (this should be impossible");
         }

         return algorithm != null;
      }

      private bool DuplicateDeclaration(ID id,RW type,bool report = true) {
         if (currentSection!.Declarations.TryGetValue(id,out CDL2Object? value)) {
            if (report) {
               AddNote(currentSection,Note.DuplicateDeclaration,$"{type} {id}",value!);
               ReportProblem(Note.DuplicateDeclaration,$"{type} {id}",value?.ToString() ?? "");
            }
            return true;
         }
         return false;
      }

      private bool ParseMacroBody(Macro macro,ParseMode mode) {
         ParseElementList(macro,macro.Elements,"ID, Affix, Local, STRING, INT, or FLOAT",mode);
         IElement? first = macro.Elements.FirstOrDefault();
         if (first is not null && first is STRING str) macro.Elements[0] = new STRING(Regex.Replace(str.value,"^ *\n","",RegexOptions.Compiled));

         if (!tokens.CanConsume(TT.END)) {
            if (mode == ParseMode.Full) ReportError("Expected .");
            return false;
         } else {
            return true;
         }
      }

      private int GroupCounter;
      public const string GroupIDLabelPrefix = "G";
      private void SetGroupId(Group group) {
         if (group.Id == ID.AnonID) group.Id = new ID($"{GroupIDLabelPrefix}{GroupCounter++}");
      }

      private bool ParseProcedureBody(Procedure proc,ParseMode mode) {
         GroupCounter = 0;
         int alternativeCounter = 1;
         Database.Instance.ClearUnrecordedNamedElements();
         SetGroupId(proc.group);
         if (!ParseAlternatives(proc,group: proc.group,mode: mode,out proc.group.Alternatives)) {
            if (mode == ParseMode.Full) ReportError("Expected alternatives");
            return false;
         }
         if (!tokens.CanConsume(TT.END)) {
            if (mode == ParseMode.Full) ReportError("Expected .");
            return false;
         }
         NumberAlternatives(proc.group,Alternative.ALTERNATIVES_END);
         return true;

         void NumberAlternatives(Group group,int groupSucceesor) {
            Alternative last = group.Alternatives.Last();
            foreach (Alternative alt in group.Alternatives) {
               alt.NextAlternativeNumber = 1 +(alt.AlternativeNumber = alternativeCounter++);
            }
            last.NextAlternativeNumber = groupSucceesor;
            foreach (Alternative alt in group.Alternatives) {
               if (alt.LastCall.type == LCT.Group) {
                  NumberAlternatives(alt.LastCall.group!,alt.NextAlternativeNumber);
               }
            }
         }
      }



      /// <summary>
      /// Parse alternatives.
      /// Note that a next alternative number is assigned to each alternative. This is
      /// for the entire procedure and is incremented for each alternative. It is also set for the last alternative of each group.
      /// This is used to generate labels for alternatives and to be also able to generate a goto to the next alternative when the current one fails.
      /// </summary>
      /// <param name="proc"></param>
      /// <param name="group"></param>
      /// <param name="mode"></param>
      /// <param name="alternatives"></param>
      /// <returns></returns>
      private bool ParseAlternatives(Procedure proc,Group group,ParseMode mode,out List<Alternative> alternatives) {
         alternatives = [];
         do {
            Notes notes = ParseNotes(needsEnd: false);
            if (ParseAlternative(proc,group,notes,mode,out Alternative alternative)) {
               alternatives.Add(alternative);
            } else {
               if (mode == ParseMode.Full) ReportError("Expected alternative");
               return false;
            }
         } while (tokens.Optional(TT.ALTSEP));
         return alternatives.Count != 0;
      }

      private bool ParseAlternative(Procedure proc,Group group,Notes notes,ParseMode mode,out Alternative alternative) {
         alternative = new(notes,group);
         do {
            if (alternative.LastCall.type != LCT.None) {
               // If we have a last call, then we should NOT have seen a separator
               if (mode == ParseMode.Full) ReportProblem(Note.UnexpectedSeparator);
               return false;
            } else if (tokens.Optional(RW.BUILTIN) && tokens.Optional(out ID id)) {
               if (ParseCall(id,proc,alternative,mode,out Call? call,builtin: true)) {
                  alternative.Calls.Add(call);
               } else {
                  if (mode == ParseMode.Full) ReportProblem(Note.ExpectedBuiltinId);
                  return false;
               }
            } else if (tokens.Optional(out id)) {
               if (ParseCall(id,proc,alternative,mode,out Call? call)) {
                  alternative.Calls.Add(call);
               } else {
                  if (mode == ParseMode.Full) ReportProblem(Note.ExpectedCall);
                  return false;
               }
            } else if (tokens.Optional(TT.SUCCEED)) {
               alternative.LastCall = new LastCall(LCT.Succeed,alternative);
            } else if (tokens.Optional(TT.FAIL)) {
               alternative.LastCall = new LastCall(LCT.Fail,alternative);
               if (!proc.CanFail) {
                  if (mode == ParseMode.Full) {
                     if (mode == ParseMode.Full) AddNote(proc,Note.IllegalFailOperator,proc.AlgorithmType);
                     if (mode == ParseMode.Full) ReportProblem(Note.TestContainsFail,proc);
                  }
                  return false;
               }
            } else if (tokens.Optional(TT.ABORT)) {
               alternative.LastCall = new LastCall(LCT.Abort,alternative);
            } else if (tokens.Optional(TT.REPEAT)) {
               if (tokens.Optional(out ID label)) {
                  if (mode == ParseMode.Full && !group.HasLabeledAncestorGroup(label)) {
                     if (mode == ParseMode.Full) AddNote(proc,Note.LabelNotFound,label.Name,proc);
                  }
                  alternative.LastCall = new LastCall(label,alternative);
                  if (id == proc.Id) proc.repeatsProcedure = true;
               } else {
                  alternative.LastCall = new LastCall(LCT.Repeat,alternative);
               }
            } else if (tokens.Optional(TT.GRPOPEN)) {
               if (!ParseGroup(proc,containingGroup: group,containingAlternative: alternative,mode,out LastCall? grp)) {
                  if (mode == ParseMode.Full) ReportProblem(Note.ExpectedGroup);
                  return false;
               }
               alternative.LastCall = grp;
            } else {
               if (mode == ParseMode.Full) ReportProblem(Note.ExpectedLastCall);
               return false;
            }
         } while (tokens.Optional(TT.CALLSEP));
         alternative.NormalizeCalls();
         return true;
      }

      private bool ParseCall(ID id,Procedure proc,Alternative containingAlternative,ParseMode mode,
         [NotNullWhen(true)] out Call? call,bool builtin = false)
         => ParseCall(this,id,proc,containingAlternative,mode,out call,builtin);
      private static bool ParseCall(Parser parser,ID id,Procedure containingProc,Alternative containingAlternative,
            ParseMode mode,[NotNullWhen(true)] out Call? call,bool builtin = false) {
         Debug.Assert(parser.currentSection != null);
         call = new(id,containingProc,containingAlternative,builtin);
         if (ParseActualArgs(parser,call,containingProc,mode)) {
            if (builtin) {
               if (Builtin.IsFunction(call,out Local? local)) {
                  if (!local!.IsBuiltinResult) {
                     local.BuiltinCallGuid = call.GUID;
                  } else {
                     if (mode == ParseMode.Full) parser.ReportProblem(Note.BuiltinResultReused,call.Id.Name);
                     call = null;
                  }
               } else if (!Builtin.IsTest(call)) {
                  if (mode == ParseMode.Full) parser.ReportProblem(Note.UnknownBuiltin,call.Id.Name);
                  call = null;
               }
            }
         } else {
            call = null;
         }
         return call is not null;
      }

      private bool ParseGroup(Procedure proc,Group containingGroup,Alternative containingAlternative,ParseMode mode,
            [NotNullWhen(true)] out LastCall? lastCall) {
         lastCall = null;
         if (!ParseOptionalLabel(containingGroup,proc,mode,out ID? label)) {
            if (mode == ParseMode.Full) ReportError("Invalid label");
            return false;
         }
         Group group = new(label,[],containingAlternative.GUID,synthetic: label is null);
         SetGroupId(group);
         if (!ParseAlternatives(proc,group,mode,out group.Alternatives)) {
            if (mode == ParseMode.Full) ReportError("Expected alternatives in group");
            return false;
         }
         if (!tokens.CanConsume(TT.GRPCLOSE)) {
            if (mode == ParseMode.Full) ReportError("Expected )");
            return false;
         }
         lastCall = new LastCall(group,containingAlternative);
         return true;
      }

      private bool ParseOptionalLabel(Group group,Procedure proc,ParseMode mode,out ID? label) {
         if (tokens.Peek().type == TT.ID && tokens.Peek(1).type == TT.LABELSEP) {
            // Consume the label and the colon
            label = ID.From(tokens.Next());
            tokens.Next();
            // Go up the group hierarchy to see if the label is already defined.
            if (mode == ParseMode.Full && group.HasLabeledAncestorGroup(label)) {
               if (mode == ParseMode.Full) AddNote(proc,Note.DuplicateLabel,label.Name);
               return false;
            } else {
               return true;
            }
         }
         label = null;
         return true;
      }

      /// <summary>
      /// Parse the actual arguments of a call.
      /// Actual arguments are a sequence of IDs or strings separated by '+'.
      /// </summary>
      /// <param Id="call"></param>
      private static bool ParseActualArgs(Parser parser,Call call,Procedure proc,ParseMode mode) {
         Debug.Assert(parser.currentSection != null);
         while (parser.tokens.Optional(TT.AFFIXSEP)) {
            if (parser.tokens.Optional(out ID id)) {
               call.argRefs.Add(id);
            } else if (parser.tokens.CanConsume(TT.STRING,out Token str)) {
               call.argRefs.Add(new STRING(str));
            } else {
               if (mode == ParseMode.Full) parser.ReportError("Expected ID or STRING");
               return false;
            }
         }
         return true;
      }

      /// <summary>
      /// Parse the locals in an algorithm declaration.
      /// </summary>
      /// <param name="mode"></param>
      /// <param name="locals"></param>
      /// <returns></returns>
      private bool ParseLocals(ParseMode mode,out Set<Local> locals) {
         locals = [];
         while (tokens.Optional(TT.LOCALSEP) && tokens.CanConsume(TT.ID,out Token token)) {
            Local local = new(ID.From(token));
            if (!locals.Add(local)) {
               if (mode == ParseMode.Full) ReportProblem(Note.PareseDuplicateLocal,$"{local.Id.Name}");
               return false;
            }
         }
         return true;
      }

      private static readonly List<TT> formalTypes = [TT.AFFIXSEP,TT.STRINGAFFIXSEP];
      /// <summary>
      /// Parse the affixes in an algorithm declaration.
      /// </summary>
      /// <param name="mode"></param>
      /// <param name="args"></param>
      /// <returns></returns>
      private bool ParseAffixes(ParseMode mode,out List<Affix> args) {
         args = [];
         while (tokens.Optional(formalTypes,out Token affixTypeInd)) {
            bool isIn = tokens.Optional(TT.AFFIXDIR);
            if (tokens.CanConsume(out ID id)) {
               bool isOut = tokens.Optional(TT.AFFIXDIR);
               AffixDir affixDir = isIn ? (isOut ? AffixDir.transput : AffixDir.input) : (isOut ? AffixDir.output : AffixDir.NONE);
               AffixType affixType = affixTypeInd.type == TT.AFFIXSEP ? AffixType.std : AffixType.str;
               if (affixType == AffixType.str && affixDir != AffixDir.NONE) {
                  if (mode == ParseMode.Full) ReportProblem(Note.ParseArgStringWithDirection,$"{id.Name}");
                  return false;
               }
               if (affixType == AffixType.std && affixDir == AffixDir.NONE) {
                  if (mode == ParseMode.Full) ReportProblem(Note.ParseArgStdArgHasNoDirection,$"{id.Name}");
                  return false;
               }
               Affix affix = new(id,affixDir,affixType);
               if (args.Contains(affix)) {
                  if (mode == ParseMode.Full) ReportProblem(Note.ParseArgDuplicateArg,$"{id.Name}");
                  return false;
               } else {
                  args.Add(affix);
               }
            } else {
               if (mode == ParseMode.Full) {
                  ReportError("Expected ID for formal parameter");
                  ReportProblem(Note.ParseArgMissingId,$"{id.Name}");
               }
               return false;
            }
         }
         return true;
      }

      /// <summary>
      /// Parse a list of LIST declarations.
      /// </summary>
      /// <param name="notes"></param>
      /// <param name="lists"></param>
      /// <param name="mode"></param>
      /// <param name="canReplace"></param>
      /// <param name="context"></param>
      /// <returns></returns>
      private bool ParseList(Notes notes,out List<LIST> lists,ParseMode mode = ParseMode.Full,Func<NamedElement,bool>? canReplace = null,ParsingContext? context = null) {
         if (tokens.CanConsume(RW.LIST,out string? comments)) {
            Debug.Assert(currentSection != null);
            return ParseIDDeclarationList(currentSection.Declarations,comments!,(mode,id) => ParseListBody(id,mode,canReplace,context),notes,mode,out lists);
         }
         lists = [];
         return false;
      }

      /// <summary>
      /// Parse the body of a list declaration. Format is list-Id(lwb:upb).
      /// <param Id="token"></param>
      /// <exception cref="Exception"></exception>
      private LIST? ParseListBody(ID id,ParseMode mode,Func<NamedElement,bool>? canReplace = null,ParsingContext? context = null) {
         Debug.Assert(currentSection != null);
         bool isDuplicate = DuplicateDeclaration(id,RW.LIST,report: context is null);
         if (isDuplicate && mode != ParseMode.Full) return null;

         LIST? list = null;
         if (tokens.Optional(TT.LISTBOUNDSTART) &&
               tokens.CanConsume(TT.ID,out Token lwbToken) &&
               tokens.CanConsume(TT.LISTBOUNDSEP) &&
               tokens.CanConsume(TT.ID,out Token upbToken) &&
               tokens.CanConsume(TT.LISTBOUNDEND)) {
            list = new(id,currentSection,ID.From(lwbToken),ID.From(upbToken));
            if (mode == ParseMode.Full) {
               if (isDuplicate) {
                  if (context is not null && canReplace?.Invoke(list) == false) return null;
                  currentSection.Declarations[id].ToNamedElement<LIST>()?.Replace(list);
                  Database.Instance.RecordUndo(currentSection.Declarations[id],list.GUID,ChangeType.Replaced);
               } else {
                  Database.Instance.RecordUndo(list,context: context?.Focus.Object,changeType: ChangeType.Added);
                  currentSection.Declarations[id] = list.GUID; // ← Protected!
               }
            }
         } else if (mode == ParseMode.Full) {
            AddNote(currentSection,Note.InvalidListBounds,id);
            ReportProblem(Note.InvalidListBounds,id);
         }
         return list;
      }

      /// <summary>
      /// Parse a var declaration.
      /// </summary>
      private bool ParseVariables(Notes notes,ParseMode mode,out List<Var> vars,ParsingContext? context) {
         Debug.Assert(currentSection != null);
         if (tokens.CanConsume(RW.VAR,out string? comments)) {
            return ParseIDDeclarationList(currentSection.Declarations,comments!,(mode,id) => ParseVar(mode,id,context),notes,mode,out vars);
         } else {
            if (mode == ParseMode.Full) ReportProblem(Note.ExpectedId);
            vars = [];
            return false;
         }
      }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="mode"></param>
      /// <param name="id"></param>
      /// <returns></returns>
      private Var? ParseVar(ParseMode mode,ID id,ParsingContext? context) {
         Debug.Assert(currentSection != null);
         if (mode == ParseMode.Full && DuplicateDeclaration(id,RW.VAR,report: context is null)) return null;
         Var v = new(id,currentSection);
         if (mode == ParseMode.Full) {
            currentSection.Declarations[id] = v.GUID;
            Database.Instance.RecordUndo(v, context?.Focus.Object, ChangeType.Added);
         }
         return v;
      }

      /// <summary>
      /// Parse a constant declaration.
      /// </summary>
      private bool ParseConstants(Notes notes,ParseMode mode,out List<Const> consts,Func<NamedElement,bool>? canReplace,ParsingContext? context) {
         Debug.Assert(currentSection != null);
         if (tokens.CanConsume(RW.CONST,out string? comments)) {
            Debug.Assert(currentSection != null);
            return ParseIDDeclarationList(currentSection.Declarations,comments!,(mode,id) => ParseConstBody(id,mode,canReplace,context),notes,mode,out consts);
         } else {
            consts = [];
            return false;
         }
      }

      /// <summary>
      /// Parse the body of a constant declaration. At this point the ID has been consumed.
      /// We should see an '=' followed by a sequence of constant elements (e.g., numbers, strings, etc.) terminated by a period or a comma.
      /// The terminator will be consumed by <see cref="ParseIDList(ICollection{ID}, ICollection{ID}?, Action{ID}?)
      /// </summary>
      /// <param Id="token">The token of the constant.</param>
      private Const? ParseConstBody(ID id, ParseMode mode, Func<NamedElement, bool>? canReplace, ParsingContext? context) {
          Debug.Assert(currentSection != null);
          bool isDuplicate = DuplicateDeclaration(id, RW.LIST, report: context is null);
          bool isImported = currentSection.Interfaces[InterfaceTypes.Import].Contains(id);

          Const c;
          if (tokens.Optional(TT.EQUALS)) {
              c = new(id, currentSection);
              ParseElementList(c, c.elements, "ID, STRING, INT, or FLOAT", mode, secondaryTerminator: TT.SEP);
          } else {
              c = new ImportedConst(id, currentSection);
          }
    
          if (mode == ParseMode.Full) {
              if (isDuplicate) {
                  if (context is not null && canReplace?.Invoke(c) == false) return null;
                  currentSection.Declarations[id].ToNamedElement<Const>()?.Replace(c);
              } else {
                  Database.Instance.RecordUndo(c, context: context?.Focus.Object, changeType: ChangeType.Added);
              }
              currentSection.Declarations[id] = c.GUID;
        
              // Handle import status...
              if (isImported) {
                  // ... existing import handling code
              } else if (c is ImportedConst) {
                  // ... existing import handling code
              }
          }
    
          return c;
      }
      /// <summary>
      /// Parse the elements of a constant or macro declaration.
      /// </summary>
      /// <param Id="c"></param>
      /// <exception cref="Exception"></exception>
      private bool ParseElementList(NamedElement parent,List<IElement> elements,string expected,ParseMode mode,TT secondaryTerminator = TokenType.END) {
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
               if (mode == ParseMode.Full) {
                  AddNote(parent,Note.UnexpectedToken,expected,tokens.Peek().ToString());
                  ReportProblem(Note.UnexpectedToken,expected,tokens.Peek().ToString());
               }
               return false;
            }
         }
         return true;
      }

      /// <summary>
      /// Parse the interfaces of a container.
      /// </summary>
      private void ParseInterfaces() {
         Debug.Assert(currentSection != null && currentLayer != null && currentModule != null);
         // The interface can be in any order.
         while (tokens.IsNext([RW.ABSTR,RW.EXT,RW.INV,RW.IMPORT,RW.EXPORT])) {
            // Provided interfaces
            ParseInterfaceList(RW.ABSTR,currentSection.Interfaces[InterfaceTypes.Abstr]);
            ParseInterfaceList(RW.EXT,currentSection.Interfaces[InterfaceTypes.Ext]);
            ParseInterfaceList(RW.EXPORT,currentSection.Interfaces[InterfaceTypes.Export]);
            // Required interfaces
            ParseInterfaceList(RW.INV,currentSection.Interfaces[InterfaceTypes.Inv]);
            ParseInterfaceList(RW.IMPORT,currentSection.Interfaces[InterfaceTypes.Import]);
         }
      }

      /// <summary>
      /// Parse a simple list of IDs occurring in interfaces.
      /// It is OK for the list to be completely absent
      /// </summary>
      /// <param Id="interfaceType"></param>
      /// <param Id="idList">The container interface list.</param>
      /// <returns></returns>
      private List<ID> ParseInterfaceList(RW interfaceType,ICollection<ID> idList) {
         if (tokens.Consume(interfaceType)) {
            return ParseIDList(interfaceType,idList);
         } else {
            return [];
         }
      }

      private void ParseLudes(Container container) {
         container.LudeParser(this,RW.PRELUDE,container);
         container.LudeParser(this,RW.ROOT,container);
         container.LudeParser(this,RW.POSTLUDE,container);
      }

      internal static bool ParseLudeOfIDs(Parser parser,RW type,Container container,out List<ID> ids) {
         ids = [];
         if (parser.tokens.Optional(type)) {
            while (parser.tokens.Optional(TT.ID,out Token idToken)) {
               ID id = ID.From(idToken);
               if (container.Ludes[type].Contains(id)) {
                  parser.ReportProblem(Note.DuplicateLude,type,id,container);
               } else {
                  container.Ludes[type].Add(id);
                  ids.Add(id);
               }
               if (!parser.tokens.CanConsumeSep()) break;
            }
            parser.tokens.CanConsumeEnd();
         }
         return true;
      }
      internal static bool ParseLudeOfIDs(Parser parser,RW type,Container container) => ParseLudeOfIDs(parser,type,container,out List<ID> _);

      /// <summary>
      /// Parse a Section lude. This is an alternative (i.e., a sequence of calls, without the other options for the last call) terminated by a period.
      /// It will be stored as a Procedure. The ID will be the LudeType.
      /// The ID is added to the Container Ludes as a single element list while Procedure itself to the Section LudeProcs.
      /// </summary>
      /// <param Id="parser"></param>
      /// <param Id="LudeType"></param>
      /// <param Id="container"></param>
      internal static bool ParseLudeOfCalls(Parser parser,RW ludeType,Container container,ParseMode mode,out ID ludeId,out Guid ludeGuid) {
         ludeId = ID.AnonID;
         ludeGuid = Guid.Empty;
         if (parser.tokens.Optional(ludeType)) {
            //Debug.Assert(container != null);
            Section section = (Section)container;
            Procedure lude = new(ludeType,section);

            Alternative alternative = new(parser.ParseNotes(),lude.group);
            if (!parser.tokens.Optional(TT.ID,out Token id)) {
               if (mode == ParseMode.Full) parser.ReportProblem(Note.ExpectedCall);
               return false;
            }
            for ( ; ; ) {
               if (ParseCall(parser,ID.From(id),lude,alternative,ParseMode.Full,out Call? call)) {
                  alternative.Calls.Add(call);
               } else {
                  if (mode == ParseMode.Full) parser.ReportProblem(Note.ExpectedCall);
                  return false;
               }
               if (!parser.tokens.CanConsumeSep()) break;
               if (!parser.tokens.Optional(TT.ID,out id)) { // Must have a call start, so an ID
                  if (mode == ParseMode.Full) parser.ReportProblem(Note.ExpectedCall);
                  return false;
               }
            }

            if (!parser.tokens.CanConsumeEnd()) {
               if (mode == ParseMode.Full) parser.ReportProblem(Note.ExpectedPeriod);
               return false;
            }
            if (alternative.Calls.Count >= 1) {
               alternative.NormalizeCalls();
            } else {
               if (mode == ParseMode.Full) parser.AddNote(container,Note.EmptyLude,ludeType);
               if (mode == ParseMode.Full) parser.ReportProblem(Note.EmptyLude,ludeType);
            }

            lude.AlgorithmType = alternative.Calls.All(call => call.HasEffect) ? RW.ACTION : RW.FUNCTION;
            lude.LudeTpe = ludeType;
            lude.group.Alternatives.Add(alternative);
            section.Ludes[ludeType].Add(ludeId = lude.Id);
            section.LudeProcs[ludeType] = ludeGuid = lude.GUID;
         }
         return true;
      }
      internal static bool ParseLudeOfCalls(Parser parser,RW ludeType,Container container) => ParseLudeOfCalls(parser,ludeType,container,ParseMode.Full,out ID _,out Guid _);


      /// <summary>
      /// Parse a list of IDs. The list is terminated by a period. The lists are normally sets.
      /// The lists cannot contain duplicates
      /// </summary>
      /// <param Id="idList"></param>
      /// <param Id="idList2"></param>
      /// <param Id="processID"></param>
      private bool ParseIDDeclarationList<T>(Section.DeclarationDictionary declarations,string comments,
            Func<ParseMode,ID,T?> parseItem,Notes notes,ParseMode mode,out List<T> objectList) where T : CDL2Object {
         objectList = [];
         NamedElement? firstObject = null;
         while (tokens.IsNext(TT.ID)) {
            ID id = ID.From(tokens.Next());
            T? CDL2Object = parseItem(mode,id);
            if (CDL2Object != null) {
               firstObject ??= (NamedElement)CDL2Object;
               objectList.Add(CDL2Object);
            }

            if (!tokens.CanConsumeSep()) break;
         }
         if (!tokens.CanConsumeEnd()) {
            if (mode == ParseMode.Full) {
               ReportError($"Expected . after {firstObject?.Id}");
            }
            return false;
         }
         if (firstObject != null) {
            firstObject.Comments = comments;
            firstObject.AddNotes(PhaseName,notes);
         }
         return true;
      }

      /// <summary>
      /// Parse plain list of IDs. Interface lists, PARTs and VARs.
      /// </summary>
      /// <param PhaseName="idList"></param>
      /// <param PhaseName="idList2"></param>
      /// <returns>The list of ids parsed.</returns>
      private List<ID> ParseIDList(RW type,ICollection<ID> idList) {
         List<ID> result = [];
         while (tokens.IsNext(TT.ID)) {
            ID id = ID.From(tokens.Next());
            if (!idList.Contains(id)) {
               idList.Add(id);
               result.Add(id);
            } else {
               ReportProblem(Note.DuplicateInterfaceElementInSection,type,id);
            }
            if (!tokens.CanConsumeSep())
               break;
         }
         tokens.CanConsumeEnd();
         return result;
      }

      /// <summary>
      /// Report a problem using a Note. The ErrorReporter used is specific to the compiler and the lab.
      /// <remark>This does NOT attach the note to anything, that has to be done separatly if required.</remark>
      /// </summary>
      /// <param name="note"></param>
      /// <param name="args"></param>
      private void ReportProblem(Note note,params object[] args) => ErrorReporter(note.NoteType,note.FormattedText(args),false);
      private void ReportError(string message,bool suppressErrorAction = false) => ErrorReporter(Severity.Error,message,suppressErrorAction);


      internal void SkipToNextEnd() {
         while (!tokens.IsNext(TT.END))
            tokens.Skip();
         tokens.Skip(); // The end itself
      }

      /// <summary>
      /// Parse the token stream. Add tjh result to the parse tree in the context of the focus.
      /// Return the resulting element or null if there was an error.
      /// </summary>
      /// <param name="context"></param>
      /// <param name="element"></param>
      /// <param name="canReplace">A function which returns true if the element can be replaced.</param>
      /// <param>The original input string. Used only as debug aid dureing development.</param>
      /// <returns></returns>
      /// <param name="replace"></param>
      internal bool Parse(ParsingContext parsingContext,out NamedElement? element,Func<NamedElement,bool> canReplace,string input,ParseMode mode = ParseMode.Full) {
         element = null;
         if (tokens.Peek().type != TokenType.RESWORD) {
            if (mode == ParseMode.Full) ReportError($"Expected a reserved word at the start of input, not \"{tokens.Peek()}\".");
            return false;
         }

         Token initialToken = tokens.Peek();
         RW objectType = initialToken.reservedWordValue ?? RW.NONE;
         string comments = initialToken.Comments ?? string.Empty;
         ID id;
         int after;
         Focus context = parsingContext.Focus;
         switch (objectType) {
            case RW.PROGRAM:
               tokens.Skip(); // Consume the reserved word
               if (tokens.CanConsume(out id) && tokens.CanConsumeEnd()) {
                  // We have a correct Program declaration. These are valid irrespective of the context.
                  after = context.IndexFor(objectType);
                  element = new Program(id,comments,after: after);
                  SetModified(element,mode);
                  Focus.SetFocus(element);
               } else {
                  ReportError($"Expected ID and . after {RW.PROGRAM} reserved word.");
               }
               break;
            case RW.PART:
               tokens.Skip(); // Consume the reserved word
               if (context.FocusType == SelectorType.PROGRAM) {
                  ParseIDList(RW.PART,(context.Object as Program)!.Parts);
                  element = context.Object;
                  SetModified(element,mode);
               } else {
                  ReportError($"{RW.PART} declaration outside of {RW.PROGRAM} context");
               }
               break;
            case RW.MODULE:
               tokens.Skip(); // Consume the reserved word
               if (tokens.CanConsume(out id) && tokens.CanConsumeEnd()) {
                  // We have a correct Module declaration. These are valid irrespective of the context.
                  after = context.IndexFor(objectType);
                  element = new Module(id,comments,after: after);
                  SetModified(element,mode);
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
                  element = new Layer(id,context.Module!,context.Layer!,comments,after: context.IndexFor(objectType));
                  SetModified(element,mode);
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
                  element = new Section(id,context.Layer!,comments,after: context.IndexFor(objectType));
                  SetModified(element,mode);
                  Focus.SetFocus(element);
               } else {
                  ReportError($"Expected ID and . after {RW.SECTION} reserved word.");
               }
               break;
            case RW.FUNCTION:
            case RW.ACTION:
            case RW.TEST:
            case RW.PREDICATE:
               // Verify that the section can be determined from the context.
               Section? section = context.Section;
               if (section is not null) {
                  currentSection = section;
                  if (ParseAlgorithm(Notes.Empty,out Algorithm? alg,mode,canReplace,context.Object)) {
                     element = alg;
                     // alg was added to the current section at the end
                     if (mode == ParseMode.Full) MoveObjectToPosition(parsingContext,context,alg);
                     SetModified(element,mode);
                  }
               } else {
                  if (mode == ParseMode.Full) ReportProblem(Note.NoSectionForObject,context.ToString());
                  element = null;
               }
               break;
            case RW.CONST:
               section = context.Section;
               if (section is not null) {
                  currentSection = section;
                  if (ParseConstants(Notes.Empty,mode,out List<Const> consts,canReplace,parsingContext)) {
                     if (mode == ParseMode.Full) MoveObjectToPosition(parsingContext,context,consts);
                     element = consts.LastOrDefault();
                     SetModified(element!,mode);
                  }
               } else {
                  if (mode == ParseMode.Full) ReportProblem(Note.NoSectionForObject,context.ToString());
                  element = null;
               }
               break;
            case RW.VAR:
               section = context.Section;
               if (section is not null) {
                  currentSection = section;
                  if (ParseVariables(Notes.Empty,mode,out List<Var> vars,parsingContext)) {
                     MoveObjectToPosition(parsingContext,context,vars);
                     element = vars.LastOrDefault();
                     SetModified(element!,mode);
                  }
               } else {
                  if (mode == ParseMode.Full) ReportProblem(Note.NoSectionForObject,context.ToString());
                  element = null;
               }
               break;
            case RW.LIST:
               section = context.Section;
               if (section is not null) {
                  currentSection = section;
                  if (ParseList(Notes.Empty,out List<LIST> lists,mode,canReplace,parsingContext)) {
                     if (mode == ParseMode.Full) MoveObjectToPosition(parsingContext,context,lists);
                     element = lists.LastOrDefault();
                     SetModified(element!,mode);
                  }
               } else {
                  if (mode == ParseMode.Full) ReportProblem(Note.NoSectionForObject,context.ToString());
                  element = null;
               }
               break;
            case RW.ABSTR:
            case RW.EXT:
            case RW.INV:
            case RW.EXPORT:
            case RW.IMPORT:
               element = section = context.Section;
               if (section is not null) {
                  foreach (ID interfaceId in ParseInterfaceList(objectType,section.Interfaces[Container.InterfaceEnumByType[objectType]])) {
                     Database.Instance.RecordUndo(section,objectType,interfaceId,ChangeType.InterfaceAdded);
                  }
                  SetModified(element!,mode);
               } else if (mode == ParseMode.Full) {
                  ReportProblem(Note.NoSectionForObject,context.ToString());
               }
               break;
            case RW.ROOT:
            case RW.PRELUDE:
            case RW.POSTLUDE:
               if (context.FocusType == SelectorType.LAYER) {
                  if (mode == ParseMode.Full) ReportProblem(Note.InvalidLudeContext,objectType.ToString(),context.ToString());
                  return false;
               } else if (context.FocusType == SelectorType.SECTION) {
                  // Either the context is a Section or it is inside a section.
                  currentSection = context.Object as Section ?? (context.Object as CDL2Object)?.Section;
                  Debug.Assert(currentSection != null,"Expected a section context for ROOT, PRELUDE, or POSTLUDE.");
                  if (ParseLudeOfCalls(this,objectType,currentSection,mode,out ID ludeProcId,out Guid ludeGuid)) {
                     if (mode == ParseMode.Full) Database.Instance.RecordUndo(currentSection,objectType,ludeProcId,ludeGuid,ChangeType.LudeAdded);
                  } else {
                     if (mode == ParseMode.Full) ReportProblem(Note.InvalidLude,$"{objectType} in {currentSection.FQDN()}");
                     return false;
                  }
                  SetModified(currentSection,mode);
                  return true;
               } else {
                  // Otherwise it is a Program or a Module
                  Container container = (context.Object as Container)!;
                  if (ParseLudeOfIDs(this,objectType,container,out List<ID> ludeIds)) {
                     if (mode == ParseMode.Full) foreach (ID ludeId in ludeIds) Database.Instance.RecordUndo(container,objectType,ludeId,ChangeType.LudeAdded);
                     SetModified(container,mode);
                  } else {
                     if (mode == ParseMode.Full) ReportProblem(Note.InvalidLude,$"{objectType} in {context.Object!.FQDN()}");
                     return false;
                  }
               }
               break;
            case RW.NOTE:
               Notes notes = ParseNotes();
               if (context.Object is not null) {
                  if (mode == ParseMode.Full) {
                     context.Object.AddNotes(PhaseName,notes);
                  }
                  element = context.Object;
                  SetModified(element,mode);
               }
               break;

            case RW.NONE:
               break;
         }

         return element != null;
      }

      private static void SetModified(NamedElement element,ParseMode mode) {
         if (mode == ParseMode.Full) element.Modified = true;
      }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="parsingContext"></param>
      /// <param name="context"></param>
      /// <param name="obj"></param>
      public static void MoveObjectToPosition(ParsingContext parsingContext,Focus context,CDL2Object? obj) {
         if (context.FocusType != SelectorType.SECTION) {
            // The context is either the section (in which case the position is correct) or an object inside it.
            if (context.Object?.GUID == obj?.GUID) return;  // May happen when an object is replaced
            int pos = context.IndexFor();
            switch (parsingContext.Location) {
               case InsertLocation.Before:
                  (obj as ISibling)?.MoveSiblingTo(pos);
                  break;
               case InsertLocation.After:
                  (obj as ISibling)?.MoveSiblingTo(pos + 1);
                  break;
               case InsertLocation.First:
                  (obj as ISibling)?.MoveSiblingTo(0);
                  break;
               default:
                  // In the right positon at the end of the section.
                  break;
            }
         }
      }
      /// <summary>
      /// Moves each object in the specified list to the given position within the parsing context.
      /// </summary>
      /// <typeparam name="T">The type of objects to move. Must inherit from CDL2Object.</typeparam>
      /// <param name="parsingContext">The parsing context in which the objects are to be moved.</param>
      /// <param name="context">The focus or target position to which each object will be moved.</param>
      /// <param name="objList">The list of objects to move. Cannot be null.</param>
      public static void MoveObjectToPosition<T>(ParsingContext parsingContext,Focus context,List<T> objList) where T : CDL2Object {
         foreach (T obj in objList.Reverse<T>()) MoveObjectToPosition(parsingContext,context,obj);
      }

      [GeneratedRegex(@"\r\n?|\n\r")]
      private static partial Regex NormalizeLineEndRE();

      /// <summary>
      /// Verify that the next token(s) in the token stream match the identity specified in the parsing context.
      /// </summary>
      /// <param name="context"></param>
      /// <returns></returns>
      internal bool VerifyIdentity(ParsingContext? context) {
         if (context is not null) {
            if (context.LudeType == RW.NONE) {
               NamedElement? obj = context.Focus.Object;
               return obj is not null && tokens.IsNextTypeAndId(obj.TypeAsReservedWord,obj.Id);
            } else {
               return tokens.IsNext(context.LudeType);
            }
         } else {
            return true;
         }
      }
   }
}