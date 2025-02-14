using CDL2v1;

/// <summary>
/// Interface for code generators.
/// A specific code generator must implement this interface.
/// They will be called by the generic code generator to generate specific elements for the target language.
/// </summary>
internal interface ICodeGenerator {
   void GenerateStart(Program program);
   void GenerateEnd(Program program);

   void GenerateStart(Module module);
   void GenerateEnd(Module module);
   void GenerateStart(Layer layer);
   void GenerateEnd(Layer layer);
   void GenerateStart(Section section);
   void GenerateEnd(Section section);

   void GenerateCodeExport(ID id);
   void GenerateCodeImport(ID id);

   void GenerateStart(Macro macro);
   void GenerateEnd(Macro macro);

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
   void GenerateStart(Code code);
   void GenerateEnd(Code code);

   void GenerateStart(Alternative alternative);
   void GenerateEnd(Alternative alternative);
   void GenerateStart(Group group);
   void GenerateEnd(Group group);

   void GenerateStart(Call call);
   void GenerateEnd(Call call);

   void GenerateCode(ActualArg arg);
   void GenerateCode(Arg arg);

   void GenerateCode(Const c);
   void GenerateCode(Var v);
   void GenerateCode(LIST l);
}