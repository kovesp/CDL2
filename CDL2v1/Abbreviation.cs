// <auto-gen>
//=======================================================================
// <copyright file="Abbreviation.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-06-20</creation-date>
// 
// <summary>
//   Handles command and selector type abbreviations.
//   Contains the command and selector type tables.
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

using System.Text.RegularExpressions;

namespace CDL2v1 {
   public class Abbreviation<T> : IComparable<Abbreviation<T>> where T : struct, Enum {
      public readonly string Name;
      public readonly int MinLength;
      public readonly T Type;
      public readonly List<T>? Containers; // Use to determine hierarchy of focus keywords, not used for commands
      public readonly bool IsFocusable = true;
      public string HelpText;

      /// <summary>
      /// Enumeration value that represents an invalid abbreviation. This is used when the input does not match any known abbreviation.
      /// Ensure that this value is defined in the enumeration type <typeparamref name="T"/> as "INVALID".
      /// </summary>
      private static T Invalid => (T)Enum.Parse(typeof(T),"INVALID",ignoreCase: false);

      private static readonly Dictionary<Type,string> ShortTypeNames = new() {
         { typeof(CommandType)   , "Cmd" },
         { typeof(SelectorType)     , "Focus" },
      };
      private static string ShortTypeName => ShortTypeNames.TryGetValue(typeof(T),out string? type) ? type : typeof(T).Name;

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
         new ("abort"    , 5,"abort:                        exit the Lab without saving the database"),
         new ("add"      , 1,"add       [SELECTOR]:         edit mode is entereed in the input area and the resulting object is added"),
         new ("analyze"  , 4,"analyze   [SELECTOR]:         perform semantic analysis on the selected program"),
         new ("bye"      , 3,"bye:                          exit the Lab after saving the database"),
         new ("bottom"   , 3,"bottom:                       move the focused object to be the last among its siblings"),
         new ("consult"  , 1,"consult   file-name:          read the file which must contain one or more CDL2 modules and/or programs"),
         new ("delete"   , 3,"delete    [SELECTOR]:         deletes (removes) the selected object (eventually this can be undone ... but not yet"),
         new ("down"     , 4,"down      [n]:                move the focused object n (or 1) higher among its siblings"),
         new ("duplicate", 3,"duplicate [name]:             duplicate this algorithm, constant, variable, list with a new name. If name is omitted, add copy to the current name"),
         new ("edit"     , 1,"edit      [SELECTOR]:         edit the selected object"),
         new ("first"    , 5,"first:                        set the focus to the first element in a sequence"),
         new ("delete"   , 3,"delete    [SELECTOR]:         delete the selected object"),
         new ("exit"     , 4,"exit:                         exit the Lab after saving the database"),
         new ("focus"    , 1,"focus     [SELECTOR]:         set the focus to the object described by the selector and display it"),
         new ("generate" , 1,"generate  SELECTOR:           generate code for the selected object which must be a PROGRAM or a MODULE"),
         new ("help"     , 1,"help      [command]:          display this list, or details for a command"),
         new ("last"     , 4,"last:                         move the focus to the last object in a sequence"),
         new ("list"     , 1,"list      [SELECTOR]:         list objects that the selector matches"),
         new ("move"     , 1,"move      [SELECTOR]:         move the focused object after or -before the selctor (NOT IMPLEMENTED)"),
         new ("next"     , 1,"next      [n | UNIT]:         move the focus to the nth next object in a sequence or to the next object of the given type"),
         new ("previous" , 1,"previous  [n | UNIT]:         move the focus to the nth previous object in a sequence or to the previous object of the given type"),
         new ("print"    , 2,"print     [SELECTOR]:         pretty print the selected object"),
         new ("redo"     , 4,"redo:     [n]:                redo the last (n) undo(s)"),
         new ("remove"   , 3,"remove    [SELECTOR]:         removes(deletes) the selected object (eventually this can be undone ... but not yet"),
         new ("rename"   , 3,"rename    [SELECTOR] name:    rename the selected object; may be used just to add/remove spaces"),
         new ("quit"     , 4,"quit:                         exit the Lab after saving the database"),
         new ("save"     , 1,"save:                         save the database to disk now"),
         new ("shell"    , 2,"shell     system command:     execute a system command"),
         new ("set"      , 3,"set       option:             set an option; +/-option for boolean, option=value otherwise"),
         new ("status"   , 4,"status:                       display information about the status of the database"),
         new ("type"     , 1,"type     [SELECTOR]:          pretty print the selected object"),
         new ("top"      , 3,"top:                          move the focused object to be the first among its siblings"),
         new ("undo"     , 1,"undo:    [n]:                 undo the last (n) modification(s); may be repeated"),
         new ("up"       , 2,"up       [n]:                 move the focused object n (or 1) higher among its siblings"),
#if DEBUG
         new ("vsdebug" , 7,"vsdebug:                       break into the VS debugger when running under Visual Studio"),
#endif
      ];
      public readonly static Set<string> ExitCommands = new(["exit","quit","bye","abort"]);

