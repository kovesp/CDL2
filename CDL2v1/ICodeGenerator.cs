using CDL2v1;

/// <summary>
/// Interface for code generators.
/// A specific code generator must implement this interface.
/// They will be called by the generic code generator to generate specific elements for the target language.
/// </summary>
internal interface ICodeGenerator {
   public void GenerateStart(Program? program,EmitterBase emiter);
   public void GenerateEnd(Program? program);

   public void GenerateStart(Module module);
   public void GenerateEnd(Module module);
   public void GenerateStart(Layer layer);
   public void GenerateEnd(Layer layer);
   public void GenerateStart(Section section);
   public void GenerateEnd(Section section);

   public void GenerateCodeExport(ID id);
   public void GenerateCodeImport(ID id);

   public void GenerateStart(Macro macro);
   public void GenerateEnd(Macro macro);

   /// <summary>
   /// The body of a CDL2 rule should look somethong like this:
   /// FUNCTION func:
   ///   test1, function1, test2, function2, test3, function3;
   ///   function4, 
   ///      (test5, * func ;
   ///       +);   
   ///   function6.
   ///   
   /// function func() {
   ///    lbl_func: while (true) {
   ///      if (test1) {
   ///         function1;
   ///         if (test2) {
   ///            function2;
   ///            if (test3) {
   ///               function3; break;
   ///            }
   ///         }
   ///      } else {
   ///         function4;
   ///         whhile (true) {
   ///            if (test5) {
   ///               continue lbl_func;
   ///            } else {
   ///               return;
   ///            }
   ///         }
   ///      } else {
   ///         function6;
   ///         break;
   ///      }    
   ///    }
   /// }
   /// </summary>
   /// <param id="macro"></param>
   public void GenerateStart(Procedure code);
   public void GenerateEnd(Procedure code);

   public void GenerateStart(Alternative alternative);
   public void GenerateEnd(Alternative alternative);
   public void GenerateStart(Group group);
   public void GenerateEnd(Group group);

   public void GenerateStart(Call call);
   public void GenerateEnd(Call call);
   public void GenerateDeclareLocal(ID local);

   /// <summary>
   /// This declares the head of a constant
   /// </summary>
   /// <param name="id"></param>
   public void GenerateCodeConst(ID id);
   public void GenerateCodeDeclareVar(ID id);
   /// <summary>
   /// This declares a list. 
   /// </summary>
   /// <param name="id"></param>
   /// <param name="lwb">The name of the constant that contains the lower bound.</param>
   /// <param name="upb">The name of the constant that contains the upper bound.</param>
   public void GenerateCodeDeclareList(ID id,ID lwb,ID upb);

   void GenerateLudeStart(RW ludeType,Container section);
   void GenerateLudeEnd(RW ludeType,Container section);
   void GenerateExport(Module module,ID expId);

   public void GenerateAlgorithmHeaderStart(Algorithm proc);
   public void GenerateAlgorithmHeaderEnd(Algorithm proc);
   public void GenerateParamSeparator();
   void GenerateMacroElemInt(long value);
   void GenerateMacroElemFloat(double value);
   void GenerateMacroElemString(string value);
   void GenerateReferenceVar(ID id);
   void GenerateReferenceList(ID id);
   void GenerateReferenceConst(ID id);
   void GenerateReferenceAffix(ID id);
   void GenerateReferenceLocal(ID id);
   void GenerateDeclareAffix(ID id,AD direction);
   void GenerateInitializeAffixOrVar(ID id,AD affixDir,bool isVar = false);
   void GenerateFinalizeAffixOrVar(ID id,AD affixDir,bool isVar = false);
   void Newline();

   public string FileExtension { get; }
}
