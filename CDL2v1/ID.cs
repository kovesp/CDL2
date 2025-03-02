using System.Diagnostics;

namespace CDL2v1 {
   /// <summary>
   /// Represents a reference to a named element, Arg or Local in the syntax tree.
   /// It contains the token it was created from.
   /// </summary>
   internal class ID : IConstElement, IMacroElement, IActualArg {
      public readonly Token token = Token.ErrorToken;
      public readonly string name = Token.ErrorToken.tokenString;
      public SymbolTable? owner;

      /// <summary>
      /// Used to ensure that multiple spellings of tokens produce the same ID.
      /// </summary>
      private static readonly Dictionary<string,ID> UniqueIDs = [];

      /// <summary>
      /// Returns the ID for the given token. If the ID does not exist, it is created.
      /// </summary>
      /// <param name="token"></param>
      /// <returns></returns>
      public static ID From(Token token) {
         Debug.Assert(token.type == TT.ID && token.sval != null,"CreateID: Token is not an ID type or sval is null");
         return UniqueIDs.TryGetValue(token.sval,out ID? id) ? id : UniqueIDs[token.sval] = new ID(token);
      }
      /// <summary>
      /// Used to create the Proceures for Section Ludes.
      /// </summary>
      /// <param name="section"></param>
      /// <param name="ludetype">The reserved word representing the lude: PRELUDE, ROOT, POSTLUDE.</param>
      /// <returns></returns>
      public static ID From(Section section,RW ludetype) => From(Token.From(section,ludetype));

      public readonly static ID ErrorID = new();
      public readonly static ID AnonID = new("Anon");

      public Token Token => token;

      public ID Id => this;

      private ID(Token token) {
         Debug.Assert(token.type == TT.ID && token.sval != null,"Program constructor: id not TokenType.ID or sval is null");
         this.token = token;
         name = token.tokenString;
      }
      protected ID(ID id) : this(id.token) { }
      private ID() { }
      private ID(string name) : this(new Token(name)) { }


      public override bool Equals(object? obj) => obj is ID iD && token == iD.token;
      public override int GetHashCode() => HashCode.Combine(token);
      public override string ToString() => token.tokenString;
      public string AsIdentifier(string separator="_",string replacement="") { 
         string parentPrefix = owner is null || owner.Owner is null ? "" : $"{owner.Owner.AsName(replacement)}{separator}";
         return $"{parentPrefix}{token.AsIdentifier(replacement)}";
      }
 
      public static bool operator ==(ID left,ID right) => left is null ? right is null : left.Equals(right);
      public static bool operator !=(ID left,ID right) => !(left == right);
   }

}
