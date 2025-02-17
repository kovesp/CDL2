using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal class SemanticAnalyzer {
      internal void Analyze(Program program) {
         Logger.Log(0,$"Analyzing {program}");
         foreach (Module module in program.children) {
            AnalyzeModule(module);
         }
      }

      private void AnalyzeModule(Module module) {
         Logger.Log(1,$"Analyzing {module.name}");
         foreach (Layer layer in module.children) {
            AnalyzeLayer(layer);
         }
      }

      private void AnalyzeLayer(Layer layer) {
         Logger.Log(1,$"Analyzing {layer.name}");
         foreach (Section section in layer.children) {
            AnalyzeSection(section);
         }
      }

      private void AnalyzeSection(Section section) {
         Logger.Log(1,$"Analyzing {section.name}");
         foreach (ID procId in section.routines) {
            Proc proc = (Proc)section.Symbols[procId];
            if (proc is Code code) {
               AnalyzeCode(code);
            } else if (proc is Macro macro) {
               AnalyzeMacro(macro);
            }
         }
      }

      private void AnalyzeMacro(Macro macro) {
         Logger.Log(2,$"Analyzing {macro.name}");
      }
      private void AnalyzeCode(Code code) {
         Logger.Log(2,$"Analyzing {code.name}");
      }
   }
}
