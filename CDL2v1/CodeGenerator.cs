// Ignore Spelling: CDL

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text;
using System.Windows.Documents;



namespace CDL2v1 {
   /// <summary>
   /// 
   /// </summary>
   /// <param id="cg"></param>
   [Serializable]
   public class CodeGenerator(ICodeGenerator cg) {
      /// <summary>
      /// Target specific code gnerator to use.
      /// </summary>
      private readonly ICodeGenerator cg = cg;

      private static readonly List<RW> ludeTypes = [ RW.PRELUDE,RW.ROOT,RW.POSTLUDE];

      /// <summary>
      /// Used to add the CDL2 code to each generated algorithm.
      /// Will not include the CDL2 comments and Notes associated with CDL2 objects.
      /// </summary>
      private readonly PrettyPrinter sourceCommentPrinter = new(cg.SourceEmitter,includeComments:false);

      /// <summary>
      /// Generate code for the unit and all its modulesWithLudes.
      /// If there is a unit, then use its Ludes. Otherwise, use the Ludes from all modulesWithLudes.
      /// </summary>
      /// <param id="modulesWithLudes"></param>
      /// <param id="Emitter"></param>
      /// <param id="isSeparate"></param>
      public void GenerateCode(Program program, EmitterBase emitter, bool isSeparate = false) {
         Logger.Log(0,$"Collecting objects reachable from {program} ...");
         CollectReachableObjects(program); // Collect all the objects reachable from the program's ludes.
         string CountObjects(Type type) => ReachableObjects.Where(obj => obj.GetType() == type).Count().Plural(type.Name);
         Logger.Log(0, $"{CountObjects(typeof(Const))}, {CountObjects(typeof(Var))}, {CountObjects(typeof(LIST))}, {CountObjects(typeof(Macro))}, {CountObjects(typeof(Procedure))} collected");
         foreach (Var var in ReachableObjects.OfType<Var>()) {
            if (AmbigousVars.Contains(var)) {
               // We know the variable was written to, but we can't tell whther it was read (becasue it was only referenced in an ACTION/PREDICATE macro).
               var.AddNote("CodeGeneration", Note.VariableMayNotHaveBeenRead, var);
               Logger.ReportError($"Variable {var} may not have been read. It was only referenced in an ACTION/PREDICATE macro.");
            } else if (!ReadVars.Contains(var)) {
               // It must have been written, but not read.
               var.AddNote("CodeGeneration", Note.VariableNotRead, var);
               Logger.ReportError($"Variable {var} was written to, but never read.");
            }
         }
         //DumpReachableObjects(program);

         if (!isSeparate) {
            // Generate an integrated program ignoring module boundaries of all objects reachable from the program's ludes.
            cg.GenerateProgramStart(program, emitter);  // Generate the overall scaffolding


            GenerateObjects<Const>(ReachableObjects.OfType<Const>(),                                        GenerateConstant);
            GenerateObjects<Var>(ReachableObjects.OfType<Var>(),                                            GenerateVar);
            GenerateObjects<LIST>(ReachableObjects.OfType<LIST>(),                                          GenerateList);

            GenerateObjects<Macro>(ReachableObjects.OfType<Macro>(),                                        GenerateMacro);
            GenerateObjects<Procedure>(ReachableObjects.OfType<Procedure>().Where(proc=>!proc.IsSynthetic), GenerateProcedure);
            GenerateObjects<Procedure>(ReachableObjects.OfType<Procedure>().Where(proc=> proc.IsSynthetic), GenerateProcedure, "Synthetic Procedure");

            cg.GenerateProgramEnd(program);

            sourceCommentPrinter.Print(program);
            cg.GenerateSourceComment();
            // Place the program's ludes first, so that they are at the top of the generated code.
            cg.GenerateComment("Program Ludes");
            foreach (RW ludeType in ludeTypes) foreach (Module mod in program.Lude(ludeType)) GenerateModuleLude(ludeType, mod, wrapped: false);
         } else {
            cg.GenerateProgramStart(program, emitter);  // Generate the overall scaffolding
            sourceCommentPrinter.Print(program);
            cg.GenerateSourceComment();
            foreach (ID mod in program.Parts) cg.GenerateProgramPart(program, mod, isSeparate);

            GenerateProgramLudes(program);
            cg.GenerateProgramEnd(program);
            foreach (ID mod in program.Parts) GenerateModule(Database.Instance.Modules[mod], isSeparate: true);
         }
      }

