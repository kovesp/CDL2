using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
   }
}
