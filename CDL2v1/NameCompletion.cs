//=======================================================================
// <copyright file="NameCompletion.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-01-15</creation-date>
//
// <summary>
//   Handles name completion logic for CDL2 object names.
// </summary>
//=======================================================================

using System.Diagnostics.CodeAnalysis;

namespace CDL2v1 {
   /// <summary>
   /// Provides name completion functionality for CDL2 object names
   /// </summary>
   public static class NameCompletion {
      /// <summary>
      /// Gets completions for the current text
      /// </summary>
      /// <param name="text">The current input text</param>
      /// <returns>Completion result if matches found, null otherwise</returns>
      public static bool GetSelectorCompletions(string text, [NotNullWhen(true)] out CompletionResult? result) {
         result = null;

         string textForSelection = TrimToFirstSelector(text);
         
         Selection? sel = new(textForSelection);
         if (sel is null || sel.Count == 0) return false;

         int endingStart = GetLastObjectName(textForSelection);
         if (endingStart < 0) return false;

         // Check if the word we're trying to complete is already complete
         int wordEnd = endingStart;
         while (wordEnd < textForSelection.Length && (char.IsLetter(textForSelection[wordEnd]) || textForSelection[wordEnd] == ' ')) {
            wordEnd++;
         }
         
         // If we're at the end and the last character was a space, nothing to complete
         if (wordEnd >= textForSelection.Length && wordEnd > endingStart && textForSelection[wordEnd - 1] == ' ') {
            return false;
         }

         string[] completions = [.. sel.Select(s => s.Object!.Id.Name).Distinct().Order()];
         int prefixLen = text.Length - textForSelection.Length;
         int completionStartPos = prefixLen + endingStart;

         result = new CompletionResult {
            Completions = completions,
            StartPosition = completionStartPos,
            CommonPrefix = FindLongestCommonPrefix(completions)
         };
         return true;
      }

      /// <summary>
      /// Gets command completions for the current text
      /// </summary>
      /// <param name="text">The current input text</param>
      /// <returns>Completion result if matching commands found, null otherwise</returns>
      public static bool GetCommandCompletions(string text,[NotNullWhen(true)] out CompletionResult? result) {
         result = null;
         // Command completion only works on the first word
         string trimmed = text.TrimStart();
         if (trimmed.Length == 0) {
            result = new CompletionResult {
               Completions = [.. Enum.GetNames<CommandType>().Where(s=>s!="INVALID").Distinct().Order() ],
               StartPosition = 0,
               CommonPrefix = ""
            };
         } else { 
            // Must start with lowercase letter
            if (!char.IsAsciiLetterLower(trimmed[0])) return false;

            // Find the end of the first word (no spaces allowed in command name)
            int wordEnd = 0;
            while (wordEnd < trimmed.Length && char.IsAsciiLetterLower(trimmed[wordEnd])) {
               wordEnd++;
            }

            // If we found a non-whitespace character, invalid command format
            if (wordEnd < trimmed.Length && !char.IsWhiteSpace(trimmed[wordEnd])) return false;

            // If there's anything after the first word (after skipping whitespace), fail
            if (wordEnd < trimmed.Length) return false;

            string commandPrefix = trimmed[..wordEnd];
            if (commandPrefix.Length == 0) return false;

            // Get all matching commands from Abbreviation<CommandType>
            string[] completions = [.. Enum.GetNames<CommandType>()
                                          .Where(cmdName => cmdName.StartsWith(commandPrefix))
                                          .Distinct().Order() ];

            if (completions.Length == 0) return false;

            // Start position is at the beginning of the trimmed text
            int startPos = text.Length - trimmed.Length;
            result = new CompletionResult {
               Completions = completions,
               StartPosition = startPos,
               CommonPrefix = FindLongestCommonPrefix(completions)
            };
         }
         
         return true;
      }