      private static readonly List<SelectorType> AlgorithmTypes = [
         SelectorType.ALGORITHM, SelectorType.PROCEDURE, SelectorType.ACTION, SelectorType.FUNCTION, SelectorType.PREDICATE, SelectorType.TEST
      ];
      private static readonly List<SelectorType> AlgorithmTypesWithMacro = AlgorithmTypes.With(SelectorType.MACRO);
      /// <summary> 
      /// Must match with the names in the FocusType enum.
      /// </summary>
      public readonly static SortedSet<Abbreviation<SelectorType>> FocusTypes = [
         // Matched by CONTAINER and ANY
         new ("PROGRAM"    ,1),
         new ("MODULE"     ,1),
         new ("LAYER"      ,1,SelectorType.MODULE),
         new ("SECTION"    ,1,SelectorType.LAYER),
         // Matched by FACE and ANY
         new ("ABSTR"      ,5,SelectorType.SECTION,focusable:false),
         new ("EXT"        ,3,SelectorType.SECTION,focusable:false),
         new ("INV"        ,3,SelectorType.SECTION,focusable:false),
         new ("EXPORT"     ,3,SelectorType.SECTION,focusable:false),
         new ("IMPORT"     ,3,SelectorType.SECTION,focusable:false),
         // Prefixes for other selectors
         new ("IMPORTED"   ,8,SelectorType.SECTION,focusable:false,help:"Prefix to another selector to limit to imported algorithms and constants (same as STUB)"),
         new ("STUB"       ,4,SelectorType.SECTION,focusable:false,help:"Prefix to another selector to limit to imported algorithms and constants (same as IMPORTED)"),
         new ("FULL"       ,4,SelectorType.SECTION,focusable:false,help:"Prefix to another selector to limit to non-imported algorithms and constants"),
         // Matched by DATA, OBJECT and ANY
         new ("CONST"      ,3,SelectorType.SECTION),
         new ("LIST"       ,4,SelectorType.SECTION),
         new ("VAR"        ,3,SelectorType.SECTION),
         // Matched by ALGORITHM, OBJECT and ANY
         new ("ACTION"     ,2,SelectorType.SECTION),
         new ("FUNCTION"   ,2,SelectorType.SECTION),
         new ("MACRO"      ,3,SelectorType.SECTION),
         new ("PROCEDURE"  ,4,SelectorType.SECTION),
         new ("PREDICATE"  ,2,SelectorType.SECTION),
         new ("TEST"       ,2,SelectorType.SECTION),
         // Matched by ANY
         new ("NOTE"       ,4),
         new ("PART"       ,4,SelectorType.PROGRAM),
         new ("POSTLUDE"   ,4,[SelectorType.PROGRAM,SelectorType.MODULE,SelectorType.SECTION],focusable:false),
         new ("PRELUDE"    ,4,[SelectorType.PROGRAM,SelectorType.MODULE,SelectorType.SECTION],focusable:false),
         new ("ROOT"       ,4,[SelectorType.PROGRAM,SelectorType.MODULE,SelectorType.SECTION],focusable:false),
         // Non focusable types, matched by ANY
         new ("AFFIX"      ,3,AlgorithmTypesWithMacro,focusable:false),
         new ("CALL"       ,1,SelectorType.PROCEDURE,focusable:false), // Calls occur also in section ludes, however the ludes are represented in the syntax tree as procedures
         new ("LOCAL"      ,3,AlgorithmTypesWithMacro,focusable:false),
         new ("BUILTIN"    ,7,SelectorType.PROCEDURE,focusable:false),
         new ("NOTE"       ,4,SelectorType.PROCEDURE,focusable:false),
         // Generic types
         new ("ALGORITHM"  ,3,SelectorType.SECTION,help:"Selects any algorithm (macro, procedure, imported)"),
         new ("ANY"        ,3),
         new ("CONTAINER"  ,9,[SelectorType.PROGRAM,SelectorType.MODULE,SelectorType.LAYER],help:"Selects program, module, layer, or section"),
         new ("DATA"       ,4,SelectorType.SECTION,help:"Selects any const, var, or list"),
         new ("FACE"       ,4,[SelectorType.PROGRAM,SelectorType.MODULE,SelectorType.SECTION],focusable:false,help:"Selects any interface list"),
         new ("LUDE"       ,4,[SelectorType.PROGRAM,SelectorType.MODULE,SelectorType.SECTION],focusable:false,help:"Selects prelude, root, or postlude"),
         new ("OBJECT"     ,3,SelectorType.SECTION,help:"Selects any DATA or ALGORITHM"),
      ];

