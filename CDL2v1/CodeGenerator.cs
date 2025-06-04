// Ignore Spelling: CDL

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
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
   /// <param Id="cg"></param>
   public class CodeGenerator(ICodeGenerator cg, CDL2 compiler) : CompilationPhase(compiler) {
      /// <summary>
      /// Target specific code gnerator to use.
      /// </summary>
      private readonly ICodeGenerator cg = cg;

      /// <summary>
      /// Used to add the CDL2 code to each generated algorithm.
      /// Will <b>not</b> include the CDL2 comments and Notes associated with the algorithms.
      /// </summary>
      private readonly PrettyPrinter sourceCommentPrinter = new(cg.SourceEmitter,includeComments:false);

      /// <summary>
      /// Generate code for the unit and all its modulesWithLudes.
      /// If there is argAffix unit, then use its Ludes. Otherwise, use the Ludes from all modulesWithLudes.
      /// </summary>
      /// <param Id="modulesWithLudes"></param>
      /// <param Id="Emitter"></param>
      /// <param Id="isSeparate"></param>
      public void GenerateCode(Program program, EmitterBase emitter, bool isSeparate = false) {
         foreach (Var var in Compiler.Reachable.Objects.OfType<Var>()) {
            if (Compiler.Reachable.AmbigousVars.Contains(var)) {
               // We know the variable was written to, but we can't tell whther it was read (becasue it was only referenced in an ACTION/PREDICATE macro).
               var.AddNote("CodeGeneration", Note.VariableMayNotHaveBeenRead, var);
               Logger.ReportError($"Variable {var} may not have been read. It was only referenced in an ACTION/PREDICATE macro.");
            } else if (!Compiler.Reachable.ReadVars.Contains(var)) {
               // It must have been written, but not read.
               var.AddNote("CodeGeneration", Note.VariableNotRead, var);
               Logger.ReportError($"Variable {var} was written to, but never read.");
            }
         }
         //DumpReachableObjects(program);

         if (!isSeparate) {
            // Generate an integrated program ignoring module boundaries of all objects reachable from the program's ludes.
            cg.GenerateProgramStart(program, emitter);  // Generate the overall scaffolding


            GenerateObjects<Const>(Compiler.Reachable.Objects.OfType<Const>(),                                        GenerateConstant);
            GenerateObjects<Var>(Compiler.Reachable.Objects.OfType<Var>(),                                            GenerateVar);
            GenerateObjects<LIST>(Compiler.Reachable.Objects.OfType<LIST>(),                                          GenerateList);

            GenerateObjects<Macro>(Compiler.Reachable.Objects.OfType<Macro>(),                                        GenerateMacro);
            GenerateObjects<Procedure>(Compiler.Reachable.Objects.OfType<Procedure>().Where(proc=>!proc.IsSynthetic), GenerateProcedure);
            GenerateObjects<Procedure>(Compiler.Reachable.Objects.OfType<Procedure>().Where(proc=> proc.IsSynthetic), GenerateProcedure, "Synthetic Procedure");

            sourceCommentPrinter.Print(program);
            cg.GenerateSourceComment();
            cg.GenerateComment("Program Ludes");
            foreach (RW ludeType in Container.LudeTypes) foreach (Module mod in program.Lude(ludeType)) GenerateModuleLude(ludeType, mod, wrapped: false);
            cg.GenerateProgramEnd(program);
         } else {
            // TODO: Needs work
            cg.GenerateProgramStart(program, emitter);  // Generate the overall scaffolding
            sourceCommentPrinter.Print(program);
            cg.GenerateSourceComment();
            foreach (ID modid in program.Parts) cg.GenerateProgramPart(program, modid, isSeparate);

            GenerateProgramLudes(program);
            cg.GenerateProgramEnd(program);
            foreach (Module mod in program.Modules) GenerateModule(mod, isSeparate: true);
         }
      }

      /// <summary>
      /// Generate the program prelude, root and postlude.
      /// </summary>
      /// <param name="program"></param>
      private void GenerateProgramLudes(Program program) {
         foreach (RW ludeType in Container.LudeTypes) {
            IEnumerable<Module> modulesWithLudes = program.Ludes[ludeType].Select(id => Database.Instance.ModuleByName(id)!).Where(mod => mod.Ludes[ludeType].Count > 0);
            if (modulesWithLudes.Any()) {
               cg.GenerateProgramLudeStart(ludeType, program);
               foreach (Module module in modulesWithLudes) cg.GenerateProgramLude(ludeType, program, module);
               cg.GenerateProgramLudeEnd(ludeType, program);
            }
         }
      }

      /// <summary>
      /// Generate code for module. It is up to the specific code generator to determine whether this code goes into a separate file or not.
      /// </summary>
      /// <param Id="module"></param>
      private void GenerateModule(Module module, bool isSeparate) {
         static void GenerateImpEx<T>(IDDictionary<T> impexList, Action<T> generateImpEx) {
            foreach (T impex in impexList.Values) {               
                generateImpEx(impex);
            }
         }

         cg.GenerateModuleStart(module, isSeparate);

         cg.GenerateImpExStart(module);
         GenerateImpEx(module.exports, cg.GenerateExport);
         GenerateImpEx(module.imports, cg.GenerateImport);
         cg.GenerateImpExEnd(module);

         foreach (Layer layer in module.Layers) GenerateLayer(layer);

         foreach (RW ludeType in Container.LudeTypes) GenerateModuleLude(ludeType, module, wrapped: true);

         cg.GenerateModuleEnd(module, isSeparate);
      }
      /// <summary>
      /// Generate code for the module ludes. There is aone for each type if it exists.
      /// </summary>
      /// <param name="ludeType"></param>
      /// <param name="module"></param>
      /// <param name="wrapped"></param>
      private void GenerateModuleLude(RW ludeType, Module module, bool wrapped) {
         IEnumerable<Section?> SectionsWithLudes = module.Ludes[ludeType].Select(id => module.SectionById(id)).Where(sec => sec?.Ludes[ludeType].Count > 0) ?? [];
         if (SectionsWithLudes.Any()) {
            cg.GenerateModuleLudeStart(ludeType, module, wrapped: wrapped);
            foreach (Section? section in SectionsWithLudes) cg.GenerateModuleLude(ludeType, module, section!);
            cg.GenerateModuleLudeEnd(ludeType, module, wrapped: wrapped);
         }
      }

      /// <summary> 
      /// Generate proc for a layer. Typically there is no target code associated with the layer itself.
      /// </summary>
      /// <param Id="layer"></param>
      private void GenerateLayer(Layer layer) {
         cg.GenerateLayerStart(layer);
         foreach (Section section in layer.Sections) GenerateSection(section);
         cg.GenerateLayerEnd(layer);
      }

      /// <summary>
      /// Generate a section. Again, there will likely be no target proc associated with section itself.
      /// So generate the constants, variables and lists folowed by the algorithms. Macros first, then user procedures and synthetic procdures (the ludes).
      /// </summary>
      /// <param Id="section"></param>
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

      /// <summary>
      /// Generate code for a list.
      /// </summary>
      /// <param name="list"></param>
      /// <exception cref="NotImplementedException"></exception>
      private void GenerateList(LIST list, int _) {
         Section section = list.ParentElement<Section>()!;
         if (section.TryGetDeclaration(list.lwb, out Const? lwb) && section.TryGetDeclaration(list.upb, out Const? upb)) {
            cg.GenerateList(list, lwb!, upb!);
         } else {
            throw new NotImplementedException($"GenerateSection: Could not find lower or upper bound for {list}");
         }
      }

      /// <summary>
      /// Generate code for a variable.
      /// </summary>
      /// <param name="v"></param>
      private void GenerateVar(Var v,int _) => cg.GenerateVar(v);

      /// <summary>
      /// Generate code for a list of objects
      /// </summary>
      /// <typeparam name="T">A CDL2Object, so Algorithm, LIST, Var, Const </typeparam>
      /// <param name="items"></param>
      /// <param name="generate"></param>
      /// <param name="specialType"></param>
      private void GenerateObjects<T>(IEnumerable<NamedElement> items, Action<T,int> generate, string? specialType = null) where T : CDL2Object {
         if (items.Any()) {
#if AllignNames
            int maxNameLength = items.Select(item=>item.Id.InternalName.Length).Max();
#else
            int maxNameLength = 0;
#endif
            cg.GenerateObjectSectionStart<T>(items, specialType ?? typeof(T).Name);
            foreach (T item in items.Cast<T>()) generate(item,maxNameLength);
            cg.GenerateObjectSectionEnd<T>(items, typeof(T).Name);
         }
      }
      /// <summary>
      /// Generate code for a constant.
      /// </summary>
      /// <param name="constant"></param>
      /// <exception cref="NotImplementedException"></exception>
      private void GenerateConstant(Const constant,int _) {
         Section section = constant.ParentElement<Section>()!;
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

      /// <summary>
      /// Generate code for a macro.
      /// </summary>
      /// <param name="macro"></param>
      /// <param name="_"></param>
      private void GenerateMacro(Macro macro,int _) {
         if (!Settings.SettingValue<bool>("NoMacroInlining") && macro.IsInlineMacro) {
            GenerateAlgorithmComment(macro);
            cg.GenerateComment("Macro inlined");
         } else {
            Section section = macro.ParentElement<Section>()!;
            IEnumerable<Var> variables = macro.GetReferencedVariables();
            cg.GenerateMacroStart(macro);
            GenerateAlgorithmHeader(macro, variables);

            GenerateMacroBody(macro);
            FinalizeAffixesAndVariables(macro, variables);
            cg.GenerateMacroEnd(macro);
         }
      }

      /// <summary>
      /// Generate the body of a macro.
      /// </summary>
      /// <param name="macro"></param>
      /// <param name="callingProc"></param>
      /// <param name="args"></param>
      /// <param name="parameters"></param>
      private void GenerateMacroBody(Macro macro, Procedure? callingProc = null, List<IActualArg>? args = null,Parameters? parameters = null) {
         parameters = new(parameters,macro.affixes, args ?? []);
         cg.GenerateMacroBodyStart(macro);
         bool first = true;
         foreach (IMacroElement elem in macro.elements) {
            GenerateMacroElement(macro, macro.Section!,callingProc, parameters, first, elem);
            first = false;
         }
         cg.GenerateMacroBodyEnd(macro);
      }

      /// <summary>
      /// Represents a list of parameters for an Algorithm call.
      /// </summary>
      private class Parameters : List<Parameters.Parameter> {
         /// <summary>
         /// Represents an Algorithm call parameter.
         /// Maps an affix to the actual argument.
         /// </summary>
         /// <param name="i">The ordinal of the argument. Not currently used.</param>
         /// <param name="affix"></param>
         /// <param name="arg"></param>
         internal struct Parameter(int i,Affix affix, IActualArg arg) {
            public int argNo = i;
            public Affix affix = affix;
            public IActualArg arg = arg;
         }

         private Parameters() : base() { }

         /// <summary>
         /// Construct a parameter list from a list of affixes and actual arguments.
         /// If an actual argument is an Affix, then it is replaced with the corresponding argument from the parameters list.
         /// This implements actula args cascaded through multiple procedure inlinings.
         /// </summary>
         /// <param name="parameters"></param>
         /// <param name="affixes"></param>
         /// <param name="args"></param>
         public Parameters(Parameters? parameters,List<Affix> affixes, List<IActualArg> args) : base() {
            parameters ??= [];
            for (int i = 0 ; i < affixes.Count ; i++) {               
               Add(new Parameter(i, affixes[i], args![i] is Affix aff && parameters.TryGetValue(aff,out IActualArg? arg) ? arg! : args![i]));
            }
         }
         /// <summary>
         /// Try to get the value of an affix from the list.
         /// </summary>
         /// <param name="affix"></param>
         /// <param name="arg"></param>
         /// <returns></returns>
         public bool TryGetValue(Affix affix, out IActualArg? arg) {
            foreach (Parameter subst in this) {
               if (subst.affix == affix) {
                  arg = subst.arg;
                  return true;
               }
            }
            arg = null;
            return false;
         }
      }
      /// <summary>
      /// 
      /// </summary>
      /// <param name="macro"></param>
      /// <param name="section"></param>
      /// <param name="callingProc"></param>
      /// <param name="parameters"></param>
      /// <param name="first"></param>
      /// <param name="elem"></param>
      /// <exception cref="NotImplementedException"></exception>
      private void GenerateMacroElement(Macro macro, Section section, Procedure? callingProc, Parameters parameters, bool first, IMacroElement elem) {
         switch (elem) {
            case INT i: cg.GenerateMacroElementInt(i.value); break;
            case FLOAT f: cg.GenerateMacroElementFloat(f.value); break;
            case STRING s: cg.GenerateMacroElementString(s.value, firstElement:first, quoted:false); break;
            case ID id:
               // This should be a reference to a Const, Var or List, so check which one
               if (section.TryGetDeclaration(id, out CDL2Object? obj)) {
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
               if (parameters.TryGetValue(aff, out IActualArg? arg)) {
                  Debug.Assert(callingProc is not null, $"GenerateMacro: Calling procedure is null for inlined macro {macro}");
                  switch (arg) {
                     case Var   vv: cg.GenerateMacroElementVar(vv, callingProc.CanFail, inlined: true); break;
                     case Const cc: cg.GenerateMacroElementConst(callingProc.Section!.GetResolvedConstant(cc)!); break;
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
      /// <summary>
      /// Generate the finalizers for all affixes and variables that need it.
      /// Variables and output and transput affixes can only be modified if the algorithm succeeds. Therefore generated local variables
      /// are used in the algorithm and the finalizer is used to copy the values from the local variables to the actual affixes and variables.
      /// </summary>
      /// <param name="algorithm"></param>
      /// <param name="variables"></param>
      private void FinalizeAffixesAndVariables(Algorithm algorithm,IEnumerable<Var> variables) {
         cg.GenerateAffixAndVariableFinalizationStart(algorithm);
         if (algorithm.NeedsFinalization) {
            foreach (Affix affix in algorithm.affixes) cg.GenerateAffixAndVariableFinalizer(algorithm, affix);
            foreach (Var var in variables) cg.GenerateAffixAndVariableFinalizer(algorithm, var, isVar: true);
         }
         cg.GenerateAffixAndVariableFinalizationEnd(algorithm);
      }

      /// <summary>
      /// Generate the header for an algorithm. This includes the affixes and locals that are used in the algorithm.
      /// </summary>
      /// <param name="alg"></param>
      /// <param name="variables"></param>
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
      /// <summary>
      /// Generate the comment for an algorithm. This is adds the pretty printed text of the algorthm as a comment.
      /// </summary>
      /// <param name="alg"></param>
      private void GenerateAlgorithmComment(Algorithm alg) {
         if (!alg.IsSynthetic) {
            sourceCommentPrinter.Print(alg);
            cg.GenerateSourceComment();
         } else {
            cg.GenerateComment(alg.ToString());
         }
      }
      /// <summary>
      /// Generate the code for a procedure.
      /// If the procedures is conditioanl compilation or it is inlined, only the algorithm comment is generated.
      /// </summary>
      /// <param name="proc"></param>
      private void GenerateProcedure(Procedure proc, int _) {
         if (proc.IsConditionalCompilation()) {
            GenerateAlgorithmComment(proc);
         } else if (proc.IsInlinable(Compiler.Reachable)) {
            GenerateAlgorithmComment(proc);
            cg.GenerateComment($"Procedure inlined");
         //} else if (!proc.IsSynthetic && proc.GetInliningParameters(Compiler.Reachable).NumberOfTimesCalled == 0) {
         //   GenerateAlgorithmComment(proc);
         //   cg.GenerateComment($"Procedure not invoked");
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

      /// <summary>
      /// Generate the body of a procedure.
      /// </summary>
      /// <param name="proc"></param>
      private void GenerateProcedureBody(Procedure proc) => GenerateAlternatives(proc, proc.group);
      /// <summary>
      /// Generate the alternatives for argAffix procedure.
      /// This method manages the conditional compilation of the alternatives based on whether the first call in the altarnative is conditional compilation on or off.
      /// TODO: Currently only single level of conditonal compilation is handled.
      /// If it is off, then no code is generated for that alternative.
      /// If it is on, then that alternative is generated, but all later alternatives are skipped.
      /// </summary>
      /// <param name="proc"></param>
      /// <param name="group"></param>
      private void GenerateAlternatives(Procedure proc, Group group) {
         bool supressRest = false;
         bool removed;
         
         int i = 1;
         foreach (Alternative alternative in group.Alternatives) {   
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
               GenerateAlternative(proc, group, alternative, supressRest || group.Alternatives.Count == i);
            }
            cg.GenerateAlternativeEnd(proc, group, i, alternative, removed);

            i++;
         }
      }
      /// <summary>
      /// Generate the code for an alternative.
      /// </summary>
      /// <param name="proc"></param>
      /// <param name="group"></param>
      /// <param name="alternative"></param>
      /// <param name="isLast"></param>
      /// <param name="parameters"></param>
      /// <exception cref="NotImplementedException"></exception>
      private void GenerateAlternative(Procedure proc,Group group,Alternative alternative,bool isLast, Parameters? parameters = null) {
         List<Call> calls = alternative.calls;
         bool canFail = false;
         foreach (Call call in calls) {
            GenerateCall(proc, call, canFail,parameters:parameters);
            canFail = canFail || call.CanFail;
         }
         switch (alternative.lastCall.type) {
            case LCT.Standard: GenerateCall(proc, alternative.lastCall.call!, canFail,onlyCallInAlternative:calls.Count == 0,lastAlternative:isLast,parameters); break;
            case LCT.Fail: cg.GenerateFail(proc, group); break;
            case LCT.Succeed: cg.GenerateSucceed(proc, group); break;
            case LCT.Abort: cg.GenerateAbort(proc, group); break;
            case LCT.Repeat: cg.GenerateRepeat(proc, group, alternative.lastCall.label!,canFail); break;
            case LCT.Group: GenerateGroup(proc, alternative.lastCall.group!); break;
            case LCT.None: break; // Used in the alternative generated for SectionById Ludes.
            default:
               throw new NotImplementedException($"GenerateAlternative: Unknown last call type {alternative.lastCall.type}");
         }
      }
      /// <summary>
      /// Generate the code for a group.
      /// </summary>
      /// <param name="proc"></param>
      /// <param name="group"></param>
      private void GenerateGroup(Procedure proc,Group group) {
         cg.GenerateGroupStart(proc,group);
         GenerateAlternatives(proc, group);
         cg.GenerateGroupEnd(proc,group);
      }
      /// <summary>
      /// Generate the code for a call.
      /// There are three main cases:
      /// <ol>
      /// <li>Inlined macros: macros with '=' body type.</li>
      /// <li>Inlined procedures: procedures that are inlineable or have ':=' body type.</li>
      /// <li>Actual calls on procedures that have ':" body type and are not inlinable and macros that have '=:' body type.</li>
      /// </ol>
      /// </summary>
      /// <param name="proc"></param>
      /// <param name="call"></param>
      /// <param name="canFail"></param>
      /// <param name="onlyCallInAlternative"></param>
      /// <param name="lastAlternative"></param>
      /// <param name="parameters"></param>
      /// <exception cref="NotImplementedException"></exception>
      private void GenerateCall(Procedure proc,Call call,bool canFail = false,bool onlyCallInAlternative=false,bool lastAlternative=false,Parameters? parameters = null) {
         if (call.IsConditionalCompilationOn) return;   // No need to generate code for this call;
         Algorithm? called = call.Called;
         if (called is not null) {
            if (!Settings.SettingValue<bool>("NoMacroInlining") && called is Macro macro && macro.IsInlineMacro) {
               cg.GenerateComment($"Inlining macro call -> {call}");
               cg.GenerateMacroInlineStart(macro);
               GenerateMacroBody(macro, proc, call.args,parameters);
               cg.GenerateMacroInlineEnd(macro);
            } else {
               Procedure calledProc = called as Procedure ?? throw new NotImplementedException($"GenerateCall: Called algorithm {called} is not a procedure");
               if (calledProc.IsInlinable(Compiler.Reachable)) {
                  cg.GenerateComment($"Inlining procedure call -> {call}");
                  GenerateAlternative(proc, calledProc.group, calledProc.group.Alternatives[0],isLast: false, new Parameters(parameters,calledProc.affixes, call.args));
               } else {
                  cg.GenerateCallStart(calledProc, proc, canFail, onlyCallInAlternative, lastAlternative);
                  parameters = new Parameters(parameters, calledProc.affixes, call.args);
                  if (parameters.Count > 0) {
                     GenerateActualArg(proc, call, calledProc.affixes[0], call.args[0]);
                     for (int i = 1 ; i < call.args.Count ; i++) {
                        cg.GenerateActualArgSeparator();
                        GenerateActualArg(proc, call, calledProc.affixes[i], call.args[i]);
                     }
                  }
                  cg.GenerateCallEnd(calledProc, proc, canFail, onlyCallInAlternative, lastAlternative);
               }
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
               cg.GenerateCallArgReferenceConst(calledAffix, proc.Section!.GetResolvedConstant(c)!);
               break;
            case Var v:
               cg.GenerateCallArgReferenceVar(calledAffix, v, needFinalization: call.Called!.NeedsFinalization);
               break;
            case ID id: // May be a reference to an affix or local of the calling proc or a const, or a var.
               if (proc.TryGetAffix(id,out Affix procAffix)) {
                  cg.GenerateCallArgReferenceAffix(calledAffix, procAffix, needFinalization: call.CanFail);
               } else if (proc.TryGetLocal(id,out Local local)) {
                  cg.GenerateCallArgReferenceLocal(calledAffix,local);
               } else if (proc.ParentElement<Section>()!.TryGetDeclaration(id,out CDL2Object? dataRef)) {
                  if (dataRef is Const c) {
                     Debug.Assert(!calledAffix.IsOutput,$"GenerateCallStart: Const argument for output affix {calledAffix}");
                     cg.GenerateCallArgReferenceConst(calledAffix, proc.Section!.GetResolvedConstant(c)!);
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
