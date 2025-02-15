using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
  internal class TokenList {
      [Flags]
      public enum Options {
         None = 0,
         SkipComments = 1,
         ThrowOnUnexpectedToken = 2,
         // Add more options as needed
      }
      
      public readonly List<Token> tokens = new List<Token>();
      public readonly static ID ErrorID = new ID();
      public readonly static ID AnonID = new ID("Anon");
      public Options options;

      public TokenList(Options options = Options.None) {
         this.options = options;
      }

      public void Add(Token token) => tokens.Add(token);
      public bool IsNonEmpty() => tokens.Count > 0;
      public Token Peek() => IsNonEmpty() ? tokens[0] : Token.ErrorToken;
      public Token Peek(int n) => n < tokens.Count ? tokens[n] : Token.ErrorToken;
      public bool IsNext(Token.TokenType type) => IsNonEmpty() && tokens[0].type == type;
      /// <summary>
      /// Check if the next token is one of the types in the list.
      /// </summary>
      /// <param name="types"></param>
      /// <returns></returns>
      public bool IsNext(List<Token.TokenType> types) => IsNonEmpty() && types.Contains(tokens[0].type);

      public bool IsNext(Token.ReservedWord reservedWord) => IsNonEmpty() && tokens[0].type == Token.TokenType.RESWORD && tokens[0].rval == reservedWord;
      public bool IsNext(List<Token.ReservedWord> reservedWords) => IsNonEmpty() && tokens[0].type == Token.TokenType.RESWORD && reservedWords.Contains(tokens[0].rval??0); // ??0 is a hack to supress error: rval can't be null because tokens[0].type == Token.TokenType.RESWORD
      public Token Next() {
         if (IsNonEmpty()) {
            Token token = tokens[0];
            Skip();
            return token;
         }
         return Token.ErrorToken;
      }

      public bool Consume(Token.TokenType type) {
         if (IsNext(type)) {
            Skip();
            return true;
         }
         return false;
      }
      public bool Consume(Token.ReservedWord type) {
         if (IsNext(type)) {
            Skip();
            return true;
         }
         return false;
      }

      public void Skip() => tokens.RemoveAt(0);

      public void SetOptions(Options options) {
         this.options = options;

         if (options.HasFlag(Options.SkipComments)) tokens.RemoveAll(token => token.type == Token.TokenType.COMMENT);
      }

      public bool CanConsume(Token.TokenType type,out Token token) {
         if (IsNext(type)) {
            token = Next();
            return true;
         }
         if (options.HasFlag(Options.ThrowOnUnexpectedToken)) {
            throw new Exception($"Expected token of type {type}, but found {Peek().type}");
         }
         token = Token.ErrorToken;
         return false;
      }
      public bool CanConsume(out ID id) {
         if (CanConsume(Token.TokenType.ID,out Token token)) {
            id = new ID(token);
            return true;
         }
         if (options.HasFlag(Options.ThrowOnUnexpectedToken)) {
            throw new Exception($"Expected ID, but found {Peek().type}");
         }
         id = ErrorID;
         return false;
      }
      public bool CanConsume(List<Token.TokenType> types,out Token token) {
         if (IsNext(types)) {
            token = Next();
            return true;
         }
         if (options.HasFlag(Options.ThrowOnUnexpectedToken)) {
            throw new Exception($"Expected token of type {types}, but found {Peek().type}");
         }
         token = Token.ErrorToken;
         return false;
      }
      public bool CanConsume(Token.TokenType type) {
         if (IsNext(type)) {
            Next();
            return true;
         }
         if (options.HasFlag(Options.ThrowOnUnexpectedToken)) {
            throw new Exception($"Expected token of type {type}, but found {Peek().type}");
         }
         return false;
      }
      public bool Optional(out ID id) {
         id = ErrorID;
         return IsNext(Token.TokenType.ID) ? CanConsume(out id) : false;
      }
      public bool Optional(Token.TokenType type) => IsNext(type) ? Consume(type) : false;
      public bool Optional(Token.ReservedWord type) => IsNext(type) ? Consume(type) : false;
      public bool Optional(List<Token.TokenType> types,out Token token) { token = Token.ErrorToken; return IsNext(types) ? CanConsume(types,out token) : false; }
      public bool Optional(Token.TokenType type,out Token token) { token = Token.ErrorToken; return IsNext(type) ? CanConsume(type,out token) : false; }

      public bool CanConsume(List<Token.TokenType> types) {
         if (IsNext(types)) {
            Next();
            return true;
         }
         if (options.HasFlag(Options.ThrowOnUnexpectedToken)) {
            throw new Exception($"Expected token of type {types}, but found {Peek().type}");
         }
         return false;
      }

      public bool CanConsume(Token.ReservedWord reservedWord) {
         if (IsNonEmpty() && tokens[0].type == Token.TokenType.RESWORD && tokens[0].rval == reservedWord) {
            Next();
            return true;
         }
         if (options.HasFlag(Options.ThrowOnUnexpectedToken)) {
            throw new Exception($"Expected reserved word {reservedWord}, but found {Peek().rval}");
         }
         return false;
      }
      public bool CanConsume(List<Token.ReservedWord> reservedWords,out Token token) {
         if (IsNext(reservedWords)) {
            token = Next();
            return true;
         }
         if (options.HasFlag(Options.ThrowOnUnexpectedToken)) {
            throw new Exception($"Expected reserved word {reservedWords}, but found {Peek().rval}");
         }
         token = Token.ErrorToken;
         return false;
      }

      /// <summary>
      /// Consume a unit start (MODULE, LAYER, SECTION, PROGRAM or end (ENDMOD, ENDLAY, ENDSEC, ENDPROG) reserved word and an ID and the ending period.
      /// </summary>
      /// <param name="unit">The unit type rewerved word.</param>
      /// <param name="id">If stating a unit, the id is set, If ending a unit, it is verified that the id matches the one given in the unit close.</param>
      /// <returns></returns>
      public bool CanConsumeContainerDelimiter(Token.ReservedWord unit,ref ID id) {
         Debug.Assert(Token.UnitStarters.Contains(unit) || Token.UnitEnders.Contains(unit));
         if (Optional(unit) && CanConsume(out ID thisid) && CanConsumeEnd()) {
            if (Token.UnitStarters.Contains(unit)) {
               id = thisid;
               return true;
            } else if (Token.UnitEnders.Contains(unit)) {
               if (thisid != id) throw new Exception($"Expected {id} in {unit.ToString()} but found {thisid}");
               return true;
            }
         }
         id = TokenList.ErrorID;
         return false;
      }

      internal bool CanConsumeEnd() => CanConsumeTerminator(Token.TokenType.END);
      internal bool CanConsumeSep() => CanConsumeTerminator(Token.TokenType.SEP);
      internal bool CanConsumeTerminator(Token.TokenType terminator) {
         if (IsNext(terminator)) {
            Next();
            return true;
         } else {
            return false;
         }
      }
   }
}
