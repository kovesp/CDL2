// Ignore Spelling: CDL

using Microsoft.VisualBasic;

using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

using static CDL2v1.Logger;


namespace CDL2v1 {
   /// <summary>
   /// Semantic Analyzer for CDL2.
   /// - An algorithm has an effect if it modifies the state of the system. This could be a variable, or a list or any other thing that changes
   ///   things (this can only happen via macros).
   /// - Modifying affixes is not considered an effect.
   /// 
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
   /// 2. Verify that there are no duplicate Declarations.
   /// 3. Verify the above rules.
   /// </summary>
   [Serializable]
   public class SemanticAnalyzer : CompilationPhase {
      public SemanticAnalyzer(CDL2 compiler) : base(compiler) { }

      /// <summary>
      /// Maps all identifiers exported by the modules in the program to the exporting module.
      /// </summary>
      private readonly Dictionary<ID, IExportable> Exports = [];

      /// <summary>
      /// Analyze the given program.
      /// </summary>
      /// <param name="MainProgram"></param>
      internal void Analyze(Program MainProgram) {
         //foreach (Program program in Database.Instance.Programs.Values) {
         //   AnalyzeProgram(program);
         //}

         AnalyzeMainProgram(MainProgram);

         foreach (Module module in Database.Instance.Modules.Values) {
            AnalyzeModule(MainProgram, module);
         }
         // Now verify that each import has a corresponding export and that the specs match.
         // Notice that the affix names don't have to match, but the directions do.
         // If resolved, add the object to the resolved imports of the module.
         foreach (Module module in MainProgram.Modules) {
            module.resolvedImports.Clear();  // valid only for the current run of the Semantic Analyzer.
            foreach (IImportable importedElem in module.imports.Values) {
               DeclaredCDL2Object imported = (DeclaredCDL2Object)importedElem;
               if (Exports.TryGetValue(imported.Id,out IExportable? exportedElem)) {
                  DeclaredCDL2Object exported = (DeclaredCDL2Object)exportedElem;
                  
                  bool resolved = true;
                  // Check that the import mathces the export
                  if (imported is ImportedConst _ && exported is Algorithm alg) {
                     AddNote(MainProgram,Note.ImpexMismatch, imported, exported, $"CONST vs. {alg.algorithmType}");
                     resolved = false;
                  } else if (imported is ImportedAlgorithm impalg && exported is Algorithm expalg) {
                     if (impalg.affixes.Count != expalg.affixes.Count) {
                        AddNote(MainProgram, Note.ImpexMismatch, imported, exported,"Affix count mismatch");
                        resolved = false;
                     } else {
                        for (int i = 0 ; i < impalg.affixes.Count ; i++) {
                           if (impalg.affixes[i].affixDir != expalg.affixes[i].affixDir) {
                              AddNote(MainProgram, Note.ImpexMismatch, imported, exported, $"Affix direction mismatch, {impalg.affixes[i]} vs. {expalg.affixes[i]}");
                              resolved = false;
                           }
                        }
                     }
                  } else {
                     AddNote(MainProgram, Note.ImpexMismatch, imported, exported, $"{((Algorithm)imported).algorithmType} vs. CONST");
                     resolved = false;
                  }
                  if (resolved) module.resolvedImports[exported.Id] = (exported as IImportable)!;
               } else {
                  AddNote(MainProgram, Note.MissingImport, imported);
               }
            }
         }
      }

      private void AnalyzeMainProgram(Program mainProgram) {
         Log(0,$"Analyzing MAIN {mainProgram.ContainerName}");
      }

      public void AnalyzeUnused(Program mainProgram, Reachable Reachable) {
         foreach (ICDL2Object obj in Reachable.AllObjects) {
            if (Reachable.Objects.Contains(obj)) {
               obj.Notes.Remove(Note.UnreferenceObject);
            } else if (obj is NamedElement elem) {
               AddNote(elem, new Note(Note.UnreferenceObject,PhaseName,obj.Parent));
            }
         }
      }

      private void AnalyzeProgram(Program program) {
         Log(0,$"Analyzing {program.ContainerName}");
      }

