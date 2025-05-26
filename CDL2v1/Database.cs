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
using System.Xml.Linq;

namespace CDL2v1 {
   /// <summary>
   /// Entrypoint for all data maintanined by the Compiler.
   /// In memory dta holder for a future CDL2 Lab implementation.
   /// </summary>
   public class Database {
      /// <summary>
      /// This is a singleton class.
      /// </summary>
      public Database() { }
      public static Database Instance { get; private set; } = new Database();

      /// <summary>
      /// Maps the cannonical form of identifiers (i.e., with whitespace removed) to the original form.
      /// </summary>
      [JsonInclude]
      [JsonPropertyOrder(0)]
      public Dictionary<string, string> CanonicalNames = [];
      /// <summary>
      /// Add a name to the canonical name list.
      /// If the canonical form is already in the dictioary make no changes. this preserves the first seen spaacing of the name.
      /// </summary>
      /// <param name="name"></param>
      /// <returns>The canonical name.</returns>
      public string AddCanonicalName(string name) {
         string canonicalName = name.Replace(" ", "");
         if (CanonicalNames.TryGetValue(canonicalName, out string? _)) Instance.CanonicalNames[canonicalName] = name;
         return canonicalName;
      }
      /// <summary>
      /// Rename a canonical name. This simply involves adding a new name to the dictionary with the new spelling.
      /// This can be used to change just the spelling of the name (i.e., spacing), or to completely change the name.
      /// In the latter case, the old name is not removed from the dictionary because it may be used in places other than the current context.
      /// This allows fpr the use of an identifier in multiple sections with the rename applying only the section of the object being renamed.
      /// </summary>
      /// <param name="oldName">not used</param>
      /// <param name="newName">the new name</param>
      /// <returns>the (possibly new) canonical name</returns>
      public string RenameCanonicalName(string oldName,string newName) {
         string canonicalName = newName.Replace(" ", "");
         CanonicalNames[canonicalName] = newName;
         return canonicalName;
      }
      /// <summary>
      /// The diplay name of the identifer.
      /// </summary>
      /// <param name="name"></param>
      /// <returns></returns>
      public string DisplayName(string name) => CanonicalNames.TryGetValue(name.Replace(" ", ""), out string? displayName) ? displayName : name;

      /// <summary>
      /// All the named elements in the database. Every other reference uses the GUID.
      /// </summary>
      [JsonInclude]
      [JsonPropertyOrder(1)]
      public Dictionary<Guid,NamedElement> NamedElements = [];
      /// <summary>
      /// Add a named element to the database.
      /// </summary>
      /// <param name="element"></param>
      public void AddNamedElement(NamedElement element) => NamedElements[element.GUID] = element;

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
      /// Contains the guids of all the programs in the syntax tree.
      /// </summary>
      [JsonInclude]
      [JsonPropertyOrder(2)]
      public List<Guid> Programs = [];      

      /// <summary>
      /// Return the first program which is used as the main program if none is specified.
      /// </summary>
      [JsonIgnore]
      public Program? FirstProgram => Programs.Count == 0 ? null : NamedElements[Programs[0]] as Program;
            /// <summary>
      /// All the modules in the database.
      /// </summary>
      [JsonInclude][JsonPropertyOrder(3)]
      public Set<Guid> Modules = [];

      public bool TryGetNamedElement<T>(string name, [MaybeNullWhen(false)] out IEnumerable<T> elements) where T : NamedElement{
         IEnumerable<T> typedElements = NamedElements.Values.OfType<T>();
         if (name == null) {
            if (typedElements.Any()) {
               elements = [typedElements.First()];
            } else {
               elements = [];
            }
         } else {
            elements = typedElements.Where(e => e.Name == name);
         }
         return elements.Any();
      }
      public bool TryGetSingleNamedelement<T>(string name, [MaybeNullWhen(false)] out T element) where T : NamedElement {
         if (TryGetNamedElement(name, out IEnumerable<T> elements) && elements.Count() == 1) {
            element = elements.First();
            return true;
         } else {
            element = null!;
            return false; 
         }
      }


