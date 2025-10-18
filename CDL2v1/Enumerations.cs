// <auto-gen>
//=======================================================================
// <copyright file="Enumerations.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-02-16</creation-date>
// 
// <summary>
//   Contains the various enumerations used in the rest of the code.
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

// Ignore Spelling: CDL

global using TT  = CDL2v1.TokenType;
global using RW  = CDL2v1.ReservedWord;
global using AD  = CDL2v1.AffixDir;
global using AT  = CDL2v1.AffixType;
global using LCT = CDL2v1.LastCallType;
global using SE  = CDL2v1.SyntacticElement;
global using ANT = CDL2v1.AlgorithmNameType;
global using DS  = CDL2v1.DecorationStyle;
global using AS  = CDL2v1.AnnotationSymbol;
global using SA  = CDL2v1.AnnotationSymbols;
global using PBT = CDL2v1.ProcedureBodyType;
global using ST  = CDL2v1.SelectorType;
using System.Text;

namespace CDL2v1 {
   /// Central place for enumerations that are used across the Compiler as well as their abbreviations.
   /// This would be called the representation of the CDL2 language.


   public enum SpaceCharacters {
      Figure      = '\u2007', // Unicode character for figure space, width of a digit
      Em          = '\u2003', // Unicode character for em space, width of an 'M' in the current font
      En          = '\u2002', // Unicode character for en space, width of an 'N' in the current font, 1/2 em
      ThreePerEm  = '\u2004', // Unicode character for three-per-em space, width of 1/3 of an em space
      Punctuation = '\u2008', // Unicode character for punctuation space, width of a period
      Thin        = '\u2009', // Unicode character for thin space, 1/5 em
      Hair        = '\u200A', // Unicode character for hair space, 1/10 em
   }


   /// <summary>
   /// Token types for the CDL2 language.
   /// The aliases are meant to be used in the parser to make the code more readable.
   /// </summary>


#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA1069 // Enums values should not be duplicated
   public enum TokenType {
      ERROR          = 0,
      RESWORD        = 1,
      INT            = 2,
      FLOAT          = 3,
      STRING         = 4,
      ID             = 5,
      PLUS           = 6,
      AFFIXSEP       = 6,  // Alias for PLUS
      SUCCEED        = 6,  // Alias for PLUS
      MINUS          = 7,
      LOCALSEP       = 7,  // Alias for MINUS
      FAIL           = 7,  // Alias for MINUS
      STAR           = 8,
      REPEAT         = 8,  // Alias for STAR
      STRINGAFFIXSEP = 8,  // Alias for STAR
      ABORT          = 9,
      AFFIXDIR       = 10,
      COLON          = 11,
      LABELSEP       = 11,  // Alias for COLON
      PROCBODY       = 11,  // Alias for COLON
      LISTBOUNDSEP   = 11,  // Alias for COLON
      INLINEPROCBODY = 12,
      EQUALS         = 13,
      MACROBODY      = 13,  // Alias for EQUALS
      MACROPROCBODY  = 14,
      PERIOD         = 15,
      END            = 15,  // Alias for PERIOD
      COMMA          = 16,
      SEP            = 16,  // Alias for COMMA
      CALLSEP        = 16,  // Alias for COMMA
      LISTSEP        = 16,  // Alias for COMMA
      SEMICOLON      = 17,
      ALTSEP         = 17,  // Alias for SEMICOLON
      ELEMSEP        = 17,  // Alias for SEMICOLON
      GRPOPEN        = 18,
      LISTBOUNDSTART = 18,  // Alias for GRPOPEN
      GRPCLOSE       = 19,
      LISTBOUNDEND   = 19,  // Alias for GRPCLOSE
      COMMENT        = 20,
      NOBODY         = 21,  // IMPORTed ALGORITHMs have no body
   }
#pragma warning restore CA1069 // Enums values should not be duplicated
#pragma warning restore IDE0079 // Remove unnecessary suppression

   /// <summary>
   /// Reserved words for the CDL2 language.
   /// </summary>
   public enum ReservedWord { 
      PROGRAM, 
      ENDPROG, 
      PART, 
      MODULE, 
      ENDMOD, 
      LAYER, 
      ENDLAY, 
      SECTION,
      ENDSEC, 
      ABSTR, 
      EXT, 
      INV, 
      EXPORT, 
      IMPORT, 
      ROOT, 
      PRELUDE, 
      POSTLUDE, 
      CONST, 
      VAR, 
      LIST, 
      ACTION, 
      FUNCTION, 
      TEST, 
      PREDICATE,
      NOTE,

