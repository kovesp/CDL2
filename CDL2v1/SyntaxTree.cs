// Ignore Spelling: Transput CDL abstr ext inv ludes lude lwb upb FQN

using Microsoft.Windows.Themes;

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
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
   public interface IMacroElement { }
   public interface IConstElement { }
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

   //public class NamedElementID {
   //   [JsonInclude] public string? TypeName;
   //   [JsonInclude] public ID? ModuleID;
   //   [JsonInclude] public ID? LayerID;
   //   [JsonInclude] public ID? SectionID;
   //   [JsonInclude] public ID? AlgorithmID;
   //   [JsonInclude] public ID?  ElementID;
   //   [JsonInclude] public Guid GUID;


   //   [JsonConstructor]
   //   public NamedElementID() { }

   //   /// <summary>
   //   /// Create a new NamedElementID for the given element.
   //   /// </summary>
   //   /// <param name="element"></param>
   //   public NamedElementID(NamedElement element) {
   //      TypeName = element.GetType().Name;
   //      GUID = element.GUID;

   //      if (element is Layer layer) {
   //         ModuleID = layer.Module?.Id;
   //      } else if (element is Section section) {
   //         ModuleID = section.Module?.Id;
   //         LayerID = section.Layer?.Id;
   //      } else if (element is CDL2Object cdl2Object) {
   //         SetContainer(cdl2Object);
   //      } else if (element is Affix affix) {
   //         SetContainer(affix.ContainingAlgorithm);
   //         AlgorithmID = affix.ContainingAlgorithm?.Id;
   //      } else if (element is Local local) {
   //         SetContainer(local.ContainingAlgorithm);
   //         AlgorithmID = local.ContainingAlgorithm?.Id;
   //      }
   //      ElementID = element!.Id;

   //      void SetContainer(CDL2Object? element) {
   //         if (element != null) {
   //            ModuleID = element.Module?.Id;
   //            LayerID = element.Layer?.Id;
   //            SectionID = element.Section?.Id;
   //         }
   //      }
   //   }

   //   /// <summary>
   //   /// Called by Database load when restroing NamedElements from NamedElementIDs
   //   /// </summary>>
   //   /// <returns></returns>
   //   /// <exception cref="NotImplementedException"></exception>
   //   public NamedElement? GetElement() {
   //      Debug.Assert(ElementID is not null, "GetElement: ElementID is null");
   //      Module   ? mod     = ModuleID is not null    ? Database.Instance. ModuleByName(ModuleID) : null;
   //      Layer    ? lay     = LayerID is not null     ? mod?.Children.FirstOrDefault(layer => layer.Id == LayerID) as Layer : null;
   //      Section  ? sec     = SectionID is not null   ? lay?.Children.FirstOrDefault(section => section.Id == SectionID) as Section : null;
   //      Algorithm? alg     = AlgorithmID is not null ? sec?.Declarations[AlgorithmID] as Algorithm : null;
   //      return TypeName switch {
   //         "Program" => Database.Instance.ProgramByName(ElementID!.Name) ?? Database.Instance.FirstProgram, 
   //         "Module"  => mod,
   //         "Layer"   => lay,
   //         "Section" => sec,
   //         "Macro" or "ImportedAlgorithm" or "Procedure" or "Macro" or "Const" or "ImportedConst" or "Var" or "LIST"
   //                   => sec!.Declarations[ElementID],
   //         "Affix"   => alg?.affixes?.Find(aff => aff.Id == ElementID),
   //         "Local"   => alg?.locals?.Where(loc => loc.Id == ElementID)?.FirstOrDefault(),
   //         "Group"   => null, //TODO: Fix Group case in NamedElementID.GetElement
   //         _         => throw new NotImplementedException($"GetValue not implemented for {TypeName}"),
   //      };
   //   }

   //   public override string ToString() {
   //      static string id(string type, ID? id) => id is null ? "" : $"{type} {id.Name} ";
   //      return $"[{GUID}] {id("MOD",ModuleID)}{id("LAY",LayerID)}{id("SEC",SectionID)}{id("ALG",AlgorithmID)}{TypeName} {ElementID}";
   //   }

   //   public override bool Equals(object? obj) => obj is NamedElementID iD && GUID.Equals(iD.GUID);
   //   public override int GetHashCode() => HashCode.Combine(GUID);
   //}

   /// <summary>
   /// Base class for all elements that have names in the syntax tree.
   /// </summary>
   public abstract class NamedElement {
      [JsonInclude][JsonPropertyOrder(0)] public Guid GUID;
      [JsonInclude][JsonPropertyOrder(1)]  public ID Id { get; set; }
      /// <summary>
      /// True if the object is synthetic, i.e., generated by the parser.
      /// Objects that can be synthetic:
      ///  - Procedures: generated for Section ludes.
      ///  - Groups: indicating that their label is generated.
      /// </summary>
      [JsonInclude][JsonPropertyOrder(2)] public bool IsSynthetic { get; }
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
      [JsonInclude]
      [JsonPropertyOrder(3)]
      public Guid Parent { get; set; } = Guid.Empty;
      [JsonInclude][JsonPropertyOrder(4)] public string? Comments { get; set; }
      [JsonInclude][JsonPropertyOrder(5)] public Notes Notes { get; set; } = [];

      /// <summary>
      /// Create a new NamedElement with the given ID.
      /// The Guid is generated and the element is added to the database.
      /// </summary>
      /// <param name="id"></param>
      /// <param name="synthetic"></param>
      public NamedElement(ID id,bool synthetic = false) {
         Id = id;
         IsSynthetic = synthetic;
         GUID = Guid.NewGuid();
         Database.Instance.AddNamedElement(this); // Register the element in the database.
      }
      /// <summary>
      /// Use when deserializing the element.
      /// </summary>
      [JsonConstructor]
      public NamedElement() => Id = ID.AnonID;

      /// <summary>
      /// Retrun the ancestor of the required type which must be a container.
      /// </summary>
      /// <typeparam name="T">Module,Layer,Section. Not useful for Program.</typeparam>
      /// <returns></returns>
      public T? AncestorContainer<T>() where T : Container {
         if (Parent == Guid.Empty) {
            return null!;  // The element has no parent
         } else if (this is T self) {
            return self;   // The element is of the given type
         } else if (Database.Instance.NamedElements.TryGetValue(Parent, out NamedElement? parent)) {
            Debug.Assert(parent is Container && parent != null, $"Ancestor: Parent {parent} is not a Container, but element is {GetType().Name}");
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
      public Container? ParentContainer() => Parent != Guid.Empty && Database.Instance.NamedElements.TryGetValue(Parent,out NamedElement? parent) && parent is Container container ? container : null;

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
   }

   /// <summary>
   /// Base class for all elements that can contain other elements, i.e., the program and modules, layers, sections.
   /// </summary>
   public abstract class Container : NamedElement {
      [JsonConstructor]
      public Container() : base() { }
      /// <summary>
      /// The Container children of the container. Layers are ordered, hence the list.
      /// </summary>
      [JsonInclude]
      public List<Guid> Children = [];

      /// <param Id="Id"></param>
      public Container(ID id,string? comments,Notes? notes) : base(id) {
         Comments = comments;
         AddNotes("Parser", notes);
      }

      public Container(ID id,Container? parent,string? comments = null,Notes? notes = null) : this(id,comments,notes) { 
         Parent = parent?.GUID ?? Guid.Empty;
         ContainerName = $"{parent?.ContainerName ?? ""} {TypeShortName} {id.Name}".Trim();
         if (parent != null && parent.Children.Contains(GUID)) {
            Logger.ReportError($"{ContainerName} is already a child of {parent.ContainerName}");
         } else {
            parent?.Children.Add(GUID);
         }
      }

      // The Ludes are stored in a dictionary with the reserved word as the key. The values are lists of IDs.
      // SectionById Ludes will be generated as Procedures and given the Id of the lude type (which are not legal as a CDL2 Id).
      [JsonInclude]
      public Dictionary<RW,List<ID>> Ludes { get; } = new() {
         { RW.PRELUDE,[] },
         { RW.ROOT,[] },
         { RW.POSTLUDE,[] }
      };
      public static readonly List<RW> LudeTypes = [RW.PRELUDE, RW.ROOT, RW.POSTLUDE];

      /// <summary>
      /// Sets the LudeParser action for the container. The default is to do nothing.
      /// </summary>
      public Action<Parser,RW,Container> LudeParser = (parser,ludeType,container) => { };

      /// <summary>
      /// The short Id of the container with its type. Used in the ToString method.
      /// </summary>
      public string ContainerName = string.Empty;
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
      public IEnumerable<Module> Lude(RW ludeType) => Ludes[ludeType].Select(id => Database.Instance.ModuleByName(id))!;

      [JsonInclude]
      public IDSet Parts = [];
      [JsonIgnore]
      public IEnumerable<Module> Modules => Database.Instance.NamedElements.Values.OfType<Module>().Where(mod=>Parts.Contains(mod.Id));
      /// <summary>
      /// Maps all identifiers exported by the modules in the program to the exporting module.
      /// </summary>
      [JsonIgnore]
      public readonly IDDictionary<IExportable> Exports = [];
      /// <summary>
      /// Program Ludes are a list of module IDs.
      /// </summary>
      /// <param Id="Id"></param>
      public Program(ID id,string? comments,Notes notes) : base(id,null,comments,notes) {
         LudeParser = Parser.ParseLudeOfIDs;
      }
      public Program() { }
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
      public Module(ID id,string? comments,Notes notes) : base(id,null,comments,notes) {
         LudeParser = Parser.ParseLudeOfIDs;
         Comments = comments;
      }

      public Section? SectionById(ID id) {
         foreach (Section section in Sections) if (section.Id == id) return section;
         return null;
      }
      public bool TryGetSectionById(ID id, out Section? section) {
         section = SectionById(id);
         return section != null;
      }

      [JsonIgnore]
      public IEnumerable<Section> Sections => Layers.SelectMany(layer => layer.Children.Select(GUID => Database.Instance.NamedElements[GUID] as Section))!;

      [JsonIgnore]
      public IEnumerable<Layer> Layers => Children.Select(GUID => Database.Instance.NamedElements[GUID] as Layer)!;
   }

   /// <summary>
   /// Represents a layer in the syntax tree.
   /// Notice that layers don'localObject have Ludes.
   /// </summary>
   /// <param Id="Id"></param>
   /// <param Id="module"></param>
   /// <param PhaseName="ancestor">The layer from which this layer is extended. Null for the lowest layer.</param>
   public class Layer(ID id,Module module,Layer? ancestor,string? comments = null,Notes? notes=null) : Container(id,module,comments,notes) {
      /// <summary>
      /// The ancestor of a layer is the previous layer in the layer list of the containing module.
      /// </summary>
      [JsonInclude]
      public Guid? AncestorGUID = ancestor?.GUID;
      [JsonIgnore]
      public Layer? Ancestor => AncestorGUID is not null && Database.Instance.NamedElements.TryGetValue(AncestorGUID.Value, out NamedElement? ancestor) && ancestor is Layer layer ? layer : null;
      [JsonIgnore]
      public Layer? Successor => Module?.Layers.FirstOrDefault(layer => layer.AncestorGUID == GUID);

      /// <summary>
      /// The visible objects in this layer, i.e, the Consts and Algorithms extended in the sections of this layer and abstracted in the sections of the ancestor.
      /// </summary>
      [JsonIgnore]
      public IDDictionary<IProvidable> Visible { get; } = [];

      public IEnumerable<Section> Sections => Children.Select(GUID => Database.Instance.NamedElements[GUID] as Section)!;
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

      [JsonInclude] public readonly Set<ID> ext = [];
      [JsonInclude] public readonly Set<ID> abstr = [];
      [JsonInclude] public readonly Set<ID> inv = [];
      [JsonInclude] public readonly Set<ID> export = [];
      [JsonInclude] public readonly Set<ID> import = [];

      /// <summary>
      /// Holds the Declarations of the SectionById. The key is the ID of the declaration.
      /// </summary>
      [JsonInclude]
      public readonly IDDictionary<CDL2Object> Declarations = [];

      [JsonIgnore] public IEnumerable<Const> Constants => Declarations.Values.OfType<Const>();
      [JsonIgnore] public IEnumerable<Var> Variables => Declarations.Values.OfType<Var>();
      [JsonIgnore] public IEnumerable<LIST> Lists => Declarations.Values.OfType<LIST>();
      [JsonIgnore] public IEnumerable<Macro> Macros => Declarations.Values.OfType<Macro>();
      [JsonIgnore] public IEnumerable<Procedure> Procedures => Declarations.Values.OfType<Procedure>();
      [JsonIgnore] public IEnumerable<Algorithm> Algorithms => Declarations.Values.OfType<Algorithm>();
      [JsonIgnore] public IEnumerable<Algorithm> NonSyntheticAlgorithms => Declarations.Values.OfType<Algorithm>().Where(alg=>!alg.IsSynthetic);

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
      /// The non-synthetic procedures.
      /// Synthetic procedures are generated by the parser for container ludes.
      /// </summary>
      [JsonIgnore] public IEnumerable<Procedure> NonSyntheticProcedures => Procedures.Where(proc => !proc.IsSynthetic);
      [JsonIgnore] public IEnumerable<Procedure> SyntheticProcedures => Procedures.Where(proc => proc.IsSynthetic);

      /// <summary>
      /// Sections have Ludes each of which contains the ID of generated FUNCTION or ACTION which consist of a single alternative.
      /// </summary>
      /// <param Id="Id"></param>
      /// <param Id="layer"></param>
      public Section(ID id,Layer layer,string? comments = null,Notes? notes = null) : base(id,layer,comments,notes) => LudeParser = Parser.ParseLudeOfCalls;

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
   public abstract class CDL2Object : NamedElement {
      public CDL2Object(ID id,Section section,string? comments,bool synthetic = false) : base(id,synthetic) {
         Parent = section.GUID;
         Comments = comments;
      }
      public CDL2Object(ID id) : base(id) { }
      public virtual bool IsImported => false;

      public SyntacticElement SE { get; protected set; }

      /// <summary>
      /// Given that objects have to be unique by name within a section and extended/abstracted objects within a layer, objects with the same Id are considered the same.
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
   public abstract class Algorithm : CDL2Object, IProvidable, IImportable, IExportable {
      [JsonInclude]
      public          RW algorithmType;            // One of FUNCTION, ACTION, TEST or PREDICATE (reservedWordValue will never be null)
      [JsonInclude]
      public readonly TT bodyType;                 // One of : or := (for CODE only) and = or =: (for MACRO only)
      [JsonInclude]
      public readonly List<Affix> affixes;         // The affixes of this algorithm. A List because they are ordered.
      [JsonInclude]
      public readonly Set<Local> locals;           // The Declarations variables of this algorithm.


      public Algorithm(ID id,List<Affix> affixes,Set<Local> locals,Token algorithmType,TT bodyType,Section section,bool synthetic = false) 
            : base(id,section,algorithmType.Comments,synthetic) {
         this.affixes = affixes;
         this.locals = locals;
         this.algorithmType = algorithmType.reservedWordValue ?? RW.FUNCTION;
         this.bodyType = bodyType;
         this.SE = SE.AlgorithmName;
         foreach (Affix affix in affixes) affix.ContainingAlgorithm = this;
         foreach (Local local in locals)  local.ContainingAlgorithm = this;
      }

      public AlgorithmNameType NameType {
         get {
            AlgorithmNameType ait = AlgorithmNameType.None;
            if (algorithmType == RW.TEST || algorithmType == RW.PREDICATE) ait |= AlgorithmNameType.CanFail;
            if (algorithmType == RW.ACTION || algorithmType == RW.PREDICATE) ait |= AlgorithmNameType.HasEffect;
            if (bodyType == TT.MACROBODY || bodyType == TT.MACROPROCBODY) ait |= AlgorithmNameType.Macro;
            return ait;
         }
      }

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
      public string AlgorithmName => $"{algorithmType} {Id}";

      [JsonIgnore] public bool CanFail => algorithmType == RW.TEST || algorithmType == RW.PREDICATE;
      [JsonIgnore] public bool AlwaysSucceeds => !CanFail;
      [JsonIgnore] public bool HasEffect => algorithmType == RW.PREDICATE || algorithmType == RW.ACTION;
      [JsonIgnore] public bool HasNoEffect => !HasEffect;
      [JsonIgnore] public bool NeedsFinalization => CanFail && (affixes.Any(affix => affix.IsOutput) || GetReferencedVariables().Any());
      [JsonIgnore] public bool IsInlineMacro => bodyType == TT.MACROBODY;
      /// <summary>
      /// Check if this is a conditional compilation flag. That is, the body consists of a single fail respectively succeed operator.
      /// </summary>
      /// <param name="group"></param>
      /// <returns></returns>
      public virtual bool IsConditionalCompilationOff => false;
      public virtual bool IsConditionalCompilationOn => false;
      public bool IsConditionalCompilation(bool? on=null) => on is null ? IsConditionalCompilationOn || IsConditionalCompilationOff : (bool)on ? IsConditionalCompilationOn : IsConditionalCompilationOff;
      public bool TryGetAffix(ID id,out Affix affix) => (affix = affixes.FirstOrDefault(affix => affix.Id == id,Affix.Default)) != Affix.Default;
      public bool TryGetLocal(ID id,out Local local) => (local = locals.FirstOrDefault(local => local.Id == id,Local.Default)) != Local.Default;

      public override string ToString() {
         StringBuilder buffer = new();
         buffer.Append($"{TypeShortName} {Id.Name}");
         foreach (Affix affix in affixes) {
            buffer.Append(Token.TokenType2Glyph[affix.IsString ? TT.STRINGAFFIXSEP : TT.AFFIXSEP]);
            buffer.Append(affix);
         }
         foreach (Local local in locals) buffer.Append(local);
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

      //         if (SectionById.inv.Contains(Id)) { // More complicated then Declarations. Need to find the container the algorithm is abstracted or extended from
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
      public abstract IEnumerable<Var> GetReferencedVariables();
      override public string TypeShortName => $"{algorithmType}";
   }

   /// <summary>
   /// An importable algorithm is a reference to an algorithm in another module. Thus it has only a header and no body.
   /// </summary>
   public class ImportedAlgorithm(ID id,List<Affix> affxies,Token algorithmType,Section section) : Algorithm(id,affxies,[],algorithmType,TT.NOBODY,section), IImportable {
      public override IEnumerable<Var> GetReferencedVariables() => [];
      public override string ToString() => "IMPORTED "+ base.ToString();
      public override bool IsImported => true;
   }

   /// <summary>
   /// Represents a macro in the syntax tree.
   /// </summary>
   /// <param Id="Id"></param>
   /// <param Id="affixes"></param>
   /// <param Id="locals"></param>
   /// <param Id="algorithmType"></param>
   /// <param Id="bodyType"></param>
   /// <param Id="container"></param>
   public class Macro(ID id,List<Affix> affixes,Set<Local> locals,Token algorithmType,TT bodyType,Section section) : Algorithm(id,affixes,locals,algorithmType,bodyType,section) {
      [JsonInclude]
      public List<IMacroElement> elements = [];

      public override IEnumerable<Var> GetReferencedVariables() => elements.OfType<Var>();
   }
   /// <summary>
   /// Represents a procedure in the syntax tree.
   /// </summary>
   /// <param Id="Id"></param>
   /// <param Id="affixes"></param>
   /// <param Id="locals"></param>
   /// <param Id="algorithmType"></param>
   /// <param Id="bodyType"></param>
   /// <param Id="SectionById"></param>
   public class Procedure(ID id,List<Affix> affxies,Set<Local> locals,Token algorithmType,TT bodyType,Section section,bool synthetic = false) 
         : Algorithm(id,affxies,locals,algorithmType,bodyType,section,synthetic) {
      [JsonInclude]
      public Group group = new(id,[],null,synthetic: false);
      /// <summary>
      /// True if the procedure is an Action or Function that has only a single alternative (which is a sequence of calls none of which can fail ... which will be guarenteed by the sematic analyzer)
      /// </summary>
      [JsonIgnore] public bool IsVerySimple => AlwaysSucceeds && group.alternatives.Count == 1 && HasNoGroups;
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
      [JsonIgnore] public override bool IsConditionalCompilationOff => CanFail && group.alternatives.Count == 1 && group.alternatives[0].calls.Count == 0 && group.alternatives[0].lastCall.type == LCT.Fail;
      [JsonIgnore] public override bool IsConditionalCompilationOn => CanFail && group.alternatives.Count == 1 && group.alternatives[0].calls.Count == 0 && group.alternatives[0].lastCall.type == LCT.Succeed;

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
            foreach (Alternative alternative in group.alternatives) {
               if (alternative.lastCall.type == LCT.Group) return false;
            }
            return true;
         }
      }

      /// <summary>
      /// The parser will set this if a repeat operator reference the procedure itself.
      /// </summary>
      [JsonInclude] public bool repeatsProcedure = false;

      public Procedure(RW ludeType,Section section) : this(ID.From(ludeType),[],[],Token.ACTIONToken,TT.PROCBODY,section,true) { } // Used for container Ludes which are parameterless actions with no locals.
      public override IEnumerable<Var> GetReferencedVariables() {
         Set<Var> variables = [];
         CollectReferencedVariables(group,variables);
         return variables;
      }
      private static void CollectReferencedVariables(Group group,Set<Var> variables) {
         foreach (Alternative alternative in group.alternatives) {
            foreach (Call call in alternative.calls) foreach (Var variable in call.args.OfType<Var>()) variables.Add(variable);
            if (alternative.lastCall.type == LCT.Standard) {
               foreach (Var variable in alternative.lastCall.call!.args.OfType<Var>()) variables.Add(variable);
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
         Alternative alternative = group.alternatives[0];
         if (group.alternatives.Count != 1 || alternative.lastCall.type != LCT.Standard) return false;
         if (alternative.calls.Any(call => call.CanFail)) return false;

         // The procedure meets the basic criteria for inlinabilty. Apply inlining parameters if appropriate.
         return   bodyType == TT.INLINEPROCBODY ||
                  GetInliningParameters(reachable).NumberOfCallsInProc == 1 ||
                  GetInliningParameters(reachable).NumberOfTimesCalled <= 1 ||
                  GetInliningParameters(reachable).NumberOfTimesCalled * GetInliningParameters(reachable).NumberOfCallsInProc <= Settings.SettingValue<int>("MaxInlineCalls");
      }

   }

   public class Call(ID id,Procedure containingProc,bool builtin=false) {
      [JsonInclude] public readonly ID id = id;
      [JsonInclude] public readonly List<IActualArg> args = [];
      [JsonInclude] public readonly Procedure ContainingProc = containingProc;
      /// <summary>
      /// Set for Compiler procedures that are evaluated at code generation time.
      /// </summary>
      [JsonInclude] public readonly bool IsBuiltin = builtin;

      [JsonIgnore] public bool IsConditionalCompilationOff => IsConditionalCompilation(on: false);
      [JsonIgnore] public bool IsConditionalCompilationOn  => IsConditionalCompilation(on: true);
      private bool IsConditionalCompilation(bool on) {
         if (Called != null) {
            return Called.IsConditionalCompilation(on);
         } else if (IsBuiltin && Builtin.IsTest(this)) {
            return on == Builtin.EvalTest(this);
         } else {
            return !on;
         }
      }

      override public string ToString() => $"{(IsBuiltin?RW.BUILTIN+" ":"")}{id.Name}{(args.Count>0?"+":"")}{string.Join("+",args.Select(arg=>arg.Id))}";
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
   /// <param Id="type"></param>   
   public class LastCall(LCT type) {
      [JsonInclude] public readonly LCT type = type;
      [JsonInclude] public readonly Group? group;
      [JsonInclude] public readonly Call? call;
      [JsonInclude] public readonly ID? label = ID.AnonID;

      public LastCall(Call call) : this(LCT.Standard) => this.call = call;
      public LastCall(Group group) : this(LCT.Group) => this.group = group;
      public LastCall(ID? label) : this(LCT.Repeat) => this.label = label;

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
   public class Alternative(List<Call> calls,LastCall lastCall,Notes notes) {
      [JsonInclude] public readonly List<Call> calls = calls;
      [JsonInclude] public readonly LastCall lastCall = lastCall;
      [JsonInclude] public readonly Notes Notes = notes;
      public bool IsConditionalOff = false;

      [JsonIgnore] public bool CanFail =>  calls.Any(call => call.CanFail) || 
                              (lastCall.type == LCT.Standard && lastCall.call!.CanFail) || 
                              lastCall.type == LCT.Fail || 
                              (lastCall.type == LCT.Group && lastCall.group!.CanFail);
      private Call? FirstCall() {
         if (calls.Count > 0) return calls[0];
         if (lastCall.type == LCT.Standard) return lastCall.call;
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
   }
   // Note that the Id in this case is the label.
   public class Group(ID? label,List<Alternative> alternatives,Group? parent,bool synthetic) 
         : NamedElement(synthetic ? Database.NextGroupLabel : label!,synthetic:synthetic) {
      [JsonInclude] public List<Alternative> alternatives = alternatives;
      [JsonInclude] public new readonly Group? Parent = parent;

      [JsonIgnore] public bool HasAnonymousRepeat => HasAnAnonymousRepeat();
      [JsonIgnore] public bool HasNoAnonymousRepeat => ! HasAnonymousRepeat;
      [JsonIgnore] public bool CanFail => alternatives.Any(alternative => alternative.lastCall.type == LastCallType.Fail) || alternatives.Last().CanFail;
      /// <summary>
      /// The group has an alternative which has at least one anonymous repeat operator.
      /// Required for target languages (e.g., PowerShell) that have to use a loop to simulate goto-s.
      /// Only anonymous repeat operators are considered because labeled repeats are handle when the label is placed.
      /// </summary>
      public bool HasAnAnonymousRepeat() {
         foreach (Alternative alternative in alternatives) {
            if (alternative.lastCall.type == LCT.Repeat && alternative.lastCall.label! == ID.AnonID) return true;
         }
         return false;
      }
      public override string ToString() => $"GRP {Id.Name} {alternatives.Count.Plural("ALT")}";
      internal int CallCount() => alternatives.Sum(alt=>alt.CallCount());
   }


   public class INT : IConstElement, IMacroElement {
      [JsonInclude] public readonly long value;
      public INT(Token intToken) {
         Debug.Assert(intToken.type == TT.INT && intToken.intValue != null);
         value = (long)intToken.intValue;
      }
      override public string ToString() => value.ToString();
   }
   public class FLOAT : IConstElement, IMacroElement {
      [JsonInclude] public readonly double value;
      public FLOAT(Token floatToken) {
         Debug.Assert(floatToken.type == TT.FLOAT && floatToken.floatValue != null);
         value = (double)floatToken.floatValue;
      }
      override public string ToString() => value.ToString();
   }
   public class STRING : IMacroElement, IConstElement, IActualArg {
      [JsonInclude] public readonly string value;
      public STRING(Token str) {
         Debug.Assert(str.type == TT.STRING && str.StringValue != null);
         value = str.StringValue;
         fakeID = ID.From(ToString());
      }

      private ID fakeID;
      public ID Id => fakeID;

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
      public string AsDecoratedCDL2String(EmitterBase emitter) => $"\"{EscapedCDL2(value)}\"".Decorate(emitter,SE.String);
      override public string ToString() => $"\"{value}\"";
   }
   public class LIST : CDL2Object, IMacroElement {
      [JsonInclude] public readonly ID lwb;
      [JsonInclude] public readonly ID upb;

      public LIST(ID id,Section section,ID lwb,ID upb) : base(id,section,null) {
         this.lwb = lwb;
         this.upb = upb;
         SE = SE.List;
      }
      override public string ToString() => $"LIST {Id}({lwb}:{upb})";
   }
   public class Var : CDL2Object, IFailureProtected, IMacroElement, IActualArg, ITrackedVar {
      public Var(ID id, Section section) : base(id, section, null) => SE = SE.Var;

      override public string ToString() => $"VAR {Id.Name}";
   }
   public class Const : CDL2Object, 
         IConstElement, IMacroElement, IProvidable, IExportable, IActualArg, IImportable {
      [JsonInclude]
      public readonly List<IConstElement> elements = [];  // Will contain ids (const, var, list) and strings, integers, floats

      public Const(ID id,Section section) : base(id,section,null) => SE = SE.Const;
   }

   public class ImportedConst(ID id,Section section) : Const(id,section), IImportable {
      public override string ToString() => "IMPORTED " + base.ToString();
      public override bool IsImported => true;
   }



   /// <summary>
   /// Represents a formal argument in an algorithm.
   /// It is just an ID with annotations. An arg is considered to be equal to another arg or ID if the names are the same.
   /// </summary>
   public class Affix : NamedElement, IFailureProtected, IMacroElement, ITrackedVar  {
      public static readonly Affix Default = new (ID.AnonID,AffixDir.NONE,AffixType.std);
      [JsonInclude] public readonly AffixDir affixDir;
      [JsonInclude] public readonly AffixType affixType;
      [JsonInclude] public Algorithm? ContainingAlgorithm { get; set; } = null;


      /// <param Id="Id"></param>
      /// <param Id="dir"></param>
      /// <param Id="type"></param>
      public Affix(ID id,AffixDir dir,AffixType type) : base(id) {
         affixDir = dir;
         affixType = type;
      }

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

   public class Local(ID id) : NamedElement(id), IMacroElement, IActualArg, ITrackedVar, IParameter {
      [JsonInclude] public Algorithm? ContainingAlgorithm { get; set; } = null;
      public static readonly Local Default = new(ID.AnonID);
      override public string ToString() => $"-{Id.Name}";
   }

   public class Undeclared : CDL2Object {
      public readonly static Undeclared Instance = new();

      private Undeclared() : base(ID.AnonID) => SE = SE.Other;
   }

}
