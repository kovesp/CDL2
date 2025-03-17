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
                     
                     GenerateProcedureCode((Procedure)section.declarations[procId]);
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
         foreach (ID expId in module.exports.Keys) cg.GenerateExport(module,expId);
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
         GenerateObjects(section.Constants,c => GenerateConstant(section,c));
         GenerateObjects(section.Variables,v => cg.GenerateCodeDeclareVar(v));
         GenerateObjects(section.Lists,l => {
            if (section.TryGetDeclaration(l.lwb,out Const? lwb) && section.TryGetDeclaration(l.upb,out Const? upb)) {
               cg.GenerateCodeDeclareList(l,lwb!,upb!);
            } else {
               throw new NotImplementedException($"GenerateSection: Could not find lower or upper bound for {l}");
            }
         });
         GenerateObjects(section.Macros,m=>GenerateMacroCode(section,m));
         GenerateObjects(section.NonSyntheticProcedures,GenerateProcedureCode);
         cg.GenerateEnd(section);
      }

      private void GenerateObjects<T>(IEnumerable<T> items,Action<T> generate) {
         cg.GenerateDataSectionStart(items.Count,typeof(T).Name);
         foreach (T item in items) generate(item);
      }

      private void GenerateConstant(Section section,Const constant) {
         cg.GenerateConstantStart(constant);
         foreach (IConstElement elem in constant.elements) {
            switch (elem) {
               case INT i: cg.GenerateConstElemInt(i.value); break;
               case FLOAT f: cg.GenerateConstElemFloat(f.value); break;
               case STRING s: cg.GenerateConstElemString(s.value); break;
               case ID id:
                  if (section.TryGetDeclaration(id,out Const? c)) {
                     cg.GenerateReference(c!);
                  } else {
                     throw new NotImplementedException($"GenerateSection: Reference to wrong element type ");
                  }
                  break;
            }
         }
         cg.GenerateConstantEnd(constant);
      }

      private void GenerateMacroCode(Section section,Macro macro) {
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
                  if (section.TryGetDeclaration(id,out Const? c)) {
                     cg.GenerateReference(c!);
                  } else if (section.TryGetLocalDeclaration(id,out ILocalCDL2DataObject? v)) {
                     if (v is Var var) cg.GenerateReference(var);
                     else if (v is LIST list) cg.GenerateReference(list);
                     else throw new NotImplementedException($"GenerateMacroCode: Reference to wrong element type {v}");
                  } else {
                     throw new NotImplementedException($"GenerateMacroCode: Unresolved reference to {id}");
                  }
                  break;
               case Affix a: cg.GenerateReference(a); break;
               case Local lo: cg.GenerateReferenceLocal(lo); break;
               default:
                  throw new NotImplementedException($"GenerateMacroCode: Unknown element type {elem.GetType()}");
            }
         }
         cg.GenerateMacroBodyEnd(macro);
         FinalizeAffixesAndVariables(macro,variables);
         cg.GenerateEnd(macro);
      }

      private void FinalizeAffixesAndVariables(Algorithm algorithm,IEnumerable<Var> variables) {
         bool needed = algorithm.NeedsFinalization;
         cg.FinalizationStart(algorithm,needed);
         if (needed) {
            foreach (Affix affix in algorithm.affixes) cg.GenerateFinalizer(affix,affix.affixDir);
            foreach (Var var in variables) cg.GenerateFinalizer(var,AD.transput,isVar: true);
         }
         cg.FinalizationEnd(algorithm,needed);
      }

      private void GenerateAlgorithmHeader(Algorithm alg,IEnumerable<Var> variables) {
         cg.GenerateComment(alg.ToString());
         cg.GenerateAlgorithmHeaderStart(alg);
         if (alg.affixes.Count > 0) {
            cg.GenerateDeclareAffix(alg.affixes[0],alg.affixes[0].affixDir);
            foreach (Affix affix in alg.affixes.Skip(1)) {
               cg.GenerateParamSeparator();
               cg.GenerateDeclareAffix(affix,affix.affixDir);
            }
         }
         cg.GenerateAlgorithmHeaderEnd(alg);
         foreach (Affix affix in alg.affixes) cg.GenerateInitializer(affix,affix.affixDir);
         foreach (Var var in variables) cg.GenerateInitializer(var,AD.transput,isVar: true);
         foreach (Local local in alg.locals) cg.GenerateDeclareLocal(local);
      }

      private void GenerateProcedureCode(Procedure proc) {
         IEnumerable<Var> variables = proc.GetReferencedVariables();
         cg.GenerateStart(proc);
         GenerateAlgorithmHeader(proc,variables);
         cg.GenerateProcedureBodyStart(proc);
         GenerateProcedureBody(proc);
         cg.GenerateProcedureBodyEnd(proc);
         FinalizeAffixesAndVariables(proc,variables);
         cg.GenerateEnd(proc);
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
            case LCT.Repeat: cg.GenerateRepeat(proc,group); break;
            case LCT.Group: GenerateGroup(proc,group.alternatives[i].lastCall.group!); break;
            case LCT.None: break; // Use in the alternative generated for section Ludes.
            default:
               throw new NotImplementedException($"GenerateAlternative: Unknown last call type {group.alternatives[i].lastCall.type}");
         }
         cg.GenerateAlternativeEnd(proc,group,i);
      }

      private void GenerateGroup(Procedure proc,Group group) => throw new NotImplementedException();

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
               Debug.Assert(calledAffix.affixType == AT.str,$"GenerateCall: String argument for non-string affix {calledAffix}");
               cg.GenerateCallArgString(s.value);
               break;
            case ID id: // May be a reference to an affix or local of the calling proc or a const, or a var.
               if (proc.TryGetAffix(id,out Affix procAffix)) {
                  cg.GenerateCallArgReferenceAffix(calledAffix,procAffix);
               } else if (proc.TryGetLocal(id,out Local local)) {
                  cg.GenerateCallArgReferenceLocal(calledAffix,local);
               } else if (proc.Parent is Section section && section.TryGetDeclaration(id,out ICDL2DataObject? dataRef)) {
                  if (dataRef is Const c) {
                     Debug.Assert(!calledAffix.IsOutput,$"GenerateCall: Const argument for output affix {calledAffix}");
                     cg.GenerateCallArgReferenceConst(calledAffix,c);
                  } else if (dataRef is Var v) {
                     cg.GenerateCallArgReferenceVar(calledAffix,v);
                  } else {
                     throw new NotImplementedException($"GenerateCall: Reference to wrong element type {dataRef}");
                  }
               } else {
                  throw new NotImplementedException($"GenerateCall: Unresolved reference to {id}");
               }
               break;
            case Affix a:  cg.GenerateCallArgReferenceAffix(calledAffix,a); break;
            case Local lo: cg.GenerateCallArgReferenceLocal(calledAffix,lo); break;
            default:
               throw new NotImplementedException($"GenerateCall: Unknown argument type {arg.GetType()}");
         }
      }
   }
}
