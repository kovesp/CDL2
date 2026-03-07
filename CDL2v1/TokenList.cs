// <auto-gen>
//=======================================================================
// <copyright file="TokenList.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-02-10</creation-date>
// 
// <summary>
//   The source is tokenized into a token list. This list is what is then parsed.
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

using CDL2v1;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace CDL2v1 {
   public class TokenList(Action<TokenType[],Token,RW[]> unexpectedTokenReporter,TokenList.Options options = TokenList.Options.None) {
      [Flags]
      public enum Options {
         None = 0,
         ThrowOnUnexpectedToken = 1,
         // Add more serializationOptions as needed
      }

      /// <summary>
      /// Used only to initialize instance variable in parser. Will not actually be used.
      /// </summary>
      public TokenList() : this((_,_,_) => { },Options.None) { }

      private readonly Action<TokenType[],Token,RW[]> ReportUnexpectedToken = unexpectedTokenReporter;

      public readonly List<Token> tokens = [];
      public Options options = options;

      public int Count => tokens.Count;

      private const int ContextLength = 10;
      public string Context => string.Concat(tokens.TakeLast(ContextLength).Select(t=>t.TokenString));

      public void Add(Token token) => tokens.Add(token);
      public bool IsNonEmpty() => tokens.Count > 0;
      public Token Peek() => IsNonEmpty() ? tokens[0] : Token.ErrorToken;
      public Token Peek(int n) => n < tokens.Count ? tokens[n] : Token.ErrorToken;
      public bool IsNext(TT type) => IsNonEmpty() && tokens[0].type == type;

      /// <summary>
      /// Check if the next token is one of the types in the list.
      /// </summary>
      /// <param Id="types"></param>
      /// <returns></returns>
      public bool IsNext(List<TT> types) => IsNonEmpty() && types.Contains(tokens[0].type);


      public bool IsNext(RW reservedWord) => IsNonEmpty() && tokens[0].type == TT.RESWORD && tokens[0].reservedWordValue == reservedWord;
      public bool IsNext(RW reservedWord,[NotNullWhen(true)] out string? comment) {
         if (IsNonEmpty() && tokens[0].type == TT.RESWORD && tokens[0].reservedWordValue == reservedWord) {
            comment = tokens[0].Comments;
            return true;
         } else {
            comment = null;
            return false;
         }
      }
      public bool IsNext(IEnumerable<RW> reservedWords) => IsNonEmpty() && tokens[0].type == TT.RESWORD && reservedWords.Contains(tokens[0].reservedWordValue ?? 0); // ??0 is a hack to suppress error: reservedWordValue can't be null because tokens[0].type == TT.RESWORD

      /// <summary>
      /// Determines whether the next two tokens represent the specified reserved word type followed by the specified
      /// identifier.
      /// </summary>
      /// <param name="type">The reserved word type to compare against the first token.</param>
      /// <param name="id">The identifier to compare against the second token.</param>
      /// <returns>true if the first token is the specified reserved word type and the second token is the specified identifier;
      /// otherwise, false.</returns>
      public bool IsNextTypeAndId(RW type,ID id,bool liberal = true) 
         => tokens.Count > 1 && tokens[0].type == TT.RESWORD && IsSameType(tokens[0].reservedWordValue??RW.NONE,type,liberal) && tokens[1].type == TT.ID && ID.From(tokens[1]) == id;
      private static readonly RW[] AlgorithmTypes = [RW.TEST,RW.PREDICATE,RW.FUNCTION,RW.ACTION];
      
      private static bool IsSameType(RW type,RW tokenType,bool liberal = true)
         => type == tokenType || (liberal && AlgorithmTypes.Contains(type) && AlgorithmTypes.Contains(tokenType));

      public Token Next() {
         if (IsNonEmpty()) {
            Token token = tokens[0];
            Skip();
            return token;
         }
         return Token.ErrorToken;
      }

      public bool Consume(TT type) {
         if (IsNext(type)) {
            Skip();
            return true;
         }
         return false;
      }
      public bool Consume(RW type) {
         if (IsNext(type)) {
            Skip();
            return true;
         }
         return false;
      }

      public void Skip() => tokens.RemoveAt(0);

      public void SetOptions(Options options) {
         this.options = options;
      }

      public bool CanConsume(TT type,out Token token) {
         if (IsNext(type)) {
            token = Next();
            return true;
         }
         ReportUnexpectedToken([type],Peek(),[]);
         token = Token.ErrorToken;
         return false;
      }
      public bool CanConsume(out ID id) {
         if (CanConsume(TT.ID,out Token token)) {
            id = ID.From(token);
            return true;
         }
         ReportUnexpectedToken([TT.ID],Peek(),[]);
         id = ID.ErrorID;
         return false;
      }
      public bool CanConsume(List<TT> types,out Token token) {
         if (IsNext(types)) {
            token = Next();
            return true;
         }
         ReportUnexpectedToken([.. types],Peek(),[]);
         token = Token.ErrorToken;
         return false;
      }
      public bool CanConsume(TT type) {
         if (IsNext(type)) {
            Next();
            return true;
         }
         ReportUnexpectedToken([type],Peek(),[]);
         return false;
      }
      public bool Optional(out ID id) {
         id = ID.ErrorID;
         return IsNext(TT.ID) && CanConsume(out id);
      }
      public bool Optional(TT type) => IsNext(type) && Consume(type);
      public bool Optional(RW type) => IsNext(type) && Consume(type);
      public bool Optional(RW type,[NotNullWhen(true)] out string? comments) => IsNext(type,out comments) && Consume(type);
      public bool Optional(List<TT> types,[NotNullWhen(true)] out Token token) { token = Token.ErrorToken; return IsNext(types) && CanConsume(types,out token); }
      public bool Optional(TT type,[NotNullWhen(true)] out Token token) { token = Token.ErrorToken; return IsNext(type) && CanConsume(type,out token); }

      public bool CanConsume(List<TT> types) {
         if (IsNext(types)) {
            Next();
            return true;
         }
         ReportUnexpectedToken([.. types],Peek(),[]);
         return false;
      }

      public bool CanConsume(RW reservedWord,[NotNullWhen(true)] out string? comments) {
         comments = null;
         if (IsNonEmpty() && tokens[0].type == TT.RESWORD && tokens[0].reservedWordValue == reservedWord) {
            comments = tokens[0].Comments;
            Next();
            return true;
         }
         ReportUnexpectedToken([TT.RESWORD],Peek(),[reservedWord]);
         Skip();
         return false;
      }
      public bool CanConsume(RW reservedWord) => CanConsume(reservedWord,out string? _);
      public bool CanConsume(List<RW> reservedWords,out Token token) {
         if (IsNext(reservedWords)) {
            token = Next();
            return true;
         }
         ReportUnexpectedToken([TT.RESWORD],Peek(),[.. reservedWords]);
         Skip();
         token = Token.ErrorToken;
         return false;
      }

      /// <summary>
      /// Consume a unit start (MODULE, LAYER, SECTION, PROGRAM or end (ENDMOD, ENDLAY, ENDSEC, ENDPROG) reserved word and an ID and the ending period.
      /// </summary>
      /// <param Id="unit">The unit type reserved word.</param>
      /// <param Id="Id">If stating a unit, the Id is set, If ending a unit, it is verified that the Id matches the one given in the unit close.</param>
      /// <returns></returns>
      public bool CanConsumeContainerDelimiter(RW unit,ref ID id,[NotNullWhen(true)] out string? comments) {
         Debug.Assert(Token.UnitStarters.Contains(unit) || Token.UnitEnders.Contains(unit));
         if (Optional(unit,out comments) && CanConsume(out ID thisId) && CanConsumeEnd()) {
            if (Token.UnitStarters.Contains(unit)) {
               id = thisId;
               return true;
            } else if (Token.UnitEnders.Contains(unit)) {
               if (thisId != id) throw new Exception($"Expected {id} in {unit} but found {thisId}");
               return true;
            }
         }
         id = ID.ErrorID;
         return false;
      }

      internal bool CanConsumeEnd() => CanConsumeTerminator(TT.END);
      internal bool CanConsumeSep() => CanConsumeTerminator(TT.SEP);
      internal bool CanConsumeTerminator(TT terminator) {
         if (IsNext(terminator)) {
            Next();
            return true;
         } else {
            return false;
         }
      }

      /// <summary>
      /// A note is written as "NOTE." and can be used to place comments in multiple places.
      /// </summary>
      /// <param name="note">Return comment(s) attached to the NOTE as Note of type Note.</param>
      /// <param name="needsEnd">When a NOTE is inside a PROCEDURE, there is no period.</param>
      /// <returns></returns>
      internal bool CanConsumeNote(out Note? note,bool needsEnd = true) {
         if (Optional(RW.NOTE,out string? comments) && comments is not null) {
            if (needsEnd) CanConsumeEnd();
            note = new Note(Severity.Note,400,comments);
            return true;
         } else {
            note = null;
            return false;
         }
      }

      /// <summary>
      /// Determines the type of input, handling comments correctly
      /// </summary>
      /// <param name="input">The raw input text</param>
      /// <param name="trimmedInput">Output: the trimmed input</param>
      /// <param name="firstWord">Output: the first word of the input</param>
      /// <returns>The classified input type</returns>
      public static InputType ClassifyInput(string input, out string trimmedInput, out string firstWord) {
          trimmedInput = input.Trim();
          firstWord = "";
          
          if (trimmedInput.Length == 0) return InputType.Empty;
          
          // Check for command comment
          if (trimmedInput.StartsWith(CommandInterpreter.CommandComment)) return InputType.CommandComment;
          
          // Check for CDL2 comment
          if (trimmedInput.StartsWith('#')) return InputType.CDL2Comment;
          
          // Get the first word for further classification
          firstWord = trimmedInput.Split(' ', '\t', '\r', '\n')[0];
          
          // Check if it's a command (lowercase letter)
          if (char.IsAsciiLetterLower(trimmedInput[0])) return InputType.Command;
          
          // Check if it's a CDL2 construct (reserved word)
          if (char.IsAsciiLetterUpper(trimmedInput[0])) {
              SelectorType type = Abbreviation<SelectorType>.Identify(firstWord.ToUpper());
              if (type != SelectorType.INVALID) return InputType.CDL2Construct;
          }
          
          return InputType.Invalid;
      }
   }
}

