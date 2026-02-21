// <auto-gen>
//=======================================================================
// <copyright file="ICodeGenerator.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-02-13</creation-date>
// 
// <summary>
//   This interface defines the interaction between the target independent code generator and a target specific code generator.
//   Target code generators implement this interface to provide the target specific code generation logic.
//   Note on 2025-0629: Since currently there is only one target code generator, this interface is subject to change.
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

namespace CDL2v1 {
   /// <summary>
   /// Interface for target code generators.
   /// A specific code generator must implement this interface.
   /// </summary>
   /// 
   /// <remarks>
   /// The methods  will be called by the generic code generator to generate specific elements for the target language.
   /// The structure of calls made on the target code generator is as follows:
   ///   For the Program:
   ///      GenerateProgramStart                                     the start of the single program
   ///         GenerateProgramPart                                   for each part of the program
   ///         [ generated modules ]                                 if the program is compiled as a single unit.
   ///         GenerateProgramLudeStart
   ///            GenerateProgramLude                                for each of PRELUDE, ROOT, POSTLUDE, for each referenced module that has a lude of the given type
   ///         GenerateProgramLudeEnd
   ///      GenerateProgramEnd                                       at the end of the program
   ///         [ generated modules ]                                 if the program is compiled into separate units.
   ///    
   ///      For each Module:
   ///         GenerateModuleStart                                   for each module
   ///            GenerateImpExStart                                 at the start of Imports and exports
   ///               GenerateExport                                  for each export
   ///               GenerateImport                                  for each import
   ///            GenerateImpExEnd                                   at the end of Imports and exports
   ///            GenerateLayerStart                                 for each layer
   ///               GenerateSectionStart                            for each SectionById
   ///                  GenerateObjectSectionStart                     start of data Declarations
   ///                     GenerateConstantStart                     for each constant, followed by the elements of the constant
   ///                        GenerateConstElementInt                   for integer constant elements
   ///                        GenerateConstElementFloat                 for float constant elements
   ///                        GenerateConstElementString                for string constant elements
   ///                        GenerateMacroElementVar                      for references to other constants
   ///                     GenerateConstantEnd
   ///                  GenerateObjectSectionEnd                       end of data SectionById
   ///                  GenerateObjectSectionStart
   ///                     GenerateVar                               for each variable
   ///                  GenerateObjectSectionEnd 
   ///                  GenerateObjectSectionStart
   ///                     GenerateList                              for each list
   ///                  GenerateObjectSectionEnd 
   ///                  GenerateObjectSectionStart
   ///                     GenerateMacroStart                        for each macro
   ///                        ---------------------------------------algorithm header
   ///                        GenerateAlgorithmHeaderStart           start of the algorithm declaration, then a sequence of alternating ...
   ///                           GenerateAffix                 for each affix
   ///                           GenerateAffixSeparator               for each parameter
   ///                        GenerateAlgorithmHeaderEnd             end of the algorithm declaration
   ///                        GenerateAffixAndVariableInitializer                    for each output and transput affix and each referenced variable
   ///                        GenerateLocalDeclaration               for each local
   ///                        ---------------------------------------end of algorithm header
   ///                        GenerateMacroBodyStart                 start of macro body, then a sequence of ...
   ///                           GenerateMacroElementInt
   ///                           GenerateMacroElementFloat
   ///                           GenerateMacroElementString
   ///                           GenerateMacroElementVar                   ... to a Const, Var, List, Affix, or Local
   ///                        GenerateMacroBodyEnd                   end of macro body
   ///                        ---------------------------------------algorithm finalization
   ///                        GenerateAffixAndVariableFinalizationStart              start of finalization, then a sequence of ...
   ///                           GenerateAffixAndVariableFinalizer                   for each output and transput affix and each referenced variable
   ///                        GenerateAffixAndVariableFinalizationEnd                end of finalization
   ///                        ---------------------------------------end algorithm finalization
   ///                     GenerateMacroEnd
   ///                  GenerateObjectSectionEnd
   ///                  GenerateObjectSectionStart
   ///                     GenerateProcedureStart
   ///                     ------------------------------------------algorithm header, same as for macros
   ///                     GenerateProcedureBodyStart                start of procedure body
   ///                     --- Very Simple Body: a sequence of calls which cannot fail
   ///                        GenerateCallStart                           for each call including the last call
   ///                     --- Simple Body: a sequence of alternatives, no groups, no repeats
   ///                        ---------------------------------------GenerateAlternative
   ///                        GenerateAlternativeStart               for each alternative
   ///                           ------------------------------------call
   ///                           GenerateCallStart                   for each call in the alternative, then an alternating sequence of ...
   ///                              GenerateActualArg
   ///                              GenerateActualArgSeparator
   ///                           GenerateCallEnd
   ///                           ------------------------------------last call
   ///                           ------------------------------------call for Standard
   ///                           GenerateFail                        for Fail
   ///                           GenerateSucceed                     for Succeed
   ///                           GenerateAbort                       for Abort
   ///                        GenerateAlternativeEnd
   ///                     --- General Body: anything goes, same as simple body, but with groups and repeats.
   ///                         ------------------------------------last call as above but adds
   ///                           GenerateRepeat                      for Repeat
   ///                           GenerateGroupStart                  for Group
   ///                              ---------------------------------GenerateAlternative for each alternative in the group
   ///                           generateGroupEnd                     
   ///                     GenerateProcedureBodyEnd
   ///                     ------------------------------------------algorithm finalization, same as for macros
   ///                     GenerateProcedureEnd
   ///                  GenerateObjectSectionEnd                     end of object declaration SectionById
   ///               GenerateSectionEnd                              at the end of the SectionById
   ///            GenerateLayerEnd                                   at the end of the layer
   ///         GenerateModuleEnd                                     at the end of the module
   /// </remarks>
   public interface ICodeGenerator {
      #region Programs, Modules, Layers, Sections
      /// <summary>
      /// This is called at the start of the program.
      /// The supplied Emitter is used to emit the generated code.
      /// </summary>
      /// <param name="program"></param>
      /// <param name="emitter">
      ///   Used to emit the code. The generator is free to change the target using the Emitter's Target property if applicable.
      ///   Only EmitterFile currently supports this.
      /// </param>
      /// <param name="settings"></param>
      /// <param name="isSeparate"></param>
      void GenerateProgramStart(Program program,Emitter emitter,string settings,bool isSeparate = false);
      /// <summary>
      /// This is called at the end of the program.
      /// </summary>
      /// <param name="program"></param>
      /// <param name="isSeparate"></param>
      void GenerateProgramEnd(Program program,bool isSeparate = false);

