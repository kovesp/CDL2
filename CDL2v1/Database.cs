using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.Win32;

namespace CDL2v1
{
   [Serializable]
   public class Database {
      public static Database Instance { get; private set; } = new Database();

      [JsonInclude]
      public readonly Dictionary<ID,Program> Programs = [];   // Contains all the programs in the syntax tree.
      [JsonInclude]
      public Program? FirstProgram = null;                    // The first program in the syntax tree.
      [JsonInclude]
      public readonly Dictionary<ID,Module> Modules = [];     // Contains all the modules in the syntax tree.

      /// <summary>
      /// Used to ensure that multiple spellings of tokens produce the same ID.
      /// </summary>
      [JsonInclude]
      public readonly Dictionary<string,ID> UniqueIDs = [];

#if GroupCounter
      public long labelCounter = 0;
      /// <summary>
      /// Labels of the form Group0000000001, Group0000000002, etc.
      /// </summary>
      /// <remarks>The underlying labelCounter is public so its value can be saved into and restored from a DB to support CDL2 Lab like behaviour.</remarks>
      /// TODO: Implement the saving and restoring of the labelCounter.
      /// <returns>The next group label ID.</returns>
      private ID GetNextGroupLabel => ID.From(new Token($"Group{labelCounter++:D10}"));
      public static ID NextGroupLabel => Instance.GetNextGroupLabel;
#else
      public static ID NextGroupLabel => ID.AnonID;
#endif // GroupCounter
      internal Program? FindProgramByName(string programName) => Programs.TryGetValue(ID.From(new Token(programName)),out Program? program) ? program : null;
      private Database() { }

      public static void SaveXML(string filePath) {
         using FileStream fs = new(Path.ChangeExtension(filePath,"XML"),FileMode.Create);
         new XmlSerializer(typeof(Database)).Serialize(fs,Instance);
      }

      public static void LoadXML(string filePath) {
         using FileStream fs = new(Path.ChangeExtension(filePath,"XML"),FileMode.Open);
         Instance = (Database)new XmlSerializer(typeof(Database)).Deserialize(fs)!;
      }
         
         public Dictionary<ID,string> test = new() {
            { ID.ErrorID,"ErrorID" },
            { ID.AnonID,"AnonID" },
         };
      private static readonly JsonSerializerOptions serializationOptions = new() { WriteIndented = true };
      //public static void SaveJSON(string filePath) => File.WriteAllText(Path.ChangeExtension(filePath,"JSON"),JsonSerializer.Serialize(Instance,serializationOptions));
      public static void SaveJSON(string filePath) {

         string json = JsonSerializer.Serialize(Instance.FirstProgram,serializationOptions);
         Program prog = JsonSerializer.Deserialize<Program>(json);

      }

      public static void LoadJSON(string filePath) => Instance = JsonSerializer.Deserialize<Database>(File.ReadAllText(Path.ChangeExtension(filePath,"JSON")))!;

#if SaveAsXML
      public static void Save(string filePath) => SaveXML(filePath);
      public static void Load(string filePath) => LoadXML(filePath);
#else
      public static void Save(string filePath) => SaveJSON(filePath);
      public static void Load(string filePath) => LoadJSON(filePath);
#endif
   }
}
