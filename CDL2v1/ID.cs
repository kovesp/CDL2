using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CDL2v1 {
   /// <summary>
   /// Represents a reference to a named syntactic element, Arg or Local in the syntax tree.
   /// It contains the token it was created from.
   /// </summary>
   [Serializable]
   //[JsonConverter(typeof(IDJsonConverter))]
   public class ID : IConstElement, IMacroElement, IActualArg {
      [JsonIgnore]
      public string InternalName = string.Empty;
      [JsonInclude]
      public string Name = string.Empty;

      [JsonIgnore]
      public ID Id => this;

      public static void Dump() {
         Debug.WriteLine("ID Dump:\n--------");
         List<ID> sortedIDs = [.. Database.Instance.UniqueIDs.Values
                                     .OrderBy(id => id.Name)
                                     //.ThenBy (Id => Id.PhaseName)
                                     ];
         int maxNameLength = sortedIDs.Select(id => id.Name.Length).Max();
         // int maxTypeLength = sortedIDs.Select(Id => Id.TargetType.Length).Max();
         foreach (ID id in sortedIDs) Debug.WriteLine(id.ToString(/*maxNameLength/*,maxTypeLength*/));
         Debug.WriteLine("--------");
      }

      /// <summary>
      /// Returns the ID for the given token. If the ID does not exist, it is created.
      /// </summary>
      /// <param Id="token"></param>
      /// <returns></returns>
      public static ID From(Token token) {
         Debug.Assert(token.type == TT.ID && token.StringValue != null,"CreateID: Token is not an ID type or StringValue is null");
         if (Database.Instance.UniqueIDs.TryGetValue(token.StringValue,out ID? id)) {
            return id;
         } else {
            return Database.Instance.UniqueIDs[token.StringValue] = new ID(name:token.TokenString);
         }
      }
      public static ID From(string name) => new(name);
      /// <summary>
      /// Used to create the Procedures for SectionById Ludes.
      /// </summary>
      /// <param Id="container"></param>
      /// <param Id="ludeType">The reserved word representing the lude: PRELUDE, ROOT, POSTLUDE.</param>
      /// <returns></returns>
      public static ID From(RW ludeType) => From(ludeType.ToString());

      public readonly static ID ErrorID = new("ERROR");
      public readonly static ID AnonID = new("Anon");

      public ID() { }
      public ID(string name) {
         Name = name;
         InternalName = name.Trim().Replace(" ","");
      }

      /// <summary>
      /// Renames an ID.
      /// This allows changing the PhaseName of an ID without changing the ID itself, in particular where spaces are in the Id.
      /// </summary>
      /// <param PhaseName="newName"></param>
      public void Rename(string newName) {
         Name = newName;
         InternalName = newName.Trim().Replace(" ","");
      }

      public override bool Equals(object? obj) => obj is ID id && Name == id.Name /*&& InternalName == id.InternalName*/ ;
      public override int GetHashCode() => HashCode.Combine(/*InternalName,*/Name);
      public override string ToString() => Name;
 
      public static bool operator ==(ID left,ID right) => left is null ? right is null : left.Equals(right);
      public static bool operator !=(ID left,ID right) => !(left == right);
   }

   [Serializable]
   public class IDDictionary<V> : Dictionary<ID, V> { }
   [Serializable]
   public class IDSet : Set<ID> { }
   public class IDDictionaryJsonConverter<V> : JsonConverter<IDDictionary<V>> {
      public override IDDictionary<V> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
         //var dictionary = new Database.StringDictionary<V>();
         var dictionary = new IDDictionary<V>();

         if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

         while (reader.Read()) {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            // Read the key as a string
            string keyString = reader.GetString()!;
            // Read the value
            reader.Read();
            dictionary[ID.From(keyString)] = JsonSerializer.Deserialize<V>(ref reader, options)!;
         }
         return dictionary;
      }

      public override void Write(Utf8JsonWriter writer, IDDictionary<V> value, JsonSerializerOptions options) {
         writer.WriteStartObject();
         foreach (ID key in value.Keys) {
            Debug.WriteLine($"{key} -> {value[key]}");
            writer.WritePropertyName(key.Name);
            try {
               JsonSerializer.Serialize(writer, value[key], options);
            } catch (Exception e) {
               writer.Flush();
               writer.Dispose();
               Debugger.Break();
            }
         }
         writer.WriteEndObject();
      }
   }

   public class IDSetJsonConverter : JsonConverter<IDSet> {
      public override IDSet Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
         var set = new IDSet();
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
         writer.WriteStartArray();
         foreach (ID key in value) {
            writer.WriteStringValue(key.Name);
         }
         writer.WriteEndArray();
      }
   }

}
