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
//   Responsible for the content of the Lab database.
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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json.Serialization;

namespace CDL2v1 {
   /// <summary>
   /// Base class for all classes that will be serialize.
   /// This is just a convenience so that items in the serialized form can be more easily identified.
   /// </summary>
   public class SerializationBase {
#if DEBUG_SERIALIZATION
      [JsonInclude][JsonPropertyOrder(0)]
      public string Type;
      [JsonConstructor]
      public SerializationBase() => Type = GetType().Name;
#else
      [JsonConstructor]
      public SerializationBase() { }
#endif // DEBUG_SERIALIZATION
   }
   /// <summary>
   /// Entry point for all data maintained by the Compiler.
   /// In memory data holder for a future CDL2 Lab implementation.
   /// </summary>
   public class Database : SerializationBase {
      /// <summary>
      /// An instance of this class is created per test thread, it is a singleon for the non-test process main thread and its UI thread.
      /// </summary>
      [JsonConstructor]
      public Database() => FocusStack.Push(new Focus());

      /// <summary>
      /// This is needed to support multiple instances used by unit testing.
      /// Note that this is thread-safe.
      /// </summary>
      private static readonly ConcurrentDictionary<int,Database> Instances = new();
      private static readonly int DefaultThreadId;

      public static Database Instance {
         get {
            if (Instances.TryGetValue(Environment.CurrentManagedThreadId,out Database? db)) {
               return db;
            } else {
               return Instances[DefaultThreadId];
            }
         }
      }

      /// <summary>
      /// Adds a database instance for the current thread. This is used by <cref href="Serializer.LoadDB(string?)"/>
      /// to create a new instance of the database for the current thread.
      /// </summary>
      /// <param name="db"></param>
      public static void AddInstance(Database? db = null) => Instances[Environment.CurrentManagedThreadId] = db ?? new Database();

      static Database() {
         AddInstance();
         DefaultThreadId = Environment.CurrentManagedThreadId; // The instance associated with this thread id will be used if the current thread does not have one of its own.
      }

      public string Name = "Database";

      /// <summary>
      /// Timer for auto-save functionality
      /// </summary>
      [JsonIgnore]
      private System.Timers.Timer? _autoSaveTimer = null;

      /// <summary>
      /// Used by autosave when implemented.
      /// Set by Program.Modified and Module.Modified
      /// </summary>
      public bool Modified {
         get => ModificationCount > 0;
         set {
            if (value) {
               ModificationCount++;
               if (Settings.SettingValue<int>("AutosaveCount") > 0 && Settings.SettingValue<int>("AutosaveCount") <= ModificationCount) Autosave();
            } else {
               ModificationCount = 0;
            }
         }
      }

      /// <summary>
      /// Configures the auto-save timer based on the AutoSaveInterval setting.
      /// </summary>
      /// <param name="intervalSeconds">Auto-save interval in seconds. If 0, auto-save is disabled.</param>
      public void ConfigureAutoSave(int intervalSeconds) {
         StopAutoSave();

         if (intervalSeconds > 0) {
            _autoSaveTimer = new System.Timers.Timer {
               Interval = intervalSeconds * 1000, // Timer uses milliseconds
               AutoReset = true
            };
            _autoSaveTimer.Elapsed += (s, e) => Autosave();
            _autoSaveTimer.Start();
         }
      }

      /// <summary>
      /// Stops the auto-save timer if it is running.
      /// </summary>
      public void StopAutoSave() {
         if (_autoSaveTimer is not null) {
            _autoSaveTimer.Stop();
            _autoSaveTimer.Dispose();
            _autoSaveTimer = null;
         }
      }

      /// <summary>
      /// Performs an automatic save of the current state. Intended to persist changes without explicit user action.
      /// 
      /// TODO AUTOSAVE NOT CURRENTLY IMPLEMENTED
      /// </summary>
      private void Autosave() {
         if (ModificationCount > 0) {

         }
      }

      private int ModificationCount = 0;
      public int GetModificationCount() => ModificationCount;

