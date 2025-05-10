using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CDL2v1 {
   internal partial class TargetCodeGenerator {

      protected EmitterBase emitter = new EmitterSink();
      #region Helpers
      protected void Newline(bool optional = false) {
         if (optional) emitter.EmitnlOption(); else emitter.Emitnl();
      }
      protected void EmitUnitStartComment(Container unit) => emitter.Emitnl($"# Begin {unit.ContainerName}");
      protected void EmitUnitEndComment(Container unit) => emitter.Emitnl($"# End {unit.ContainerName}");
      protected void GenerateComment(string comment) {
         foreach (string line in comment.Split('\n')) emitter.Emitnl("# ", line);
      }

      public void IncrementIndent() => emitter.IndentLevel++;
      public void DecrementIndent() => emitter.IndentLevel--;

      protected static bool HasMultipleStatments(Macro macro) => macro.elements.OfType<STRING>().Any(str => MatchMultipleStatementsRegex().IsMatch(str.value));

      protected static readonly Random Random =  new();
      protected static string RandomInitialValue => Random.Next(0, int.MaxValue).ToString() + "  <# Random value to catch uninitialized VARs, LOCALs, and output AFFIXes #>";

      [GeneratedRegex(@"(?<!['""])(?:\n|;)(?![^'""]*['""])", RegexOptions.Compiled)]
      private static partial Regex MatchMultipleStatementsRegex();
      #endregion Helpers

   }
}
