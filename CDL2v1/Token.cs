// <auto-gen>
//=======================================================================
// <copyright file="Token.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-02-10</creation-date>
// 
// <summary>
//   this is the actual lexical anylyzer. It creates the next token from the input stream and returns it.
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

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CDL2v1 {
   /// <summary>
   ///   CDL2 language tokens. Comments are preserved as tokens.
   ///   
   /// Tokens must be constructed using <see cref="TryCreateToken(ref string, out Token)"/>.
   /// </summary>
   public partial class Token {
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

      public static readonly Token ErrorToken;
      public static readonly Token ACTIONToken;

      // RE-s that have to be initialized in the static constructor becasue they make use of dynamic data
      private static readonly Regex GlyphRE_;
      private static readonly Regex ReservedWordRE_;
      private static readonly Regex IdRE_;
      private static readonly Regex StringEscapeRE_;

      [GeneratedRegex(@"^"".*?(?:$"".*?)*""",                   RegexOptions.Compiled)]private static partial Regex StringRE();
      [GeneratedRegex(@"^(?m:#((?:##)?.*?)?(?:#|$))",           RegexOptions.Compiled)]private static partial Regex CommentRE();
      [GeneratedRegex(@"^(?:0x[\dA-Fa-f]+|[+-]?[_\d]+)",        RegexOptions.Compiled)]private static partial Regex IntRE();
      [GeneratedRegex(@"^[+-]?\d+\.\d(?:\d*(?:[eE][+-]?\d+)?)?",RegexOptions.Compiled)]private static partial Regex FloatRE();
      [GeneratedRegex(@"\s+",                                   RegexOptions.Compiled)]private static partial Regex ReduceWhitespaceRE();
      private static Regex GlyphRE() => GlyphRE_; // Lazy initialization of the regex to avoid static constructor issues.
      private static Regex ReservedWordRE() => ReservedWordRE_; // Lazy initialization of the regex to avoid static constructor issues.
      private static Regex IdRE() => IdRE_; // Lazy initialization of the regex to avoid static constructor issues.
      private static Regex StringEscapeRE() => StringEscapeRE_; // Lazy initialization of the regex to avoid static constructor issues.

      static Token() {
         // Place multi-character glyphs first to ensure they match before any single character contained in them.
         Glyph2TokenType = new Dictionary<string,TokenType> {
            { "=:", TokenType.MACROPROCBODY },     // Indicates a macro body that should NOT be in-lined (the default for = is to in-line).
            { ":=", TokenType.INLINEPROCBODY },    // Indicates a ContainingProc body that should be in-lined.
            { "+",  TokenType.PLUS },              // Used as affixes (argument) separator and as the succeed operator.
            { "-",  TokenType.MINUS },             // Used as Declarations variable separator and as the fail operator.
            { "*",  TokenType.STAR },              // Repeat from group start operator and string parameter.
            { "?",  TokenType.ABORT },             // Terminate the program.
            { ">",  TokenType.AFFIXDIR },          // Used to indicated the argument direction, as >in, out>, or >in-out>.
            { ",",  TokenType.SEP },               // Used in interface lists, CONST and VAR Declarations, and as call separators.
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

         GlyphRE_        = new Regex(@$"^({string.Join("|", Glyph2TokenType.Keys.Select(Regex.Escape))})", RegexOptions.Compiled);
         ReservedWordRE_ = new Regex(@$"^(?:{string.Join("|", Enum.GetNames(typeof(ReservedWord)))})", RegexOptions.Compiled);
         // Allows annotation symbols to precede and follow the ID; these are removed.
         IdRE_           = new Regex(@$"^{AnnotationSymbols.CharacterClass}*([a-z][a-z0-9 ]*){AnnotationSymbols.CharacterClass}*", RegexOptions.Compiled);
         // Must match all occurrences anywhere in a string
         StringEscapeRE_ = new Regex(@$"\$([{string.Join("", Escape2Char.Keys)}])", RegexOptions.Compiled);

         ErrorToken = new Token();
         ACTIONToken = new Token(TokenClass.ResWord,"ACTION");

         CommentableReservedWords = [  RW.PROGRAM, RW.MODULE, RW.LAYER, RW.SECTION,
                                       RW.ACTION, RW.FUNCTION, RW.PREDICATE, RW.TEST,
                                       RW.CONST, RW.LIST, RW.VAR, RW.NOTE
                                    ];
      }

      public static string ToGlyph(TokenType tt) => Token.TokenType2Glyph.TryGetValue(tt,out string? glyph) ? glyph ?? "" : "";

      [JsonInclude]
      public readonly TokenType type;
      // Depending on the type, one of the following may be populated:
      //    RESWORD: reservedWordValue is the enum of the reserved word
      //    ID:      StringValue is the identifier Id
      //    INT:     intValue is the long
      //    STRING:  StringValue is the string
      //    FLOAT:   floatValue is the double
      //    COMMENT: StringValue is the comment

      [JsonInclude]
      public string TokenString { get; private set; } = "";

      [JsonInclude]
      public readonly ReservedWord? reservedWordValue;
      [JsonInclude]
      public string? StringValue { get; private set; }
      [JsonInclude]
      public readonly long? intValue;
      [JsonInclude]
      public readonly double? floatValue;
      // [JsonInclude]
      public readonly string? Comments;  // Only for certain reserved words and used only during parsing.

      public enum TokenClass { String, ID, ResWord, Glyph, Comment, Int, Float, Error };

      [JsonConstructor]
      public Token() : this(TokenClass.Error,"ERROR") { }
      public Token(string text) : this(TokenClass.ID,text) { }
      public Token(RW rw) : this(TokenClass.ResWord,rw.ToString()) { }

      private Token(TokenClass cls,string text) {
         TokenString = text;
         switch (cls) {
            case TokenClass.Error:
               type = TokenType.ERROR;
               StringValue = text.Trim().Replace(" ","");
               break;
            case TokenClass.Comment:
               type = TokenType.COMMENT;
               StringValue = text.Trim('#','\n');
               break;
            case TokenClass.String:
               type = TokenType.STRING;
               StringValue = StringEscapeRE().Replace(text.Trim('"'),match => Escape2Char[match.Groups[1].Value]);
               break;
            case TokenClass.ID:
               type = TokenType.ID;
               TokenString = ReduceWhitespaceRE().Replace(text, " ").Trim();  // Reduce all white space to a single space
               StringValue = ReduceWhitespaceRE().Replace(text,"");
               break;
            case TokenClass.ResWord:
               type = TokenType.RESWORD;
               reservedWordValue = Enum.Parse<ReservedWord>(text);
               // Attach comments encountered before reserved words that can have comments.
               if (CommentableReservedWords.Contains(reservedWordValue.Value) && collectedComments.Count > 0) {
                  bool blockComment = collectedComments[0].StartsWith("##");
                  IEnumerable<string> trimmedComments = collectedComments.Select(c => c.Trim('#', ' '));
                  int width = trimmedComments.Select(c => c.Length).Max();
                  string mark = blockComment ? "###" : "#";
                  StringBuilder sb = new();
                  if (blockComment) sb.AppendLine(new string('#',width+8));
                  foreach (string comment in trimmedComments) sb.AppendLine(string.Format("{0} {1} {0}",mark,comment.Trim().PadRight(width)));
                  if (blockComment) sb.AppendLine(new string('#',width+8));
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
                  intValue = long.Parse(text.Replace("_",""),text.StartsWith("0x") ? NumberStyles.HexNumber : NumberStyles.Integer,CultureInfo.InvariantCulture);
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
      /// <param Id="regex">The RE that describes a token of the given class.</param>
      /// <param Id="tokenClass">The class of the token.</param>
      /// <param Id="input">The input string. If a token is found the characters consumed are removed.</param>
      /// <param Id="token">The token that was found. If none, it will be ErrorToken.</param>
      /// <returns>True if a token was found.</returns>
      /// <param Id="fileName"></param><param Id="lineNumber"></param>
      private static bool HandleMatch(Regex regex,TokenClass tokenClass,ref string input,out Token token) {
         Match match = regex.Match(input);
         if (match.Success) {
            input = input[match.Length..].TrimStart();
            token = new Token(tokenClass,match.Groups[^1].Value);
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
      /// <param Id="input">The input string. Consumed characters are removed.</param>
      /// <param Id="token">The token that was found.</param>
      /// <param Id="fileName">The Id of the file being tokenized.</param>
      /// <param Id="lineNumber">The line number of the token.</param>
      /// <returns>true if the staring started with a valid token.</returns>
      public static bool TryCreateToken(ref string input,out Token token,string fileName,ref int lineNumber) {
         while (true) {
            input = input.TrimStart();
            token = ErrorToken; 
            if (string.IsNullOrEmpty(input)) return false;

            // Collect comments into a list
            Match match = CommentRE().Match(input);
            if (match.Success) {
               input = input[match.Length..].TrimStart();
               if (! match.Groups[^1].Value.StartsWith(Note.Marker)) collectedComments.Add(match.Groups[^1].Value);
               continue;
            }

            if (HandleMatch(StringRE(),TokenClass.String,ref input,out token)) return true;
            if (HandleMatch(ReservedWordRE(),TokenClass.ResWord,ref input,out token)) return true;
            if (HandleMatch(IdRE(),TokenClass.ID,ref input,out token)) {
               Debug.Assert(token.StringValue != null,"ID token has no string value");
               // Guarantee that all IDs with the same string value (i.e., ignoring spaces) are the same.
               if (IDTokens.TryGetValue(token.StringValue,out Token? idToken)) {
                  token = idToken;
               } else {
                  IDTokens[token.StringValue] = token;
               }
               return true;
            }
            if (HandleMatch(FloatRE(),TokenClass.Float,ref input,out token)) return true;
            if (HandleMatch(IntRE(),TokenClass.Int,ref input,out token)) return true;
            if (HandleMatch(GlyphRE(),TokenClass.Glyph,ref input,out token)) return true; // Must be placed after Int & Float as they may start with + or -
            return false;
         }
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
      /// Return the token as a Id. If the token is an ID, the Id is returned.
      /// - Runs of spaces and non-letters are replaced with the replacement string.
      /// - If the token is not an ID, the token type is prepended to the Id.
      /// - The Id is lowercased.
      /// </summary>
      /// <param Id="spaceReplacement"></param>
      /// <returns>The normalized Id.</returns>
      /// <example>Token.TryCreateToken("3.14",out Token token).AsIdentifier() -> "float_3_14"</example>
      internal string AsIdentifier(string replacement = "_",bool camelCase = true) 
         => $"{(type != TT.ID ? type.ToString().ToLower() + replacement : "")}{Regex.Replace(TokenString,@"(?:\s+|[^\p{L}\d])",replacement).AsIdentifier(camelCase:camelCase)}";
      internal static Token From(RW rw) => new(TokenClass.ID,rw.ToString());
      public static bool operator ==(Token? left,Token? right) => EqualityComparer<Token>.Default.Equals(left,right);
      public static bool operator !=(Token? left,Token? right) => !(left == right);

      
   }
}
