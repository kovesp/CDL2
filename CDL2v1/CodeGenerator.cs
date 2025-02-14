using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace CDL2v1 {
   /// <summary>
   /// 
   /// </summary>
   /// <param name="cg"></param>
   internal class CodeGenerator(ICodeGenerator cg) {
      ICodeGenerator cg = cg;

      public void GenerateCode(Program program) {
         cg.GenerateStart(program);  // Generate the ovrall scafolding
         foreach (Module module in program.parts) {
            GenerateModuleCode(module);
         }
         cg.GenerateEnd(program);
      }

      private void GenerateModuleCode(Module module) {
         cg.GenerateStart(module);  // Generate the code for each module
         foreach (Layer layer in module.layers) {
            GenerateLayerCode(layer);
         }
         cg.GenerateEnd(module);
      }

      private void GenerateLayerCode(Layer layer) {
         cg.GenerateStart(layer);
         foreach (Section section in layer.sections) {
            GenerateSectionCode(section);
         }
         cg.GenerateEnd(layer);
      }

      private void GenerateSectionCode(Section section) {
         cg.GenerateStart(section);
         foreach (ID procId in section.routines) {
            Proc proc = (Proc)section.symbolTable[procId];
            if (proc is Code code) {
               GenerateCodeCode(code);
            } else if (proc is Macro macro) {
               GenerateMacroCode(macro);
            }
         }
         cg.GenerateEnd(section);
      }

      private void GenerateMacroCode(Macro macro) {
         cg.GenerateStart(macro);
         cg.GenerateEnd(macro);
      }

      private void GenerateCodeCode(Code code) {
         cg.GenerateStart(code);
         cg.GenerateEnd(code);
      }
   }
}