      /// <summary>
      /// Maps the canonical form of identifiers (i.e., with whitespace removed) to the original form.
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
      /// Undo records for NamedElements.  entries are inserted whenever an elment is changed or removed.
      /// Each entry is a stack of undo records for the element.
      /// A record contains
      /// <ol>
      /// <li> The time the record was created.</li>
      /// <li> An optional tag</li>
      /// <li> The type of the element (required for deserialization</li>
      /// <li> The serialized element</li>"
      /// </ol>
      /// </summary>
      private const int UndoStackDefaultSize = 100;
      [JsonInclude]
      [JsonPropertyOrder(3)]
      public SwapableTopStack<UndoRecord> UndoStack = new(UndoStackDefaultSize);
      [JsonInclude]
      [JsonPropertyOrder(4)]
      public BoundedStack<UndoRecord> RedoStack = new(UndoStackDefaultSize);
      /// <summary>
      /// Contains the guids of all the programs in the syntax tree.
      /// </summary>
      [JsonInclude]
      [JsonPropertyOrder(5)]
      public List<Guid> Programs = [];
      [JsonIgnore] public IEnumerable<Program> ProgramObjects => Programs.Select(guid => NamedElements[guid] as Program).Where(p => p is not null).Cast<Program>();
      /// <summary>
      /// All the modules in the database.
      /// </summary>
      [JsonInclude]
      [JsonPropertyOrder(6)]
      public List<Guid> Modules = [];
      [JsonIgnore] public IEnumerable<Module> ModuleObjects => Modules.Select(guid => NamedElements[guid] as Module).Where(m => m is not null).Cast<Module>();
      /// <summary>
      /// When a note is added to an element, the element is also added here.
      /// Should be cleared at the begining of compilation.
      /// In database mode, i.e., when operating on smaller units (e.g., algorithms) the Parser and SemanticAnalyzer must ensure that
      /// analyzed elements are appropriately removed or added.
      /// </summary>
      [JsonInclude]
      [JsonPropertyOrder(7)]
      public Set<Guid> ElementsWithNotes = [];

      /// <summary>
      /// Bookmarks are managed in the Focus class.
      /// </summary>
      [JsonInclude]
      [JsonPropertyOrder(8)]
      public Dictionary<string,Focus> Bookmarks = [];

      public static void SetBookmark(string bookmarkName) {
         if (string.IsNullOrWhiteSpace(bookmarkName))
            return;
         if (Instance.Bookmarks.ContainsKey(bookmarkName)) {
            Instance.Bookmarks[bookmarkName] = Instance.FocusStack.Peek();
         } else {
            Instance.Bookmarks.Add(bookmarkName,Instance.FocusStack.Peek());
         }
      }
      public static bool RestoreBookmark(string bookmarkName,bool push = false) {
         if (string.IsNullOrWhiteSpace(bookmarkName))
            return false;
         if (Instance.Bookmarks.TryGetValue(bookmarkName,out Focus? bookmarkedFocus)) {
            if (!push) Instance.FocusStack.Pop();
            Instance.FocusStack.Push(bookmarkedFocus);
            return true;
         }
         return false;
      }
      public static void RemoveBookmark(string bookmarkName) {
         if (string.IsNullOrWhiteSpace(bookmarkName))
            return;
         Instance.Bookmarks.Remove(bookmarkName);
      }
      public static void ClearBookmarks() => Instance.Bookmarks.Clear();

      private const int DefaultFocusStackSize = 10;
      /// <summary>
      /// The focus can be pushed or popped to allow for easier navigation.
      /// It is not preserved across sessions.
      /// </summary>

      public readonly BoundedStack<Focus> FocusStack = new(DefaultFocusStackSize);

      [JsonInclude][JsonPropertyOrder(9)] public Guid CurrentFocusGuid = Guid.Empty;

      [JsonIgnore]
      private CommandInterpreter? _CLI;
      [JsonIgnore]
      private bool _isCLISet = false;

      /// <summary>
      /// Ensure CLI is set only once.
      /// It will be set when the GUI is created to use the GUI for output.
      /// </summary>
      [JsonIgnore]
      public CommandInterpreter CLI {
         get => _CLI ??= new();
         set {
            if (_isCLISet) {
               throw new InvalidOperationException("CLI has already been set. Cannot be changed.");
            } else {
               _CLI = value;
               _isCLISet = true;
            }
         }
      }

      /// <summary>
      /// Add a name to the canonical name list.
      /// If the canonical form is already in the dictionary make no changes. this preserves the first seen spacing of the name.
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
      /// The display name of the identifier.
      /// </summary>
      /// <param name="name"></param>
      /// <returns></returns>
      public string DisplayName(string name) => CanonicalNames.TryGetValue(name.Replace(" ",""),out string? displayName) ? displayName : name;