      /// <summary>
      /// Analyze a module.
      /// Notice that a check is made to ensure that all objects are exported from a single module. As a result, the exports table can be used to resolve imports.
      /// </summary>
      /// <param name="prog"></param>
      /// <param name="module"></param>
      private void AnalyzeModule(Program prog,Module module) {
         Log(1,$"Analyzing {module.ContainerName}");

         foreach (Layer layer in module.Children.Cast<Layer>()) {
            AnalyzeLayer(layer);
         }
      }

      private void AnalyzeLayer(Layer layer) {
         Log(1,$"Analyzing {layer.ContainerName}");

         foreach (Section section in layer.Children.Cast<Section>()) {
            AnalyzeSection(section);
         }
         // At thiis point Visible contains all the objects that are visible in the layer, i.e., that have been extended in this layers sections or abstracted from below.
         // We can no check to see if everything invoked in this layer is in the visible dictionary. Note that there may be imported objects. Those will be linked up with
         // exports when the program is analyzed.
         foreach (Section section in layer.Children.Cast<Section>()) {
            foreach (ID elemid in section.inv) {
               if (!layer.Visible.ContainsKey(elemid)) {
                  AddNote(section, Note.MissingInvoke, elemid, layer);
               }
            }
         }
      }

      private void AnalyzeSection(Section section) {
         Log(1,$"Analyzing {section.ContainerName}");
         Log(2,$"Analyzing interfaces");
         AnalyzeProvidedInterfaces(section, RW.EXT,   section.ext,   section.Layer!.Visible);
         AnalyzeProvidedInterfaces(section, RW.ABSTR, section.abstr, section.Layer?.Successor?.Visible);
         AnalyzeProvidedInterfaces(section, RW.EXPORT,section.export,section.Module!.exports);

         // Invocations are analyzed at the Layer level.
         AnalyzeImports(section);

         foreach (Algorithm algorithm in section.Declarations.Values.Where(obj => obj is Algorithm algorithm).Cast<Algorithm>()) {
            Log(2,$"Analyzing {algorithm.GetType().Name} {algorithm.AlgorithmName}");
            if (algorithm is Procedure procedure) {
               AnalyzeProcedure(procedure,section);
            } else if (algorithm is Macro macro) {
               AnalyzeMacro(macro);
            }
         }
      }

      private void AnalyzeImports(Section section) {
         // Verify that elements in the imports list are specified.
         foreach (ID elemid in section.import) {
            if (section.Declarations.TryGetValue(elemid, out DeclaredCDL2Object? obj)) {
               if (obj is not IImportable) {
                  AddNote(obj, Note.ObjectImportedButHasBody, obj);
               } else {
                  // Add the import to the module's imports so it can be verified later.
                  // The import can appear in multiple sections, but then all specifications must be the same.
                  if (section.Module!.imports.TryGetValue(elemid, out IImportable? imported)) {
                     CheckSameImportSpec(obj, obj, imported);
                  } else {
                     section.Module.imports[elemid] = (IImportable)obj;
                  }
               }
            } else {
               AddNote(section,Note.MissingImportSpec, elemid,section);
            }
         }
         // Verify that elements that have no body are imported.
         foreach (DeclaredCDL2Object obj in section.Declarations.Values.OfType<IImportable>().Cast<DeclaredCDL2Object>()) {
            if (!section.import.Contains(obj.Id)) {
               AddNote(obj, Note.ObjectNotImported, obj);
            }
         }
      }

      /// <summary>
      /// Compare obj1 to obj2.
      /// If they are both imported constants return true.
      /// If they are both imported algorithms, then their affix counts ahd directions must match.
      /// If there is any mismatch attach an apropriate note or notes to the first object
      /// </summary>
      /// <param name="obj1"></param>
      /// <param name="obj2"></param>
      /// <returns></returns>
      private void  CheckSameImportSpec(NamedElement problemObject, DeclaredCDL2Object obj1, IImportable obj2) {
         if (obj1 is ImportedConst && obj2 is ImportedConst) {
         } else if (obj1 is ImportedAlgorithm alg1 && obj2 is ImportedAlgorithm alg2) {
            if (alg1.affixes.Count != alg2.affixes.Count) {
               AddNote(problemObject, Note.ImpexMismatch, obj1, obj2, "Affix count mismatch");
            } else {
               for (int i = 0 ; i < alg1.affixes.Count ; i++) {
                  if (alg1.affixes[i].affixDir != alg2.affixes[i].affixDir) {
                     AddNote(problemObject, Note.ImpexMismatch, alg1, alg2, $"Affix direction mismatch, {alg1.affixes[i]} vs. {alg2.affixes[i]}");
                  }
               }
            }
         } else {
            AddNote(problemObject, Note.ImpexMismatch, obj1, obj2, "type mismatch, Agorithm vs. CONST");
         }
      }

