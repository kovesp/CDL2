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
   public class CodeGenerator(ICodeGenerator cg,Reachable reachable) {
      /// <summary>
      /// Target specific code gnerator to use.
      /// </summary>
      private readonly ICodeGenerator cg = cg;
      private readonly Reachable Reachable = reachable;

      /// <summary>
      /// Used to add the CDL2 code to each generated algorithm.
      /// Will not include the CDL2 comments and Notes associated with CDL2 objects.
      /// </summary>
      private readonly PrettyPrinter sourceCommentPrinter = new(cg.SourceEmitter,includeComments:false);



      /// <summary>
      /// Generate code for the unit and all its modulesWithLudes.
      /// If there is argAffix unit, then use its Ludes. Otherwise, use the Ludes from all modulesWithLudes.
      /// </summary>
      /// <param id="modulesWithLudes"></param>
      /// <param id="Emitter"></param>
      /// <param id="isSeparate"></param>
      public void GenerateCode(Program program, EmitterBase emitter, bool isSeparate = false) {
         foreach (Var var in Reachable.Objects.OfType<Var>()) {
            if (Reachable.AmbigousVars.Contains(var)) {
               // We know the variable was written to, but we can't tell whther it was read (becasue it was only referenced in an ACTION/PREDICATE macro).
               var.AddNote("CodeGeneration", Note.VariableMayNotHaveBeenRead, var);
               Logger.ReportError($"Variable {var} may not have been read. It was only referenced in an ACTION/PREDICATE macro.");
            } else if (!Reachable.ReadVars.Contains(var)) {
               // It must have been written, but not read.
               var.AddNote("CodeGeneration", Note.VariableNotRead, var);
               Logger.ReportError($"Variable {var} was written to, but never read.");
            }
         }
         //DumpReachableObjects(program);

         if (!isSeparate) {
            // Generate an integrated program ignoring module boundaries of all objects reachable from the program's ludes.
            cg.GenerateProgramStart(program, emitter);  // Generate the overall scaffolding


            GenerateObjects<Const>(Reachable.Objects.OfType<Const>(),                                        GenerateConstant);
            GenerateObjects<Var>(Reachable.Objects.OfType<Var>(),                                            GenerateVar);
            GenerateObjects<LIST>(Reachable.Objects.OfType<LIST>(),                                          GenerateList);

            GenerateObjects<Macro>(Reachable.Objects.OfType<Macro>(),                                        GenerateMacro);
            GenerateObjects<Procedure>(Reachable.Objects.OfType<Procedure>().Where(proc=>!proc.IsSynthetic), GenerateProcedure);
            GenerateObjects<Procedure>(Reachable.Objects.OfType<Procedure>().Where(proc=> proc.IsSynthetic), GenerateProcedure, "Synthetic Procedure");

            sourceCommentPrinter.Print(program);
            cg.GenerateSourceComment();
            cg.GenerateComment("Program Ludes");
            foreach (RW ludeType in Container.LudeTypes) foreach (Module mod in program.Lude(ludeType)) GenerateModuleLude(ludeType, mod, wrapped: false);
            cg.GenerateProgramEnd(program);
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
         foreach (RW ludeType in Container.LudeTypes) {
            IEnumerable<Module> modulesWithLudes = program.Ludes[ludeType].Select(id => Database.Instance.Modules[id]).Where(mod => mod.Ludes[ludeType].Count > 0);
            if (modulesWithLudes.Any()) {
               cg.GenerateProgramLudeStart(ludeType, program);
               foreach (Module module in modulesWithLudes) cg.GenerateProgramLude(ludeType, program, module);
               cg.GenerateProgramLudeEnd(ludeType, program);
            }
         }
      }

      /// <summary>
      /// Generate code for argAffix module. It is up to the specific code generator to determine whether this code goes into argAffix separate file or not.
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

         foreach (RW ludeType in Container.LudeTypes) GenerateModuleLude(ludeType, module, wrapped: true);

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
      /// Generate proc for argAffix layer. Typically there is no target proc associated with this.
      /// </summary>
      /// <param id="layer"></param>
      private void GenerateLayer(Layer layer) {
         cg.GenerateLayerStart(layer);
         foreach (Section section in layer.Children.Cast<Section>()) GenerateSection(section);
         cg.GenerateLayerEnd(layer);
      }

      /// <summary>
      /// Generate argAffix container. Again, there will likely be no target proc associated with argAffix container itself.
      /// So generate proc for each routine and for the Ludes.
      /// A lude is just proc with argAffix special id
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
         if (!Settings.SettingValue<bool>("NoMacroInlining") && macro.IsInlineMacro) {
            GenerateAlgorithmComment(macro);
            cg.GenerateComment("Macro inlined");
         } else {
            Section section = (Section)macro.Parent!;
            IEnumerable<Var> variables = macro.GetReferencedVariables();
            cg.GenerateMacroStart(macro);
            GenerateAlgorithmHeader(macro, variables);

            GenerateMacroBody(macro);
            FinalizeAffixesAndVariables(macro, variables);
            cg.GenerateMacroEnd(macro);
         }
      }

      private void GenerateMacroBody(Macro macro, Procedure? callingProc = null, List<IActualArg>? args = null) {
         Dictionary<Affix,IActualArg> subst = args is null ? [] : macro.affixes.Zip(args, (key, value) => new { key, value }).ToDictionary(x => x.key, x => x.value);

         cg.GenerateMacroBodyStart(macro);
         bool first = true;
         foreach (IMacroElement elem in macro.elements) {
            GenerateMacroElement(macro, macro.Section,callingProc, subst, first, elem);
            first = false;
         }
         cg.GenerateMacroBodyEnd(macro);
      }

      private void GenerateMacroElement(Macro macro, Section section, Procedure? callingProc, Dictionary<Affix, IActualArg> subst, bool first, IMacroElement elem) {
         switch (elem) {
            case INT i: cg.GenerateMacroElementInt(i.value); break;
            case FLOAT f: cg.GenerateMacroElementFloat(f.value); break;
            case STRING s: cg.GenerateMacroElementString(s.value, firstElement:first, quoted:false); break;
            case ID id:
               // This should be a reference to a Const, Var or List, so check which one
               if (section.TryGetDeclaration(id, out ILocalCDL2DataObject? obj)) {
                  switch (obj) {
                     case Const c: cg.GenerateMacroElementConst(c); break;
                     case Var v: cg.GenerateMacroElementVar(v, macro.CanFail); break;
                     case LIST l: cg.GenerateMacroElementList(l); break;
                     default:
                        throw new NotImplementedException($"GenerateMacro: Reference to wrong element type {obj}");
                  }
               } else {
                  throw new NotImplementedException($"GenerateMacro: Unresolved reference to {id}");
               }
               break;
            case Affix aff:
               if (subst.TryGetValue(aff, out IActualArg? arg)) {
                  Debug.Assert(callingProc is not null, $"GenerateMacro: Calling procedure is null for inlined macro {macro}");
                  switch (arg) {
                     case Var   vv: cg.GenerateMacroElementVar(vv, callingProc.CanFail, inlined: true); break;
                     case Const cc: cg.GenerateMacroElementConst(cc); break;
                     case Local ll: cg.GenerateMacroElementLocal(ll); break;
                     case Affix aa: cg.GenerateMacroElementAffix(aa, callingProc.CanFail); break;
                     case STRING s: cg.GenerateMacroElementString(s.value, firstElement:false,quoted:true); break;
                     default: Debugger.Break(); break;
                  }
               } else {
                  cg.GenerateMacroElementAffix(aff, macro.CanFail);
               }
               break;
            case Local loc: cg.GenerateMacroElementLocal(loc); break;
            default:
               throw new NotImplementedException($"GenerateMacro: Unknown element type {elem.GetType()}");
         }
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
         }         cg.GenerateAlgorithmHeaderEnd(alg);

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
            GenerateAlternatives(proc, proc.group);
            cg.GenerateProcedureBodyEnd(proc, proc.ProcedureBodyType);
            FinalizeAffixesAndVariables(proc, variables);
            cg.GenerateProcedureEnd(proc);
         }
      }

      private void GenerateProcedureBody(Procedure proc) => GenerateAlternatives(proc, proc.group);
      /// <summary>
      /// Generate the alternatives for argAffix procedure.
      /// This method manages the conditional compilation of the alternatives based on whether the first call in the altarnative is conditional compilation on or off.
      /// If it is off, then no code is generated for that alternative.
      /// If it is on, then that alternative is generated, but all later alternatives are skipped.
      /// </summary>
      /// <param name="proc"></param>
      /// <param name="group"></param>
      private void GenerateAlternatives(Procedure proc, Group group) {
         bool supressRest = false;
         bool removed;
         
         int i = 1;
         foreach (Alternative alternative in group.alternatives) {   
            cg.GenerateAlternativeStart(proc, group, i);
            removed = false;
            if (supressRest) {
               cg.GenerateComment($"Alternative supressed by previous conditional compilation ON");
               removed = true;
            } else if (alternative.IsConditionalCompilationOff) {           // Ignore this alternative
               cg.GenerateComment($"Alternative removed by conditional compilation OFF");
               removed = true;
            } else {          
               supressRest = alternative.IsConditionalCompilationOn;       // Ignore following alternatives
               GenerateAlternative(proc, group, alternative, supressRest || group.alternatives.Count == i);
            }
            cg.GenerateAlternativeEnd(proc, group, i, alternative, removed);

            i++;
         }
      }

      private void GenerateAlternative(Procedure proc,Group group,Alternative alternative,bool isLast) {
         List<Call> calls = alternative.calls;
         bool canFail = false;
         foreach (Call call in calls) {
            GenerateCall(proc, call, canFail);
            canFail = canFail || call.CanFail;
         }
         switch (alternative.lastCall.type) {
            case LCT.Standard: GenerateCall(proc, alternative.lastCall.call!, canFail,onlyCallInAlternative:calls.Count == 0,lastAlternative:isLast); break;
            case LCT.Fail: cg.GenerateFail(proc, group); break;
            case LCT.Succeed: cg.GenerateSucceed(proc, group); break;
            case LCT.Abort: cg.GenerateAbort(proc, group); break;
            case LCT.Repeat: cg.GenerateRepeat(proc, group, alternative.lastCall.label!,canFail); break;
            case LCT.Group: GenerateGroup(proc, alternative.lastCall.group!); break;
            case LCT.None: break; // Used in the alternative generated for Section Ludes.
            default:
               throw new NotImplementedException($"GenerateAlternative: Unknown last call type {alternative.lastCall.type}");
         }
      }

      private void GenerateGroup(Procedure proc,Group group) {
         cg.GenerateGroupStart(proc,group);
         GenerateAlternatives(proc, group);
         cg.GenerateGroupEnd(proc,group);
      }

      private void GenerateCall(Procedure proc,Call call,bool canFail = false,bool onlyCallInAlternative=false,bool lastAlternative=false) {
         if (call.IsConditionalCompilationOn) return;   // No need to generate code for this call;
         if (call.Called is not null) {
            if (!Settings.SettingValue<bool>("NoMacroInlining") && call.Called is Macro macro && macro.IsInlineMacro) {
               cg.GenerateMacroInlineStart(macro);
               GenerateMacroBody(macro, proc, call.args);
               cg.GenerateMacroInlineEnd(macro);
            } else {
               cg.GenerateCallStart(call.Called!, proc, canFail, onlyCallInAlternative, lastAlternative);
               if (call.args.Count > 0) {
                  GenerateActualArg(proc, call, call.Called!.affixes[0], call.args[0]);
                  for (int i = 1 ; i < call.args.Count ; i++) {
                     cg.GenerateActualArgSeparator();
                     GenerateActualArg(proc, call, call.Called.affixes[i], call.args[i]);
                  }
               }
               cg.GenerateCallEnd(call.Called!, proc, canFail, onlyCallInAlternative, lastAlternative);
            }
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
            case ID id: // May be a reference to an affix or local of the calling proc or argAffix const, or argAffix var.
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
            case Affix argAffix:  cg.GenerateCallArgReferenceAffix(calledAffix, argAffix,proc.NeedsFinalization); break;
            case Local lo: cg.GenerateCallArgReferenceLocal(calledAffix,lo); break;
            default:
               throw new NotImplementedException($"GenerateCallStart: Unknown argument type {arg.GetType()}");
         }
      }

   }
}
