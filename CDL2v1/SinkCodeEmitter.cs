using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal class SinkCodeEmitter : ICodeEmiter {
      public string Target => "Sink";

      public void CloseTarget() { }
      public void Emit(params string[] code) { }
      public void Emitnl(params string[] code) { }
      public void OpenTarget(string target) { }
   }
}
