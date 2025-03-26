using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal class EmitterDebug : EmitterBase {
      private static readonly EmitterDebug Instance = new();
      public EmitterDebug() {
         Target = "Debug";
         SupressDebug = true;
      }
      protected override void WriteLine(string line) => Debug.WriteLine(LinePrefix+line.Replace("\n","\n"+LinePrefix));
      public static void WriteDebug(string line) => Instance.WriteLine(Instance.RemoveSpans(line));
   }
}
