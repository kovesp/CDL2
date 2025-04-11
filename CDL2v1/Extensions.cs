using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.CommandLine.Parsing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Windows.Media;
using System.Reflection.Emit;
using System.Diagnostics.Metrics;
using System.Xml.Serialization;
using System.Windows;

namespace CDL2v1 {
   
   [Serializable]
   public class Set<T> : HashSet<T> {
      public Set() { }
      public Set(ICollection<T> collection) : base(collection) { }
      public Set(IEnumerable<T> enumerable) {
         foreach (T item in enumerable) Add(item);
      }
   }

   /// <summary>
   /// A stack of int-s whose top element can be incremented and decrmented.
   /// </summary>
   public class ModifiableStack : Stack<int> {
      public ModifiableStack() : base() { }
      public ModifiableStack(int capacity) : base(capacity) { }
      public ModifiableStack(IEnumerable<int> collection) : base(collection) { }

      /// <summary>
      /// Increment the top elment of the stack.
      /// </summary>
      /// <param name="stack"></param>
      /// <returns></returns>
      public static ModifiableStack operator ++(ModifiableStack stack) {
         stack.Push(stack.Pop() + 1);
         return stack;
      }
      /// <summary>
      /// Decrement the top element of the stack, but do not allow it to go below 0.
      /// </summary>
      /// <param name="stack"></param>
      /// <returns></returns>
      public static ModifiableStack operator --(ModifiableStack stack) {
         if (stack.Peek() > 0) stack.Push(stack.Pop() - 1);
         return stack;
      }
      /// <summary>
      /// Compare the top element of the stack with a value.
      /// </summary>
      /// <param name="stack"></param>
      /// <param name="value"></param>
      /// <returns></returns>
      /// <exception cref="InvalidOperationException"></exception>
      public static bool operator >=(ModifiableStack stack, int value) {
         if (stack.Count == 0) {
            throw new InvalidOperationException("Cannot compare an empty stack.");
         }
         return stack.Peek() >= value;
      }
      /// <summary>
      /// Compare the top element of the stack with a value.
      /// </summary>
      /// <param name="stack"></param>
      /// <param name="value"></param>
      /// <returns></returns>
      /// <exception cref="InvalidOperationException"></exception>
      public static bool operator <=(ModifiableStack stack, int value) {
         if (stack.Count == 0) {
            throw new InvalidOperationException("Cannot compare an empty stack.");
         }
         return stack.Peek() <= value;

      }
      /// <summary>
      /// Set the top element of the stack to a value.
      /// </summary>
      /// <param name="v">The value >= 0 to set, default is 0.</param>
      internal void SetTop(int v=0) {
         if (Count > 0) Pop();
         Push(Math.Min(0,v));
      }
      /// <summary>
      /// Reset the top element of the stack to 0.
      /// </summary>
      internal void ResetTop() => SetTop(0);
   }

   public static class Extensions {
      public static bool IsValidFileName(this string? fileName) {
         if (string.IsNullOrWhiteSpace(fileName)) {
            return false;
         } else {
            return fileName.All(ch => !Path.GetInvalidFileNameChars().Contains(ch));
         }
      }

      /// <summary>
      /// Return the types that implement the given interface.
      /// </summary>
      /// <typeparam name="TInterface"></typeparam>
      /// <returns></returns>
      public static IEnumerable<Type> GetImplementorsOfInterface<TInterface>() => Assembly.GetExecutingAssembly().GetTypes()
             .Where(type => typeof(TInterface).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);
      /// <summary>
      /// Normalize a string to a valid identifier.
      /// </summary>
      /// <param name="str"></param>
      /// <param name="prefix"></param>
      /// <param name="replacement"></param>
      /// <param name="camelCase"></param>
      /// <param name="literalObjectName"></param>
      /// <returns></returns>
      public static string AsIdentifier(this string str,string prefix = "",string replacement = "",bool camelCase = false,bool literalObjectName = false) {
         if (literalObjectName) return str.Replace(" ","");
         if (prefix != "") prefix += "_";
         str = Regex.Replace(str,@"[^\p{L}\d\s]+","_",RegexOptions.Compiled).Trim();
         if (camelCase) {
            return prefix.ToLower() + str.Split(" ").Select((word,i) => i == 0 ? word.ToLower() : char.ToUpper(word[0]) + word[1..].ToLower()).Aggregate((a,b) => a + b);
         } else {
            return prefix.ToLower() + str.ToLower().Replace(" ",replacement);
         }
      }