      /// This is called for each part of the program. Typically does nothing, but may be used to setup linkage to the participating modules.
      /// </summary>
      /// <param name="program"></param>
      /// <param name="mod"></param>
      void GenerateProgramPart(Program program,ID mod,bool isSeparate = false);

      /// <summary>
      /// 
      /// </summary>
      /// <param name="module"></param>
      /// <param name="isSeparate">
      ///   True if the generator should generate separate units. In this case, the modules are generated after GenerateProgramEnd, otherwise they are generated
      ///   before that call.
      ///   The generator is free to ignore this parameter.
      /// </param>
      /// <param name="target">May specify a target "file", or van be null. The generator can ignore this.</param>
      void GenerateModuleStart(Module module,bool isSeparate = false,string? target = null);
      /// <summary>
      /// This is called at the end of the module.
      /// </summary>
      /// <param name="module"></param>
      /// <param name="isSeparate"></param>
      void GenerateModuleEnd(Module module,bool isSeparate = false);

      /// <summary>
      /// This is called at the start of a layer. Unlikely to be used for anything.
      /// </summary>
      /// <param name="layer"></param>
      void GenerateLayerStart(Layer layer);
      /// <summary>
      /// This is called at the end of a layer. Unlikely to be used.
      /// </summary>
      /// <param name="layer"></param>
      void GenerateLayerEnd(Layer layer);

