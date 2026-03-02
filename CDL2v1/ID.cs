// <auto-gen>
//=======================================================================
// <copyright file="ID.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-02-27</creation-date>
// 
// <summary>
//   Implements CDL2 identifiers. Includes support for the equivalence of identifiers that have spaces in them.
//   The first occurrence of spacing is used. The Database class maintains the mapping between the canonical names (no spaces), and the single version with spaces. 
// </summary>
// <attribution>
//   This file is part of the clean room reimplementation of the
//      CDL2 Compiler
//      CDL2 Laboratory
//      CDL2 Target Code Generators
//
//    Based on original work on CDL and CDL2 led by C. H. A. Koster
//    and the CDL2 team at the Universities of Berlin, Germany and
//    Nijmegen, The Netherlands.
//
//    The CDL2 Laboratory was the work of Epsilon GmbH, Berlin.
//    H. M. Stahl, H. Feuerhahn, JP. Dehotay, B. Böhringer
//    (and others I don't remember ... sorry).
//
//    This project is not affiliated with the original CDL2 project.
// </attribution>
//=======================================================================
// </auto-gen>

using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CDL2v1 {
   /// <summary>
   /// Represents a reference to a named syntactic element, Arg or Local in the syntax tree.
   /// </summary>
   public partial class ID : IElement, IActualArg, IComparable {
      [JsonInclude]
      public string CanonicalName = string.Empty;
      [JsonIgnore]
      public string Name => Database.Instance.DisplayName(CanonicalName);

      [JsonIgnore]
      public ID Id => this;

      public bool IsAnonymous => this == AnonID;
      public bool IsAnonymousGroup => IsAnonymous || Name.StartsWith(Parser.GroupIDLabelPrefix);

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
      public int CompareTo(object? obj) => obj is ID id ? CanonicalName.CompareTo(id.CanonicalName) : 1;
      internal void Rename(string newName) => CanonicalName = Database.Instance.RenameCanonicalName(CanonicalName,newName);

      /// <summary>
      /// True if the name matches this ID. If the name starts with '/', it is treated as a regular expression.
      /// </summary>
      /// <param name="name">A name, a regular expression or * which matches any ID.</param>
      /// <returns></returns>
      public bool Matches(string name) => name == "*" || this == name || (name.StartsWith('/') && new Regex(name[1..]).IsMatch(CanonicalName));
      public string Quoted(string quote="\"") => $"{quote}{Name}{quote}";

      public static bool operator ==(ID left,ID right) => left is null ? right is null : left.Equals(right);
      public static bool operator ==(ID left,string right) => left is null ? right is null : left.Equals(right);
      public static bool operator ==(string left,ID right) => left is null ? right is null : right.Equals(left);
      public static bool operator !=(ID left,ID right) => !(left == right);
      public static bool operator !=(ID left,string right) => !(left == right);
      public static bool operator !=(string left,ID right) => !(left == right);
   }

   public class IDDictionary<V> : Dictionary<ID,V> { }
   public class IDSet : Set<ID> { }

}

