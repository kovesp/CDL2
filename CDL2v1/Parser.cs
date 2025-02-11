using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
            if (tokens.CanConsume(Token.ReservedWord.MODULE)) {
               ParseModule(ref tokens);
            } else if (tokens.CanConsume(Token.ReservedWord.PROGRAM)) {
               ParseProgram(ref tokens);
            } else {
               throw new Exception("Expected MODULE or PROGRAM");
            }
         }
      }

      private void ParseModule(ref TokenList tokens) {
         // The next token should be an ID
         tokens.CanConsume(Token.TokenType.ID,out Token id);
         currentModule = new Module(id);
         Symbols[currentModule.name] = currentModule;

         // Now should see layers
         while (tokens.CanConsume(Token.ReservedWord.LAYER)) {
            ParseLayer(ref tokens);
         }

         // Consume the ENDMOD token
         tokens.CanConsume(Token.ReservedWord.ENDMOD);
      }

      private void ParseLayer(ref TokenList tokens) {
         // The next token should be an ID
         tokens.CanConsume(Token.TokenType.ID,out Token id);
         currentLayer = new Layer(id);
         Debug.Assert(currentModule != null);
         currentModule.layers.Add(currentLayer);
         Symbols[currentLayer.name] = currentLayer;

         // Now should see sections
         while (tokens.CanConsume(Token.ReservedWord.SECTION)) {
            ParseSection(ref tokens);
         }

         // Consume the ENDLAY token
         tokens.CanConsume(Token.ReservedWord.ENDLAY);
      }

      private void ParseSection(ref TokenList tokens) {
         // The next token should be an ID
         tokens.CanConsume(Token.TokenType.ID,out Token id);
         currentSection = new Section(id);
         Debug.Assert(currentLayer != null);
         currentLayer.sections.Add(currentSection);
         Symbols[currentSection.name] = currentSection;

         // Now should see section parts

         // Consume the ENDSEC token
         tokens.CanConsume(Token.ReservedWord.ENDSEC);
         // Now could see prelude, root, postlude in that order
         ParseLudes(ref tokens,typeof(Proc));
      }

      private void ParseLudes(ref TokenList tokens,Type type) {
         Debug.Assert(currentSection != null);
         ParseLude(ref tokens,Token.ReservedWord.PRELUDE,typeof(Proc),currentSection.prelude);
         ParseLude(ref tokens,Token.ReservedWord.ROOT,typeof(Proc),currentSection.root);
         ParseLude(ref tokens,Token.ReservedWord.POSTLUDE,typeof(Proc),currentSection.postlude);
      }
      private void ParseLude(ref TokenList tokens,Token.ReservedWord ludeType,Type itemType,List<ID> idlist) {
         // Implementation for parsing lude sections (prelude, root, postlude)
         // Use the 'lude' parameter to determine which section to parse
         // Use the 'type' parameter to handle the specific type (e.g., Proc)
         Debug.Assert(currentSection != null);
         if (tokens.IsNext(ludeType)) {
            tokens.Next();
            while (tokens.IsNext(Token.TokenType.ID)) {
               Token id = tokens.Next();
               if (Symbols.ContainsKey(id) && Symbols[id].GetType() == itemType) {
                  idlist.Add(new ID(id));
               } else {
                  throw new Exception($"The name {id} referenced in the {ludeType} of {currentSection} but found {id}");
               }
            }
         }
      }

      private void ParseProgram(ref TokenList tokens) {
         // The next token should be an ID
         tokens.CanConsume(Token.TokenType.ID,out Token id);
         program = new Program(id);
         Symbols[program.name] = program;

         // Now should see parts
         while (tokens.CanConsume(Token.ReservedWord.PART)) {
            ParseModule(ref tokens);
         }

         // Consume the ENDPROG token
         tokens.CanConsume(Token.ReservedWord.ENDPROG);
      }
   }
}