      // Reserved for use as a call qualifier. 
      // Examples of called built-ins might be:
      // FUNCTION date string+date>.
      // FUNCTION time string+time>.
      // FUNCTION version string+version>.
      // FUNCTION option*name+value>.
      // TEST     is option*name.                      // Can be used for conditional compilation.
      // TEST     is option value*name*value.          // Can be used for conditional compilation..
      // FUNCTION environment variable*name+value>.
      // TEST     is environment variable*name.       // Is it defined?
      // TEST     is target*target                    // The target the given one? In principle can be used to select code for different targets.
      // Syntax and usage examples:
      //   FUNCTION add+>a+>b+c>:
      //      BUILTIN is target*"PowerShell", ps add+a+b+c;
      //      BUILTIN is target*"C#", cs add+a+b+c;
      //      add+a+b+c.
      // With this, the Compiler could avoid generating code for the second alternative, and with full flow analysis also avoid generating code for
      // whatever is called from there recursively.
      //   ACTION parse prolog:
      //      BUILTIN is option value*"Prolog Syntax"*"MProlog", parse mprolog;
      //      parse clocksin mellish.
      //   
      BUILTIN,
      NONE,
   }


   /// <summary>
   /// Command types for the command interpreter. See the Commands set in Abbreviations.
   /// </summary>
   public enum CommandType {
      INVALID,
      abort,
      append,
      add,
      bye,
      consult,
      delete,
      edit,
      exit,
      first,
      focus,
      generate,
      help,
      insert,
      last,
      list,
      next,
      previous,
      print,
      remove,
      redo,
      rename,
      replace,
      quit,
      save,
      set,
      status,
      type,
      undo,
#if DEBUG
      vsdebug,
#endif
   }

   /// <summary>
   /// Focus types.
   /// For simplicity, the FocusTypes contin the SelectionTypes as well.
   /// This allows a single abbreviation table for both.
   /// Differentiation will be made using SelectionType
   /// </summary>
   // cspell:disable
   public enum SelectorType {
      INVALID,
      ABSTR,
      ACTION,
      ALGORITHM,
      CONST,
      EXPORT,
      EXT,
      FUNCTION,
      IMPORT,
      IMPORTED,
      INV,
      LAYER,
      LIST,
      MACRO,
      MODULE,
      NOTE,
      PART,
      POSTLUDE,
      PREDICATE,
      PRELUDE,
      PROCEDURE,
      PROGRAM,
      ROOT,
      SECTION,
      TEST,
      VAR,
      // Generic types
      ANY,
      CONTAINER,
      DATA,
      FACE,
      OBJECT,
      // Non-focusable types.
      AFFIX,
      CALL,
      LOCAL,
   }
   // cspell:enable

   /// <summary>
   /// How to parse. Used by some parsing methods.
   /// </summary>
   public enum ParseMode { 
      Full,    // Parse and add result to syntax-tree.
      Check,   // Just verify syntax.
      Result,  // Verify syntax and if correct, return cosntruct but don't add to parse tree.
   }

   /// <summary>
   /// Used to categorize annotations.
   /// </summary>
   public enum Severity {
      Error,
      Warning,
      Info,
      Note,
      NONE,
   }

   public enum SettingType {
      Boolean,
      Integer,
      String,
   }

   /// <summary>
   /// The type of change that occurred to an object. Recorded in the UndoRecord.
   /// </summary>
   public enum ChangeType {
      Added,
      Removed,
      Replaced,
      Renamed,
   }

   public enum FocusMoveDirection { Forward, Backward, First, Last };

   /// <summary>
   /// Formal parameter directions of procedures.
   /// </summary>
   public enum AffixDir { 
      input,
      output, 
      transput, 
      NONE
   }
   /// <summary>
   /// Formal parameter types.
   ///   std : standard argument
   ///   str : string argument
   /// </summary>
   public enum AffixType { 
      std, 
      str 
   }

   /// <summary>
   /// Call types that can be used in the last calls of an alternative.
   /// </summary>
   public enum LastCallType {
      Standard, 
      Succeed, 
      Fail, 
      Abort, 
      Repeat, 
      Group,
      None,                   // Use in the alternative generated for container Ludes.
   }

   public enum ProcedureBodyType {
      VerySimple,
      Simple,
      General
   }

   public enum InsertLocation {       
      Before,
      After,
      First,
      Last,
      Sorted,
   }

   [Flags]
   public enum InterfaceType {
      None   = 0, 
      Abstr  = 1,
      Ext    = 2,
      Inv    = 4,
      Import = 8,
      Export = 16,
   }

