// Ignore Spelling: Transput CDL abstr ext inv ludes lude lwb upb FQN

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
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
using System.Xml.Linq;

using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CDL2v1 {
   // Marker interfaces to allow lists to be composed of permissible elements.
   public interface IMacroElement { }
   public interface IConstElement { }
   public interface IInterfaceElement { }
   public interface IProvidedElement : IInterfaceElement { }
   public interface IRequiredElement : IInterfaceElement { }
   public interface  IImpexElement : IInterfaceElement { }     // ImpexElement is an interface for all elements that can be imported or exported (Const & Algorithm)
   public interface IActualArg {
      ID id { get; }
   }
   public interface INamedElement {
      bool HasCommentOrNote { get; }
      bool IsSynthetic { get; }
      Container? Parent { get; set; }      // null for the Program and Modules.
      string? Comments { get; set; }
      Notes Notes { get; set; }
      string FQDN();
   }
   /// <summary>
   /// Any CDL2 object: Algorithm, Const, Var, LIST.
   /// </summary>
   public interface ICDL2Object : INamedElement {
      public SE SE { get; }
   }
   /// <summary>
   /// Any CDL2 data object: Const, Var, LIST.
   /// </summary>
   public interface ICDL2DataObject : ICDL2Object { }
   /// <summary>
   /// Any CDL2 object that is local to a SectionById: Algorithm, Var, LIST.
   /// </summary>
   public interface ILocalCDL2Object : ICDL2Object { }
   /// <summary>
   /// Any CDL2 data object that is local to a SectionById: Var, LIST.
   /// </summary>
   public interface ILocalCDL2DataObject : ILocalCDL2Object, ICDL2DataObject { }
   /// <summary>
   /// Represents a failure protected objects: output and transput affixes and variables. This means that if used in an algorithm that fails,
   /// any changes to the object is undone.
   /// </summary>
   /// <param name="id"></param>
   public interface IFailureProtected : IActualArg { }

   /// <summary>
   /// Notes can be attached to NamedElements. The primary use is annotate objects with error/warning/info messages.
   /// The PrettyPrinter will these with the object as a comment starting with the <see cref="Marker"/>.
   /// If LexAnal (<see cref="Token.TryCreateToken(ref string, out Token)"/>) observes such comments, it will not preserve them.
   /// </summary>
   /// <param name="type"></param>
   /// <param name="text"></param>
   /// <param name="number"></param>
   public class Note {
      public readonly NoteType Type;
      public readonly string Text;
      public readonly int Number;
      public readonly string PhaseName;
      public NamedElement? Owner = null;
      public Note(NoteType type, int number, string text, string phaseName = "") { 
         Type = type;
         Text = text;
         Number = number;
         PhaseName = phaseName;
      }
      public Note(Note template, string phaseName, NamedElement owner, params object[] args)
         : this(template.Type, template.Number, string.Format(template.Text, [.. args.Select(arg => arg is Affix aff ? aff.id : arg is Local loc ? loc.id : arg)]), phaseName) => Owner = owner;

      public static readonly string Marker = " >>> ";

      public static readonly Note Defect                            = new(NoteType.Error  , 001, "Procedure has defect (has an effect tough tough declared as {0})");
      public static readonly Note Effect                            = new(NoteType.Error  , 002, "Procedure has effect tough declared as {0}");
      public static readonly Note CanFail                           = new(NoteType.Error  , 003, "Procedure can fail tough declared as {0}");
      public static readonly Note CannotFail                        = new(NoteType.Error  , 004, "Procedure can not fail tough declared as {0}");
      public static readonly Note LabelNotFound                     = new(NoteType.Error  , 005, "*{0}: label not found in Procedure");
      public static readonly Note DuplicateLabel                    = new(NoteType.Error  , 006, "{0}: duplicate label in Procedure");
      public static readonly Note IllegalFailOperator               = new(NoteType.Error  , 007, "Procedure has FAIL operator (-) tough declared as {0}");
      public static readonly Note UndeclaredAlgorithmCall           = new(NoteType.Error  , 008, "Call of undeclared Algorithm {0}");
      public static readonly Note ArgumentCountMismatch             = new(NoteType.Error  , 009, "Argument count mismatch. {0} has {1} affixes, but called with {2}");
      public static readonly Note WrongTypeOfargumentForStringAffix = new(NoteType.Error  , 010, "Only a literal string or a CONST can be passed to a string affix for {0}");
      public static readonly Note UninitializedOutputPassedAsInput  = new(NoteType.Error  , 011, "Output affix {0} that has not been set passed as input or transput in {1}");
      public static readonly Note InputAffixPassedToOutput          = new(NoteType.Error  , 012, "Input affix {0} passed to output or transput in {0}");
      public static readonly Note ConstPassedToOutput               = new(NoteType.Error  , 013, "CONST {0} passed to output or transput in {0}");
      public static readonly Note ConstPassedToTransput             = new(NoteType.Error  , 014, "CONST {0} passed to transput in {1}");      
      public static readonly Note OutputAffixNotAssigned            = new(NoteType.Error  , 015, "Output affix {0} that has not been set passed as input in {1}");
      public static readonly Note LocalNotAssigned                  = new(NoteType.Error  , 016, "Local {0} that has not been set passed as input in {1}");
      public static readonly Note LocalOverwritten                  = new(NoteType.Error  , 017, "Local {0} whose value has not been read passed to output in {1}");
      public static readonly Note AlgorithmStubNotImported          = new(NoteType.Error  , 018, "Algorithm stub is not imported");
      public static readonly Note ConstantStubNotImported           = new(NoteType.Error  , 019, "Constant stub is not imported");
      public static readonly Note ImportedAlgorithmHasBody          = new(NoteType.Error  , 020, "Imported Algorithm has body");
      public static readonly Note ImportedConstantHasBody           = new(NoteType.Error  , 021, "Imported Constant has body");
      public static readonly Note InvalidInputArg                   = new(NoteType.Error  , 022, "Only CONST, VAR, AFFIX (input or transput) or LOCAL may be passed to an input affix {0}");
      public static readonly Note InvalidOutputArg                  = new(NoteType.Error  , 023, "Only VAR, AFFIX (output or transput) or LOCAL may be passed to an output affix {0}");
      public static readonly Note InvalidStringArg                  = new(NoteType.Error  , 024, "Only CONST, literal string, or string affix can be passed to {1}, not {0}");
      public static readonly Note InvalidTransputArg                = new(NoteType.Error  , 025, "Only VAR, AFFIX (output or transput) or LOCAL may be passed to a transput affix {0}");
      public static readonly Note InvalidArgumentType               = new(NoteType.Error  , 026, "Argument must reference an affix, const or var, not {0}");
      public static readonly Note UnresolvedArgument                = new(NoteType.Error  , 027, "Argument could not be resolved {0}");
      public static readonly Note DuplicateExport                   = new(NoteType.Error,   029, "{0} exported by {1} was also exported by {2}");
      public static readonly Note MissingImport                     = new(NoteType.Error,   030, "{0} is imported but was not exported by any program part");
      public static readonly Note ImpexMismatch                     = new(NoteType.Error,   031, "Import {0} does not match export {1} ({2})");

      public static readonly Note NoEffect                          = new(NoteType.Warning, 101, "Procedure has no effect tough is declared as {0}");
      public static readonly Note OutputAffixOverwritten            = new(NoteType.Warning, 102, "Output affix {0} whose value has not been read passed to output in {1}");
      public static readonly Note TransputAffixOverwritten          = new(NoteType.Warning, 103, "Transput affix (0} whose value has not been read passed to output in {1}");
      public static readonly Note VariableNotRead                   = new(NoteType.Warning, 104, "Variable {0} was assigned a value which was never read");
      public static readonly Note VariableNotWritten                = new(NoteType.Warning, 105, "Variable {0} was read, but never assigned a value");
      public static readonly Note VariableMayNotHaveBeenRead        = new(NoteType.Warning, 106, "Variable {0} was assigned a value, but may not have been read");

      public static readonly Note AffixNotRefeenced                 = new(NoteType.Info   , 201, "Affix {0} was not used in procedure {1}");
      public static readonly Note LocalNotReferenced                = new(NoteType.Info   , 202, "Local {0} was not used in procedure {1}");
      public static readonly Note UnreferenceObject                 = new(NoteType.Info   , 203, "Object is defined but not used in program");


      public override string ToString() => $"{Type} {Number}: {Text}";
   }

   public class Notes : List<Note> { }

   /// <summary>
   /// Base class for all elements that have names in the syntax tree.
   /// </summary>
   /// <param id="id"></param>
   [Serializable]
   public class NamedElement(ID id,bool synthetic = false) : INamedElement {
      public ID id { get; } = id;
      /// <summary>
      /// True if the object is synthetic, i.e., generated by the parser.
      /// Objects that can be synthetic:
      ///  - Procedures: generated for SectionById ludes.
      ///  - Groups: indicating that their label is generated.
      /// </summary>
      public bool IsSynthetic { get; } = synthetic;
      public Container? Parent { get; set; }      // null for the Program and Modules.

      public Section? Section => this is Section ? (Section)this : (Parent as Section)!;
      public Module? Module => Section?.Parent?.Parent as Module;

      /// <summary>
      /// Contains the objects that reference this object.
      /// What may be here depends on the type of this object.
      ///  - Const: Algorithms and Consts.
      ///  - Vars:  Algorithms.
      ///  - LISTs: Macros.
      ///  - Algorithms: Algorithms.
      /// </summary>
      public Set<ICDL2Object> Refeences = [];

      override public string ToString() => $"{TypeShortName} {id.Name}";
      public virtual string TypeShortName => GetType().Name.ToUpper()[..3];
      public string? Comments { get; set; }
      public Notes Notes { get; set; } = [];
      public bool HasCommentOrNote => Comments != null || Notes.Count > 0;
      public void AddNote(string phase, Note note, params object[] insertions) {
         Notes.Add(new Note(note, phase, this, insertions));
         Database.Instance.ElementsWithNotes.Add(this);
      }
      public void AddNotes(string phase, Notes? notes) => notes?.ForEach(note => AddNote(phase, note));

      /// <summary>
      /// Fully qualified name as Module_Layer_Section_Object.
      /// Separator can be specified. Default is "_".
      /// </summary>
      /// <param name="separator"></param>
      /// <returns></returns>
      public string FQN(string separator = "_",string prefix = "",string replacement = "",bool camelCase = false,bool literalObjectName = false) {
         string sectionName = Parent!.id.Name.AsIdentifier(prefix,replacement,camelCase);
         string layerName = Parent!.Parent!.id.Name.AsIdentifier(prefix,replacement,camelCase);
         string moduleName = Parent!.Parent!.Parent!.id.Name.AsIdentifier(prefix,replacement,camelCase);
         string objectName = id.Name.AsIdentifier(prefix,replacement,camelCase,literalObjectName);
         return $"{moduleName}{separator}{layerName}{separator}{sectionName}{(IsSynthetic?separator+separator:separator)}{objectName}";
      }
      /// <summary>
      /// Element display name, i.e. MOD mod LAY lay SEC sec obj.
      /// </summary>
      /// <returns></returns>
      public string FQDN() {
         string sectionName = Parent!.ToString();
         string layerName   = Parent!.Parent!.ToString();
         string moduleName  = Parent!.Parent!.Parent!.ToString();
         string objectName  = ToString();
         return $"{moduleName} {layerName} {sectionName} {objectName}";
      }
   }

   /// <summary>
   /// Base class for all elements that can contain other elements, i.e., the program and modules, layers, sections.
   /// </summary>
   [Serializable]
   public abstract class Container : NamedElement {
      /// <summary>
      /// The Children of the container. Layers are ordered, hence the list.
      /// </summary>
      [JsonInclude]
      public List<Container> Children = [];
      /// <param id="id"></param>
      public Container(ID id,string? comments,Notes? notes) : base(id) {
         Comments = comments;
         AddNotes("Parser", notes);
      }

      public Container(ID id,Container? parent,string? comments = null,Notes? notes = null) : this(id,comments,notes) { 
         Parent = parent;
         ContainerName = $"{Parent?.ContainerName ?? ""} {TypeShortName} {id.Name}".Trim();
         if (Parent != null && (bool)(Parent.Children.Contains(this))) {
            Logger.ReportError($"{ContainerName} is already a child of {Parent.ContainerName}");
         } else {
            this.Parent?.Children.Add(this);
         }
      }

      // The Ludes are stored in a dictionary with the reserved word as the key. The values are lists of IDs.
      // SectionById Ludes will be generated as Procedures and given the id of the lude type (which are not legal as a CDL2 id).
      [JsonInclude]
      public Dictionary<RW,List<ID>> Ludes { get; } = new() {
         { RW.PRELUDE,[] },
         { RW.ROOT,[] },
         { RW.POSTLUDE,[] }
      };
      public static readonly List<RW> LudeTypes = [RW.PRELUDE, RW.ROOT, RW.POSTLUDE];

      public Container? Child(ID id) => Children.FirstOrDefault(child => child.id == id);

      /// <summary>
      /// Sets the LudeParser action for the container. The default is to do nothing.
      /// </summary>
      public Action<Parser,RW,Container> LudeParser = (parser,ludeType,container) => { };

      /// <summary>
      /// The short id of the container with its type. Used in the ToString method.
      /// </summary>
      public string ContainerName = string.Empty;
   }

   /// <summary>
   /// Represents a program in the syntax tree.
   /// </summary>
   [Serializable]
   public class Program : Container {
      override public string TypeShortName => "PROG";
      /// <summary>
      /// Get the modules that have the given lude type.
      /// </summary>
      /// <param id="ludeType"></param>
      /// <returns>A collection of modules that are in the lude of the given type.</returns>
      public IEnumerable<Module> Lude(RW ludeType) => this.Ludes[ludeType].Select(id => Database.Instance.Modules[id]);

      public Set<ID> Parts { get; } = [];
      /// <summary>
      /// Program Ludes are a list of module IDs.
      /// </summary>
      /// <param id="id"></param>
      public Program(ID id,string? comments,Notes notes) : base(id,null,comments,notes) {
         LudeParser = Parser.ParseLudeOfIDs;
         Database.Instance.FirstProgram ??= this;
      }
   }

   /// <summary>
   /// Represents a module in the syntax tree.
   /// </summary>
   /// <param id="id"></param>
   [Serializable]
   public class Module : Container {
      public readonly Dictionary<ID,Section> imports = [];        // Imports are specified in sections, but are propagated up the module level.
      public readonly Dictionary<ID,Section> exports = [];        // Exports are specified in sections, but are propagated up the module level.
      /// <summary>
      /// Resolved imports are the imports that have been resolved to their definitions by the semantic analyzer.
      /// Reconstiotuted each time the semantic analyzer is run.
      /// </summary>
      public readonly Dictionary<ID, IImpexElement> resolvedImports = [];

      /// <summary>
      /// Module Ludes are a list of container IDs.
      /// </summary>
      /// <param id="id"></param>
      public Module(ID id,string? comments,Notes notes) : base(id,null,comments,notes) {
         LudeParser = Parser.ParseLudeOfIDs;
         Comments = comments;
      }

      public Section? SectionById(ID id) {
         foreach (Container layer in Children) {
            foreach (Container section in layer.Children) {
               if (id == section.id) return (Section)section;
            }
         }
         return null;
      }
   }

   /// <summary>
   /// Represents a layer in the syntax tree.
   /// Notice that layers don't have Ludes.
   /// </summary>
   /// <param id="id"></param>
   /// <param id="module"></param>
   /// <param PhaseName="ancestor">The layer from which this layer is extended. Null for the lowest layer.</param>
   [Serializable]
   public class Layer(ID id,Module module,Layer? ancestor,string? comments = null,Notes? notes=null) : Container(id,module,comments,notes) {
      public readonly Layer? Ancestor = ancestor;
      public readonly Dictionary<ID,Section> ext = [];
      public readonly Dictionary<ID,Section> abstr = [];

   }

   /// <summary>
   /// Represents a container in the syntax tree.
   /// </summary>
   /// <param id="id"></param>
   /// <param id="layer"></param>
   [Serializable]
   public class Section : Container {
      /// <summary>
      /// The interfaces.
      /// </summary>
      public readonly Set<ID> ext = [];
      public readonly Set<ID> abstr = [];
      public readonly Set<ID> inv = [];
      public readonly Set<ID> export = [];
      public readonly Set<ID> import = [];

      /// <summary>
      /// Hold the declarations of the SectionById. The key is the ID of the declaration.
      /// </summary>
      public readonly Dictionary<ID,ICDL2Object> declarations = [];
      /// <summary>
      /// Acts a cache for <see cref="TryGetDeclaration{T}(ID,out T)"/>."/>
      /// </summary>
      public readonly Dictionary<ID,ICDL2Object> resolvedDeclarations = [];
      public IEnumerable<Const> Constants => declarations.Values.OfType<Const>();
      public IEnumerable<Var> Variables => declarations.Values.OfType<Var>();
      public IEnumerable<LIST> Lists => declarations.Values.OfType<LIST>();
      public IEnumerable<Macro> Macros => declarations.Values.OfType<Macro>();
      public IEnumerable<Procedure> Procedures => declarations.Values.OfType<Procedure>();

      /// <summary>
      /// The non-synthetic procedures.
      /// Synthetic procedures are generated by the parser for container ludes.
      /// </summary>
      public IEnumerable<Procedure> NonSyntheticProcedures => Procedures.Where(proc => !proc.IsSynthetic);
      public IEnumerable<Procedure> SyntheticProcedures => Procedures.Where(proc => proc.IsSynthetic);

      /// <summary>
      /// Sections have Ludes each of which contains the ID of an publicly generated CODE FUNCTION or ACTION which consist of a single alternative.
      /// TODO: Ensure that the generated CODE is correctly typed and that only ACTIONs and/or FUNCTIONs are called.
      /// </summary>
      /// <param id="id"></param>
      /// <param id="layer"></param>
      public Section(ID id,Layer layer,string? comments = null,Notes? notes = null) : base(id,layer,comments,notes) => LudeParser = Parser.ParseLudeOfCalls;

      public static Type[] ProvidedElementImplementors;
      static Section() => ProvidedElementImplementors = [.. Extensions.GetImplementorsOfInterface<IProvidedElement>()];

      /// <summary>
      /// Get the declaration with the given ID. If the declaration is not found in this SectionById, it must be an inv and is looked for in the
      /// containing layer's extensions and the previous layer's abstractions.
      /// Assumes that semantic analysis has been done and that the declaration is found.
      /// </summary>
      /// <param name="id"></param>
      /// <typeparam name="T">The type of the requested object which must be an ICDL2Object.</typeparam>
      /// <returns>The declaration if found. </returns>
      /// 
      public bool TryGetDeclaration<T>(ID id,out T? declaration) where T : ICDL2Object {
         if (resolvedDeclarations.TryGetValue(id,out ICDL2Object? cached) && cached is T resolved) {
            declaration = resolved;
            return true; // Found in the cache
         } else if (TryGetLocalDeclaration(id,out ILocalCDL2Object? obj) && obj is T local) {
            declaration = local;
            resolvedDeclarations[id] = local; // Cache the result to avoid checking in both declaration and resolvedDeclaration.
            return true; // Found locally
         } else if (inv.Contains(id)) {
            Debug.Assert(Parent != null && Parent is Layer,$"Parent of {this} is null or not a Layer");
            Layer layer = (Layer)Parent;
            if (layer.ext.TryGetValue(id,out Section? declaringSection) && declaringSection.declarations[id] is T extended) {
               declaration = extended;
               resolvedDeclarations[id] = extended;
               return true;
            } else if (layer.Ancestor != null && layer.Ancestor.abstr.TryGetValue(id,out declaringSection) && declaringSection.declarations[id] is T abstracted) {
               declaration = abstracted;
               resolvedDeclarations[id] = abstracted;
               return true;
            }
         }
         declaration = default;
         return false;
      }
      public bool TryGetLocalDeclaration<T>(ID id,out T? declaration) where T : ILocalCDL2Object {
         if (declarations.TryGetValue(id,out ICDL2Object? obj) && obj is T local) {
            declaration = local;
            return true;
         }
         declaration = default;
         return false;
      }
   }

   // ---------------------------------------------------------------------------------------------------

   /// <summary>
   /// Represents an algorithm in the syntax tree. Concretely it is either a Macro or Procedure. 
   /// </summary>
   [Serializable]
   public class DeclaredCDL2Object : NamedElement {
      public DeclaredCDL2Object(ID id,Section section,string? comments,bool synthetic = false) : base(id,synthetic) {
         Parent = section;
         Comments = comments;
      }
   }
   /// <summary>
   /// Represents the common properties of Algorithms (Macros and Procedures).
   /// </summary>
   [Serializable]
   public abstract class Algorithm : DeclaredCDL2Object, IProvidedElement, ILocalCDL2Object, IImpexElement {
      // public readonly SectionById container = container;
      public          RW algorithmType;            // One of FUNCTION, ACTION, TEST or PREDICATE (reservedWordValue will never be null)
      public readonly TT bodyType;                 // One of : or := (for CODE only) and = or =: (for MACRO only)
      public readonly List<Affix> affixes;         // The affixes of this algorithm. A List because they are ordered.       
      public readonly Set<Local> locals;           // The declarations variables of this algorithm.

      public SE SE => SE.AlgorithmName;
      public Algorithm(ID id,List<Affix> affixes,Set<Local> locals,Token algorithmType,TT bodyType,Section section,bool synthetic = false) 
            : base(id,section,algorithmType.Comments,synthetic) {
         this.affixes = affixes;
         this.locals = locals;
         this.algorithmType = algorithmType.reservedWordValue ?? RW.FUNCTION;
         this.bodyType = bodyType;
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
      public string AlgorithmName => $"{algorithmType} {id}";

      public bool CanFail => algorithmType == RW.TEST || algorithmType == RW.PREDICATE;
      public bool AlwaysSucceeds => !CanFail;
      public bool HasEffect => algorithmType == RW.PREDICATE || algorithmType == RW.ACTION;
      public bool HasNoEffect => !HasEffect;
      public bool NeedsFinalization => CanFail && (affixes.Any(affix => affix.IsOutput) || GetReferencedVariables().Any());
      public bool IsInlineMacro => bodyType == TT.MACROBODY;
      /// <summary>
      /// Check if this is a conditional compilation flag. That is, the body consists of a single fail respectively succeed operator.
      /// </summary>
      /// <param name="group"></param>
      /// <returns></returns>
      public virtual bool IsConditionalCompilationOff => false;
      public virtual bool IsConditionalCompilationOn => false;
      public bool IsConditionalCompilation(bool? on=null) => on is null ? IsConditionalCompilationOn || IsConditionalCompilationOff : (bool)on ? IsConditionalCompilationOn : IsConditionalCompilationOff;
      public bool TryGetAffix(ID id,out Affix affix) => (affix = affixes.FirstOrDefault(affix => affix.id == id,Affix.Default)) != Affix.Default;
      public bool TryGetLocal(ID id,out Local local) => (local = locals.FirstOrDefault(local => local.id == id,Local.Default)) != Local.Default;

      public override string ToString() {
         StringBuilder buffer = new();
         buffer.Append($"{TypeShortName} {id.Name}");
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

      //         if (SectionById.inv.Contains(id)) { // More complicated then declarations. Need to find the container the algorithm is abstracted or extended from
      //                                         // Examine siblings to find the container the algorithm is extended from.
      //            Layer currentLayer = SectionById.Parent as Layer;
      //            Debug.Assert(currentLayer != null);
      //            foreach (SectionById sibling in currentLayer.Children.Where(sec => sec != SectionById).Cast<SectionById>()) {
      //               if (sibling.ext.Contains(id)) {
      //                  if (sibling.import.Contains(id)) return new SA(Prefix1: AS.Ext,Prefix2: AS.ImportExport);
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
      //                     if (ancestor.abstr.Contains(id)) {
      //                        if (ancestor.import.Contains(id)) return new SA(Prefix1: AS.Abstr,Prefix2: AS.ImportExport);
      //                        return new SA(Prefix1: AS.Abstr);
      //                     }
      //                  }
      //               }
      //               return new SA(Prefix1: AS.Inv);   // Only possible in a partially analyzed context.
      //            } else { // declarations
      //               bool exported = SectionById.export.Contains(id);
      //               bool imported = SectionById.import.Contains(id);
      //               bool abstr = SectionById.abstr.Contains(id);
      //               bool ext = SectionById.ext.Contains(id);
      //               if (imported) {
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
   /// An imported algorithm is a reference to an algorithm in another module. Thus it has only a header and no body.
   /// </summary>
   [Serializable]
   public class ImportedAlgorithm(ID id,List<Affix> formals,Token algorithmType,Section section) : Algorithm(id,formals,[],algorithmType,TT.NOBODY,section) {
      public override IEnumerable<Var> GetReferencedVariables() => [];
      public override string ToString() => "IMPORTED "+ base.ToString();
   }

   /// <summary>
   /// Represents a macro in the syntax tree.
   /// </summary>
   /// <param id="id"></param>
   /// <param id="affixes"></param>
   /// <param id="locals"></param>
   /// <param id="algorithmType"></param>
   /// <param id="bodyType"></param>
   /// <param id="container"></param>
   [Serializable]
   public class Macro(ID id,List<Affix> formals,Set<Local> locals,Token algorithmType,TT bodyType,Section section) : Algorithm(id,formals,locals,algorithmType,bodyType,section) {
      public List<IMacroElement> elements = [];

      public override IEnumerable<Var> GetReferencedVariables() => elements.OfType<Var>();
   }
   /// <summary>
   /// Represents a code in the syntax tree.
   /// </summary>
   /// <param id="id"></param>
   /// <param id="affixes"></param>
   /// <param id="locals"></param>
   /// <param id="algorithmType"></param>
   /// <param id="bodyType"></param>
   /// <param id="SectionById"></param>
   [Serializable]
   public class Procedure(ID id,List<Affix> formals,Set<Local> locals,Token algorithmType,TT bodyType,Section section,bool synthetic = false) 
         : Algorithm(id,formals,locals,algorithmType,bodyType,section,synthetic) {
      public Group group = new(id,[],null,synthetic: false);
      /// <summary>
      /// True if the procedure is an Action or Function that has only a single alternative (which is a sequence of calls none of which can fail ... which will be guarenteed by the sematic analyzer)
      /// </summary>
      public bool IsVerySimple => AlwaysSucceeds && group.alternatives.Count == 1 && HasNoGroups;
      /// <summary>
      /// Can have alternatives, but there are mo groups except for the primary one.
      /// It can also fail.
      /// </summary>
      public bool IsSimple => HasNoGroups && HasNoRepeat;
      public PBT ProcedureBodyType => IsVerySimple ? PBT.VerySimple : IsSimple ? PBT.Simple : PBT.General;

      /// <summary>
      /// Check if this is a conditional compilation flag. That is, the body consists of a single fail respectively succeed operator.
      /// TODO: This is the intial version. It will be refined to check that all calls in a procedure are to other procedures that are also conditional compilation flags.
      /// </summary>
      /// <returns></returns>
      public override bool IsConditionalCompilationOff => CanFail && group.alternatives.Count == 1 && group.alternatives[0].calls.Count == 0 && group.alternatives[0].lastCall.type == LCT.Fail;
      public override bool IsConditionalCompilationOn => CanFail && group.alternatives.Count == 1 && group.alternatives[0].calls.Count == 0 && group.alternatives[0].lastCall.type == LCT.Succeed;

      /// <summary>
      /// The procedure has repeats.
      /// </summary>
      public bool HasRepeat => group.HasAnAnonymousRepeat();
      public bool HasNoRepeat => ! HasRepeat;

      public bool NeedsWrapper => repeatsProcedure || NeedsFinalization || HasRepeat;

      /// <summary>
      /// None of the alternatives in the primary group ends with a group.
      /// </summary>
      public bool HasNoGroups {
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
      public bool repeatsProcedure = false;

      public Procedure(RW ludeType,Section section) : this(ID.From(ludeType),[],[],Token.ACTIONToken,TT.CODEBODY,section,true) { } // Used for container Ludes which are parameterless actions with no locals.
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

    
   }

   [Serializable]
   public class Call(ID id,Procedure containingProc,bool builtin=false) {
      public readonly ID id = id;
      public readonly List<IActualArg> args = [];
      public readonly Procedure ContainingProc = containingProc;
      /// <summary>
      /// Set for compiler procedures that are evaluated at code generation time.
      /// </summary>
      public readonly bool IsBuiltin = builtin;

      public bool IsConditionalCompilationOff => IsConditionalCompilation(on: false);
      public bool IsConditionalCompilationOn  => IsConditionalCompilation(on: true);
      private bool IsConditionalCompilation(bool on) {
         if (Called != null) {
            return called!.IsConditionalCompilation(on);
         } else if (IsBuiltin && Builtin.IsTest(this)) {
            return on == Builtin.EvalTest(this);
         } else {
            return !on;
         }
      }

      override public string ToString() => $"{(IsBuiltin?RW.BUILTIN+" ":"")}{id.Name}{(args.Count>0?"+":"")}{string.Join("+",args.Select(arg=>arg.id))}";
      public bool TryGetAffix(ID id,out Affix affix) => ContainingProc.TryGetAffix(id,out affix);
      public bool TryGetLocal(ID id,out Local local) => ContainingProc.TryGetLocal(id,out local);
      private Algorithm? called = null;
      public Algorithm? Called {
         get {
            if (called == null && TryGetCalled(out Algorithm? calledByMe)) called = calledByMe;
            //Debug.Assert(called != null,$"{this} was unable to resolve the called algorithm");
            return called;
         }
      }
      public bool TryGetCalled(out Algorithm? called) {
         Debug.Assert(ContainingProc != null,$"{this} has null ContainingProc or its Parent is not a SectionById ");
         if (ContainingProc.Section.TryGetDeclaration(id,out called)) return true;
         called = null;
         return false;
      }
      public bool CanFail => Called?.CanFail ?? true;
      public bool AlwaysSucceeds => Called?.AlwaysSucceeds ?? false;
      public bool HasEffect => Called?.HasEffect ?? false;
      public bool HasNoEffect => Called?.HasNoEffect ?? false;
   }
   /// <summary>
   /// The last element(in an alternative) can be:
   /// Standard - a normal algorithm call which is the last item in the alternative's call list.
   /// Success, Fail, Abort - i.e., +, -, or?.
   /// Repeat - * with a reference to the group that is repeated possibly using the label
   /// Group - a nested group.
   /// </summary>
   /// <param id="type"></param>   
   [Serializable]
   public class LastCall(LCT type) {

      public readonly LCT type = type;
      public readonly Group? group;
      public readonly Call? call;
      public readonly ID? label = ID.AnonID;

      public LastCall(Call call) : this(LCT.Standard) => this.call = call;
      public LastCall(Group group) : this(LCT.Group) => this.group = group;
      public LastCall(ID? label) : this(LCT.Repeat) => this.label = label;

      public bool TryGetCalled(out Algorithm? called) {
         if (type == LCT.Standard && call!.ContainingProc.Section.TryGetDeclaration(call.id,out called)) return true;
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
   [Serializable]
   public class Alternative(List<Call> calls,LastCall lastCall,Notes notes) {
      public readonly List<Call> calls = calls;
      public readonly LastCall lastCall = lastCall;
      public readonly Notes Notes = notes;
      public bool IsConditionalOff = false;

      public bool CanFail => calls.Any(call => call.CanFail) || (lastCall.type == LCT.Standard && lastCall.call!.CanFail);
      private Call? FirstCall() {
         if (calls.Count > 0) return calls[0];
         if (lastCall.type == LCT.Standard) return lastCall.call;
         return null;
      }
      /// <summary>
      /// True if the alternative terminates the algorithm, i.e., its last call is a fail or abort.
      /// No need to check for succeed becasue that is just normal alternative completion.
      /// </summary>
      public bool Terminates => lastCall.type == LCT.Fail || lastCall.type == LCT.Abort;
      public bool IsConditionalCompilationOn => FirstCall() is Call firstCall && firstCall.IsConditionalCompilationOn;
      public bool IsConditionalCompilationOff => FirstCall() is Call firstCall && firstCall.IsConditionalCompilationOff;
   }
   // Note that the id in this case is the label.
   [Serializable]
   public class Group(ID? label,List<Alternative> alternatives,Group? parent,bool synthetic) 
         : NamedElement(synthetic ? Database.NextGroupLabel : label!,synthetic:synthetic) {
      public List<Alternative> alternatives = alternatives;
      public new readonly Group? Parent = parent;

      public bool HasAnonymousRepeat => HasAnAnonymousRepeat();
      public bool HasNoAnonymousRepeat => ! HasAnonymousRepeat;
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
      public override string ToString() => $"GRP {id.Name} {alternatives.Count.Plural("ALT")}";
   }


   [Serializable]
   public class INT : IConstElement, IMacroElement {
      public readonly long value;
      public INT(Token intToken) {
         Debug.Assert(intToken.type == TT.INT && intToken.intValue != null);
         value = (long)intToken.intValue;
      }
      override public string ToString() => value.ToString();
   }
   [Serializable]
   public class FLOAT : IConstElement, IMacroElement {
      public readonly double value;
      public FLOAT(Token floatToken) {
         Debug.Assert(floatToken.type == TT.FLOAT && floatToken.floatValue != null);
         value = (double)floatToken.floatValue;
      }
      override public string ToString() => value.ToString();
   }
   [Serializable]
   public class STRING : IMacroElement, IConstElement, IActualArg {
      public readonly string value;
      public STRING(Token str) {
         Debug.Assert(str.type == TT.STRING && str.StringValue != null);
         value = str.StringValue;
         fakeID = ID.From(ToString());
      }

      private ID fakeID;
      public ID id => fakeID;

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
   [Serializable]
   public class LIST(ID id,Section section,ID lwb,ID upb) : DeclaredCDL2Object(id,section,null), IMacroElement, ILocalCDL2DataObject {
      public readonly ID lwb = lwb;
      public readonly ID upb = upb;

      public SE SE => SE.List;

      override public string ToString() => $"LIST {id}({lwb}:{upb})";
   }
   [Serializable]
   public class Var(ID id,Section section) : DeclaredCDL2Object(id,section,null), IFailureProtected, IMacroElement, ILocalCDL2DataObject, IActualArg {
      public SE SE => SE.Var;

      override public string ToString() => $"VAR {id.Name}";
   }
   [Serializable]
   public class Const(ID id,Section section) : DeclaredCDL2Object(id,section,null), 
         IConstElement, IMacroElement, IProvidedElement, ICDL2DataObject, ILocalCDL2Object, ILocalCDL2DataObject, IActualArg, IImpexElement {
      public SE SE => SE.Const;
      public readonly List<IConstElement> elements = [];  // Will contain ids (const, var, list) and strings, integers, floats
   }

   [Serializable]
   public class ImportedConst(ID id,Section section) : Const(id,section) {
      public override string ToString() => "IMPORTED " + base.ToString();
   }



   /// <summary>
   /// Represents a formal argument in an algorithm.
   /// It is just an ID with annotations. An arg is considered to be equal to another arg or ID if the names are the same.
   /// </summary>
   /// <param id="id"></param>
   /// <param id="dir"></param>
   /// <param id="type"></param>
   [Serializable]
   public class Affix(ID id,AffixDir dir,AffixType type) : NamedElement(id), IFailureProtected, IMacroElement  {
      public static readonly Affix Default = new (ID.AnonID,AffixDir.NONE,AffixType.std);
      public readonly AffixDir affixDir = dir;
      public readonly AffixType affixType = type;

      public bool IsInput => affixDir == AffixDir.input || affixDir == AffixDir.transput;
      public bool IsInputOnly => affixDir == AffixDir.input;
      public bool IsOutput => affixDir == AffixDir.output || affixDir == AffixDir.transput;
      public bool IsOutputOnly => affixDir == AffixDir.output;
      public bool IsTransput => affixDir == AffixDir.transput;
      public bool IsString => affixType == AffixType.str;

     public SE SyntaxElement => IsString ? SE.StringAffix : IsTransput ? SE.TransputAffix : IsInput ? SE.InputAffix : SE.OutputAffix;

      public override bool Equals(object? obj) => obj is Affix affix && EqualityComparer<ID>.Default.Equals(id,affix.id);
      public override int GetHashCode() => HashCode.Combine(id);

      override public string ToString() => affixType == AffixType.std ? $"{(IsInput ? ">" : "")}{id}{(IsOutput ? ">" : "")}" : $"*{id}";

      public static bool operator ==(Affix? left,Affix? right) => EqualityComparer<Affix>.Default.Equals(left,right);
      public static bool operator !=(Affix? left,Affix? right) => !(left == right);
   }

   [Serializable]
   public class Local(ID id) : NamedElement(id), IMacroElement, IActualArg {
      public static readonly Local Default = new(ID.AnonID);

      override public string ToString() => $"-{id.Name}";
   }

   [Serializable]
   public class Undeclared() : NamedElement(ID.AnonID), ICDL2Object {
      public SE SE => SE.Other;
      public readonly static Undeclared Instance = new();
   }

}
