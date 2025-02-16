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
      ICodeEmiter emitter = new SinkCodeEmitter();

      private static List<Token.ReservedWord> ludeTypes = new List<Token.ReservedWord> {
         Token.ReservedWord.PRELUDE,
         Token.ReservedWord.ROOT,
         Token.ReservedWord.POSTLUDE
      };
      public void GenerateCode(Program program,ICodeEmiter emitter) {
         this.emitter = emitter;

         cg.GenerateStart(program,emitter);  // Generate the overall scafolding

         foreach (Module module in program.children) {
            GenerateModuleCode(module);
         }

         foreach (Token.ReservedWord ludeType in ludeTypes) {

         }

         cg.GenerateEnd(program);
      }

      private void GenerateModuleCode(Module module) {
         cg.GenerateStart(module);  // Generate the code for each module
         foreach (Layer layer in module.children) {
            GenerateLayerCode(layer);
         }
         cg.GenerateEnd(module);
      }

      private void GenerateLayerCode(Layer layer) {
         cg.GenerateStart(layer);
         foreach (Section section in layer.children) {
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
         foreach (Token.ReservedWord ludeType in ludeTypes) {
            cg.generateLudeStart(ludeType,section);

            cg.generateLudeEend(ludeType,section);
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
