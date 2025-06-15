using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal class EmitterSink : Emitter {
      public EmitterSink() => Target = "Sink";
      protected override void WriteLine(string item) { }
   }
}
