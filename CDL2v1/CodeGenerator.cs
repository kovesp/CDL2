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

using System.Collections;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows.Controls.Ribbon.Primitives;
using System.Windows.Documents;

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

         if (!isSeparate) {
            // Generate an integrated program ignoring module boundaries of all objects reachable from the program's ludes.
            cg.GenerateProgramStart(program,emitter,settings,isSeparate: false);  // Generate the overall scaffolding


            IEnumerable<Var> allVars = Reachable.Objects.OfType<Var>();
            IEnumerable<LIST> allLists = Reachable.Objects.OfType<LIST>();

            GenerateObjects<Const>(Reachable.Objects.OfType<Const>(),GenerateConstant);
            GenerateObjects<Var>(allVars,GenerateVar);
            GenerateObjects<LIST>(allLists,GenerateList);

            IOrderedEnumerable<Macro>     macros              = Reachable.Objects.OfType<Macro>().Where(alg => !alg.IsInlinable()).OrderBy(p => p.Id.Name);
            IOrderedEnumerable<Procedure> procedures          = Reachable.Objects.OfType<Procedure>()
                                                            .Where(proc => !proc.IsSynthetic && !proc.IsConditionalCompilation() && !proc.IsInlinable(Reachable))
                                                            .OrderBy(p => p.Id.Name);
            IOrderedEnumerable<Procedure> syntheticProcedures = Reachable.Objects.OfType<Procedure>().Where(proc => proc.IsSynthetic).OrderBy(p => p.Id.Name);

            IOrderedEnumerable<Algorithm> allProcs = ((IEnumerable<Algorithm>)[.. macros,.. procedures,.. syntheticProcedures]).OrderBy(p => p.Id.Name);
            Dictionary<CDL2Object,int> procIndex = allProcs.Select((proc,index) => (proc,index)).ToDictionary(pair => (CDL2Object)pair.proc,pair => pair.index);

            if (cg.RequiresPredeclaration) {
               GenerateObjects<Macro>(macros,GeneratePredeclaration,"Macro Forward Declaration");
               GenerateObjects<Procedure>(procedures,GeneratePredeclaration,"Procedure Forward Declaration");
               GenerateObjects<Procedure>(syntheticProcedures,GeneratePredeclaration,"Synthetic Procedure Forward Declaration");
            }
            GenerateObjects<Macro>(macros,GenerateMacro,itemIndex:procIndex);
            GenerateObjects<Procedure>(procedures,GenerateProcedure,itemIndex:procIndex);
            GenerateObjects<Procedure>(syntheticProcedures,GenerateProcedure,"Synthetic Procedure",itemIndex:procIndex);
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
                  foreach (LIST list in allLists) cg.GenerateDebugInfoList( list);
                  cg.GenerateDebugInfoListEnd(allLists);
               }
               cg.GenerateDebugInfoEnd();
            }


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
            foreach (Section? section in SectionsWithLudes) cg.GenerateModuleLude(ludeType,module,section!);
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
      private void GenerateObjects<T>(IEnumerable<NamedElement> items,Action<T,int,int> generate,string? specialType = null,Dictionary<CDL2Object,int>? itemIndex=null) where T : CDL2Object {
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
         if (!Settings.InliningMacros || !macro.IsInlineMacro) {
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
      private void GenerateMacroBody(Macro macro,Procedure? callingProc = null,int alternativeNumber=-1,List<IActualArg>? args = null,AList? aList = null,bool inlining = false) {
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
         foreach (IElement elem in lastExpression) {
            GenerateMacroElement(macro,macro.Section!,callingProc,aList,first,elem);
            first = false;
         }
         if (inlining) cg.GenerateMacroInlineEnd(macro,alternativeNumber); else cg.GenerateReturnExpressionEnd(macro,alternativeNumber);
         cg.GenerateMacroBodyEnd(macro,inlining);
      }


      /// <summary>
      /// Represents a list of affix mappings for an Algorithm call.
      /// This is essentially an association list as pioneered by Lisp.
      /// </summary>
      private class AList : List<AList.AffixMapping> {
         /// <summary>
         /// Maps an affix to the actual argument.
         /// </summary>
         /// <param name="i">The ordinal of the argument. Not currently used.</param>
         /// <param name="affix"></param>
         /// <param name="arg"></param>
         internal struct AffixMapping(int i,Affix affix,IActualArg arg) {
            public Affix affix = affix;
            public IActualArg arg = arg;
#if DEBUG
            public int argNo = i;
            public override readonly string ToString() => $"[{argNo}] {arg} -> {affix}";
#else
            public override readonly string ToString() => $"{affix} -> {arg}";
#endif
         }

         private AList() : base() { }

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
               Add(new AffixMapping(i,affixes[i],args[i] is Affix aff && aList.TryGetValue(aff,out IActualArg? arg) ? arg : args[i]));
            }
         }
         /// <summary>
         /// Try to get the value of an affix from the list.
         /// </summary>
         /// <param name="affix"></param>
         /// <param name="arg"></param>
         /// <returns></returns>
         public bool TryGetValue(Affix affix,out IActualArg arg) {
            foreach (AffixMapping subst in this) {
               if (subst.affix == affix) {
                  arg = subst.arg;
                  return true;
               }
            }
            arg = affix;
            return false;
         }
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
         public IActualArg GetValue(IActualArg arg) { 
            if (arg is Affix affix) TryGetValue(affix,out arg);
            return arg;
         }
         
         public bool TryGetValue(ID id,out IActualArg? arg) {
            foreach (AffixMapping subst in this) {
               if (subst.affix.Id == id) {
                  arg = subst.arg;
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
            case STRING s: GenerateMacroElementString(s,first:first,quoted:false); break;
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
         => cg.GenerateMacroElementString(s.value.Replace("$#",cg.LineComment),firstElement: first,quoted:quoted);

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
            sourceCommentPrinter.Print(alg);
            cg.GenerateSourceComment(nl:false);
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
         Procedure proc  => CollectLocals(proc.group,[.. proc.Locals.Where(local=>!local.IsBuiltinResult)]), 
         Macro     macro => macro.IsInlinable() ? [] : [.. macro.Locals],
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
         foreach (Call call in alternative.calls) CollectLocals(call,locals);
         switch (alternative.lastCall.type) {
            case LCT.Standard: CollectLocals(alternative.lastCall.call!,locals); break;
            case LCT.Group: CollectLocals(alternative.lastCall.group!,locals); break;
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
               if (called is Macro macro && Settings.InliningMacros && macro.IsInlineMacro) {
                  locals.AddAll(macro.Locals);
               } else if (called is Procedure proc && Settings.InliningProcs  && proc.IsInlinable(Reachable)) {
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
         bool suppressRest = false;
         bool removed;

         int i = 1;
         int lastRemoved = -1;
         foreach (Alternative alternative in group.Alternatives) {
            removed = false;
            if (suppressRest) {
               cg.GenerateComment($"Alternative {i} suppressed by previous conditional compilation ON");
               removed = true;
            } else if (alternative.IsConditionalCompilationOff) {           // Ignore this alternative
               cg.GenerateComment($"Alternative {i} removed by conditional compilation OFF");
               removed = true;
               lastRemoved = i;
            } else {
               suppressRest = alternative.IsConditionalCompilationOn;       // Ignore following alternatives
               cg.GenerateAlternativeStart(proc,group,i,lastRemoved == i-1);
               GenerateAlternative(proc,group,alternative,suppressRest || group.Alternatives.Count == i);
               cg.GenerateAlternativeEnd(proc,group,i,alternative,removed,lastRemoved == i - 1);
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
      private void GenerateAlternative(Procedure proc,Group group,Alternative alternative,bool isLast,AList? aList = null) {
         List<Call> calls = alternative.calls;
         bool canFail = false;
         foreach (Call call in calls) {
            GenerateCall(proc,alternative,call,canFail,currentAList: aList,lastAlternative: isLast);
            canFail = canFail || call.CanFail;
         }
         switch (alternative.lastCall.type) {
            case LCT.Standard: 
               GenerateCall(proc,alternative,alternative.lastCall.call!,canFail,onlyCallInAlternative: calls.Count == 0,lastAlternative: isLast,aList); break;
            case LCT.Fail: cg.GenerateFail(proc,group); break;
            case LCT.Succeed: cg.GenerateSucceed(proc,group); break;
            case LCT.Abort: cg.GenerateAbort(proc,group,proc.AlgId); break;
            case LCT.Repeat: cg.GenerateRepeat(proc,group,alternative.lastCall.label!,canFail); break;
            case LCT.Group: GenerateGroup(proc,alternative.lastCall.group!); break;
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
                                 bool lastAlternative = false,AList? currentAList = null) {
         if (call.IsConditionalCompilationOn) return;   // No need to generate code for this call;
         if (call.IsBuiltin && Builtin.IsFunction(call)) {
            // Ignore the call here. The value of the builtin will be inserted directly where needed.
            cg.GenerateComment($"{call} -> {Builtin.EvalFunction(call)}");
         } else {
            Algorithm? called = call.Called;
            if (called is not null) {
               if (Settings.InliningMacros && called is Macro macro && macro.IsInlineMacro) {
                  cg.GenerateComment($"Inlining macro call -> {call}");
                  GenerateAlgorithmComment(called,nl: false);
                  GenerateMacroBody(macro,proc,alternativeNumber: alternative.NextAlternativeNumber,[.. call.Args],currentAList,inlining: true);
               } else if (Settings.InliningProcs && called is Procedure calledProc && calledProc.IsInlinable(Reachable)) {
                  cg.GenerateComment($"Inlining procedure call -> {call} ({calledProc.inliningParameters?.Display()??"?"})");
                  GenerateAlgorithmComment(called,nl:false);
                  // The following works because currently only procedures with a single alternative are inlineable.
                  GenerateAlternative(proc,calledProc.group,calledProc.group.Alternatives[0],isLast: false,new AList(currentAList,calledProc.Affixes,[.. call.Args]));
               } else {
                  cg.GenerateCallStart(called,proc,canFail,onlyCallInAlternative,lastAlternative);
                  AList aList = new(currentAList,called.Affixes,call.Args);
                  called.Affixes.GenerateJoinedSequence(GenerateActualArg(proc,call,aList),cg.GenerateActualArgSeparator);
                  cg.GenerateCallEnd(called,proc,alternative,canFail,onlyCallInAlternative,lastAlternative);
               }
            } else {
               cg.GenerateComment($"Call to undefined algorithm {call} skipped.");
            }
         }
      }

      private Action<IActualArg> GenerateActualArg(Procedure proc,Call call,AList aList) => aff => GenerateActualArg(proc,call,(Affix)aff,aList.GetValue(aff));
      private void GenerateActualArg(Procedure proc,Call call,Affix affix,IActualArg actualArg) {
         Section callProcSection = call.ContainingProc.Section!;
         switch (actualArg) {
            case STRING s:
               Debug.Assert(affix.affixType == AT.str,$"GenerateCallStart: String argument for non-string affix {affix}");
               cg.GenerateCallArgString(s.value);
               break;
            case Const c:
               cg.GenerateCallArgReferenceConst(affix,callProcSection.GetResolvedConstant(c)!);
               break;
            case Var v:
               cg.GenerateCallArgReferenceVar(affix,callProcSection.GetResolvedObject(v)!,needsFinalization: proc.NeedsFinalization);
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
         if (AvailableCodeGenerators.ContainsKey (target)) {
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
                  codeGenerator.GenerateCode(program,emitter,$"{Settings.Display("MaxInlineCalls","NoProcInlining","NoMacroInlining","backtrace","trace")}");
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

