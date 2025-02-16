using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal interface ICodeEmiter {
      void Emit(params string[] code);
      void Emitnl(params string[] code);
      void OpenTarget(string target);
      void CloseTarget();

      string Target { get; }
   }
}