      private void GenerateProgramLudes(Program program) {
         foreach (RW ludeType in ludeTypes) {
            IEnumerable<Module> modulesWithLudes = program.Ludes[ludeType].Select(id => Database.Instance.Modules[id]).Where(mod => mod.Ludes[ludeType].Count > 0);
            if (modulesWithLudes.Any()) {
               cg.GenerateProgramLudeStart(ludeType, program);
               foreach (Module module in modulesWithLudes) cg.GenerateProgramLude(ludeType, program, module);
               cg.GenerateProgramLudeEnd(ludeType, program);
            }
         }
      }

      /// <summary>
      /// Generate code for a module. It is up to the specific code generator to determine whether this code goes into a separate file or not.
      /// </summary>
      /// <param id="module"></param>
      private void GenerateModule(Module module, bool isSeparate) {
         void GenerateImpEx(Dictionary<ID, Section> impexList, Action<IProvidedElement> generateImpEx) {
            foreach (ID id in impexList.Keys) {
               if (impexList[id].TryGetLocalDeclaration(id, out ILocalCDL2DataObject? obj) && obj is IProvidedElement impex) {
                  generateImpEx(impex);
               } else {
                  throw new NotImplementedException($"GenerateModule: Import/Export {id} not found in {module}");
               }
            }
         }

         cg.GenerateModuleStart(module, isSeparate);

         cg.GenerateImpExStart(module);
         GenerateImpEx(module.exports, cg.GenerateExport);
         GenerateImpEx(module.imports, cg.GenerateImport);
         cg.GenerateImpExEnd(module);

         foreach (Layer layer in module.Children.Cast<Layer>()) GenerateLayer(layer);

         foreach (RW ludeType in ludeTypes) GenerateModuleLude(ludeType, module, wrapped: true);

         cg.GenerateModuleEnd(module, isSeparate);
      }

      private void GenerateModuleLude(RW ludeType, Module module, bool wrapped) {
         IEnumerable<Section?> SectionsWithLudes = module.Ludes[ludeType].Select(id => module.Section(id)).Where(sec => sec?.Ludes[ludeType].Count > 0) ?? [];
         if (SectionsWithLudes.Any()) {
            cg.GenerateModuleLudeStart(ludeType, module, wrapped: wrapped);
            foreach (Section? section in SectionsWithLudes) cg.GenerateModuleLude(ludeType, module, section!);
            cg.GenerateModuleLudeEnd(ludeType, module, wrapped: wrapped);
         }
      }

      /// <summary> 
      /// Generate proc for a layer. Typically there is no target proc associated with this.
      /// </summary>
      /// <param id="layer"></param>
      private void GenerateLayer(Layer layer) {
         cg.GenerateLayerStart(layer);
         foreach (Section section in layer.Children.Cast<Section>()) GenerateSection(section);
         cg.GenerateLayerEnd(layer);
      }

      /// <summary>
      /// Generate a container. Again, there will likely be no target proc associated with a container itself.
      /// So generate proc for each routine and for the Ludes.
      /// A lude is just proc with a special id
      /// </summary>
      /// <param id="container"></param>
      private void GenerateSection(Section section) {
         cg.GenerateSectionStart(section);
         GenerateObjects<Const>(section.Constants,                   GenerateConstant);
         GenerateObjects<Var>(section.Variables,                     GenerateVar);
         GenerateObjects<LIST>(section.Lists,                        GenerateList);

         GenerateObjects<Macro>(section.Macros,                      GenerateMacro);
         GenerateObjects<Procedure>(section.NonSyntheticProcedures,  GenerateProcedure);
         GenerateObjects<Procedure>(section.SyntheticProcedures,     GenerateProcedure, "Synthetic Procedure");

         cg.GenerateSectionEnd(section);
      }

