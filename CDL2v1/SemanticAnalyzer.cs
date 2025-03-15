// Ignore Spelling: CDL

using Microsoft.VisualBasic;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static CDL2v1.Logger;


namespace CDL2v1 {
   /// <summary>
   /// Semantic Analyzer for CDL2.
   /// - An algorithm has an effect if it modifies the state of the system. This could be a variable, or a list or any other thing that changes
   ///   things (this can only happen via macros).
   /// - Modifying affixes is not considered an effect.
   /// - Algorithms of the type ACTION and PREDICATE must have an effect. This cannot be verified for macros, but is verified for PROCEDUREs
   /// - An algorithm of the type FUNCTION or TEST cannot have an effect. This cannot be verified for macros, but is verified for PROCEDUREs
   /// - TESTs and PREDICATEs may fail. The analyzer verifies that PROCEDURES of this kind can indeed fail.
   /// - If a PREDICATE fails after it has invoked a PREDICATE or ACTION it is said to have a defect. This is not allowed.
   /// - The output and transput parameters of a TEST and PREDICATE are not modified if the ALGORITHM fails. This is enforced by the target specific code generator.
   /// - Only ALGORRITHMs and CONSTs may appear in the interface (ABSTR, EXT, INV, IMPORT, EXPORT) lists of sections.
   /// - For ABSTR, EXT, EXPORT the corresponding declaration must be in the same SECTION.
   /// - For IMPORT the corresponding stub declaration must be in the same SECTION.
   /// - For INV a corresponding declaration must NOT be in the same SECTION. It must be either in the EXT list of one of the sections of the same LAYER, or
   ///   in in the ABSTR list of one of the sections of the LAYER below the current one.
   /// - For IMPORT, the corresponding declaration must be in the some SECTION in one of the MODULEs listed in the PARTS list of the PROGRAM being compiled
   ///   It must also be EXPORTed from there.
   /// - For each Algorithm call f verify that for each argument x of the call corresponding to affix a of f
   ///   - If a is input (i.e., >a), then x is a CONST, VAR, an input or transput affix of the procedure containing the call or 
   ///     a local or output affix that has already received a value (i.e., was the actual arg of a previous output affix).
   ///   - If a is output (i.e., a>), then x is a VAR, a local or an affix of the procedure containing the call.
   ///   - If a is transput (i.e., >a>), then apply the criteria for a being input, but disallow CONSTs
   ///   - Produce a warning for any case where a VAR or an affix is passed to an output affix, and then passed to another output affix without being
   ///     passed to input or transput affix.
   ///   - If a is a string (i.e., *a), then x is a string ("...") or a string affix of the containing procedure.
   /// 
   /// 
   /// 1. Verify that all referenced objects are declared and accessible.
   /// 2. Verify that there are no duplicate declarations.
   /// 3. Verify the above rules.
   /// </summary>
   internal class SemanticAnalyzer {
      internal void Analyze(Program MainProgram) {
         foreach (Program program in Program.Programs.Values) {
            AnalyzeProgram(program);
         }

         AnalyzeMainProgram(MainProgram);

         foreach (Module module in Program.Modules.Values) {
            AnalyzeModule(module);
         }
      }

      private void AnalyzeMainProgram(Program mainProgram) {
         Log(0,$"Analyzing MAIN {mainProgram.ContainerName}");
      }


      private void AnalyzeProgram(Program program) {
         Log(0,$"Analyzing {program.ContainerName}");
      }

      private void AnalyzeModule(Module module) {
         Log(1,$"Analyzing {module.ContainerName}");
         foreach (Layer layer in module.Children) {
            AnalyzeLayer(layer);
         }
      }

      private void AnalyzeLayer(Layer layer) {
         Log(1,$"Analyzing {layer.ContainerName}");
         foreach (Section section in layer.Children) {
            AnalyzeSection(section);
         }
      }

