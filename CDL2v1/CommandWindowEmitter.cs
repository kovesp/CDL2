using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal class CommandWindowEmitter : Emitter {
      private readonly CommandPromptWindow commandWindow;
      public CommandWindowEmitter(CommandPromptWindow commandWindow) => this.commandWindow = commandWindow;
      protected override void WriteLine(string item) => commandWindow.WriteLine(item);
   }
}
