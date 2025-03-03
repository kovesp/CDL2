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

namespace CDL2v1 {
   public static class Extensions {
      public static bool IsValidFileName(this string? fileName) {
         if (string.IsNullOrWhiteSpace(fileName)) {
            return false;
         } else {
            return fileName.All(ch => !Path.GetInvalidFileNameChars().Contains(ch));
         }
      }

      public static IEnumerable<Type> GetImplementorsOfInterface<TInterface>() {
         return Assembly.GetExecutingAssembly().GetTypes()
             .Where(type => typeof(TInterface).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);
      }

      public static string AsIdentifier(this string str,string prefix = "",string replacement = "",bool camelCase = false) {
         if (prefix != "") prefix += "_";
         str = Regex.Replace(str,@"[^\p{L}\d\s]+","_").Trim();
         if (camelCase) {
            return prefix.ToLower() + str.Split(" ").Select((word,i) => i == 0 ? word.ToLower() : char.ToUpper(word[0]) + word.Substring(1).ToLower()).Aggregate((a,b) => a + b);
         } else {
            return prefix.ToLower() + str.ToLower().Replace(" ",replacement);
         }
      }

      internal static string Decorate(this string str,EmitterBase emitter,SE element,AIT invocationType) {
         if (emitter.SupportsDecoration) {
            PrettyPrinter.Decoration decor = element == SE.AlgorithmInvocation ? PrettyPrinter.InvocationDecorators[invocationType] : PrettyPrinter.Decorators[element];
            Debug.Assert(decor != null,$"No decoration for {element}");
            return $"<span color='{decor.Color}' style='{decor.Style}'>{str}</span>";
         } else {
            return str;
         }
      }
      internal static string Decorate(this RW rw,EmitterBase emitter,SE element) => rw.ToString().Decorate(emitter,element,AIT.None);
   }
}
