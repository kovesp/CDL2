using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal class CodeGeneratorPowerShell : ICodeGenerator {
      ICodeEmiter emitter = new SinkCodeEmitter();
      string module = "";
      string layer = "";
      string section = "";
      string proc = "";

      public string FileExtension => ".ps1";

      private static string ProgramHeader => @"
class BoundedArray {
    [int]$LowerBound
    [int]$UpperBound
    [object[]]$Array

    BoundedArray([int]$lowerBound, [int]$upperBound) {
        if ($upperBound - $lowerBound < 0) {
            throw [System.ArgumentException]::new(""Upper bound must be greater than or equal to lower bound."")
        }
        $this.LowerBound = $lowerBound
        $this.UpperBound = $upperBound
        $this.Array = New-Object object[] ($upperBound - $lowerBound + 1)
    }

    [object]GetItem([int]$index) {
        if ($index - $this.LowerBound -ge 0 -and $index - $this.LowerBound -le $this.UpperBound - $this.LowerBound) {
            return $this.Array[$index - $this.LowerBound]
        } else {
            throw [System.IndexOutOfRangeException]::new(""Index out of range."")
        }
    }

    [void]SetItem([int]$index, [object]$value) {
        if ($index - $this.LowerBound -ge 0 -and $index - $this.LowerBound -le $this.UpperBound - $this.LowerBound) {
            $this.Array[$index - $this.LowerBound] = $value
        } else {
            throw [System.IndexOutOfRangeException]::new(""Index out of range."")
        }
    }
}
";

      public void GenerateStart(Program program,ICodeEmiter emitter) { 
         this.emitter = emitter;
         emitter.Emitnl(ProgramHeader);
      }

      public void GenerateEnd(Program program) { }

      public void GenerateStart(Module module) { this.module = module.name.name; emitter.Emitnl($"# Begin {module.FullName()}"); }
      public void GenerateStart(Layer layer) { this.layer = layer.name.name; emitter.Emitnl($"# Begin {layer.FullName()}"); }
      public void GenerateStart(Section section) { this.section = section.name.name; emitter.Emitnl($"# Begin {section.FullName()}"); }
      public void GenerateEnd(Module module) => emitter.Emitnl($"# End {module.FullName()}");
      public void GenerateEnd(Layer layer) => emitter.Emitnl($"# End {layer.FullName()}");
      public void GenerateEnd(Section section) => emitter.Emitnl($"# End {section.FullName()}");

      private string PSVar(string name) => $"${PSName(name)}";
      private string PSVar(ID name) => $"${PSName(name)}";
      private string PSVar(NamedElement name) => $"${PSName(name)}";
      private string PSName(string name) => $"{module}_{layer}_{section}_{name}";
      private string PSName(ID name) => $"{module}_{layer}_{section}_{name}";
      private string PSName(NamedElement name) => $"{module}_{layer}_{section}_{name.name}";

      public void GenerateCode(Const c) {
         string value = "{PSVar(c)} = ";
         foreach (ConstElement e in c.elements) {
            switch (e) {
               case STRING s:
                  value += $"\"{s.value}\"";
                  break;
               case INT n:
                  value += $"{n.value}";
                  break;
               case FLOAT f:
                  value += $"{f.value}";
                  break;
               case Const ce:
                  value += PSVar(ce);
                  break;
               case ID id:
                  value += PSVar(id);
                  break;
               default:
                  throw new NotImplementedException();
            }
         }
         emitter.Emitnl(value);
      }
      public void GenerateCode(Var v) => emitter.Emitnl($"{PSVar(v)}");
      public void GenerateCode(LIST l,string lwb,string upb) => emitter.Emitnl($"{PSVar(l)} = New-Object BoundedArray {lwb} {upb}");

      public void GenerateCode(ActualArg arg) => emitter.Emit(arg is STRING s ? s.value : arg is ID i ? PSVar(i) : throw new NotImplementedException());
      public void GenerateCode(Param arg) => emitter.Emit($"${arg.name}");

      public void GenerateCodeExport(ID id) => throw new NotImplementedException();
      public void GenerateCodeImport(ID id) => throw new NotImplementedException();

      public void GenerateProcHeaderStart(Proc proc) => emitter.Emit($"function {PSName(proc.name)} (");
      public void GenerateProcHeaderEnd(Proc proc) => emitter.Emitnl(") {");

      public void GenerateStart(Code code) => throw new NotImplementedException();
      public void GenerateEnd(Code code) => throw new NotImplementedException();

      public void GenerateEnd(Alternative alternative) => throw new NotImplementedException();
      public void GenerateEnd(Group group) => throw new NotImplementedException();
      public void GenerateEnd(Call call) => throw new NotImplementedException();     

      public void GenerateStart(Alternative alternative) => throw new NotImplementedException();
      public void GenerateStart(Group group) => throw new NotImplementedException();
      public void GenerateStart(Call call) => throw new NotImplementedException();

   
      public void generateLudeStart(RW ludeType,Container section) => throw new NotImplementedException();
      public void generateLudeEnd(RW ludeType,Container section) => throw new NotImplementedException();
      public void GenerateStart(Program program,string target) => throw new NotImplementedException();

      public void GenerateLudeStart(RW ludeType,Container section) => throw new NotImplementedException();
      public void GenerateLudeEend(RW ludeType,Container section) => throw new NotImplementedException();
   }
}