      private void GenerateList(LIST list, int maxNameLength) {
         Section section = (Section)list.Parent!;
         if (section.TryGetDeclaration(list.lwb, out Const? lwb) && section.TryGetDeclaration(list.upb, out Const? upb)) {
            cg.GenerateList(list, lwb!, upb!);
         } else {
            throw new NotImplementedException($"GenerateSection: Could not find lower or upper bound for {list}");
         }
      }

      private void GenerateVar(Var v,int maxNameLength) => cg.GenerateVar(v);

      private void GenerateObjects<T>(IEnumerable<NamedElement> items, Action<T,int> generate, string? specialType = null) where T : NamedElement {
         if (items.Any()) {
            int maxNameLength = items.Select(item=>item.id.InternalName.Length).Max();
            cg.GenerateObjectSectionStart<T>(items, specialType ?? typeof(T).Name);
            foreach (T item in items) generate(item,maxNameLength);
            cg.GenerateObjectSectionEnd<T>(items, typeof(T).Name);
         }
      }

      private void GenerateConstant(Const constant,int maxNameLength) {
         Section section = (Section)constant.Parent!;
         cg.GenerateConstantStart(constant);
         foreach (IConstElement elem in constant.elements) {
            switch (elem) {
               case INT i: cg.GenerateConstElementInt(i.value); break;
               case FLOAT f: cg.GenerateConstElementFloat(f.value); break;
               case STRING s: cg.GenerateConstElementString(s.value); break;
               case ID id:
                  if (section.TryGetDeclaration(id,out Const? c)) {
                     cg.GenerateConstElementConst(c!);
                  } else {
                     throw new NotImplementedException($"GenerateSection: Reference to wrong element type ");
                  }
                  break;
            }
         }
         cg.GenerateConstantEnd(constant);
      }

      private void GenerateMacro(Macro macro,int _) {
         Section section = (Section)macro.Parent!;
         IEnumerable<Var> variables = macro.GetReferencedVariables();
         cg.GenerateMacroStart(macro);
         GenerateAlgorithmHeader(macro,variables);

         cg.GenerateMacroBodyStart(macro);
         bool first = true;
         foreach (IMacroElement elem in macro.elements) {
            switch (elem) {
               case INT i: cg.GenerateMacroElementInt(i.value); break;
               case FLOAT f: cg.GenerateMacroElementFloat(f.value); break;
               case STRING s: cg.GenerateMacroElementString(s.value, macro.CanFail, first); break;
               case ID id:
                  // This should be a reference to a Const, Var or List, so check which one
                  if (section.TryGetDeclaration(id,out ILocalCDL2DataObject? obj)) {
                     switch (obj) {
                        case Const c: cg.GenerateMacroElementConst(c); break;
                        case Var v:   cg.GenerateMacroElementVar(v, macro.CanFail);   break;
                        case LIST l:  cg.GenerateMacroElementList(l);  break;
                        default:
                           throw new NotImplementedException($"GenerateMacro: Reference to wrong element type {obj}");
                     }
                  } else {
                     throw new NotImplementedException($"GenerateMacro: Unresolved reference to {id}");
                  }
                  break;
               case Affix aff: cg.GenerateMacroElementAffix(aff, macro.CanFail); break;
               case Local loc: cg.GenerateMacroElementLocal(loc); break;
               default:
                  throw new NotImplementedException($"GenerateMacro: Unknown element type {elem.GetType()}");
            }
            first = false;
         }
         cg.GenerateMacroBodyEnd(macro);
         FinalizeAffixesAndVariables(macro,variables);
         cg.GenerateMacroEnd(macro);
      }

      private void FinalizeAffixesAndVariables(Algorithm algorithm,IEnumerable<Var> variables) {
         cg.GenerateAffixAndVariableFinalizationStart(algorithm);
         if (algorithm.NeedsFinalization) {
            foreach (Affix affix in algorithm.affixes) cg.GenerateAffixAndVariableFinalizer(algorithm, affix);
            foreach (Var var in variables) cg.GenerateAffixAndVariableFinalizer(algorithm, var, isVar: true);
         }
         cg.GenerateAffixAndVariableFinalizationEnd(algorithm);
      }

