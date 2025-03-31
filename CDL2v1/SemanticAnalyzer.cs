// Ignore Spelling: CDL

using Microsoft.VisualBasic;

using System;
using System.CodeDom;
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
   ///   - If a is input (i.e., >a), then x is a CONST, VAR, an input or transput affix of the ContainingProc containing the call or 
   ///     a local or output affix that has already received a value (i.e., was the actual arg of a previous output affix).
   ///   - If a is output (i.e., a>), then x is a VAR, a local or an affix of the ContainingProc containing the call.
   ///   - If a is transput (i.e., >a>), then apply the criteria for a being input, but disallow CONSTs
   ///   - Produce a warning for any case where a VAR or an affix is passed to an output affix, and then passed to another output affix without being
   ///     passed to input or transput affix.
   ///   - If a is a string (i.e., *a), then x is a string ("...") or a string affix of the containing ContainingProc.
   /// 
   /// 
   /// 1. Verify that all referenced objects are declared and accessible.
   /// 2. Verify that there are no duplicate declarations.
   /// 3. Verify the above rules.
   /// </summary>
   [Serializable]
   public class SemanticAnalyzer : CompilationPhase {
      public SemanticAnalyzer(CDL2 compiler) : base(compiler) { }

      /// <summary>
      /// Analyze the given program.
      /// </summary>
      /// <param name="MainProgram"></param>
      internal void Analyze(Program MainProgram) {
         foreach (Program program in Database.Instance.Programs.Values) {
            AnalyzeProgram(program);
         }

         AnalyzeMainProgram(MainProgram);

         foreach (Module module in Database.Instance.Modules.Values) {
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
         // IMPORT items must be EXPORT items in some container of known modules. In addition, there must be a corresponding VAR, LIST, CONST, MACRO or CODE
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
         //         ReportError(container,$"{kind} {id} is not one of {{{string.Join(",",Section.ProvidedElementImplementors.Select(type => type.PhaseName))}}}");
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
         if (MissingDefinitions(proc,proc.group)) return;

         bool hasEffect = AnalyzeEffect(proc.group);
         if (proc.HasEffect && !hasEffect) {
            AddNote(proc,Note.NoEffect,proc.algorithmType);
            ReportError(section,$"Procedure {proc.AlgorithmName} does not have an effect. Should be {(proc.algorithmType == RW.PREDICATE ? RW.TEST : RW.FUNCTION)}?");
         } else if (!proc.HasEffect && hasEffect) {
            AddNote(proc,Note.Defect,proc.algorithmType);
            ReportError(section,$"Procedure {proc.AlgorithmName} has a defect. Should be {(proc.algorithmType == RW.TEST ? RW.PREDICATE : RW.ACTION)}?");
         }

         bool canFail = AnalyzeCanFail(proc.group,section);
         if (proc.CanFail && !canFail) {
            AddNote(proc,Note.CannotFail,proc.algorithmType);
            //AddNote(proc,new(NoteType.Warning,"Test warning",108));
            //AddNote(proc,new(NoteType.Info,"Test info",208));
            ReportError(section,$"Procedure {proc.AlgorithmName} cannot fail. Should be {(proc.algorithmType==RW.TEST?RW.FUNCTION:RW.ACTION)}?");
         } else if (!proc.CanFail && canFail) {
            AddNote(proc,Note.CanFail,proc.algorithmType);
            ReportError(section,$"Procedure {proc.AlgorithmName} can fail. Should be {(proc.algorithmType == RW.FUNCTION ? RW.TEST : RW.PREDICATE)}?");
         }
      }

      private bool MissingDefinitions(Procedure proc,Group group) {
         bool missingDefinitions = false;
         foreach (Alternative alt in group.alternatives) {
            missingDefinitions = MissingDefintions(proc,alt) || missingDefinitions;
         }
         return missingDefinitions;
      }

      private bool MissingDefintions(Procedure proc,Alternative alt) {
         bool missingDefinitions = false;
         foreach (Call call in alt.calls) {
            missingDefinitions = missingCallDefinition(call) || missingDefinitions;
         }
         if (alt.lastCall.type == LCT.Group) {
            missingDefinitions = MissingDefinitions(proc, alt.lastCall.group!) || missingDefinitions;
         } else if (alt.lastCall.type == LCT.Standard) {
            missingDefinitions = missingCallDefinition(alt.lastCall.call!);
         }
         return missingDefinitions;

         bool missingCallDefinition(Call call) {
            if (!call.IsBuiltin) {
               if (call.Called is null) {
                  proc.AddNote(PhaseName, Note.UndeclaredAlgorithmCall, call.id);
                  return true;
               }
            }
            return false;
         }
      }

      /// <summary>
      /// Analyze the effect of a group of alternatives.
      /// If any call in any alternative has an effect then the group has an effect.
      /// </summary>
      /// <param name="group"></param>
      /// 
      /// <returns></returns>
      private bool AnalyzeEffect(Group group) {
         bool effect = false;
         foreach (Alternative alt in group.alternatives) effect |= AnalyzeEffect(alt);
         return effect;
      }
      private bool AnalyzeCanFail(Group group,Section section) {
         foreach (Alternative alternative in group.alternatives) {
            if (alternative.lastCall.type == LCT.Fail) return true;
            if (alternative.lastCall.type == LCT.Group && AnalyzeCanFail(alternative.lastCall.group!,section)) return true;
         }
         LastCall lc = group.alternatives.Last().lastCall;
         return lc.type == LCT.Standard && lc.call!.CanFail;   // Group and Fail already handled above.
      }

      /// <summary>
      /// Analyze the effect of an alternative.
      /// If any call in the alternative, or its ending group if any has an effect then the alternative has an effect.
      /// </summary>
      /// <param name="alt"></param>
      /// 
      /// <returns></returns>
      /// <exception cref="Exception"></exception>
      private bool AnalyzeEffect(Alternative alt) {
         foreach (Call call in alt.calls) {
            if (CallhasEffect(call)) return true;
         }
         if (alt.lastCall.type == LastCallType.Standard) {
            return CallhasEffect(alt.lastCall.call!);
         } else if (alt.lastCall.type == LastCallType.Group) {
            return AnalyzeEffect(alt.lastCall.group!);
         } else {
            return false;
         }
      }
      /// <summary>
      /// A call has an effect if the algorithm it invokes has an effect.
      /// </summary>
      static bool CallhasEffect(Call call) => !call.IsBuiltin && call.Called.HasEffect;      
   }
}
