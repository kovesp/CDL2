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
         public NamedElement this[Token id] {
            get => base[new ID(id)];
            set => base[new ID(id)] = value;
         }
      }

         public readonly SymbolTable Symbols = [];

      public Program? program;

      private Module? currentModule;
      private Layer? currentLayer;
      private Section? currentSection;

      public Parser() { }

      internal void Parse(TokenList tokens) {
         // The list of tokens should contain a set of modules and possibly a program
         tokens.SetOptions(TokenList.Options.SkipComments | TokenList.Options.ThrowOnUnexpectedToken); // Remove comments from the token list and throw on unexpected tokens
                                                                                                       // The first token should be a module or program
         while (tokens.IsNonEmpty()) {
            Token unitId = Token.ErrorToken;
            if (tokens.CanConsumeUnitDelimiter(Token.ReservedWord.MODULE,ref unitId)) {
               ParseModule(ref tokens,unitId);
            } else if (tokens.CanConsumeUnitDelimiter(Token.ReservedWord.PROGRAM, ref unitId)) {
               ParseProgram(ref tokens);
            } else {
               throw new Exception("Expected MODULE or PROGRAM");
            }
         }
      }

      private void ParseModule(ref TokenList tokens,Token moduleId) {
         currentModule = new Module(moduleId);
         Symbols[currentModule.name] = currentModule;

         // Now should see layers
         Token layerId = Token.ErrorToken;
         while (tokens.CanConsumeUnitDelimiter(Token.ReservedWord.LAYER,ref layerId)) {
            ParseLayer(ref tokens,layerId);
         }
         // Consume the ENDMOD
         tokens.CanConsumeUnitDelimiter(Token.ReservedWord.ENDMOD,ref moduleId);
      }

      private void ParseLayer(ref TokenList tokens,Token layerId) {
         Debug.Assert(currentModule != null);
         currentLayer = new Layer(layerId,currentModule);
         Symbols[currentLayer.name] = currentLayer;

         // Now should see sections
         Token sectionId = Token.ErrorToken;
         while (tokens.CanConsumeUnitDelimiter(Token.ReservedWord.SECTION,ref sectionId)) {
            ParseSection(ref tokens,sectionId);
         }
         // Consume the ENDLAY
         tokens.CanConsumeUnitDelimiter(Token.ReservedWord.ENDLAY,ref layerId);
      }

      private void ParseSection(ref TokenList tokens,Token sectionId) {
         Debug.Assert(currentLayer != null);
         // The next token should be an ID
         currentSection = new Section(sectionId,currentLayer);
         Symbols[currentSection.name] = currentSection;

         // Now should see section parts
         // Interfaces first
         ParseInterfaces(ref tokens);

         // Now could see routines, lists, vars, consts in any order
         while (!tokens.IsNext(Token.ReservedWord.ENDSEC)) {
            if (tokens.IsNext(Token.ReservedWord.ROUTINE)) {
               ParseRoutine(ref tokens);
            } else if (tokens.IsNext(Token.ReservedWord.LIST)) {
               ParseList(ref tokens);
            } else if (tokens.IsNext(Token.ReservedWord.VAR)) {
               ParseVar(ref tokens);
            } else if (tokens.IsNext(Token.ReservedWord.CONST)) {
               ParseConst(ref tokens);
            } else {
               throw new Exception("Expected ROUTINE, LIST, VAR, or CONST");
            }
         }


         // Consume the ENDSEC
         tokens.CanConsumeUnitDelimiter(Token.ReservedWord.ENDSEC,ref sectionId);
         // Now could see prelude, root, postlude in that order
         ParseLudes(ref tokens,typeof(Proc));
      }

      private void ParseInterfaces(ref TokenList tokens) {
         Debug.Assert(currentSection != null && currentModule != null);
         // Provided interfaces
         ParceInterface(ref tokens,Token.ReservedWord.ABSTR,currentSection.abstr);
         ParceInterface(ref tokens,Token.ReservedWord.EXT,currentSection.ext);
         ParceInterface(ref tokens,Token.ReservedWord.EXPORT,currentSection.export);
         // Required interfaces
         ParceInterface(ref tokens,Token.ReservedWord.INV,currentSection.inv);
         ParceInterface(ref tokens,Token.ReservedWord.IMPORT,currentSection.import,currentModule.import);
      }

      private bool ParceInterface(ref TokenList tokens,Token.ReservedWord interfaceType,ICollection<ID> idList1,ICollection<ID>? idList2=null) {
         Debug.Assert(currentSection != null);
         if (tokens.Consume(interfaceType)) {
            ParseIDList(tokens,idList1,idList2);
            return true;
         } else {
            return false;
         }
      }

      private void ParseLudes(ref TokenList tokens,Type type) {
         Debug.Assert(currentSection != null);
         ParseLude(ref tokens,Token.ReservedWord.PRELUDE,typeof(ProvidedElement),currentSection.prelude);
         ParseLude(ref tokens,Token.ReservedWord.ROOT,typeof(ProvidedElement),currentSection.root);
         ParseLude(ref tokens,Token.ReservedWord.POSTLUDE,typeof(ProvidedElement),currentSection.postlude);
      }
      private void ParseLude(ref TokenList tokens,Token.ReservedWord ludeType,Type itemType,List<ID> idlist) {
         // Implementation for parsing lude sections (prelude, root, postlude)
         // Use the 'lude' parameter to determine which section to parse
         // Use the 'type' parameter to handle the specific type (e.g., Proc)
         Debug.Assert(currentSection != null);
         if (tokens.IsNext(ludeType)) {
            tokens.Next();
            ParseIDList(tokens,idlist);
         }
      }

      private void ParseIDList(TokenList tokens,ICollection<ID> idList1,ICollection<ID>? idList2 = null) {
         while (tokens.IsNext(Token.TokenType.ID)) {
            ID id = new(tokens.Next());
            if (!idList1.Contains(id)) idList1.Add(id);
            if (idList2 != null && !idList2.Contains(id)) idList2.Add(id);
            if (!tokens.CanConsumeSep()) break;
         }
         tokens.CanConsumeEnd();
      }

      private void ParseProgram(ref TokenList tokens) {
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
