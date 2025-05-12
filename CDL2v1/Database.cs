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
   [Serializable]
   public class Database {
      /// <summary>
      /// This is a singleton class.
      /// </summary>
      private Database() { }
      public static Database Instance { get; private set; } = new Database();

      [JsonInclude]
      public readonly IDDictionary<Program> Programs = [];       // Contains all the programs in the syntax tree.
      [JsonInclude]
      public Program? FirstProgram = null;                        // The first program in the syntax tree.
      [JsonInclude]
      public readonly Dictionary<ID, Module> Modules = [];         // Contains all the modules in the syntax tree.

      /// <summary>
      /// When a note is added to an element, the element is also added here.
      /// Should be cleared at the begining of compilation.
      /// In database mode, i.e., when operating on smaller units (e.g., algorithms) the Parser and SemanticAnalyzer must ensure that
      /// analyzed elements are appropriately removed or added.
      /// </summary>
      [JsonInclude]
      public readonly Set<NamedElement> ElementsWithNotes = [];   // Contains all the arguments in the syntax tree.

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

      internal Program? FindProgramByName(string programName) => Programs.TryGetValue(ID.From(new Token(programName)), out Program? program) ? program : null;

#if SaveAsXML
      public static void SaveXML(string filePath) {
         using FileStream fs = new(Path.ChangeExtension(filePath,"XML"),FileMode.Create);
         new XmlSerializer(typeof(Database)).Serialize(fs,Instance);
      }

      public static void LoadXML(string filePath) {
         using FileStream fs = new(Path.ChangeExtension(filePath,"XML"),FileMode.Open);
         Instance = (Database)new XmlSerializer(typeof(Database)).Deserialize(fs)!;
      }
#endif // SaveAsXML
#if SaveAsJSON

      //public class StringDictionary<V> : Dictionary<string, V> { }

      //public class IDDictionary<V> /*: IDictionary<ID,V>*/ {
      //   [JsonInclude]
      //   public StringDictionary<V> Dictionary = [];
      //   //private List<ID> keys = [];

      //   public IDDictionary() { }
      //   public IDDictionary(Dictionary<ID, V> dictionary) {
      //      foreach (ID key in dictionary.Keys) Add(key, dictionary[key]);
      //   }
      //   public IDDictionary(Dictionary<string, V> dictionary) => Dictionary = dictionary as StringDictionary<V>;
      //   public IDDictionary(StringDictionary<V> dictionary) => Dictionary = dictionary;



      //   public ICollection<ID> Keys => [.. Dictionary.Keys.Select(k => ID.From(k))];

      //   public ICollection<V> Values => Dictionary.Values;
      //   public int Count => Dictionary.Count;
      //   public bool IsReadOnly => false;

      //   //public Dictionary<string, V> ToSerializableDictionary()
      //   //    => Dictionary.ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value);

      //   //public void FromSerializableDictionary(Dictionary<string, V> serializableDictionary) 
      //   //   => Dictionary = serializableDictionary.ToDictionary(kvp => ID.From(kvp.Key), kvp => kvp.Value);

      //   public void Clear() => Dictionary.Clear();
      //   public void Add(ID key, V value) {
      //      //keys.Add(key);
      //      Dictionary.Add(key.InternalName, value);
      //   }
      //   public bool ContainsKey(ID key) => Dictionary.ContainsKey(key.InternalName);
      //   public bool Remove(ID key) => Dictionary.Remove(key.InternalName);
      //   public bool TryGetValue(ID key, [MaybeNullWhen(false)] out V value) => Dictionary.TryGetValue(key.InternalName, out value);
      //   public void Add(KeyValuePair<ID, V> item) => Add(item.Key, item.Value);
      //   public bool Contains(KeyValuePair<ID, V> item) => Dictionary.Contains(new(item.Key.InternalName, item.Value));
      //   //public void CopyTo(KeyValuePair<ID, V>[] array, int arrayIndex) => throw new NotImplementedException();
      //   //public bool Remove(KeyValuePair<ID, V> item) => throw new NotImplementedException();
      //   //public IEnumerator<KeyValuePair<ID, V>> GetEnumerator() => throw new NotImplementedException();
      //   //IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      //   public V this[ID key] {
      //      get => Dictionary[key.InternalName];
      //      set => Dictionary[key.InternalName] = value;
      //   }
      //}

      //[JsonInclude]
      //[JsonConverter(typeof(IDDictionaryJsonConverter<string>))]
      //public static IDDictionary<string> test = new(new Dictionary<ID, string> {
      //     { ID.ErrorID, "ErrorID" },
      //     { ID.AnonID, "AnonID" }
      //   });
      //[JsonConverter(typeof(IDDictionaryJsonConverter<string>))]
      public static IDDictionary<string> test = new() {
           { ID.ErrorID, "ErrorID" },
           { ID.AnonID, "AnonID" }
         };


      private static readonly JsonSerializerOptions serializationOptions = new() { 
         WriteIndented = true,
         Converters = { 
            new IDDictionaryJsonConverter<string>(),
            new IDDictionaryJsonConverter<Program>(),
            new IDDictionaryJsonConverter<IProvidable>(),
            new IDSetJsonConverter(),
         }, 
         ReferenceHandler = ReferenceHandler.Preserve 
       };
      public static void SaveJSON(string filePath) {
         string path = Path.ChangeExtension(filePath, "JSON");

         string json = JsonSerializer.Serialize(Database.Instance.Programs, serializationOptions);
         Debug.WriteLine(json);
         File.WriteAllText(path, json);

         //test.Clear();

         json = File.ReadAllText(path);
         var progs = JsonSerializer.Deserialize<IDDictionary<Program>>(json, serializationOptions);

      }


      public static void LoadJSON(string filePath) => Instance = JsonSerializer.Deserialize<Database>(File.ReadAllText(Path.ChangeExtension(filePath,"JSON")))!;
#endif // SaveAsJSON

#if SaveAsXML
      public static void Save(string filePath) => SaveXML(filePath);
      public static void Load(string filePath) => LoadXML(filePath);
#elif SaveAsJSON
      public static void Save(string filePath) => SaveJSON(filePath);
      public static void Load(string filePath) => LoadJSON(filePath);
#else
      public static void Save(string filePath) { }
      public static void Load(string filePath) { }
#endif
   }

}
