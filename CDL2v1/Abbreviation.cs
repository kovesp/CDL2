using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CDL2v1 {
   public class Abbreviation<T> where T : struct, Enum {
      public readonly string Name;
      public readonly int MinLength;
      public readonly T Type;
      public readonly List<T>? Nesting; // Use to determine hierarhcy of focus kewywords, not used for commands

      /// <summary>
      /// Enumeration value that represents an invalid abbreviation. This is used when the input does not match any known abbreviation.
      /// Ensure that this value is defined in the enumeration type <typeparamref name="T"/> as "INVALID".
      /// </summary>
      private static T Invalid => (T)Enum.Parse(typeof(T), "INVALID", ignoreCase: false);

      private static readonly Dictionary<Type,string> ShortTypeNames = new() {
         { typeof(CommandType)   , "Cmd" },
         { typeof(FocusType)     , "Focus" },
      };
      private string ShortTypeName => ShortTypeNames.TryGetValue(typeof(T), out string? type) ? type : typeof(T).Name;



      /// <summary>
      /// Must match with the names in the CommandType enum.
      /// </summary>
      private readonly static Set<Abbreviation<CommandType>> Commands = [
         new ("append"  , 1),
         new ("edit"    , 1),
         new ("focus"   , 1),
         new ("generate", 1),
         new ("help"    , 1),
         new ("insert"  , 1),
         new ("list"    , 1),
         new ("next"    , 1),
         new ("prev"    , 1),
         new ("print"   , 2),
         new ("quit"    , 4),
         new ("rename"  , 3),
         new ("replace" , 1),
         new ("set"     , 3),
         new ("status"  , 4),
         new ("undo"    , 1),
      ];

      /// <summary>
      /// Must match with the names in the FocusType enum.
      /// </summary>
      private readonly static Set<Abbreviation<FocusType>> FocusTypes = [
         new ("ABSTR"      ,5,FocusType.SECTION),
         new ("ACTION"     ,2,FocusType.SECTION),
         new ("AFFIX"      ,3,FocusType.ALGORITHM),
         new ("ALGORITHM"  ,3,FocusType.SECTION),
         new ("ALTERNATIVE",3,FocusType.GROUP),
         new ("ARG"        ,3,FocusType.CALL),
         new ("CALL"       ,1,FocusType.ALTERNATIVE),
         new ("CONST"      ,1,FocusType.SECTION),
         new ("EXPORT"     ,3,FocusType.SECTION),
         new ("EXT"        ,3,FocusType.SECTION),
         new ("FUNCTION"   ,2,FocusType.SECTION),
         new ("GROUP"      ,1,FocusType.ALGORITHM),
         new ("IMPORT"     ,3,FocusType.SECTION),
         new ("INV"        ,3,FocusType.SECTION),
         new ("LAYER"      ,3,FocusType.MODULE),
         new ("LIST"       ,4,FocusType.SECTION),
         new ("LOCAL"      ,3,FocusType.ALGORITHM),
         new ("MODULE"     ,1),
         new ("NOTE"       ,4),
         new ("PART"       ,4,FocusType.PROGRAM),
         new ("POSTLUDE"   ,4,[FocusType.PROGRAM,FocusType.MODULE,FocusType.SECTION]),
         new ("PREDICATE"  ,2,FocusType.SECTION),
         new ("PRELUDE"    ,3,[FocusType.PROGRAM,FocusType.MODULE,FocusType.SECTION]),
         new ("PROGRAM"    ,4),
         new ("ROOT"       ,4,[FocusType.PROGRAM,FocusType.MODULE,FocusType.SECTION]),
         new ("SECTION"    ,1,FocusType.LAYER),
         new ("TEST"       ,2,FocusType.SECTION),
         new ("VAR"        ,3,FocusType.SECTION),
      ];

      private static Set<Abbreviation<T>> Abbreviations => typeof(T).Name switch {
         nameof(CommandType) => Commands.Cast<Abbreviation<T>>().ToSet(),
         nameof(FocusType)   => FocusTypes.Cast<Abbreviation<T>>().ToSet(),
         _ => throw new ArgumentException($"Unknown abbreviation type: {typeof(T).Name}"),
      };

      public Abbreviation(string name, int minLength, List<T>? nesting = null) {
         Name = name;
         MinLength = minLength;
         Type = Enum.Parse<T>(name, true);
         Nesting = nesting;
      }

      public Abbreviation(string name, int minLength, T nesting) : this(name,minLength,[nesting]) {}

      /// <summary>
      /// Identifies the type associated with the specified word based on predefined abbreviations.
      /// </summary>
      /// <remarks>The method trims the input word and applies case normalization before attempting to match
      /// it  against a set of abbreviations. A match is determined if the word meets the minimum length  requirement
      /// and starts with the abbreviation's name.</remarks>
      /// <param name="name">The word to identify. Must not be null, empty, or consist only of whitespace.</param>
      /// <returns>The identified type of <typeparamref name="T"/> if a matching abbreviation is found;  otherwise, returns <see
      /// cref="Abbreviation{T}.Invalid"/>.</returns>
      /// <example>
      ///    Abbreviation<CommandType>.Identify(name);
      /// </example>
      public static T Identify(string name) =>
         ((Func<string,T>)
            (word => Abbreviations.FirstOrDefault(abbrev => word.Length >= abbrev.MinLength && abbrev.Name.StartsWith(word))?.Type ?? Abbreviation<T>.Invalid)
         )(name.Trim().ToFirstLetterCase());

      public override string ToString() => $"{ShortTypeName}[{Name[..MinLength].ToUpper()}{Name[MinLength..]}]";
   }
}