      private void AnalyzeSection(Section section) {
         Log(1,$"Analyzing {section.ContainerName}");
         Log(2,$"Analyzing provided interfaces");
         AnalyzeProvidedInterfaces(section,RW.ABSTR,section.abstr);
         AnalyzeProvidedInterfaces(section,RW.EXT,section.ext);
         AnalyzeProvidedInterfaces(section,RW.EXPORT,section.export);
         AnalyzeInvs(section);
         AnalyzeImports(section);



         foreach (Algorithm algorithm in section.declarations.Values.Where(obj => obj is Algorithm algorithm)) {
            Log(2,$"Analyzing {algorithm.GetType().Name} {algorithm.AlgorithmName}");
            if (algorithm is Procedure procedure) {
               AnalyzeProcedure(procedure,section);
            } else if (algorithm is Macro macro) {
               AnalyzeMacro(macro);
            }
         }
      }

      private void AnalyzeImports(Section section) {
         // IMPORT items must be EXPORT items in some container of known modules. In addtion, there must be a corresponding VAR, LIST, CONST, MACRO or CODE
         // declaration in this container as follows:
         //    - VAR, LIST, CONST: just a id
         //    - CDOE, MACRO: just the proc header without the locals with no body.
      }

      private void AnalyzeInvs(Section section) {
         // INV items must be in some container in the current layer declared as EXT or in the current layer's Owner declared as ABSTR.
      }

      private static void AnalyzeProvidedInterfaces(Section section,RW kind,Set<ID> set) {
         //foreach (ID id in set) {
         //   if (container.Symbols.ContainsKey(id)) {
         //      if (container.Symbols[id] is Undeclared) {
         //         ReportError(container,$"{kind} {id} is Instance");
         //      } else if (container.Symbols[id] is not IProvidedElement) {
         //         ReportError(container,$"{kind} {id} is not one of {{{string.Join(",",Section.ProvidedElementImplementors.Select(type => type.Name))}}}");
         //      }
         //   } else {
         //      ReportError(container,$"{kind} {id} not found");
         //   }
         //}
      }

      private static void ReportError(Container unit,string message) => Logger.ReportError($"{unit.ContainerName}: {message}");

      private void AnalyzeMacro(Macro macro) {
      }
      private void AnalyzeProcedure(Procedure proc,Section section) {
         bool hasEffect = AnalyzeEffect(proc.group,section);
         if (proc.HasEffect && !hasEffect) {
            ReportError(section,$"Procedure {proc.AlgorithmName} must have an effect");
         } else if (!proc.HasEffect && hasEffect) {
            ReportError(section,$"Procedure {proc.AlgorithmName} has a defect");
         }

      }

      /// <summary>
      /// Analyze the effect of a group of alternatives.
      /// If any call in any alternative has an effect then the group has an effect.
      /// </summary>
      /// <param name="group"></param>
      /// <param name="section"></param>
      /// <returns></returns>
      private bool AnalyzeEffect(Group group,Section section) {
         bool effect = false;
         foreach (Alternative alt in group.alternatives) effect |= AnalyzeEffect(alt,section);
         return effect;
      }

      /// <summary>
      /// Analyze the effect of an alternative.
      /// If any call in the alternative, or its ending group if any has an effect then the alternative has an effect.
      /// </summary>
      /// <param name="alt"></param>
      /// <param name="section"></param>
      /// <returns></returns>
      /// <exception cref="Exception"></exception>
      private bool AnalyzeEffect(Alternative alt,Section section) {
         foreach (Call call in alt.calls) {
            if (CheckCall(call,section)) return true;
         }
         if (alt.lastCall.type == LastCallType.Standard) {
            return CheckCall(alt.lastCall.call!,section);
         } else if (alt.lastCall.type == LastCallType.Group) {
            return AnalyzeEffect(alt.lastCall.group!,section);
         } else {
            return false;
         }

         /// <summary>
         /// A call has an effect if the algorithm it invokes has an effect.
         /// </summary>
         static bool CheckCall(Call call,Section section) {
            if (section.TryGetDeclaration(call.id,out Algorithm? algorithm)) {
               return algorithm!.HasEffect;
            } else {
               throw new Exception("Algorithm not found");
            }
         }
      }
   }
}