      /// <summary>
      /// This is called at the start of a SectionById.
      /// </summary>
      /// <param name="section"></param>
      void GenerateSectionStart(Section section);
      /// <summary>
      /// This is called at the end of a SectionById.
      /// </summary>
      /// <param name="section"></param>
      void GenerateSectionEnd(Section section);
      #endregion Programs, Modules, Layers, Sections

      #region Prelude, Root, Postlude
      /// <summary>
      /// Begins the generation of the program's lude section.
      /// </summary>
      void GenerateProgramLudesStart();

      /// <summary>
      /// Ends the generation of the program's lude section.
      /// </summary>
      void GenerateProgramLudesEnd();

      /// <summary>
      /// This is called at the start generation of program and module ludes
      /// </summary>
      /// <param name="ludeType"></param>
      /// <param name="program"></param>
      void GenerateProgramLudeStart(RW ludeType,Program program);
      /// <summary>
      /// This is called for each lude of the given type in the program and in modules.
      /// </summary>
      /// <param name="ludeType"></param>
      /// <param name="program"></param>
      /// <param name="module"></param>
      void GenerateProgramLude(RW ludeType,Program program,Module module);
      /// <summary>
      /// This is called at the end of the program and module ludes.
      /// </summary>
      /// <param name="ludeType"></param>
      /// <param name="program"></param>
      void GenerateProgramLudeEnd(RW ludeType,Program program);

      /// <summary>
      /// This is called at the start generation of program and module ludes
      /// </summary>
      /// <param name="ludeType"></param>
      /// <param name="module"></param>
      /// <param name="wrapped"></param>
      void GenerateModuleLudeStart(RW ludeType,Module module,bool wrapped);
      /// <summary>
      /// This is called for each lude of the given type in the program and in modules.
      /// </summary>
      /// <param name="ludeType"></param>
      /// <param name="module"></param>
      /// <param name="section"></param>
      void GenerateModuleLude(RW ludeType,Module program,Section module);
      /// <summary>
      /// This is called at the end of the program and module ludes.
      /// </summary>
      /// <param name="ludeType"></param>
      /// <param name="module"></param>
      /// <param name="wrapped"></param>
      void GenerateModuleLudeEnd(RW ludeType,Module module,bool wrapped);


      void GenerateSectionLudeStart(RW ludeType,Section section);
      void GenerateSectionLudeEnd(RW ludeType,Section section);
      #endregion

      #region Import/Export
      /// <summary>
      /// This is called at the start of Imports and exports.
      /// </summary>
      /// <param name="module"></param>
      void GenerateImpExStart(Module module);
      /// <summary>
      /// For each export. Note that IProvidable is either an Algorithm or a Const.
      /// </summary>
      /// <param name="export"></param>
      void GenerateExport(IProvidable export);
      /// <summary>
      /// For each import. Note that IProvidable is either an Algorithm or a Const.
      /// </summary>
      /// <param name="import"></param>
      void GenerateImport(IProvidable import);
      /// <summary>
      /// This is called at the end of Imports and exports.
      /// </summary>
      /// <param name="module"></param>
      void GenerateImpExEnd(Module module);
      #endregion Import/Export

      #region Object Sections
      /// <summary>
      /// Starts a SectionById of the given kind.
      /// </summary>
      /// <param name="count"></param>
      /// <param name="kind">CONST, VAR, LIST, MACRO, PROCEDURE</param>
      void GenerateObjectSectionStart<T>(IEnumerable<NamedElement> items,string kind) where T : NamedElement;
      void GenerateObjectSectionEnd<T>(IEnumerable<NamedElement> items,string kind) where T : NamedElement;
      #endregion Object Sections

