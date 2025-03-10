// Ignore Spelling: CDL

global using TT  = CDL2v1.TokenType;
global using RW  = CDL2v1.ReservedWord;
global using PD  = CDL2v1.AffixDir;
global using PT  = CDL2v1.AffixType;
global using LCT = CDL2v1.LastCallType;
global using SE  = CDL2v1.SyntacticElement;
global using AIT = CDL2v1.AlgorithmNameType;
global using DS  = CDL2v1.DecorationStyle;
global using AS = CDL2v1.AnnotationSymbol;
global using SA = CDL2v1.AnnotationSymbols;
using System.Text;

namespace CDL2v1 {
   /// Central place for enumerations that are used across the compiler as well as their abbreviations.
   /// This would be called the representation of the CDL2 language.

   /// <summary>
   /// Token types for the CDL2 language.
   /// The aliases are meant to be used in the parser to make the code more readable.
   /// </summary>
   public enum TokenType {
      ERROR          = 0,
      RESWORD        = 1,
      INT            = 2,
      FLOAT          = 3,
      STRING         = 4,
      ID             = 5,
      PLUS           = 6,
      PARAMSEP       = 6,  // Alias for PLUS
      SUCCEED        = 6,  // Alias for PLUS
      MINUS          = 7,
      LOCALSEP       = 7,  // Alias for MINUS
      FAIL           = 7,  // Alias for MINUS
      STAR           = 8,
      REPEAT         = 8,  // Alias for STAR
      STRINGPARAMSEP = 8,  // Alias for STAR
      ABORT          = 9,
      AFFIXDIR       = 10,
      COLON          = 11,
      LABELSEP       = 11,  // Alias for COLON
      CODEBODY       = 11,  // Alias for COLON
      LISTBOUNDSEP   = 11,  // Alias for COLON
      INLINECODEBODY = 12,
      EQUALS         = 13,
      MACROBODY      = 13,  // Alias for EQUALS
      MACROPROCBODY  = 14,
      PERIOD         = 15,
      END            = 15,  // Alias for PERIOD
      COMMA          = 16,
      SEP            = 16, // Alias for COMMA
      CALLSEP        = 16, // Alias for COMMA
      LISTSEP        = 16, // Alias for COMMA
      ALTSEP         = 17,
      GRPOPEN        = 18,
      LISTBOUNDSTART = 18,  // Alias for GRPOPEN
      GRPCLOSE       = 19,
      LISTBOUNDEND   = 19,  // Alias for GRPCLOSE
      COMMENT        = 20,
      NOBODY         = 21, // IMPORTed PROCs have no body
   }

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
      SECTION, ENDSEC, 
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
      PREDICATE
   }

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
      None,                   // Use in the alternative generated for section Ludes.
   }

   /// <summary>
   /// Used for PrettyPrint decoration
   /// </summary>
   public enum SyntacticElement {
      Id,                        // Default for ids, unless otherwise specified, e.g., AlgorithmName, ...Affix, Local.
      ReservedWord,              // Default for reserved words, unless otherwise specified, e.g., Unit
      Unit,                      // Units (PROGRAM, MODULE, LAYER, SECTION).
      AlgorithmName,             // When used it will be overridden by the AlgorithmNameDecorators table
      InputAffix,                // Applied to affix ids in both Algorithm definitions and invocations.
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
      Label,
   }
   /// <summary>
   /// Algorithm Name types.
   /// Use the CanFail flag to select italic.
   /// Use the Macro flag to select underline.
   /// If the algorithm is
   ///   - defined in the current section select dark green
   ///   - invoked from extension select lighter green
   ///   - invoked from abstraction select even lighter green
   ///   - imported from another module select orange
   ///   
   ///  exported abstr⇑  ext⇒ abstr-ext⇗ exported-abstr➤⇑ exported-ext➤⇒ exported-abstr-ext➤⇗
   ///  imported⇐  ⇒invoked-ext ⇑invokded-abstr imported-ext⇔ emported-abstr
   ///  /// </summary>
   [Flags]
   public enum AlgorithmNameType {
      None     = 0,          // 
      CanFail  = 1,          // Test or Predicate: Italic (otherwise Normal)
      Macro    = 2,          // Macro: (underline)
      Abstr    = 4,          // Abstracted from previous layer.
      Ext      = 8,          // Extended from another section in current layer.
      Inv      = 16,         // Invoked from another section in the current or previous layer. This is not used. Determine whether it was 
      Imported = 32,         // Imported from another module
      Exported = 64,         // Exported from current module. TODO: Not yet implemented

      AbstrExt = Abstr | Ext,
      AbstrImported = Abstr | Imported,
      ExtImported = Ext | Imported,
      AbstrExtImported = Abstr | Ext | Imported,
      AbstrExported = Abstr | Exported,
      ExtExported = Ext | Exported,
      AbstrExtExported = Abstr | Ext | Exported,

      CanFailExt = CanFail | Ext,
      CanFailAbstr = CanFail | Abstr,
      CanFailImported = CanFail | Imported,
      CanFailAbstrExt = CanFail | Abstr | Ext,
      CanFailAbstrImported = CanFail | Abstr | Imported,
      CanFailExtImported = CanFail | Ext | Imported,
      CanFailAbstrExtImported = CanFail | Abstr | Ext | Imported,
      CanFailInv = CanFail | Inv,
      MacroAbstr = Macro | Abstr,
      MacroExt = Macro | Ext,
      MacroImported = Macro | Imported,
      MacroAbstrExt = Macro | Abstr | Ext,
      MacroAbstrImported = Macro | Abstr | Imported,
      MacroExtImported = Macro | Ext | Imported,
      MacroAbstrExtImported = Macro | Abstr | Ext | Imported,
      MacroInv = Macro | Inv,
   }
   [Flags]
   public enum DecorationStyle {
      Normal      = 0,
      Bold        = 1,
      Italic      = 2,
      Underline   = 4,
   }

   /// <summary>
   /// Character codes in the Wingdings 3 font.
   /// ImportExport is placed to the left of the Name when imported and to the right when exported. Mutually exclusive.
   /// Abstr/Ext/AbstrExt is placed to the left of the Name when invoked, and to the right when defined.
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
      /// The string to use as a prefix for a Name.
      /// </summary>
      public string Prefix => (Prefix1 != AS.None ? $"{(char)Prefix1}" : "")+(Prefix2 != AS.None ? $"{(char)Prefix2}" : "");
      /// <summary>
      /// The string to use as a suffix for a Name.
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