      /// <summary>
      /// Used to ensure that multiple spellings of tokens produce the same ID.
      /// </summary>
      //[JsonInclude][JsonPropertyOrder(0)]
      //public Dictionary<string, ID> UniqueIDs = [];





      /// <summary>
      /// When a note is added to an element, the element is also added here.
      /// Should be cleared at the begining of compilation.
      /// In database mode, i.e., when operating on smaller units (e.g., algorithms) the Parser and SemanticAnalyzer must ensure that
      /// analyzed elements are appropriately removed or added.
      /// </summary>
      [JsonIgnore]
      public Set<NamedElement> ElementsWithNotes = [];
      [JsonInclude][JsonPropertyOrder(2)]
      public Set<NamedElementID> ElementsWithNoteIDs = [];

      /// <summary>
      /// Contains the full id of all the named elements in the database.
      /// THe elements themselves contain the GUID, the NamedElementID can then be used to locate the actual element.
      /// This will be used in serializtion to avoid multiple copies of an element.
      /// </summary>
      //[JsonIgnore]
      //public Dictionary<Guid,NamedElement> NamedElements = []; // Records all named elements in the database.
      [JsonInclude][JsonPropertyOrder(1)]
      public Dictionary<string, NamedElementID> NamedElementIDs = []; // Records the NamedElementIDs of all named elements in the database.

      /// <summary>
      /// Record the element in Namedelements with a NamedElementId.
      /// </summary>
      /// <param name="element"></param>
      public static void Record(NamedElement element) => Instance.NamedElements[element.GUID] = element;

      public void NamedElementsToNamedElementIDs() {
         NamedElementIDs.Clear();
         foreach (KeyValuePair<Guid,NamedElement> element in NamedElements) NamedElementIDs[element.Key.ToString()] = new(element.Value);
         ElementsWithNoteIDs.Clear();
         foreach (NamedElement element in ElementsWithNotes) ElementsWithNoteIDs.Add(NamedElementIDs[element.GUID.ToString()]);
      }
      public void NamedElementIDsToNamedElements() {
         NamedElements     = NamedElenentIDsToNamedElements(NamedElementIDs);
         ElementsWithNotes = NamedElenentIDsToNamedElements(ElementsWithNoteIDs,NamedElements);
      }
      public static Dictionary<Guid, NamedElement> NamedElenentIDsToNamedElements(Dictionary<string, NamedElementID> namedElements) {
         Dictionary<Guid, NamedElement> elements = [];
         foreach (KeyValuePair<string, NamedElementID> element in namedElements) elements[Guid.Parse(element.Key)] = element.Value.GetElement()!;
         return elements;
      }
      public static Set<NamedElement> NamedElenentIDsToNamedElements(Set<NamedElementID> namedElementIDs, Dictionary<Guid, NamedElement>? namedElements = null) {
         Set<NamedElement> elements = [];
         namedElements ??= [];
         foreach (NamedElementID element in namedElementIDs) elements.Add(namedElements.TryGetValue(element.GUID,out NamedElement? elem) ? elem : element.GetElement()!);
         return elements;
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

         string path = Path.ChangeExtension(filePath, "JSON");

         string json = JsonSerializer.Serialize(Database.Instance, serializationOptions);
         File.WriteAllText(path, json);

         //json = File.ReadAllText(path);

         var db = JsonSerializer.Deserialize<Database>(json, serializationOptions);
         db?.NamedElementIDsToNamedElements();

      }


      public static void LoadJSON(string filePath) => Instance = JsonSerializer.Deserialize<Database>(File.ReadAllText(Path.ChangeExtension(filePath,"JSON")))!;

      public static void Save(string filePath) => SaveJSON(filePath);
      public static void Load(string filePath) => LoadJSON(filePath);

   }

}