      #region Data Declarations
      /// <summary>
      /// 
      /// </summary>
      /// <param name="c"></param>
      void GenerateConstantStart(Const c);
      /// <summary>
      /// This is called for each element of a constant that is a string.
      /// </summary>
      /// <param name="value"></param>
      void GenerateConstElementString(string value);
      /// <summary>
      /// This is called for each element of a constant that is a float.
      /// </summary>
      /// <param name="value"></param>
      void GenerateConstElementFloat(double value);
      /// <summary>
      /// This is called for each element of a constant that is an integer.
      /// </summary>
      /// <param name="value"></param>
      void GenerateConstElementInt(long value);
      /// <summary>
      /// This is called for each element of a constant that is a reference to another const.
      /// </summary>
      /// <param name="constant"></param>
      void GenerateConstElementConst(Const constant);
      /// <summary>
      /// Marks the end of the constant element sequence.
      /// </summary>
      /// <param name="c"></param>
      void GenerateConstantEnd(Const c);

      /// <summary>
      /// This declares a variable.
      /// </summary>
      /// <param name="var"></param>
      void GenerateVar(Var var);
      /// <summary>
      /// This declares a list. 
      /// </summary>
      /// <param name="id"></param>
      /// <param name="lwb">The constant that contains the lower bound.</param>
      /// <param name="upb">The constant that contains the upper bound.</param>
      void GenerateList(LIST list,Const lwb,Const upb);
      #endregion Data Declarations

      #region Algorithm Common
      /// <summary>
      /// Some taget languages require (e.g., C) procedures to be declared befor call.
      /// The code genrator must return true for that to happen.
      /// </summary>
      /// <returns></returns>
      bool RequiresPredeclaration => false;
      void GenerateDeclaration(Algorithm algorithm);


      /// <summary>
      /// This called to start generating the header for an algorithm.
      /// It typically generates the name of the algorithm using the syntax of the target language.
      /// </summary>
      /// <param name="proc"></param>
      void GenerateAlgorithmHeaderStart(Algorithm proc);
      /// <summary>
      /// Generates what is required for an affix. This is called for each affix in the algorithm header.
      /// </summary>
      /// <param name="affix"></param>
      /// <param name="direction"></param>
      /// <param name="algorithmCanFail"></param>
      void GenerateAffix(Affix affix,AD direction,bool algorithmCanFail);

       /// <summary>
      /// This is called to generate the separator between affixes in the algorithm header.
      /// </summary>
      void GenerateAffixSeparator();
      /// <summary>
      /// This is called at the end of the algorithm header.
      /// </summary>
      /// <param name="proc"></param>
      void GenerateAlgorithmHeaderEnd(Algorithm proc);
      /// <summary>
      /// Called to declare the locals of the algorithm if necessary.
      /// </summary>
      /// <param name="local"></param>
      void GenerateLocal(Local local);

      /// <summary>
      /// This called to perform initialization of affixes and variables used in the algorithm.
      /// This is required to ensure that output and transput affixes and variables remain unchanged when the algorithm fails.
      /// The finalization section is then generated at the end of the algorithm to set the values of the affixes and variables.
      /// </summary>
      /// <remarks>
      /// All of these API-s are called for all algorithms even if they cannot fail. It is up to the target generator to omit generating code if it is not needed.
      ///</remarks>
      /// <param name="alg"></param>
      /// <example>
      /// The Powershell code generator uses this to copy the values of the output and transput affixes and variables to a temporary variable.
      /// The finalizer then copies the values back to the original affixes and variables, but only if the algorithm succeeds.
      /// For Powershell, both initialization and finalization is always necessary due to how Powershell handles parameters passed by reference 
      /// <see cref="https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_ref?view=powershell-7.5"/>.
      /// </example>
      void GenerateAffixAndVariableInitializationStart(Algorithm alg);
      /// <summary>
      /// This is called for each affix and each variable referenced in the algorithm.
      /// </summary>
      /// <param name="alg"></param>
      /// <param name="var"></param>
      /// <param name="isVar"></param>
      void GenerateAffixAndVariableInitializer(Algorithm alg,IFailureProtected var,bool isVar = false);
      /// <summary>
      /// This is called at the end of the initialization of affixes and variables.
      /// </summary>
      /// <param name="alg"></param>
      void GenerateAffixAndVariableInitializationEnd(Algorithm alg);

