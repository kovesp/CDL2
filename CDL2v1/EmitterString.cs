using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal class EmitterString : EmitterBase {

      private string prefix = "";
      private string suffix = "";
      public EmitterString(string prefix = "", string suffix = "") {
         this.prefix = prefix;
         this.suffix = suffix;
         SupressDebug = true;
      }

      private readonly StringBuilder sb = new StringBuilder();

      protected override void WriteLine(string line) => sb.Append(prefix).Append(line).AppendLine(suffix);

      public override string ToString() {
         try {
            return sb.ToString();
         }
         finally {
            sb.Clear();
         }
      }
   }
}
