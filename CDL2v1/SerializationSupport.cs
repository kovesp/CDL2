#define Debug
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using System.IO;
using static CDL2v1.TokenList;
using System.IO.Enumeration;
using System.Printing;
using static CDL2v1.Serializer;

namespace CDL2v1 {

   public class FilePaths {
      public string FileName;
      public string NDJsonPath;
#if DEBUG
      public string ReferenceDataPath;
      public string DBPath;
#endif
      public FilePaths(string filePath) {
         FileName = Path.GetFullPath(Path.GetFileNameWithoutExtension(filePath)); ;
         NDJsonPath = FileName + ".NDJSON";
#if DEBUG
         ReferenceDataPath = FileName + ".Ref.JSON";
         DBPath = FileName + ".DB.JSON";
#endif
      }
   }

   public class Serializer {
      private readonly JsonSerializerOptions SerializationOptionsNDJSON;
#if DEBUG
      private readonly JsonSerializerOptions SerializationOptionsIndentedJSON;
#endif

      private Database Input;
      private Database Output;

      public Serializer(Database? output = null, Database? input = null) {
         Input = input ?? Database.Instance;
         Output = output ?? Database.Instance;
         MacroElementListJsonConverter MacroElementListJsonConverter = new(Output);

         SerializationOptionsNDJSON = new() {
            WriteIndented = false,
            Converters = {
               new IDDictionaryJsonConverter<string>(),
               new IDDictionaryJsonConverter<Program>(),
               new IDDictionaryJsonConverter<Module>(),
               new IDDictionaryJsonConverter<Layer>(),
               new IDDictionaryJsonConverter<Section>(),
               new IDDictionaryJsonConverter<IProvidable>(),
               new IDDictionaryJsonConverter<IExportable>(),
               new IDDictionaryJsonConverter<CDL2Object>(),
               new IDSetJsonConverter(),
               MacroElementListJsonConverter,
               //new ListJsonConverter<Call>(),
               new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            },
            ReferenceHandler = ReferenceHandler.Preserve
         };
#if DEBUG
         SerializationOptionsIndentedJSON = new(SerializationOptionsNDJSON) { WriteIndented = true };
#endif
      }

      private FilePaths? filePaths;
      public void SaveJSON(string filePath) {
         filePaths = new(filePath);

         ReferenceData referenceData = ReferenceData.FromDatabase();

         //object objectToSerialize = Database.Instance;
         Module objectToSerialize = Database.Instance.Modules.Values.Skip(0).First();

         using (var writer = new StreamWriter(filePaths.NDJsonPath)) {
            string line = JsonSerializer.Serialize(referenceData, SerializationOptionsNDJSON);
            writer.WriteLine(line);
            line = JsonSerializer.Serialize(Input, SerializationOptionsNDJSON);
            writer.WriteLine(line);
         }
#if DEBUG
         File.WriteAllText(filePaths.ReferenceDataPath, JsonSerializer.Serialize(referenceData, SerializationOptionsIndentedJSON));
         File.WriteAllText(filePaths.DBPath, JsonSerializer.Serialize(objectToSerialize, SerializationOptionsIndentedJSON));
#endif

         Database result = LoadJSON<Database>()!;


         // No need to go back to do all the fixups.
         // 1. Macro Elements
         foreach (Macro macro in Output.NamedElements.Values.OfType<Macro>()) {
            for (int i = 0 ; i < macro.elements.Count ; i++) {
               if (macro.elements[i] is MacroElementPlaceholder placeholder) {
                  if (Output.NamedElements.TryGetValue(placeholder.GUID, out NamedElement? element)) {
                     macro.elements[i] = (IMacroElement)element;
                  } else {
                     Debug.WriteLine($"Serializer.LoadJSON: {placeholder.typeName} {placeholder.GUID} not found");
                     Debugger.Break();
                  }
               }

            }
         }
         // 2. ...
      }


      public T? LoadJSON<T>() {
         using StreamReader reader = new(filePaths!.NDJsonPath);
         string? line;
         if ((line = reader.ReadLine()) != null) {
            ReferenceData referenceData = JsonSerializer.Deserialize<ReferenceData>(line, SerializationOptionsNDJSON)!;
            referenceData?.SetDatabaseReferrenceData(Output);
         }
         if ((line = reader.ReadLine()) != null) {
            T result = JsonSerializer.Deserialize<T>(line, SerializationOptionsNDJSON)!;
            Output.NamedElementIDsToNamedElements();
            return result;
         }
         return default;
      }

      public class IDDictionaryJsonConverter<V> : JsonConverter<IDDictionary<V>> {
         public override IDDictionary<V> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            var dictionary = new IDDictionary<V>("IDDictionaryJsonConverter.Read.dictionary");

            if (reader.TokenType != JsonTokenType.StartObject)
               throw new JsonException();

            while (reader.Read()) {
               if (reader.TokenType == JsonTokenType.EndObject) break;
               // Read the key as a string
               string keyString = reader.GetString()!;
               (string key, string typeName) = keyString.Split2('-');
               Type? type = Type.GetType($"CDL2v1.{typeName}");
               // Read the value
               reader.Read();
               try {
                  dictionary[ID.From(key)] = (V)JsonSerializer.Deserialize(ref reader, type!, options)!;
               } catch (Exception e) {
                  Debug.WriteLine($"IDDictionaryJsonConverter: {keyString} -> {e.Message}");
                  Debugger.Break();
               }
            }
            return dictionary;
         }