      /// <summary>
      /// This is called at the end of the algorithm to finalize the affixes and variables.
      /// </summary>
      /// <param name="algorithm"></param>
      /// 
      void GenerateAffixAndVariableFinalizationStart(Algorithm algorithm);
      /// <summary>
      /// This is called for each affix and each variable referenced in the algorithm.
      /// </summary>
      /// <param name="alg"></param>
      /// <param name="var"></param>
      /// <param name="isVar"></param>
      void GenerateAffixAndVariableFinalizer(Algorithm alg,IFailureProtected var,bool isVar = false);
      /// <summary>
      /// This is called at the end of the finalization of affixes and variables.
      /// </summary>
      /// <param name="algorithm"></param>
      /// 
      void GenerateAffixAndVariableFinalizationEnd(Algorithm algorithm);
      #endregion Algorithm Common

      #region Macros

      string LineComment { get; }
      (string Start, string End) BlockComment { get; }

      /// <summary>
      /// Gets a value indicating whether the target requires macro splitting during processing.
      /// </summary>
      bool TargetRequiresMacroSpliting { get; }
      string StatementSeparator { get; }
      /// <summary>
      /// This is called at the start of a macro.
      /// </summary>
      /// <param name="macro"></param>
      void GenerateMacroStart(Macro macro);
      /// <summary>
      /// This is called at the end of a macro.
      /// </summary>
      /// <param name="macro"></param>
      void GenerateMacroEnd(Macro macro);
      /// <summary>
      /// This is called at the start of the body of a macro.
      /// </summary>
      /// <param name="macro"></param>
      /// 
      void GenerateMacroBodyStart(Macro macro);
      /// <summary>
      /// This is called at the end of the body of a macro.
      /// </summary>
      /// <param name="macro"></param>
      void GenerateMacroBodyEnd(Macro macro);
      /// <summary>
      /// Begins generation of a return expression for the specified macro.
      /// </summary>
      /// <param name="macro">The macro for which to start generating the return expression. Cannot be null.</param>
      void GenerateReturnExpressionStart(Macro macro);
      /// <summary>
      /// Generates the end of a return expression for the specified macro.
      /// </summary>
      /// <param name="macro">The macro for which to generate the return expression end. Cannot be null.</param>
      void GenerateReturnExpressionEnd(Macro macro);

      /// <summary>
      /// This is called for each element of a macro that is an integer.
      /// </summary>
      /// <param name="value"></param>
      void GenerateMacroElementInt(long value);
      /// <summary>
      /// 
      /// </summary>
      /// <param name="value"></param>
      void GenerateMacroElementFloat(double value);
      /// <summary>
      /// This is called for each element of a macro that is a string.
      /// </summary>
      /// <param name="value"></param>
      /// <param name="canFail"></param>
      /// <param name="firstElement"></param>
      void GenerateMacroElementString(string value,bool firstElement,bool quoted);
      /// <summary>
      /// This is called for each reference in a const, macro or proc that is a constant.
      /// </summary>
      /// <param name="constant"></param>
      void GenerateMacroElementConst(Const constant);
      void GenerateMacroElementVar(Var var,bool macroCanFail,bool inlined = false);
      void GenerateMacroElementList(LIST list);
      void GenerateMacroElementAffix(Affix aff,bool macroCanFail);
      void GenerateMacroElementLocal(Local id,Affix calledAffix);
      void GenerateMacroInlineStart(Macro macro);
      void GenerateMacroInlineEnd(Macro macro);
      #endregion Macros