      public class SwapableTopStack<T>(int maxSize) : BoundedStack<T>(maxSize) {
         private bool _swap = false;
         public bool Swap { 
            get => _swap;
            set { 
               if (value && _swap) throw new InvalidOperationException("SwapableTopStack Swap is already set to true.");
               _swap = value;
            }
         }
         public override bool Push(T item) {
            T top = item;
            if (_swap && Count > 0) {
               top = Pop();
               base.Push(item);
            }
            _swap = false;
            return base.Push(top);
         }
      }

      /// <summary>
      /// Contains the information required to resurect a removed object. Editing an object is
      /// treated as a remove followed by a create.
      /// Records a timestamp when the record was created, possibly a tag allowing a named reference to the record,
      /// the GUID of the object itself and flags indicating the interface declarations the object was contained in.
      /// Note that the actual object remains in the NamedElements dictionary, it is just not referenced by any other object.
      /// It is removed from the NamedElements dictionary when the undo record is pushed out of the BoundedStack.
      /// </summary>
      public class UndoRecord : IDisposable {
         [JsonInclude][JsonPropertyOrder(1)] public DateTime Timestamp { get; } = DateTime.Now;
         [JsonInclude][JsonPropertyOrder(2)] public string Tag { get; set; } = "";
         [JsonInclude][JsonPropertyOrder(3)] public Guid ObjectGuid { get; set; } = Guid.Empty;
         [JsonInclude][JsonPropertyOrder(4)] public InterfaceTypes InterfaceStatus { get; set; } = InterfaceTypes.None;
         [JsonInclude][JsonPropertyOrder(5)] public RW LudeType { get; set; } = RW.NONE;
         [JsonInclude][JsonPropertyOrder(6)] public ChangeType ChangeType { get; set; }

         // For rename operations
         [JsonInclude][JsonPropertyOrder(7)] public string OriginalName { get; set; } = "";
         [JsonInclude][JsonPropertyOrder(8)] public string NewName { get; set; } = "";
         [JsonInclude][JsonPropertyOrder(9)] public bool UpdateReferences { get; private set; }
         [JsonInclude][JsonPropertyOrder(10)] public ID Id { get; set; } = ID.AnonID;

         // For replace operations
         [JsonInclude][JsonPropertyOrder(11)] public Guid ReplacementGuid { get; set; } = Guid.Empty;
         [JsonInclude][JsonPropertyOrder(12)] public int Position { get; set; } = int.MaxValue;
         [JsonInclude][JsonPropertyOrder(13)] public int OriginalPosition { get; set; } = int.MaxValue;

         // Derived values
         [JsonIgnore] public NamedElement? Object => Database.Instance.NamedElements.TryGetValue(ObjectGuid,out NamedElement? obj) ? obj : null;
         [JsonIgnore] public NamedElement? ReplacementObject => Database.Instance.NamedElements.TryGetValue(ReplacementGuid,out NamedElement? obj) ? obj : null;
         [JsonIgnore] public CDL2Object? CDL2Object => Database.Instance.NamedElements.TryGetValue(ObjectGuid,out NamedElement? obj) ? obj as CDL2Object : null;
         [JsonIgnore] public NamedElement? MoveTarget { get => ReplacementObject; set => ReplacementGuid = value?.GUID ?? Guid.Empty; }

         public static string DisplayPosition(int pos,string qual="at") => $"{qual} {(pos == int.MaxValue ? "bottom" : pos == 0 ? "top" : "position "+ pos)}";

         public override string ToString() => $"UndoRecord: {Description()}";
         public string Description() => ChangeType switch {
            ChangeType.Renamed          => $"Renamed          {OriginalName} ==> {NewName}",
            ChangeType.InterfaceChanged => $"InterfaceChanged {Object!.FQDN()} {InterfaceStatus} ==> {(Object as CDL2Object)!.GetInterfaces()}",
            ChangeType.Added            => $"Added            {Object!.FQDN()} {DisplayPosition(Position)}",
            ChangeType.Removed          => $"Removed          {Object!.FQDN()} {DisplayPosition(Position)}",
            ChangeType.Replaced         => $"Replaced         {Object!.FQDN()}",
            ChangeType.InterfaceAdded   => $"InterfaceAdded   {InterfaceStatus} {Id} in {Object!.FQDN()}",
            ChangeType.InterfaceRemoved => $"InterfaceRemoved {InterfaceStatus} {Id} in {Object!.FQDN()}",
            ChangeType.LudeAdded        => $"LudeAdded        {LudeType} {(Object is Section ? "" : Id)} in {Object!.FQDN()}",
            ChangeType.LudeRemoved      => $"LudeRemoved      {LudeType} {(Object is Section ? "" : $"{Id} {DisplayPosition(Position)}")} in {Object!.FQDN()}",
            ChangeType.LudeReplaced     => $"LudeReplaced     in {Object!.FQDN()}",
            ChangeType.MovedRelative    => $"MovedRelative    {Object!.FQDN()} {DisplayPosition(OriginalPosition,"from")} {DisplayPosition(Position,"to")}",
            ChangeType.MovedAbsolute    => $"MovedAbsolute    {Object!.FQDN()} {DisplayPosition(Position,"to")} in {MoveTarget!.Container.FQDN()}",
            _                           => $"{ChangeType} unknown",
         };