         public override void Write(Utf8JsonWriter writer, IDDictionary<V> value, JsonSerializerOptions options) {
            writer.WriteStartObject();
            foreach (ID key in value.Keys) {
               writer.WritePropertyName($"{key.Name}-{value[key]!.GetType().Name}");
               try {
                  JsonSerializer.Serialize(writer, value[key], value[key]!.GetType(), options);
               } catch (Exception e) {
                  Debug.WriteLine(e.Message);
                  Debugger.Break();
               }
            }
            writer.WriteEndObject();
         }
      }

      public class IDSetJsonConverter : JsonConverter<IDSet> {
         public override IDSet Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            IDSet set = new();
            if (reader.TokenType != JsonTokenType.StartArray)
               throw new JsonException();

            while (reader.Read()) {
               if (reader.TokenType == JsonTokenType.EndArray) break;
               // Read the key as a string
               string keyString = reader.GetString()!;
               set.Add(ID.From(keyString));
            }
            return set;
         }

         public override void Write(Utf8JsonWriter writer, IDSet value, JsonSerializerOptions options) {
            // Debug.WriteLine($"JsonConverter.Write(IDSet {value.Name})");
            writer.WriteStartArray();
            foreach (ID key in value) {
               writer.WriteStringValue(key.Name);
            }
            writer.WriteEndArray();
         }
      }

      public class MacroElementPlaceholder(string typeName, string guid) : IMacroElement {
         public string GUID = guid;
         public string typeName = typeName;
         public ID Id { get; set; } = ID.AnonID; // Not use but needed for interface
      }

      public class MacroElementListJsonConverter(Database output) : JsonConverter<List<IMacroElement>> {
         private readonly Database Output = output;

         public override List<IMacroElement> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            List<IMacroElement> list = [];

            if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("MacroElementListJsonConverter.Read expected [ for element list");

            while (reader.Read()) {
               if (reader.TokenType == JsonTokenType.EndArray) break;
               if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("MacroElementListJsonConverter.Read expected [ for element");
               reader.Read();
               string typeName = reader.GetString()!;
               reader.Read();
               switch (typeName) {
                  case "INT":
                     list.Add(new INT(reader.GetInt64()));
                     break;
                  case "FLOAT":
                     list.Add(new FLOAT(reader.GetDouble()));
                     break;
                  case "STRING":
                     list.Add(new STRING(reader.GetString()!));
                     break;
                  default:
                     string guid = reader.GetString()!;
                     list.Add(new MacroElementPlaceholder(typeName, guid));
                     //if (Output.NamedElements.TryGetValue(new Guid(guid), out NamedElement? element) && element is IMacroElement macroElement) {
                     //   list.Add(macroElement);
                     //} else {
                     //   throw new JsonException($"MacroElementListJsonConverter.Read: {typeName} {guid} not found");
                     //}
                     break;
               }
               reader.Read();
               if (reader.TokenType != JsonTokenType.EndArray) throw new JsonException("MacroElementListJsonConverter.Read expected ] for element list");
            }
            return list;
         }

         public override void Write(Utf8JsonWriter writer, List<IMacroElement> value, JsonSerializerOptions options) {

            writer.WriteStartArray();
            foreach (IMacroElement elem in value) {
               writer.WriteStartArray();
               writer.WriteStringValue(elem.GetType().Name);
               switch (elem) {
                  case INT ei: writer.WriteNumberValue(ei.value); break;
                  case FLOAT ef: writer.WriteNumberValue(ef.value); break;
                  case STRING es: writer.WriteStringValue(es.value); break;
                  case NamedElement n: writer.WriteStringValue(n.GUID); break;
               }
               writer.WriteEndArray();
            }
            writer.WriteEndArray();
         }
      }

   //   public class ListJsonConverter<T>() : JsonConverter<List<T>> {
   //      public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
   //         List<T> list = [];

   //         if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("ListJsonConverter.Read expected [ for element list");

   //         while (reader.Read()) {
   //            if (reader.TokenType == JsonTokenType.EndArray) break;
   //            T elem = JsonSerializer.Deserialize<T>(ref reader, options)!;
   //            list.Add(elem);
   //         }
   //         return list;
   //      }

   //      public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options) {

   //         writer.WriteStartArray();
   //         foreach (T elem in value) {
   //            JsonSerializer.Serialize(writer, elem, options);
   //         }
   //         writer.WriteEndArray();
   //      }
   //   }
   //   public class CallJsonConverter() : JsonConverter<Call> {
   //      public override Call Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
   //         Call call;

   //         if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("MacroElementListJsonConverter.Read expected [ for element list");


   //         return call;
   //      }

   //      public override void Write(Utf8JsonWriter writer, Call value, JsonSerializerOptions options) {
   //         writer.WriteStartObject();
   //         writer.WritePropertyName("id");
   //         writer.WriteStringValue(value.id.Name);
   //         writer.WritePropertyName("args");
   //         JsonSerializer.Serialize(writer, value.args, options);
   //         writer.WritePropertyName("ContainingProc");
   //         writer.WriteStringValue(value.ContainingProc.GUID);
   //         writer.WritePropertyName("IsBuiltin");
   //         writer.WriteBooleanValue(value.IsBuiltin);
   //         writer.WriteEndObject();
   //      }
   //   }
   } 
}