      #region Procedures
      /// <summary>
      /// This is called at the start of a procedure.
      /// </summary>
      /// <param name="code"></param>
      void GenerateProcedureStart(Procedure code);
      /// <summary>
      /// This is called at the end of a procedure.
      /// </summary>
      /// <param name="code"></param>
      void GenerateProcedureEnd(Procedure code);
      void GenerateProcedureBodyStart(Procedure macro,PBT bodyType);
      void GenerateProcedureBodyEnd(Procedure macro,PBT bodyType);
      #region Alternatives
      /// <summary>
      /// Generate the start of alternative i the group.
      /// </summary>
      /// <param name="proc"></param>
      /// <param name="group"></param>
      /// <param name="i">The alternative number within the group</param>
      /// <param name="supressLabel">Whether to suppress the label for this alternative</param>
      void GenerateAlternativeStart(Procedure proc,Group group,int i,bool supressLabel = false);
      /// <summary>
      /// Generate the end of alternative i int he group.
      /// </summary>
      /// <param name="proc"></param>
      /// <param name="group"></param>
      /// <param name="i">The alternative number within the group</param>
      /// <param name="alternative"></param>
      /// <param name="removed">Set if the alternative tail was skipped due to conditional compilation.</param>
      /// 
      void GenerateAlternativeEnd(Procedure proc,Group group,int i,Alternative alternative,bool removed = false);

      #endregion Alternatives

      #region Groups
      /// <summary>
      /// Called at the start of a group.
      /// </summary>
      /// <param name="proc"></param>
      /// <param name="group"></param>
      void GenerateGroupStart(Procedure proc,Group group);
      /// <summary>
      /// Called at the end of a group.
      /// </summary>
      /// <param name="proc"></param>
      /// <param name="group"></param>
      void GenerateGroupEnd(Procedure proc,Group group);
      #endregion Groups

      #region Calls

      /// <summary>
      /// Generate code for the repeat operator.
      /// </summary>
      /// <param name="proc"></param>
      /// <param name="group">The immediate group containing the operator.</param>
      /// <param name="label">The label to be repeated, or Anon to repeat the enclosing group.</param>
      /// <param name="canFail">True if the alternative that ends with this repeat can fail.</param>
      void GenerateRepeat(Procedure proc,Group group,ID label,bool canFail);
      /// <summary>
      /// Exit the current procedure with a fail. This can only occur in a TEST/PREDICATE.
      /// </summary>
      /// <param name="proc"></param>
      /// <param name="group"></param>
      void GenerateFail(Procedure proc,Group group);
      /// <summary>
      /// Exit the current procedure with success. This is probably a no-op.
      /// </summary>
      /// <param name="proc"></param>
      /// <param name="group"></param>
      void GenerateSucceed(Procedure proc,Group group);
      /// <summary>
      /// Terminate the running program.
      /// </summary>
      /// <param name="proc"></param>
      /// <param name="group"></param>
      void GenerateAbort(Procedure proc,Group group);

      void GenerateActualArgSeparator();
      void GenerateCallStart(Algorithm called,Procedure proc,bool firstCall = false,bool onlyCallInAlternative = false,bool lastAlternative = false);
      void GenerateCallEnd(Algorithm call,Procedure proc,bool firstCall = false,bool onlyCallInAlternative = false,bool lastAlternative = false);
      void GenerateCallArgString(string value);
      void GenerateCallArgReferenceAffix(Affix calledAffix,Affix a,bool needFinalization);
      void GenerateCallArgReferenceLocal(Affix calledAffix,Local lo);
      void GenerateCallArgReferenceConst(Affix calledAffix,Const c);
      void GenerateCallArgReferenceVar(Affix calledAffix,Var v,bool needFinalization);
      #endregion Calls

      #endregion Procedures

      #region Support
      void GenerateNewline();
      void GenerateComment(string comment);
      void GenerateComment(PrettyPrinter sourcePrinter);
      void GenerateSourceComment(bool nl = true);

      void IncrementIndent();
      void DecrementIndent();

      /// <summary>
      /// Provides the file extension for the target language.
      /// </summary>
      string FileExtension { get; }

      /// <summary>
      /// Provides the source comment printer for the target language.
      /// This is used to generate source code as comments into the target program.
      /// </summary>
      Emitter SourceEmitter { get; }
      #endregion Support
   }
}














