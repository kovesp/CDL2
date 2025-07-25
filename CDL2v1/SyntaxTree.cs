// <auto-gen>
//=======================================================================
// <copyright file="SyntaxTree.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-02-10</creation-date>
// 
// <summary>
//   Contains the classes that correspond to the elements of a CDL2 program.
//   Notice that in order to support serialization/deserialization all linkages are maintained by GUIDs.
//   This is not neccessary when the the linkage is 1-1 between parent and child.
//   TODO: Replace Guids with generated unique indices?
// </summary>
// <remarks>
// Classes with /*abstract*/ shuld be abstract, but this is not supported by the serializer.
// </remarks>
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

// Ignore Spelling: Transput CDL abstr ext inv ludes lude lwb upb FQN

using Microsoft.Windows.Themes;

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows.Navigation;
using System.Xml.Linq;

using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CDL2v1 {
   // Marker interfaces to allow lists to be composed of permissible elements.
   public interface IElement { }
   public interface IDataElement { }
   public interface IInterfaceElement { }
   public interface IProvidable : IInterfaceElement {
      ID Id { get; }
      Section? Section { get; }
      bool IsImported { get; }
   }
   public interface IImportable : IProvidable { }
   public interface IExportable : IProvidable { }

   public interface IActualArg {
      ID Id { get; }
   }
   public interface IParameter { // Afixxes and locals
      Algorithm? ContainingAlgorithm { get; set; }
   }

   /// <summary>
   /// Represents a failure protected objects: output and transput affixes and variables. This means that if used in an algorithm that fails,
   /// any changes to the object is undone.
   /// </summary>
   /// <param name="id"></param>
   public interface IFailureProtected : IActualArg { }

   /// <summary>
   /// Used to mark objects that are tracked in flow analysis. Affixes, Locals and Vars.
   /// </summary>
   public interface ITrackedVar { }

   public interface IUnrecordedElement { }

   /// <summary>
   /// Base class for all elements that have names in the syntax tree.
   /// To support serialization all references to NamedElements are by GUID through <see cref="Database.Instance.NamedElements"/>.
   /// </summary>
   [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
   [JsonDerivedType(typeof(Container), "Container")]
   [JsonDerivedType(typeof(Program), "Program")]
   [JsonDerivedType(typeof(Module), "Module")]
   [JsonDerivedType(typeof(Layer), "Layer")]
   [JsonDerivedType(typeof(Section), "Section")]
   [JsonDerivedType(typeof(CDL2Object), "CDL2Object")]
   [JsonDerivedType(typeof(Algorithm), "Algorithm")]
   [JsonDerivedType(typeof(ImportedAlgorithm), "ImportedAlgorithm")]
   [JsonDerivedType(typeof(Macro), "Macro")]
   [JsonDerivedType(typeof(Procedure), "Procedure")]
   [JsonDerivedType(typeof(Group), "Group")]
   [JsonDerivedType(typeof(LIST), "LIST")]
   [JsonDerivedType(typeof(Var), "Var")]
   [JsonDerivedType(typeof(Const), "Const")]
   [JsonDerivedType(typeof(ImportedConst), "ImportedConst")]
   [JsonDerivedType(typeof(Affix), "Affix")]
   [JsonDerivedType(typeof(Local), "Local")]
   [JsonDerivedType(typeof(Undeclared), "Undeclared")]
   public /*abstract*/ class NamedElement /*: SerializationBase */ {
      [JsonInclude][JsonPropertyOrder(1)] public Guid  GUID { get; set; }
      [JsonInclude][JsonPropertyOrder(2)] public ID Id { get; set; }
      /// <summary>
      /// True if the object is synthetic, i.e., generated by the parser.
      /// Objects that can be synthetic:
      ///  - Procedures: generated for Section ludes.
      ///  - Groups: indicating that their label is generated.
      /// </summary>
      [JsonInclude][JsonPropertyOrder(3)] public bool IsSynthetic { get; set; }
      /// <summary>
      /// A reference to the Container which contains this object.
      /// The following have a  Parent:
      ///   Layer     -> Module
      ///   Section   -> Layer
      ///   Algorithm -> Section (Macro, Procedure, ImportedAlgorithm)
      ///   LIST      -> Section
      ///   Var       -> Section
      ///   Const     -> Section (ImportedConst)
      ///   
      /// The Parent is Guid.Empty for:
      ///   Program
      ///   Module
      ///   Group
      ///   Affix
      ///   Local
      /// </summary>
      [JsonInclude][JsonPropertyOrder(4)] public Guid Parent { get; set; } = Guid.Empty;
      [JsonInclude][JsonPropertyOrder(5)] public string? Comments { get; set; }
      [JsonInclude][JsonPropertyOrder(6)] public Notes Notes { get; set; } = [];

      [JsonIgnore] public virtual bool IsImported => false;

      /// <summary>
      /// Create a new NamedElement with the given ID.
      /// The Guid is generated and the element is added to the database.
      /// </summary>
      /// <param name="id"></param>
      /// <param name="synthetic"></param>
      public NamedElement(ID id,bool synthetic = false, SelectorType focusType = SelectorType.INVALID) {
         Id = id;
         IsSynthetic = synthetic;
         GUID = Guid.NewGuid();
         Database.Instance.AddNamedElement(this); // Register the element in the database.
         FocusType = focusType;
      }
      /// <summary>
      /// Use when deserializing the element.
      /// </summary>
      [JsonConstructor]
      public NamedElement() => Id = ID.AnonID;

      /// <summary>
      /// Return the ancestor of the required type which must be a container.
      /// Thus this does not work for Groups, or Alternatives.
      /// </summary>
      /// <typeparam name="T">Module,Layer,Section. Not useful for Program.</typeparam>
      /// <returns></returns>
      public T? AncestorContainer<T>() where T : Container {
         if (Parent == Guid.Empty) {
            return null!;  // The element has no parent
         } else if (this is T self) {
            return null;   // The element is of the given container type
         } else if (Database.Instance.NamedElements.TryGetValue(Parent, out NamedElement? parent)) {
            if (parent is T container) return container;
            return parent!.AncestorContainer<T>();
         } else {
            return null;
         }
      }
      /// <summary>
      /// The Parent as an element.
      /// </summary>
      /// <returns></returns>
        public T? ParentElement<T>() where T : NamedElement => Parent != Guid.Empty && Database.Instance.NamedElements.TryGetValue(Parent,out NamedElement? parent) && parent is T element ? element : default;

      public virtual IEnumerable<NamedElement> ChildElements() => [];

      /// <summary>
      /// Retrieves all descendant elements of the current element that are of type <see cref="NamedElement"/>.
      /// </summary>
      /// <remarks>This method performs a recursive search to find all descendant elements of the specified
      /// type. The search includes all levels of the hierarchy below the current element.</remarks>
      /// <returns>An <see cref="IEnumerable{T}"/> containing all descendant elements of type <see cref="NamedElement"/>. If no
      /// such elements exist, an empty collection is returned.</returns>
      public IEnumerable<NamedElement> DescendantElements() => DescendantElements<NamedElement>();

      /// <summary>
      /// Retrieves all descendant elements of the current element that are of the specified type in a depth-first traversal order.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <returns></returns>
      public IEnumerable<T> DescendantElements<T>() where T : NamedElement {
         foreach (NamedElement child in ChildElements()) {
            if (child is T typedChild) yield return typedChild;
            foreach (T descendant in child.DescendantElements<T>()) {
               yield return descendant;
            }
         }
      }
      /// <summary>
      /// Retrieves all descendant elements of the specified type from the current element and its child elements.
      /// </summary>
      /// <remarks>This method performs a recursive traversal of the element hierarchy, starting from the
      /// current element's children. It yields elements that match the specified type, including those nested at any
      /// depth.</remarks>
      /// <param name="type">The <see cref="Type"/> to filter the descendant elements. Only elements that are instances of this type will
      /// be included.</param>
      /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="NamedElement"/> objects that are of the specified type. The
      /// sequence includes matching elements from both the immediate children and deeper descendants.</returns>
      public IEnumerable<NamedElement> DescendantElements(Type type) {
         foreach (NamedElement child in ChildElements()) {
            if (type.IsInstanceOfType(child)) yield return child;
            foreach (NamedElement descendant in child.DescendantElements(type)) {
               yield return descendant;
            }
         }
      }

      [JsonIgnore]
      public SelectorType FocusType = SelectorType.INVALID;
      /// <summary>
      /// The section which contains this object or null
      /// Valid (non-null) only for Section and objects that are contained in a section. <see cref="Parent"/>.
      /// </summary>
      [JsonIgnore]
      public Section? Section => AncestorContainer<Section>();
      /// <summary>
      /// The layer which contains this object or null.
      /// </summary>
      [JsonIgnore]
      public Layer? Layer => AncestorContainer<Layer>();
      /// <summary>
      /// The module that contains this object or null.
      /// </summary>
      [JsonIgnore]
      public Module? Module => AncestorContainer<Module>();

      /// <summary>
      /// Contains the objects that reference this object.
      /// What may be here depends on the type of this object.
      ///  - Const: Algorithms and Consts.
      ///  - Vars:  Algorithms.
      ///  - LISTs: Macros.
      ///  - Algorithms: Algorithms.
      ///  TODO: Not currently used.
      /// </summary>
      [JsonIgnore]
      public Set<CDL2Object> References { get; } = [];

      override public string ToString() => $"{TypeShortName} {Id.Name}";
      [JsonIgnore]
      public virtual string TypeShortName => this.GetType().Name.ToUpper()[..3];

      [JsonIgnore]
      public bool HasCommentOrNote => Comments != null || Notes.Count > 0;
      public void AddNote(string phase, Note note, params object[] insertions) {
         Notes.Add(new Note(note, phase, this, insertions));
         Database.Instance.ElementsWithNotes.Add(GUID);
      }
      public void AddNotes(string phase, Notes? notes) => notes?.ForEach(note => AddNote(phase, note));

      /// <summary>
      /// Fully qualified name as Module_Layer_Section_Object.
      /// Separator can be specified. Default is "_".
      /// </summary>
      /// <param name="separator"></param>
      /// <returns></returns>
      public string FQN(string separator = "_",string prefix = "",string replacement = "",bool camelCase = false,bool literalObjectName = false) {
         string sectionName = AncestorContainer<Section>()!.Id.Name.AsIdentifier(prefix,replacement,camelCase);
         string layerName   = AncestorContainer<Layer>()!.Id.Name.AsIdentifier(prefix,replacement,camelCase);
         string moduleName  = AncestorContainer<Module>()!.Id.Name.AsIdentifier(prefix,replacement,camelCase);
         string objectName  = Id.Name.AsIdentifier(prefix,replacement,camelCase,literalObjectName);
         return $"{moduleName}{separator}{layerName}{separator}{sectionName}{(IsSynthetic?separator+separator:separator)}{objectName}";
      }
      /// <summary>
      /// Element display name, i.e. MOD mod LAY lay SEC sec declared.
      /// </summary>
      /// <returns></returns>
      public string FQDN() => $"{AncestorContainer<Module>().WithSpace()}{AncestorContainer<Layer>().WithSpace()}{AncestorContainer<Section>().WithSpace()}{ToString()}";

      public static T? From<T>(Guid guid) where T: NamedElement => Database.Instance.NamedElements.TryGetValue(guid, out NamedElement? element) && element is T typedElement ? typedElement : null;
      public static bool From<T>(Guid guid, out T? element) where T : NamedElement => (element = NamedElement.From<T>(guid)) is not null;
      /// <summary>
      /// Return true if any of the objects is an ancestor of this NamedElement
      /// </summary>
      /// <param name="objects"></param>
      /// <returns></returns>
      internal bool HasAncestorAmong(IEnumerable<NamedElement> objects) {
         foreach (NamedElement obj in objects) {
            if (Parent == obj.GUID) return true; // The parent is one of the objects.
            if (ParentElement<NamedElement>()?.HasAncestorAmong(objects) ?? false) return true; // The parent is not one of the objects, but it may have an ancestor that is.
         }
         return false; // No ancestor is among the objects.
      }
   }

   /// <summary>
   /// Base class for all elements that can contain other elements, i.e., the program and modules, layers, sections.
   /// </summary>
   public /*abstract*/ class Container : NamedElement {
      [JsonConstructor]
      public Container() : base() { }
      /// <summary>
      /// The Container children of the container. Layers are ordered, hence the list.
      /// </summary>
      [JsonInclude] [JsonPropertyOrder(10)] public List<Guid> Children { get; set; } = [];
      public override IEnumerable<NamedElement> ChildElements() => Children.Select(guid=>Database.Instance.NamedElements[guid]);

      /// <param Id="Id"></param>
      public Container(ID id,string? comments,Notes? notes, SelectorType FocusType = SelectorType.INVALID) : base(id,focusType:FocusType) {
         Comments = comments;
         AddNotes("Parser", notes);
      }

      public Container(ID id,Container? parent,string? comments = null,Notes? notes = null, SelectorType FocusType = SelectorType.INVALID) : this(id,comments,notes,FocusType) { 
         Parent = parent?.GUID ?? Guid.Empty;
         if (parent != null && parent.Children.Contains(GUID)) {
            Logger.ReportError($"{ContainerName} is already a child of {parent.ContainerName}");
         } else {
            parent?.Children.Add(GUID);
         }
      }

      // The Ludes are stored in a dictionary with the reserved word as the key. The values are lists of IDs.
      // Section Ludes will be generated as Procedures and given the Id of the lude type (which are not legal as a CDL2 Id).
      [JsonInclude][JsonPropertyOrder(11)]
      public Dictionary<RW,List<ID>> Ludes { get; set; } = new() {
         { RW.PRELUDE,[] },
         { RW.ROOT,[] },
         { RW.POSTLUDE,[] }
      };
      public static readonly List<RW> LudeTypes = [RW.PRELUDE, RW.ROOT, RW.POSTLUDE];

      /// <summary>
      /// Sets the LudeParser action for the container. The default is to do nothing.
      /// </summary>
      [JsonIgnore]
      public Action<Parser,RW,Container> LudeParser = (parser,ludeType,container) => { };

      /// <summary>
      /// The short Id of the container with its type. Used in the ToString method.
      /// </summary>
      [JsonIgnore]
      public string ContainerName => $"{ParentElement<Container>()?.ContainerName ?? ""} {TypeShortName} {Id.Name}".Trim();
   }

   /// <summary>
   /// Represents a program in the syntax tree.
   /// </summary>
   public class Program : Container {
      [JsonIgnore]
      override public string TypeShortName => "PROG";
      /// <summary>
      /// Get the modules that have the given lude type.
      /// </summary>
      /// <param Id="ludeType"></param>
      /// <returns>A collection of modules that are in the lude of the given type.</returns>
      public List<Module> Lude(RW ludeType) => [.. Ludes[ludeType].Select(id => Database.Instance.ModuleByName(id))!];

      [JsonInclude][JsonPropertyOrder(20)]
      public IDSet Parts = [];
      /// <summary>
      /// Gets the collection of modules associated with the current program.
      /// </summary>
      /// <remarks>Note that here and elsewhere the iteration must be fixed to avoid multiple calls interferring with each other.</remarks>
      [JsonIgnore] public List<Module> Modules => [.. Database.Instance.NamedElements.Values.OfType<Module>().Where(mod=>Parts.Contains(mod.Id)) ];
      public override IEnumerable<NamedElement> ChildElements() => Modules;

      /// <summary>
      /// Maps all identifiers exported by the modules in the program to the exporting module.
      /// </summary>
      [JsonIgnore]
      public readonly IDDictionary<IExportable> Exports = [];
      /// <summary>
      /// Program Ludes are a list of module IDs.
      /// </summary>
      /// <param Id="Id"></param>
      public Program(ID id, string? comments, Notes notes) : base(id, null, comments, notes,SelectorType.PROGRAM) => LudeParser = Parser.ParseLudeOfIDs;
      [JsonConstructor]
      public Program() { LudeParser = Parser.ParseLudeOfIDs; FocusType = SelectorType.PROGRAM; }
   }

   /// <summary>
   /// Represents a module in the syntax tree.
   /// </summary>
   /// <param Id="Id"></param>
   public class Module : Container {
      [JsonIgnore]
      public readonly IDDictionary<IImportable> imports = [];        // Imports are specified in sections, but are propagated up the module level.
      [JsonIgnore]
      public readonly IDDictionary<IProvidable> exports = [];        // Exports are specified in sections, but are propagated up the module level.
      /// <summary>
      /// Resolved imports are the imports that have been resolved to their definitions by the semantic analyzer.
      /// Reconstiotuted each time the semantic analyzer is run.
      /// </summary>
      [JsonIgnore]
      public readonly IDDictionary<IImportable> resolvedImports = [];

      /// <summary>
      /// Module Ludes are a list of container IDs.
      /// </summary>
      /// <param Id="Id"></param>
      public Module(ID id,string? comments,Notes notes) : base(id,null,comments,notes,SelectorType.MODULE) {
         LudeParser = Parser.ParseLudeOfIDs;
         Comments = comments;
      }
      [JsonConstructor]
      public Module() {
         LudeParser = Parser.ParseLudeOfIDs;
         FocusType = SelectorType.MODULE;
      }

      public Section? SectionById(ID id) {
         foreach (Section section in Sections) if (section.Id == id) return section;
         return null;
      }
      public bool TryGetSectionById(ID id, out Section? section) {
         section = SectionById(id);
         return section != null;
      }

      [JsonIgnore] public List<Layer> Layers => [.. Children.Select(GUID => Database.Instance.NamedElements[GUID] as Layer)!];
      [JsonIgnore] public List<Section> Sections => [..Layers.SelectMany(layer => layer.Children.Select(GUID => Database.Instance.NamedElements[GUID] as Section))!];
   }

   /// <summary>
   /// Represents a layer in the syntax tree.
   /// Notice that layers don'localObject have Ludes.
   /// </summary>
   public class Layer : Container {
      /// <summary>
      /// The ancestor of a layer is the previous layer in the layer list of the containing module.
      /// </summary>
      [JsonInclude][JsonPropertyOrder(30)]
      public Guid AncestorGUID = Guid.Empty;

      /// <param Id="Id"></param>
      /// <param Id="module"></param>
      /// <param PhaseName="ancestor">The layer from which this layer is extended. Null for the lowest layer.</param>
      public Layer(ID id, Module module, Layer? ancestor, string? comments = null, Notes? notes = null) 
         : base(id, module, comments, notes,SelectorType.LAYER) => AncestorGUID = ancestor?.GUID ?? Guid.Empty;
      [JsonConstructor]
      public Layer() : base() => FocusType = SelectorType.LAYER;  // For deserialization

      [JsonIgnore]
      public Layer? Ancestor => AncestorGUID != Guid.Empty && NamedElement.From(AncestorGUID, out NamedElement? ancestor) && ancestor is Layer layer ? layer : null;
      [JsonIgnore]
      public Layer? Successor => Module?.Layers.FirstOrDefault(layer => layer.AncestorGUID == GUID);

      /// <summary>
      /// The visible objects in this layer, i.e, the Consts and Algorithms extended in the sections of this layer and /*abstract*/ed in the sections of the ancestor.
      /// </summary>
      [JsonIgnore]
      public IDDictionary<IProvidable> Visible { get; } = [];

      [JsonIgnore]
      public List<Section> Sections => [..Children.Select(GUID => Database.Instance.NamedElements[GUID] as Section)!];
   }

   /// <summary>
   /// Represents a container in the syntax tree.
   /// </summary>
   /// <param Id="Id"></param>
   /// <param Id="layer"></param>
   public class Section : Container {
      /// <summary>
      /// The interfaces.
      /// </summary>
      [JsonInclude][JsonPropertyOrder(40)] public  Set<ID> ext = [];
      [JsonInclude][JsonPropertyOrder(41)] public  Set<ID> abstr = [];
      [JsonInclude][JsonPropertyOrder(42)] public  Set<ID> inv = [];
      [JsonInclude][JsonPropertyOrder(43)] public  Set<ID> export = [];
      [JsonInclude][JsonPropertyOrder(44)] public  Set<ID> import = [];

      public class DeclarationDictionary : IDDictionary<Guid> {
         public IEnumerable<T> AsCDL2Objects<T>() where T : NamedElement => [.. Values.Select(From<T>).OfType<T>() ];
         public IEnumerable<T> AsCDL2Objects<T>(Func<T,bool> pred) where T : NamedElement => [.. Values.Select(From<T>).OfType<T>().Where(pred) ];

         public bool TryGetValue<T>(ID id, out T? value) where T : CDL2Object {
            if (base.TryGetValue(id, out Guid guid) && NamedElement.From<T>(guid,out T? elem)) {
               value = elem;
               return true;
            }
            value = null;
            return false;
         }

         public override string ToString() => $"Declarations: {Count}";
      }

      /// <summary>
      /// Holds the Declarations of the SectionById. The key is the ID of the declaration.
      /// </summary>
      [JsonInclude][JsonPropertyOrder(45)] public DeclarationDictionary Declarations = [];

      public override IEnumerable<NamedElement> ChildElements() => Declarations.Values.Select(guid=>Database.Instance.NamedElements[guid]);

      [JsonIgnore] public List<Const> Constants => [.. Declarations.AsCDL2Objects<Const>()];
      [JsonIgnore] public List<Const> ImportedConstants => [.. Declarations.AsCDL2Objects<ImportedConst>()];
      [JsonIgnore] public List<Var> Variables => [.. Declarations.AsCDL2Objects<Var>()];
      [JsonIgnore] public List<LIST> Lists => [.. Declarations.AsCDL2Objects<LIST>() ];
      [JsonIgnore] public List<Macro> Macros => [.. Declarations.AsCDL2Objects<Macro>() ];
      [JsonIgnore] public List<Procedure> Procedures => [.. Declarations.AsCDL2Objects<Procedure>() ];
      [JsonIgnore] public List<Algorithm> Algorithms => [.. Declarations.AsCDL2Objects<Algorithm>() ];
      [JsonIgnore] public List<Algorithm> ImportedAlgorithms => [.. Declarations.AsCDL2Objects<ImportedAlgorithm>()];
      [JsonIgnore] public List<Algorithm> NonSyntheticAlgorithms => [.. Declarations.AsCDL2Objects<Algorithm>(alg=>!alg.IsSynthetic) ];
      [JsonIgnore] public List<Procedure> NonSyntheticProcedures => [.. Declarations.AsCDL2Objects<Procedure>(proc => !proc.IsSynthetic) ];
      [JsonIgnore] public List<Procedure> SyntheticProcedures   => [.. Declarations.AsCDL2Objects<Procedure>(proc => proc.IsSynthetic) ];

      /// <summary>
      /// Get the object with the given ID. If the object is not found in this Section, then if it is invoked it is looked for in the layer.
      /// If found in the layer
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="Id"></param>
      /// <param name="resolvedObject"></param>
      /// <returns></returns>
      public bool TryGetResolvedObject<T>(ID Id, out T? resolvedObject) where T : CDL2Object {
         if (Declarations.TryGetValue(Id, out CDL2Object? declared) && declared is T localObject) {
            resolvedObject = localObject;
         } else if (inv.Contains(Id) && Layer!.Visible.TryGetValue(Id, out IProvidable? visible) && visible is T visibleObject) {
            resolvedObject = visibleObject;
         } else {
            resolvedObject = null;
         }
         if (resolvedObject is not null && resolvedObject.IsImported) {
            if (Module!.resolvedImports.TryGetValue(Id, out IImportable? imported) && imported is T importedObject) {
               resolvedObject = importedObject;
            } else {
               resolvedObject = null;
            }
         }
         return resolvedObject != null;
      }
      public CDL2Object? GetResolvedObject(ID Id) {
         CDL2Object? resolvedObject = null;
         if (Declarations.TryGetValue(Id, out CDL2Object? localObject)) {
            resolvedObject = localObject;
         } else if (inv.Contains(Id) && Layer!.Visible.TryGetValue(Id, out IProvidable? visibleObject)) {
            resolvedObject = (CDL2Object?)visibleObject;
         }
         if (resolvedObject?.IsImported == true) {
            if (Module!.resolvedImports.TryGetValue(Id, out IImportable? importedObject)) {
               resolvedObject = (CDL2Object)importedObject;
            } else {
               resolvedObject = null;
            }
         }
         return resolvedObject;
      }
      /// <summary>
      /// Return the actual constant if c is imported, otherwise c itself
      /// </summary>
      /// <param name="c"></param>
      /// <returns></returns>
      public Const? GetResolvedConstant(Const c) => GetResolvedObject(c.Id) as Const; 

      /// <summary>
      /// Sections have Ludes each of which contains the ID of generated FUNCTION or ACTION which consist of a single alternative.
      /// </summary>
      /// <param Id="Id"></param>
      /// <param Id="layer"></param>
      public Section(ID id,Layer layer,string? comments = null,Notes? notes = null) : base(id,layer,comments,notes,SelectorType.SECTION) 
         => LudeParser = Parser.ParseLudeOfCalls;
      [JsonConstructor]
      public Section() { LudeParser = Parser.ParseLudeOfCalls; FocusType = SelectorType.SECTION; }

      public static Type[] ProvidedElementImplementors;
      static Section() => ProvidedElementImplementors = [.. Extensions.GetImplementorsOfInterface<IProvidable>()];

      /// <summary>
      /// Get the declaration with the given ID. If the declaration is not found in this Section, it is looked for in the layer.
      /// Note: this object may be importable, needs to be checked later.
      /// </summary>
      /// <param name="id"></param>
      /// <typeparam name="T">The type of the requested object which must be an ICDL2Object.</typeparam>
      /// <returns>The declaration if found. </returns>
      /// 
      public bool TryGetDeclaration<T>(ID id,out T? declaration) where T : CDL2Object {
         if (TryGetLocalDeclaration(id, out T? local)) {
            declaration = local;
         } else if (Layer!.Visible.TryGetValue(id, out IProvidable? visible) && visible is T visibleDeclaration) {
            declaration = visibleDeclaration;
         } else {
            // Neither declared nor invoked (not in Visible).
            declaration = default;
            return false;
         }
         Debug.Assert(declaration != null, $"Could not find declaration {id} in {this}");
         if (declaration.IsImported && CDL2.Compiler.CompilationPhase?.PhaseName == typeof(CodeGenerator).Name) {
            // This object is an import stub, but note that resolvedImports are only available in the Code Generator phase.
            declaration = Module!.resolvedImports[id] as T;
         }
         return true;
      }

      /// <summary>
      /// Get a CDL2 object that is declared in the current section.
      /// Note: this object may be importable, needs to be checked later.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="id"></param>
      /// <param name="declaration"></param>
      /// <returns></returns>
      public bool TryGetLocalDeclaration<T>(ID id,out T? declaration) where T : CDL2Object {
         if (Declarations.TryGetValue(id,out CDL2Object? obj) && obj is T local) {
            declaration = local;
            return true;
         }
         declaration = default;
         return false;
      }
   }

   // ---------------------------------------------------------------------------------------------------

   /// <summary>
   /// This is the base class of all CDL2 objects that can be declared.
   /// Algorithm (Macro, Porcedure, ImportedAlgorithm), Const (ImportedConst), Var and LIST.
   /// </summary>
   public /*abstract*/ class CDL2Object : NamedElement {
      public CDL2Object(ID id, Section section, string? comments, bool synthetic = false, SelectorType FocusType=SelectorType.INVALID) : base(id, synthetic,FocusType) {
         Parent = section.GUID;
         Comments = comments;
      }
      public CDL2Object(ID id, SelectorType FocusType = SelectorType.INVALID) : base(id,focusType:FocusType) { }

      [JsonConstructor]
      public CDL2Object() : base() { } // For deserialization

      [JsonIgnore]
      public SyntacticElement SE { get; protected set; }

      /// <summary>
      /// Given that objects have to be unique by name within a section and extended//*abstract*/ed objects within a layer, objects with the same Id are considered the same.
      /// </summary>
      /// <param name="obj"></param>
      /// <returns></returns>
      public override bool Equals(object? obj) => obj is CDL2Object @object && EqualityComparer<ID>.Default.Equals(Id, @object.Id);
      public override int GetHashCode() => HashCode.Combine(Id);

      public static bool operator ==(CDL2Object? left, CDL2Object? right) => EqualityComparer<CDL2Object>.Default.Equals(left, right);
      public static bool operator !=(CDL2Object? left, CDL2Object? right) => !(left == right);
   }

   /// <summary>
   /// Represents the common properties of Algorithms (Macros and Procedures).
   /// </summary>
   public /*abstract*/ class Algorithm : CDL2Object, IProvidable, IImportable, IExportable {
      [JsonInclude][JsonPropertyOrder(10)] public RW AlgorithmType;            // One of FUNCTION, ACTION, TEST or PREDICATE (reservedWordValue will never be null)
      [JsonInclude][JsonPropertyOrder(11)] public TT BodyType;                 // One of : or := (for CODE only) and = or =: (for MACRO only)
      [JsonInclude][JsonPropertyOrder(12)] public List<Guid> affixGuids = [];  // The affixes of this algorithm. A List because they are ordered.
      [JsonInclude][JsonPropertyOrder(13)] public Set<Guid> localGuids = [];   // The Dlocals of this algorithm.

      [JsonIgnore] public List<Affix> Affixes => [.. affixGuids.Select(guid => NamedElement.From<Affix>(guid))];
      [JsonIgnore] public Set<Local> Locals => [.. localGuids.Select(guid => NamedElement.From<Local>(guid))];

      public bool IsAlgorithmType(RW algorithmType) => AlgorithmType == algorithmType;
      public bool IsAction => IsAlgorithmType(RW.ACTION);
      public bool IsFunction => IsAlgorithmType(RW.FUNCTION);
      public bool IsTest => IsAlgorithmType(RW.TEST);
      public bool IsPredicate => IsAlgorithmType(RW.PREDICATE);


      public Algorithm(ID id,List<Affix> affixes,Set<Local> locals,Token algorithmType,TT bodyType,Section section,bool synthetic = false) 
            : base(id,section,algorithmType.Comments,synthetic) {
         affixGuids = [.. affixes.Select(affix=>affix.GUID)];
         localGuids = (Set<Guid>)locals.Select(local=>local.GUID).ToSet<Guid>();
         this.AlgorithmType = algorithmType.reservedWordValue ?? RW.FUNCTION;
         this.BodyType = bodyType;
         this.SE = SE.AlgorithmName;
         foreach (Affix affix in affixes) affix.ContainingAlgorithm = this;
         foreach (Local local in locals)  local.ContainingAlgorithm = this;
         FocusType = this is Procedure ? SelectorType.PROCEDURE : SelectorType.MACRO;
      }
      [JsonConstructor]
      public Algorithm() { }
      public bool HasLocal(ID id) => Locals.FirstOrDefault(aff => aff.Id == id) is not null;

      [JsonIgnore]
      public AlgorithmNameType NameType {
         get {
            AlgorithmNameType ait = AlgorithmNameType.None;
            if (AlgorithmType == RW.TEST || AlgorithmType == RW.PREDICATE) ait |= AlgorithmNameType.CanFail;
            if (AlgorithmType == RW.ACTION || AlgorithmType == RW.PREDICATE) ait |= AlgorithmNameType.HasEffect;
            if (BodyType == TT.MACROBODY || BodyType == TT.MACROPROCBODY) ait |= AlgorithmNameType.Macro;
            return ait;
         }
      }
      [JsonIgnore]
      public DecorationStyle NameStyle {
         get {
            AlgorithmNameType ait = NameType;
            DecorationStyle ds = DecorationStyle.Normal;
            if (ait.HasFlag(ANT.CanFail)) ds |= DecorationStyle.Italic;
            if (ait.HasFlag(ANT.Macro)) ds |= DecorationStyle.Underline;
            if (ait.HasFlag(ANT.HasEffect)) ds |= DecorationStyle.Bold;
            return ds;
         }
      }
      [JsonIgnore]
      public string AlgorithmName => $"{AlgorithmType} {Id}";

      [JsonIgnore] public bool CanFail => AlgorithmType == RW.TEST || AlgorithmType == RW.PREDICATE;
      [JsonIgnore] public bool AlwaysSucceeds => !CanFail;
      [JsonIgnore] public bool HasEffect => AlgorithmType == RW.PREDICATE || AlgorithmType == RW.ACTION;
      [JsonIgnore] public bool HasNoEffect => !HasEffect;
      [JsonIgnore] public bool NeedsFinalization => CanFail && (Affixes.Any(affix => affix.IsOutput) || GetReferencedVariables().Any());
      [JsonIgnore] public bool IsInlineMacro => BodyType == TT.MACROBODY;
      /// <summary>
      /// Check if this is a conditional compilation flag. That is, the body consists of a single fail respectively succeed operator.
      /// </summary>
      /// <param name="group"></param>
      /// <returns></returns>
      [JsonIgnore]
      public virtual bool IsConditionalCompilationOff => false;
      [JsonIgnore]
      public virtual bool IsConditionalCompilationOn => false;
      public bool IsConditionalCompilation(bool? on=null) => on is null ? IsConditionalCompilationOn || IsConditionalCompilationOff : (bool)on ? IsConditionalCompilationOn : IsConditionalCompilationOff;
      public bool TryGetAffix(ID id,out Affix affix) => (affix = Affixes.FirstOrDefault(affix => affix.Id == id,Affix.Default)) != Affix.Default;
      public bool TryGetLocal(ID id,out Local local) => (local = Locals.FirstOrDefault(local => local.Id == id,Local.Default)) != Local.Default;

      public override string ToString() {
         StringBuilder buffer = new();
         buffer.Append($"{TypeShortName} {Id.Name}");
         foreach (Affix affix in Affixes) {
            buffer.Append(Token.TokenType2Glyph[affix.IsString ? TT.STRINGAFFIXSEP : TT.AFFIXSEP]);
            buffer.Append(affix);
         }
         foreach (Local local in Locals) buffer.Append(local);
         return buffer.ToString();
      }

      /// <summary>
      /// Get the annotation symbols for the ID of this algorithm. Computed on first use.
      /// Note that the failure conditions should have been ruled out by the semantic analyzer.
      /// TODO: The above will be true for a full run, but not necessarily for a lab like environment
      /// </summary>
      //public SA NameAnnotation {
      //   get {
      //      SA getSA() {
      //         SectionById SectionById = Parent as SectionById;
      //         Debug.Assert(SectionById != null);

      //         if (SectionById.inv.Contains(Id)) { // More complicated then Declarations. Need to find the container the algorithm is /*abstract*/ed or extended from
      //                                         // Examine siblings to find the container the algorithm is extended from.
      //            Layer currentLayer = SectionById.Parent as Layer;
      //            Debug.Assert(currentLayer != null);
      //            foreach (SectionById sibling in currentLayer.Children.Where(sec => sec != SectionById).Cast<SectionById>()) {
      //               if (sibling.ext.Contains(Id)) {
      //                  if (sibling.import.Contains(Id)) return new SA(Prefix1: AS.Ext,Prefix2: AS.ImportExport);
      //                  return new SA(Prefix1: AS.Ext);
      //               }
      //            }
      //            // If still here, examine the layer below if any.
      //            List<Container> moduleLayers = currentLayer.Parent?.Children;
      //            Debug.Assert(moduleLayers != null);
      //            if (moduleLayers.Count > 1) {
      //               int currentLayerPosition = moduleLayers.IndexOf(currentLayer);
      //               if (currentLayerPosition > 0) {
      //                  Container layerBelow = moduleLayers[currentLayerPosition - 1];
      //                  foreach (SectionById ancestor in layerBelow.Children.Cast<SectionById>()) {
      //                     if (ancestor.abstr.Contains(Id)) {
      //                        if (ancestor.import.Contains(Id)) return new SA(Prefix1: AS.Abstr,Prefix2: AS.ImportExport);
      //                        return new SA(Prefix1: AS.Abstr);
      //                     }
      //                  }
      //               }
      //               return new SA(Prefix1: AS.Inv);   // Only possible in a partially analyzed context.
      //            } else { // Declarations
      //               bool exported = SectionById.export.Contains(Id);
      //               bool importable = SectionById.import.Contains(Id);
      //               bool abstr = SectionById.abstr.Contains(Id);
      //               bool ext = SectionById.ext.Contains(Id);
      //               if (importable) {
      //                  if (abstr && ext) return new SA(Prefix1: AS.ImportExport,Suffix1: AS.AbstrExt);
      //                  if (abstr) return new SA(Prefix1: AS.ImportExport,Suffix1: AS.Abstr);
      //                  if (ext) return new SA(Prefix1: AS.ImportExport,Suffix1: AS.Ext);
      //               } else if (exported) {
      //                  if (abstr && ext) return new SA(Suffix1: AS.ImportExport,Suffix2: AS.AbstrExt);
      //                  if (abstr) return new SA(Suffix1: AS.ImportExport,Suffix2: AS.Abstr);
      //                  if (ext) return new SA(Suffix1: AS.ImportExport,Suffix2: AS.Ext);
      //               } else {
      //                  if (abstr && ext) return new SA(Suffix1: AS.AbstrExt);
      //                  if (abstr) return new SA(Suffix1: AS.Abstr);
      //                  if (ext) return new SA(Suffix1: AS.Ext);
      //               }
      //            }
      //         }
      //         return new SA();  // Should be impossible to get here.
      //      }
      //      return sa ??= getSA();
      //   }
      //}
      //private SA? sa = null;
      /// <summary>
      /// Used to force the re-computation of the PhaseName annotations.
      /// TODO: figure out when to re-compute PhaseName annotations.
      /// </summary>
      //public void ResetNameAnnotations() => sa = null;
      public virtual IEnumerable<Var> GetReferencedVariables() => [];
      [JsonIgnore]
      override public string TypeShortName => $"{AlgorithmType}";
   }

   /// <summary>
   /// An importable algorithm is a reference to an algorithm in another module. Thus it has only a header and no body.
   /// </summary>
   public class ImportedAlgorithm : Algorithm, IImportable {
      public ImportedAlgorithm(ID id,List<Affix> affxies,Token algorithmType,Section section) : base(id,affxies,[],algorithmType,TT.NOBODY,section) {
      }

      [JsonConstructor]
      public ImportedAlgorithm() { } // For deserialization
      public override IEnumerable<Var> GetReferencedVariables() => [];
      public override string ToString() => "IMPORTED "+ base.ToString();
      public override bool IsImported => true;
   }

   /// <summary>
   /// Represents a macro in the syntax tree.
   /// </summary>
   public class Macro : Algorithm {
      [JsonInclude]
      [JsonPropertyOrder(60)]
      public List<IElement> elements = [];

      /// <param Id="Id"></param>
      /// <param Id="affixes"></param>
      /// <param Id="locals"></param>
      /// <param Id="algorithmType"></param>
      /// <param Id="bodyType"></param>
      /// <param Id="container"></param>
      public Macro(ID id,List<Affix> affixes,Set<Local> locals,Token algorithmType,TT bodyType,Section section) : base(id,affixes,locals,algorithmType,bodyType,section) { }
      [JsonConstructor]
      public Macro() : base() { } // For deserialization

      public override IEnumerable<Var> GetReferencedVariables() => elements.OfType<ID>().Select(id=>Section?.GetResolvedObject(id)).OfType<Var>().Distinct();
   }
   /// <summary>
   /// Represents a procedure in the syntax tree.
   /// </summary>
   public class Procedure : Algorithm {
      [JsonInclude][JsonPropertyOrder(20)]
      public Group group = new();
      public override IEnumerable<NamedElement> ChildElements() => [group];

      /// <summary>
      /// True if the procedure is an Action or Function that has only a single alternative (which is a sequence of calls none of which can fail ... which will be guarenteed by the sematic analyzer)
      /// </summary>
      [JsonIgnore] public bool IsVerySimple => AlwaysSucceeds && group.Alternatives.Count == 1 && HasNoGroups;
      /// <summary>
      /// Can have alternatives, but there are mo groups except for the primary one.
      /// It can also fail.
      /// </summary>
      [JsonIgnore] public bool IsSimple => HasNoGroups && HasNoRepeat;
      [JsonIgnore] public PBT ProcedureBodyType => IsVerySimple ? PBT.VerySimple : IsSimple ? PBT.Simple : PBT.General;

      /// <summary>
      /// Check if this is a conditional compilation flag. That is, the body consists of a single fail respectively succeed operator.
      /// TODO: This is the intial version. It will be refined to check that all calls in a procedure are to other procedures that are also conditional compilation flags.
      /// </summary>
      /// <returns></returns>
      [JsonIgnore] public override bool IsConditionalCompilationOff => CanFail && group.Alternatives.Count == 1 && group.Alternatives[0].calls.Count == 0 && group.Alternatives[0].lastCall.type == LCT.Fail;
      [JsonIgnore] public override bool IsConditionalCompilationOn => CanFail && group.Alternatives.Count == 1 && group.Alternatives[0].calls.Count == 0 && group.Alternatives[0].lastCall.type == LCT.Succeed;

      /// <summary>
      /// The procedure has repeats.
      /// </summary>
      [JsonIgnore] public bool HasRepeat => group.HasAnAnonymousRepeat();
      [JsonIgnore] public bool HasNoRepeat => ! HasRepeat;

      [JsonIgnore] public bool NeedsWrapper => repeatsProcedure || NeedsFinalization || HasRepeat;

      /// <summary>
      /// None of the alternatives in the primary group ends with a group.
      /// </summary>
      [JsonIgnore] public bool HasNoGroups {
         get {
            foreach (Alternative alternative in group.Alternatives) {
               if (alternative.lastCall.type == LCT.Group) return false;
            }
            return true;
         }
      }

      /// <summary>
      /// The parser will set this if a repeat operator reference the procedure itself.
      /// </summary>
      [JsonInclude] public bool repeatsProcedure = false;
      /// <param Id="Id"></param>
      /// <param Id="affixes"></param>
      /// <param Id="locals"></param>
      /// <param Id="algorithmType"></param>
      /// <param Id="bodyType"></param>
      /// <param Id="SectionById"></param>
      public Procedure(ID id,List<Affix> affxies,Set<Local> locals,Token algorithmType,TT bodyType,Section section,bool synthetic = false) : base(id,affxies,locals,algorithmType,bodyType,section,synthetic) {
         group.Parent = GUID;
         group.Id = id; // The group has the same ID as the procedure.
      }
      [JsonConstructor]
      public Procedure() { }

      public Procedure(RW ludeType,Section section) : this(ID.From(ludeType),[],[],Token.ACTIONToken,TT.PROCBODY,section,true) { } // Used for container Ludes which are parameterless actions with no locals.
      public override IEnumerable<Var> GetReferencedVariables() {
         Set<Var> variables = [];
         CollectReferencedVariables(group,variables);
         return variables;
      }
      private static void CollectReferencedVariables(Group group,Set<Var> variables) {
         foreach (Alternative alternative in group.Alternatives) {
            foreach (Call call in alternative.calls) foreach (Var variable in call.Args.OfType<Var>()) variables.Add(variable);
            if (alternative.lastCall.type == LCT.Standard) {
               foreach (Var variable in alternative.lastCall.call!.Args.OfType<Var>()) variables.Add(variable);
            } else if (alternative.lastCall.type == LCT.Group) {
               CollectReferencedVariables(alternative.lastCall.group!,variables);
            }
         }
      }

      public class InliningParameters(Procedure proc, Reachable reachable) {
         public int MaxInlineCalls = Settings.SettingValue<int>("MaxInlineCalls");
         public int NumberOfTimesCalled = reachable.ProcedureCalls.TryGetValue(proc.Id, out int n) ? n : 0;
         public int NumberOfCallsInProc = proc.group.CallCount();

         private readonly Procedure proc = proc;

         public override string ToString() 
            => $"Proc {proc.FQDN()} -> MaxInlineCalls: {MaxInlineCalls}, NumberOfTimesCalled: {NumberOfTimesCalled}, NumberOfCallsInProc: {NumberOfCallsInProc}";
      }

      private InliningParameters? inliningParameters = null!;
      public InliningParameters GetInliningParameters(Reachable reachable) => inliningParameters ??= new InliningParameters(this, reachable);//return inliningParameters;

      public int CallCount() => group.CallCount();

      /// <summary>
      /// True if this procedure can be inlined by the code generator.
      /// Current implemenation:
      /// The procedure has a single alternative conssiting of calls only where only the last call can fail (in which case, of course, the procedure can fail).
      /// Then if the procedure was marked for inlining OR contains only a single call or it is called only once, it is always inlineable.
      /// Otherwise let n = the number of times it is called, m = the number of calls in the procedure.
      /// It is inlinable if n*m <= the threshold specified in the settings.
      /// <param name="reachable">The reachability graph.</param>
      /// </summary>
      public bool IsInlinable(Reachable reachable) {
         if (Settings.SettingValue<bool>("NoProcInlining")) return false;
         if (IsConditionalCompilationOff || IsConditionalCompilationOn) return false;  // Handled explictily by the code generator.
         Alternative alternative = group.Alternatives[0];
         if (group.Alternatives.Count != 1 || alternative.lastCall.type != LCT.Standard) return false;
         if (alternative.calls.Any(call => call.CanFail)) return false;

         // The procedure meets the basic criteria for inlinabilty. Apply inlining parameters if appropriate.
         return   BodyType == TT.INLINEPROCBODY ||
                  GetInliningParameters(reachable).NumberOfCallsInProc == 1 ||
                  GetInliningParameters(reachable).NumberOfTimesCalled <= 1 ||
                  GetInliningParameters(reachable).NumberOfTimesCalled * GetInliningParameters(reachable).NumberOfCallsInProc <= Settings.SettingValue<int>("MaxInlineCalls");
      }

   }

   public class Call : NamedElement, IUnrecordedElement {
#if DEBUG_SERIALIZATION
#pragma warning disable CS0414
      [JsonInclude][JsonPropertyOrder(0)][JsonPropertyName("$type")] private readonly string _type = "Call";
#pragma warning restore CS0414
#endif
      /// <summary>
      /// The id of the algorithm being called.
      /// </summary>
      [JsonInclude][JsonPropertyOrder(51)] public ID id;
      [JsonIgnore] public List<IActualArg> Args {
         get {
            Procedure containingProc = ContainingProc;
            return [.. argRefs.Select<IElement,IActualArg>(argRef => {
               if (argRef is ID id) {
                  if (containingProc.TryGetLocal(id, out Local? local)) return local;
                  if (containingProc.TryGetAffix(id, out Affix? affix)) return affix;
                  CDL2Object? resolved = containingProc.Section?.GetResolvedObject(id);
                  if (resolved is IActualArg actual) return actual;
                  throw new ArgumentException($"Call {this} has an argument with ID {id} that is not an affix, local , var or const in {containingProc}.");
               } else if (argRef is STRING str) {
                  return str;
               } else {
                  throw new ArgumentException($"Call {this} has an argument referrence that is not an ID or a str: {argRef}.");
               }
            })];
         }
      }
      [JsonInclude][JsonPropertyOrder(52)] public List<IElement> argRefs = []; // Restricted to ID-s and strings
      [JsonInclude][JsonPropertyOrder(53)] public Guid containingProc;

      [JsonIgnore] public Procedure ContainingProc => NamedElement.From<Procedure>(containingProc)!;
     
      /// <summary>
      /// Set for Compiler procedures that are evaluated at code generation time.
      /// </summary>
      [JsonInclude][JsonPropertyOrder(54)] public bool IsBuiltin;

      [JsonIgnore] public bool IsConditionalCompilationOff => IsConditionalCompilation(on: false);
      [JsonIgnore] public bool IsConditionalCompilationOn  => IsConditionalCompilation(on: true);
      public Call(ID id, Procedure containingProc,Alternative containingAlternative, bool builtin = false) : base (id,focusType:SelectorType.CALL) {
         this.id = id;
         Parent = containingAlternative.GUID;
         IsBuiltin = builtin;
         this.containingProc = containingProc.GUID;
      }
      [JsonConstructor]
      public Call() {
         id = ID.AnonID; // For deserialization
         FocusType = SelectorType.CALL;
      }

      private bool IsConditionalCompilation(bool on) {
         if (Called != null) {
            return Called.IsConditionalCompilation(on);
         } else if (IsBuiltin && Builtin.IsTest(this)) {
            return on == Builtin.EvalTest(this);
         } else {
            return !on;
         }
      }

      override public string ToString() => $"{(IsBuiltin?RW.BUILTIN+" ":"")}{id.Name}{(Args.Any()?"+":"")}{string.Join("+",Args.Select(arg=>arg.Id))}";
      public bool TryGetAffix(ID id,out Affix affix) => ContainingProc.TryGetAffix(id,out affix);
      public bool TryGetLocal(ID id,out Local local) => ContainingProc.TryGetLocal(id,out local);
      [JsonIgnore] public Algorithm? Called {
         get {
            if (TryGetCalled(out Algorithm? called)) {
               return called;
            } else {
               return null;
            }
         }
      }
      public bool TryGetCalled(out Algorithm? called) {
         Debug.Assert(ContainingProc != null,$"{this} has null ContainingProc or its Parent is not a SectionById ");
         if (ContainingProc.Section!.TryGetDeclaration(id,out called)) return true;
         called = null;
         return false;
      }

      /// <summary>
      /// All cals are distinct.
      /// </summary>
      /// <param name="obj"></param>
      /// <returns></returns>
      public override bool Equals(object? obj) => false;
      public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

      [JsonIgnore] public bool CanFail => Called?.CanFail ?? true;
      [JsonIgnore] public bool AlwaysSucceeds => Called?.AlwaysSucceeds ?? false;
      [JsonIgnore] public bool HasEffect => Called?.HasEffect ?? false;
      [JsonIgnore] public bool HasNoEffect => Called?.HasNoEffect ?? false;
   }
   /// <summary>
   /// The last element(in an alternative) can be:
   /// Standard - a normal algorithm call which is the last item in the alternative's call list.
   /// Success, Fail, Abort - i.e., +, -, or?.
   /// Repeat - * with a reference to the group that is repeated possibly using the label
   /// Group - a nested group.
   /// </summary>   
   public class LastCall : NamedElement, IUnrecordedElement {
#if DEBUG_SERIALIZATION
#pragma warning disable CS0414
      [JsonInclude][JsonPropertyOrder(0)][JsonPropertyName("$type")] private readonly string _type = "LastCall";
#pragma warning restore CS0414
#endif
      [JsonInclude][JsonPropertyOrder(51)] public LCT type = LCT.None;
      [JsonInclude][JsonPropertyOrder(52)] public Group? group;
      [JsonInclude][JsonPropertyOrder(53)] public Call? call;
      [JsonInclude][JsonPropertyOrder(54)] public ID? label = ID.AnonID;
 
      public LastCall(LCT type, Alternative containingAlternative) {
         this.type = type;
         Parent = containingAlternative.GUID;
         FocusType = SelectorType.INVALID;
      }
      [JsonConstructor]
      public LastCall() { type = LCT.None; FocusType = SelectorType.INVALID; } // For deserialization

      public LastCall(Call call, Alternative containingAlternative) : this(LCT.Standard, containingAlternative) => this.call = call;
      public LastCall(Group group, Alternative containingAlternative) : this(LCT.Group, containingAlternative) => this.group = group;
      public LastCall(ID? label, Alternative containingAlternative) : this(LCT.Repeat, containingAlternative) => this.label = label;

      public bool TryGetCalled(out Algorithm? called) {
         if (type == LCT.Standard && call!.ContainingProc.Section!.TryGetDeclaration(call.id,out called)) return true;
         called = null;
         return false;
      }

      public override string ToString() => type switch {
         LCT.Standard => call?.ToString() ?? "",
         LCT.Succeed => "+",
         LCT.Fail => "-",
         LCT.Abort => "?",
         LCT.Repeat => $"*{(label is null || label == ID.AnonID ? "" : label.Name)}",
         LCT.Group => group?.ToString() ?? "",
         _ => "ERROR",
      };
   }
   public class Alternative : NamedElement, IUnrecordedElement {
#if DEBUG_SERIALIZATION
#pragma warning disable CS0414
      [JsonInclude][JsonPropertyOrder(0)][JsonPropertyName("$type")] private readonly string _type = "Alternative";
#pragma warning restore CS0414
#endif
      [JsonInclude][JsonPropertyOrder(41)] public List<Call> calls = [];
      [JsonInclude][JsonPropertyOrder(42)] public LastCall lastCall = new();
      public override IEnumerable<NamedElement> ChildElements() => [..calls, lastCall];

      [JsonIgnore] public bool IsConditionalOff = false;

      public Alternative(List<Call> calls, LastCall lastCall, Notes notes, Group containingGroup) : base(ID.AnonID, synthetic:false,SelectorType.INVALID) {
         this.calls = calls;
         this.lastCall = lastCall;
         Notes = notes;
         Parent = containingGroup.GUID;
      }

      public Alternative(Notes notes, Group group) : base(ID.AnonID, synthetic: false, SelectorType.INVALID) {
         Notes = notes;
         Parent = group.GUID;
      }

      [JsonConstructor] public Alternative() : base(ID.AnonID, synthetic: false, SelectorType.INVALID) { } // For deserialization

      [JsonIgnore] public bool CanFail =>  calls.Any(call => call.CanFail) || 
                              (lastCall!.type == LCT.Standard && lastCall.call!.CanFail) || 
                              lastCall.type == LCT.Fail || 
                              (lastCall.type == LCT.Group && lastCall.group!.CanFail);
      private Call? FirstCall() {
         if (calls.Count > 0) return calls[0];
         if (lastCall!.type == LCT.Standard) return lastCall.call;
         return null;
      }

      internal int CallCount() => calls.Count + (lastCall.type == LCT.Standard ? 1 : 0) + (lastCall.type == LCT.Group ? lastCall.group!.CallCount() : 0);

      /// <summary>
      /// True if the alternative terminates the algorithm, i.e., its last call is a fail or abort.
      /// No need to check for succeed becasue that is just normal alternative completion.
      /// </summary>
      [JsonIgnore] public bool Terminates => lastCall.type == LCT.Fail || lastCall.type == LCT.Abort;
      [JsonIgnore] public bool IsConditionalCompilationOn => FirstCall() is Call firstCall && firstCall.IsConditionalCompilationOn;
      [JsonIgnore] public bool IsConditionalCompilationOff => FirstCall() is Call firstCall && firstCall.IsConditionalCompilationOff;

      /// <summary>
      /// If the last call position contained an actual call convert it to a LastCall
      /// </summary>
      public void NormalizeCalls() {
         if (lastCall.type == LCT.None) {
            lastCall = new LastCall(calls.Last(), this);
            calls.RemoveAt(calls.Count - 1);
         }
      }
   }

   /// <summary>
   /// Represents a group of alternatives
   /// </summary>
   /// <remarks>A group contains a collection of <see cref="Alternative"/>-s.
   /// <see cref="Procedure"/>-s contain a single top level group. In this case the parent is the procedures.
   /// A group nested in an alternative has the alteernative as its parent. Use 
   /// </remarks>
   public class Group : NamedElement, IUnrecordedElement {
      [JsonInclude][JsonPropertyOrder(30)] public List<Alternative> Alternatives = [];
      public override IEnumerable<NamedElement> ChildElements() => Alternatives;

      [JsonConstructor]
      public Group() : base(ID.AnonID,synthetic: false, SelectorType.INVALID) { }
      public Group(ID label,Guid parentGuid) : base(label, synthetic: false, SelectorType.INVALID) {
         Parent = parentGuid;
      }
      public Group(ID? label,List<Alternative> alternatives,Guid parentGuid,bool synthetic) : base(synthetic ? Database.NextGroupLabel : label!,synthetic:synthetic) {
         Parent = parentGuid;
         Alternatives = alternatives;
         FocusType = SelectorType.INVALID;
      }

      public Group? ParentGroup() {
         NamedElement? parent = NamedElement.From<Group>(Parent);
         if (parent is Group group) return group;
         if (parent is Alternative alternative && alternative.lastCall.type == LCT.Group) {
            return alternative.lastCall.group;
         }
         return null;
      }
      public bool HasLabeledAncestorGroup(ID label) {
         Group? group = ParentGroup();
         while (group != null) {
            if (group.Id == label) return true;
            group = group.ParentGroup();
         }
         return false;
      }
      [JsonIgnore] public bool HasAnonymousRepeat => HasAnAnonymousRepeat();
      [JsonIgnore] public bool HasNoAnonymousRepeat => ! HasAnonymousRepeat;
      [JsonIgnore] public bool CanFail => Alternatives.Any(alternative => alternative.lastCall.type == LastCallType.Fail) || Alternatives.Last().CanFail;
      /// <summary>
      /// The group has an alternative which has at least one anonymous repeat operator.
      /// Required for target languages (e.g., PowerShell) that have to use a loop to simulate goto-s.
      /// Only anonymous repeat operators are considered because labeled repeats are handle when the label is placed.
      /// </summary>
      public bool HasAnAnonymousRepeat() {
         foreach (Alternative alternative in Alternatives) {
            if (alternative.lastCall.type == LCT.Repeat && alternative.lastCall.label! == ID.AnonID) return true;
         }
         return false;
      }
      public override string ToString() => $"GRP {Id.Name} {Alternatives.Count.Plural("ALT")}";
      internal int CallCount() => Alternatives.Sum(alt=>alt.CallCount());
   }


   public class INT : IElement {
      [JsonInclude] public long value;
      public INT(Token intToken) {
         Debug.Assert(intToken.type == TT.INT && intToken.intValue != null);
         value = (long)intToken.intValue;
      }
      public INT(long value) => this.value = value;
      [JsonConstructor]
      public INT() : this(0) { } // For deserialization
      override public string ToString() => value.ToString();
   }
   public class FLOAT : IElement {
      [JsonInclude] public double value;
      public FLOAT(Token floatToken) {
         Debug.Assert(floatToken.type == TT.FLOAT && floatToken.floatValue != null);
         value = (double)floatToken.floatValue;
      }
      public FLOAT(double value) => this.value = value;
      [JsonConstructor]
      public FLOAT() : this(0.0) { } // For deserialization
      override public string ToString() => value.ToString();
   }
   public class STRING : IElement, IActualArg {
      [JsonInclude] public string value;
      public STRING(Token str) {
         Debug.Assert(str.type == TT.STRING && str.StringValue != null);
         value = str.StringValue;
      }
      public STRING(string str) => value = str;
      [JsonConstructor]
      public STRING() : this("") { } // For deserialization
      public ID Id => ID.AnonID;

      private static string EscapedCDL2(string str) {
         StringBuilder sb = new();
         foreach (char c in str) {
            if (Token.Char2Escape.TryGetValue(c.ToString(),out string? escape)) {
               sb.Append($"${escape}");
            } else {
               sb.Append(c);
            }
         }
         return sb.ToString();
      }
      public string AsDecoratedCDL2String(Emitter emitter) => $"\"{EscapedCDL2(value)}\"".Decorate(emitter,SE.String);
      override public string ToString() => $"\"{value}\"";
   }
   public class LIST : CDL2Object, IDataElement {
      [JsonInclude] public ID lwb;
      [JsonInclude] public ID upb;

      public LIST(ID id,Section section,ID lwb,ID upb) : base(id,section,null, FocusType:SelectorType.LIST) {
         this.lwb = lwb;
         this.upb = upb;
         SE = SE.List;
      }
      [JsonConstructor]
      public LIST() : base() {
         lwb = ID.AnonID;
         upb = ID.AnonID;
         FocusType = SelectorType.LIST;
      } // For deserialization
      override public string ToString() => $"LIST {Id}({lwb}:{upb})";
   }
   public class Var : CDL2Object, IDataElement, IFailureProtected, IActualArg, ITrackedVar {
      public Var(ID id, Section section) : base(id, section, null, FocusType:SelectorType.VAR) => SE = SE.Var;
      [JsonConstructor]
      public Var() : base() { FocusType = SelectorType.VAR; } // For deserialization

      override public string ToString() => $"VAR {Id.Name}";
   }
   public class Const : CDL2Object, IDataElement, IProvidable, IExportable, IActualArg, IImportable {
      [JsonInclude]
      public List<IElement> elements = [];  // Will contain ids (const, var, list) and strings, integers, floats

      public Const(ID id,Section section) : base(id,section,null, FocusType:SelectorType.CONST) => SE = SE.Const;
      [JsonConstructor]
      public Const() : base() { FocusType = SelectorType.CONST;  } // For deserialization
   }

   public class ImportedConst : Const, IImportable, IDataElement {

      public override string ToString() => "IMPORTED " + base.ToString();
      public override bool IsImported => true;

      public ImportedConst(ID id,Section section) : base(id,section) { }
      public ImportedConst() : base() { } // For deserialization
   }



   /// <summary>
   /// Represents a formal argument in an algorithm.
   /// It is just an ID with annotations. An arg is considered to be equal to another arg or ID if the names are the same.
   /// </summary>
   public class Affix : NamedElement, IFailureProtected, ITrackedVar  {
      public static readonly Affix Default = new (ID.AnonID,AffixDir.NONE,AffixType.std);
      [JsonInclude] public AffixDir affixDir;
      [JsonInclude] public AffixType affixType;
      [JsonIgnore] public Algorithm? ContainingAlgorithm {
         get => Database.Instance.NamedElements[Parent] as Algorithm;
         set => Parent = value?.GUID ?? Guid.Empty;
      }


      /// <param Id="Id"></param>
      /// <param Id="dir"></param>
      /// <param Id="type"></param>
      public Affix(ID id,AffixDir dir,AffixType type) : base(id,focusType:SelectorType.AFFIX) {
         affixDir = dir;
         affixType = type;
      }
      [JsonConstructor]
      public Affix() : base() { FocusType = SelectorType.AFFIX; } // For deserialization

      [JsonIgnore] public bool IsInput => affixDir == AffixDir.input || affixDir == AffixDir.transput;
      [JsonIgnore] public bool IsInputOnly => affixDir == AffixDir.input;
      [JsonIgnore] public bool IsOutput => affixDir == AffixDir.output || affixDir == AffixDir.transput;
      [JsonIgnore] public bool IsOutputOnly => affixDir == AffixDir.output;
      [JsonIgnore] public bool IsTransput => affixDir == AffixDir.transput;
      [JsonIgnore] public bool IsString => affixType == AffixType.str;

      [JsonIgnore] public SE SyntaxElement => IsString ? SE.StringAffix : IsTransput ? SE.TransputAffix : IsInput ? SE.InputAffix : SE.OutputAffix;

      public override bool Equals(object? obj) => obj is Affix affix && EqualityComparer<ID>.Default.Equals(Id,affix.Id);
      public override int GetHashCode() => HashCode.Combine(Id);

      override public string ToString() => affixType == AffixType.std ? $"{(IsInput ? ">" : "")}{Id}{(IsOutput ? ">" : "")}" : $"*{Id}";

      public static bool operator ==(Affix? left,Affix? right) => EqualityComparer<Affix>.Default.Equals(left,right);
      public static bool operator !=(Affix? left,Affix? right) => !(left == right);
   }

   public class Local(ID id) : NamedElement(id,focusType:SelectorType.LOCAL), IActualArg, ITrackedVar, IParameter {
      [JsonIgnore] public Algorithm? ContainingAlgorithm {
         get => Database.Instance.NamedElements[Parent] as Algorithm;
         set => Parent = value?.GUID ?? Guid.Empty;
      }
      [JsonIgnore]
      public static readonly Local Default = new(ID.AnonID);
      override public string ToString() => $"-{Id.Name}";
      [JsonConstructor]
      public Local() : this(ID.AnonID) { } // For deserialization
   }

   public class Undeclared : CDL2Object {
      public readonly static Undeclared Instance = new();

      private Undeclared() : base(ID.AnonID) => SE = SE.Other;
   }

}

