using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CDL2v1 {
   public class Abbreviation<T> : IComparable<Abbreviation<T>> where T : struct, Enum {
      public readonly string Name;
      public readonly int MinLength;
      public readonly T Type;
      public readonly List<T>? Nesting; // Use to determine hierarhcy of focus kewywords, not used for commands
      public string HelpText;

      /// <summary>
      /// Enumeration value that represents an invalid abbreviation. This is used when the input does not match any known abbreviation.
      /// Ensure that this value is defined in the enumeration type <typeparamref name="T"/> as "INVALID".
      /// </summary>
      private static T Invalid => (T)Enum.Parse(typeof(T), "INVALID", ignoreCase: false);

      private static readonly Dictionary<Type,string> ShortTypeNames = new() {
         { typeof(CommandType)   , "Cmd" },
         { typeof(SelectionType)     , "Focus" },
      };
      private static string ShortTypeName => ShortTypeNames.TryGetValue(typeof(T), out string? type) ? type : typeof(T).Name;

      /// <summary>
      /// Compare on the Name of the abbreviation to support SortedSet.
      /// </summary>
      /// <param name="other"></param>
      /// <returns></returns>
      public int CompareTo(Abbreviation<T>? other) => other is null ? 1 : string.Compare(Name,other.Name,StringComparison.Ordinal);

      public override bool Equals(object? obj) => obj is Abbreviation<T> other && Name.Equals(other.Name);

      public override int GetHashCode() => Name.GetHashCode();

      /// <summary>
      /// Must match with the names in the CommandType enum.
      /// </summary>
      public readonly static SortedSet<Abbreviation<CommandType>> Commands = [
         new ("append"  , 1,"append   [SELECTOR] object:  append the object (which must be of the correct type) after the SELECTOR"),
         new ("edit"    , 1,"edit     [SELECTOR]:         edit the selected object"),
         new ("focus"   , 1,"focus    [SELECTOR]:         set the focus to the object described by the selector and display it"),
         new ("generate", 1,"generate [SELECTOR]:         generate code for the selected object which must be a PROGRAM or a MODULE"),
         new ("help"    , 1,"help     [command]:          display this list, or details for a command"),
         new ("insert"  , 1,"insert   [SELECTOR] object:  insert the object (which must be of the correct type) before the SELECTOR"),
         new ("list"    , 1,"list     [SELECTOR]:         list objects that the selector matches"),
         new ("next"    , 1,"next     [SELECTOR]:         move the focus to the next object of the given type"),
         new ("previous", 3,"previous [SELECTOR]:         move the focus to the previous object of the given type"),
         new ("print"   , 1,"print    [SELECTOR]:         pretty print the selected object"),
         new ("quit"    , 4,"quit:                        exit the lab after saving the database"),
         new ("rename"  , 3,"rename   [SELECTOR] name:    rename tbe selected object; may be used just to add/remove spaces"),
         new ("replace" , 1,"replace  [SELECTOR] object:  replaced the selection with the new object"),
         new ("save"    , 1,"save:                        save the database to disk now"),
         new ("set"     , 3,"set      option:             set an option; +/-option for boolean, option=value otherwise"),
         new ("status"  , 4,"status:                      display information about the status of the database"),
         new ("undo"    , 1,"undo:                        undo the last modification; may be repeated (NOT IMPLEMENTED)"),
      ];

      /// <summary>
      /// Must match with the names in the FocusType enum.
      /// </summary>
      public readonly static SortedSet<Abbreviation<SelectionType>> FocusTypes = [
         new ("ABORT"      ,5,SelectionType.ALTERNATIVE),
         new ("ABSTR"      ,5,SelectionType.SECTION),
         new ("ACTION"     ,2,SelectionType.SECTION),
         new ("AFFIX"      ,3,SelectionType.ALGORITHM),
         new ("ALGORITHM"  ,3,SelectionType.SECTION),
         new ("ALTERNATIVE",3,SelectionType.GROUP),
         new ("ARG"        ,3,SelectionType.CALL),
         new ("CALL"       ,1,SelectionType.ALTERNATIVE),
         new ("CONST"      ,3,SelectionType.SECTION),
         new ("EXPORT"     ,3,SelectionType.SECTION),
         new ("EXT"        ,3,SelectionType.SECTION),
         new ("FAIL"       ,4,SelectionType.ALTERNATIVE),
         new ("FUNCTION"   ,2,SelectionType.SECTION),
         new ("GROUP"      ,1,SelectionType.ALGORITHM),
         new ("IMPORT"     ,3,SelectionType.SECTION),
         new ("INV"        ,3,SelectionType.SECTION),
         new ("LAYER"      ,3,SelectionType.MODULE),
         new ("LIST"       ,4,SelectionType.SECTION),
         new ("LOCAL"      ,3,SelectionType.ALGORITHM),
         new ("MACRO"      ,3,SelectionType.SECTION),
         new ("MODULE"     ,1),
         new ("NOTE"       ,4),
         new ("PART"       ,4,SelectionType.PROGRAM),
         new ("POSTLUDE"   ,4,[SelectionType.PROGRAM,SelectionType.MODULE,SelectionType.SECTION]),
         new ("PREDICATE"  ,2,SelectionType.SECTION),
         new ("PRELUDE"    ,3,[SelectionType.PROGRAM,SelectionType.MODULE,SelectionType.SECTION]),
         new ("PROCEDURE"  ,4,SelectionType.SECTION),
         new ("PROGRAM"    ,4),
         new ("REPEAT"     ,3,SelectionType.ALTERNATIVE),
         new ("ROOT"       ,4,[SelectionType.PROGRAM,SelectionType.MODULE,SelectionType.SECTION]),
         new ("SECTION"    ,1,SelectionType.LAYER),
         new ("SUCCEED"    ,7,SelectionType.ALTERNATIVE),
         new ("TEST"       ,2,SelectionType.SECTION),
         new ("VAR"        ,3,SelectionType.SECTION),
      ];

      private static Set<Abbreviation<T>> Abbreviations => typeof(T).Name switch {
         nameof(CommandType) => Commands.Cast<Abbreviation<T>>().ToSet(),
         nameof(SelectionType)   => FocusTypes.Cast<Abbreviation<T>>().ToSet(),
         _ => throw new ArgumentException($"Unknown abbreviation type: {typeof(T).Name}"),
      };

      public Abbreviation(string name, int minLength,List<T>? nesting,string help = "") {
         Name = name;
         MinLength = minLength;
         Type = Enum.Parse<T>(name, true);
         Nesting = nesting;
         HelpText = help;
      }

      public Abbreviation(string name, int minLength, T nesting,string help="") : this(name,minLength,[nesting],help) {}
      public Abbreviation(string name, int minLength, string help = "") : this(name, minLength, [], help) { }

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

      public override string ToString() => $"{ShortTypeName}[{NameWithAbbreviation}]";
      public string NameWithAbbreviation => $"{Name[..MinLength].ToUpper()}{Name[MinLength..]}";
   }
}