      public readonly static Dictionary<SelectorType,Abbreviation<SelectorType>> FocusTypeMap = FocusTypes.ToDictionary(abbrev => abbrev.Type,abbrev => abbrev);
      /// <summary>
      /// Return true if the first selection type is a valid ancestor of the second selection type.
      /// </summary>
      /// <param name="ancestor"></param>
      /// <param name="child"></param>
      /// <returns></returns>
      /// <example>
      ///   AncestorFocusType(FocusType.MODULE, FocusType.CALL); // True, because MODULE => LAYER => SECTION => PROCEDURE => GROUP => ALTERNATIVE => CALL
      /// </example>
      public static bool AncestorFocusTypeOf(SelectorType ancestor,SelectorType child) {
         if (ancestor == SelectorType.INVALID || child == SelectorType.INVALID) return false;
         List<SelectorType>? containers = FocusTypeMap[child].Containers; // The direct containers of the child type
         return containers is not null && (containers.Contains(ancestor) || containers.Any(container => AncestorFocusTypeOf(ancestor,container)));
      }

      public static bool Focusable(SelectorType type) => FocusTypeMap.TryGetValue(type,out Abbreviation<SelectorType>? abbrev) && abbrev.IsFocusable;

      private static Set<Abbreviation<T>> Abbreviations => typeof(T).Name switch {
         nameof(CommandType) => Commands.Cast<Abbreviation<T>>().ToSet,
         nameof(SelectorType) => FocusTypes.Cast<Abbreviation<T>>().ToSet,
         _ => throw new ArgumentException($"Unknown abbreviation type: {typeof(T).Name}"),
      };

      public Abbreviation(string name,int minLength,List<T>? nesting,string help = "",bool focusable = true) {
         Name = name;
         MinLength = minLength;
         Type = Enum.Parse<T>(name,true);
         Containers = nesting;
         HelpText = help;
         IsFocusable = focusable;
      }

      public Abbreviation(string name,int minLength,T nesting,string help = "",bool focusable = true) : this(name,minLength,[nesting],help,focusable: focusable) { }
      public Abbreviation(string name,int minLength,string help = "") : this(name,minLength,[],help) { }

      /// <summary>
      /// Identifies the type or command associated with the specified word based on predefined abbreviations.
      /// </summary>
      /// <param name="name">The word to identify. Must not be null, empty, or consist only of whitespace.</param>
      /// <returns>The identified type of <typeparamref name="T"/> if a matching abbreviation is found;  otherwise, returns <see
      /// cref="Abbreviation{T}.Invalid"/>. If thee is more than one match, the one with the shortest minimum length is chosen.</returns>
      /// <example>
      ///    Abbreviation<CommandType>.Identify(name);
      /// </example>
      public static T Identify(string name) => IdentifyAbbreviation(name)?.Type ?? Abbreviation<T>.Invalid;
      public static Abbreviation<T>? IdentifyAbbreviation(string name) {
         name = name.Trim().ToFirstLetterCase();
         return Abbreviations.Where(abbr => name.Length >= abbr.MinLength && abbr.Name.StartsWith(name)).MinBy(abbr => abbr.MinLength);
      }

      /// <summary>
      /// Currently returns the short help text for all commands.
      /// </summary>
      /// <param name="name"></param>
      /// <returns></returns>
      public static string LongHelp(string name,bool toastFormat = false) {
         static string SingleCommandHelp(string text,string name) => $"{Regex.Replace(text,@"^[a-z]+",name + "|",RegexOptions.Compiled).Replace(':','|')}";
         Abbreviation<T>? cmd = IdentifyAbbreviation(name);
         if (cmd is not null) {
            return SingleCommandHelp(cmd.HelpText,cmd.NameWithAbbreviation);
         } else {
            return string.Join("\n",Abbreviation<CommandType>.Commands.Select(cmd => SingleCommandHelp(cmd.HelpText,cmd.NameWithAbbreviation)));
         }

      }
      public override string ToString() => $"{ShortTypeName}[{NameWithAbbreviation}]";
      public string NameWithAbbreviation => char.IsLower(Name[0])
         ? $"{MinimumAbreviation().ToUpper()}{Name[MinLength..]}"
         : $"{MinimumAbreviation()}{Name[MinLength..].ToLower()}";
      public string MinimumAbreviation(int min = 0) => Name[..Math.Max(min,MinLength)];
   }
}

