using System.Diagnostics;

namespace CDL2v1 {
   /// <summary>
   /// Represents a reference to a named element, Arg or Local in the syntax tree.
   /// It contains the token it was created from.
   /// </summary>
   internal class ID : IConstElement, IMacroElement, IActualArg {
      public readonly Token token = Token.ErrorToken;
      public string Name => token.TokenString;
      public Section? section = null;

      /// <summary>
      /// Used to ensure that multiple spellings of tokens produce the same ID.
      /// </summary>
      private static readonly Dictionary<string,ID> UniqueIDs = [];

      /// <summary>
      /// Returns the ID for the given token. If the ID does not exist, it is created.
      /// </summary>
      /// <param id="token"></param>
      /// <returns></returns>
      public static ID From(Token token) {
         Debug.Assert(token.type == TT.ID && token.StringValue != null,"CreateID: Token is not an ID type or StringValue is null");
         return UniqueIDs.TryGetValue(token.StringValue,out ID? id) ? id : UniqueIDs[token.StringValue] = new ID(token);
      }
      /// <summary>
      /// Used to create the Procedures for Section Ludes.
      /// </summary>
      /// <param id="section"></param>
      /// <param id="ludeType">The reserved word representing the lude: PRELUDE, ROOT, POSTLUDE.</param>
      /// <returns></returns>
      public static ID From(Section section,RW ludeType) => From(Token.From(section,ludeType));

      public readonly static ID ErrorID = new();
      public readonly static ID AnonID = new("Anon");


      private ID(Token token) {
         Debug.Assert(token.type == TT.ID && token.StringValue != null,"Program constructor: id not TokenType.ID or StringValue is null");
         this.token = token;
      }

      private ID() { }
      private ID(string name) : this(new Token(name)) { }

      /// <summary>
      /// Changes the Name of an ID. Can be used to change where the spaces are.
      /// </summary>
      /// <param Name="newName"></param>
      public void Rename(string newName) => token.Rename(newName);


      public override bool Equals(object? obj) => obj is ID iD && token == iD.token;
      public override int GetHashCode() => HashCode.Combine(token);
      public override string ToString() => token.TokenString;
      public string AsIdentifier(string separator="_",string replacement="") { 
         string parentPrefix = section is null || section.Parent is null ? "" : $"{section.Parent.AsName(replacement)}{separator}";
         return $"{parentPrefix}{token.AsIdentifier(replacement)}";
      }
 
      public static bool operator ==(ID left,ID right) => left is null ? right is null : left.Equals(right);
      public static bool operator !=(ID left,ID right) => !(left == right);
   }

}