         /// <summary>
         /// Alias
         /// </summary>
         [JsonIgnore]
         public Guid LudeProcGuid {
            get => ReplacementGuid;
            set => ReplacementGuid = value;
         }


         [JsonIgnore]
         public ST InterfaceType => InterfaceStatus switch {
            InterfaceTypes.Abstr => ST.ABSTR,
            InterfaceTypes.Ext => ST.EXT,
            InterfaceTypes.Inv => ST.INV,
            InterfaceTypes.Export => ST.EXPORT,
            InterfaceTypes.Import => ST.IMPORT,
            InterfaceTypes.None => ST.INVALID,
            _ => throw new InvalidOperationException($"InterfaceStatus contains multiple types or an invalid value: {InterfaceStatus}")
         };

         public UndoRecord(NamedElement? element,ChangeType changeType) {
            ObjectGuid = element?.GUID ?? Guid.Empty;
            InterfaceStatus = element?.GetInterfaces() ?? InterfaceTypes.None;
            ChangeType = changeType;
            Position = element?.Siblings.IndexOf(element.GUID)+1 ?? int.MaxValue;
         }
         /// <summary>
         /// Records relative moves of objects and old positions to their new positions
         /// </summary>
         /// <param name="element"></param>
         /// <param name="position"></param>
         /// <param name="changeType"></param>
         public UndoRecord(NamedElement? element,int position,ChangeType changeType) : this(element,changeType) {
            Position = position;
            if (element is not null) {
               int pos = element.Siblings.IndexOf(element.GUID);
               OriginalPosition = pos < element.Siblings.Count-1 ? pos : int.MaxValue;
            }
         }

         /// <summary>
         /// Records Addtions and their context
         /// </summary>
         /// <param name="element"></param>
         /// <param name="context"></param>
         /// <param name="changeType"></param>
         public UndoRecord(CDL2Object? element,NamedElement? context,ChangeType changeType) : this(element,changeType) {
            Position = context is CDL2Object contextElement ? contextElement.Siblings.IndexOf(contextElement.GUID)+1 : int.MaxValue;
         }

         public UndoRecord(Guid replaced,Guid replacement,ChangeType changeType) : this(null,changeType) {
            ObjectGuid = replaced;
            ReplacementGuid = replacement;
         }

         /// <summary>
         /// Used when an object is renamed.
         /// </summary>
         /// <param name="element"></param>
         /// <param name="originalName"></param>
         /// <param name="newName"></param>
         public UndoRecord(ID id,string originalName,string newName,bool updateReferences,ChangeType changeType) : this(null,changeType) {
            Id = id;
            OriginalName = originalName;
            NewName = newName;
            UpdateReferences = updateReferences;
         }

         //public UndoRecord(CDL2Object element,InterfaceTypes interfaceType,ChangeType changeType) : this(element,changeType) {
         //   InterfaceStatus = interfaceType;
         //}

         [JsonConstructor]
         public UndoRecord() { }

         public void Dispose() {
            if (ObjectGuid == Guid.Empty) return;
            Debug.Assert(Database.Instance.NamedElements.ContainsKey(ObjectGuid) && Database.Instance.NamedElements[ObjectGuid] is CDL2Object,"UndoRecord.Dispose: Object not in NamedElements or not CDL2Object");
            if (Database.Instance.NamedElements.TryGetValue(ObjectGuid,out NamedElement? obj)) {
               // Given how the UndoRecord is constructed, this should always be true and will be a CDL2Object. See the Debug.Assert above.
               Database.Instance.NamedElements.Remove(ObjectGuid);
               if (obj is IDisposable disposable) {
                  disposable.Dispose();
                  GC.SuppressFinalize(this);
               }
            }
         }
      }

      /// <summary>
      /// The next push to the undo stack will swap the top two elements.
      /// </summary>
      public void RecordUndoSetSwap() => UndoStack.Swap = true;

