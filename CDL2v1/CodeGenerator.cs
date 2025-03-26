// Ignore Spelling: CDL

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Text;



namespace CDL2v1 {
   /// <summary>
   /// 
   /// </summary>
   /// <param id="cg"></param>
   [Serializable]
   internal class CodeGenerator(ICodeGenerator cg) {
      private readonly ICodeGenerator cg = cg;

      private static readonly List<RW> ludeTypes = [ RW.PRELUDE,RW.ROOT,RW.POSTLUDE];

      /// <summary>
      /// Generate code for the unit and all its modulesWithLudes.
      /// If there is a unit, then use its Ludes. Otherwise, use the Ludes from all modulesWithLudes.
      /// </summary>
      /// <param id="modulesWithLudes"></param>
      /// <param id="Emitter"></param>
      /// <param id="isSeparate"></param>
      public void GenerateCode(Program program, EmitterBase emitter, bool isSeparate = false) {
         cg.GenerateProgramStart(program, emitter);  // Generate the overall scaffolding

         foreach (ID mod in program.Parts) cg.GenerateProgramPart(program, mod, isSeparate);

         if (!isSeparate) foreach (ID mod in program.Parts) GenerateModule(Database.Instance.Modules[mod], isSeparate: false);


         foreach (RW ludeType in ludeTypes) {            
            IEnumerable<Module> modulesWithLudes = program.Ludes[ludeType].Select(id => Database.Instance.Modules[id]).Where(mod => mod.Ludes[ludeType].Count > 0);
            if (modulesWithLudes.Any()) {
               cg.GenerateProgramLudeStart(ludeType, program);
               foreach (Module module in modulesWithLudes) cg.GenerateProgramLude(ludeType, program, module);
               cg.GenerateProgramLudeEnd(ludeType, program);
            }
         }

         cg.GenerateProgramEnd(program);

         if (isSeparate) foreach (ID mod in program.Parts) GenerateModule(Database.Instance.Modules[mod], isSeparate: true);
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
         cg.GenerateImpExStart(module);

         foreach (Layer layer in module.Children.Cast<Layer>()) GenerateLayer(layer);

        
         foreach (RW ludeType in ludeTypes) {
            IEnumerable<Section?> SectionsWithLudes = module.Ludes[ludeType].Select(id => module.Section(id)).Where(sec => sec?.Ludes[ludeType].Count > 0) ?? [];
            if (SectionsWithLudes.Any()) {
               cg.GenerateModuleLudeStart(ludeType, module);
               foreach (Section? section in SectionsWithLudes) cg.GenerateModuleLude(ludeType, module, section!);
               cg.GenerateModuleLudeEnd(ludeType, module);
            }
          }

         cg.GenerateModuleEnd(module,isSeparate);
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
         GenerateObjects(section.Constants, c => GenerateConstant(section, c));
         GenerateObjects(section.Variables, v => cg.GenerateVar(v));
         GenerateObjects(section.Lists, l => {
            if (section.TryGetDeclaration(l.lwb, out Const? lwb) && section.TryGetDeclaration(l.upb, out Const? upb)) {
               cg.GenerateList(l, lwb!, upb!);
            } else {
               throw new NotImplementedException($"GenerateSection: Could not find lower or upper bound for {l}");
            }
         });

         GenerateObjects(section.Macros, m => GenerateMacro(section, m));
         GenerateObjects(section.NonSyntheticProcedures, GenerateProcedure);
         GenerateObjects(section.SyntheticProcedures, GenerateProcedure, "Synthetic Procedure");

         cg.GenerateSectionEnd(section);
      }

      private void GenerateObjects<T>(IEnumerable<T> items, Action<T> generate, string? specialType = null) {
         if (items.Any()) {
            cg.GenerateObjectSectionStart(items.Count, specialType ?? typeof(T).Name);
            foreach (T item in items) generate(item);
            cg.GenerateObjectSectionEnd(items.Count, typeof(T).Name);
         }
      }

