using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal class CodeGeneratorPowerShell : ICodeGenerator {
      CodeEmitterBase emitter = new CodeEmitterSink();
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
class CDL2Ref {
   [PSVariable]   $Var     = $null
   [Int32]        $Value   = 0   
   CDL2Ref([string]$name) { 
      $this.Var = Get-Variable -Name $name -Scope Script
      $this.Reset() 
   }
   [void]Reset() { 
      $this.Value = $this.Var.Value
   }
   [void]Finalize() { 
      Set-Variable -Name $this.Var.Name -Value $this.Value -Scope Script 
   }
}
";

      public void GenerateStart(Program program,CodeEmitterBase emitter) {
         this.emitter = emitter;
         emitter.Emitnl(ProgramHeader);
         EmitUnitStartComment(program);
      }

      public void GenerateEnd(Program program) {
         EmitUnitEndComment(program);
      }

      public void GenerateStart(Module module) {
         this.module = module.name.name;
         EmitUnitStartComment(module);
      }
      public void GenerateEnd(Module module) {
         EmitUnitEndComment(module);
      }
      public void GenerateStart(Layer layer) {
         this.layer = layer.name.name;
         EmitUnitStartComment(layer);
      }
      public void GenerateEnd(Layer layer) {
         EmitUnitEndComment(layer);
      }
      public void GenerateStart(Section section) {
         this.section = section.name.name;
         EmitUnitStartComment(section);
      }      
      public void GenerateEnd(Section section) {
         EmitUnitEndComment(section);
      }

      private static string PSVar(ID name) => $"${PSName(name)}";
      private static string PSVar(NamedElement name) => $"${PSName(name)}";
      private static string PSName(ID name) => name.AsIdentifier();
      private static string PSName(NamedElement name) => name.AsName();

      public void GenerateCode(Const c) {
         string value = "{PSVar(c)} = ";
         foreach (IConstElement e in c.elements) {
            switch (e) {
               case STRING s:
                  value += $"\"{s.value}\"";
                  break;
               case INT n:
                  value += n.value;
                  break;
               case FLOAT f:
                  value += f.value;
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

      public void GenerateCode(IActualArg arg) => emitter.Emit(arg is STRING s ? s.value : arg is ID i ? PSVar(i) : throw new NotImplementedException());
      public void GenerateCode(Param arg) => emitter.Emit($"${arg.name}");

      public void GenerateCodeExport(ID id) { }
      public void GenerateCodeImport(ID id) { }

      public void GenerateAlgorithmHeaderStart(Algorithm proc) => emitter.Emit($"function {PSName(proc.name)} (");
      public void GenerateAlgorithmHeaderEnd(Algorithm proc) => emitter.Emitnl(") {");

      public void GenerateStart(Procedure code) { }
      public void GenerateEnd(Procedure code) => emitter.NlEmitnl("}");

      public void GenerateEnd(Alternative alternative) => throw new NotImplementedException();
      public void GenerateEnd(Group group) => throw new NotImplementedException();
      public void GenerateEnd(Call call) => throw new NotImplementedException();

      public void GenerateStart(Alternative alternative) => throw new NotImplementedException();
      public void GenerateStart(Group group) => throw new NotImplementedException();
      public void GenerateStart(Call call) => throw new NotImplementedException();


      public void generateLudeStart(RW ludeType,Container section) {

      }
      public void generateLudeEnd(RW ludeType,Container section) => throw new NotImplementedException();
      public void GenerateStart(Program program,string target) => throw new NotImplementedException();

      public void GenerateLudeStart(RW ludeType,Container section) => throw new NotImplementedException();
      public void GenerateLudeEend(RW ludeType,Container section) => throw new NotImplementedException();
      public void GenerateStart(Macro macro) { }
      public void GenerateEnd(Macro macro) => emitter.NlEmitnl("}");
      public void GenerateCode(LIST l) => throw new NotImplementedException();
      public void GenerateExport(Module module,ID expId) => throw new NotImplementedException();
      public void GenerateLudeStart(RW ludeType,Section section) => throw new NotImplementedException();
      public void GenerateLudeEend(RW ludeType,Section section) => throw new NotImplementedException();
      public void GenerateParamSeparator() => emitter.Emit(",");


      private void EmitUnitStartComment(Container unit) => emitter.Emitnl($"# Begin {unit.ContainerName}");
      private void EmitUnitEndComment(Container unit) => emitter.Emitnl($"# End {unit.ContainerName}");
      public void GenerateLocalDeclaration(ID local) => emitter.Emitnl($"   {PSVar(local)} = $null");
   }
}
