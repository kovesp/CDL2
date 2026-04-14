// <auto-gen>
//=======================================================================
// <copyright file="CodeGenerator.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-02-10</creation-date>
// 
// <summary>
//   This is the target independent code generator. It collaborates with a selected target specific code generator to generate the target code.
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

// Ignore Spelling: CDL

using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;


namespace CDL2v1 {
   /// <summary>
   /// 
   /// </summary>
   /// <param Id="cg"></param>
   public class CodeGenerator(ICodeGenerator cg,CDL2 compiler,Action<Note,object[]> problemReporter) : CompilationPhase(compiler) {
      /// <summary>
      /// Target specific code generator to use.
      /// </summary>
      private readonly ICodeGenerator cg = cg;
      private readonly Action<Note,object[]> ReportProblem = problemReporter;

      /// <summary>
      /// Used to add the CDL2 code to each generated algorithm.
      /// Will <b>not</b> include the CDL2 comments and Notes associated with the algorithms.
      /// </summary>
      private readonly PrettyPrinter sourceCommentPrinter = new(cg.SourceEmitter,includeComments: false);

      private Reachable Reachable = new();
      /// <summary>
      /// Generate code for the unit and all its modulesWithLudes.
      /// If there is argAffix unit, then use its Ludes. Otherwise, use the Ludes from all modulesWithLudes.
      /// </summary>
      /// <param Id="modulesWithLudes"></param>
      /// <param Id="Emitter"></param>
      /// <param Id="isSeparate"></param>
      public void GenerateCode(Program program,Emitter emitter,string settings,bool isSeparate = false) {
         Reachable = program.Reachable;
         foreach (Var var in program.Reachable.Objects.OfType<Var>()) {
            if (Reachable.AmbigousVars.Contains(var)) {
               // We know the variable was written to, but we can't tell whether it was read (because it was only referenced in an ACTION/PREDICATE macro).
               var.AddNote("CodeGeneration",Note.VariableMayNotHaveBeenRead,var);
               ReportProblem(Note.VariableMayNotHaveBeenRead,[var]);
            } else if (!Reachable.ReadVars.Contains(var)) {
               // It must have been written, but not read.
               var.AddNote("CodeGeneration",Note.VariableNotRead,var);
               ReportProblem(Note.VariableNotRead,[var]);
            }
         }
         //DumpReachableObjects(program);

         ResetBuiltinLocals();

         sourceCommentPrinter.Emitter.Clear();

         if (!isSeparate) {
            // Generate an integrated program ignoring module boundaries of all objects reachable from the program's ludes.
            cg.GenerateProgramStart(program,emitter,settings,isSeparate: false);  // Generate the overall scaffolding


            IEnumerable<Var> allVars = Reachable.Objects.OfType<Var>();
            IEnumerable<LIST> allLists = Reachable.Objects.OfType<LIST>();

            GenerateObjects<Const>(Reachable.Objects.OfType<Const>(),GenerateConstant);
            GenerateObjects<Var>(allVars,GenerateVar);
            GenerateObjects<LIST>(allLists,GenerateList);

            IOrderedEnumerable<Macro> macros = Reachable.Objects.OfType<Macro>().Where(alg => !alg.IsInlinable()).OrderBy(p => p.Id.Name);
            IOrderedEnumerable<Procedure> procedures = Reachable.Objects.OfType<Procedure>()
                                                            .Where(proc => !proc.IsSynthetic && !proc.IsConditionalCompilation() && !proc.IsInlinable(Reachable))
                                                            .OrderBy(p => p.Id.Name);

            // Order ludes by type: PRELUDE, ROOT, POSTLUDE
            IEnumerable<Procedure> syntheticProcedures = Reachable.Objects.OfType<Procedure>()
                .Where(proc => proc.IsSynthetic)
                .OrderBy(p => p.Id.Name.Contains("PRELUDE") ? 0 : p.Id.Name.Contains("ROOT") ? 1 : p.Id.Name.Contains("POSTLUDE") ? 2 : 3)
                .ThenBy(p => p.Id.Name);
            IEnumerable<Procedure> nonInlinedSyntheticProcedures = syntheticProcedures.Where(p => !p.IsInlinable(Reachable));

            // Ludes first (ordered as PRELUDE, ROOT, POSTLUDE), then algorithms sorted by container then by name
            IEnumerable<Algorithm> allProcs = syntheticProcedures
                .Cast<Algorithm>()
                .Concat(macros.Cast<Algorithm>().Concat(procedures).OrderBy(p => p.ParentElement<Section>()?.ParentElement<Container>()?.Id.Name ?? "").ThenBy(p => p.Id.Name));
            Dictionary<CDL2Object,int> procIndex = allProcs.Select((proc,index) => (proc,index)).ToDictionary(pair => (CDL2Object)pair.proc,pair => pair.index);

            GenerateCodePredeclarations(macros,procedures,nonInlinedSyntheticProcedures);

            GenerateObjects<Macro>(macros,GenerateMacro,itemIndex: procIndex);
            GenerateObjects<Procedure>(procedures,GenerateProcedure,itemIndex: procIndex);
            GenerateObjects<Procedure>(nonInlinedSyntheticProcedures,GenerateProcedure,"Synthetic Procedure",itemIndex: procIndex);

            GenerateDebugInfo(allVars,allLists,allProcs);

            cg.GenerateListInitializers(allLists);

            sourceCommentPrinter.Print(program);
            cg.GenerateSourceComment();
            cg.GenerateProgramLudesStart();
            cg.GenerateComment("Program Ludes");
            foreach (RW ludeType in Container.LudeTypes) foreach (Module mod in program.Lude(ludeType)) GenerateModuleLude(ludeType,mod,wrapped: false);
            cg.GenerateProgramLudesEnd();
            cg.GenerateProgramEnd(program);
         } else {
            // TODO: Needs work to handle generating modules as separate units.
            cg.GenerateProgramStart(program,emitter,settings,isSeparate: true);  // Generate the overall scaffolding
            sourceCommentPrinter.Print(program);
            cg.GenerateSourceComment();
            foreach (ID modId in program.Parts) cg.GenerateProgramPart(program,modId,isSeparate);

            GenerateProgramLudes(program);
            cg.GenerateProgramEnd(program);
            foreach (Module mod in program.Modules) GenerateModule(mod,isSeparate: true);
         }
      }

