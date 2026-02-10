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
      public static CompletionResult? GetCompletions(string text) {
         string textForSelection = TrimToFirstSelector(text);
         Selection? sel = new(textForSelection);
         if (sel is null || sel.Count == 0) return null;

         int endingStart = GetLastObjectName(textForSelection);
         if (endingStart < 0) return null;

         string[] completions = [.. sel.Select(s => s.Object!.Id.Name).Distinct()];
         int prefixLen = text.Length - textForSelection.Length;
         int completionStartPos = prefixLen + endingStart;

         return new CompletionResult {
            Completions = completions,
            StartPosition = completionStartPos,
            CommonPrefix = FindLongestCommonPrefix(completions)
         };
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
               if (i == 0 || !char.IsLetter(text[i - 1]) || char.IsWhiteSpace(text[i - 1])) return text[i..];
            }
         }

         return text;
      }

      private static int GetLastObjectName(string text) {
         int lastCapPos = -1;

         for (int i = 0 ; i < text.Length ; i++) {
            if (i == 0 || !char.IsLetter(text[i - 1]) || char.IsWhiteSpace(text[i - 1])) {
               if (char.IsUpper(text[i])) lastCapPos = i;
            }
         }

         if (lastCapPos < 0) return -1;

         // Find end of this capitalized word
         int pos = lastCapPos;
         while (pos < text.Length && char.IsLetter(text[pos])) pos++;

         // Skip whitespace after the word
         while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;

         return pos;
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