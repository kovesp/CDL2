using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal class DebugCodeEmitter : ICodeEmiter {
      public string Target => "Debugger";

      public void CloseTarget() { }
      public void Emit(params string[] code) {
         foreach (string item in code) Debug.Write(item);
      }
      public void Emitnl(params string[] code) {
         Emit(code);
         Debug.WriteLine("");
      }
      public void OpenTarget(string target) {}
   }
}