      private void GenerateAlgorithmHeader(Algorithm alg,IEnumerable<Var> variables) {
         GenerateAlgorithmComment(alg);
         cg.GenerateAlgorithmHeaderStart(alg);
         if (alg.affixes.Count > 0) {
            cg.GenerateAffix(alg.affixes[0], alg.affixes[0].affixDir, alg.CanFail);
            foreach (Affix affix in alg.affixes.Skip(1)) {
               cg.GenerateAffixSeparator();
               cg.GenerateAffix(affix, affix.affixDir, alg.CanFail);
            }
         }
         cg.GenerateAlgorithmHeaderEnd(alg);

         cg.GenerateAffixAndVariableInitializationStart(alg);
         if (alg.NeedsFinalization) {
            foreach (Affix affix in alg.affixes) cg.GenerateAffixAndVariableInitializer(alg, affix);
            foreach (Var var in variables) cg.GenerateAffixAndVariableInitializer(alg, var, isVar: true);
         }
         foreach (Local local in alg.locals) cg.GenerateLocal(local);
         cg.GenerateAffixAndVariableInitializationEnd(alg);
      }

      private void GenerateAlgorithmComment(Algorithm alg) {
         if (!alg.IsSynthetic) {
            sourceCommentPrinter.Print(alg);
            cg.GenerateSourceComment();
         } else {
            cg.GenerateComment(alg.ToString());
         }
      }

      private void GenerateProcedure(Procedure proc, int _) {
         if (proc.IsConditionalCompilation()) {
            GenerateAlgorithmComment(proc);
         } else {
            IEnumerable<Var> variables = proc.GetReferencedVariables();
            cg.GenerateProcedureStart(proc);
            GenerateAlgorithmHeader(proc, variables);
            cg.GenerateProcedureBodyStart(proc, proc.ProcedureBodyType);
            GenerateProcedureBody(proc);
            cg.GenerateProcedureBodyEnd(proc, proc.ProcedureBodyType);
            FinalizeAffixesAndVariables(proc, variables);
            cg.GenerateProcedureEnd(proc);
         }
      }

      private void GenerateProcedureBody(Procedure proc) {
         if (proc.IsVerySimple) {
            // Just a sequence of calls none of which can fail.
            cg.GenerateComment("Very simple body");
            Debug.Assert(proc.group.alternatives.Count == 1,$"GenerateProcedureBody: Expected single alternative, found {proc.group.alternatives.Count}");
            foreach (Call call in proc.group.alternatives[0].calls) {
               if (call.Called!.IsConditionalCompilationOn) continue;   // No need to generate code for this call;
               if (call.IsConditionalCompilationOff) {
                  if (proc.CanFail) cg.GenerateAlternativeFail();
                  return;    // No need to generate code for the rest of the proc's single alternative.
               }
               GenerateCall(proc, call);
            }
            if (proc.group.alternatives[0].lastCall.type == LCT.Standard) GenerateCall(proc,proc.group.alternatives[0].lastCall.call!);
         } else if (proc.IsSimple) {
            // A sequence of alternatives, no groups or repeats.
            cg.GenerateComment("Simple body");
            GenerateAlternatives(proc, proc.group);
         } else {
            cg.GenerateComment("General body");
            GenerateAlternatives(proc, proc.group);
         }
      }

      private void GenerateAlternatives(Procedure proc, Group group) {
         bool generateFollowingAlternatives = true;
         for (int i = 0 ; i < group.alternatives.Count && generateFollowingAlternatives ; i++) {
            generateFollowingAlternatives = GenerateAlternative(proc, group, i);
         }
      }

