using System.Collections.Immutable;
using System.Diagnostics;

namespace CDL2v1 {
   /// <summary>
   /// Represents a reference to a named syntactic element, Arg or Local in the syntax tree.
   /// It contains the token it was created from.
   /// </summary>
   internal class ID : IConstElement, IMacroElement, IActualArg {
      public readonly Token token = Token.ErrorToken;
      public string Name => token.TokenString;
      public Container? container = null;

      /// <summary>
      /// Used to ensure that multiple spellings of tokens produce the same ID.
      /// </summary>
      private static readonly Dictionary<string,ID> UniqueIDs = [];

      public static void Dump() {
         Debug.WriteLine("ID Dump:\n--------");
         List<ID> sortedIDs = [.. UniqueIDs.Values
                                     .OrderBy(id => id.Name)
                                     //.ThenBy (id => id.Name)
                                     ];
         int maxNameLength = sortedIDs.Select(id => id.Name.Length).Max();
         // int maxTypeLength = sortedIDs.Select(id => id.TargetType.Length).Max();
         foreach (ID id in sortedIDs) Debug.WriteLine(id.ToString(maxNameLength/*,maxTypeLength*/));
         Debug.WriteLine("--------");
      }

      /// <summary>
      /// Returns the ID for the given token. If the ID does not exist, it is created.
      /// </summary>
      /// <param id="token"></param>
      /// <returns></returns>
      public static ID From(Token token,Type targetType) {
         Debug.Assert(token.type == TT.ID && token.StringValue != null,"CreateID: Token is not an ID type or StringValue is null");
         return UniqueIDs.TryGetValue(token.StringValue,out ID? id) ? id : UniqueIDs[token.StringValue] = new ID(token,targetType);
      }
      /// <summary>
      /// Used to create the Procedures for Section Ludes.
      /// </summary>
      /// <param id="container"></param>
      /// <param id="ludeType">The reserved word representing the lude: PRELUDE, ROOT, POSTLUDE.</param>
      /// <returns></returns>
      public static ID From(Section section,RW ludeType) {
         ID id = From(Token.From(section,ludeType),typeof(Algorithm));
         id.container = section;
         return id;
      }

      public readonly static ID ErrorID = new();
      public readonly static ID AnonID = new("Anon",typeof(Undeclared));


      protected ID(Token token,Type targetType) {
         Debug.Assert(token.type == TT.ID && token.StringValue != null,"Program constructor: id not TokenType.ID or StringValue is null");
         this.token = token;
      }

      private ID() { }
      private ID(string name,Type targetType) : this(new Token(name),targetType) { }

      /// <summary>
      /// Changes the Name of an ID. Can be used to change where the spaces are.
      /// </summary>
      /// <param Name="newName"></param>
      public void Rename(string newName) => token.Rename(newName);


      public override bool Equals(object? obj) => obj is ID iD && token == iD.token;
      public override int GetHashCode() => HashCode.Combine(token);
      public override string ToString() => Name;
      private string ToString(int nameWidth = 0/*,int typeWidth = 0*/) {
         string name = nameWidth > 0 ? string.Format("{0,-" + nameWidth + "}",Name) : Name;
         // string type = typeWidth > 0 ? string.Format("{0,-" + typeWidth + "}",TargetType) : TargetType;
         //return nameWidth == 0 ? Name : $"{name}->{(container == null ? type : type+"   "+container.ToString())}";
         return nameWidth == 0 ? Name : $"{name}->{(container == null ? "N/A" : container.ToString())}";
      }
      public string AsIdentifier(string separator="_",string replacement="") { 
         string parentPrefix = container is null || container.Parent is null ? "" : $"{container.Parent.AsName(replacement)}{separator}";
         return $"{parentPrefix}{token.AsIdentifier(replacement)}";
      }
 
      public static bool operator ==(ID left,ID right) => left is null ? right is null : left.Equals(right);
      public static bool operator !=(ID left,ID right) => !(left == right);
   }

}