      /// <summary>
      /// Generates predeclaration code for macros, procedures, and synthetic procedures if required by the code
      /// generation context.
      /// </summary>
      /// <remarks>Predeclarations are generated only if the code generation context indicates that they are
      /// necessary. This method groups the predeclarations by type for clarity in the generated output.</remarks>
      /// <param name="macros">An ordered collection of macros for which forward declarations will be generated.</param>
      /// <param name="procedures">An ordered collection of procedures for which forward declarations will be generated.</param>
      /// <param name="nonInlinedSyntheticProcedures">A collection of synthetic procedures that are not inlined and require forward declarations.</param>
      private void GenerateCodePredeclarations(IOrderedEnumerable<Macro> macros,IOrderedEnumerable<Procedure> procedures,IEnumerable<Procedure> nonInlinedSyntheticProcedures) {
         if (cg.RequiresPredeclaration) {
            GenerateObjects<Macro>(macros,GeneratePredeclaration,"Macro Forward Declaration");
            GenerateObjects<Procedure>(procedures,GeneratePredeclaration,"Procedure Forward Declaration");
            GenerateObjects<Procedure>(nonInlinedSyntheticProcedures,GeneratePredeclaration,"Synthetic Procedure Forward Declaration");
         }
      }

      /// <summary>
      /// If the target code gnerator supports debug info and backtrace is set, generate the debug info for all variables, lists and procedures.
      /// The specific format of the debug info is determined by the target code generator.
      /// </summary>
      /// <param name="allVars"></param>
      /// <param name="allLists"></param>
      /// <param name="allProcs"></param>
      private void GenerateDebugInfo(IEnumerable<Var> allVars,IEnumerable<LIST> allLists,IEnumerable<Algorithm> allProcs) {
         if (cg.SupportsDebug && Settings.IsBacktrace) {
            cg.GenerateDebugInfoStart();

            if (cg.SupportsSimpleDebug) {
               cg.GenerateDebugInfoProcsStart(allProcs);
               cg.GenerateDebugInfoVarsStart(allVars);
               cg.GenerateDebugInfoListsStart(allLists);
            } else {
               cg.GenerateDebugInfoProcsStart(allProcs);
               foreach (Algorithm proc in allProcs) cg.GenerateDebugInfoProc(proc);
               cg.GenerateDebugInfoProcsEnd(allProcs);

               cg.GenerateDebugInfoVarsStart(allVars);
               foreach (Var var in allVars) cg.GenerateDebugInfoVar(var);
               cg.GenerateDebugInfoVarsEnd(allVars);

               cg.GenerateDebugInfoListsStart(allLists);
               foreach (LIST list in allLists) cg.GenerateDebugInfoList(list);
               cg.GenerateDebugInfoListEnd(allLists);
            }
            cg.GenerateDebugInfoEnd();
         }
      }


      /// <summary>
      /// Reset all builtins to cause their values to be recomputed.
      /// </summary>
      private static void ResetBuiltinLocals() {
         foreach (Local local in Database.Instance.NamedElements.Values.OfType<Local>()) local.ResetBuiltinResult();
      }

      /// <summary>
      /// Generate the program prelude, root and postlude.
      /// </summary>
      /// <param name="program"></param>
      private void GenerateProgramLudes(Program program) {
         foreach (RW ludeType in Container.LudeTypes) {
            IEnumerable<Module> modulesWithLudes = program.Ludes[ludeType].Select(id => Database.Instance.ModuleByName(id)!).Where(mod => mod.Ludes[ludeType].Count > 0);
            if (modulesWithLudes.Any()) {
               cg.GenerateProgramLudeStart(ludeType,program);
               foreach (Module module in modulesWithLudes) cg.GenerateProgramLude(ludeType,program,module);
               cg.GenerateProgramLudeEnd(ludeType,program);
            }
         }
      }

