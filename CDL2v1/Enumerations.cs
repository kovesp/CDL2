global using TT = CDL2v1.TokenType;
global using RW = CDL2v1.ReservedWord;
global using PD = CDL2v1.ParamDir;
global using PT = CDL2v1.ParamType;
global using LCT = CDL2v1.LastCallType;

namespace CDL2v1 {
   /// Cantral place for enumerations that are used accross the compiler as well as their abbreviations.
   /// Thsi would be called the representation of the CDL2 language.

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
      PARAMDIR       = 10,
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
   public enum ParamDir { 
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
   public enum ParamType { 
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
      None,                   // Use in the alternative generated for section ludes.
   }

}
