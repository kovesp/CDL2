// Ignore Spelling: CDL

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
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

      
      public static readonly ReservedWord[] UnitStarters = [ReservedWord.MODULE,ReservedWord.LAYER,ReservedWord.SECTION,ReservedWord.PROGRAM];
      public static readonly ReservedWord[] UnitEnders = [ReservedWord.ENDMOD,ReservedWord.ENDLAY,ReservedWord.ENDSEC,ReservedWord.ENDPROG];

      public static readonly Dictionary<string,TokenType> Glyph2TokenType;
      public static readonly Dictionary<TokenType,string> TokenType2Glyph;
      public static readonly Dictionary<string,string> Escape2Char;
      public static readonly Dictionary<string,string> Char2Escape =[];

      public static readonly Dictionary<string,Token> IDTokens = [];

      // Reserved words that can have comments attached.
      private static readonly Set<RW> CommentableReservedWords = [];

      private static readonly Regex GlyphRE;
      private static readonly Regex ReservedWordRE;
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
            { "=:", TokenType.MACROPROCBODY },     // Indicates a macro body that should NOT be in-lined (the default for = is to inline).
            { ":=", TokenType.INLINECODEBODY },    // Indicates a ContainingProc body that should be in-lined.
            { "+",  TokenType.PLUS },              // Used as affixes (argument) separator and as the succeed operator.
            { "-",  TokenType.MINUS },             // Used as declarations variable separator and as the fail operator.
            { "*",  TokenType.STAR },              // Repeat from group start operator and string parameter.
            { "?",  TokenType.ABORT },             // Terminate the program.
            { ">",  TokenType.AFFIXDIR },          // Used to indicated the argument direction, as >in, out>, or >in-out>.
            { ",",  TokenType.SEP },               // Used in interface lists, CONST and VAR declarations, and as call separators.
            { ";",  TokenType.ALTSEP },            // Separates alternatives.
            { "(",  TokenType.GRPOPEN },           // Starts a group and a LIST bound.
            { ")",  TokenType.GRPCLOSE },          // Ends a group and a LIST bound.
            { ":",  TokenType.COLON },             // Algorithm that is a ContainingProc. But also used in LIST bounds and to place labels, e.g., ACTION proc: init,(main: is not done, (try first, first; try next, next, *main); quit.
            { "=",  TokenType.EQUALS },            // Algorithm that is a macro and normally in-lined. Also used to define constants.
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


         // Must match at the beginning of the input
         GlyphRE = new Regex(@$"^({string.Join("|",Glyph2TokenType.Keys.Select(Regex.Escape))})",RegexOptions.Compiled);
         ReservedWordRE = new Regex(@$"^(?:{string.Join("|",Enum.GetNames(typeof(ReservedWord)))})",RegexOptions.Compiled);
         // Allows annotation symbols to precede and follow the ID; these are removed.
         IdRE = new Regex(@$"^{AnnotationSymbols.CharacterClass}*([a-z][a-z0-9 ]*){AnnotationSymbols.CharacterClass}*",RegexOptions.Compiled);
         StringRE = new Regex(@"^"".*?(?:$"".*?)*""",RegexOptions.Compiled);
         CommentRE = new Regex(@"^(?m:#(.*?)?(?:#|$))",RegexOptions.Compiled);
         IntRE = new Regex(@"^(?:0x[\dA-Fa-f]+|[+-]?\d+)",RegexOptions.Compiled);
         FloatRE = new Regex(@"^[+-]?\d+(?:\.\d+(?:[eE][+-]?\d+)?)?",RegexOptions.Compiled);
         // Must match all occurrences anywhere in a string
         StringEscapeRE = new Regex(@$"\$([{string.Join("",Escape2Char.Keys)}])",RegexOptions.Compiled);

         ErrorToken = new Token();
         AnonIDToken = new Token(TokenClass.ID,"Anon","",0);
         AnonID = ID.From(AnonIDToken);
         ACTIONToken = new Token(TokenClass.ResWord,"ACTION","",0);

         CommentableReservedWords = [  RW.PROGRAM, RW.MODULE, RW.LAYER, RW.SECTION,
                                       RW.ACTION, RW.FUNCTION, RW.PREDICATE, RW.TEST,
                                       RW.CONST, RW.LIST, RW.VAR
                                    ];
      }

      public static string ToGlyph(TokenType tt) => Token.TokenType2Glyph.TryGetValue(tt,out string? glyph) ? glyph ?? "" : "";

      readonly public TokenType type;
      // Depending on the type, one of the following may be populated:
      //    RESWORD: reservedWordValue is the enum of the reserved word
      //    ID:      StringValue is the identifier id
      //    INT:     intValue is the long
      //    STRING:  StringValue is the string
      //    FLOAT:   floatValue is the double
      //    COMMENT: StringValue is the comment

      public string TokenString { get; private set; } = "";
      readonly public int lineNumber = 0;
      readonly public int columnNumber = 0;
      readonly public string fileName = "";

      readonly public ReservedWord? reservedWordValue;
      public string? StringValue { get; private set; }
      readonly public long? intValue;
      readonly public double? floatValue;
      readonly public string? Comments;  // Only for certain reserved words

      private enum TokenClass { String, ID, ResWord, Glyph, Comment, Int, Float, Error };

      private Token() : this(TokenClass.Error,"ERROR","",0) { }
      public Token(string text) : this(TokenClass.ID,text,"",0) { }
      public Token(RW rw) : this(TokenClass.ResWord,rw.ToString(),"",0) { }

      private Token(TokenClass cls,string text,string fileName,int lineNumber) {
         TokenString = text;
         this.fileName = fileName;
         this.lineNumber = lineNumber;
         switch (cls) {
            case TokenClass.Error:
               type = TokenType.ERROR;
               break;
            case TokenClass.Comment:
               type = TokenType.COMMENT;
               StringValue = text.Trim('#','\n');
               break;
            case TokenClass.String:
               type = TokenType.STRING;
               StringValue = StringEscapeRE.Replace(text.Trim('"'),match => Escape2Char[match.Groups[1].Value]);
               break;
            case TokenClass.ID:
               type = TokenType.ID;
               TokenString = Regex.Replace(text,@"\s+"," ").Trim();  // Reduce all white space to a single space
               StringValue = Regex.Replace(text,@"\s+","");
               break;
            case TokenClass.ResWord:
               type = TokenType.RESWORD;
               reservedWordValue = Enum.Parse<ReservedWord>(text);
               // Attach comments encountered before reserved words that can have comments.
               if (CommentableReservedWords.Contains(reservedWordValue.Value) && collectedComments.Count > 0) {
                  int width = collectedComments.Select(c => c.Trim().Length).Max();
                  StringBuilder sb = new();
                  foreach (string comment in collectedComments) sb.AppendLine(string.Format("# {0} #",comment.Trim().PadRight(width)));
                  Comments = sb.ToString();
               }
               ClearComments();
               break;
            case TokenClass.Glyph:
               type = Glyph2TokenType[text];
               // Discard comments that cannot be attached to reserved words specified as commentable.
               if (type == TokenType.END) ClearComments();
               break;
            case TokenClass.Int:
               type = TokenType.INT;
               TokenString = text;
               try {
                  intValue = long.Parse(text,text.StartsWith("0x") ? NumberStyles.HexNumber : NumberStyles.Integer,CultureInfo.InvariantCulture);
               } catch {
                  type = TokenType.ERROR;
               }
               break;
            case TokenClass.Float:
               type = TokenType.FLOAT;
               floatValue = double.Parse(text);
               break;
         }
      }

      /// <summary>
      /// Match an RE that describes a class of tokens to the beginning of input. If it matches, construct the token and return true.
      /// The last group in the match captures the token. If it does not have groups, the entire match is the token.
      /// </summary>
      /// <param id="regex">The RE that describes a token of the given class.</param>
      /// <param id="tokenClass">The class of the token.</param>
      /// <param id="input">The input string. If a token is found the characters consumed are removed.</param>
      /// <param id="token">The token that was found. If none, it will be ErrorToken.</param>
      /// <returns>True if a token was found.</returns>
      /// <param id="fileName"></param><param id="lineNumber"></param>
      private static bool HandleMatch(Regex regex,TokenClass tokenClass,ref string input,out Token token,string fileName,int lineNumber) {
         Match match = regex.Match(input);
         if (match.Success) {
            input = input[match.Length..].TrimStart();
            token = new Token(tokenClass,match.Groups[^1].Value,fileName,lineNumber);
            return true;
         } else {
            token = ErrorToken;
            return false;
         }
      }
      private static readonly List<string> collectedComments = [];
      public static void ClearComments() => collectedComments.Clear();
      /// <summary>
      /// Scan the next token and return it token. Remove the consumed characters from input.
      /// Comments are also returned as tokens.
      /// Return true, if a valid token was found.
      /// This is the only way to construct tokens, as all constructors are private.
      /// </summary>
      /// <param id="input">The input string. Consumed characters are removed.</param>
      /// <param id="token">The token that was found.</param>
      /// <param id="fileName">The id of the file being tokenized.</param>
      /// <param id="lineNumber">The line number of the token.</param>
      /// <returns>true if the staring started with a valid token.</returns>
      public static bool TryCreateToken(ref string input,out Token token,string fileName,ref int lineNumber) {
         while (true) {
            input = input.TrimStart();
            token = ErrorToken; 
            if (string.IsNullOrEmpty(input)) return false;

            // Collect comments into a list
            Match match = CommentRE.Match(input);
            if (match.Success) {
               input = input[match.Length..].TrimStart();
               collectedComments.Add(match.Groups[^1].Value);
               continue;
            }

            if (HandleMatch(StringRE,TokenClass.String,ref input,out token,fileName,lineNumber)) return true;
            if (HandleMatch(ReservedWordRE,TokenClass.ResWord,ref input,out token,fileName,lineNumber)) return true;
            if (HandleMatch(IdRE,TokenClass.ID,ref input,out token,fileName,lineNumber)) {
               Debug.Assert(token.StringValue != null,"ID token has no string value");
               // Guarantee that all IDs with the same string value (i.e., ignoring spaces) are the same.
               if (IDTokens.TryGetValue(token.StringValue,out Token? idToken)) {
                  token = idToken;
               } else {
                  IDTokens[token.StringValue] = token;
               }
               return true;
            }
            if (HandleMatch(IntRE,TokenClass.Int,ref input,out token,fileName,lineNumber)) return true;
            if (HandleMatch(FloatRE,TokenClass.Float,ref input,out token,fileName,lineNumber)) return true;
            if (HandleMatch(GlyphRE,TokenClass.Glyph,ref input,out token,fileName,lineNumber)) return true; // Must be placed after Int & Float as they may start with + or -
            return false;
         }
      }

      /// <summary>
      /// Renames a token and by implications the ID it represents.
      /// This allows changing the Name of an ID without changing the ID itself, in particular where spaces are in the id.
      /// </summary>
      /// <param Name="newName"></param>
      public void Rename(string newName) {
         Debug.Assert(type == TT.ID,"Rename called on non-ID token");
         TokenString = newName;
         StringValue = newName.Replace(" ","");
      }

      
      public static bool TryCreateToken(ref string input,out Token token) {
         int lineNumber = 0; // TODO: Remove this when line number passed in TryCreateToken
         return TryCreateToken(ref input,out token,"",ref lineNumber);
      }

      public override string ToString() {
         string EscapedString() => StringValue != null ? StringValue.Replace("\n","\\n").Replace("\r","\\r").Replace("\t","\\t").Replace("\"","\\\"") : string.Empty;
         return type switch {
            TT.RESWORD => reservedWordValue?.ToString() ?? "NONE",
            TT.COMMENT => $"COMMENT<{EscapedString()}>",
            TT.STRING  => $"STRING<{EscapedString()}>",
            TT.INT     => $"INT<{intValue?.ToString() ?? "0"}>",
            TT.FLOAT   => $"FLOAT<{floatValue?.ToString() ?? "0.0"}>",
            TT.ID      => $"ID<{StringValue ?? string.Empty}>",
            TT.ERROR   => "ERROR",
            _          => TokenType2Glyph.TryGetValue(type,out string? value) ? value : type.ToString(),
         };
      }

      public override bool Equals(object? obj) => obj is Token token && type == token.type && type switch {
         TT.COMMENT or TT.STRING or TT.ID => StringValue == token.StringValue,
         TT.RESWORD                       => reservedWordValue == token.reservedWordValue,
         TT.INT                           => intValue == token.intValue,
         TT.FLOAT                         => floatValue == token.floatValue,
         _                                => false
      }; 
      public override int GetHashCode() => HashCode.Combine(type,reservedWordValue,StringValue,intValue,floatValue);
      /// <summary>
      /// Return the token as a id. If the token is an ID, the id is returned.
      /// - Runs of spaces and non-letters are replaced with the replacement string.
      /// - If the token is not an ID, the token type is prepended to the id.
      /// - The id is lowercased.
      /// </summary>
      /// <param id="spaceReplacement"></param>
      /// <returns>The normalized id.</returns>
      /// <example>Token.TryCreateToken("3.14",out Token token).AsIdentifier() -> "float_3_14"</example>
      internal string AsIdentifier(string replacement = "_",bool camelCase = true) 
         => $"{(type != TT.ID ? type.ToString().ToLower() + replacement : "")}{Regex.Replace(TokenString,@"(?:\s+|[^\p{L}\d])",replacement).AsIdentifier(camelCase:camelCase)}";
      internal static Token From(RW rw) => new(TokenClass.ID,rw.ToString(),"",0);
      public static bool operator ==(Token? left,Token? right) => EqualityComparer<Token>.Default.Equals(left,right);
      public static bool operator !=(Token? left,Token? right) => !(left == right);
   }
}