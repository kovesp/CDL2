using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static CDL2v1.Logger;

namespace CDL2v1 {
   internal class SemanticAnalyzer {
      internal void Analyze(Program program) {
         Log(0,$"Analyzing {program}");
         foreach (Module module in program.Children) {
            AnalyzeModule(module);
         }
      }

      private void AnalyzeModule(Module module) {
         Log(1,$"Analyzing {module.id}");
         foreach (Layer layer in module.Children) {
            AnalyzeLayer(layer);
         }
      }

      private void AnalyzeLayer(Layer layer) {
         Log(1,$"Analyzing {layer.id}");
         foreach (Section section in layer.Children) {
            AnalyzeSection(section);
         }
      }

      private void AnalyzeSection(Section section) {
         Log(1,$"Analyzing {section.id}");
         Log(2,$"Analyzing provided interfaces");
         AnalyzeProvidedInterfaces(section,RW.ABSTR,section.abstr);
         AnalyzeProvidedInterfaces(section,RW.EXT,section.ext);
         AnalyzeProvidedInterfaces(section,RW.EXPORT,section.export);
         AnalyzeInvs(section);
         AnalyzeImports(section);



         foreach (ID procId in section.routines) {
            Algorithm proc = (Algorithm)section.Symbols[procId];
            if (proc is Procedure code) {
               AnalyzeCode(code);
            } else if (proc is Macro macro) {
               AnalyzeMacro(macro);
            }
         }
      }

      private void AnalyzeImports(Section section) {
         // IMPORT items must be EXPORT items in some section of known modules. In addtion, there must be a corresponding VAR, LIST, CONST, MACRO or CODE
         // declaration in this section as follows:
         //    - VAR, LIST, CONST: just a id
         //    - CDOE, MACRO: just the proc header without the locals with no body.
      }

      private void AnalyzeInvs(Section section) {
         // INV items must be in some section in the current layer declared as EXT or in the current layer's Owner declared as ABSTR.
      }

      private static void AnalyzeProvidedInterfaces(Section section,RW kind,Set<ID> set) {
         foreach (ID id in set) {
            if (section.Symbols.ContainsKey(id)) {
               if (section.Symbols[id] is Undeclared) {
                  ReportError(section,$"{kind} {id} is undeclared");
               } else if (section.Symbols[id] is not IProvidedElement) {
                  ReportError(section,$"{kind} {id} is not one of {{{string.Join(",",Section.ProvidedElementImplementors.Select(type => type.Name))}}}");
               }
            } else {
               ReportError(section,$"{kind} {id} not found");
            }
         }
      }

      private static void ReportError(Container unit,string message) => Logger.ReportError($"{unit.ContainerName}: {message}");

      private void AnalyzeMacro(Macro macro) {
         Log(2,$"Analyzing {macro.id}");
      }
      private void AnalyzeCode(Procedure code) {
         Log(2,$"Analyzing {code.id}");
      }
   }
}
