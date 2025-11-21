using System;
using System.Collections.Generic;
using System.Text;

namespace CDL2v1 {
   /// <summary>
   /// This emitter distributes emitted lines to multiple underlying emitters.
   /// </summary>
   internal class EmitterMulticast : Emitter {
      private readonly List<Emitter> _emitters = [];
      public EmitterMulticast(params Emitter[] emitters) {
         ArgumentNullException.ThrowIfNull(emitters);
         _emitters.AddRange(emitters);
      }
      protected override void WriteLine(string item) {
         foreach (Emitter emitter in _emitters) {
            emitter.Emit(item);
         }
      }
   }
}
