using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal class CodeEmitterDebug : CodeEmitterBase {
      public CodeEmitterDebug() => Target = "Debug";
      protected override void WriteLine(string line) => Debug.WriteLine(LinePrefix+line.Replace("\n","\n"+LinePrefix));
   }
}