      /// <summary>
      /// Verify that the provied interfaces are valid within the section.
      ///  -- No duplications: uniqeness is already guaranteed by the collection being a interfaceElements.
      ///  -- Each item in the list is declared in the same section and is a Const or an Algorithm
      ///  -- Does not already occur in the providables, which will be
      ///     -- The current layer's Visible dictionary for kind = EXT
      ///     -- The successor layer's Visible dictionary for kind = ABSTR. In this case it may be null if the layer is the last one.
      ///        if null generate a warning.
      ///     -- The module's exprots dictionary for kind = EXPORT
      /// </summary>
      /// <param name="section"></param>
      /// <param name="kind"></param>
      /// <param name="interfaceElements"></param>
      /// <param name="providables"></param>
      private void AnalyzeProvidedInterfaces(Section section, RW kind, Set<ID> interfaceElements, Dictionary<ID, IProvidable>? providables) {
         if (providables == null && interfaceElements.Count > 0) AddNote(section, Note.AbstractionsInTopLayer);
         foreach (ID elemId in interfaceElements) {
            if (section.Declarations.TryGetValue(elemId,out DeclaredCDL2Object? decl)) {
               if (providables is not null) {
                  if (providables.TryGetValue(elemId, out IProvidable? prov)) {
                     AddNote(section, Note.DuplicateInterfaceElement, elemId, kind, section,prov.Section!);
                  } else if (decl is IProvidable providable) {
                     providables[elemId] = providable;
                  } else {
                     AddNote(section, Note.InterfaceElementNotProvidable, elemId, kind, decl.TypeShortName);
                  }
               }
            } else {
               AddNote(section, Note.InterfaceElementMissing, elemId, kind);
            }
         }
      }

      private static void ReportError(Container unit,string message) => Logger.ReportError($"{unit.ContainerName}: {message}");

      private void AnalyzeMacro(Macro macro) {
      }
      private class DataFlowInfo(Procedure proc) {
         private readonly Set<Affix> readableAffixes     = proc.affixes.Where(affix => affix.IsInput).ToSet();
         private readonly Set<Local> readableLocals      = [];
         private readonly Set<Affix> writableAffixes     = proc.affixes.Where(affix => affix.IsOutput).ToSet();
         private readonly Set<Local> writableLocals      = [..proc.locals];
         private readonly Set<Affix> neverWrittenAffixes = proc.affixes.Where(affix => affix.IsOutputOnly).ToSet();
         private readonly Set<Local> neverWrittenLocals  = [..proc.locals ];