      /// <summary>
      /// Return the plural of the word word for count.
      /// </summary>
      /// <param name="count">Number of items.</param>
      /// <param name="word">The item name.</param>
      /// <param name="plural">If given the plural of word. Otherwise an s, es, or ies is added as ap appropriate.</param>
      /// <returns></returns>
      public static string Plural(this int count, string word, string? plural = null) {
         string items;
         if (count == 1) {
            items = word;
         } else if (plural != null) {
            items = plural;
         } else if (Regex.IsMatch(word, @"(s|sh|ch|x|z)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)) {
            items =  word + "es";
         } else if (Regex.IsMatch(word, @"[^aeiou]y$", RegexOptions.IgnoreCase | RegexOptions.Compiled)) {
           items = Regex.Replace(word, "y$", "ies", RegexOptions.IgnoreCase | RegexOptions.Compiled);
         } else {
            items = word + "s";
         }
         return $"{count:N0} {items}";
      }
      public static string Plural(this string word,int count,string? plural=null) => count.Plural(word, plural);
      /// <summary>
      /// Dim a color by a factor.
      /// </summary>
      /// <param name="color"></param>
      /// <param name="factor">The 0 <= factor <= 1 to use.</param>
      /// <returns></returns>
      /// <exception cref="ArgumentOutOfRangeException"></exception>
      public static Color DimColor(this Color color,double factor) {
         if (factor < 0 || factor > 1)
            throw new ArgumentOutOfRangeException(nameof(factor),"Factor must be between 0 and 1.");

         return Color.FromArgb(
             color.A,
             (byte)(color.R * factor),
             (byte)(color.G * factor),
             (byte)(color.B * factor)
         );
      }
      /// <summary>
      /// Intensify a color by a factor.
      /// </summary>
      /// <param name="color"></param>
      /// <param name="factor">The factor >= 1 to use.</param>
      /// <returns></returns>
      /// <exception cref="ArgumentOutOfRangeException"></exception>
      public static Color IntensifyColor(this Color color,double factor) {
         if (factor < 1)
            throw new ArgumentOutOfRangeException(nameof(factor),"Factor must be greater than or equal to 1.");

         return Color.FromArgb(
             color.A,
             (byte)Math.Min(255,color.R * factor),
             (byte)Math.Min(255,color.G * factor),
             (byte)Math.Min(255,color.B * factor)
         );
      }
      public static string IntensifyColor(this string color,double factor) => FromHex(color).IntensifyColor(factor).ToHex();
      public static Color DimColor(this string color,double factor) => FromHex(color).DimColor(factor);

      /// <summary>
      /// Convert a hex string to a Color object.
      /// </summary>
      /// <param name="hex"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentException"></exception>
      public static Color FromHex(this string hex) {
         if (string.IsNullOrWhiteSpace(hex))
            throw new ArgumentException("Invalid hex color string",nameof(hex));

         // Ensure the hex string starts with '#'
         if (hex[0] != '#')
            hex = "#" + hex;

         // Use ColorConverter to convert the hex string to a Color object
         return (Color)ColorConverter.ConvertFromString(hex);
      }
      /// <summary>
      /// Convert a Color to a hex string.
      /// </summary>
      /// <param name="color"></param>
      /// <returns></returns>
      public static string ToHex(this Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

      /// <summary>
      /// Convert an IEnumerable to a Set.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="enumerable"></param>
      /// <returns></returns>
      public static Set<T> ToSet<T>(this IEnumerable<T> enumerable) => [.. enumerable];

      /// <summary>
      /// Decorate a string with the given decoration.
      /// This means encpsulating the string in a span tag with the given style and the foreground and background colors.
      /// This is only done if the emitter supports decoration.
      /// </summary>
      /// <param name="str"></param>
      /// <param name="emitter"></param>
      /// <param name="element"></param>
      /// <param name="decoration"></param>
      /// <returns></returns>
      internal static string Decorate(this string str,EmitterBase emitter,SE element,PrettyPrinter.Decoration? decoration=null) {
         if (str == null) return "";
         if (emitter.SupportsDecoration) {
            if (element != SE.AlgorithmName) {
               decoration = PrettyPrinter.Decorators[element];
            } else if (decoration == null) {
            }
            Debug.Assert(decoration != null,$"No decoration for {element}");
            return string.Join("\n",Regex.Split(str,@"\r\n|\r|\n",RegexOptions.Compiled)
                           .Select(str => $"<span fg='{decoration.FG}' bg='{decoration.BG}' style='{decoration.Style}'>{str}</span>"));
         } else {
            return str;
         }
      }
      internal static string Decorate(this RW rw,EmitterBase emitter,SE element) => rw.ToString().Decorate(emitter,element);
      //internal static string Decorate(this string str,EmitterBase Emitter,SE element) =>str.Decorate(Emitter,element);
      internal static string Decorate(this Token token,EmitterBase emitter,SE element) => token.TokenString.Decorate(emitter,element);
      internal static string Decorate(this ID id,EmitterBase emitter,SE element) 
         => /*id.Comments!.Decorate(Emitter,SE.Comment) +*/ id.Name.Decorate(emitter,element);
      internal static string Decorate(this long i,EmitterBase emitter) => i.ToString().Decorate(emitter,SE.Number);
      internal static string Decorate(this double d,EmitterBase emitter) => d.ToString().Decorate(emitter,SE.Number);
      internal static string Decorate(this ID algorithmId,EmitterBase emitter,PrettyPrinter.Decoration decoration) => algorithmId.ToString().Decorate(emitter,SE.AlgorithmName,decoration);
   }
}