      /// <summary>
      /// Create an undo record for the given element. Used for InterfaceChanged, Added, and Removed change types.
      /// </summary>
      /// <param name="element"></param>
      public void RecordUndo(CDL2Object element,ChangeType changeType) => UndoStack.Push(new UndoRecord(element,changeType));

      public void RecordUndo(CDL2Object element,NamedElement? context,ChangeType changeType) {
         if (context is not null) UndoStack.Push(new UndoRecord(element,context,changeType));
      }

      /// <summary>
      /// Create an undo record for a rename operation. Renames are performed on IDs so have to be recorded like that.
      /// </summary>
      /// <param name="id"></param>
      /// <param name="originalName"></param>
      /// <param name="newName"></param>
      public void RecordUndo(ID id,string originalName,string newName,bool updateReferences,ChangeType changeType) 
         => UndoStack.Push(new UndoRecord(id,originalName,newName,updateReferences: updateReferences,changeType: changeType));

      /// <summary>
      /// Records an undo operation for the specified element, indicating it has been replaced.
      /// </summary>
      /// <remarks>This method pushes an undo record onto the undo stack, allowing the operation to be
      /// reversed if needed.</remarks>
      /// <param name="replacedGuid">The object for which the undo operation is being recorded. Cannot be null.</param>
      /// <param name="replacementGuid">The unique identifier of the replacement element. This is the now live guid.</param>
      public void RecordUndo(Guid replaced,Guid replacement,ChangeType changeType) => UndoStack.Push(new UndoRecord(replaced,replacement,changeType));

      /// <summary>
      /// Used to record adding or deleting an interface declaration or ludes from a section.
      /// </summary>
      /// <param name="section"></param>
      /// <param name="elementType"></param>
      /// <param name="id"></param>
      /// <param name="changeType"></param>
      public void RecordUndo(Container container,RW elementType,ID id,ChangeType changeType) {
         switch (elementType) {
            case RW.EXPORT:
            case RW.IMPORT:
            case RW.EXT:
            case RW.ABSTR:
            case RW.INV:
               UndoStack.Push(new UndoRecord(null,changeType) {
                  ObjectGuid = container.GUID,
                  InterfaceStatus = Container.InterfaceEnumByType[elementType],
                  Id = id,
               });
               break;
            case RW.PRELUDE:
            case RW.ROOT:
            case RW.POSTLUDE:
               UndoStack.Push(new UndoRecord(null,changeType) {
                  ObjectGuid = container.GUID,
                  LudeType = elementType,
                  Id = id,
                  Position = container.Ludes[elementType].IndexOf(id),
               });
               break;
            default:
               throw new ArgumentOutOfRangeException(nameof(elementType),elementType,null);
         }
      }
      /// <summary>
      /// Used for recording changes to ludes.
      /// </summary>
      /// <param name="container"></param>
      /// <param name="elementType"></param>
      /// <param name="ludeId"></param>
      /// <param name="ludeGuid"></param>
      /// <param name="changeType"></param>
      public void RecordUndo(Container container,RW elementType,ID ludeId,Guid ludeGuid,ChangeType changeType) => UndoStack.Push(new UndoRecord(null,changeType) {
         ObjectGuid = container.GUID,
         LudeType = elementType,
         LudeProcGuid = ludeGuid,
         Id = ludeId,
      });

      /// <summary>
      /// Records an undo operation for a relative move of the specified element.
      /// </summary>
      /// <param name="element">The element for which the undo operation is being recorded. Cannot be null.</param>
      /// <param name="position">The position within the element where the change occurred.</param>
      /// <param name="changeType">The type of change that was made to the element.</param>
      public void RecordUndo(NamedElement element,int position,ChangeType changeType) => UndoStack.Push(new UndoRecord(element,position,changeType));



      /// <summary>
      /// When registration is suspended, named elements are still added to the NamedElements dictionary,
      /// but are removed when suspension is turned off.
      /// This implementation allows for nested suspensions, as long as the Resume call is made the same number of times.
      /// </summary>
      [JsonIgnore] private bool _suspendNamedElementRegistration = false;
      [JsonIgnore] private readonly List<Guid> _suspendedNamedElementRegistrationList = [];
      [JsonIgnore] private uint _suspendNamedElementRegistrationCount = 0;
      public void SuspendNamedElementRegistration() {
         if (_suspendNamedElementRegistrationCount++ == 0) _suspendedNamedElementRegistrationList.Clear();
         _suspendNamedElementRegistration = true;
      }
      public void ResumeNamedElementRegistration() {
         if (_suspendNamedElementRegistrationCount == 0) {
            throw new InvalidOperationException("Cannot resume named element registration when it is not suspended.");
         }
         if (--_suspendNamedElementRegistrationCount == 0) {
            foreach (Guid guid in _suspendedNamedElementRegistrationList) NamedElements.Remove(guid);
            _suspendedNamedElementRegistrationList.Clear();
         }
         _suspendNamedElementRegistration = false;
      }

