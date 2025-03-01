using CDL2v1;

/// <summary>
/// Interface for code generators.
/// A specific code generator must implement this interface.
/// They will be called by the generic code generator to generate specific elements for the target language.
/// </summary>
internal interface ICodeGenerator {
   public void GenerateStart(Program program,CodeEmitterBase emiter);
   public void GenerateEnd(Program program);

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
   /// <param name="macro"></param>
   public void GenerateStart(Procedure code);
   public void GenerateEnd(Procedure code);

   public void GenerateStart(Alternative alternative);
   public void GenerateEnd(Alternative alternative);
   public void GenerateStart(Group group);
   public void GenerateEnd(Group group);

   public void GenerateStart(Call call);
   public void GenerateEnd(Call call);

   public void GenerateCode(IActualArg arg);
   public void GenerateCode(Affix arg);
   public void GenerateLocalDeclaration(ID local);

   public void GenerateCode(Const c);
   public void GenerateCode(Var v);
   public void GenerateCode(LIST l);

   void GenerateLudeStart(RW ludeType,Container section);
   void GenerateLudeEend(RW ludeType,Container section);
   void GenerateExport(Module module,ID expId);

   public void GenerateAlgorithmHeaderStart(Algorithm proc);
   public void GenerateAlgorithmHeaderEnd(Algorithm proc);
   public void GenerateParamSeparator();

   public string FileExtension { get; }
}
