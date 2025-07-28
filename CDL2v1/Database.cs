// <auto-gen>
//=======================================================================
// <copyright file="Database.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-03-17</creation-date>
// 
// <summary>
//   Responsilbe for the content of the Lab database.
//   It contains the parsed syntax trees of CDL2 Programs and Modules.
//   THe Database class also handles the saving and loading of the database to and from a gzip compressed JSON file.
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
using System.Text.RegularExpressions;

namespace CDL2v1 {
   /// <summary>
   /// Base class for all classes that will be serialize.
   /// This is just a convenience so that items in the serliazied form can be more easily identified.
   /// </summary>
   public class SerializationBase {
#if DEBUG_SERIALIZATION
      [JsonInclude][JsonPropertyOrder(0)]
      public string Type;
      [JsonConstructor]
      public SerializationBase() => Type = GetType().Name;
#else
      public SerializationBase() { }
#endif // DEBUG_SERIALIZATION
   }
   /// <summary>
   /// Entrypoint for all data maintanined by the Compiler.
   /// In memory dta holder for a future CDL2 Lab implementation.
   /// </summary>
   public class Database : SerializationBase {
      /// <summary>
      /// This is a singleton class.
      /// </summary>
      public Database() { }
      public static Database Instance => DatabaseStack.Count > 0 ? DatabaseStack.Peek() : throw new InvalidOperationException("Database stack is empty. No database instance available.");

      private static readonly Stack<Database> DatabaseStack = [];
      static Database() => PushDatabase();

      public static void PushDatabase(Database? db = null) => DatabaseStack.Push(db ?? new Database());
      public static void PopDatabase() {
         if (DatabaseStack.Count > 1) {
            DatabaseStack.Pop();
         } else {
            throw new InvalidOperationException("Cannot pop the last database instance from the stack.");
         }
      }

      public string Name = "Database";

      /// <summary>
      /// Maps the cannonical form of identifiers (i.e., with whitespace removed) to the original form.
      /// </summary>
      [JsonInclude]
      [JsonPropertyOrder(1)]
      public Dictionary<string,string> CanonicalNames = [];
      /// <summary>
      /// All the named elements in the database. Every other reference uses the GUID.
      /// </summary>
      [JsonInclude]
      [JsonPropertyOrder(2)]
      public Dictionary<Guid,NamedElement> NamedElements = [];
      /// <summary>
      /// Undo records for NamedElements.  entries are inserted wheneer an elment is changed or removed.
      /// Each entry is a stack of undo records for the element.
      /// A record contains
      /// <ol>
      /// <li> The time the record was created.</li>
      /// <li> An optional tag</li>
      /// <li> The type of the element (required for deserialization</li>
      /// <li> The serialized element</li>"
      /// </ol>
      /// </summary>
      [JsonInclude]
      [JsonPropertyOrder(3)]
      public Dictionary<Guid,Stack<UndoRecord<NamedElement>>> NamedElementUndoRecords = [];
      /// <summary>
      /// Contains the guids of all the programs in the syntax tree.
      /// </summary>
      [JsonInclude]
      [JsonPropertyOrder(4)]
      public List<Guid> Programs = [];
      /// <summary>
      /// All the modules in the database.
      /// </summary>
      [JsonInclude]
      [JsonPropertyOrder(5)]
      public List<Guid> Modules = [];
      /// <summary>
      /// When a note is added to an element, the element is also added here.
      /// Should be cleared at the begining of compilation.
      /// In database mode, i.e., when operating on smaller units (e.g., algorithms) the Parser and SemanticAnalyzer must ensure that
      /// analyzed elements are appropriately removed or added.
      /// </summary>
      [JsonInclude]
      [JsonPropertyOrder(6)]
      public Set<Guid> ElementsWithNotes = [];

      /// <summary>
      /// Bookmarks are managed in the Focus class.
      /// </summary>
      [JsonInclude]
      [JsonPropertyOrder(7)]
      public Dictionary<string,Focus> Bookmarks = [];


