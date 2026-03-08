// <auto-gen>
//=======================================================================
// <copyright file="SerializationSupport.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>System.Object[]</creation-date>
// 
// <summary>
//   Implements serialization and deserialization of syntax elements.
// </summary>
// <attribution>
//   This file is part of the clean room reimplementation of the
//      CDL2 Compiler
//      CDL2 Laboratory
//      CDL2 Target Code Generators
//
//    Based on original work on CDL and CDL2 led by C. H. A. Koster
//    and the CDL2 team at the Universities of Berlin, Germany and
//    Nijmegen, The Netherlands.
//
//    The CDL2 Laboratory was the work of Epsilon GmbH, Berlin.
//    H. M. Stahl, H. Feuerhahn, JP. Dehotay, B. Böhringer
//    (and others I don't remember ... sorry).
//
//    This project is not affiliated with the original CDL2 project.
// </attribution>
//=======================================================================
// </auto-gen>

#define Debug
#define COMPRESSED_DATABASE
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace CDL2v1 {

   public static class Serializer {
      public static IToaster? Toaster { get; set; }
      public static string SerializeElement<T>(T element) where T : NamedElement => JsonSerializer.Serialize(element,serializationOptions);
#if SERIALIZED_UNDO_RECORDS
      public static T DeserializeElement<T>(Database.UndoRecord<NamedElement> undo) where T : NamedElement {
         T? element = JsonSerializer.Deserialize(undo.SerializedElement, undo.RecordType.AsType(), serializationOptions) as T;
         return element ?? throw new JsonException($"Deserializer.DeserializeElement: Could not deserialize undo record for {undo.RecordType}");
      }
#endif

      private static readonly JsonSerializerOptions serializationOptions = new() {
#if DEBUG_SERIALIZATION
         WriteIndented = true,
#endif
         Converters = {
            new DeclarationDictionaryJsonConverter(),
            new IDDictionaryJsonConverter<Guid>(),
            new IDSetJsonConverter(),
            new IElementListJsonConverter(),
            new IDJsonConverter(),
            new BoundedStackJsonConverter<Focus>(),
            new BoundedStackJsonConverter<Database.UndoRecord>(),
            new SwapableTopStackJsonConverter<Database.UndoRecord>(),
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
         },
         IncludeFields = true,
         //ReferenceHandler = ReferenceHandler.Preserve 
      };

#if COMPRESSED_DATABASE
      public const string DBExtension = ".lab.gz";

      /// <summary>
      /// Saves the database as compressed JSON.
      /// </summary>
      /// <param name="path">Base path for the file (extension will be replaced)</param>
      /// TODO: Add a backup capability to save the previous database as a backup before overwriting
      public static string SaveDB(string? path = null) {
         string pathWithSize = string.Empty;
         path ??= Settings.LabDBPath; // Use the default lab database path if not provided
         if (!path.EndsWith(DBExtension))
            path = Path.ChangeExtension(path,DBExtension);
         //TODO: Add a backup capability to save the previous database as a backup before overwriting
         CompressStringToFile(JsonSerializer.Serialize(Database.Instance,serializationOptions),path);

         pathWithSize = $"{path}, {new FileInfo(path).Length.HumanReadableSize()}";
         Logger.logger.WriteLine(1,$"CDL2: Compressed database saved to {pathWithSize}");
         return pathWithSize;
      }

      /// <summary>
      /// Loads a compressed JSON database from a file.
      /// </summary>
      /// <param name="path">Path for the file (extension will be replaced). Use the path form settings if not given.</param>
      /// <param name="addInstance">Whether to push the loaded database onto the stack</param>
      /// <param name="databaseName">Name for the loaded database</param>
      /// <returns>The loaded database or null if loading failed</returns>
      public static Database? LoadDB(string? path = null,bool addInstance = true) {
         Database? database = null;
         Toaster!.ShowToast($"Loading Lab DB from {Settings.LabDBPath}",() => {
            path ??= Settings.LabDBPath; // Use the default lab database path if not provided
            if (!path.EndsWith(DBExtension)) path = Path.ChangeExtension(path,DBExtension);
            if (!Path.Exists(path)) throw new FileNotFoundException($"CDL2: Database file not found at {path}");

            Logger.logger.WriteLine(1,$"CDL2: Loading compressed database from {path}");

            try {
               string json = DecompressFileToString(path);

               // Deserialize the JSON
               Database? db = JsonSerializer.Deserialize<Database>(json,serializationOptions);

               if (db is not null) {
                  if (addInstance) Database.AddInstance(db);
                  Logger.logger.WriteLine(1,$"CDL2: Loaded compressed database from {path} with name {db.Name}");
               }

               database = db;
            } catch (Exception ex) {
               Logger.logger.WriteLine(0,$"CDL2: Error loading compressed database: {ex.Message}");
               Logger.logger.WriteLine(0,$"CDL2: Stack trace: {ex.StackTrace}");
               database = null;
            }
         },minShowInterval: 2000);
         return database;
      }
#else
      public const string DBExtension = ".lab";
      public static void SaveDB(string filePath) {
         string path = Path.ChangeExtension(filePath, DBExtension);

         Logger.logger.WriteLine(1, $"Saving database to {path}");
         string json = JsonSerializer.Serialize(Database.Instance, serializationOptions);
         File.WriteAllText(path, json);
      }


      public static Database? LoadDB(string filePath, bool push = true, string databaseName="Loaded Database") {
         string path = Path.ChangeExtension(filePath, DBExtension);

         Logger.logger.WriteLine(1, $"Loading database from {path}");
         string json = File.ReadAllText(path);
         Database? db = JsonSerializer.Deserialize<Database>(json, serializationOptions);
         if (db is not null) {
            if (push) Database.PushDatabase(db);
            db.Name = databaseName;
            Logger.logger.WriteLine(1, $"Loaded database from {path} and name {db.Name}");
         }
         return db;
      }
#endif



      /// <summary>
      /// Compresses a string and saves it to a file.
      /// </summary>
      /// <param name="data">The string to compress</param>
      /// <param name="filePath">The path to save the compressed data to</param>
      public static void CompressStringToFile(string data,string filePath) {
         using FileStream fileStream = File.Create(filePath);
         using GZipStream gzipStream = new(fileStream,CompressionLevel.Optimal);
         using StreamWriter writer = new(gzipStream);

         writer.Write(data);
      }

      /// <summary>
      /// Reads and decompresses a file to a string.
      /// </summary>
      /// <param name="filePath">The path of the compressed file</param>
      /// <returns>The decompressed string</returns>
      public static string DecompressFileToString(string filePath) {
         using FileStream fileStream = File.OpenRead(filePath);
         using GZipStream gzipStream = new(fileStream,CompressionMode.Decompress);
         using StreamReader reader = new(gzipStream);

         return reader.ReadToEnd();
      }
   }


   /// <summary>
   /// Provides custom JSON serialization and deserialization for a <see cref="List{T}"/> of <see cref="IElement"/>
   /// objects.
   /// </summary>
   /// <remarks>This converter handles the serialization and deserialization of <see cref="IElement"/> objects
   /// by encoding their type and value into JSON. During deserialization, the converter expects each element to be
   /// represented as an object containing a "type" property and a "value" property. Supported types include "INT",
   /// "FLOAT", "STRING", and "ID".
   /// </remarks>
   public class IElementListJsonConverter : JsonConverter<List<IElement>> {
      public override List<IElement> Read(ref Utf8JsonReader reader,Type typeToConvert,JsonSerializerOptions options) {
         var list = new List<IElement>();
         if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected start of array for List<IElement>.");

         while (reader.Read()) {
            if (reader.TokenType == JsonTokenType.EndArray) break;
            if (reader.TokenType == JsonTokenType.StartObject) {
               reader.Read(); // Read the "type" property
               if (reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "type") {
                  throw new JsonException("Expected 'type' property for IElement.");
               }
               reader.Read(); // Read the type value
               string typeName = reader.GetString()!;
               reader.Read(); // Read the "value" property name
               if (reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "value" || (typeName != "INT" && typeName != "FLOAT" && typeName != "STRING" && typeName != "ID")) {
                  throw new JsonException($"Expected 'value' or 'guid' property for {typeName} IElement.");
               }
               reader.Read(); // Read the value
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
                  case "ID":
                     list.Add(new ID(reader.GetString()!));
                     break;
                  default:
                     throw new JsonException($"Unknown type '{typeName}' for IElement.");
               }
               reader.Read(); // Read the end of the object
               if (reader.TokenType != JsonTokenType.EndObject) {
                  throw new JsonException("Expected end of object for IElement.");
               }
            } else {
               throw new JsonException("Expected start of object for List<IElement>.");
            }
         }
         return list;
      }

      public override void Write(Utf8JsonWriter writer,List<IElement> value,JsonSerializerOptions options) {
         writer.WriteStartArray();
         foreach (IElement element in value) {
            writer.WriteStartObject();
            writer.WriteString("type",element.GetType().Name);
            switch (element) {
               case INT ei: writer.WriteNumber("value",ei.value); break;
               case FLOAT ef: writer.WriteNumber("value",ef.value); break;
               case STRING es: writer.WriteString("value",es.value); break;
               case ID id: writer.WriteString("value",id.CanonicalName); break;
            }
            writer.WriteEndObject();
         }
         writer.WriteEndArray();
      }
   }
   /// <summary>
   /// Provides custom JSON serialization and deserialization for a <see cref="Dictionary{TKey, TValue}"/> where the key is
   /// of type <see cref="RW"/> and the value is a <see cref="List{T}"/> of <see cref="ID"/> objects.
   /// </summary>
   /// <remarks>This converter is designed to handle JSON objects where the keys are string representations of the
   /// <see cref="RW"/> enum and the values are arrays of string representations of <see cref="ID"/> objects. It ensures
   /// proper parsing and validation of the JSON structure, throwing exceptions for invalid formats.
   /// 
   /// It is designed specifically for the Lude JSON format used in CDL2v1, where reserved words (RW) are used as keys.
   /// Not currently used as the standard mechanism handles it correctly, but kept for reference.
   /// </remarks>
   public class LudeJsonConverter : JsonConverter<Dictionary<RW,List<ID>>> {
      public override Dictionary<RW,List<ID>> Read(ref Utf8JsonReader reader,Type typeToConvert,JsonSerializerOptions options) {
         var dict = new Dictionary<RW,List<ID>>();
         if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected start of object for Dictionary<RW, List<ID>>.");
         while (reader.Read()) {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Expected property name for Dictionary<RW, List<ID>>.");
            string propertyName = reader.GetString()!;
            if (!Enum.TryParse<RW>(propertyName,out RW ludeType)) {
               throw new JsonException($"Unknown reserved word '{propertyName}' for Lude.");
            }
            reader.Read(); // Move to the start of the array
            if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Expected start of array for List<ID>.");
            var ids = new List<ID>();
            while (reader.Read()) {
               if (reader.TokenType == JsonTokenType.EndArray) break;
               if (reader.TokenType != JsonTokenType.String) throw new JsonException("Expected string value for ID.");
               ids.Add(new ID(reader.GetString()!));
            }
            dict[ludeType] = ids;
         }
         return dict;
      }

      public override void Write(Utf8JsonWriter writer,Dictionary<RW,List<ID>> value,JsonSerializerOptions options) {
         writer.WriteStartObject();
         foreach (RW ludeType in value.Keys) {
            writer.WritePropertyName(ludeType.ToString());
            writer.WriteStartArray();
            foreach (ID id in value[ludeType]) {
               writer.WriteStringValue(id.CanonicalName);
            }
            writer.WriteEndArray();
         }
         writer.WriteEndObject();
      }
   }

   /// <summary>
   /// Provides custom JSON serialization and deserialization for the <see cref="ID"/> type.
   /// </summary>
   /// <remarks>This converter handles the conversion of <see cref="ID"/> objects to and from their string
   /// representations during JSON serialization and deserialization. The string representation is expected to match the
   /// format defined by the <see cref="ID"/> type.
   /// 
   /// This is an optimization as IDs occur often in the CDL2v1 format, and using a custom converter avoids the overhead of writing it as an object with a single property.
   /// </remarks>
   public class IDJsonConverter : JsonConverter<ID> {
      public override ID Read(ref Utf8JsonReader reader,Type typeToConvert,JsonSerializerOptions options) {
         if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Expected string value for ID.");
         string idString = reader.GetString()!;
         return new ID(idString);
      }

      public override void Write(Utf8JsonWriter writer,ID value,JsonSerializerOptions options) => writer.WriteStringValue(value.CanonicalName);
   }

   /// <summary>
   /// Provides custom JSON serialization and deserialization for <see cref="IDDictionary{V}"/> objects.
   /// </summary>
   /// <remarks>This converter handles the serialization of <see cref="IDDictionary{V}"/> objects by writing
   /// their keys as strings using the canonical name of the <see cref="ID"/> type. During deserialization, it
   /// reconstructs the dictionary by parsing the keys back into <see cref="ID"/> instances and deserializing the
   /// associated values.
   /// </remarks>
   /// <typeparam name="V">The type of the values stored in the <see cref="IDDictionary{V}"/>.</typeparam>
   public class IDDictionaryJsonConverter<V> : JsonConverter<IDDictionary<V>> {
      public override IDDictionary<V> Read(ref Utf8JsonReader reader,Type typeToConvert,JsonSerializerOptions options) {
         var dictionary = new IDDictionary<V>();

         if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

         while (reader.Read()) {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            // Read the key as a string
            string keyString = reader.GetString()!;
            // Read the value
            reader.Read();
            dictionary[new ID(keyString)] = JsonSerializer.Deserialize<V>(ref reader,options)!;
         }
         return dictionary;
      }

      public override void Write(Utf8JsonWriter writer,IDDictionary<V> value,JsonSerializerOptions options) {
         //Debug.WriteLine($"Write(IDDictionary<{typeof(V)}>)");
         writer.WriteStartObject();
         foreach (ID key in value.Keys) {
            //Debug.WriteLine($"{key} -> {value[key]}");
            writer.WritePropertyName(key.CanonicalName);
            try {
               JsonSerializer.Serialize(writer,value[key],options);
            } catch (Exception e) {
               Debug.WriteLine(e.Message);
               Debugger.Break();
            }
         }
         writer.WriteEndObject();
      }
   }
   /// <summary>
   /// Provides custom JSON serialization and deserialization for the <see cref="Section.DeclarationDictionary"/> type.
   /// </summary>
   /// <remarks>This converter handles the serialization and deserialization of <see
   /// cref="Section.DeclarationDictionary"/>,  where the keys are <see cref="ID"/> objects and the values are <see
   /// cref="Guid"/> instances.  During serialization, the keys are written as their canonical string representations. 
   /// During deserialization, the keys are reconstructed from their string representations.
   /// </remarks>
   public class DeclarationDictionaryJsonConverter : JsonConverter<Section.DeclarationDictionary> {
      public override Section.DeclarationDictionary Read(ref Utf8JsonReader reader,Type typeToConvert,JsonSerializerOptions options) {
         var dictionary = new Section.DeclarationDictionary();
         if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();
         while (reader.Read()) {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            // Read the key as a string
            string keyString = reader.GetString()!;
            // Read the value
            reader.Read();
            dictionary[new ID(keyString)] = JsonSerializer.Deserialize<Guid>(ref reader,options)!;
         }
         return dictionary;
      }
      public override void Write(Utf8JsonWriter writer,Section.DeclarationDictionary value,JsonSerializerOptions options) {
         writer.WriteStartObject();
         foreach (ID key in value.Keys) {
            writer.WritePropertyName(key.CanonicalName);
            JsonSerializer.Serialize(writer,value[key],options);
         }
         writer.WriteEndObject();
      }
   }
   /// <summary>
   /// Provides custom JSON serialization and deserialization for the <see cref="IDSet"/> type.
   /// </summary>
   /// <remarks>This converter serializes an <see cref="IDSet"/> as a JSON array of strings, where each string
   /// represents the canonical name of an <see cref="ID"/> in the set. During deserialization, it reconstructs the <see
   /// cref="IDSet"/> from the array of strings.
   /// </remarks>
   public class IDSetJsonConverter : JsonConverter<IDSet> {
      public override IDSet Read(ref Utf8JsonReader reader,Type typeToConvert,JsonSerializerOptions options) {
         var set = new IDSet();
         if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException();

         while (reader.Read()) {
            if (reader.TokenType == JsonTokenType.EndArray) break;
            // Read the key as a string
            string keyString = reader.GetString()!;
            //set.Add(ID.From(keyString));
            set.Add(new ID(keyString)); // Notice that the string saved is the CanonicalName which is why this works
         }
         return set;
      }

      public override void Write(Utf8JsonWriter writer,IDSet value,JsonSerializerOptions options) {
         writer.WriteStartArray();
         foreach (ID key in value) {
            writer.WriteStringValue(key.CanonicalName);
         }
         writer.WriteEndArray();
      }
   }

   /// <summary>
   /// Provides custom JSON serialization and deserialization for <see cref="BoundedStack{T}"/>.
   /// Serializes as an object with "Capacity" and "Items" (top-to-bottom order).
   /// </summary>
   public class BoundedStackJsonConverter<T> : JsonConverter<BoundedStack<T>> {
      public override BoundedStack<T>? Read(ref Utf8JsonReader reader,Type typeToConvert,JsonSerializerOptions options) {
         if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected start of object for BoundedStack<T>.");

         int capacity = 0;
         List<T> items = new List<T>();

         while (reader.Read()) {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName)
               throw new JsonException("Expected property name in BoundedStack<T> object.");

            string propertyName = reader.GetString()!;
            reader.Read();

            if (propertyName == "Capacity") {
               capacity = reader.GetInt32();
            } else if (propertyName == "Items") {
               if (reader.TokenType != JsonTokenType.StartArray)
                  throw new JsonException("Expected start of array for Items in BoundedStack<T>.");
               while (reader.Read()) {
                  if (reader.TokenType == JsonTokenType.EndArray) break;
                  T item = JsonSerializer.Deserialize<T>(ref reader,options)!;
                  items.Add(item);
               }
            } else {
               reader.Skip();
            }
         }
         if (capacity < 1)
            throw new JsonException("BoundedStack<T> must have positive Capacity.");
         return new BoundedStack<T>(capacity,items);
      }

      public override void Write(Utf8JsonWriter writer,BoundedStack<T> value,JsonSerializerOptions options) {
         writer.WriteStartObject();
         writer.WriteNumber("Capacity",value.Capacity);
         writer.WritePropertyName("Items");
         writer.WriteStartArray();
         foreach (T item in value)
            JsonSerializer.Serialize(writer,item,options);
         writer.WriteEndArray();
         writer.WriteEndObject();
      }
   }
   /// <summary>
   /// Provides custom JSON serialization and deserialization for <see cref="Database.SwapableTopStack{T}"/>.
   /// Serializes as an object with "Capacity", "Items" (top-to-bottom order), and "Swap" state.
   /// </summary>
   public class SwapableTopStackJsonConverter<T> : JsonConverter<Database.SwapableTopStack<T>> {
      public override Database.SwapableTopStack<T>? Read(ref Utf8JsonReader reader,Type typeToConvert,JsonSerializerOptions options) {
         if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected start of object for SwapableTopStack<T>.");

         int capacity = 0;
         List<T> items = [];
         bool swap = false;

         while (reader.Read()) {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName)
               throw new JsonException("Expected property name in SwapableTopStack<T> object.");

            string propertyName = reader.GetString()!;
            reader.Read();

            if (propertyName == "Capacity") {
               capacity = reader.GetInt32();
            } else if (propertyName == "Items") {
               if (reader.TokenType != JsonTokenType.StartArray)
                  throw new JsonException("Expected start of array for Items in SwapableTopStack<T>.");
               while (reader.Read()) {
                  if (reader.TokenType == JsonTokenType.EndArray) break;
                  T item = JsonSerializer.Deserialize<T>(ref reader,options)!;
                  items.Add(item);
               }
            } else if (propertyName == "Swap") {
               swap = reader.GetBoolean();
            } else {
               reader.Skip();
            }
         }

         if (capacity < 1)
            throw new JsonException("SwapableTopStack<T> must have positive Capacity.");

         Database.SwapableTopStack<T> stack = new(capacity);
         foreach (T item in items.Reverse<T>())
            stack.Push(item);
         stack.Swap = swap;

         return stack;
      }

      public override void Write(Utf8JsonWriter writer,Database.SwapableTopStack<T> value,JsonSerializerOptions options) {
         writer.WriteStartObject();
         writer.WriteNumber("Capacity",value.Capacity);
         writer.WriteBoolean("Swap",value.Swap);
         writer.WritePropertyName("Items");
         writer.WriteStartArray();
         foreach (T item in value)
            JsonSerializer.Serialize(writer,item,options);
         writer.WriteEndArray();
         writer.WriteEndObject();
      }
   }
}
