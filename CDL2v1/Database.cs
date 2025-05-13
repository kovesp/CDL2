#define SaveAsJSON
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
using System.Diagnostics.CodeAnalysis;
using System.Collections;
using System.Diagnostics;

namespace CDL2v1 {
   /// <summary>
   /// Entrypoint for all data maintanined by the Compiler.
   /// In memory dta holder for a future CDL2 Lab implementation.
   /// </summary>
   public class Database {
      /// <summary>
      /// This is a singleton class.
      /// </summary>
      private Database() { }
      public static Database Instance { get; private set; } = new Database();

      [JsonInclude]
      public readonly IDDictionary<Program> Programs = [];       // Contains all the programs in the syntax tree.
      [JsonInclude]
      private ID? firstProgram = null;                        // The first program in the syntax tree.
      [JsonIgnore]
      public Program? FirstProgram {
         get {  if (firstProgram is null) {
               if (Programs.Count > 0) {
                  firstProgram = Programs.Values.First().Id;
               } else {
                  firstProgram = ID.ErrorID;
               }
            }
            return Programs.TryGetValue(firstProgram, out Program? prog) ? prog : null;
         }
         set => firstProgram = value?.Id ?? ID.ErrorID;
      }
      [JsonInclude]
      public readonly IDDictionary<Module> Modules = [];         // Contains all the modules in the syntax tree.

      /// <summary>
      /// When a note is added to an element, the element is also added here.
      /// Should be cleared at the begining of compilation.
      /// In database mode, i.e., when operating on smaller units (e.g., algorithms) the Parser and SemanticAnalyzer must ensure that
      /// analyzed elements are appropriately removed or added.
      /// </summary>
      [JsonInclude]
      public Set<NamedElement> ElementsWithNotes { get;  } = [];  

      /// <summary>
      /// Used to ensure that multiple spellings of tokens produce the same ID.
      /// </summary>
      [JsonInclude]
      public readonly Dictionary<string, ID> UniqueIDs = [];

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
      /// <summary>
      /// Contains the full id of all the named elements in the database.
      /// THe elements themselves contain the GUID, the NamedElementID can then be used to locate the actual element.
      /// This will be used in serializtion to avoid multiple copies of an element.
      /// </summary>
      [JsonIgnore]
      public Dictionary<Guid,NamedElement> NamedElements { get; } = []; // Records all named elements in the database.
      public Dictionary<Guid, NamedElementID> NamedElementIDs { get; } = []; // Records the NamedElementIDs of all named elements in the database.
      /// <summary>
      /// Record the element in Namedelements with a NamedElementId.
      /// </summary>
      /// <param name="element"></param>
      public static void Record(NamedElement element) => Instance.NamedElements[element.GUID] = element;
      public void NamedElementsToNamedElementIDs() {
         NamedElementIDs.Clear();
         foreach (KeyValuePair<Guid,NamedElement> element in NamedElements) NamedElementIDs[element.Key] = new(element.Value);
      }
      public void NamedElementIDsToNamedElements() {
         NamedElements.Clear();
         foreach (KeyValuePair<Guid, NamedElementID> element in NamedElementIDs) NamedElements[element.Key] = element.Value.GetElement()!;
      }

      internal Program? FindProgramByName(string programName) => Programs.TryGetValue(ID.From(new Token(programName)), out Program? program) ? program : null;

      private static readonly JsonSerializerOptions serializationOptions = new() { 
         WriteIndented = true,
         Converters = { 
            new IDDictionaryJsonConverter<string>(),
            new IDDictionaryJsonConverter<Program>(),
            new IDDictionaryJsonConverter<Module>(),
            new IDDictionaryJsonConverter<Layer>(),
            new IDDictionaryJsonConverter<Section>(),
            new IDDictionaryJsonConverter<IProvidable>(),
            new IDDictionaryJsonConverter<IExportable>(),
            new IDSetJsonConverter(),
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
         }, 
         ReferenceHandler = ReferenceHandler.Preserve 
       };
      public static void SaveJSON(string filePath) {
         Instance.NamedElementsToNamedElementIDs();
         foreach (NamedElementID id in Instance.NamedElementIDs.Values.Take(10)) Debug.WriteLine(id.ToString());
         string path = Path.ChangeExtension(filePath, "JSON");

         string json = JsonSerializer.Serialize(Database.Instance, serializationOptions);
         Debug.WriteLine(json);
         File.WriteAllText(path, json);


         json = File.ReadAllText(path);
         Database? db = JsonSerializer.Deserialize<Database>(json, serializationOptions);

      }


      public static void LoadJSON(string filePath) => Instance = JsonSerializer.Deserialize<Database>(File.ReadAllText(Path.ChangeExtension(filePath,"JSON")))!;

      public static void Save(string filePath) => SaveJSON(filePath);
      public static void Load(string filePath) => LoadJSON(filePath);

   }

}
