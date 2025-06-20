using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CDL2v1 {
   public class Abbreviation<T>(string name, int minLength, List<T> nesting) {
      public readonly string Name = name;
      public readonly int MinLength = minLength;
      public readonly T Type = (T)Enum.Parse(typeof(T), name, true);
      public readonly List<T>? Nesting = nesting; // Use to determine hierarhcy of focus kewywords, not used for commands

      /// <summary>
      /// Must match with the names in the CommandType enum.
      /// </summary>
      private readonly static Set<Abbreviation<CommandType>> Commands = [
         new ("focus"   , 1),
         new ("next"    , 1),
         new ("prev"    , 1),
         new ("list"    , 1),
         new ("print"   , 2),
         new ("set"     , 3),
         new ("replace" , 1),
         new ("rename"  , 3),
         new ("append"  , 1),
         new ("insert"  , 1),
         new ("edit"    , 1),
         new ("undo"    , 1),
         new ("generate", 1),
         new ("quit"    , 4),
         new ("help"    , 1),
         new ("status"  , 4),
      ];

      private readonly static Set<Abbreviation<FocusType>> FocusTypes = [
         new ("PROGRAM"    ,4),
         new ("PART"       ,4,FocusType.PROGRAM),
         new ("MODULE"     ,3),
         new ("LAYER"      ,3,FocusType.MODULE),
         new ("SECTION"    ,3,FocusType.LAYER),
         new ("ABSTR"      ,5,FocusType.SECTION),
         new ("EXT"        ,3,FocusType.SECTION),
         new ("INV"        ,3,FocusType.SECTION),
         new ("EXPORT"     ,3,FocusType.SECTION),
         new ("IMPORT"     ,3,FocusType.SECTION),
         new ("ROOT"       ,1,[FocusType.PROGRAM,FocusType.MODULE,FocusType.SECTION]),
         new ("PRELUDE"    ,3,[FocusType.PROGRAM,FocusType.MODULE,FocusType.SECTION]),
         new ("POSTLUDE"   ,4,[FocusType.PROGRAM,FocusType.MODULE,FocusType.SECTION]),
         new ("CONST"      ,3,FocusType.SECTION),
         new ("VAR"        ,3,FocusType.SECTION),
         new ("LIST"       ,4,FocusType.SECTION),
         new ("ACTION"     ,2,FocusType.SECTION),
         new ("FUNCTION"   ,2,FocusType.SECTION),
         new ("TEST"       ,2,FocusType.SECTION),
         new ("PREDICATE"  ,2,FocusType.SECTION),
         new ("NOTE"       ,4),
         new ("ALTERNATIVE",1,FocusType.GROUP),
         new ("GROUP"      ,2,FocusType.ALGORITHM),
         new ("CALL"       ,4,FocusType.ALTERNATIVE),
         new ("ALGORITHM"  ,3,FocusType.SECTION),
         new ("AFFIX"      ,3,FocusType.ALGORITHM),
         new ("LOCAL"      ,3,FocusType.ALGORITHM),
         new ("ARG"        ,3,FocusType.CALL),
         ];

      public Abbreviation(string name, int minLength, T? nesting = default) : this(name,minLength,[nesting]) {}

      private static S? Identify<S>(string word,Set<Abbreviation<S>> abbreviations,S invalid) {
         if (string.IsNullOrWhiteSpace(word)) return invalid;
         word = word.Trim().ToLower();
         foreach (Abbreviation<S> abbrev in abbreviations) {
            if (word.Length >= abbrev.MinLength && abbrev.Name.StartsWith(word)) {
               return abbrev.Type;
            }
         }
         return invalid;
      }
      public static CommandType IdentifyCommand(string command) => Identify<CommandType>(command,Commands,CommandType.INVALID);
      public static FocusType IdentifyFocusType(string focusType) => Identify<FocusType>(focusType,FocusTypes,FocusType.INVALID);

      public override string ToString() => $"CMD[{Name[..MinLength].ToUpper()}{Name[MinLength..]}]";
   }
}
