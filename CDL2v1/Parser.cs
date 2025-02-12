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
   internal class Parser {

      public class SymbolTable : Dictionary<ID,NamedElement> {
         public bool ContainsKey(Token token) => ContainsKey(new ID(token));
         // Indexer to access elements using ID
         public NamedElement this[Token token] {
            get => base[new ID(token)];
            // This is used to add elements when being declared. So if it already exists, then
            // - if the current value is an Undeclared, then replace it with the new value.
            // - if the current value is not an Undeclared, then throw an exception.
            set {
               ID id = new(token);
               if (TryGetValue(id,out NamedElement? currentValue)) {
                  if (currentValue is Undeclared) {
                     base[id] = value;
                     return;
                  }
                  throw new Exception($"ID {id} already declared as {currentValue}");
               }
            }
         }
         /// <summary>
         /// Check if the symbol table contains the given ID. If not, add an Undeclared to the symbol table.
         /// Used when a reference to an ID is encountered before the ID is declared.
         /// </summary>
         /// <param name="token">An ID token.</param>
         /// <returns>The ID that was constructed from the token.</returns>
         public ID Reference(Token token) {
            ID id = new(token);
            if (TryGetValue(id,out NamedElement? currentValue)) { // If the ID is already in the symbol table
               if (currentValue is Undeclared) return id; // If the ID is already an Undeclared, then do nothing               
            } else { // If the ID is not in the symbol table
               base[id] = new Undeclared(token); // Add an Undeclared to the symbol table
            }
            return id;
         }
         public ID Reference(ID id) => Reference(id.token); 
      }

      public readonly SymbolTable Symbols = [];
      public required TokenList tokens;
      public Program? program;
      public HashSet<Module> modules = [];

      private Module? currentModule;
      private Layer? currentLayer;
      private Section? currentSection;

      internal void Parse(TokenList tokens) {
         this.tokens = tokens;
         // The list of tokens should contain a set of modules and possibly a program
         tokens.SetOptions(TokenList.Options.SkipComments | TokenList.Options.ThrowOnUnexpectedToken); // Remove comments from the token list and throw on unexpected tokens
                                                                                                       // The first token should be a module or program
         while (tokens.IsNonEmpty()) {
            Token unitId = Token.ErrorToken;
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

      private void ParseModule(Token moduleId) {
         currentModule = new Module(moduleId);
         modules.Add(currentModule);
         Symbols[currentModule.name] = currentModule;

         // Now should see layers
         Token layerId = Token.ErrorToken;
         while (tokens.CanConsumeUnitDelimiter(Token.ReservedWord.LAYER,ref layerId)) {
            ParseLayer(layerId);
         }
         // Consume the ENDMOD
         tokens.CanConsumeUnitDelimiter(Token.ReservedWord.ENDMOD,ref moduleId);
      }

      private void ParseLayer(Token layerId) {
         Debug.Assert(currentModule != null);
         currentLayer = new Layer(layerId,currentModule);
         Symbols[currentLayer.name] = currentLayer;

         // Now should see sections
         Token sectionId = Token.ErrorToken;
         while (tokens.CanConsumeUnitDelimiter(Token.ReservedWord.SECTION,ref sectionId)) {
            ParseSection(sectionId);
         }
         // Consume the ENDLAY
         tokens.CanConsumeUnitDelimiter(Token.ReservedWord.ENDLAY,ref layerId);
      }

      private static readonly List<Token.ReservedWord> procTypes = [Token.ReservedWord.FUNCTION,Token.ReservedWord.ACTION,Token.ReservedWord.TEST,Token.ReservedWord.PREDICATE];
      private void ParseSection(Token sectionId) {
         Debug.Assert(currentLayer != null);
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

      private static readonly List<Token.TokenType> bodyTypes = [Token.TokenType.COLON,Token.TokenType.INLINECODEBODY,Token.TokenType.INLINEMACROBODY];
      private void ParseProc() {
         Debug.Assert(currentSection != null);
         Token id = tokens.Next();
         if (tokens.CanConsume(procTypes,out Token procType)) {
            // Now should see args
            List<Arg> args = ParseArgs();
            // Now could see locals
            List<ID> locals = ParseLocals();
            Proc proc;
            if (tokens.CanConsume(bodyTypes,out Token bodyType)) {
               if (bodyType.type == Token.TokenType.COLON || bodyType.type == Token.TokenType.INLINECODEBODY) {
                  // Parse the code body
                  proc = new Code(id,args,locals,procType,bodyType.type,currentSection);
                  ParseCodeBody(proc);
               } else {
                  // Parse the macro body
                  proc = new Macro(id,args,locals,procType,bodyType.type,currentSection);
                  ParseMacroBody(proc);
               }
               Symbols[proc.name] = proc;
            }


         } else {
            throw new Exception("Expected FUNCTION, ACTION, TEST, or PREDICATE");
         }
      }

      private void ParseMacroBody(Proc proc) => throw new NotImplementedException();
      private void ParseCodeBody(Proc proc) => throw new NotImplementedException();
      private List<ID> ParseLocals() => throw new NotImplementedException();
      private List<Arg> ParseArgs() => throw new NotImplementedException();

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
            Symbols[id] = new LIST(id.token,lwb,upb);
         } else {
            throw new Exception("Expected list bounds");
         }
      }
      private void ParseVar() {
         if (tokens.CanConsume(Token.ReservedWord.VAR)) {
            Debug.Assert(currentSection != null);
            ParseIDList(currentSection.vars,null,id => Symbols[id] = new Var(id.token));
         }
      }

      private void ParseConst() {
         if (tokens.CanConsume(Token.ReservedWord.CONST)) {
            Debug.Assert(currentSection != null);
            ParseIDList(currentSection.vars,null,id => ParseConstBody(id));
         }
      }

      /// <summary>
      /// Parse the body of a constant declaration. At this point the ID has been consumed.
      /// We should see an '=' followed by a sequence of constant elements (e.g., numbers, strings, etc.) terminated by a period or a comma.
      /// The terminator will be consumed by <see cref="ParseIDList(ICollection{ID}, ICollection{ID}?, Action{ID}?)
      /// </summary>
      /// <param name="id">The id of the constant.</param>
      private void ParseConstBody(ID id) {
         Const c = new(id.token);
         Symbols[id] = c;
         if (tokens.CanConsume(Token.TokenType.EQUALS)) {
            ParseConstElements(c);
         }
      }

      private void ParseConstElements(Const c) {
         while (!tokens.IsNext(Token.TokenType.END) && !tokens.IsNext(Token.TokenType.SEP)) {
            if (tokens.CanConsume(Token.TokenType.ID,out Token elemId)) {
               c.elements.Add(Symbols.Reference(new ID(elemId)));
            } else if (tokens.CanConsume(Token.TokenType.STRING,out Token str)) {
               c.elements.Add(new STRING(str));
            } else if (tokens.CanConsume(Token.TokenType.INT,out Token i)) {
               c.elements.Add(new INT(i));
            } else if (tokens.CanConsume(Token.TokenType.FLOAT,out Token f)) {
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
         tokens.CanConsume(Token.TokenType.ID,out Token id);
         program = new Program(id);
         Symbols[program.name] = program;

         // Now should see parts
         while (tokens.CanConsume(Token.ReservedWord.PART)) {
         }

         // Consume the ENDPROG token
         tokens.CanConsume(Token.ReservedWord.ENDPROG);
      }
   }
}
