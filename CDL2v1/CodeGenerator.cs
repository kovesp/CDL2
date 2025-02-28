using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
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

         foreach (RW ludeType in ludeTypes) {
            foreach (ID modId in program.ludes[ludeType]) {
               Module mod = (Module)program.Symbols[modId];
               foreach (ID secId in mod.ludes[ludeType]) {
                  foreach (Container layer in mod.children) {
                     foreach (Container section in layer.children) {
                        if (section.name == secId) {
                           Debug.Assert(section.ludes[ludeType].Count == 1,$"CG: {section} referenced in {ludeType} of {mod}. Expected referrence to single Procedure, found {section.ludes[ludeType].Count}");
                           ID procId = section.ludes[ludeType][0];
                           Logger.Log($"Generating proc for {procId}");
                           GenerateProcedureCode((Procedure)section.Symbols[procId]);
                        }
                     }
                  }
               }
            }
         }
         cg.GenerateEnd(program);

         foreach (Module module in program.children) {
            GenerateModuleCode(module);
         }
      }

      /// <summary>
      /// Generate proc for a mudule. It is up to the specific proc generator to determine whether this proc goes into a separate file or not.
      /// </summary>
      /// <param name="module"></param>
      private void GenerateModuleCode(Module module) {
         cg.GenerateStart(module);  // Generate the proc for each module
         foreach (ID expId in module.export) {
            cg.GenerateExport(module,expId);
         }
         foreach (Layer layer in module.children) {
            GenerateLayer(layer);
         }
         cg.GenerateEnd(module);
      }

      /// <summary>
      /// Generate proc for a layer. Typically there is no target proc associated with this.
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
      /// Geberate a section. Again, there will likely be no target proc associated with a section itself.
      /// So generate proc for each routine and for the ludes.
      /// A lude is just proc with a speciaol name
      /// </summary>
      /// <param name="section"></param>
      private void GenerateSection(Section section) {
         cg.GenerateStart(section);
         foreach (ID procId in section.routines) {
            Algorithm proc = (Algorithm)section.Symbols[procId];
            if (proc is Procedure code) {
               GenerateProcedureCode(code);
            } else if (proc is Macro macro) {
               GenerateMacroCode(macro);
            }
         }
         //foreach (RW ludeType in ludeTypes) {
         //   cg.GenerateLudeStart(ludeType,section);

         //   cg.GenerateLudeEend(ludeType,section);
         //}
         cg.GenerateEnd(section);
      }

      private void GenerateMacroCode(Macro macro) {
         cg.GenerateStart(macro);
         GenerateAlgorithmHeader(macro);

         cg.GenerateEnd(macro);
      }

      private void GenerateAlgorithmHeader(Algorithm alg) {
         cg.GenerateAlgorithmHeaderStart(alg);
         if (alg.formals.Count > 0) {
            cg.GenerateCode(alg.formals[0]);
            foreach (Param formal in alg.formals.Skip(1)) {
               cg.GenerateParamSeparator();
               cg.GenerateCode(formal);
            }
         }
         cg.GenerateAlgorithmHeaderEnd(alg);
      }

      private void GenerateProcedureCode(Procedure proc) {
         cg.GenerateStart(proc);
         GenerateAlgorithmHeader(proc);

         cg.GenerateEnd(proc);
      }
   }
}