      /// <summary>
      /// Add a name to the canonical name list.
      /// If the canonical form is already in the dictioary make no changes. this preserves the first seen spaacing of the name.
      /// </summary>
      /// <param name="name"></param>
      /// <returns>The canonical name.</returns>
      public string AddCanonicalName(string name) {
         string canonicalName = name.Replace(" ","");
         CanonicalNames.TryAdd(canonicalName,name);
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
         string canonicalName = newName.Replace(" ","");
         CanonicalNames[canonicalName] = newName;
         return canonicalName;
      }
      /// <summary>
      /// The diplay name of the identifer.
      /// </summary>
      /// <param name="name"></param>
      /// <returns></returns>
      public string DisplayName(string name) => CanonicalNames.TryGetValue(name.Replace(" ",""),out string? displayName) ? displayName : name;

      public class UndoRecord<T> : SerializationBase where T : NamedElement {
         [JsonInclude][JsonPropertyOrder(1)] public DateTime Timestamp { get; } = DateTime.Now;
         [JsonInclude][JsonPropertyOrder(2)] public string Tag { get; set; } = "";
         [JsonInclude][JsonPropertyOrder(3)] public string RecordType { get; set; } = "";
         [JsonInclude][JsonPropertyOrder(4)] public string SerializedElement { get; set; }

         public UndoRecord(T element) {
            RecordType = element.GetType().Name;

            SerializedElement = Serializer.SerializeElement(element) ?? "";
         }

         [JsonConstructor]
         public UndoRecord() => SerializedElement = "";
      }

      /// <summary>
      /// Create an undo record for the given named element.
      /// </summary>
      /// <param name="element"></param>
      public void RecordUndo(NamedElement element) {
         if (!NamedElementUndoRecords.TryGetValue(element.GUID,out Stack<UndoRecord<NamedElement>>? undoStack)) {
            NamedElementUndoRecords[element.GUID] = undoStack = new Stack<UndoRecord<NamedElement>>();
         }
         undoStack.Push(new UndoRecord<NamedElement>(element));
      }
      /// <summary>
      /// Add a tag to the top undo record for the given named element.
      /// </summary>
      /// <param name="guid"></param>
      /// <param name="label"></param>
      /// <returns></returns>
      public bool TagUndoRecord(Guid guid,string label) {
         if (NamedElementUndoRecords.TryGetValue(guid,out Stack<UndoRecord<NamedElement>>? undoStack) && undoStack.Count > 0) {
            undoStack.Peek().Tag = label;
            return true;
         } else {
            return false;
         }
      }
      public bool TagUndoRecord(NamedElement element,string label) => TagUndoRecord(element.GUID,label);
      public T GetUndo<T>(Guid guid) where T : NamedElement {
         if (NamedElementUndoRecords.TryGetValue(guid,out Stack<UndoRecord<NamedElement>>? undoStack) && undoStack.Count > 0) {
            if (undoStack.Peek().RecordType != typeof(T).Name) {
               throw new InvalidCastException($"Cannot cast undo record of type {undoStack.Peek().RecordType} to {typeof(T).Name}");
            }
            return Serializer.DeserializeElement<T>(undoStack.Pop())!;
         } else {
            throw new KeyNotFoundException($"No undo record found for element with GUID {guid}");
         }
      }

      /// <summary>
      /// Add a named element to the database.
      /// Add to Programs or Modules if of that type.
      /// </summary>
      /// <param name="element"></param>
      public void AddNamedElement(NamedElement element) {
         if (element is not IUnrecordedElement) {
            NamedElements[element.GUID] = element;
            if (element is Program) {
               Programs.Add(element.GUID);
            } else if (element is Module) {
               Modules.Add(element.GUID);
            }
         }
      }

