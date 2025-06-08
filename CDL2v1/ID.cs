using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CDL2v1 {
   /// <summary>
   /// Represents a reference to a named syntactic element, Arg or Local in the syntax tree.
   /// </summary>
   public partial class ID : IElement, IActualArg {
      [JsonInclude]
      public string CanonicalName = string.Empty;
      [JsonIgnore]
      public string Name=> Database.Instance.DisplayName(CanonicalName);

      [JsonIgnore]
      public ID Id => this;

      /// <summary>
      /// Returns the ID for the given token. If the ID does not exist, it is created.
      /// </summary>
      /// <param Id="token"></param>
      /// <returns></returns>
      public static ID From(Token token) {
         Debug.Assert(token.type == TT.ID && token.StringValue != null,"CreateID: Token is not an ID type or StringValue is null");
         return ID.From(token.TokenString);
      }
      public static ID From(string name) => new(Database.Instance.AddCanonicalName(name));
      /// <summary>
      /// Used to create the Procedures for SectionById Ludes.
      /// </summary>
      /// <param Id="container"></param>
      /// <param Id="ludeType">The reserved word representing the lude: PRELUDE, ROOT, POSTLUDE.</param>
      /// <returns></returns>
      public static ID From(RW ludeType) => From(ludeType.ToString());

      public readonly static ID ErrorID = new("ERROR");
      public readonly static ID AnonID = new("Anon");

      public ID() { }
      [JsonConstructor]
      public ID(string name) => CanonicalName = name;

      public override bool Equals(object? obj) => (obj is ID id && CanonicalName == id.CanonicalName) || (obj is string s && CanonicalName == s.Replace(" ",""));
      public override int GetHashCode() => HashCode.Combine(CanonicalName);
      public override string ToString() => Name;
 
      public static bool operator ==(ID left,ID right) => left is null ? right is null : left.Equals(right);
      public static bool operator ==(ID left, string right) => left is null ? right is null : left.Equals(right);
      public static bool operator ==(string left, ID right) => left is null ? right is null : right.Equals(left);
      public static bool operator !=(ID left,ID right) => !(left == right);
      public static bool operator !=(ID left, string right) => !(left == right);
      public static bool operator !=(string left, ID right) => !(left == right);
   }

   public class IDDictionary<V> : Dictionary<ID, V> { }
   public class IDSet : Set<ID> { }

}
