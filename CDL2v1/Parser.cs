using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal partial class Parser {

      public class CompilationObject(Parser parser) {
         readonly Parser parser = parser;

         public string Program => (parser?.program?.ToString()+" ") ?? "";
         public string Module  => (parser?.currentModule?.ToString() + " ") ?? "";
         public string Layer   => (parser?.currentLayer?.ToString() + " ") ?? "";
         public string Section => (parser?.currentSection?.ToString() + " ") ?? "";

         public enum ObjType { ABSTR, EXT, INV, IMPORT, EXPORT, VAR, CONSTANT, LIST, FUNCTION, ACTION, TEST, PREDICATE, PRELUDE, ROOT, POSTLUDE, MODULE, LAYER, SECTION, PROGRAM };
         private static ObjType[] procs = [ObjType.FUNCTION, ObjType.ACTION, ObjType.TEST, ObjType.PREDICATE];
         private ObjType type;
         private ID name = TokenList.ErrorID;
         public string obj => $"{type}{(procs.Contains(type) ? $" {name}" : "")}";
         public (Token.ReservedWord,ID) Object {
            set {
               type = (ObjType)Enum.Parse(typeof(ObjType),value.Item1.ToString());
               name = value.Item2;
            }
         }

         override public string ToString() => $"{Program}{Module}{Layer}{Section}{obj}";
      }

      public readonly SymbolTable Symbols = [];
      public TokenList tokens = new();
      public Program? program;
      public HashSet<Module> modules = [];

      private Module? currentModule;
      private Layer? currentLayer;
      private Section? currentSection;
      public CompilationObject currentObject;

      public Parser() => currentObject = new CompilationObject(this);


      internal void Parse(TokenList tokens) {
         this.tokens = tokens;
         // The list of tokens should contain a set of modules and possibly a program
         tokens.SetOptions(TokenList.Options.SkipComments | TokenList.Options.ThrowOnUnexpectedToken); // Remove comments from the token list and throw on unexpected tokens
                                                                                                       // The first token should be a module or program
         while (tokens.IsNonEmpty()) {
            ID unitId = TokenList.ErrorID;
            if (tokens.CanConsumeUnitDelimiter(Token.ReservedWord.MODULE,ref unitId)) {
               ParseModule(unitId);
            } else if (tokens.CanConsumeUnitDelimiter(Token.ReservedWord.PROGRAM,ref unitId)) {
               ParseProgram();
            } else {
               throw new Exception("Expected MODULE or PROGRAM");
            }
         }
         //TODO: Must verify that no undeclareds ramain when parsing is complete.
         //TODO: Must verify that All ids referenced in CONST elements refer to a declared const, var, or list.
      }

      private void ParseModule(ID moduleId) {
         currentObject.Object = (Token.ReservedWord.MODULE, moduleId);
         currentModule = new Module(moduleId);
         modules.Add(currentModule);
         Symbols[currentModule.name] = currentModule;

         // Now should see layers
         ID layerId = TokenList.ErrorID;
         while (tokens.CanConsumeUnitDelimiter(Token.ReservedWord.LAYER,ref layerId)) {
            ParseLayer(layerId);
         }
         // Consume the ENDMOD
         tokens.CanConsumeUnitDelimiter(Token.ReservedWord.ENDMOD,ref moduleId);
      }

      private void ParseLayer(ID layerId) {
         Debug.Assert(currentModule != null);
         currentObject.Object = (Token.ReservedWord.MODULE, layerId);
         currentLayer = new Layer(layerId,currentModule);
         Symbols[currentLayer.name] = currentLayer;

         // Now should see sections
         ID sectionId = TokenList.ErrorID;
         while (tokens.CanConsumeUnitDelimiter(Token.ReservedWord.SECTION,ref sectionId)) {
            ParseSection(sectionId);
         }
         // Consume the ENDLAY
         tokens.CanConsumeUnitDelimiter(Token.ReservedWord.ENDLAY,ref layerId);
      }

      private static readonly List<Token.ReservedWord> procTypes = [Token.ReservedWord.FUNCTION,Token.ReservedWord.ACTION,Token.ReservedWord.TEST,Token.ReservedWord.PREDICATE];
      private void ParseSection(ID sectionId) {
         Debug.Assert(currentLayer != null);
         currentObject.Object = (Token.ReservedWord.SECTION, sectionId);
         // The next token should be an ID
         currentSection = new Section(sectionId,currentLayer);
         Symbols[currentSection.name] = currentSection;

         // Now should see section parts
         // Interfaces first
         ParseInterfaces();

         // Now could see routines, lists, vars, consts in any order
         while (!tokens.IsNext(Token.ReservedWord.ENDSEC)) {
            if (tokens.IsNext(procTypes)) {
               ParseProc();
            } else if (tokens.IsNext(Token.ReservedWord.LIST)) {
               ParseList();
            } else if (tokens.IsNext(Token.ReservedWord.VAR)) {
               ParseVar();
            } else if (tokens.IsNext(Token.ReservedWord.CONST)) {
               ParseConst();
            } else {
               throw new Exception("Expected ROUTINE, LIST, VAR, or CONST");
            }
         }

         // Consume the ENDSEC
         tokens.CanConsumeUnitDelimiter(Token.ReservedWord.ENDSEC,ref sectionId);
         // Now could see prelude, root, postlude in that order
         ParseLudes(typeof(Proc));
      }

      private static readonly List<Token.TokenType> bodyTypes = [Token.TokenType.INLINECODEBODY,Token.TokenType.INLINEMACROBODY,Token.TokenType.EQUALS,Token.TokenType.COLON];
      private void ParseProc() {
         Debug.Assert(currentSection != null);
         if (tokens.CanConsume(procTypes,out Token procType) && tokens.CanConsume(out ID id)) {
            currentObject.Object = (procType.rval ?? Token.ReservedWord.FUNCTION, id);
            // Now should see args
            List<Arg> args = ParseArgs();
            // Now could see locals
            List<ID> locals = ParseLocals();
            Proc proc;
            if (tokens.CanConsume(bodyTypes,out Token bodyType)) {
               if (bodyType.type == Token.TokenType.COLON || bodyType.type == Token.TokenType.INLINECODEBODY) {
                  // Parse the code body
                  proc = new Code(id,args,locals,procType,bodyType.type,currentSection);
                  ParseCodeBody((Code)proc);
               } else {
                  // Parse the macro body
                  proc = new Macro(id,args,locals,procType,bodyType.type,currentSection);
                  ParseMacroBody((Macro)proc);
               }
               Symbols[proc.name] = proc;
            }
         } else {
            throw new Exception("Expected FUNCTION, ACTION, TEST, or PREDICATE");
         }
      }

      private void ParseMacroBody(Macro macro) {
         while (!tokens.Optional(Token.TokenType.END)) {
            if (tokens.Optional(Token.TokenType.ID,out Token id)) {
               macro.elements.Add(Symbols.Reference(new ID(id)));
            } else if (tokens.Optional(Token.TokenType.STRING,out Token str)) {
               macro.elements.Add(new STRING(str));
            } else if (tokens.Optional(Token.TokenType.INT,out Token i)) {
               macro.elements.Add(new INT(i));
            } else if (tokens.Optional(Token.TokenType.FLOAT,out Token f)) {
               macro.elements.Add(new FLOAT(f));
            } else {
               ReportError("Expected ID, STRING, INT, or FLOAT");
            }
         }
      }
      private void ParseCodeBody(Code proc) {
         proc.alternatives = parseAlternatives();
         if (!tokens.CanConsume(Token.TokenType.END)) ReportError("Expected .");
      }
      private List<Alternative> parseAlternatives() {
         List<Alternative> alternatives = [];
         do {
            alternatives.Add(parseAlternative());
         } while (tokens.Optional(Token.TokenType.ALTSEP)) ;
         return alternatives;
      }

      private Alternative parseAlternative() {
         List<Call> calls = [];
         LastCall? lastCall =null;
         do {
            if (lastCall != null) {
               // If we have a last call, then we should NOT have see a separator
               ReportError("Unexpected ,");
            } else if (tokens.Optional(out ID id)) {
               Call call = new(Symbols.Reference(id));
               calls.Add(call);
               parseActualArgs(call);
            } else if (tokens.Optional(Token.TokenType.PLUS)) {
               lastCall = new LastCall(LastCall.CallType.Success);
            } else if (tokens.Optional(Token.TokenType.MINUS)) {
               lastCall = new LastCall(LastCall.CallType.Fail);
            } else if (tokens.Optional(Token.TokenType.ABORT)) {
               lastCall = new LastCall(LastCall.CallType.Abort);
            } else if (tokens.Optional(Token.TokenType.STAR)) {
               lastCall = tokens.Optional(out id) ? new LastCall(id) : new LastCall(LastCall.CallType.Repeat);
            } else if (tokens.Optional(Token.TokenType.GRPOPEN)) {
               lastCall = parseGroup();
            } else {
               ReportError("Expected ID, +, -, ?, or *");
            }
         } while (tokens.Optional(Token.TokenType.SEP));
         if (lastCall == null) {
            // The last all postion contained an actual call so convert it to a last call
            lastCall = new LastCall(calls.Last());
            calls.RemoveAt(calls.Count - 1);
         }
         return new Alternative(calls,lastCall);
      }

      private LastCall parseGroup() {
         LastCall? lastCall;
         ID label = parseOptionalLabel();
         Group group = new(label,parseAlternatives());
         if (!tokens.CanConsume(Token.TokenType.GRPCLOSE)) ReportError("Expected )");
         lastCall = new LastCall(group);
         return lastCall;
      }

      private ID parseOptionalLabel() {
         if (tokens.Peek().type == Token.TokenType.ID && tokens.Peek(1).type == Token.TokenType.COLON) {
            // Consume the label and the colon
            ID label = new(tokens.Next());
            tokens.Next();
            return label;
         } else {
            return TokenList.AnonID;
         }
      }

      /// <summary>
      /// Parse the actual arguments of a call.
      /// Actual arguments are a sequence of IDs or strings separated by '+'.
      /// </summary>
      /// <param name="call"></param>
      private void parseActualArgs(Call call) {
         while (tokens.Optional(Token.TokenType.PLUS)) {
            if (tokens.Optional(out ID id)) {
               call.args.Add(Symbols.Reference(id));
            } else if (tokens.CanConsume(Token.TokenType.STRING,out Token str)) {
               call.args.Add(new STRING(str));
            } else {
               ReportError("Expected ID or STRING");
            }
         }
      }

      private List<ID> ParseLocals() {
         List<ID> locals = [];
         while (tokens.Optional(Token.TokenType.MINUS) && tokens.CanConsume(Token.TokenType.ID,out Token id)) locals.Add(new ID(id));
         return locals;
      }
      private static List<Token.TokenType> argTypes = [Token.TokenType.PLUS,Token.TokenType.STAR];
      private List<Arg> ParseArgs() {
         List<Arg> args = [];
         while (tokens.Optional(argTypes,out Token argTypeInd)) {
            bool isIn = tokens.Optional(Token.TokenType.ARGDIR);
            if (tokens.CanConsume(out ID id)) {
               bool isOut = tokens.Optional(Token.TokenType.ARGDIR);
               Arg.ArgDir argDir = isIn ? (isOut ? Arg.ArgDir.transput : Arg.ArgDir.input) : (isOut ? Arg.ArgDir.output : Arg.ArgDir.NONE);
               Arg.ArgType argType = argTypeInd.type == Token.TokenType.PLUS ? Arg.ArgType.std : Arg.ArgType.str;
               if (argType == Arg.ArgType.str && argDir != Arg.ArgDir.NONE) ReportError("String arguments cannot have a direction");
               if (argType == Arg.ArgType.std && argDir == Arg.ArgDir.NONE) ReportError("Standard arguments must be input, output, or transput");
               args.Add(new Arg(id,argDir,argType));
            }
         }
         return args;
      }

      private void ParseList() {
         if (tokens.CanConsume(Token.ReservedWord.LIST)) {
            Debug.Assert(currentSection != null);
            ParseIDList(currentSection.lists,null,id => ParseListBody(id));
         }
      }

      private static readonly List<Token.TokenType> boundTypes = [Token.TokenType.ID,Token.TokenType.INT];
      private void ParseListBody(ID id) {
         if (  tokens.CanConsume(Token.TokenType.GRPOPEN) &&
               tokens.CanConsume(boundTypes,out Token lwb) &&
               tokens.CanConsume(Token.TokenType.COLON) &&
               (tokens.CanConsume(Token.TokenType.ID,out Token upb) || tokens.CanConsume(Token.TokenType.INT,out upb)) &&
               tokens.CanConsume(Token.TokenType.GRPCLOSE)) {
            Symbols[id] = new LIST(id,lwb,upb);
         } else {
            throw new Exception("Expected list bounds");
         }
      }
      private void ParseVar() {
         if (tokens.CanConsume(Token.ReservedWord.VAR)) {
            Debug.Assert(currentSection != null);
            ParseIDList(currentSection.vars,null,id => Symbols[id] = new Var(id));
         }
      }

      private void ParseConst() {
         if (tokens.CanConsume(Token.ReservedWord.CONST)) {
            Debug.Assert(currentSection != null);
            ParseIDList(currentSection.consts,null,id => ParseConstBody(id));
         }
      }

      /// <summary>
      /// Parse the body of a constant declaration. At this point the ID has been consumed.
      /// We should see an '=' followed by a sequence of constant elements (e.g., numbers, strings, etc.) terminated by a period or a comma.
      /// The terminator will be consumed by <see cref="ParseIDList(ICollection{ID}, ICollection{ID}?, Action{ID}?)
      /// </summary>
      /// <param name="id">The id of the constant.</param>
      private void ParseConstBody(ID id) {
         Const c = new(id);
         Symbols[id] = c;
         if (tokens.CanConsume(Token.TokenType.EQUALS)) {
            ParseConstElements(c);
         }
      }

      private void ParseConstElements(Const c) {
         while (!tokens.IsNext(Token.TokenType.END) && !tokens.IsNext(Token.TokenType.SEP)) {
            if (tokens.Optional(Token.TokenType.ID,out Token elemId)) {
               c.elements.Add(Symbols.Reference(new ID(elemId)));
            } else if (tokens.Optional(Token.TokenType.STRING,out Token str)) {
               c.elements.Add(new STRING(str));
            } else if (tokens.Optional(Token.TokenType.INT,out Token i)) {
               c.elements.Add(new INT(i));
            } else if (tokens.Optional(Token.TokenType.FLOAT,out Token f)) {
               c.elements.Add(new FLOAT(f));
            } else {
               throw new Exception("Expected ID, STRING, INT, or FLOAT");
            }
         }
      }

      private void ParseInterfaces() {
         Debug.Assert(currentSection != null && currentModule != null);
         // Provided interfaces
         ParseSimpleList(Token.ReservedWord.ABSTR,currentSection.abstr);
         ParseSimpleList(Token.ReservedWord.EXT,currentSection.ext);
         ParseSimpleList(Token.ReservedWord.EXPORT,currentSection.export);
         // Required interfaces
         ParseSimpleList(Token.ReservedWord.INV,currentSection.inv);
         ParseSimpleList(Token.ReservedWord.IMPORT,currentSection.import,currentModule.import);
      }

      private bool ParseSimpleList(Token.ReservedWord interfaceType,ICollection<ID> idList1,ICollection<ID>? idList2 = null) {
         Debug.Assert(currentSection != null);
         if (tokens.Consume(interfaceType)) {
            ParseIDList(idList1,idList2);
            return true;
         } else {
            return false;
         }
      }

      private void ParseLudes(Type type) {
         Debug.Assert(currentSection != null);
         ParseLude(Token.ReservedWord.PRELUDE,typeof(ProvidedElement),currentSection.prelude);
         ParseLude(Token.ReservedWord.ROOT,typeof(ProvidedElement),currentSection.root);
         ParseLude(Token.ReservedWord.POSTLUDE,typeof(ProvidedElement),currentSection.postlude);
      }

      private void ParseLude(Token.ReservedWord ludeType,Type itemType,List<ID> idlist) {
         // Implementation for parsing lude sections (prelude, root, postlude)
         // Use the 'lude' parameter to determine which section to parse
         // Use the 'type' parameter to handle the specific type (e.g., Proc)
         Debug.Assert(currentSection != null);
         if (tokens.IsNext(ludeType)) {
            tokens.Next();
            ParseIDList(idlist);
         }
      }

      private void ParseIDList(ICollection<ID> idList1,ICollection<ID>? idList2 = null,Action<ID>? processID = null) {
         while (tokens.IsNext(Token.TokenType.ID)) {
            ID id = new(tokens.Next());
            if (!idList1.Contains(id)) idList1.Add(id);
            if (idList2 != null && !idList2.Contains(id)) idList2.Add(id);
            processID?.Invoke(id);
            if (!tokens.CanConsumeSep()) break;
         }
         tokens.CanConsumeEnd();
      }

      private void ParseProgram() {
         // The next token should be an ID
         tokens.CanConsume(out ID id);
         program = new Program(id);
         Symbols[program.name] = program;

         // Now should see parts
         while (tokens.CanConsume(Token.ReservedWord.PART)) {
         }

         // Consume the ENDPROG token
         tokens.CanConsume(Token.ReservedWord.ENDPROG);
      }

      private void ReportError(string v) => Logger.ReportError($"MOD {currentModule} LAY {currentLayer} SEC {currentSection}: {v}");
      internal void SkipToNextEnd() {
         while (!tokens.IsNext(Token.TokenType.END)) tokens.Skip();
         tokens.Skip(); // The end itself
      }
   }
}
