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

namespace CDL2v1 {
   public static class Extensions {
      public static bool IsValidFileName(this string? fileName) {
         if (string.IsNullOrWhiteSpace(fileName)) {
            return false;
         } else {
            return fileName.All(ch => !Path.GetInvalidFileNameChars().Contains(ch));
         }
      }

      public static IEnumerable<Type> GetImplementorsOfInterface<TInterface>() => Assembly.GetExecutingAssembly().GetTypes()
             .Where(type => typeof(TInterface).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);

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

      public static Color FromHex(this string hex) {
         if (string.IsNullOrWhiteSpace(hex))
            throw new ArgumentException("Invalid hex color string",nameof(hex));

         // Ensure the hex string starts with '#'
         if (hex[0] != '#')
            hex = "#" + hex;

         // Use ColorConverter to convert the hex string to a Color object
         return (Color)ColorConverter.ConvertFromString(hex);
      }
      public static string ToHex(this Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

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