      /// <summary>
      /// Generate code for module. It is up to the specific code generator to determine whether this code goes into a separate file or not.
      /// </summary>
      /// <param Id="module"></param>
      private void GenerateModule(Module module,bool isSeparate) {
         static void GenerateImpEx<T>(IDDictionary<T> impexList,Action<T> generateImpEx) {
            foreach (T impex in impexList.Values) {
               generateImpEx(impex);
            }
         }

         cg.GenerateModuleStart(module,isSeparate);

         cg.GenerateImpExStart(module);
         GenerateImpEx(module.exports,cg.GenerateExport);
         GenerateImpEx(module.imports,cg.GenerateImport);
         cg.GenerateImpExEnd(module);

         foreach (Layer layer in module.Layers) GenerateLayer(layer);

         foreach (RW ludeType in Container.LudeTypes) GenerateModuleLude(ludeType,module,wrapped: true);

         cg.GenerateModuleEnd(module,isSeparate);
      }
      /// <summary>
      /// Generate code for the module ludes. There is one for each type if it exists.
      /// </summary>
      /// <param name="ludeType"></param>
      /// <param name="module"></param>
      /// <param name="wrapped"></param>
      private void GenerateModuleLude(RW ludeType,Module module,bool wrapped) {
         IEnumerable<Section?> SectionsWithLudes = module.Ludes[ludeType].Select(id => module.SectionById(id)).Where(sec => sec?.Ludes[ludeType].Count > 0) ?? [];
         if (SectionsWithLudes.Any()) {
            cg.GenerateModuleLudeStart(ludeType,module,wrapped: wrapped);
            foreach (Section? section in SectionsWithLudes) {
               Guid? ludeGuid = section?.LudeProcs[ludeType];
               if (ludeGuid is not null && ludeGuid != Guid.Empty) {
                  Procedure? ludeProc = ludeGuid?.ToNamedElement<Procedure>();
                  if (ludeProc is not null) {
                     if (ludeProc.IsInlinable(Reachable)) {
                        cg.GenerateComment($"Inlining {ludeType}");
                        GenerateAlgorithmComment(ludeProc);
                        GenerateProcedureBody(ludeProc);
                     } else {
                        cg.GenerateModuleLude(ludeType,module,section!);
                     }
                  }
               }
            }
            cg.GenerateModuleLudeEnd(ludeType,module,wrapped: wrapped);
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
      /// So generate the constants, variables and lists followed by the algorithms. Macros first, then user procedures and synthetic procedures (the ludes).
      /// </summary>
      /// <param Id="section"></param>
      private void GenerateSection(Section section) {
         cg.GenerateSectionStart(section);
         GenerateObjects<Const>(section.Constants,GenerateConstant);
         GenerateObjects<Var>(section.Variables,GenerateVar);
         GenerateObjects<LIST>(section.Lists,GenerateList);

         // Must be done after all data declarations  ... requirement in C.
         cg.GenerateListInitializers(section.Lists);

         if (cg.RequiresPredeclaration) {
            GenerateObjects<Macro>(section.Macros,GeneratePredeclaration,"Macro Declrations");
            GenerateObjects<Procedure>(section.NonSyntheticProcedures,GeneratePredeclaration,"Procedure Declrations");
            GenerateObjects<Procedure>(section.SyntheticProcedures,GeneratePredeclaration,"Synthetic Procedure Declrations");
         }

         GenerateObjects<Macro>(section.Macros,GenerateMacro);
         GenerateObjects<Procedure>(section.NonSyntheticProcedures,GenerateProcedure);
         GenerateObjects<Procedure>(section.SyntheticProcedures,GenerateProcedure,"Synthetic Procedure");

         cg.GenerateSectionEnd(section);
      }

      private void GeneratePredeclaration(Algorithm alg,int _,int __) {
         if (!alg.IsInlinable(Reachable)) cg.GenerateDeclaration(alg);
      }

      /// <summary>
      /// Generate code for a list.
      /// </summary>
      /// <param name="list"></param>
      /// <exception cref="NotImplementedException"></exception>
      private void GenerateList(LIST list,int _,int __) {
         Section section = list.ParentElement<Section>()!;
         if (section.TryGetDeclaration(list.lwb,out Const? lwb) && section.TryGetDeclaration(list.upb,out Const? upb)) {
            cg.GenerateList(list,lwb!,upb!);
         } else {
            throw new NotImplementedException($"GenerateSection: Could not find lower or upper bound for {list}");
         }
      }

      /// <summary>
      /// Generate code for a variable.
      /// </summary>
      /// <param name="v"></param>
      private void GenerateVar(Var v,int _,int __) => cg.GenerateVar(v);

      /// <summary>
      /// Generate code for a list of objects
      /// </summary>
      /// <typeparam name="T">A CDL2Object, so Algorithm, LIST, Var, Const </typeparam>
      /// <param name="items"></param>
      /// <param name="generate"></param>
      /// <param name="specialType"></param>
      private void GenerateObjects<T>(IEnumerable<NamedElement> items,Action<T,int,int> generate,string? specialType = null,Dictionary<CDL2Object,int>? itemIndex = null) where T : CDL2Object {
         if (items.Any()) {
#if AllignNames
            int maxNameLength = items.Select(item=>item.Id.InternalName.Length).Max();
#else
            int maxNameLength = 0;
#endif
            cg.GenerateObjectSectionStart<T>(items,specialType ?? typeof(T).Name);
            foreach (T item in items.Cast<T>()) generate(item,maxNameLength,itemIndex is null ? 0 : itemIndex[item]);
            cg.GenerateObjectSectionEnd<T>(items,typeof(T).Name);
         }
      }
      /// <summary>
      /// Generate code for a constant.
      /// </summary>
      /// <param name="constant"></param>
      /// <exception cref="NotImplementedException"></exception>
      private void GenerateConstant(Const constant,int _,int __) {
         Section section = constant.ParentElement<Section>()!;
         cg.GenerateConstantStart(constant);
         foreach (IElement elem in constant.elements) {
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
      private void GenerateMacro(Macro macro,int _,int index) {
         if (!Settings.IsInliningMacros || !macro.IsInlineMacro) {
            IEnumerable<Var> variables = macro.GetReferencedVariables();
            cg.GenerateMacroStart(macro);
            GenerateAlgorithmHeader(macro,variables,index);
            GenerateMacroBody(macro);
            FinalizeAffixesAndVariables(macro,variables);
            cg.GenerateMacroEnd(macro);
         }
      }

      /// <summary>
      /// Generate the body of a macro.
      /// </summary>
      /// <param name="macro"></param>
      /// <param name="callingProc"></param>
      /// <param name="args"></param>
      /// <param name="aList"></param>
      /// <remarks>
      /// Note that GenerateReturnExpression start and end must either do nothing (as e.g., for PowerShell)
      /// or generate code that returns the value of the expression. In that case it must itself check that whether
      /// the macro can fail.
      /// </remarks>
      /// <param name="inlining"></param>
      private void GenerateMacroBody(Macro macro,Procedure? callingProc = null,Alternative? alternative = null,
         int nextAlt = Alternative.ALTERNATIVES_END,List<IActualArg>? args = null,AList? aList = null,bool inlining = false,bool inLastAlternative = false,bool lastCall = false) {
         aList = new(aList,macro.Affixes,args ?? []);
         bool first = true;
         List<IElement> lastExpression;

         cg.GenerateMacroBodyStart(macro,inlining);
         if (macro.CanFail) { // Split the elements so the conditional logic can be applied to the last expression.
            (List<IElement> beforeLast,lastExpression) = TargetCodeGenerator.SplitMacroBody(macro,cg.StatementSeparators);
            foreach (IElement elem in beforeLast) {
               GenerateMacroElement(macro,macro.Section!,callingProc,aList,first,elem);
               first = false;
            }
         } else {
            lastExpression = macro.Elements;
         }
         if (inlining) cg.GenerateMacroInlineStart(macro); else cg.GenerateReturnExpressionStart(macro);
         if (macro.CanFail && lastExpression.First() is STRING str) str.value = str.value.TrimStart();
         foreach (IElement elem in lastExpression) {
            GenerateMacroElement(macro,macro.Section!,callingProc,aList,first,elem);
            first = false;
         }
         if (inlining) {
            cg.GenerateMacroInlineEnd(macro,callingProc!,nextAlt,inLastAlternative);
         } else {
            cg.GenerateReturnExpressionEnd(macro,nextAlt);
         }
         cg.GenerateMacroBodyEnd(macro,inlining,lastCall);
      }


      /// <summary>
      /// Represents a list of affix mappings for an Algorithm call.
      /// This is essentially an association list as pioneered by Lisp.
      /// </summary>
      private class AList : Dictionary<Affix,IActualArg> {
         public AList() : base() { }

         /// <summary>
         /// Construct a parameter list from a list of affixes and actual arguments.
         /// If an actual argument is an Affix, then it is replaced with the corresponding argument from the parameters list.
         /// This implements actual args cascaded through multiple procedure inlinings.
         /// When generating standalone macros, args may be empty while affixes exist - in this case, affixes without args
         /// will be treated as formal parameters by GenerateMacroElement.
         /// </summary>
         /// <param name="aList"></param>
         /// <param name="affixes"></param>
         /// <param name="args"></param>
         public AList(AList? aList,List<Affix> affixes,List<IActualArg> args) : base() {
            aList ??= [];
            int argCount = Math.Min(affixes.Count,args.Count);
            for (int i = 0 ; i < argCount ; i++) {
               this[affixes[i]] = args[i] is Affix aff && aList.TryGetValue(aff,out IActualArg? arg) ? arg : args[i];
            }
         }
         /// <summary>
         /// Try to get the value of an affix from the list.
         /// </summary>
         /// <param name="affix"></param>
         /// <param name="arg"></param>
         /// <returns></returns>

         /// <summary>
         /// Retrieves the value associated with the specified argument if it is of type Affix; otherwise, returns the
         /// original argument.
         /// </summary>
         /// <remarks>If the provided argument is not of type Affix, this method returns the argument
         /// unchanged.</remarks>
         /// <param name="arg">The argument for which to retrieve the value. If this argument is of type Affix, its associated value is
         /// returned.</param>
         /// <returns>An instance of IActualArg representing the value associated with the argument if it is an Affix; otherwise,
         /// the original argument.</returns>
         public IActualArg GetValue(IActualArg arg) => arg is Affix affix && TryGetValue(affix,out IActualArg? aarg) ? aarg : arg;


         public bool TryGetValue(ID id,[NotNullWhen(true)] out IActualArg? arg) {
            foreach (Affix aff in Keys) {
               if (aff.Id == id) {
                  arg = this[aff];
                  return true;
               }
            }
            arg = id;
            return false;
         }
      }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="macro"></param>
      /// <param name="section"></param>
      /// <param name="callingProc"></param>
      /// <param name="aList"></param>
      /// <param name="first"></param>
      /// <param name="elem"></param>
      /// <exception cref="NotImplementedException"></exception>
      private void GenerateMacroElement(Macro macro,Section section,Procedure? callingProc,AList aList,bool first,IElement elem) {
         switch (elem) {
            case INT i: cg.GenerateMacroElementInt(i.value); break;
            case FLOAT f: cg.GenerateMacroElementFloat(f.value); break;
            case STRING s: GenerateMacroElementString(s,first: first,quoted: false); break;
            case ID id:
               if (macro.TryGetAffix(id,out Affix aff)) {
                  if (aList.TryGetValue(aff,out IActualArg? arg)) {
                     Debug.Assert(callingProc is not null,$"GenerateMacro: Calling procedure is null for inlined macro {macro}");
                     if (arg is ID aid) {
                        if (section.TryGetDeclaration(aid,out CDL2Object? argObj) && argObj is IActualArg aaArg) {
                           arg = aaArg;
                        } else {
                           Debugger.Break();
                           throw new NotImplementedException($"GenerateMacro: Unresolved reference to {arg}");
                        }
                     }
                     switch (arg) {
                        case Var vv: cg.GenerateMacroElementVar(vv,callingProc.CanFail,inlined: true); break;
                        case Const cc: cg.GenerateMacroElementConst(cc!); break;
                        case Local ll:
                           if (ll.IsBuiltinResult) {
                              cg.GenerateMacroElementString(ll.BuiltinResult,firstElement: first,quoted: true);
                           } else {
                              cg.GenerateMacroElementLocal(ll,aff);
                           }
                           break;
                        case Affix aa: cg.GenerateMacroElementAffix(aa,callingProc.CanFail); break;
                        case STRING s: GenerateMacroElementString(s,first: false,quoted: true); break;
                        default:
                           Debugger.Break();
                           throw new NotImplementedException($"GenerateMacro: Reference to unresolved element {arg}");
                     }
                  } else {
                     cg.GenerateMacroElementAffix(aff,macro.CanFail);
                  }
               } else if (macro.TryGetLocal(id,out Local loc)) {
                  cg.GenerateMacroElementLocal(loc,aff);
               } else if (section.TryGetDeclaration(id,out CDL2Object? obj)) { // This should be a reference to an affix, local, Const, Var or List, so check which one
                  switch (obj) {
                     case Const c: cg.GenerateMacroElementConst(c); break;
                     case Var v: cg.GenerateMacroElementVar(v,macro.CanFail); break;
                     case LIST l: cg.GenerateMacroElementList(l); break;
                     default:
                        throw new NotImplementedException($"GenerateMacro: Reference to wrong element type {obj}");
                  }
               } else {
                  throw new NotImplementedException($"GenerateMacro: Unresolved reference to {id}");
               }
               break;
            default:
               throw new NotImplementedException($"GenerateMacro: Unknown element type {elem.GetType()}");
         }
      }

      private void GenerateMacroElementString(STRING s,bool first,bool quoted)
         => cg.GenerateMacroElementString(s.value.Replace("$#",cg.LineComment),firstElement: first,quoted: quoted);

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
            foreach (Affix affix in algorithm.Affixes) cg.GenerateAffixAndVariableFinalizer(algorithm,affix);
            foreach (Var var in variables) cg.GenerateAffixAndVariableFinalizer(algorithm,var,isVar: true);
         }
         cg.GenerateAffixAndVariableFinalizationEnd(algorithm);
      }

      /// <summary>
      /// Generate the header for an algorithm. This includes the affixes and locals that are used in the algorithm.
      /// </summary>
      /// <param name="alg"></param>
      /// <param name="variables"></param>
      private void GenerateAlgorithmHeader(Algorithm alg,IEnumerable<Var> variables,int algIndex) {
         GenerateAlgorithmComment(alg);

         cg.GenerateAlgorithmHeaderStart(alg);
         GenerateAlgorithmAffixes(cg,alg);
         cg.GenerateAlgorithmHeaderEnd(alg);

         cg.GenerateAffixAndVariableInitializationStart(alg);
         if (alg.NeedsFinalization) {
            foreach (Affix affix in alg.Affixes) cg.GenerateAffixAndVariableInitializer(alg,affix);
            foreach (Var var in variables) cg.GenerateAffixAndVariableInitializer(alg,var,isVar: true);
         }
         GenerateLocalInitializers(alg,algIndex,CollectLocals(alg));
         cg.GenerateAffixAndVariableInitializationEnd(alg);
      }

      /// <summary>
      /// Generates the affix declarations in the header.
      /// Can be used by target code generators.
      /// </summary>
      /// <param name="cg"></param>
      /// <param name="alg"></param>
      public static void GenerateAlgorithmAffixes(ICodeGenerator cg,Algorithm alg) {
         if (alg.Affixes.Count > 0) {
            cg.GenerateAffix(alg.Affixes[0],alg.Affixes[0].affixDir,alg.CanFail);
            foreach (Affix affix in alg.Affixes.Skip(1)) {
               cg.GenerateAffixSeparator();
               cg.GenerateAffix(affix,affix.affixDir,alg.CanFail);
            }
         }
      }

      /// <summary>
      /// Generates initializers for all local variables defined in the specified algorithm.
      /// </summary>
      /// <param name="alg">The algorithm containing the local variables for which initializers will be generated. Cannot be null.</param>
      /// <remarks>Notice that nothing is genrated for built-in result locals ... these are virtual.</remarks>
      /// <param name="algIndex"></param>
      private void GenerateLocalInitializers(Algorithm alg,int algIndex) => GenerateLocalInitializers(alg,algIndex,alg.Locals);
      private void GenerateLocalInitializers(Algorithm alg,int algIndex,IEnumerable<Local> locals) {
         foreach (Local local in locals) if (!local.IsBuiltinResult) cg.GenerateLocal(local);
         cg.GenerateTraceEnter(alg,locals,algIndex);
      }

      /// <summary>
      /// Generate the comment for an algorithm. This is adds the pretty printed text of the algorithm as a comment.
      /// </summary>
      /// <param name="alg"></param>
      /// <param name="nl"></param>
      private void GenerateAlgorithmComment(Algorithm alg,bool nl = true) {
         //if (!alg.IsSynthetic) {
         sourceCommentPrinter.Print(alg,synthetics: true);
         cg.GenerateSourceComment(nl: false);
         //} else {
         //   cg.GenerateComment(alg.FQDN());
         //}
      }
      /// <summary>
      /// Generate the code for a procedure.
      /// If the procedures is conditional compilation or it is inlined, only the algorithm comment is generated.
      /// </summary>
      /// <param name="proc"></param>
      private void GenerateProcedure(Procedure proc,int _,int index) {
         if (proc.IsConditionalCompilation()) {
            GenerateAlgorithmComment(proc);
         } else if (proc.IsSynthetic || !proc.IsInlinable(Reachable)) {
            proc.AlgId = index;
            IEnumerable<Var> variables = proc.GetReferencedVariables();
            cg.GenerateProcedureStart(proc);
            GenerateAlgorithmHeader(proc,variables,index);
            cg.GenerateProcedureBodyStart(proc,proc.ProcedureBodyType);
            GenerateProcedureBody(proc);
            cg.GenerateProcedureBodyEnd(proc,proc.ProcedureBodyType);
            FinalizeAffixesAndVariables(proc,variables);
            cg.GenerateProcedureEnd(proc);
         }
      }

      /// <summary>
      /// Collects the locals defined by the group itself as well as inlined calls.
      /// Applies only to Procedures. Locals for macros are handled in GenerateMacroBody.
      /// </summary>
      /// /// <param name="alg">The algorithm from which to collect local variables. Must be of type Procedure to retrieve its locals.</param>
      /// <returns>A set of local variables defined in the provided algorithm. Returns an empty set if the algorithm is not a
      /// Procedure.</returns>
      /// <remarks>Since proc.Locals always generates a new set, there is no need to copy it.</remarks>
      public Set<Local> CollectLocals(Algorithm alg) => alg switch {
         Procedure proc => CollectLocals(proc.group,[.. proc.Locals.Where(local => !local.IsBuiltinResult)]),
         Macro macro => macro.IsInlinable() ? [] : [.. macro.Locals],
         _ => []
      };

      /// <summary>
      /// Collects the locals of inlined calls in the group and adds them to locals.
      /// </summary>
      /// <param name="group"></param>
      /// <param name="locals"></param>
      private Set<Local> CollectLocals(Group group,Set<Local> locals) {
         foreach (Alternative alternative in group.Alternatives) {
            if (alternative.IsConditionalCompilationOff) continue;
            CollectLocals(alternative,locals);
            if (alternative.IsConditionalCompilationOn) break;
         }
         return locals;
      }

      /// <summary>
      /// Collects local variables from all inlined calls within the specified alternative and adds them to the provided
      /// collection.
      /// </summary>
      /// <remarks>This method processes each call in the alternative, including the final call, to ensure
      /// all relevant local variables are gathered. It supports both standard and grouped call types within the
      /// alternative.</remarks>
      /// <param name="alternative">The alternative containing the sequence of calls from which local variables are to be collected.</param>
      /// <param name="locals">An enumerable collection that receives the local variables identified from the calls in the alternative.</param>
      private void CollectLocals(Alternative alternative,Set<Local> locals) {
         foreach (Call call in alternative.Calls) CollectLocals(call,locals);
         switch (alternative.LastCall.type) {
            case LCT.Standard: CollectLocals(alternative.LastCall.call!,locals); break;
            case LCT.Group: CollectLocals(alternative.LastCall.group!,locals); break;
         }
      }

      /// <summary>
      /// Collects locals from the specified call and adds them to the provided collection.
      /// </summary>
      /// <param name="call"></param>
      /// <param name="locals"></param>
      private void CollectLocals(Call call,Set<Local> locals) {
         if (!call.IsConditionalCompilationOn && !call.IsBuiltin) {
            Algorithm? called = call.Called;
            if (called is not null) {
               if (called is Macro macro && Settings.IsInliningMacros && macro.IsInlineMacro) {
                  locals.AddAll(macro.Locals);
               } else if (called is Procedure proc && Settings.IsInliningProcs && proc.IsInlinable(Reachable)) {
                  locals.AddAll(proc.Locals.Where(local => !local.IsBuiltinResult));
                  CollectLocals(proc.group,locals);
               }
            }
         }
      }

      /// <summary>
      /// Generate the body of a procedure.
      /// </summary>
      /// <param name="proc"></param>
      private void GenerateProcedureBody(Procedure proc) => GenerateGroup(proc,proc.group);
      /// <summary>
      /// Generate the alternatives for argAffix procedure.
      /// This method manages the conditional compilation of the alternatives based on whether the first call in the alternative is conditional compilation on or off.
      /// TODO: Currently only single level of conditional compilation is handled.
      /// If it is off, then no code is generated for that alternative.
      /// If it is on, then that alternative is generated, but all later alternatives are skipped.
      /// </summary>
      /// <param name="proc"></param>
      /// <param name="group"></param>
      private void GenerateAlternatives(Procedure proc,Group group) {
         bool supressRest = false;

         int i = 1;
         bool removed;
         foreach (Alternative alternative in group.Alternatives) {
            removed = false;
            if (supressRest) {
               cg.GenerateComment($"{alternative} suppressed by previous conditional compilation ON");
               removed = true;
            } else if (alternative.IsConditionalCompilationOff) {           // Ignore this alternative
               cg.GenerateComment($"{alternative} removed by conditional compilation OFF");
               removed = true;
            } else {
               supressRest = alternative.IsConditionalCompilationOn;       // Ignore following alternatives
               bool islast = alternative.IsLastAlternative;
               cg.GenerateAlternativeStart(proc,group,i,islast);
               GenerateAlternative(proc,group,alternative,islast,nextAlternative: alternative.NextAlternativeNumber);
               cg.GenerateAlternativeEnd(proc,group,i,alternative,removed,islast);
            }

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
      /// <param name="aList"></param>
      /// <exception cref="NotImplementedException"></exception>
      /// <param name="nextAlternative"></param>
      /// <param name="inlining"></param>
      private void GenerateAlternative(Procedure proc,Group group,Alternative alternative,bool isLast,AList? aList = null,
               int nextAlternative = Alternative.ALTERNATIVES_END,bool inlining = false) {
         List<Call> calls = alternative.Calls;
         bool canFail = false;
         foreach (Call call in calls) {
            GenerateCall(proc,alternative,call,canFail,currentAList: aList,lastAlternative: isLast,nextAlternative: nextAlternative);
            canFail = canFail || call.CanFail;
         }
         switch (alternative.LastCall.type) {
            case LCT.Standard:
               GenerateCall(proc,alternative,alternative.LastCall.call!,canFail,onlyCallInAlternative: calls.Count == 0,
                              lastAlternative: isLast,currentAList: aList,nextAlternative: nextAlternative,lastCall: true); break;
            case LCT.Fail: cg.GenerateFail(proc,group); break;
            case LCT.Succeed: cg.GenerateSucceed(proc,group); break;
            case LCT.Abort: cg.GenerateAbort(proc,group,proc.AlgId); break;
            case LCT.Repeat: cg.GenerateRepeat(proc,group,alternative.LastCall.label!,canFail); break;
            case LCT.Group: GenerateGroup(proc,alternative.LastCall.group!); break;
            case LCT.None: break; // Used in the alternative generated for SectionById Ludes.
            default:
               throw new NotImplementedException($"GenerateAlternative: Unknown last call type {alternative.LastCall.type}");
         }
      }
      /// <summary>
      /// Generate the code for a group.
      /// </summary>
      /// <param name="proc"></param>
      /// <param name="group"></param>
      private void GenerateGroup(Procedure proc,Group group) {
         cg.GenerateGroupStart(proc,group);
         GenerateAlternatives(proc,group);
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
      /// <param name="currentAList"></param>
      /// <exception cref="NotImplementedException"></exception>
      private void GenerateCall(Procedure proc,Alternative alternative,Call call,bool canFail = false,bool onlyCallInAlternative = false,
                                 bool lastAlternative = false,AList? currentAList = null,int nextAlternative = 0,bool lastCall = false) {
         currentAList ??= [];
         if (call.IsConditionalCompilationOn) {
            cg.GenerateComment($"Alternative selected by conditional compilation ON -> {call}");
            return;   // No need to generate code for this call;
         }
         if (call.IsBuiltin && Builtin.IsFunction(call)) {
            // Ignore the call here. The value of the builtin will be inserted directly where needed.
            cg.GenerateComment($"{call} -> {Builtin.EvalFunction(call)}");
         } else {
            Algorithm? called = call.Called;
            if (called is not null) {
               if (Settings.IsInliningMacros && called is Macro macro && macro.IsInlineMacro) {
                  cg.GenerateComment($"Inlining macro call -> {call}");
                  GenerateAlgorithmComment(called,nl: false);
                  GenerateMacroBody(macro,proc,alternative,nextAlternative,args: [.. call.Args],aList: currentAList,
                     inlining: true,inLastAlternative: lastAlternative,lastCall: lastCall);
               } else if (Settings.IsInliningProcs && called is Procedure calledProc && calledProc.IsInlinable(Reachable)) {
                  cg.GenerateComment($"Inlining procedure call -> {call} ({calledProc.inliningParameters?.Display() ?? "?"})");
                  GenerateAlgorithmComment(called,nl: false);
                  // The following works because currently only procedures with a single alternative are inlineable.
                  GenerateAlternative(proc,calledProc.group,calledProc.group.Alternatives[0],isLast: lastAlternative,
                     aList: new AList(currentAList,calledProc.Affixes,call.Args),nextAlternative: nextAlternative,inlining: true);
               } else {
                  cg.GenerateCallStart(called,proc,canFail,onlyCallInAlternative,lastAlternative);
                  AList aList = new(currentAList,called.Affixes,call.Args);
                  called.Affixes.GenerateJoinedSequence(GenerateActualArg(proc,call,aList),cg.GenerateActualArgSeparator);
                  cg.GenerateCallEnd(called,proc,alternative,canFail,onlyCallInAlternative,lastAlternative,nextAlternative);
               }
            } else {
               cg.GenerateComment($"Call to undefined algorithm {call} skipped.");
            }
         }
      }

      private Action<IActualArg> GenerateActualArg(Procedure proc,Call call,AList aList) => aff => GenerateActualArg(proc,call,(Affix)aff,aList.GetValue(aff));
      private void GenerateActualArg(Procedure proc,Call call,Affix affix,IActualArg actualArg) {
         switch (actualArg) {
            case STRING s:
               Debug.Assert(affix.affixType == AT.str,$"GenerateCallStart: String argument for non-string affix {affix}");
               cg.GenerateCallArgString(s.value);
               break;
            case Const c:
               cg.GenerateCallArgReferenceConst(affix,c!);
               break;
            case Var v:
               cg.GenerateCallArgReferenceVar(affix,v!,needsFinalization: proc.NeedsFinalization);
               break;
            case Local local:
               if (local.IsBuiltinResult) {
                  cg.GenerateCallArgString(local.BuiltinResult);
               } else {
                  cg.GenerateCallArgReferenceLocal(affix,local);
               }
               break;
            case Affix argAffix:
               cg.GenerateCallArgReferenceAffix(affix,argAffix,proc.NeedsFinalization);
               break;
            case ID id: // May be a reference to an affix or local of the calling proc or a const, or a var.
               if (proc.TryGetAffix(id,out Affix procAffix)) {
                  cg.GenerateCallArgReferenceAffix(affix,procAffix,needFinalization: proc.NeedsFinalization/*call.CanFail*/);
               } else if (proc.TryGetLocal(id,out Local local)) {
                  if (local.IsBuiltinResult) {
                     cg.GenerateCallArgString(local.BuiltinResult);
                  } else {
                     cg.GenerateCallArgReferenceLocal(affix,local);
                  }
               } else if (proc.ParentElement<Section>()!.TryGetDeclaration(id,out CDL2Object? dataRef)) {
                  if (dataRef is Const c) {
                     Debug.Assert(!affix.IsOutput,$"GenerateCallStart: Const argument for output affix {affix}");
                     cg.GenerateCallArgReferenceConst(affix,proc.Section!.GetResolvedConstant(c)!);
                  } else if (dataRef is Var v) {
                     cg.GenerateCallArgReferenceVar(affix,v,needsFinalization: proc.NeedsFinalization);
                  } else {
                     throw new NotImplementedException($"GenerateCallStart: Reference to wrong element type {dataRef}");
                  }
               } else {
                  throw new NotImplementedException($"GenerateCallStart: Unresolved reference to {id}");
               }
               break;
            default:
               throw new NotImplementedException($"GenerateCallStart: Unknown argument type {actualArg.GetType()}");
         }
      }

      #region Find target code generators

      public static readonly Dictionary<string,Type> AvailableCodeGenerators = [];

      private static readonly Dictionary<string,ICodeGenerator?> CodeGeneratorCache = [];

      static CodeGenerator() {
         foreach (Type cg in GetAvailableCodeGenerators()) {
            AvailableCodeGenerators[cg.Name.Replace("CodeGenerator","")] = cg;
         }
      }
      private static ICodeGenerator? CreateCodeGenerator(string target,Action<Note,object[]> problemReporter,string dataType = "long") {
         if (CodeGeneratorCache.TryGetValue(target,out ICodeGenerator? cached)) return cached;
         try {
            if (AvailableCodeGenerators.TryGetValue(target,out Type? type)) {
               return CodeGeneratorCache[target] = Activator.CreateInstance(type,dataType) as ICodeGenerator;
            }
         } catch (Exception ex) {
            problemReporter(Note.CodeGenCreationError,[target,dataType,ex.Message]);
         }
         return CodeGeneratorCache[target] = null;
      }

      public static IEnumerable<Type> GetAvailableCodeGenerators() {
         Assembly currentAssembly = Assembly.GetExecutingAssembly();
         return currentAssembly.GetTypes()
            .Where(t =>
               t.IsClass &&
               !t.IsAbstract &&
               typeof(ICodeGenerator).IsAssignableFrom(t) &&
               t.Name.StartsWith("CodeGenerator"));
      }

      public static void GenerateCode(ref string targetFileName,Action<Note,object[]> problemReporter,string? target = null,Program? program = null) {
         program ??= CDL2.GetMainProgram();
         if (program is null) {
            problemReporter(Note.NoProgram,[]);
            return;
         }
         program.AnalysisRequired = true;
         target = program.Target ?? Settings.SettingValue<string>("Target")!;
         if (AvailableCodeGenerators.ContainsKey(target)) {
            ICodeGenerator? cg = CreateCodeGenerator(target,problemReporter);

            if (cg != null) {
               Emitter? emitter = null;
               try {
                  if (targetFileName == "") {
                     targetFileName = Path.Combine(Settings.OutputDirectory,Path.ChangeExtension(program!.Id.Name,cg.FileExtension));
                  } else {
                     if (Path.GetFileName(targetFileName) == targetFileName) targetFileName = Path.Combine(Settings.OutputDirectory,targetFileName);
                     if (Path.GetExtension(targetFileName) == "") targetFileName = Path.ChangeExtension(targetFileName,cg.FileExtension);
                  }
                  emitter = new EmitterFile(targetFileName) { IgnoreLineLength = true,SuppressDebug = !Settings.SettingValue<bool>("CGDebug") };
                  CodeGenerator codeGenerator = new(cg,CDL2.Compiler,problemReporter);
                  cg.SetCodeGenerator(codeGenerator);
                  codeGenerator.GenerateCode(program,emitter,$"{Settings.Display("MaxInlineCalls","NoProcInlining","NoMacroInlining","backtrace","trace","debug","profile")}");
                  problemReporter(Note.CodeGenDone,[target,program,targetFileName]);
               } catch (Exception ex) {
                  problemReporter(Note.CodeGenError,[target,targetFileName,ex.Message]);
               } finally {
                  emitter?.Close();
               }
            } else {
               problemReporter(Note.NoCodeGenerator,[target]);
            }
         }
      }
      #endregion
   }
}

