using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CDL2v1 {
   /// <summary>
   ///   CDL2 language tokens. Comments are preserved as tokens.
   ///   
   /// Tokens must be constructed using <see cref="TryCreateToken(ref string, out Token)"/>.
   /// </summary>
   internal class Token {
      public enum TokenType { ERROR, RESWORD, INT, FLOAT, STRING, ID, PLUS, MINUS, STAR, ABORT, LABEL, ARGDIR, COLON, INLINECODEBODY, EQUALS, INLINEMACROBODY, END, SEP, ALTSEP, GRPOPEN, GRPCLOSE, COMMENT }
      public enum ReservedWord { PROGRAM, ENDPROG, PART, MODULE, ENDMOD, LAYER, ENDLAY, SECTION, ENDSEC, ABSTR, EXT, INV, EXPORT, IMPORT, ROOT, PRELUDE, POSTLUDE, CONST, VAR, LIST, ACTION, FUNCTION, TEST, PREDICATE }

      private static readonly Dictionary<string,TokenType> Glyph2TokenType;
      private static readonly Dictionary<TokenType,string> TokenType2Glyph;
      private static readonly Dictionary<string,string> Escape2Char;

      private static readonly Regex GlyphRE;
      private static readonly Regex ReswordRE;
      private static readonly Regex StringEscapeRE;
      private static readonly Regex IdRE;
      private static readonly Regex StringRE;
      private static readonly Regex CommentRE;
      private static readonly Regex IntRE;
      private static readonly Regex FloatRE;

      public static readonly Token ErrorToken;
      public static readonly Token AnonIdTken;


      static Token() {
         // Place multi-character glyphs first to ensure they match before any single character contained in them.
         Glyph2TokenType = new Dictionary<string,TokenType> {
            { "=:", TokenType.INLINEMACROBODY },   // Indicates a macro body that should be inlined.
            { ":=", TokenType.INLINECODEBODY },    // Indicates a code body that should be inlined.
            { "+",  TokenType.PLUS },              // Used as affix (argument) seperator and as the success operator.
            { "-",  TokenType.MINUS },             // Used as local variable separators and as the fail operator.
            { "*",  TokenType.STAR },              // Repeat from group start operator and string parameter.
            { "?",  TokenType.ABORT },             // Terminate the program.
            { ">",  TokenType.ARGDIR },            // Used to indicated the argument direction, as >in, out>, or >in-out>.
            { ",",  TokenType.SEP },               // Used in interface lists (not yet implemented), CONST and VAR declarations, and as call separators.
            { ";",  TokenType.ALTSEP },            // Separates alterntives.
            { "(",  TokenType.GRPOPEN },           // Starts a group and a LIST bound.
            { ")",  TokenType.GRPCLOSE },          // Ends a group and a LIST bound.
            { ":",  TokenType.COLON },             // Code that is a procedure. But also used in LIST bounds and to place labels, e.g., ACTION proc: init,(main: is not done, (try first, first; try next, next, *main); quit.
            { "=",  TokenType.EQUALS },            // Macro that is aprocedure. Also used to define constants.
            { ".",  TokenType.END },               // Ends all sentences.
         };
         TokenType2Glyph = Glyph2TokenType.ToDictionary(kvp => kvp.Value,kvp => kvp.Key);

         Escape2Char = new Dictionary<string,string> {
            { "L", "\n" },
            { "l", "\n" },
            { "T", "\t" },
            { "t", "\t" },
            { "\"", "\"" },
            { "$", "$" },
         };
         // Must match at the begining of the input
         GlyphRE = new Regex(@$"^({string.Join("|",Glyph2TokenType.Keys.Select(Regex.Escape))})");
         ReswordRE = new Regex(@$"^(?:{string.Join("|",Enum.GetNames(typeof(ReservedWord)))})");
         IdRE = new Regex(@"^[a-z][a-z0-9 ]*");
         StringRE = new Regex(@"^"".*?(?:$"".*?)*""");
         CommentRE = new Regex(@"^(?m:#(.*)?(?:#|$))");
         IntRE = new Regex(@"^(?:0x[\dA-Fa-f]+|[+-]?\d+)");
         FloatRE = new Regex(@"^[+-]?\d+(?:\.\d+(?:[eE][+-]?\d+)?)?");
         // Must match all occurences anywhere in a string
         StringEscapeRE = new Regex(@$"\$([{string.Join("",Escape2Char.Keys/*.Select(Regex.Escape)*/)}])");

         ErrorToken = new Token();
         AnonIdTken = new Token(TokenClass.ID,"Anon");
      }

      readonly public TokenType type;
      // Depending on the type, one of the following may be populated:
      //    RESWORD: rval is the enum of the reserved word
      //    ID:      sval is the identifier name
      //    INT:     ival is the long
      //    STRING:  sval is the string
      //    FLOAT:   fval is the double
      //    COMMENT: sval is the comment

      readonly public string tokenString = "";
      readonly public ReservedWord? rval;
      readonly public string? sval;
      readonly public long? ival;
      readonly public double? fval;

      private enum TokenClass { String, ID, ResWord, Glyph, Comment, Int, Float };
      private Token() => type = TokenType.ERROR;
      
      private Token(TokenClass cls,string text) {
         tokenString = text;
         switch (cls) {
            case TokenClass.Comment:
               type = TokenType.COMMENT;
               sval = text.Trim('#','\n');
               break;
            case TokenClass.String:
               type = TokenType.STRING;
               sval = StringEscapeRE.Replace(text.Trim('"'),match => Escape2Char[match.Groups[1].Value]);
               break;
            case TokenClass.ID:
               type = TokenType.ID;
               tokenString = Regex.Replace(text,@"\s+"," ");  // Reduce all white space to a single space
               sval = Regex.Replace(text,@"\s+","");
               break;
            case TokenClass.ResWord:
               type = TokenType.RESWORD;
               rval = Enum.Parse<ReservedWord>(text);
               break;
            case TokenClass.Glyph:
               type = Glyph2TokenType[text];
               break;
            case TokenClass.Int:
               type = TokenType.INT;
               tokenString = text;
               try {
                  ival = long.Parse(text,text.StartsWith("0x") ? NumberStyles.HexNumber : NumberStyles.Integer,CultureInfo.InvariantCulture);
               } catch {
                  type = TokenType.ERROR;
               }
               break;
            case TokenClass.Float:
               type = TokenType.FLOAT;
               fval = double.Parse(text);
               break;
         }
      }

      /// <summary>
      /// Match an RE that describes a class of tokens to the beginning of input. If it matches, construct the token and return true. 
      /// </summary>
      /// <param name="regex">The RE that describes a token of the given class.</param>
      /// <param name="tokenClass">The class of the token.</param>
      /// <param name="input">The input string. If a token is found the characters consumed are removed.</param>
      /// <param name="token">The token that was found. If none, it will be ErrorToken.</param>
      /// <returns>True if a token was found.</returns>
      private static bool HandleMatch(Regex regex,TokenClass tokenClass,ref string input,out Token token) {
         Match match = regex.Match(input);
         if (match.Success) {
            input = input[match.Length..].TrimStart();
            token = new Token(tokenClass,match.Value);
            return true;
         } else {
            token = ErrorToken;
            return false;
         }
      }

      /// <summary>
      /// Scan the next token and return it token. Remove the consumed chartecters from input.
      /// Comments are also returned as tokens.
      /// Return true, if a valid token was found.
      /// This is the only way to construct tokens, as all constructors are private.
      /// </summary>
      /// <param name="input">The input string. Consumed characters are removed.</param>
      /// <param name="token">The token that was found.</param>
      /// <returns>true if the staring started with a valid token.</returns>
      public static bool TryCreateToken(ref string input,out Token token) {
         input = input.TrimStart();
         token = ErrorToken;

         if (string.IsNullOrEmpty(input)) return false;
         if (HandleMatch(CommentRE,TokenClass.Comment,ref input,out token)) return true;
         if (HandleMatch(StringRE,TokenClass.String,ref input,out token)) return true;
         if (HandleMatch(ReswordRE,TokenClass.ResWord,ref input,out token)) return true;
         if (HandleMatch(IdRE,TokenClass.ID,ref input,out token)) return true;
         if (HandleMatch(IntRE,TokenClass.Int,ref input,out token)) return true;
         if (HandleMatch(FloatRE,TokenClass.Float,ref input,out token)) return true;
         if (HandleMatch(GlyphRE,TokenClass.Glyph,ref input,out token)) return true; // Must be place after Int & Float as they may start with + or -

         return false;
      }

      public override string ToString() {
         string EscapedString() => sval != null ? sval.Replace("\n","\\n").Replace("\r","\\r").Replace("\t","\\t").Replace("\"","\\\"") : string.Empty;
         return type switch {
            TokenType.RESWORD => rval?.ToString() ?? "NONE",
            TokenType.COMMENT => $"COMMENT<{EscapedString()}>",
            TokenType.STRING  => $"STRING<{EscapedString()}>",
            TokenType.INT     => $"INT<{ival?.ToString() ?? "0"}>",
            TokenType.FLOAT   => $"FLOAT<{fval?.ToString() ?? "0.0"}>",
            TokenType.ID      => $"ID<{sval ?? string.Empty}>",
            TokenType.ERROR   => "ERROR",
            _                 => TokenType2Glyph.ContainsKey(type) ? TokenType2Glyph[type] : type.ToString(),
         };
      }
   }
}