      private bool GenerateAlternative(Procedure proc,Group group,int i) {
         bool generateFollowingAlternatives = true;
         cg.GenerateAlternativeStart(proc,group,i);
         List<Call> calls = group.alternatives[i].calls;
         bool removeAlternative = calls.Count > 0 && calls[0].IsConditionalCompilationOff;
         bool terminated = false;
         if (removeAlternative) {
            cg.GenerateComment($"Alternative removed by conditional compilation set by {calls[0].Called}");
         } else {
            bool canFail = false;
            foreach (Call call in group.alternatives[i].calls) {
               if (call.IsConditionalCompilationOn) {
                  generateFollowingAlternatives = false; // Supress all later alternatives (in the current group).
                  continue;   // No need to generate code for this call;
               }
               if (call.IsConditionalCompilationOff) {
                  // Skip the rest of the alternative.
                  if (proc.CanFail) cg.GenerateAlternativeFail();
                  cg.GenerateAlternativeEnd(proc, group, i, false, removed: true);
                  return generateFollowingAlternatives;
               }
               GenerateCall(proc, call, canFail);
               canFail = canFail || call.CanFail;
            }
            
            switch (group.alternatives[i].lastCall.type) {
               case LCT.Standard: GenerateCall(proc, group.alternatives[i].lastCall.call!, canFail,onlyCallInAlternative:calls.Count == 0,lastAlternative:group.alternatives.Count == i+1); break;
               case LCT.Fail: cg.GenerateFail(proc, group); terminated = true; break;
               case LCT.Succeed: cg.GenerateSucceed(proc, group); break;
               case LCT.Abort: cg.GenerateAbort(proc, group); terminated = true; break;
               case LCT.Repeat: cg.GenerateRepeat(proc, group, group.alternatives[i].lastCall.label!,canFail); break;
               case LCT.Group: GenerateGroup(proc, group.alternatives[i].lastCall.group!); break;
               case LCT.None: break; // Used in the alternative generated for Section Ludes.
               default:
                  throw new NotImplementedException($"GenerateAlternative: Unknown last call type {group.alternatives[i].lastCall.type}");
            }
         }
         // If conditional compilation removes later alternatives pretend that this was the last one.
         cg.GenerateAlternativeEnd(proc, group, generateFollowingAlternatives ? i : group.alternatives.Count - 1, terminated, removed: removeAlternative, singleCallInAlternative: calls.Count == 0);
         return generateFollowingAlternatives;
      }

      private void GenerateGroup(Procedure proc,Group group) {
         cg.GenerateGroupStart(proc,group);
         GenerateAlternatives(proc, group);
         cg.GenerateGroupEnd(proc,group);
      }

      private void GenerateCall(Procedure proc,Call call,bool canFail = false,bool onlyCallInAlternative=false,bool lastAlternative=false) {
         if (call.Called is not null) {
            cg.GenerateCallStart(call.Called!, proc, canFail, onlyCallInAlternative, lastAlternative);
            if (call.args.Count > 0) {
               GenerateActualArg(proc, call, call.Called!.affixes[0], call.args[0]);
               for (int i = 1; i < call.args.Count; i++) {
                  cg.GenerateActualArgSeparator();
                  GenerateActualArg(proc, call, call.Called.affixes[i], call.args[i]);
               }
            }
            cg.GenerateCallEnd(call.Called!, proc, canFail, onlyCallInAlternative,lastAlternative);
         } else {
            cg.GenerateComment($"Call to undefined algorithm {call} skipped.");
         }
      }

