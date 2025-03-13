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
   /// <param id="cg"></param>
   internal class CodeGenerator(ICodeGenerator cg) {
      private readonly ICodeGenerator cg = cg;
      //private EmitterBase emitter = new EmitterSink();

      private static readonly List<RW> ludeTypes = [ RW.PRELUDE,RW.ROOT,RW.POSTLUDE];

      /// <summary>
      /// Generate code for the program and all its modules.
      /// If there is a program, then use its Ludes. Otherwise, use the Ludes from all modules.
      /// </summary>
      /// <param id="program"></param>
      /// <param id="modules"></param>
      /// <param id="emitter"></param>
      public void GenerateCode(Program program,EmitterBase emitter) {
         cg.GenerateStart(program,emitter);  // Generate the overall scaffolding
         foreach (ID mod in program.Parts) GenerateModuleCode(Program.Modules[mod]);
         foreach (RW ludeType in ludeTypes)
            foreach (Module mod in program.Lude(ludeType).Where(mod => mod.Ludes[ludeType].Count > 0))
               GenerateLude(ludeType,mod);
         cg.GenerateEnd(program);
      }

      /// <summary>
      /// Generate code for the Lude of a given type for a module.
      /// </summary>
      /// <param id="ludeType"></param>
      /// <param id="mod"></param>
      private void GenerateLude(RW ludeType,Module mod) {
         foreach (ID secId in mod.Ludes[ludeType]) {
            foreach (Layer layer in mod.Children.Cast<Layer>()) {
               foreach (Section section in layer.Children.Cast<Section>()) {
                  if (section.id == secId) {
                     Debug.Assert(section.Ludes[ludeType].Count == 1,$"CG: {section} referenced in {ludeType} of {mod}. Expected reference to single Procedure, found {section.Ludes[ludeType].Count}");
                     ID procId = section.Ludes[ludeType][0];
                     Logger.Log($"Generating Procedure for {procId}");
                     GenerateProcedureCode((Procedure)section.local[procId]);
                  }
               }
            }
         }
      }

      /// <summary>
      /// Generate code for a module. It is up to the specific code generator to determine whether this code goes into a separate file or not.
      /// </summary>
      /// <param id="module"></param>
      private void GenerateModuleCode(Module module) {
         cg.GenerateStart(module);  // Generate the code for each module
         foreach (ID expId in module.exports) cg.GenerateExport(module,expId);
         foreach (Layer layer in module.Children.Cast<Layer>()) GenerateLayer(layer); 
         cg.GenerateEnd(module);
      }

      /// <summary>
      /// Generate proc for a layer. Typically there is no target proc associated with this.
      /// </summary>
      /// <param id="layer"></param>
      private void GenerateLayer(Layer layer) {
         cg.GenerateStart(layer);
         foreach (Section section in layer.Children.Cast<Section>()) GenerateSection(section);
         cg.GenerateEnd(layer);
      }

      /// <summary>
      /// Generate a container. Again, there will likely be no target proc associated with a container itself.
      /// So generate proc for each routine and for the Ludes.
      /// A lude is just proc with a special id
      /// </summary>
      /// <param id="container"></param>
      private void GenerateSection(Section section) {
         cg.GenerateStart(section);
         GenerateObjects(section.Constants,GenerateConstant);
         GenerateObjects(section.Variables,v=>cg.GenerateCodeDeclareVar(v.id));
         GenerateObjects(section.Lists,l => cg.GenerateCodeDeclareList(l.id,(section.local[l.lwb] as Const)!.id,(section.local[l.upb] as Const)!.id));
         GenerateObjects(section.Macros,GenerateMacroCode);
         GenerateObjects(section.Procedures,GenerateProcedureCode);
         cg.GenerateEnd(section);
      }

      private void GenerateObjects<T>(IEnumerable<T> items,Action<T> generate) {
         cg.GenerateDataSectionStart(items.Count,typeof(T).Name);
         foreach (T item in items) generate(item);
      }

      private void GenerateConstant(Const constant) {
         cg.GenerateConstantStart(constant.id);
         foreach (IConstElement elem in constant.elements) {
            switch (elem) {
               case INT i: cg.GenerateConstElemInt(i.value); break;
               case FLOAT f: cg.GenerateConstElemFloat(f.value); break;
               case STRING s: cg.GenerateConstElemString(s.value); break;
               case ID id:
                  if (id.container is Section sec && sec.local[id] is Const c) {
                     cg.GenerateReferenceConst(c.id);
                  } else {
                     throw new NotImplementedException($"GenerateSection: Reference to wrong element type ");
                  }
                  break;
            }
         }
         cg.GenerateConstantEnd(constant.id);
      }

      private void GenerateMacroCode(Macro macro) {
         IEnumerable<Var> variables = macro.GetReferencedVariables();
         cg.GenerateStart(macro);
         GenerateAlgorithmHeader(macro,variables);

         cg.GenerateMacroBodyStart(macro);
         foreach (IMacroElement elem in macro.elements) {
            switch (elem) {
               case INT i: cg.GenerateMacroElemInt(i.value); break;
               case FLOAT f: cg.GenerateMacroElemFloat(f.value); break;
               case STRING s: cg.GenerateMacroElemString(s.value); break;
               case ID id:
                  // This should be a reference to a Const, Var or List, so check which one
                  if (id.container is Section section) {
                     switch (section.local[id]) {
                        case Const c: cg.GenerateReferenceConst(c.id); break;
                        case Var v:   cg.GenerateReferenceVar(v.id); break;
                        case LIST l:  cg.GenerateReferenceList(l.id); break;
                        default:
                           throw new NotImplementedException($"GenerateMacroCode: Reference to wrong element type {section.local[id].GetType().Name}");
                     }
                  } else {
                     throw new NotImplementedException($"GenerateMacroCode: Unresolved reference to {id}");
                  }
                  break;
               case Affix a: cg.GenerateReferenceAffix(a.id); break;
               case Local lo: cg.GenerateReferenceLocal(lo.id); break;
               default:
                  throw new NotImplementedException($"GenerateMacroCode: Unknown element type {elem.GetType()}");
            }
         }
         cg.GenerateMacroBodyEnd(macro);

         FinalizeAffixesAndVars(macro,variables);
         cg.GenerateEnd(macro);
      }

      private void FinalizeAffixesAndVars(Algorithm algorithm,IEnumerable<Var> variables) {
         bool needed = algorithm.NeedsFinalization;
         cg.FinalizationStart(algorithm,needed);
         if (needed) {
            foreach (Affix affix in algorithm.affixes) cg.GenerateFinalizeAffixOrVar(affix.id,affix.affixDir);
            foreach (Var var in variables) cg.GenerateFinalizeAffixOrVar(var.id,AD.transput,isVar: true);
         }
         cg.FinalizationEnd(algorithm,needed);
      }

      private void GenerateAlgorithmHeader(Algorithm alg,IEnumerable<Var> variables) {
         cg.GenerateAlgorithmHeaderStart(alg);
         if (alg.affixes.Count > 0) {
            cg.GenerateDeclareAffix(alg.affixes[0].id,alg.affixes[0].affixDir);
            foreach (Affix affix in alg.affixes.Skip(1)) {
               cg.GenerateParamSeparator();
               cg.GenerateDeclareAffix(affix.id,affix.affixDir);
            }
         }
         cg.GenerateAlgorithmHeaderEnd(alg);
         foreach (Affix affix in alg.affixes) cg.GenerateInitializeAffixOrVar(affix.id,affix.affixDir);
         foreach (Var var in variables) cg.GenerateInitializeAffixOrVar(var.id,AD.transput,isVar: true);
         foreach (Local local in alg.locals) cg.GenerateDeclareLocal(local.id);
      }

      private void GenerateProcedureCode(Procedure proc) {
         IEnumerable<Var> variables = proc.GetReferencedVariables();
         cg.GenerateStart(proc);
         GenerateAlgorithmHeader(proc,variables);
         // gen code here
         FinalizeAffixesAndVars(proc,variables);
         cg.GenerateEnd(proc);
      }
   }
}
