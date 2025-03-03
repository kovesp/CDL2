global using TT  = CDL2v1.TokenType;
global using RW  = CDL2v1.ReservedWord;
global using PD  = CDL2v1.AffixDir;
global using PT  = CDL2v1.AffixType;
global using LCT = CDL2v1.LastCallType;
global using SE  = CDL2v1.SyntacticElement;
global using AIT = CDL2v1.AlgorithmInvocationType;
global using DS  = CDL2v1.DecorationStyle;

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
      Unit,
      ReservedWord,
      AlgorithmInvocation,
      InputAffix,
      OutputAffix,
      TransputAffix,
      Local,
      Const,
      Var,
      List,
   }
   /// <summary>
   /// Algorithm Invocation types.
   /// Use the CanFail flag to select italic.
   /// Use the Macro flag to select underline.
   /// If the algorithm is
   ///   - defined in the current section select dark green
   ///   - invoked from extension select lighter green
   ///   - invoked from abstraction select even lighter green
   ///   - imported from another module select orange
   /// </summary>
   [Flags]
   public enum AlgorithmInvocationType {
      None = 0,         // 
      CanFail = 1,      // Test or Predicate: Italic (otherwise Normal)
      Macro = 2,        // Macro: (underline)
      Abstr = 4,        // Abstr from previous layer: 
      Ext = 8,          // Ext from another section in current layer
      Imported = 16,    // Imported from another module
   }
   [Flags]
   public enum DecorationStyle {
      Normal      = 0,
      Bold        = 1,
      Italic      = 2,
      Underline   = 4,
   }

}