      private void GenerateActualArg(Procedure proc, Call call, Affix calledAffix, IActualArg arg) {
         switch (arg) {
            case STRING s:
               Debug.Assert(calledAffix.affixType == AT.str,$"GenerateCallStart: String argument for non-string affix {calledAffix}");
               cg.GenerateCallArgString(s.value);
               break;
            case Const c:
               cg.GenerateCallArgReferenceConst(calledAffix, c);
               break;
            case Var v:
               cg.GenerateCallArgReferenceVar(calledAffix, v, needFinalization: call.Called!.NeedsFinalization);
               break;
            case ID id: // May be a reference to an affix or local of the calling proc or a const, or a var.
               if (proc.TryGetAffix(id,out Affix procAffix)) {
                  cg.GenerateCallArgReferenceAffix(calledAffix, procAffix, needFinalization: call.CanFail);
               } else if (proc.TryGetLocal(id,out Local local)) {
                  cg.GenerateCallArgReferenceLocal(calledAffix,local);
               } else if (proc.Parent is Section section && section.TryGetDeclaration(id,out ICDL2DataObject? dataRef)) {
                  if (dataRef is Const c) {
                     Debug.Assert(!calledAffix.IsOutput,$"GenerateCallStart: Const argument for output affix {calledAffix}");
                     cg.GenerateCallArgReferenceConst(calledAffix,c);
                  } else if (dataRef is Var v) {
                     cg.GenerateCallArgReferenceVar(calledAffix, v, needFinalization: call.Called!.NeedsFinalization);
                  } else {
                     throw new NotImplementedException($"GenerateCallStart: Reference to wrong element type {dataRef}");
                  }
               } else {
                  throw new NotImplementedException($"GenerateCallStart: Unresolved reference to {id}");
               }
               break;
            case Affix a:  cg.GenerateCallArgReferenceAffix(calledAffix, a, needFinalization: call.Called!.NeedsFinalization); break;
            case Local lo: cg.GenerateCallArgReferenceLocal(calledAffix,lo); break;
            default:
               throw new NotImplementedException($"GenerateCallStart: Unknown argument type {arg.GetType()}");
         }
      }

      private void DumpReachableObjects(Program prog) {
         Debug.WriteLine($"Reachable Objects for {prog}:");
         foreach (string objectName in ReachableObjects.Select(obj=> ((NamedElement)obj).FQDN()).ToImmutableSortedSet()) { 
            Debug.WriteLine($"   {objectName}");
         }
         Debug.WriteLine("End of reachable objects.");
      }

      private readonly Set<ICDL2Object> ReachableObjects = [];
      private readonly Set<Var> ReadVars = [];     // Used to track the variables that are read in the program. Write references are in <see cref="ReferencedObjects."/>.
      private readonly Set<Var> AmbigousVars = []; //
      private void CollectReachableObjects(Program prog) {
         foreach (RW ludeType in ludeTypes) {
            foreach (ID id in prog.Ludes[ludeType]) {
               if (Database.Instance.Modules.TryGetValue(id, out Module? module)) {
                  CollectReachableObjects(ludeType,module);
               }
            }
         }

      }
      private void CollectReachableObjects(RW Ludetype, Module module) {
         foreach (Section? section in module.Ludes[Ludetype].Select(id => module.Section(id))) {
            if (section is not null) CollectReachableObjects(Ludetype, section); 
         }
      }
      private void CollectReachableObjects(RW ludetype, Section section) {
         // Section ludes contain teh single entry of a synthetic procedure that is the lude
         // So we need to collect all the objects in the section that are reachable from this lude.
         Debug.Assert(section.Ludes[ludetype].Count == 1, $"CollectReachableObjects: Expected single lude in {section}");
         if (section.TryGetDeclaration(section.Ludes[ludetype][0], out Procedure? proc)) {
            if (ReachableObjects.Add(proc!)) CollectReachableObjects(proc!.group);
         } else {
            throw new NotImplementedException($"CollectReachableObjects: Could not find lude {section.Ludes[ludetype][0]} in {section}");
         }
      }
      private void CollectReachableObjects(Group proc) {
         // Collect all the objects reachable from this group.
         foreach (Alternative alt in proc.alternatives) CollectReachableObjects(alt);
      }
      
      private void CollectReachableObjects(Alternative alt) {
         foreach (Call call in alt.calls) {
            if (!CollectReachableObjects(call)) return; // Skip the rest of the alternative.
         }
         switch (alt.lastCall.type) {
            case LCT.Standard:
               if (alt.lastCall.call is not null) CollectReachableObjects(alt.lastCall.call);
               break;
            case LCT.Group:
               if (alt.lastCall.group is not null) CollectReachableObjects(alt.lastCall.group);
               break;
         }
      }

