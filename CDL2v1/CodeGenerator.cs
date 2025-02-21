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
      CodeEmitterBase emitter = new CodeEmitterSink();

      private static List<RW> ludeTypes = new List<RW> {
         RW.PRELUDE,
         RW.ROOT,
         RW.POSTLUDE
      };
      public void GenerateCode(Program program,CodeEmitterBase emitter) {
         this.emitter = emitter;

         cg.GenerateStart(program,emitter);  // Generate the overall scafolding

         foreach (Module module in program.children) {
            GenerateModuleCode(module);
         }

         foreach (RW ludeType in ludeTypes) {

         }

         cg.GenerateEnd(program);
      }

      /// <summary>
      /// Generate code for a mudule. It is up to the specific code generator to determine whether this code goes into a separate file or not.
      /// </summary>
      /// <param name="module"></param>
      private void GenerateModuleCode(Module module) {
         cg.GenerateStart(module);  // Generate the code for each module
         foreach (ID expId in module.export) {
            cg.GenerateExport(module,expId);
         }
         foreach (Layer layer in module.children) {
            GenerateLayer(layer);
         }
         cg.GenerateEnd(module);
      }

      /// <summary>
      /// Generate code for a layer. Typically there is no target code associated with this.
      /// </summary>
      /// <param name="layer"></param>
      private void GenerateLayer(Layer layer) {
         cg.GenerateStart(layer);
         foreach (Section section in layer.children) {
            GenerateSection(section);
         }
         cg.GenerateEnd(layer);
      }

      /// <summary>
      /// Geberate a section. Again, there will likely be no target code associated with a section itself.
      /// So generate code for each routine and for the ludes.
      /// A lude is just code with a speciaol name
      /// </summary>
      /// <param name="section"></param>
      private void GenerateSection(Section section) {
         cg.GenerateStart(section);
         foreach (ID procId in section.routines) {
            Proc proc = (Proc)section.Symbols[procId];
            if (proc is Code code) {
               GenerateCodeCode(code);
            } else if (proc is Macro macro) {
               GenerateMacroCode(macro);
            }
         }
         foreach (RW ludeType in ludeTypes) {
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
