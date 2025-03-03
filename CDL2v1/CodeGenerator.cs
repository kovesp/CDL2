// Ignore Spelling: CDL

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
      private readonly ICodeGenerator cg = cg;
      //private EmitterBase emitter = new EmitterSink();

      private static readonly List<RW> ludeTypes = [ RW.PRELUDE,RW.ROOT,RW.POSTLUDE];

      /// <summary>
      /// Generate code for the program and all its modules.
      /// If there is a program, then use its Ludes. Otherwise, use the Ludes from all modules.
      /// </summary>
      /// <param name="program"></param>
      /// <param name="modules"></param>
      /// <param name="emitter"></param>
      public void GenerateCode(Program? program,Set<Module> modules,EmitterBase emitter) {
         cg.GenerateStart(program,emitter);  // Generate the overall scaffolding
         foreach (RW ludeType in ludeTypes)
            foreach (Module mod in (program != null ? program.Lude(ludeType) : modules).Where(mod => mod.Ludes[ludeType].Count > 0))
               GenerateLude(ludeType,mod);
         cg.GenerateEnd(program);
         foreach (Module module in modules) {
            GenerateModuleCode(module);
         }
      }

      /// <summary>
      /// Generate code for the Lude of a given type for a module.
      /// </summary>
      /// <param name="ludeType"></param>
      /// <param name="mod"></param>
      private void GenerateLude(RW ludeType,Module mod) {
         foreach (ID secId in mod.Ludes[ludeType]) {
            foreach (Container layer in mod.Children) {
               foreach (Container section in layer.Children) {
                  if (section.name == secId) {
                     Debug.Assert(section.Ludes[ludeType].Count == 1,$"CG: {section} referenced in {ludeType} of {mod}. Expected reference to single Procedure, found {section.Ludes[ludeType].Count}");
                     ID procId = section.Ludes[ludeType][0];
                     Logger.Log($"Generating proc for {procId}");
                     GenerateProcedureCode((Procedure)section.Symbols[procId]);
                  }
               }
            }
         }
      }

      /// <summary>
      /// Generate code for a module. It is up to the specific code generator to determine whether this code goes into a separate file or not.
      /// </summary>
      /// <param name="module"></param>
      private void GenerateModuleCode(Module module) {
         cg.GenerateStart(module);  // Generate the code for each module
         foreach (ID expId in module.export) cg.GenerateExport(module,expId);
         foreach (Layer layer in module.Children.Cast<Layer>()) GenerateLayer(layer); 
         cg.GenerateEnd(module);
      }

      /// <summary>
      /// Generate proc for a layer. Typically there is no target proc associated with this.
      /// </summary>
      /// <param name="layer"></param>
      private void GenerateLayer(Layer layer) {
         cg.GenerateStart(layer);
         foreach (Section section in layer.Children.Cast<Section>()) GenerateSection(section);
         cg.GenerateEnd(layer);
      }

      /// <summary>
      /// Generate a section. Again, there will likely be no target proc associated with a section itself.
      /// So generate proc for each routine and for the Ludes.
      /// A lude is just proc with a special name
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
            foreach (Affix formal in alg.formals.Skip(1)) {
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