      /// <summary>
      /// All named elements of a given type.
      /// Mostly a debug convenience.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <returns></returns>
      public static IEnumerable<T> NamedElementsOfType<T>(Func<T,bool>? pred = null,Func<IEnumerable<T>,IEnumerable<T>>? mapper = null) where T : NamedElement
         => Instance.NamedElements.Values.OfType<T>().OptWhere(pred).OptMap(mapper is not null,mapper!);
      public static IEnumerable<T> NamedElementsOfType<T>(Func<T,bool>? pred = null,bool asList = false) where T : NamedElement => NamedElementsOfType<T>(pred,asList ? Enumerable.ToList : null);
      public static IEnumerable<Const> NamedConsts(Func<Const,bool>? pred = null) => NamedElementsOfType<Const>(pred,true);
      public static IEnumerable<LIST> NamedLists(Func<LIST,bool>? pred = null) => NamedElementsOfType<LIST>(pred,true);
      public static IEnumerable<Var> NamedVars(Func<Var,bool>? pred = null) => NamedElementsOfType<Var>(pred,true);
      public static IEnumerable<Algorithm> NamedAlgorithms(Func<Algorithm,bool>? pred = null) => NamedElementsOfType<Algorithm>(pred,true);
      public static IEnumerable<Macro> NamedMacros(Func<Macro,bool>? pred = null) => NamedElementsOfType<Macro>(pred,true);
      public static IEnumerable<Procedure> NamedProcedures(Func<Procedure,bool>? pred = null) => NamedElementsOfType<Procedure>(pred,true);

      /// <summary>
      /// Remove an element
      /// </summary>
      /// <param name="element"></param>
      public void RemoveElement(NamedElement element) {
         RecordUndo(element);
         NamedElements.Remove(element.GUID);
         if (element is Program) {
            Programs.Remove(element.GUID);
         } else if (element is Module) {
            Modules.Remove(element.GUID);
         }
      }
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
      /// Return the first program which is used as the main program if none is specified.
      /// </summary>
      [JsonIgnore]
      public Program? FirstProgram => Programs.Count == 0 ? null : NamedElements[Programs[0]] as Program;

      public static bool TryGetNamedElements<T>(string name,[MaybeNullWhen(false)] out IEnumerable<T> elements) where T : NamedElement
         => TryGetNamedElements(Instance.NamedElements.Values,name,out elements);
      /// <summary>
      /// Try and get a collection of elements of type T with the given name from the given collection of NamedElements.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="objects"></param>
      /// <param name="name">This can be empty to match all, </param>
      /// <param name="elements"></param>
      /// <returns></returns>
      public static bool TryGetNamedElements<T>(IEnumerable<NamedElement> objects,string name,[MaybeNullWhen(false)] out IEnumerable<T> elements) where T : NamedElement {
         IEnumerable<T> typedElements = objects.OfType<T>();
         if (name == string.Empty) {
            elements = typedElements;
         } else if (name.StartsWith('/')) {
            elements = typedElements.Where(e => Regex.IsMatch(e.Id.CanonicalName,name.Trim('/').RemoveWhitespace()));
         } else {
            elements = typedElements.Where(e => e.Id.CanonicalName.Contains(name.RemoveWhitespace()));
         }
         return elements.Any();
      }

      public bool TryGetNamedElement<T>(string name,[MaybeNullWhen(false)] out T element) where T : NamedElement {
         if (TryGetNamedElements(name,out IEnumerable<T>? elements) && elements.Count() == 1) {
            element = elements.First();
            return true;
         } else {
            element = null!;
            return false;
         }
      }
      public bool IsNamedElement<T>(string name) where T : NamedElement => NamedElements.Values.OfType<T>().Any(elem => elem.Id == name);
      public bool IsNamedElement<T>(ID id) where T : NamedElement => IsNamedElement<T>(id.CanonicalName);

      public Program? ProgramByName(string programName) => NamedElements.Values.OfType<Program>().FirstOrDefault(p => p.Id == programName);
      public Program? ProgramByName(ID ProgramId) => NamedElements.Values.OfType<Program>().FirstOrDefault(p => p.Id == ProgramId);
      public Module? ModuleByName(string moduleName) => NamedElements.Values.OfType<Module>().FirstOrDefault(m => m.Id == moduleName);
      public Module? ModuleByName(ID moduleId) => NamedElements.Values.OfType<Module>().FirstOrDefault(m => m.Id == moduleId);


      public static string Save(string? filePath = null) => Serializer.SaveDB(filePath);
      public static void Load(string? filePath = null) => Serializer.LoadDB(filePath);
      public static void InitializeForTests() {
         // Can't using runsettings to set the working directory
         Directory.SetCurrentDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,@"..\..\..\..\CDL2v1\CDL2"));
         Load();
      }
   }
}
