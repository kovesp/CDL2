using System;
using System.Collections.Generic;
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
      public Options options;

      public TokenList(Options options = Options.None) {
         this.options = options;
      }

      public void Add(Token token) => tokens.Add(token);
      public bool IsNonEmpty() => tokens.Count > 0;
      public Token Peek() => IsNonEmpty() ? tokens[0] : Token.ErrorToken;
      public Token Peek(int n) => n < tokens.Count ? tokens[n] : Token.ErrorToken;
      public bool IsNext(Token.TokenType type) => IsNonEmpty() && tokens[0].type == type;
      public bool IsNext(Token.ReservedWord reservedWord) => IsNonEmpty() && tokens[0].type == Token.TokenType.RESWORD && tokens[0].rval == reservedWord;
      public Token Next() {
         if (IsNonEmpty()) {
            Token token = tokens[0];
            Skip();
            return token;
         }
         return Token.ErrorToken;
      }

      public void Skip() => tokens.RemoveAt(0);

      public void SetOptions(Options options) {
         this.options = options;
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
   }
}
