// <auto-gen>
//=======================================================================
// <copyright file="SemanticAnalyzer.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-02-13</creation-date>
// 
// <summary>
//   Performs static semantic analysis on the CDL2 source tree.
//   responsible for verifying identifiers and connecting those mentioned in the section interfaces.
// </summary>
// <attribution>
//   This file is part of the clean room reimplementation of the
//      CDL2 Compiler
//      CDL2 Laboratory
//      CDL2 Target Code Generators
//
//    Based on original work on CDL and CDL2 led by C. H. A. Koster
//    and the CDL2 team at the Universities of Berlin, Germany and
//    Nijmegen, The Netherlands.
//
//    The CDL2 Laboratory was the work of Epsilon GmbH, Berlin.
//    H. M. Stahl, H. Feuerhahn, JP. Dehotay, B. Böhringer
//    (and others I don't remember ... sorry).
//
//    This project is not affiliated with the original CDL2 project.
// </attribution>
//=======================================================================
// </auto-gen>


using System.Diagnostics;

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
   /// - Only algorithms and CONSTs may appear in the interface (ABSTR, EXT, INV, IMPORT, EXPORT) lists of sections.
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
   /// 1. Verify that all imports are consistent with exports in the modules in the program.
   /// 2. Resolve all invocations to their corresponding extensions and abstractions.
   /// 3. Perform local analysis to verify that
   ///    a. References to other constants in a constants's elements are resolved.
   ///    b. References in a macro's elements to constants, variables, lists, affixes and locals are resolved.
   ///    subject. References in procedures to constants, variables, affixes and locals are resolved.
   ///    d. Procedures are consistent with respect to their declared type and dataflow rules are kept.
   /// </summary>
   public class SemanticAnalyzer(CDL2 compiler) : CompilationPhase(compiler) {
      /// <summary>
      /// Analyze the given program.
      /// </summary>
      /// <param name="program"></param>
      internal void Analyze(Program program) {
         Log(1,$"Analyzing {program}");
         //phase 1
         AnalyzeProgram(program);
         // Phase 2
         AnalyzeProgramInterfaces(program);
         // Phase 3
         AnalyzeProgramImportsAndExports(program);
         // Pahse 4
         foreach (Module module in program.Modules) {
            AnalyzeModule(module);
         }
      }

      /// <summary>
      /// Analyze the imports of the given program.
      /// Ensure that all imports used in the modules are found and are consistent with the exports.
      /// </summary>
      /// <param name="program"></param>
      internal void AnalyzeProgramImportsAndExports(Program program) {
         Log(1,$"Analyzing {program} imports and exports");
         // Collect all the exports from the modules in the program.
         program.Exports.Clear();
         foreach (Module module in program.Modules) {
            AnalyzeModuleExports(module);
            foreach (IExportable export in module.exports.Values.Cast<IExportable>()) {
               if (export.IsImported) {
                  AddNote(program,Note.ImportIsExported,export);
               } else {
                  program.Exports[export.Id] = export;
               }
            }
         }
         // Now verify that each import has a corresponding export and that the specs match.
         foreach (Module module in program.Modules) {
            AnalyzeModuleImportsAndExports(program,module);
         }
      }

      public void AnalyzeModuleImportsAndExports(Program? program,Module module) {
         Log(2,$"Analyzing {module} import and export resolution");
         // First collect all the imports in the sections into the imports table of the module.
         // While doing this check for consistency in case an object is imported ínto multiple sections.
         foreach (Section section in module.Sections) {
            foreach (ID elemid in section.Interfaces[InterfaceTypes.Import]) {
               if (section.Declarations.TryGetValue(elemid,out CDL2Object? obj)) {
                  if (obj is IImportable imported) {
                     if (module.imports.TryGetValue(elemid,out IImportable? importedObj)) {
                        CheckImportConsistency(obj,obj,(CDL2Object)importedObj);
                     } else {
                        module.imports[imported.Id] = imported;
                     }
                  } else {
                     AddNote(section,Note.InterfaceElementNotProvidable,obj!.Id,RW.IMPORT,obj.TypeShortName);
                  }
               } else {
                  AddNote(section,Note.InterfaceElementMissing,elemid,RW.IMPORT);
               }
            }
         }
         if (program != null) {
            // Now check that all the imports are in the exports table of the program and are consistent with those exports.
            // Also insert the target of the import into the resolvedImports table of the module
            foreach (CDL2Object imported in module.imports.Values.Cast<CDL2Object>()) {
               if (program.Exports.TryGetValue(imported.Id,out IExportable? exported)) {
                  CheckImportConsistency(imported,imported,(CDL2Object)exported);
                  module.resolvedImports[imported.Id] = (IImportable)exported;
               } else {
                  AddNote(program,Note.MissingImport,imported);
               }
            }
         }
         //AnalyzeModule(module);
      }

      /// <summary>
      /// Construct the exports table for the given module and verify that each object is exported only once.
      /// </summary>
      /// <param name="module"></param>
      private void AnalyzeModuleExports(Module module) {
         Log(2,$"Analyzing module {module} exports");
         foreach (Section section in module.Sections) AnalyzeProvidedInterfaces(section,RW.EXPORT,section.Interfaces[InterfaceTypes.Export],module.exports,logDepth: 3);
      }
      /// <summary>
      /// Verify the consistency of interface declarations.
      /// </summary>
      /// <param name="program"></param>
      private void AnalyzeProgramInterfaces(Program program) {
         foreach (Module module in program.Modules) {
            Log(1,$"Analyzing internal interfaces of {module}");
            // Construct the Visible table of each layer in the module
            foreach (Section section in module.Sections) {
               AnalyzeProvidedInterfaces(section,RW.EXT,section.Interfaces[InterfaceTypes.Ext],section.Layer!.Visible,logDepth: 2);
               AnalyzeProvidedInterfaces(section,RW.ABSTR,section.Interfaces[InterfaceTypes.Abstr],section.Layer?.Successor?.Visible,logDepth: 2);
            }
            // At this point Visible of each layer contains all the objects that are visible in the layer, i.e., that have been extended in this layer's sections
            // or abstracted from below.
            // We can now check to see if everything invoked in this layer is in the Visible dictionary. Note that there may be imported objects. Those will be linked up with
            // exports prior to code generation.
            // Also ensures that the actual element IDs are in the inv list. (Should not stritly be necessary, but just in case.)
            foreach (Layer layer in module.Layers) {
               foreach (Section section in layer.Sections) {
                  Log(2,$"Analyzing section {section} {RW.INV}");
                  Set<ID> invs = [.. section.Interfaces[InterfaceTypes.Inv]];
                  section.Interfaces[InterfaceTypes.Inv].Clear();
                  foreach (ID elemid in invs) {
                     if (layer.Visible.TryGetValue(elemid,out IProvidable? elem)) {
                        section.Interfaces[InterfaceTypes.Inv].Add(elem.Id);
                     } else {
                        AddNote(section,Note.MissingInvoke,elemid,layer);
                     }
                  }
               }
            }
         }
      }

      /// <summary>
      /// Find objects that are declared but not used and mark them as such with an Info note.
      /// Do not do this for any object that is exported from any module. 
      /// </summary>
      /// <param name="mainProgram"></param>
      /// <param name="Reachable"></param>
      public void AnalyzeUnused(Program mainProgram,Reachable Reachable) {
         int unused = 0;
         SortedList<string,CDL2Object> unusedObjects = [];
         foreach (CDL2Object obj in Reachable.AllObjects) {
            if (Reachable.Objects.Contains(obj) /*|| mainProgram.Exports.ContainsKey(obj.Id)*/) {
               obj.Notes.Remove(Note.UnreferenceObject);
            } else {
               AddNote(obj,new Note(Note.UnreferenceObject,PhaseName,obj.ParentElement<CDL2Object>()!));
               unused++;
               unusedObjects.Add(obj.Id.Name,obj);
            }
         }
         Log(1,$"There are {unused.Plural("unused object")} in the program");
         if (Settings.AnyVerbosity(4)) foreach (CDL2Object obj in unusedObjects.Values) Log(1,$"  {obj}");
      }

      public void AnalyzeProgram(Program program) {
         IDDictionary<Module> validModules = [];
         Log(1,$"Analyzing module presence of {program}");
         // First verify that all modules in the parts list are present in the database.
         // Modules are found by name, and added to valid modules by their ID. Note that the ID object in Parts may not be the same as in the module itself
         foreach (ID modId in program.Parts) {
            Module? mod;
            if ((mod = Database.Instance.ModuleByName(modId)) is not null) {
               validModules[mod.Id] = mod;
            } else {
               AddNote(program,Note.ModuleNotFound,modId);
            }
         }
         // Now ensure that the parts list contains the actual module IDs.
         program.Parts.Clear();
         foreach (ID modId in validModules.Keys) program.Parts.Add(modId);

         // Verify that all lude references are correct and replace the IDs in the lude with the actual ones.
         foreach (RW ludeType in Container.LudeTypes) {
            Log(1,$"Analyzing program {ludeType}");
            List<ID> progLudeEntries = [.. program.Ludes[ludeType]];
            program.Ludes[ludeType].Clear();
            foreach (ID modId in progLudeEntries) {
               if (validModules.TryGetValue(modId,out Module? mod)) {
                  if (mod.Ludes[ludeType].Count == 0) {
                     AddNote(program,Note.LudeNotFound,RW.MODULE,modId,ludeType);
                  } else {
                     List<ID> modLudeEntries = [.. mod.Ludes[ludeType]];
                     mod.Ludes[ludeType].Clear();
                     foreach (ID lude in modLudeEntries) {
                        // lude should be the name of a section in the module. If the section has a lude of the required type then it
                        // must contain the generated name of a lude procedure.
                        if (mod.TryGetSectionById(lude,out Section? section)) {
                           if (section.Ludes[ludeType].Count == 0) {
                              AddNote(mod,Note.LudeNotFound,RW.SECTION,lude,ludeType);
                           } else {
                              mod.Ludes[ludeType].Add(section.Id);
                           }
                        } else {
                           AddNote(mod,Note.LudeNotFound,RW.SECTION,lude,ludeType);
                        }
                     }
                  }
                  program.Ludes[ludeType].Add(mod.Id);
               } else {
                  AddNote(program,Note.LudeNotFound,RW.MODULE,modId,ludeType);
               }
               // Valid ludes are added to the program's lude table.
            }
         }
      }

      /// <summary>
      /// Analyze a module.
      /// Notice that a check is made to ensure that all objects are exported from a single module. As a result, the exports table can be used to resolve imports.
      /// </summary>
      /// <param name="prog"></param>
      /// <param name="module"></param>
      public void AnalyzeModule(Module module) {
         Log(1,$"Analyzing {module.ContainerName}");
         foreach (Layer layer in module.Layers) {
            AnalyzeLayer(layer);
         }
      }

      private void AnalyzeLayer(Layer layer) {
         Log(2,$"Analyzing {layer.ContainerName}");
         foreach (Section section in layer.Sections) {
            AnalyzeSection(section);
         }
      }

      private void AnalyzeSection(Section section) {
         Log(3,$"Analyzing {section.ContainerName}");

         // Analyze Constants
         Log(4,$"Analyzing constants");
         foreach (Const c in section.Constants) {
            if (c.IsImported) continue; // Imported constants are not analyzed.
            // Ensure that each CONST element in the constant is declared
            Set<ID> resolvedIDs = [];
            foreach (ID elemId in c.elements.OfType<ID>()) {
               resolvedIDs.Add(ResolveIdToDeclaringId<Const,Const>(section,c,elemId,Note.UnresolvedConstElement,Note.InvalidConstElement));
            }
            List<IElement> originalelements = [.. c.elements];
            c.elements.Clear();
            foreach (IElement elem in originalelements) c.elements.Add(elem is ID elemId ? resolvedIDs.GetActualValue(elemId) : elem);
         }

         // Analyze Lists
         Log(4,$"Analyzing Lists");
         foreach (LIST list in section.Lists) {
            list.lwb = ResolveIdToDeclaringId<LIST,Const>(section,list,list.lwb,Note.UnresolvedListBound,Note.InvalidListBound,"lower bound");
            list.upb = ResolveIdToDeclaringId<LIST,Const>(section,list,list.upb,Note.UnresolvedListBound,Note.InvalidListBound,"upper bound");
         }

         // Analyze procedures and macros.
         Log(4,$"Analyzing Algorithms");
         foreach (Algorithm algorithm in section.NonSyntheticAlgorithms) {
            if (algorithm.IsImported) continue; // Imported algorithms are not analyzed.
            Log(5,$"Analyzing {algorithm.GetType().Name} {algorithm.AlgorithmName}");
            if (algorithm is Procedure procedure) {
               AnalyzeProcedure(procedure,section);
            } else if (algorithm is Macro macro) {
               AnalyzeMacro(macro);
            }
         }
      }

      /// <summary>
      /// Check the type of the object with the given ID. Return the declaring ID object if found and of the correct type.
      /// Otherwise just return the id and add an appropriate note to the subject.
      /// </summary>
      /// <typeparam name="S">The type of subject.</typeparam>
      /// <typeparam name="T">The type of the object that id must resolve to</typeparam>
      /// <param name="section"></param>
      /// <param name="subject"></param>
      /// <param name="id"></param>
      /// <param name="extra">Extra information to add to the note.</param>
      /// <returns>
      ///   If the id is unresolved or does not resolve to the required type, return it. 
      ///   Otherwise return the id of the resolved object.
      /// </returns>
      private ID ResolveIdToDeclaringId<S, T>(Section section,S subject,ID id,Note unresolved,Note wrongType,string? extra = null,
            Predicate<CDL2Object>? ensure = null) where S : CDL2Object where T : CDL2Object {
         CDL2Object? resolvedObject = section.GetResolvedObject(id);
         switch (resolvedObject) {
            case null:
               if (extra != null) {
                  AddNote(subject,unresolved,extra,id);
               } else {
                  AddNote(subject,unresolved,id);
               }
               return id;
            case T:
               if (ensure != null && !ensure(resolvedObject)) {
                  if (extra != null) {
                     AddNote(subject,wrongType,extra,resolvedObject);
                  } else {
                     AddNote(subject,wrongType,resolvedObject);
                  }
                  return id;
               }
               return resolvedObject.Id;
            default:
               if (extra != null) {
                  AddNote(subject,wrongType,extra,resolvedObject);
               } else {
                  AddNote(subject,wrongType,resolvedObject);
               }
               return id;
         }
      }

      /// <summary>
      /// Compare obj1 to obj2.
      /// Both are imported when checking consistency between imports in the same module.
      /// One is imported and the other is not when checking consistency between imports and exports.
      /// If they are both constants return true.
      /// If they are both algorithms, then their affix counts ahd directions must match.
      /// If there is any mismatch attach an appropriate note or notes to the first object
      /// </summary>
      /// <param name="problemObject">If there are issues with the spec, attach the note to this object.</param>
      /// <param name="obj1"></param>
      /// <param name="obj2"></param>
      /// <returns></returns>
      private void CheckImportConsistency(NamedElement problemObject,CDL2Object obj1,CDL2Object obj2) {
         if (obj1 is Const && obj2 is Const) {
         } else if (obj1 is Algorithm alg1 && obj2 is Algorithm alg2) {
            if (alg1.Affixes.Count != alg2.Affixes.Count) {
               AddNote(problemObject,Note.ImpexMismatch,obj1,obj2,"Affix count mismatch");
            } else {
               for (int i = 0 ; i < alg1.Affixes.Count ; i++) {
                  if (alg1.Affixes[i].affixDir != alg2.Affixes[i].affixDir) {
                     AddNote(problemObject,Note.ImpexMismatch,alg1,alg2,$"Affix direction mismatch, {alg1.Affixes[i]} vs. {alg2.Affixes[i]}");
                  }
               }
            }
         } else {
            AddNote(problemObject,Note.ImpexMismatch,obj1,obj2,"type mismatch, ALGORITHM vs. CONST");
         }
      }

      /// <summary>
      /// Verify that the provided interfaces are valid within the section.
      ///  -- No duplications: uniqueness is already guaranteed by the collection being of interfaceElements.
      ///  -- Each item in the list is declared in the same section and is a Const or an Algorithm
      ///  -- Does not already occur in the providable-s, which will be
      ///     -- The current layer's Visible dictionary for kind = EXT. Note that in this case the items abstracted from the previous
      ///        layer may already be in there. This is done by sections, but order is not relevant, either way duplicates are detected. 
      ///     -- The successor layer's Visible dictionary for kind = ABSTR. In this case it may be null if the layer is the last one.
      ///        In this case there should be no abstractions in the section, a warning is generated if there are.
      ///     -- The module's exports dictionary for kind = EXPORT
      /// </summary>
      /// <param name="section"></param>
      /// <param name="kind"></param>
      /// <param name="interfaceElements"></param>
      /// <param name="providables"></param>
      private void AnalyzeProvidedInterfaces(Section section,RW kind,SortedSet<ID> interfaceElements,IDDictionary<IProvidable>? providables,int logDepth) {
         Log(logDepth,$"Analyzing section {section} {kind}");
         // Providables will be null in the top layer, hence there should be no abstractions in this section ... add a warning.
         if (providables == null) {
            if (interfaceElements.Count > 0) AddNote(section,Note.AbstractionsInTopLayer);
         } else {
            Set<ID> elems = [.. interfaceElements];
            interfaceElements.Clear();
            foreach (ID elemId in elems) {
               if (section.Declarations.TryGetValue(elemId,out CDL2Object? decl)) {
                  if (providables.TryGetValue(elemId,out IProvidable? prov)) {
                     AddNote(section,Note.DuplicateInterfaceElement,elemId,kind,section,prov.Section!);
                  } else if (decl is IProvidable providable) {
                     interfaceElements.Add(providable.Id);
                     providables[providable.Id] = providable;
                  } else {
                     AddNote(section,Note.InterfaceElementNotProvidable,elemId,kind,decl!.TypeShortName);
                  }
               } else {
                  AddNote(section,Note.InterfaceElementMissing,elemId,kind);
               }
            }
         }
      }

      private static void ReportError(Container unit,string message) => Logger.ReportError($"{unit.ContainerName}: {message}");

      private void AnalyzeMacro(Macro macro) {
         List<IElement> originalelements = [.. macro.elements];
         macro.elements.Clear();
         foreach (IElement elem in originalelements) {
            if (elem is ID id) {
               if (macro.Affixes.TryGetValueWithId(id,out Affix? affix)) {
                  macro.elements.Add(affix!.Id);
               } else if (macro.Locals.TryGetValueWithId(id,out Local? local)) {
                  macro.elements.Add(local!.Id);
               } else {
                  macro.elements.Add(ResolveIdToDeclaringId<Macro,CDL2Object>(macro.Section!,macro,id,
                     Note.UnresolvedMacroElement,Note.InvalidMacroElement,ensure: x => x is IDataElement));
               }
            } else {
               macro.elements.Add(elem);
            }
         }
      }

      private class DataFlowInfo {
         private readonly Procedure proc;
         private readonly Set<Affix> readableAffixes = [];
         private readonly Set<Local> readableLocals = [];
         private readonly Set<Affix> writableAffixes = [];
         private readonly Set<Local> writableLocals = [];
         private readonly Set<Affix> neverWrittenAffixes = [];
         private readonly Set<Local> neverWrittenLocals = [];
         public DataFlowInfo(Procedure proc) { this.proc = proc; Reset(VarSet.all); }

         [Flags]
         public enum VarSet {

            readableAffixes = 1,
            readableLocals = 2,
            writableAffixes = 4,
            writableLocals = 8,
            neverWrittenAffixes = 16,
            neverWrittenLocals = 32,

            all = readableAffixes | readableLocals | writableAffixes | writableLocals | neverWrittenAffixes | neverWrittenLocals,
         }
         public void Reset(VarSet flags) {
            void reset<T>(VarSet flag,Set<T> set,Set<T> values) {
               if ((flags & flag) == flag) {
                  set.Clear();
                  foreach (T value in values) set.Add(value);
               }
            }
            reset(VarSet.readableAffixes,readableAffixes,proc.Affixes.Where(affix => affix.IsInput).ToSet);
            reset(VarSet.readableLocals,readableLocals,[]);
            reset(VarSet.writableAffixes,writableAffixes,proc.Affixes.Where(affix => affix.IsOutput).ToSet);
            reset(VarSet.writableLocals,writableLocals,[.. proc.Locals]);
            reset(VarSet.neverWrittenAffixes,neverWrittenAffixes,proc.Affixes.Where(affix => affix.IsOutputOnly).ToSet);
            reset(VarSet.neverWrittenLocals,neverWrittenLocals,[.. proc.Locals]);
         }

         public bool Readable(Affix affix) => readableAffixes.Contains(affix);
         public bool Readable(Local local) => readableLocals.Contains(local);
         public bool Writable(Affix affix) => writableAffixes.Contains(affix);
         public bool Writable(Local local) => writableLocals.Contains(local);
         public bool Unreadable(Affix affix) => !readableAffixes.Contains(affix);
         public bool Unreadable(Local local) => !readableLocals.Contains(local);
         public bool Unwritable(Affix affix) => !writableAffixes.Contains(affix);
         public bool Unwritable(Local local) => !writableLocals.Contains(local);
         public bool NeverWritten(Affix affix) => neverWrittenAffixes.Contains(affix);
         public bool NeverWritten(Local local) => neverWrittenLocals.Contains(local);
         public void MakeReadable(Affix affix) { readableAffixes.Add(affix); neverWrittenAffixes.Remove(affix); }
         public void MakeReadable(Local local) { readableLocals.Add(local); neverWrittenLocals.Remove(local); }
         public void MakeWritable(Affix affix) => writableAffixes.Add(affix);
         public void MakeWritable(Local local) => writableLocals.Add(local);
         public void MakeUnreadable(Affix affix) => readableAffixes.Remove(affix);
         public void MakeUnreadable(Local local) => readableLocals.Remove(local);
         public void MakeUnwritable(Affix affix) => writableAffixes.Remove(affix);
         public void MakeUnwritable(Local local) => writableLocals.Remove(local);
      }
      private void AnalyzeProcedure(Procedure proc,Section section) {
         DataFlowInfo info = new(proc);
         if (AnalyzeGroup(proc,proc.group,info)) return;

         bool hasEffect = AnalyzeEffect(proc.group);
         if (proc.HasEffect && !hasEffect) {
            AddNote(proc,Note.NoEffect,proc.AlgorithmType);
            ReportError(section,$"Procedure {proc.AlgorithmName} does not have an effect. Should be {(proc.AlgorithmType == RW.PREDICATE ? RW.TEST : RW.FUNCTION)}?");
         } else if (!proc.HasEffect && hasEffect) {
            AddNote(proc,Note.Defect,proc.AlgorithmType);
            ReportError(section,$"Procedure {proc.AlgorithmName} has a defect. Should be {(proc.AlgorithmType == RW.TEST ? RW.PREDICATE : RW.ACTION)}?");
         }

         if (!proc.IsConditionalCompilation()) {
            bool canFail = AnalyzeCanFail(proc.group,section);
            if (proc.CanFail && !canFail) {
               AddNote(proc,Note.CannotFail,proc.AlgorithmType);
               ReportError(section,$"Procedure {proc.AlgorithmName} cannot fail. Should be {(proc.AlgorithmType == RW.TEST ? RW.FUNCTION : RW.ACTION)}?");
            } else if (!proc.CanFail && canFail) {
               AddNote(proc,Note.CanFail,proc.AlgorithmType);
               ReportError(section,$"Procedure {proc.AlgorithmName} can fail. Should be {(proc.AlgorithmType == RW.FUNCTION ? RW.TEST : RW.PREDICATE)}?");
            }
         }
      }

      /// <summary>
      /// Analyze the group.
      /// </summary>
      /// <param name="proc"></param>
      /// <param name="group"></param>
      /// <param name="info"></param>
      /// <returns>true if there are any undefined calls.</returns>
      private bool AnalyzeGroup(Procedure proc,Group group,DataFlowInfo info) {
         bool missingDefinitions = false;
         foreach (Alternative alt in group.Alternatives) {
            missingDefinitions = AnalyzeAlternative(proc,alt,info) || missingDefinitions;
            // info.Reset(DataFlowInfo.VarSet.neverWrittenLocals | DataFlowInfo.VarSet.writableLocals | DataFlowInfo.VarSet.readableLocals);
         }
         return missingDefinitions;
      }

      /// <summary>
      /// Analyze the alternative.
      /// </summary>
      /// <param name="proc"></param>
      /// <param name="alt"></param>
      /// <param name="info"></param>
      /// <returns>true if there are any undefined calls.</returns>
      private bool AnalyzeAlternative(Procedure proc,Alternative alt,DataFlowInfo info) {
         bool missingDefinitions = false;
         foreach (Call call in alt.calls) {
            missingDefinitions = AnalyzeCall(call,proc,info) || missingDefinitions;
         }
         if (alt.lastCall.type == LCT.Group) {
            missingDefinitions = AnalyzeGroup(proc,alt.lastCall.group!,info) || missingDefinitions;
         } else if (alt.lastCall.type == LCT.Standard) {
            missingDefinitions = AnalyzeCall(alt.lastCall.call!,proc,info) || missingDefinitions;
         }
         return missingDefinitions;
      }

      /// <summary>
      /// Analyze the call.
      /// </summary>
      /// <param name="call"></param>
      /// <param name="proc"></param>
      /// <param name="info"></param>
      /// <returns>true if there call is undefined</returns>
      private bool AnalyzeCall(Call call,Procedure proc,DataFlowInfo info) {
         if (!call.IsBuiltin) {
            Algorithm? calledAlg = call.Called;
            if (calledAlg is null) {
               proc.AddNote(PhaseName,Note.UndeclaredAlgorithmCall,call.id);
               return true;
            }
            call.Id = calledAlg.Id; // Normalize the id of the call to the declared algorithm's id.
            if (calledAlg.Affixes.Count != call.Args.Count) {
               proc.AddNote(PhaseName,Note.ArgumentCountMismatch,call.id,call.Args.Count,calledAlg.Affixes.Count);
               return true;
            } else if (call.Args.Count == 0) {
               return false;
            } else {
               List<Affix> affix = calledAlg.Affixes;
               List<IActualArg> args = [.. call.Args];
               for (int i = 0 ; i < args.Count ; i++) {
                  if (args[i] is ID id) {
                     // ID that was not resolved during parsing
                     if (proc.Section!.TryGetDeclaration(id,out CDL2Object? obj)) {
                        switch (obj) {
                           case Var var:
                              args[i] = var; break;
                           case Const c:
                              args[i] = c; break;
                           default:
                              proc.AddNote(PhaseName,Note.InvalidArgumentType,args[i],call);
                              break;
                        }
                     } else {
                        proc.AddNote(PhaseName,Note.UnresolvedArgument,args[i],call);
                        return true; // No point in continuing
                     }
                  }

                  if (affix[i].IsString) {
                     // The actual argument must be a constant, a string or a string affix of the containing procedure.
                     switch (args[i]) {
                        case Const:
                        case STRING:
                        case Affix stringArg when stringArg.IsString:
                        case Local local when local.IsBuiltinResult:
                           break;
                        default:
                           proc.AddNote(PhaseName,Note.InvalidStringArg,args[i],call);
                           break;
                     }
                  } else if (affix[i].IsInputOnly) {
                     // The actual argument must be a constant, a variable, an input or transput affix of the containing procedure,
                     // or a local or output affix that has already received a value.
                     switch (args[i]) {
                        case Const:
                        case Var:
                        case Affix inputArg when inputArg.IsInput:   // Includes transput
                           break;
                        case Affix outputArg when outputArg.IsOutputOnly:
                           if (info.NeverWritten(outputArg)) proc.AddNote(PhaseName,Note.OutputAffixNotAssigned,outputArg.Id,call);
                           break;
                        case Local local:
                           if (info.NeverWritten(local)) proc.AddNote(PhaseName,Note.LocalNotAssigned,local,call);
                           break;
                        default:
                           proc.AddNote(PhaseName,Note.InvalidInputArg,args[i],call);
                           break;
                     }
                  } else if (affix[i].IsOutputOnly) {
                     // The actual argument must be a variable, a local or an affix (output or transput) of the containing procedure.
                     // The local or affix must have been read since it was last written (this is a warning).
                     switch (args[i]) {
                        case Var:
                           break;
                        case Affix outputArg when outputArg.IsOutput:   // Includes transput
                           if (info.Unwritable(outputArg)) proc.AddNote(PhaseName,Note.OutputAffixOverwritten,outputArg,call);
                           info.MakeReadable(outputArg);
                           info.MakeUnwritable(outputArg);
                           break;
                        case Local local:
                           if (!info.NeverWritten(local) && info.Unwritable(local)) proc.AddNote(PhaseName,Note.LocalOverwritten,local,call);
                           info.MakeReadable(local);
                           info.MakeUnwritable(local);
                           break;
                        default:
                           proc.AddNote(PhaseName,Note.InvalidOutputArg,args[i],call);
                           break;
                     }
                  } else {
                     Debug.Assert(affix[i].IsTransput,"Transput affix expected");
                     // The actual argument must be a variable, a transput affix or a local or an output affix which has already been assigned a value of the containing procedure.
                     switch (args[i]) {
                        case Var _:
                           break;
                        case Affix transputArg when transputArg.IsTransput:
                           info.MakeReadable(transputArg);
                           info.MakeUnwritable(transputArg);
                           break;
                        case Affix outputArg when outputArg.IsOutputOnly:
                           // TODO: Differentiate between output never assigned and output assigned but not read. Same for local. But how? Another interfaceElements in info?
                           if (info.NeverWritten(outputArg)) proc.AddNote(PhaseName,Note.OutputAffixNotAssigned,outputArg.Id,call);
                           else if (info.Unreadable(outputArg)) proc.AddNote(PhaseName,Note.OutputAffixOverwritten,outputArg,call);
                           info.MakeReadable(outputArg);
                           info.MakeUnwritable(outputArg);
                           break;
                        case Local local:
                           if (info.NeverWritten(local) || info.Unreadable(local)) proc.AddNote(PhaseName,Note.LocalNotAssigned,local,call);
                           if (info.Unreadable(local)) proc.AddNote(PhaseName,Note.LocalOverwritten,local,call);
                           info.MakeReadable(local);
                           info.MakeUnwritable(local);
                           break;
                        default:
                           proc.AddNote(PhaseName,Note.InvalidTransputArg,args[i],call);
                           break;
                     }
                  }
               }
            }
         }
         return false;
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
         foreach (Alternative alt in group.Alternatives) effect |= AnalyzeEffect(alt);
         return effect;
      }
      private bool AnalyzeCanFail(Group group,Section section) {
         foreach (Alternative alternative in group.Alternatives) {
            if (alternative.lastCall.type == LCT.Fail) return true;
            if (alternative.lastCall.type == LCT.Group && AnalyzeCanFail(alternative.lastCall.group!,section)) return true;
         }
         Alternative lastAlternative = group.Alternatives.Last();
         if (lastAlternative.CanFail) return true;
         LastCall lc = lastAlternative.lastCall;
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