      private void GenerateConstant(Section section,Const constant) {
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

      private void GenerateMacro(Section section,Macro macro) {
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
         bool needed = algorithm.NeedsFinalization;
         cg.GenerateAffixAndVariableFinalizationStart(algorithm,needed);
         if (needed) {
            foreach (Affix affix in algorithm.affixes) cg.GenerateAffixAndVariableFinalizer(algorithm, affix);
            foreach (Var var in variables) cg.GenerateAffixAndVariableFinalizer(algorithm, var, isVar: true);
         }
         cg.GenerateAffixAndVariableFinalizationEnd(algorithm,needed);
      }

      private void GenerateAlgorithmHeader(Algorithm alg,IEnumerable<Var> variables) {
         if (!alg.IsSynthetic && alg is Procedure proc) {
            cg.SourcePrinter.Print(proc, proc.Section);
            cg.GenerateComment(cg.SourcePrinter);
         } else {
            cg.GenerateComment(alg.ToString());
         }
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
         if (alg.CanFail) {
            foreach (Affix affix in alg.affixes) cg.GenerateAffixAndVariableInitializer(alg, affix);
            foreach (Var var in variables) cg.GenerateAffixAndVariableInitializer(alg, var, isVar: true);
         }
         foreach (Local local in alg.locals) cg.GenerateLocal(local);
         cg.GenerateAffixAndVariableInitializationEnd(alg);
      }

      private void GenerateProcedure(Procedure proc) {
         IEnumerable<Var> variables = proc.GetReferencedVariables();
         cg.GenerateProcedureStart(proc);
         GenerateAlgorithmHeader(proc,variables);
         cg.GenerateProcedureBodyStart(proc,proc.ProcedureBodyType);
         GenerateProcedureBody(proc);
         cg.GenerateProcedureBodyEnd(proc,proc.ProcedureBodyType);
         FinalizeAffixesAndVariables(proc,variables);
         cg.GenerateProcedureEnd(proc);
      }

      private void GenerateProcedureBody(Procedure proc) {
         if (proc.IsVerySimple) {
            // Just a sequence of calls none of which can fail.
            cg.GenerateComment("Very simple body");
            Debug.Assert(proc.group.alternatives.Count == 1,$"GenerateProcedureBody: Expected single alternative, found {proc.group.alternatives.Count}");
            foreach (Call call in proc.group.alternatives[0].calls) GenerateCall(proc,call);
            if (proc.group.alternatives[0].lastCall.type == LCT.Standard) GenerateCall(proc,proc.group.alternatives[0].lastCall.call!);
         } else if (proc.IsSimple) {
            // A sequence of alternatives, no groups or repeats.
            cg.GenerateComment("Simple body");
           
            for (int i = 0; i < proc.group.alternatives.Count ; i++) {
               GenerateAlternative(proc,proc.group,i);
            }
         } else {
            cg.GenerateComment("General body");

            for (int i = 0 ; i < proc.group.alternatives.Count ; i++) {
               GenerateAlternative(proc,proc.group,i);
            }
         }
      }

      private void GenerateAlternative(Procedure proc,Group group,int i) {
         cg.GenerateAlternativeStart(proc,group,i);
         bool canFail = false;
         foreach (Call call in group.alternatives[i].calls) {            
            GenerateCall(proc,call,canFail);
            canFail = canFail || call.CanFail;
         }
         switch (group.alternatives[i].lastCall.type) {
            case LCT.Standard: GenerateCall(proc,group.alternatives[i].lastCall.call!,canFail); break;
            case LCT.Fail: cg.GenerateFail(proc,group); break;
            case LCT.Succeed: cg.GenerateSucceed(proc,group); break;
            case LCT.Abort: cg.GenerateAbort(proc,group); break;
            case LCT.Repeat: cg.GenerateRepeat(proc,group,group.alternatives[i].lastCall.label!); break;
            case LCT.Group: GenerateGroup(proc,group.alternatives[i].lastCall.group!); break;
            case LCT.None: break; // Used in the alternative generated for Section Ludes.
            default:
               throw new NotImplementedException($"GenerateAlternative: Unknown last call type {group.alternatives[i].lastCall.type}");
         }
         cg.GenerateAlternativeEnd(proc,group,i);
      }

      private void GenerateGroup(Procedure proc,Group group) {
         cg.GenerateGroupStart(proc,group);
         for (int i = 0 ; i < group.alternatives.Count ; i++) {
            GenerateAlternative(proc,group,i);
         }
         cg.GenerateGroupEnd(proc,group);
      }

      private void GenerateCall(Procedure proc,Call call,bool canFail = false) {
         cg.GenerateCallStart(call.Called,proc,canFail);
         if (call.args.Count > 0) {
            GenerateActualArg(proc,call.Called.affixes[0],call.args[0]);
            for (int i = 1 ; i < call.args.Count ; i++) {
               cg.GenerateActualArgSeparator();
               GenerateActualArg(proc,call.Called.affixes[i],call.args[i]);
            }
         }
         cg.GenerateCallEnd(call.Called,proc,canFail);
      }

      private void GenerateActualArg(Procedure proc,Affix calledAffix,IActualArg arg) {
         switch (arg) {
            case STRING s:
               Debug.Assert(calledAffix.affixType == AT.str,$"GenerateCallStart: String argument for non-string affix {calledAffix}");
               cg.GenerateCallArgString(s.value);
               break;
            case ID id: // May be a reference to an affix or local of the calling proc or a const, or a var.
               if (proc.TryGetAffix(id,out Affix procAffix)) {
                  cg.GenerateCallArgReferenceAffix(calledAffix,procAffix);
               } else if (proc.TryGetLocal(id,out Local local)) {
                  cg.GenerateCallArgReferenceLocal(calledAffix,local);
               } else if (proc.Parent is Section section && section.TryGetDeclaration(id,out ICDL2DataObject? dataRef)) {
                  if (dataRef is Const c) {
                     Debug.Assert(!calledAffix.IsOutput,$"GenerateCallStart: Const argument for output affix {calledAffix}");
                     cg.GenerateCallArgReferenceConst(calledAffix,c);
                  } else if (dataRef is Var v) {
                     cg.GenerateCallArgReferenceVar(calledAffix,v);
                  } else {
                     throw new NotImplementedException($"GenerateCallStart: Reference to wrong element type {dataRef}");
                  }
               } else {
                  throw new NotImplementedException($"GenerateCallStart: Unresolved reference to {id}");
               }
               break;
            case Affix a:  cg.GenerateCallArgReferenceAffix(calledAffix,a); break;
            case Local lo: cg.GenerateCallArgReferenceLocal(calledAffix,lo); break;
            default:
               throw new NotImplementedException($"GenerateCallStart: Unknown argument type {arg.GetType()}");
         }
      }
   }
}