         public bool Readable(Affix affix)       => readableAffixes.Contains(affix);
         public bool Readable(Local local)       => readableLocals.Contains(local);
         public bool Writable(Affix affix)       => writableAffixes.Contains(affix);
         public bool Writable(Local local)       => writableLocals.Contains(local);
         public bool Unreadable(Affix affix)     => !readableAffixes.Contains(affix);
         public bool Unreadable(Local local)     => !readableLocals.Contains(local);
         public bool Unwritable(Affix affix)     => !writableAffixes.Contains(affix);
         public bool Unwritable(Local local)     => !writableLocals.Contains(local);
         public bool NeverWritten(Affix affix)   => neverWrittenAffixes.Contains(affix);
         public bool NeverWritten(Local local)   => neverWrittenLocals.Contains(local);
         public void MakeReadable(Affix affix)   { readableAffixes.Add(affix); neverWrittenAffixes.Remove(affix); }
         public void MakeReadable(Local local)   { readableLocals.Add(local);  neverWrittenLocals.Remove(local); }
         public void MakeWritable(Affix affix)   => writableAffixes.Add(affix);
         public void MakeWritable(Local local)   => writableLocals.Add(local);
         public void MakeUnreadable(Affix affix) => readableAffixes.Remove(affix);
         public void MakeUnreadable(Local local) => readableLocals.Remove(local);
         public void MakeUnwritable(Affix affix) => writableAffixes.Remove(affix);
         public void MakeUnwritable(Local local) => writableLocals.Remove(local);
      }
      private void AnalyzeProcedure(Procedure proc,Section section) {
         DataFlowInfo info = new(proc);
         if (AnalyzeGroup(proc, proc.group, info)) return;

         bool hasEffect = AnalyzeEffect(proc.group);
         if (proc.HasEffect && !hasEffect) {
            AddNote(proc,Note.NoEffect,proc.algorithmType);
            ReportError(section,$"Procedure {proc.AlgorithmName} does not have an effect. Should be {(proc.algorithmType == RW.PREDICATE ? RW.TEST : RW.FUNCTION)}?");
         } else if (!proc.HasEffect && hasEffect) {
            AddNote(proc,Note.Defect,proc.algorithmType);
            ReportError(section,$"Procedure {proc.AlgorithmName} has a defect. Should be {(proc.algorithmType == RW.TEST ? RW.PREDICATE : RW.ACTION)}?");
         }

         if (! proc.IsConditionalCompilation()) {
            bool canFail = AnalyzeCanFail(proc.group, section);
            if (proc.CanFail && !canFail) {
               AddNote(proc, Note.CannotFail, proc.algorithmType);
               //AddNote(proc,new(NoteType.Warning,"Test warning",108));
               //AddNote(proc,new(NoteType.Info,"Test info",208));
               ReportError(section, $"Procedure {proc.AlgorithmName} cannot fail. Should be {(proc.algorithmType == RW.TEST ? RW.FUNCTION : RW.ACTION)}?");
            } else if (!proc.CanFail && canFail) {
               AddNote(proc, Note.CanFail, proc.algorithmType);
               ReportError(section, $"Procedure {proc.AlgorithmName} can fail. Should be {(proc.algorithmType == RW.FUNCTION ? RW.TEST : RW.PREDICATE)}?");
            }
         }
      }

      private bool AnalyzeGroup(Procedure proc, Group group, DataFlowInfo info) {
         bool missingDefinitions = false;
         foreach (Alternative alt in group.alternatives) {
            missingDefinitions = AnalyzeAlternative(proc, alt, info) || missingDefinitions;
         }
         return missingDefinitions;
      }

