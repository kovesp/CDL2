using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal class DebugCodeEmitter : CodeEmitterBase {
      public DebugCodeEmitter() => Target = "Debug";
      protected override void Write(params object[] items) => Debug.WriteLine(items);
   }
}
