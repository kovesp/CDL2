using System.Diagnostics;

namespace CDL2v1 {
   public class FileCodeEmiter : ICodeEmiter {
      private string? targetFileName;
      private StreamWriter? writer;

      public FileCodeEmiter(string targetFileName) => this.OpenTarget(targetFileName);

      public string Target => targetFileName ?? throw new InvalidOperationException("Target file is not open.");

      public void Emit(params string[] code) {
         if (writer == null) throw new InvalidOperationException("Target file is not open.");
         foreach (var item in code) {
            writer.Write(item);
         }
      }

      public void Emitnl(params string[] code) {
         Emit(code);
         Debug.Assert(writer != null);
         writer.WriteLine();
      }

      public void OpenTarget(string target) {
         if (writer != null) CloseTarget();
         targetFileName = target;
         writer = new StreamWriter(targetFileName);
      }

      public void CloseTarget() {
         writer?.Close();
         writer = null;
         targetFileName = null;
      }
   }
}