   /// <summary>
   /// Used for PrettyPrint decoration
   /// </summary>
   public enum SyntacticElement {
      Id,                        // Default for ids, unless otherwise specified, e.g., AlgorithmName, ...Affix, Local.
      ReservedWord,              // Default for reserved words, unless otherwise specified, e.g., Unit
      Unit,                      // Units (PROGRAM, MODULE, LAYER, SECTION).
      Builtin,                   // The built in indicator
      AlgorithmName,             // When used it will be overridden by the AlgorithmNameDecorators table
      InputAffix,                // Applied to affixes ids in both Algorithm definitions and invocations.
      OutputAffix,               // Ditto
      TransputAffix,             // Ditto
      StringAffix,               // Ditto
      Local,                     // Local variables
      Const,                     // Constants
      Var,                       // Variables
      List,                      // Lists
      Number,                    // Numbers
      String,                    // Strings
      Comment,                   // Comments
      Other,                     // Default for other elements.
      Label,                     // Labels
      NoteError,                 // For showing errors
      NoteWarning,               // For showing warnings
      NoteInfo,                  // For showing information
      UNDEFINED,                 // For showing undefined elements
      ConditionalCompilationOn,  // For showing conditional compilation
      ConditionalCompilationOff, // For showing conditional compilation
   }
   /// <summary>
   /// Algorithm PhaseName types.
   /// Use the CanFail flag to select italic.
   /// Use the Macro flag to select underline.
   /// If the algorithm is
   ///   - defined in the current container select dark green
   ///   - invoked from extension select lighter green
   ///   - invoked from abstraction select even lighter green
   ///   - imported from another module select orange
   ///   
   ///  exported abstr⇑  ext⇒ abstr-ext⇗ exported-abstr➤⇑ exported-ext➤⇒ exported-abstr-ext➤⇗
   ///  imported⇐  ⇒invoked-ext ⇑invoked-abstr imported-ext⇔ imported-abstr
   ///  /// </summary>
   [Flags]
   public enum AlgorithmNameType {
      None     = 0,          // 
      CanFail  = 1,          // Test or Predicate: Italic (otherwise Normal)
      Macro    = 2,          // Macro: (underline)
      Abstr    = 4,          // Abstracted from previous layer.
      Ext      = 8,          // Extended from another container in current layer.
      Inv      = 16,         // Invoked from another container in the current or previous layer. This is not used. Determine whether it was 
      Imported = 32,         // Imported from another module
      Exported = 64,         // Exported from current module. TODO: Not yet implemented
      HasEffect = 128,       // Action or predicate

      AbstrExt = Abstr | Ext,
      AbstrImported = Abstr | Imported,
      ExtImported = Ext | Imported,
      AbstrExtImported = Abstr | Ext | Imported,
      AbstrExported = Abstr | Exported,
      ExtExported = Ext | Exported,
      AbstrExtExported = Abstr | Ext | Exported,
   }
   [Flags]
   public enum DecorationStyle {
      Normal      = 0,
      Bold        = 1,
      Italic      = 2,
      Underline   = 4,
      Intense     = 8,
      Dimmed      = 16,
   }

   /// <summary>
   /// Character codes in the Wingdings 3 font.
   /// ImportExport is placed to the left of the PhaseName when imported and to the right when exported. Mutually exclusive.
   /// Abstr/Ext/AbstrExt is placed to the left of the PhaseName when invoked, and to the right when defined.
   /// </summary>
   public enum  AnnotationSymbol {
      None = 0,
      ImportExport = 0x86, // Rightward pointing triangular arrow
      Abstr = 0xdb,        // Upward pointing arrow
      Ext = 0xda,          // Rightward pointing arrow
      AbstrExt = 0xde,     // NE pointing arrow
      Inv = 0xd2,          // Rightward pointing arrow, but thinner than Ext. Means we don't know yet where it comes from.
   }

   public class AnnotationSymbols(AS Prefix1 = AS.None,AS Prefix2 = AS.None,AS Suffix1 = AS.None,AS Suffix2 = AS.None) {
      public AS Prefix1 = Prefix1;
      public AS Prefix2 = Prefix2;
      public AS Suffix1 = Suffix1;
      public AS Suffix2 = Suffix2;

      /// <summary>
      /// The string to use as a prefix for a PhaseName.
      /// </summary>
      public string Prefix => (Prefix1 != AS.None ? $"{(char)Prefix1}" : "")+(Prefix2 != AS.None ? $"{(char)Prefix2}" : "");
      /// <summary>
      /// The string to use as a suffix for a PhaseName.
      /// </summary>
      public string Suffix => (Suffix1 != AS.None ? $"{(char)Suffix1}" : "")+(Suffix2 != AS.None ? $"{(char)Suffix2}" : "");

      /// <summary>
      /// The character class for the AnnotationSymbol enumeration.
      /// </summary>
      public static string CharacterClass => characterClass;

      private static readonly string characterClass;
      static AnnotationSymbols() { 
         // Get the values of the AnnotationSymbol enumeration
         AnnotationSymbol[] values = (AnnotationSymbol[])Enum.GetValues(typeof(AnnotationSymbol));

         // Convert each value to its corresponding character and build the character class string
         StringBuilder characterClass = new("[");
         foreach (AnnotationSymbol value in values) {
            if (value != AnnotationSymbol.None) {
               characterClass.Append((char)value);
            }
         }
         characterClass.Append(']');

         AnnotationSymbols.characterClass = characterClass.ToString();
      }
   }
}