      private bool AnalyzeAlternative(Procedure proc, Alternative alt, DataFlowInfo info) {
         bool missingDefinitions = false;
         foreach (Call call in alt.calls) {
            missingDefinitions = AnalyzeCall(call, proc, info) || missingDefinitions;            
         }
         if (alt.lastCall.type == LCT.Group) {
            missingDefinitions = AnalyzeGroup(proc, alt.lastCall.group!, info) || missingDefinitions;
         } else if (alt.lastCall.type == LCT.Standard) {
            missingDefinitions = AnalyzeCall(alt.lastCall.call!, proc, info);
         }
         return missingDefinitions;

         bool AnalyzeCall(Call call, Procedure proc, DataFlowInfo info) {
            if (!call.IsBuiltin) {
               if (call.Called is null) {
                  proc.AddNote(PhaseName, Note.UndeclaredAlgorithmCall, call.id);
                  return true;
               } else if (call.Called.affixes.Count != call.args.Count) {
                  proc.AddNote(PhaseName, Note.ArgumentCountMismatch, call.id, call.args.Count, call.Called.affixes.Count);
                  return true;
               } else if (call.args.Count == 0) {
                  return false;
               } else {
                  List<Affix> affix = call.Called.affixes;
                  List<IActualArg> arg = call.args;
                  for (int i = 0; i < call.args.Count; i++) {
                     if (arg[i] is ID id) {
                        // ID that was not resolved during parsing
                        if (proc.Section.TryGetDeclaration(id, out DeclaredCDL2Object? obj)) {                           
                           switch (obj) {
                              case Var var:
                                 arg[i] = var; break;
                              case Const c:
                                 arg[i] = c; break;
                              default:
                                 proc.AddNote(PhaseName, Note.InvalidArgumentType, arg[i],call);
                                 break;
                           }               
                        } else {
                           proc.AddNote(PhaseName, Note.UnresolvedArgument, arg[i],call);
                           return true; // No point in continuing
                        }
                     }

                     if (affix[i].IsString) {
                        // The actual argument must be a constant, a string or a string affix of the containing procedure.
                        switch (arg[i]) {
                           case Const _:
                           case Affix stringArg when stringArg.IsString:
                           case STRING _:
                              break;
                           default:
                              proc.AddNote(PhaseName, Note.InvalidStringArg, arg[i], call);
                              break;
                        }
                     } else if (affix[i].IsInputOnly) {
                        // The actual argument must be a constant, a variable, an input or transput affix of the containing procedure,
                        // or a local or output affix that has already received a value.
                        switch (arg[i]) {
                           case Const _:
                           case Var _:
                           case Affix inputArg when inputArg.IsInput:   // Includes transput
                              break;
                           case Affix outputArg when outputArg.IsOutputOnly:
                              if (info.NeverWritten(outputArg)) proc.AddNote(PhaseName, Note.OutputAffixNotAssigned, outputArg.Id,call);
                              break;
                           case Local local:
                              if (info.NeverWritten(local)) proc.AddNote(PhaseName, Note.LocalNotAssigned, local, call);
                              break;
                           default:
                              proc.AddNote(PhaseName, Note.InvalidInputArg, arg[i], call);
                              break;
                        }
                     } else if (affix[i].IsOutputOnly) {
                        // The actual argument must be a variable, a local or an affix (output or transput) of the containing procedure.
                        // The local or affix must have been read since it was last written (this is a warning).
                        switch (arg[i]) {
                           case Var _:
                              break;
                           case Affix outputArg when outputArg.IsOutput:   // Includes transput
                              if (info.Unwritable(outputArg)) proc.AddNote(PhaseName, Note.OutputAffixOverwritten, outputArg, call);
                              info.MakeReadable(outputArg);
                              info.MakeUnwritable(outputArg);
                              break;
                           case Local local:
                              if (info.Unwritable(local)) proc.AddNote(PhaseName, Note.LocalOverwritten, local, call);
                              info.MakeReadable(local);
                              info.MakeUnwritable(local);
                              break;
                           default:
                              proc.AddNote(PhaseName, Note.InvalidOutputArg, arg[i], call);
                              break;
                        }
                     } else {
                        Debug.Assert(affix[i].IsTransput, "Transput affix expected");
                        // The actual argument must be a variable, a transput affix or a local or an output affix which has already been assigned a value of the containing procedure.
                        switch (arg[i]) {
                           case Var _:
                              break;
                           case Affix transputArg when transputArg.IsTransput:
                              info.MakeReadable(transputArg);
                              info.MakeUnwritable(transputArg);
                              break;
                           case Affix outputArg when outputArg.IsOutputOnly:
                              // TODO: Differentiate between output never assigned and output assigned but not read. Same for local. But how? Another interfaceElements in info?
                              if (info.NeverWritten(outputArg)) proc.AddNote(PhaseName, Note.OutputAffixNotAssigned, outputArg.Id,call);
                              else if (info.Unreadable(outputArg)) proc.AddNote(PhaseName, Note.OutputAffixOverwritten, outputArg,call);
                              info.MakeReadable(outputArg);
                              info.MakeUnwritable(outputArg);
                              break;
                           case Local local:
                              if (info.Unreadable(local)) proc.AddNote(PhaseName, Note.LocalNotAssigned, local, call);
                              if (info.Unreadable(local)) proc.AddNote(PhaseName, Note.LocalOverwritten, local,call);
                              info.MakeReadable(local);
                              info.MakeUnwritable(local);
                              break;
                           default:
                              proc.AddNote(PhaseName, Note.InvalidTransputArg, arg[i], call);
                              break;
                        }
                     }
                  }
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
      static bool CallhasEffect(Call call) => !call.IsBuiltin && (call.Called?.HasEffect ?? false);      
   }
}
