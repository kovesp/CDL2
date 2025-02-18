using System.Diagnostics;

namespace CDL2v1 {
   internal class FileCodeEmitter : CodeEmitterBase {
      private StreamWriter? writer = null;
      private string? target = null;

      /// <summary>
      /// The target file name. Setting this will close the current file and open a new one.
      /// The new one is opened only if the target is not null or empty.
      /// This will throw an exception if the file cannot be opened.
      /// </summary>
      public override string Target {
         get => target??"";
         set {
            writer?.Close();
            writer = null;
            target = value;
            if (target is not null && target != "") writer = new StreamWriter(value);            
         }
      }

      public FileCodeEmitter() => Target = "";
      public FileCodeEmitter(string targetFileName) => Target = targetFileName;
      ~FileCodeEmitter() => Target = "";

      /// <summary>
      /// Write the item to the target file.
      /// </summary>
      /// <param name="item"></param>
      protected override void Write(string item) => writer?.Write(item);
   }
}