      public static void WithSuspendedNamedElementRegistration(Action action) {
         Instance.SuspendNamedElementRegistration();
         try {
            action();
         } finally {
            Instance.ResumeNamedElementRegistration();
         }
      }
      public static T WithSuspendedNamedElementRegistration<T>(Func<T> func) {
         Instance.SuspendNamedElementRegistration();
         try {
            return func();
         } finally {
            Instance.ResumeNamedElementRegistration();
         }
      }
      public static void WithSuspendedNamedElementRegistration(bool iftrue,Action action) {
         if (iftrue) {
            WithSuspendedNamedElementRegistration(action);
         } else {
            action();
         }
      }
      public static T WithSuspendedNamedElementRegistration<T>(bool iftrue,Func<T> func)
         => iftrue ? WithSuspendedNamedElementRegistration(func) : func();

      /// <summary>
      /// Add a named element to the database.
      /// If registration is suspenede, add the guid to the suspend list. 
      /// These will be removed when registration is resumed.
      /// </summary>
      /// <param name="element"></param>
      public void AddNamedElement(NamedElement element,bool record) {
         if (record || element is not IUnrecordedElement) {
            NamedElements[element.GUID] = element;
            if (_suspendNamedElementRegistration) _suspendedNamedElementRegistrationList.Add(element.GUID);
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
      //public void RemoveElement(NamedElement element) {
      //   RecordUndo(element);
      //   NamedElements.Remove(element.GUID);
      //   if (element is Program) {
      //      Programs.Remove(element.GUID);
      //   } else if (element is Module) {
      //      Modules.Remove(element.GUID);
      //   }
      //}
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
         elements = objects.OfType<T>().Where(e => e.MatchesNamePattern(name));
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

      public Program? ProgramByName(string programName) => NamedElements.Values.OfType<Program>().FirstOrDefault(p => p.Id.Matches(programName));
      public Program? ProgramByName(ID ProgramId) => NamedElements.Values.OfType<Program>().FirstOrDefault(p => p.Id == ProgramId);
      public IEnumerable<Module> ModulesByName(string moduleName) => NamedElements.Values.OfType<Module>().Where(m => m.Id.Matches(moduleName));
      public Module? ModuleByName(string moduleName) => ModulesByName(moduleName).FirstOrDefault();
      public Module? ModuleByName(ID moduleId) => NamedElements.Values.OfType<Module>().FirstOrDefault(m => m.Id == moduleId);

      // Some convinience method for use in the debugger.
      public IEnumerable<Section> SectionsByName(string section,string module="*") => ModulesByName(module).SelectMany(m => m.Sections).Where(s => s.Id.Matches(section));
      public Section? SectionByName(string section,string module="*") => SectionsByName(section,module).FirstOrDefault();
      public IEnumerable<Guid> SiblingsByName(string obj,string section = "*",string module = "*")
         => (SectionsByName(section,module).FirstOrDefault()?.ChildElements().FirstOrDefault(e=>e.Id.Matches(obj)) as CDL2Object)?.Siblings ?? [];
      public static IEnumerable<Section> Secs(string sec,string mod = "*") => Instance.SectionsByName(sec,mod);
      public static Section? Sec(string sec,string mod = "*") => Instance.SectionByName(sec,mod);


      public static string Save(string? filePath = null) {
         Instance.CurrentFocusGuid = Focus.Current.Object?.GUID ?? Guid.Empty;
         return Serializer.SaveDB(filePath);
      }
      public static void Load(string? filePath = null) {
         Serializer.LoadDB(filePath);
         Instance.ConfigureAutoSave(Settings.SettingValue<int>("AutosaveInterval"));
         Focus.SetFocus(Instance.CurrentFocusGuid);
      }
      public static void InitializeForTests() {
         Directory.SetCurrentDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,@"..\..\..\..\CDL2v1\CDL2"));
         Load();
         Debug.WriteLine($"Database loaded for thread {Environment.CurrentManagedThreadId} ({Thread.CurrentThread.Name})");
      }
   }
}
