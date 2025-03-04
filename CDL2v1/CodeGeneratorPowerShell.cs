using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal class CodeGeneratorPowerShell : ICodeGenerator {
      EmitterBase emitter = new EmitterSink();

      public string FileExtension => ".ps1";

      private static string ProgramHeader => @"
class BoundedArray {
   [int]$LowerBound
   [int]$UpperBound
   [Array]$Array
   

   BoundedArray([int]$lowerBound, [int]$upperBound) {
       if ($upperBound - $lowerBound -lt 0) {
           throw [System.ArgumentException]::new(""Upper bound $($this.UpperBound) must be greater than or equal to lower bound $($this.LowerBound)."")
       }
       $this.LowerBound = $lowerBound
       $this.UpperBound = $upperBound
       $this.Array = $script:TypeAlias.Word::new($upperBound - $lowerBound + 1)
   }
   [void]CheckIndex([int]$index) {
      if ($index - $this.LowerBound -ge 0 -and $index - $this.LowerBound -le $this.UpperBound - $this.LowerBound) {
         return
      } else {
         throw [System.IndexOutOfRangeException]::new(""Index: $index, LowerBound: $($this.LowerBound), UpperBound: $($this.UpperBound)"")
       }
   }
   # These do NOT create a [] indexer
   [object]Item([int]$index) {
      $this.CheckIndex($index)
      return $this.Array[$index - $this.LowerBound]
   }

   [void]Item([int]$index, $value) {
      $this.CheckIndex($index)
      $this.Array[$index - $this.LowerBound] = $value
   }
}
";

      public void GenerateStart(Program? program,EmitterBase emitter) {
         this.emitter = emitter;
         emitter.Emitnl(ProgramHeader);
         if (program != null) EmitUnitStartComment(program);
      }

      public void GenerateEnd(Program? program) {
         if (program != null) EmitUnitEndComment(program);
      }
      public void GenerateStart(Module module) => EmitUnitStartComment(module);
      public void GenerateEnd(Module module) => EmitUnitEndComment(module);
      public void GenerateStart(Layer layer) => EmitUnitStartComment(layer);
      public void GenerateEnd(Layer layer) => EmitUnitEndComment(layer);
      public void GenerateStart(Section section) => EmitUnitStartComment(section);
      public void GenerateEnd(Section section) => EmitUnitEndComment(section);

      private static string PSVar(ID name) => $"${PSName(name)}";
      private static string PSVar(NamedElement name) => $"${PSName(name)}";
      private static string PSName(ID name) => name.AsIdentifier();
      private static string PSName(NamedElement name) => name.AsName();

      public void GenerateCode(Const c) {
         string value = "{PSVar(c)} = ";
         foreach (IConstElement e in c.elements) {
            value += e switch {
               STRING s => $"\"{s.value}\"",
               INT n    => n.value,
               FLOAT f  => f.value,
               Const ce => PSVar(ce),
               ID id    => PSVar(id),
               _        => throw new NotImplementedException(),
            };
         }
         emitter.Emitnl(value);
      }
      public void GenerateCode(Var v) => emitter.Emitnl($"{PSVar(v)}");
      public void GenerateCode(LIST l,string lwb,string upb) => emitter.Emitnl($"{PSVar(l)} = New-Object BoundedArray {lwb} {upb}");

      public void GenerateCode(IActualArg arg) => emitter.Emit(arg is STRING s ? s.value : arg is ID i ? PSVar(i) : throw new NotImplementedException());
      public void GenerateCode(Affix arg) => emitter.Emit($"${arg.id}");

      public void GenerateCodeExport(ID id) { }
      public void GenerateCodeImport(ID id) { }

      public void GenerateAlgorithmHeaderStart(Algorithm proc) => emitter.Emit($"function {PSName(proc.id)} (");
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
