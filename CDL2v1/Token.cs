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
      // The aliases are meant to be used in the parser to make the code more readable.

      
      public static readonly ReservedWord[] UnitStarters = { ReservedWord.MODULE,ReservedWord.LAYER,ReservedWord.SECTION,ReservedWord.PROGRAM };
      public static readonly ReservedWord[] UnitEnders = { ReservedWord.ENDMOD,ReservedWord.ENDLAY,ReservedWord.ENDSEC,ReservedWord.ENDPROG };

      public static readonly Dictionary<string,TokenType> Glyph2TokenType;
      public static readonly Dictionary<TokenType,string> TokenType2Glyph;
      public static readonly Dictionary<string,string> Escape2Char;
      public static readonly Dictionary<string,string> Char2Escape =[];

      private static readonly Regex GlyphRE;
      private static readonly Regex ReswordRE;
      private static readonly Regex StringEscapeRE;
      private static readonly Regex IdRE;
      private static readonly Regex StringRE;
      private static readonly Regex CommentRE;
      private static readonly Regex IntRE;
      private static readonly Regex FloatRE;

      public static readonly Token ErrorToken;
      public static readonly ID AnonID;
      public static readonly Token AnonIDToken;
      public static readonly Token ACTIONToken;

      static Token() {
         // Place multi-character glyphs first to ensure they match before any single character contained in them.
         Glyph2TokenType = new Dictionary<string,TokenType> {
            { "=:", TokenType.MACROPROCBODY },     // Indicates a macro body that should NOT be inlined (the default for = is to inline).
            { ":=", TokenType.INLINECODEBODY },    // Indicates a code body that should be inlined.
            { "+",  TokenType.PLUS },              // Used as affix (argument) seperator and as the succeed operator.
            { "-",  TokenType.MINUS },             // Used as local variable separator and as the fail operator.
            { "*",  TokenType.STAR },              // Repeat from group start operator and string parameter.
            { "?",  TokenType.ABORT },             // Terminate the program.
            { ">",  TokenType.PARAMDIR },            // Used to indicated the argument direction, as >in, out>, or >in-out>.
            { ",",  TokenType.SEP },               // Used in interface lists, CONST and VAR declarations, and as call separators.
            { ";",  TokenType.ALTSEP },            // Separates alterntives.
            { "(",  TokenType.GRPOPEN },           // Starts a group and a LIST bound.
            { ")",  TokenType.GRPCLOSE },          // Ends a group and a LIST bound.
            { ":",  TokenType.COLON },             // Code that is a procedure. But also used in LIST bounds and to place labels, e.g., ACTION proc: init,(main: is not done, (try first, first; try next, next, *main); quit.
            { "=",  TokenType.EQUALS },            // Macro that is aprocedure. Also used to define constants.
            { ".",  TokenType.END },               // Ends all sentences.
            { "#",  TokenType.COMMENT },           // Starts and ends a comment.
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
         Escape2Char.Values.Distinct().ToList().ForEach(v => Char2Escape[v] = Escape2Char.First(kvp => kvp.Value == v).Key.ToUpper());


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
         AnonIDToken = new Token(TokenClass.ID,"Anon","",0);
         AnonID = new ID(AnonIDToken);
         ACTIONToken = new Token(TokenClass.ResWord,"ACTION","",0);
      }

      public static string ToGlyph(TokenType tt) => Token.TokenType2Glyph.TryGetValue(tt,out string? glyph) ? glyph ?? "" : "";

      readonly public TokenType type;
      // Depending on the type, one of the following may be populated:
      //    RESWORD: rval is the enum of the reserved word
      //    ID:      sval is the identifier name
      //    INT:     ival is the long
      //    STRING:  sval is the string
      //    FLOAT:   fval is the double
      //    COMMENT: sval is the comment

      readonly public string tokenString = "";
      readonly public int lineNumber = 0;
      readonly public int columnNumber = 0;
      readonly public string fileName = "";

      readonly public ReservedWord? rval;
      readonly public string? sval;
      readonly public long? ival;
      readonly public double? fval;

      private enum TokenClass { String, ID, ResWord, Glyph, Comment, Int, Float, Error };

      private Token() : this(TokenClass.Error,"ERROR","",0) { }
      public Token(string text) : this(TokenClass.ID,text,"",0) { }
      public Token(RW rw) : this(TokenClass.ResWord,rw.ToString(),"",0) { }

      private Token(TokenClass cls,string text,string fileName,int lineNumber) {
         tokenString = text;
         this.fileName = fileName;
         this.lineNumber = lineNumber;
         switch (cls) {
            case TokenClass.Error:
               type = TokenType.ERROR;
               break;
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
               tokenString = Regex.Replace(text,@"\s+"," ").Trim();  // Reduce all white space to a single space
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
      /// <param name="fileName"></param><param name="lineNumber"></param>
      private static bool HandleMatch(Regex regex,TokenClass tokenClass,ref string input,out Token token,string fileName,int lineNumber) {
         Match match = regex.Match(input);
         if (match.Success) {
            input = input[match.Length..].TrimStart();
            token = new Token(tokenClass,match.Value,fileName,lineNumber);
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
      /// <param name="fileName">The name of the file being tokenized.</param>
      /// <param name="lineNumber">The line number of the token.</param>
      /// <returns>true if the staring started with a valid token.</returns>
      public static bool TryCreateToken(ref string input,out Token token,string fileName,ref int lineNumber) {
         input = input.TrimStart();
         token = ErrorToken;

         if (string.IsNullOrEmpty(input)) return false;
         if (HandleMatch(CommentRE,TokenClass.Comment,ref input,out token,fileName,lineNumber)) return true;
         if (HandleMatch(StringRE,TokenClass.String,ref input,out token,fileName,lineNumber)) return true;
         if (HandleMatch(ReswordRE,TokenClass.ResWord,ref input,out token,fileName,lineNumber)) return true;
         if (HandleMatch(IdRE,TokenClass.ID,ref input,out token,fileName,lineNumber)) return true;
         if (HandleMatch(IntRE,TokenClass.Int,ref input,out token,fileName,lineNumber)) return true;
         if (HandleMatch(FloatRE,TokenClass.Float,ref input,out token,fileName,lineNumber)) return true;
         if (HandleMatch(GlyphRE,TokenClass.Glyph,ref input,out token,fileName,lineNumber)) return true; // Must be place after Int & Float as they may start with + or -

         return false;
      }
      // TODO: Remove this when linenumber passed in TryCreateToken
      public static bool TryCreateToken(ref string input,out Token token) {
         int lineNumber = 0;
         return TryCreateToken(ref input,out token,"",ref lineNumber);
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

      public override bool Equals(object? obj) => obj is Token token && type == token.type && type switch {
         TokenType.RESWORD => rval == token.rval,
         TokenType.COMMENT => sval == token.sval,
         TokenType.STRING => sval == token.sval,
         TokenType.INT => ival == token.ival,
         TokenType.FLOAT => fval == token.fval,
         TokenType.ID => sval == token.sval,
         _ => true
      }; 
      public override int GetHashCode() => HashCode.Combine(type,rval,sval,ival,fval);
      /// <summary>
      /// Return the token as a name. If the token is an ID, the name is returned.
      /// - Runs of spaces and non-letters are replaced with the replacement string.
      /// - If the token is not an ID, the token type is prepended to the name.
      /// - The name is lowercased.
      /// </summary>
      /// <param name="spaceReplacement"></param>
      /// <returns>The normalized name.</returns>
      /// <example>Token.TryCreateToken("3.14",out Token token).AsName() -> "float_3_14"</example>
      internal string AsName(string replacement = "_") 
         => $"{(type != TT.ID ? type.ToString().ToLower() + replacement : "")}{Regex.Replace(tokenString,@"(?:\s+|[^\p{L}\d])",replacement).ToLower()}";

      public static bool operator ==(Token? left,Token? right) => EqualityComparer<Token>.Default.Equals(left,right);
      public static bool operator !=(Token? left,Token? right) => !(left == right);
   }
}