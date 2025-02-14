using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal class CodeGeneratorPowerShell : ICodeGenerator {
      public CodeGeneratorPowerShell() { }

      public void GenerateCode(Program program) => throw new NotImplementedException();

      void ICodeGenerator.GenerateCode(ActualArg arg) => throw new NotImplementedException();
      void ICodeGenerator.GenerateCode(Arg arg) => throw new NotImplementedException();
      void ICodeGenerator.GenerateCode(Const c) => throw new NotImplementedException();
      void ICodeGenerator.GenerateCode(Var v) => throw new NotImplementedException();
      void ICodeGenerator.GenerateCode(LIST l) => throw new NotImplementedException();
      void ICodeGenerator.GenerateCodeExport(ID id) => throw new NotImplementedException();
      void ICodeGenerator.GenerateCodeImport(ID id) => throw new NotImplementedException();
      void ICodeGenerator.GenerateEnd(Program program) => throw new NotImplementedException();
      void ICodeGenerator.GenerateEnd(Module module) => throw new NotImplementedException();
      void ICodeGenerator.GenerateEnd(Layer layer) => throw new NotImplementedException();
      void ICodeGenerator.GenerateEnd(Section section) => throw new NotImplementedException();
      void ICodeGenerator.GenerateEnd(Macro macro) => throw new NotImplementedException();
      void ICodeGenerator.GenerateEnd(Code code) => throw new NotImplementedException();
      void ICodeGenerator.GenerateEnd(Alternative alternative) => throw new NotImplementedException();
      void ICodeGenerator.GenerateEnd(Group group) => throw new NotImplementedException();
      void ICodeGenerator.GenerateEnd(Call call) => throw new NotImplementedException();
      void ICodeGenerator.GenerateStart(Program program) => throw new NotImplementedException();
      void ICodeGenerator.GenerateStart(Module module) => throw new NotImplementedException();
      void ICodeGenerator.GenerateStart(Layer layer) => throw new NotImplementedException();
      void ICodeGenerator.GenerateStart(Section section) => throw new NotImplementedException();
      void ICodeGenerator.GenerateStart(Macro macro) => throw new NotImplementedException();
      void ICodeGenerator.GenerateStart(Code code) => throw new NotImplementedException();
      void ICodeGenerator.GenerateStart(Alternative alternative) => throw new NotImplementedException();
      void ICodeGenerator.GenerateStart(Group group) => throw new NotImplementedException();
      void ICodeGenerator.GenerateStart(Call call) => throw new NotImplementedException();
   }
}
