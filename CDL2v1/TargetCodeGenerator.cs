// <auto-gen>
//=======================================================================
// <copyright file="TargetCodeGenerator.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-05-09</creation-date>
// 
// <summary>
//   Base class of taget code generators providing some simple support methods.
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

#define HASH_TARGET_NAMES
#define USE_CAMEL_CASE

using System.Diagnostics.Eventing.Reader;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CDL2v1 {
   internal abstract partial class TargetCodeGenerator {

      protected Emitter emitter = new EmitterSink();

      public virtual bool RequiresPredeclaration => false;
      public virtual void GenerateDeclaration(Algorithm algorithm) { }

      #region Helpers
      protected void Newline(bool optional = false) {
         if (optional) emitter.EmitnlOption(); else emitter.Emitnl();
      }
      protected void EmitUnitStartComment(Container unit) => emitter.Emitnl($"{LineComment} Begin {unit.ContainerName}");
      protected void EmitUnitEndComment(Container unit) => emitter.Emitnl($"{LineComment} End {unit.ContainerName}");
      public virtual void GenerateComment(string comment,bool block=false) {
         foreach (string line in comment.Split('\n')) {
            if (block) {
               emitter.Emit($"{BlockComment.Start} {line.Trim()} {BlockComment.End} ");
            } else {
               emitter.Emitnl($"{LineComment} {line.Trim()}");
            }
         }
      }

      /// <summary>
      /// Override when constructing a subclass if required
      /// </summary>
      public virtual string LineComment => "//";
      public virtual (string Start, string End) BlockComment => ("/*", "*/");

      public void IncrementIndent() => emitter.IndentLevel++;
      public void DecrementIndent() => emitter.IndentLevel--;

      /// <summary>
      /// Return true if the macro contains multiple statements separated by any of the given separators.
      /// </summary>
      /// <param name="macro"></param>
      /// <param name="separators"></param>
      /// <returns></returns>
      protected static bool HasMultipleStatments(Macro macro,Regex separators) =>
         macro.Elements.OfType<STRING>().Any(str => FindLastSeparator(str.value,separators).position >= 0);

      /// <summary>
      /// Splits the elements of a macro into two groups based on the last occurrence of any specified separator.
      /// </summary>
      /// <remarks>The method searches backwards through macro elements to find the first STRING element that contains
      /// any separator. Within that STRING, it finds the last separator and splits the STRING at that point, keeping
      /// the separator with the first part. This method does not modify the original macro or its elements.</remarks>
      /// <param name="macro">The macro whose elements are to be split. Must not be null.</param>
      /// <param name="separators">Array of separator strings (e.g., [";", "\n"] for PowerShell).</param>
      /// <returns>A tuple containing two lists: the first list includes all elements up to and including the last separator, and
      /// the second list contains all elements following the last separator. If no separator is found, the first list
      /// is empty and the second contains all elements.</returns>
      public static (List<IElement> beforeLast, List<IElement> lastExpression) SplitMacroBody(Macro macro,Regex separators) {
         if (macro.Elements.Count == 0) return ([],[]);

         List<IElement> beforeLast = [];
         List<IElement> lastExpression = [];

         for (int i = macro.Elements.Count - 1 ; i >= 0 ; i--) {
            if (macro.Elements[i] is STRING str) {
               (int position, string separator) = FindLastSeparator(str.value,separators);
               if (position >= 0) {
                  for (int j = 0 ; j < i ; j++) beforeLast.Add(macro.Elements[j]);

                  int newlineIndex = separator.IndexOf('\n');
                  int splitAt = position + separator.Length;
                  string beforeSeparator = str.value.Substring(0,splitAt);
                  string afterSeparator = str.value.Substring(splitAt);

                  if (beforeSeparator.Length > 0) beforeLast.Add(new STRING(beforeSeparator));
                  if (afterSeparator.Length > 0) lastExpression.Add(new STRING(afterSeparator));

                  for (int j = i + 1 ; j < macro.Elements.Count ; j++) lastExpression.Add(macro.Elements[j]);

                  return (beforeLast, lastExpression);
               }
            }
         }

         lastExpression.AddRange(macro.Elements);
         return (beforeLast, lastExpression);
      }

      private static (int position, string separator) FindLastSeparator(string value,Regex separators) {
         Match match = separators.Match(value);
         Match lastMatch = match;

         while (match.Success) {
            lastMatch = match;
            match = match.NextMatch();
         }

         if (lastMatch.Success) return (lastMatch.Index, lastMatch.Value);
         return (-1, "");
      }


      // Target object names
      private static string Prefix(NamedElement obj) => obj switch {
         Var       => "V_",
         LIST      => "LL_",
         Const     => "C_",
         Affix     => "A_",
         Local     => "L_",
         Algorithm => AlgPrefix((Algorithm)obj),
         _         => ""
      };

      private static string AlgPrefix(Algorithm alg) => (alg is Procedure ? "P" :"M")+alg.AlgorithmType switch {
         RW.TEST      => "T_",
         RW.PREDICATE => "P_",
         RW.FUNCTION  => "F_",
         RW.ACTION    => "A_",
         _            => "_"
      };

#if USE_CAMEL_CASE
      private const bool useCamelCase = true;
#else
      private const bool useCamelCase = false;
#endif

#if HASH_TARGET_NAMES
      /// <summary>
      /// Composes a unique name for the specified element, optionally including a hash-based prefix for certain element
      /// types.
      /// </summary>
      /// <remarks>For elements of type CDL2Object, Affix, or Local, the composed name includes a hash-based
      /// prefix derived from the module, layer, and section identifiers. For other element types, only the element's
      /// name is used. The resulting name is suitable for use as an identifier and may use underscores as space
      /// replacements.</remarks>
      /// <param name="elem">The element for which to compose a name. Must not be null.</param>
      /// <returns>A string representing the composed name for the element. For certain element types, the name includes a
      /// hash-based prefix to ensure uniqueness.</returns>
      protected static string ComposeName(NamedElement elem) {
         string namePart = elem.Id.Name.AsIdentifier(camelCase: useCamelCase,spaceReplacement: "_",literalObjectName: elem.IsSynthetic);

         switch (elem) {
            case CDL2Object:
            case Affix:
            case Local:
               string[] parts = [elem.Module!.Id.CanonicalName,elem.Layer!.Id.CanonicalName,elem.Section!.Id.CanonicalName];
               //string prefix = new([.. parts.Take(3).Select(p => p.FirstOrDefault())]);
               string prefix = "";

               ulong hash = ComputeHash64(string.Join("",parts));
               string hashPart = ToBase62(hash,5);

               return (prefix + hashPart)[..8] + "_" + namePart;
            default:
               return namePart;
         }
      }

      private static ulong ComputeHash64(string input) {
         byte[] hashBytes = MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input));
         return BitConverter.ToUInt64(hashBytes,0);
      }

      private static string ToBase62(ulong value,int length) {
         const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
         StringBuilder result = new();
         while (value > 0) {
            result.Insert(0,chars[(int)(value % 62)]);
            value /= 62;
         }
         return result.ToString().PadLeft(length,'0');
      }
#else
      protected static string ComposeName(NamedElement elem) 
         => elem.FQN(camelCase: useCamelCase,spaceReplacement:spaceReplacement,literalObjectName: elem.IsSynthetic);
#endif // HASH_TARGET_NAMES

      protected virtual string TargetName(NamedElement obj,string suffix = "") => Prefix(obj)+ComposeName(obj)+suffix;

      protected virtual string TargetName(ID id) => id.Name.AsIdentifier(camelCase: false);
      protected virtual string TargetName(Group group) => group.Id.Name.AsIdentifier(camelCase: false);

      protected virtual string TArgName(IActualArg arg,string suffix = "") => arg switch {
         Affix a => TargetName(a,suffix),
         Local l => TargetName(l,suffix),
         Var v => TargetName(v,suffix),
         _ => throw new NotImplementedException($"TName not implemented for type {arg.GetType()}."),
      };

      protected static readonly Random Random = new();
      protected virtual string InitialValue => Random.Next(0,int.MaxValue).ToString();

#endregion Helpers

   }
}