      /// <summary>
      /// Gets setting name completions for the current text
      /// </summary>
      /// <param name="text">The current input text</param>
      /// <param name="result">Completion result if matching settings found</param>
      /// <returns>True if completions found, false otherwise</returns>
      public static bool GetSettingCompletions(string text,[NotNullWhen(true)] out CompletionResult? result) {
         result = null;
         string trimmed = text.TrimStart();
         if (trimmed.Length == 0) return false;

         // Fail if there's any capitalized word (selector present)
         if (TrimToFirstSelector(trimmed) != trimmed) return false;

         // Skip the first word (the command)
         int firstWordEnd = 0;
         while (firstWordEnd < trimmed.Length && !char.IsWhiteSpace(trimmed[firstWordEnd])) {
            firstWordEnd++;
         }

         // If we haven't moved past the first word, no completions
         if (firstWordEnd >= trimmed.Length) return false;

         // Skip whitespace after first word
         while (firstWordEnd < trimmed.Length && char.IsWhiteSpace(trimmed[firstWordEnd])) {
            firstWordEnd++;
         }

         // If nothing after the first word, no completions
         if (firstWordEnd >= trimmed.Length) return false;

         // Find the last lowercase word (possibly starting with '-')
         string afterFirstWord = trimmed[firstWordEnd..];
         int lastWordStart = afterFirstWord.Length;

         // Work backwards to find the start of the last word
         for (int i = afterFirstWord.Length - 1 ; i >= 0 ; i--) {
            if (char.IsWhiteSpace(afterFirstWord[i])) {
               lastWordStart = i + 1;
               break;
            }
            if (i == 0) {
               lastWordStart = 0;
            }
         }

         string lastWord = afterFirstWord[lastWordStart..];
         if (lastWord.Length == 0) return false;

         // Check if input already has a '-'
         bool hasDash = lastWord.StartsWith('-');
         
         // Remove leading '-' if present for matching
         string settingPrefix = hasDash ? lastWord[1..] : lastWord;

         // Must be lowercase letters (or empty for just '-')
         if (settingPrefix.Length > 0 && !char.IsAsciiLetterLower(settingPrefix[0])) return false;

         // Get all setting names that match the prefix (case-insensitive)
         string[] completions = [.. Settings.AllSettings
                                    .Select(s => s.Name.ToLower())
                                    .Where(name => name.StartsWith(settingPrefix,StringComparison.OrdinalIgnoreCase))
                                    .Select(name => "-" + name)  // Always prepend '-' to completions
                                    .Distinct()
                                    .Order()];

         if (completions.Length == 0) return false;

         // Calculate start position (start of the word, whether it has '-' or not)
         int startPos = text.Length - trimmed.Length + firstWordEnd + lastWordStart;

         result = new CompletionResult {
            Completions = completions,
            StartPosition = startPos,
            CommonPrefix = FindLongestCommonPrefix(completions)
         };

         return true;
      }

      private static string TrimToFirstSelector(string text) {
         bool inString = false;

         for (int i = 0 ; i < text.Length ; i++) {
            char c = text[i];

            if (c == '"') {
               // Check if it's escaped by $
               if (i > 0 && text[i - 1] == '$') continue;
               else inString = !inString;
            }

            if (!inString && char.IsUpper(c)) {
               // Check if it's the start of a word
               if (i == 0 || !char.IsLetter(text[i - 1]) || char.IsWhiteSpace(text[i - 1])) {
                  // Check if there's a '^' immediately before the capital letter
                  int startPos = i;
                  if (i > 0 && text[i - 1] == '^') {
                     startPos = i - 1;
                  }
                  return text[startPos..];
               }
            }
         }

         return text;
      }

      private static int GetLastObjectName(string text) {
         int lastCapPos = -1;
         
         // Find the LAST capitalized word - any capital letter that's not preceded by a lowercase letter
         for (int i = 0 ; i < text.Length ; i++) {
            if (char.IsUpper(text[i])) {
               // It's a capitalized word start if previous char is not a lowercase letter
               if (i == 0 || !char.IsLower(text[i - 1])) {
                  lastCapPos = i;
               }
            }
         }
         
         if (lastCapPos < 0) return -1;
         
         // Skip past the capitalized word itself
         int pos = lastCapPos;
         while (pos < text.Length && char.IsLetter(text[pos])) pos++;
         
         // Skip whitespace after the capitalized word
         while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
         
         // If there's lowercase text after, return its position (the partial identifier)
         if (pos < text.Length && char.IsAsciiLetterLower(text[pos])) {
            return pos;
         }
         
         // Otherwise, we're completing the capitalized word itself
         return lastCapPos;
      }

      private static string FindLongestCommonPrefix(IEnumerable<string> names) {
         string[] nameArray = names.ToArray();
         if (nameArray.Length == 0) return "";
         if (nameArray.Length == 1) return nameArray[0];

         string first = nameArray[0];
         int prefixLen = 0;

         for (int i = 0 ; i < first.Length ; i++) {
            char c = first[i];
            bool allMatch = true;

            foreach (string name in nameArray.Skip(1)) {
               if (i >= name.Length || name[i] != c) {
                  allMatch = false;
                  break;
               }
            }

            if (!allMatch) break;
            prefixLen = i + 1;
         }

         return first.Substring(0,prefixLen);
      }
   }

   /// <summary>
   /// Result of a name completion operation
   /// </summary>
   public record CompletionResult {
      /// <summary>
      /// Array of possible completions
      /// </summary>
      public required string[] Completions { get; init; }

      /// <summary>
      /// Position in the original text where the completion should start
      /// </summary>
      public required int StartPosition { get; init; }

      /// <summary>
      /// Longest common prefix of all completions
      /// </summary>
      public required string CommonPrefix { get; init; }
   }
}