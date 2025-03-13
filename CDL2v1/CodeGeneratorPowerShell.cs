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

      enum PSVarType { Var, List, Const, Affix, Local }

      private static string PSVarPrefix(PSVarType type) => type switch {
         PSVarType.Var => "V_",
         PSVarType.List => "LL_",
         PSVarType.Const => "C_",
         PSVarType.Affix => "A_",
         PSVarType.Local => "L_",
         _ => throw new NotImplementedException(),
      };

      private static string PSVar(ID name,PSVarType type,string prefix = "",string suffix = "") => $"${prefix}{PSVarPrefix(type)}{PSName(name)}{suffix}";
      private static string PSVar(NamedElement name) => $"${PSName(name)}";
      private static string PSName(ID name) => name.AsIdentifier();
      private static string PSName(NamedElement name) => name.AsName();

      public void GenerateCode(Const c) {
         string value = $"{PSVar(c.id,PSVarType.Const)} = ";
         foreach (IConstElement e in c.elements) {
            value += e switch {
               STRING s => $"\"{s.value}\"",
               INT n => n.value,
               FLOAT f => f.value,
               Const ce => PSVar(ce.id,PSVarType.Const),
               //ID id    => PSVar(id),
               _ => throw new NotImplementedException(),
            };
         }
         emitter.Emitnl(value);
      }
      public void GenerateCode(Var v) => emitter.Emitnl($"{PSVar(v)}");
      public void GenerateCode(LIST l,string lwb,string upb) => emitter.Emitnl($"{PSVar(l)} = New-Object BoundedArray {lwb} {upb}");

      public void GenerateCodeExport(ID id) { }
      public void GenerateCodeImport(ID id) { }

      public void GenerateAlgorithmHeaderStart(Algorithm proc) => emitter.Emit($"function {PSName(proc.id)} (");
      public void GenerateAlgorithmHeaderEnd(Algorithm proc) {
         emitter.Emitnl(") {");
         emitter.IndentLevel++;
      }

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
      public void GenerateLudeEnd(RW ludeType,Container section) => throw new NotImplementedException();
      public void GenerateStart(Macro macro) { }
      public void GenerateEnd(Macro macro) {
         emitter.IndentLevel--;
         emitter.NlEmitnl("}");
      }
      public void GenerateCode(LIST l) => throw new NotImplementedException();
      public void GenerateExport(Module module,ID expId) => throw new NotImplementedException();
      public void GenerateLudeStart(RW ludeType,Section section) => throw new NotImplementedException();
      public void GenerateLudeEnd(RW ludeType,Section section) => throw new NotImplementedException();
      public void GenerateParamSeparator() => emitter.Emit(",");


      private void EmitUnitStartComment(Container unit) => emitter.Emitnl($"# Begin {unit.ContainerName}");
      private void EmitUnitEndComment(Container unit) => emitter.Emitnl($"# End {unit.ContainerName}");
      //public void GenerateLocalDeclaration(ID affix) => emitter.Emitnl($"   {PSVar(affix)} = $null");
      public void GenerateMacroElemInt(long value) => emitter.Emit(value);
      public void GenerateMacroElemFloat(double value) => emitter.Emit(value);
      public void GenerateMacroElemString(string value) {
         string[] lines = value.Split('\n');
         foreach (string line in lines.SkipLast(1)) emitter.Emitnl(line);
         emitter.Emit(lines.Last());
      }
      public void GenerateReferenceVar(ID id) => emitter.Emit(PSVar(id,PSVarType.Var,"_"));
      public void GenerateReferenceList(ID id) => emitter.Emit(PSVar(id,PSVarType.List));
      public void GenerateReferenceConst(ID id) => emitter.Emit(PSVar(id,PSVarType.Const));
      public void GenerateReferenceAffix(ID id) => emitter.Emit(PSVar(id,PSVarType.Affix,"_"));
      public void GenerateReferenceLocal(ID id) => emitter.Emit(PSVar(id,PSVarType.Local));
      public void GenerateDeclareLocal(ID local) => emitter.Emit(PSVar(local,PSVarType.Local)," = $null");
      public void GenerateDeclareAffix(ID affix,AD dir) {
         switch (dir) {
            case AD.input:
               emitter.Emit(PSVar(affix,PSVarType.Affix,"_"));
               break;
            case AD.NONE:
               throw new NotImplementedException();
            default:
               emitter.Emit("[ref]",PSVar(affix,PSVarType.Affix));
               break;
         }
      }
      public void GenerateCodeConst(ID id) => throw new NotImplementedException();
      public void GenerateCodeDeclareVar(ID id) => emitter.Emit(PSVar(id,PSVarType.Var));
      public void GenerateCodeDeclareList(ID id,ID lwb,ID upb) => emitter.Emitnl(PSVar(id,PSVarType.List),$" = new BoundArray({PSVar(lwb,PSVarType.Const)},{PSVar(upb,PSVarType.Const)})");
      public void GenerateInitializeAffixOrVar(ID id,AD affixDir,bool isVar = false) {
         PSVarType type = isVar ? PSVarType.Var : PSVarType.Affix;
         string value = isVar ? "" : ".Value";
         switch (affixDir) {
            case AD.NONE:
               throw new NotImplementedException();
            case AD.input:
               break;
            case AD.output:
               emitter.Emitnl(PSVar(id,type,"_")," = ",0);
               break;
            case AD.transput:
               emitter.Emitnl(PSVar(id,type,"_")," = ",PSVar(id,type,suffix: value));
               break;

         }
      }

      public void GenerateFinalizeAffixOrVar(ID id,AD affixDir,bool isVar = false) {
         PSVarType type = isVar ? PSVarType.Var : PSVarType.Affix;
         string value = isVar ? "" : ".Value";
         switch (affixDir) {
            case AD.NONE:
               throw new NotImplementedException();
            case AD.input:
               break;
            default:
               emitter.Emitnl(PSVar(id,type,suffix: value)," = ",PSVar(id,type,"_"));
               break;
         }
      }

      public void Newline() => emitter.Emitnl();
      public void GenerateMacroBodyStart(Macro macro) {
         if (macro.CanFail) emitter.Emitnl("$__b = (");
         emitter.IndentLevel++;
      }
      public void GenerateMacroBodyEnd(Macro macro) {
         emitter.Emitnl();
         emitter.IndentLevel--;
         if (macro.CanFail) emitter.Emitnl(")");
      }

      public void FinalizationStart(Algorithm algorithm,bool IsNeeded) {
         if (IsNeeded && algorithm.CanFail) {
            emitter.Emitnl("if ($__b) {");
            emitter.IndentLevel++;
         }
      }
      public void FinalizationEnd(Algorithm algorithm,bool IsNeeded) {
         if (algorithm.CanFail) {
            if (IsNeeded) {
               emitter.IndentLevel--;
               emitter.Emitnl("}");
            }
            emitter.Emitnl("return $__b");
         }
      }
   }
}
