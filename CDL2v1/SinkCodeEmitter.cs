using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal class SinkCodeEmitter : CodeEmitterBase {
      public SinkCodeEmitter() => Target = "Sink";
      protected override void Write(string item) { }
   }
}