      /// <summary>
      /// Return false if the rest of the alternative contining the call is to be ignored.
      /// </summary>
      /// <param name="call"></param>
      /// <returns></returns>
      private bool CollectReachableObjects(Call call) {
         if (call.Called is not null) {
            Algorithm called = call.Called;
            if (called is ImportedAlgorithm importedAlg) {
               // TODO: Find imported algorithm and assign it to called.
            }
            if (called.IsConditionalCompilationOn) return true;   // Ignore
            if (called.IsConditionalCompilationOff) return false; // Skip the rest of the alternative.

            // Collect objects referrenced in actual args
            for (int i = 0; i<call.args.Count; i++) {
               IActualArg arg = call.args[i];
               Affix affix = called.affixes[i];
               switch (arg) {
                  case Const c:
                     CollectReachableObjects(c);
                     break;
                  case Var v:
                     ReachableObjects.Add(v);
                     if (affix.IsInput) ReadVars.Add(v);
                     break;
                  case ID id:
                     if (call.Called.Section.TryGetDeclaration(id, out ICDL2DataObject? obj)) {
                        if (obj is Const c) {
                           CollectReachableObjects(c);
                        } else if (obj is Var v) {
                           ReachableObjects.Add(v);
                        }
                     } else {
                        throw new NotImplementedException($"CollectReachableObjects: Unresolved reference to {id}");
                     }
                     break;
               }
            }
            if (ReachableObjects.Add(called)) {
               if (called is Macro macro) {
                  CollectReachableObjects(macro);
               } else {
                  Debug.Assert(called is Procedure, $"CollectReachableObjects: Unknown call type {called}");
                  CollectReachableObjects(((Procedure)called).group);
               }
            }
         }
         return true;
      }
      private void CollectReachableObjects(Const constant) {
         if (ReachableObjects.Add(constant)) {
            foreach (IConstElement elem in constant.elements) {
               switch (elem) {
                  case ID id:
                     if (((Section)constant.Parent!).TryGetDeclaration(id, out ICDL2DataObject? obj)) {
                        if (obj is Const c) CollectReachableObjects(c);
                     } else {
                        throw new NotImplementedException($"CollectReachableObjects: Unresolved reference to {id}");
                     }
                     break;
               }
            }
         }
      }
      private void CollectReachableObjects(Macro macro) {
         foreach (IMacroElement element in macro.elements) {
            switch (element) {
               case Affix:
               case Local:
                  break;
               case ID id:
                  if (macro.Section.TryGetDeclaration(id, out ICDL2DataObject? obj)) {
                     switch (obj) {
                        case ImportedConst ic:
                           // TODO: Find imported constant.
                           break;
                        case Const c:
                           CollectReachableObjects(c);
                           break;
                        case Var v:
                           ReachableObjects.Add(v);
                           break;
                        case LIST l:
                           CollectReachableObjects(macro, l);
                           break;
                     }
                  }
                  break;
               case ImportedConst ic:
                  // TODO: Find imported constant.
                  break;
               case Const c:
                  CollectReachableObjects(c);
                  break;
               case Var v:
                  ReachableObjects.Add(v);
                  if (macro.HasNoEffect) {
                     // Assume the variable is read. Because (1) it can't be written, otherwise it would not meet the macro contract and (2) it is referenced so it must be read.
                     // OTOH, with ACTIONs/PREDICATEs we can't tell.
                     ReadVars.Add(v);
                     AmbigousVars.Remove(v);
                  } else if (! ReadVars.Contains(v)){
                     AmbigousVars.Add(v);
                  }
                  break;
               case LIST l:
                  CollectReachableObjects(macro, l);
                  break;
            }
         }
      }

      private void CollectReachableObjects(Macro macro, LIST list) {
         if (ReachableObjects.Add(list)) {
            if (macro.Section.TryGetDeclaration(list.lwb, out ICDL2DataObject? lwbObj) && lwbObj is Const lwb) CollectReachableObjects(lwb);
            if (macro.Section.TryGetDeclaration(list.upb, out ICDL2DataObject? upbObj) && lwbObj is Const upb) CollectReachableObjects(upb);
         }
      }
   }
}
