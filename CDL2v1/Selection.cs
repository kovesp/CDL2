// <auto-gen>
//=======================================================================
// <copyright file="Selection.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-06-26</creation-date>
// 
// <summary>
//   Parses a selection string from the command line and returns a list of selected items.
//   Also responsible for the Focus which selects a single object. A stack of focuses as well as bookmarks is maintained.
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

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CDL2v1 {

   /// <summary>
   /// Represents a selection of a single object.
   /// </summary>
   /// <remarks>
   /// Details of the different types used:
   /// <list type="bullet">
   ///   <item>Simple objects.
   ///      <list type="bullet">
   ///         <item>Program</item>
   ///         <item>Module</item>
   ///         <item>Layer</item>
   ///         <item>Section</item>
   ///         <item>Const</item>
   ///         <item>Var</item>
   ///         <item>List</item>
   ///         <item>Algorithm</item>
   ///      </list>
   ///   </item>
   ///   <item>Complex objects.
   ///      <list type="table">
   ///         <item><term>List</term> <description>Ordinal=-1: the whole List, =0: the LWB, =1: the UPB</description></item>
   ///         <item><term>Const</term> <description>Ordinal=-1: the whole Const, >=0: the index of the Const element</description></item>
   ///         <item><term>Macro</term> <description>Ordinal=-1: the whole Macro, >=0: the index of the Macro element</description></item>
   ///         <item><term>Alternative</term> <description>Ordinal>=0: idex of the alternative within the group</description></item>
   ///         <item><term>Call</term>
   ///            <description>
   ///               <list type="number">
   ///                  <item>Ordinal>=0: the index of the Call in the alternative</item>
   ///                  <item>Arg=-1: the call itself, Arg>=0: the index of the call argument.</item>
   ///               </list>
   ///            </description>
   ///         </item>
   ///         <item><term>Affix</term> <description>Ordinal>=0: the index of the Affix in the algorithm</description></item>      ///         
   ///      </list>
   ///   </item>
   /// </list>  
   /// </remarks>
   public class SingleSelection {
      [JsonConstructor]
      public SingleSelection() { }

      public SingleSelection(NamedElement? obj) => Object = obj;
      /// <summary>
      /// Creates a new SingleSelection.
      /// If obj is null, then a "tpe-only" selection is created; this is used by the next and previous commands.
      /// Otherwise the selection refers to a specific object.
      /// </summary>
      /// <param name="obj"></param>
      /// <param name="type"></param>
      public SingleSelection(NamedElement? obj,SelectorType type) {
         Object = obj;
         if (obj is not null) {
            ListType = type;
         } else {
            _kind = type;
         }
      }
      public SingleSelection(NamedElement? obj,SelectorType type,string name) {
         Object = obj;
         ListType = type;
         if (name.IsNotNullEmptyOrWhitespace) Id = new(name);
      }

      public static SingleSelection Empty => new();
      /// <summary>
      /// The Guid of a NamedElement in the selection.
      /// For "simple" objects (e.g., Vars, Layers) this uniquely identifies the object.
      /// For others, further identification is needed.
      /// </summary>
      [JsonInclude, JsonPropertyOrder(0)]
      public Guid ObjectGuid = Guid.Empty;
      [JsonIgnore]
      public NamedElement? Object {
         get => ObjectGuid == Guid.Empty ? null : NamedElement.From<NamedElement>(ObjectGuid);
         set {
            if (value == null) {
               ObjectGuid = Guid.Empty;
            } else {
               ObjectGuid = value.GUID;
            }
         }
      }

      // Used when the selection is only for a type, without any object
      [JsonInclude, JsonPropertyOrder(0)] public SelectorType _kind = SelectorType.INVALID;
      [JsonIgnore] public SelectorType Kind {
         get {
            if (ObjectGuid == Guid.Empty) return _kind;
            return Object?.FocusType ?? SelectorType.INVALID;  
         }
      }

      /// <summary>
      /// The ordinal of the selection sub element for types where this makes sense, -1 otherwise.
      /// </summary>
      [JsonInclude, JsonPropertyOrder(1)]
      public int Ordinal = -1;
      /// <summary>
      /// The ordinal of the argument of a call.
      /// </summary>
      [JsonInclude, JsonPropertyOrder(2)]
      public int Arg = -1;

      /// <summary>
      /// Specifies the selection type in certain cases.
      /// When the selection is a Section, this specifies that one of the interface lists is selected, the Ordinal is then the index within the list.
      /// It may be
      /// NONE when other then a PROGRAM, MODULE, or SECTION is selected, or when the whole object is the selection.
      /// PRELUDE, ROOT, POSTLUDE for a PROGRAM, MODULE or SECTION
      /// ABSTR, EXT, INV, IMPORT, EXPORT for a SECTION.
      /// </summary>
      [JsonInclude, JsonPropertyOrder(3)]
      public SelectorType ListType = SelectorType.INVALID;

      [JsonInclude, JsonPropertyOrder(4)] public ID Id = ID.AnonID;


      private static readonly Type[] NonFocusableTypes = [typeof(Affix),typeof(Local),typeof(Call)];
      [JsonIgnore]
      public bool IsFocusable => !NonFocusableTypes.Contains(Object?.GetType() ?? typeof(NamedElement));
      public override string ToString() => $"SingleSelection<{(Object is null ? Kind : Object)}{(ListType != SelectorType.INVALID ? " " + ListType : "")}{(Id.IsAnonymous ? "" : " " + Id)}>";
   }

   /// <summary>
   /// Represents the objects selected by a selector. A valid selector will always select at least one object.
   /// </summary>
   public partial class Selection : List<SingleSelection> {
      #region SelectionSegments
      /// ================================================================================================================
      /// <summary>
      /// SelectionSegments are used during the parsing of the selection string to identify the segments of the selection.
      /// </summary>
      private abstract class SelectionSegment {
         public SelectorType SegmentType => this is UnitSegment unit ? unit.Type : SelectorType.INVALID;
         public string SegmentName => this is NameSegment id ? id.Name : "";
         public int SegmentOffset => this is OffsetSegment offset ? offset.Offset : 0;
      }
      private class UnitSegment(SelectorType type) : SelectionSegment {
         public SelectorType Type { get; private set; } = type;
         public override string ToString() => Type.ToString();
      }
      private class NameSegment(string name) : SelectionSegment {
         public string Name { get; private set; } = name;
         public override string ToString() => Name.ToString();

      }
      private class OffsetSegment(string offset) : SelectionSegment {
         public int Offset { get; private set; } = int.Parse(offset.WithNoWhitespace);
         public override string ToString() => Offset.ToString("+#;-#;0");
      }

      /// <summary>
      /// Collects the segments into a list.
      /// </summary>
      /// <remarks>
      /// Construction of instance will guarantee that there is always an even number of elements in the list.
      /// The elements alternate between a Unit and a Name or Offset segment.
      /// The Index is normally 0, but can be set by the optional ": <index>" segment at the end of the selection string.
      /// </remarks>
      private class SelectionSegments : List<SelectionSegment> {
         public int Index = 0;
         public override string ToString() => "SelectionSegments<" + (this.Aggregate("",(a,b) => $"{a} {b}")).TrimStart() + (Index > 0 ? $" : {Index}>" : ">");
      }
      /// ================================================================================================================
      #endregion SelectionSegments

      public string ErrorMessage = "";
      public bool IsValid => ErrorMessage == string.Empty;
      public bool IsInvalid => ErrorMessage != string.Empty;

      public bool IsFocusable => Count > 0 && this[0].IsFocusable;

      /// <summary>
      /// Create a new empty selection.
      /// </summary>
      public Selection() : base() { }
      public Selection(SingleSelection selection) : base() => Add(selection);

      private static readonly List<SelectorType> ImportableFocusType = [SelectorType.CONST,SelectorType.ALGORITHM,SelectorType.MACRO,SelectorType.PROCEDURE,SelectorType.FUNCTION,SelectorType.ACTION,SelectorType.TEST,SelectorType.PREDICATE];
      /// <summary>
      /// Create a new selection from a selection string.
      /// </summary>
      /// <param name="obj">The object to select.</param>
      public Selection(string selectionString,bool typeOnly=false) : base() {
         if (string.IsNullOrWhiteSpace(selectionString)) return;
         selectionString = selectionString.Trim();


         bool isRooted = false;
         if (selectionString.StartsWith('^')) {
            isRooted = true; // The selection is rooted
            selectionString = selectionString[1..].Trim();
         }

         if (selectionString == string.Empty) {
            Add(SingleSelection.Empty);
            return;
         }

         SelectionSegments segments = [];
         if (!ParseSelectionSegments(selectionString,segments,out bool importedSeen,out bool fullSeen)) return;

         if (typeOnly && segments.Count == 2) {
            Add(new SingleSelection(null,segments[0].SegmentType));
            return;
         }

         IEnumerable<NamedElement> candidateObjects;
         IEnumerable<NamedElement> selectedObjects = [];

         if (!isRooted && segments[1].SegmentName == "" && Abbreviation<SelectorType>.AncestorFocusTypeOf(ancestor: segments[0].SegmentType,child: Focus.Current.FocusType)) {
            // The initial segment is just a type without a name, and the focus is on an object that is a descendant of that type. e.g. "Module" when the focus is on a Layer.
            candidateObjects = [Focus.Current.Object!.GetAncestorOfType(segments[0].SegmentType)];
         } else if (!isRooted && !Abbreviation<SelectorType>.Focusable(segments[0].SegmentType)) {
            Add(new SingleSelection(Focus.Current.Container,segments[0].SegmentType,segments[1].SegmentName));
            return;
         } else if (isRooted) {
            candidateObjects = Database.Instance.NamedElements.Values;
         } else if (!Abbreviation<SelectorType>.AncestorFocusTypeOf(ancestor: Focus.Current.FocusType,child: segments[0].SegmentType)) {
            candidateObjects = Focus.Current.Object?.AncestorContainer()?.DescendantElements() ?? Database.Instance.NamedElements.Values;
         } else {
            candidateObjects = Focus.Current.Object!.DescendantElements();
            // The selection is relative to the current focus. TODO: sub elements are being ignored
         }

         candidateObjects = candidateObjects.Where(obj=>obj.IsDeclared);

         if (!candidateObjects.Any()) {
            // Nothing matches
            ErrorMessage = "Info:No matches";
            return;
         }
         // Use the segments to successively narrow down the selection.
         SelectorType listType = ST.INVALID;
         for (int segNo = 0 ; segNo < segments.Count ; segNo += 2) {
            selectedObjects = NarrowSelectionByType(candidateObjects,selectedObjects,segments,segNo,isRooted,importedSeen,fullSeen,out listType,ref ErrorMessage);
            if (ErrorMessage != string.Empty) return;
            if (segNo < segments.Count - 2) candidateObjects = selectedObjects.SelectMany(e => e.DescendantElements());
         }

         if (segments.Index < 1) {
            // If the selectedObjects are all siblings, add them in sibling order, otherwise OrderedAsSiblings leaves the order unchanged.
            AddRange(selectedObjects.OrderedAsSiblings.Select(obj => new SingleSelection(obj,listType)));
         } else {
            Add(new SingleSelection(selectedObjects.ElementAt(Math.Min(segments.Index,segments.Count) - 1),listType));
         }
      }


      private IEnumerable<NamedElement> NarrowSelectionByType(IEnumerable<NamedElement> candidateObjects,IEnumerable<NamedElement> currentSelectedObjects,SelectionSegments segments,int segNo,
            bool isRooted,bool importedSeen,bool fullSeen,out ST listType,ref string errorMessage) {
         listType = ST.INVALID;
         IEnumerable<NamedElement> selectedObjects = [];

         string name = segments[segNo + 1].SegmentName;

         switch (segments[segNo].SegmentType) {
            // Generic types
            case SelectorType.ANY: selectedObjects = NarrowSelection<NamedElement>(candidateObjects,name,importedSeen,fullSeen); break;
            case SelectorType.CONTAINER: selectedObjects = NarrowSelection<Container>(candidateObjects,name,importedSeen,fullSeen); break;
            case SelectorType.DATA: selectedObjects = NarrowSelection<CDL2Object>(candidateObjects,name,importedSeen,fullSeen,obj => obj is IDataElement); break;
            case SelectorType.OBJECT: selectedObjects = NarrowSelection<CDL2Object>(candidateObjects,name,importedSeen,fullSeen); break;

            // Containers
            case SelectorType.PROGRAM: selectedObjects = NarrowSelection<Program>(candidateObjects,name,importedSeen,fullSeen); break;
            case SelectorType.MODULE: selectedObjects = NarrowContainerSelection(typeof(Module),candidateObjects,segments,segNo,importedSeen,fullSeen,isRooted); break;
            case SelectorType.LAYER: selectedObjects = NarrowContainerSelection(typeof(Layer),candidateObjects,segments,segNo,importedSeen,fullSeen,isRooted); break;
            case SelectorType.SECTION: selectedObjects = NarrowContainerSelection(typeof(Section),candidateObjects,segments,segNo,importedSeen,fullSeen,isRooted); break;

            // Specific OBJECTS
            case SelectorType.ALGORITHM: selectedObjects = NarrowSelection<Algorithm>(candidateObjects,name,importedSeen,fullSeen,alg => !alg.IsSynthetic); break;
            case SelectorType.PROCEDURE: selectedObjects = NarrowSelection<Procedure>(candidateObjects,name,importedSeen,fullSeen,alg => !alg.IsSynthetic); break;
            case SelectorType.MACRO: selectedObjects = NarrowSelection<Macro>(candidateObjects,name,importedSeen,fullSeen); break;
            case SelectorType.FUNCTION: selectedObjects = NarrowSelection<Algorithm>(candidateObjects,name,importedSeen,fullSeen,alg => alg.IsFunction && !alg.IsSynthetic); break;
            case SelectorType.ACTION: selectedObjects = NarrowSelection<Algorithm>(candidateObjects,name,importedSeen,fullSeen,alg => alg.IsAction && !alg.IsSynthetic); break;
            case SelectorType.TEST: selectedObjects = NarrowSelection<Algorithm>(candidateObjects,name,importedSeen,fullSeen,alg => alg.IsTest && !alg.IsSynthetic); break;
            case SelectorType.PREDICATE: selectedObjects = NarrowSelection<Algorithm>(candidateObjects,name,importedSeen,fullSeen,alg => alg.IsPredicate && !alg.IsSynthetic); break;
            case SelectorType.CONST: selectedObjects = NarrowSelection<Const>(candidateObjects,name,importedSeen,fullSeen); break;
            case SelectorType.VAR: selectedObjects = NarrowSelection<Var>(candidateObjects,name,importedSeen,fullSeen); break;
            case SelectorType.LIST: selectedObjects = NarrowSelection<LIST>(candidateObjects,name,importedSeen,fullSeen); break;

            // Lists where the selection is the entire list  
            case SelectorType.ABSTR or SelectorType.EXT or SelectorType.INV or SelectorType.IMPORT or SelectorType.EXPORT or SelectorType.FACE:
               selectedObjects = NarrowSelectionToInterface();
               break;

            // Ludes
            case SelectorType.PRELUDE or SelectorType.ROOT or SelectorType.POSTLUDE or SelectorType.LUDE: 
               selectedObjects = NarrowSelectionToLude(segments[segNo].SegmentType,selectedObjects,currentSelectedObjects);
               listType = segments[segNo].SegmentType;
               break;

            case SelectorType.AFFIX or SelectorType.CALL or SelectorType.LOCAL:
               selectedObjects = NarrowSelectionToNonFocusable<Algorithm>(candidateObjects,segments,segNo,importedSeen,segments[segNo].SegmentType);
               break;

            // NOTE and PART. Not clear yet whether these should be supported.
            case SelectorType.PART:
            case SelectorType.NOTE: 
               goto default;

            // Special prefixes that are used to select imported or non-imported CONSTs and ALGORITHMs. Handled during segment construction above
            case SelectorType.IMPORTED or SelectorType.STUB or SelectorType.FULL:
               errorMessage = $"Fapipa Unfiltered IMPORTED/STUB/FULL which should not be possible"; // Hommage à Mihályi Kati 
               break;

            // A selector type that may have been missed
            case SelectorType.INVALID: 
               errorMessage = $"Unrecognized selector type";
               break;
            // A selector type that may have been missed
            default:
               errorMessage = $"Unimplemented selector type: {segments[segNo].SegmentType}"; 
               break;
         }
         if (!selectedObjects.Any()) errorMessage = "Info:No matches";
         return selectedObjects.Where(obj => obj.IsDeclared);
      }

      /// <summary>
      /// Narrowing to a container must support sticking to ancestors of the focus unless the selector is rooted
      /// </summary>
      /// <example>
      /// Assume the focus is Mod m Lay l Sec s Fu f.
      /// Then the selector "Mod Sec" selects all section in m. The selector "^Mod Sec t" selects all sections whose name contains a t in all modules.
      /// </example>
      /// <param name="type"></param>
      /// <param name="candidateObjects"></param>
      /// <param name="segments"></param>
      /// <param name="segNo"></param>
      /// <param name="importedSeen"></param>
      /// <param name="fullSeen"></param>
      /// <param name="isRooted"></param>
      /// <returns></returns>
      private static IEnumerable<NamedElement> NarrowContainerSelection(Type type,IEnumerable<NamedElement> candidateObjects,SelectionSegments segments,int segNo,
            bool importedSeen,bool fullSeen,bool isRooted) {
         string name = segments[segNo+1].SegmentName;
         // If the subselector is not the last and the current focus is on a "smaller" unit then a blank name should be treated as the coontainer containing the focus.
         // Otherwise it should match anything.
         if (!isRooted && segNo < segments.Count - 2 && name == "" && Abbreviation<SelectorType>.AncestorFocusTypeOf(ancestor: segments[segNo].SegmentType,child: Focus.Current.FocusType)) {
            NamedElement? ancestor = Focus.Current.Object!.GetAncestorOfType(segments[segNo].SegmentType);
            if (ancestor is not null) name = ancestor.Id.Name;
         }

         return type switch {
            Type t when t == typeof(Module) => NarrowSelection<Module>(candidateObjects,name,importedSeen,fullSeen),
            Type t when t == typeof(Layer) => NarrowSelection<Layer>(candidateObjects,name,importedSeen,fullSeen),
            Type t when t == typeof(Section) => NarrowSelection<Section>(candidateObjects,name,importedSeen,fullSeen),
            _ => [],
         };
      }

      private static IEnumerable<NamedElement> NarrowSelection<T>(IEnumerable<NamedElement> candidateObjects,string name,
            bool importedSeen,bool fullSeen,Func<T,bool>? pred = null) where T : NamedElement {
         if (Database.TryGetNamedElements<T>(candidateObjects,name,out IEnumerable<T>? elements) && elements is not null) {
            if (pred is not null) elements = elements.Where(e => pred((T)e));
            if (importedSeen) elements = elements.Where(e => e.IsImported);
            if (fullSeen)     elements = elements.Where(e => ! e.IsImported);
            return elements;
         }
         return [];
      }

      private IEnumerable<NamedElement> NarrowSelectionToNonFocusable<T>(IEnumerable<NamedElement> candidateObjects,SelectionSegments segments,int segNo,bool importedSeen,SelectorType elementType) where T : Algorithm {
         // TODO: this implementaton should also work for CALLs in ludes since they are synthetic PROCEDUREs. Verification needed.
         IEnumerable<NamedElement> selectedObjects = NarrowSelection<T>(candidateObjects,segments[segNo + 1].SegmentName,importedSeen,importedSeen,null); // Narrow down to algorithms for AFFIX and LOCAL any, for CALL PROCEDUREs only.

         switch (elementType) {
            case SelectorType.AFFIX:
               selectedObjects = NarrowToHeaderSubComponent<Affix>(selectedObjects,segments[segNo + 1].SegmentName);
               break;
            case SelectorType.LOCAL:
               selectedObjects = NarrowToHeaderSubComponent<Local>(selectedObjects,segments[segNo + 1].SegmentName);
               break;
            case SelectorType.CALL:
               /// TODO: Implement later. Needs to somwhow collect all calls in each algorithm and the filter by name.
               break;
            default:
               ErrorMessage = $"NarrowSelectionToNonFocusable: Unrecognized element type {elementType}";
               return [];
         }
         return selectedObjects;
      }

      private IEnumerable<NamedElement> NarrowToHeaderSubComponent<U>(IEnumerable<NamedElement> selectedObjects,string segmentName) where U : NamedElement {
         return selectedObjects.SelectMany(obj
            => Database.TryGetNamedElements<U>(((Algorithm)obj).Affixes,segmentName,out IEnumerable<U>? affixes) ? affixes : []);
      }

      /// <summary>
      /// Selects containers that have ludes of the specified type.
      /// </summary>
      /// <param name="type"></param>
      /// <param name="currentSelectedObjects"></param>
      /// <returns></returns>
      /// <param name="listType"></param>
      private IEnumerable<NamedElement> NarrowSelectionToLude(ST type,IEnumerable<NamedElement> selectedObjects,IEnumerable<NamedElement> currentSelectedObjects) {
         IEnumerable<Container> candidates = selectedObjects.OfType<Container>();
         if (!candidates.Any()) candidates = currentSelectedObjects.OfType<Container>();
         static bool HasLudesOfType(Container c,ST t) {
            if (t == ST.LUDE) return c.Ludes.Values.Sum(v => v.Count) > 0;
            return c.Ludes[Container.LudeTypeBySelector[t]].Count > 0;
         }
         return candidates.Where(c => HasLudesOfType(c,type));
      }

      private IEnumerable<NamedElement> NarrowSelectionToInterface() => throw new NotImplementedException();

      private bool ParseSelectionSegments(string selectionString,SelectionSegments segments,out bool importedSeen, out bool fullSeen) {
         bool previousSegmentWasUnit = false;
         bool previousSegmentWasNameOrOffset = false;
         importedSeen = false;
         fullSeen = false;
         while (selectionString.Length > 0) {
            Match match = SelectorSegmentRE().Match(selectionString);
            if (!match.Success) break; // No more matches, exit loop
            selectionString = selectionString[match.Length..].Trim(); // Remove the matched segment from the string
            string segment = match.Value.Trim();
            if (char.IsAsciiLetterUpper(segment[0])) {
               // Uppercase segment, identify as a unit type
               SelectorType type = Abbreviation<SelectorType>.Identify(segment.ToUpper());
               if (type == SelectorType.INVALID) {
                  ErrorMessage = $"Invalid selector type: {segment}";
                  return false;
               } else if (type == SelectorType.IMPORTED || type == SelectorType.STUB) {
                  if (importedSeen) {
                     if (fullSeen) {
                        ErrorMessage = $"Invalid selection: IMPORTED/STUB cannot be used with FULL";
                     } else {
                        ErrorMessage = $"Invalid selection: multiple IMPORTED/STUBs are not allowed";
                     }
                     return false;
                  } else {
                     importedSeen = true; // Mark that an IMPORTED segment was seen
                  }
               } else if (type == SelectorType.FULL) {
                  if (fullSeen) {
                     if (importedSeen) {
                        ErrorMessage = $"Invalid selection: IMPORTED/STUB cannot be used with FULL";
                     } else {
                        ErrorMessage = $"Invalid selection: multiple FULLs are not allowed";
                     }
                     return false;
                  } else {
                     fullSeen = true; // Mark that a FULL segment was seen
                  }
               } else {
                  if (previousSegmentWasUnit) segments.Add(new NameSegment("")); // Add empty name segment if previous was uppercase
                  segments.Add(new UnitSegment(type));
                  previousSegmentWasUnit = true;
                  previousSegmentWasNameOrOffset = false;
               }
            } else if (segment.StartsWith(':')) {  // index into the selections, it will be applied at the end
               segments.Index = int.Parse(match.Groups["index"].Value.Trim()); // Parse the index from the segment
            } else if (previousSegmentWasNameOrOffset) {
               ErrorMessage = $"Invalid selection: {segment} after a name or offset segment";
               return false; // Invalid sequence, can't have adjacent name and offset segments
            } else if (char.IsAsciiLetterLower(segment[0]) || segment[0] == '/') { // Name segment
               segments.Add(new NameSegment(segment));
               previousSegmentWasUnit = false;
               previousSegmentWasNameOrOffset = true;
            } else {
               segments.Add(new OffsetSegment(segment));
               previousSegmentWasUnit = false;
               previousSegmentWasNameOrOffset = true;
            }
         }
         if (segments.Count == 0) return false; // No valid parts found
         if (previousSegmentWasUnit) segments.Add(new NameSegment("")); // Add empty name segment if the last was a unit, ensure an even number of elements
         if (segments.Count > 0 && segments.Count % 2 == 1) {
            ErrorMessage = $"Unable to parse selector";
            return false;
         }
         // Verify that the types are in hierarchical order
         for (int i = 0 ; i < segments.Count - 2 ; i += 2) {
            if (segments[i].SegmentType == SelectorType.IMPORTED) continue; // Skip IMPORTED segment
            if (!Abbreviation<SelectorType>.AncestorFocusTypeOf(ancestor: segments[i].SegmentType,child: segments[i + 2].SegmentType)) {
               // The types are not in hierarchical order, return without setting selection
               ErrorMessage = $"Invalid selection: {segments[i + 2].SegmentType} cannot follow {segments[i].SegmentType}";
               return false;
            }
         }

         return true;
      }

      private const int SingleSelectionCount = 2;
      public override string ToString() {
         if (IsInvalid) return $"Selection(Invalid: {ErrorMessage})";
         return "Selection<" + (this.Take(SingleSelectionCount).Aggregate("",(a,b) => $"{a} {b}")).TrimStart() + (this.Count > SingleSelectionCount ? "..." : "") + ">";
      }

      /// <summary>
      /// Provides a compiled regular expression for parsing selector segments according to specific patterns.
      /// </summary>
      /// <remarks>The returned regular expression is optimized for performance using the <see
      /// cref="RegexOptions.Compiled"/> option. It supports matching multiple selector segment formats, such as named
      /// segments, paths, indices, and numeric values. Use the named group 'index' to extract index values when
      /// present.</remarks>
      /// <returns>A compiled <see cref="Regex"/> instance that matches selector segment patterns, including capitalized words,
      /// path segments, index specifiers, signed numbers, and lowercase identifiers.</returns>
      [GeneratedRegex(@"([A-Z][A-Za-z]*)|(/(?:[^\s\\]|\\.)*)|(:\s*(?<index>\d+)$)|([+-]\s*\d+)|([a-z][a-z0-9\s]*)",RegexOptions.Compiled)]
      private static partial Regex SelectorSegmentRE();
   }

   /// <summary>
   /// Provides functionality related to the current object, the Focus, of the CDL2 Laboratory
   /// </summary>
   public class Focus {

      public static Focus Current => Database.Instance.FocusStack.Peek();

      public static void Push() => Database.Instance.FocusStack.Push(new Focus());
      public static void Pop() => Database.Instance.FocusStack.Pop();

      [JsonInclude, JsonPropertyOrder(0)]
      public SingleSelection Selection = SingleSelection.Empty;
      [JsonIgnore]
      public SelectorType FocusType => Selection.Object?.FocusType ?? SelectorType.INVALID;

      [JsonConstructor]
      public Focus() { }
      public Focus(SingleSelection selection) => Selection = selection;
      public Focus(Selection selection) => Selection = selection.Count > 0 ? selection.First() : SingleSelection.Empty;

      /// <summary>
      /// If the focus is on an object of type specified by the reserved word, return the index of that object in its siblings.
      /// </summary>
      /// <param name="objectType"></param>
      /// <returns></returns>
      public int IndexFor(RW objectType) => Selection.Object?.GetType() == Parser.RW2Type[objectType] ? IndexFor() : -1;
      /// <summary>
      /// Gets the zero-based index of the currently selected object's GUID within its siblings collection.
      /// </summary>
      /// <remarks>The siblings collection is assumed to contain the GUIDs of objects related to the current
      /// selection. This method requires that the selected object and its siblings collection are not null.</remarks>
      /// <returns>The zero-based index of the selected object's GUID in the siblings collection, or -1 if the GUID is not found.</returns>
      public int IndexFor() => Selection.Object!.Siblings.IndexOf(Selection.Object.GUID);
      /// <summary>
      /// Similar to IndexFor, but returns the object itself from the list of candidates.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="objectType"></param>
      /// <param name="candidates"></param>
      /// <returns>The object or null.</returns>
      /// <example>
      /// <code>
      /// // Assuming we have module 
      /// Layer? layer = Focus.Current.ObjectFor(RW.LAYER,module.Layers);
      /// </code>
      /// </example>
      public T? ObjectFor<T>(RW objectType,IEnumerable<T> candidates) where T : NamedElement {
         int index = IndexFor(objectType);
         if (index >= 0 && index < candidates.Count()) {
            return candidates.ElementAt(index);
         } else {
            return null;
         }
      }

      [JsonIgnore]
      public Module? Module => Selection.Object switch {
         Module module => module,
         Layer layer => layer.Module,
         Section section => section.Module,
         CDL2Object cdl2Object => cdl2Object.Module,
         _ => null,
      };
      [JsonIgnore]
      public Layer? Layer => Selection.Object switch {
         Layer layer => layer,
         Section section => section.Layer,
         CDL2Object cdl2Object => cdl2Object.Layer,
         _ => null,
      };
      [JsonIgnore]
      public Section? Section => Selection.Object switch {
         Section section => section,
         CDL2Object cdl2Object => cdl2Object.Section,
         _ => null,
      };

      /// <summary>
      /// Parse the focus string and set the focus if it is valid.
      /// Currently supports format of the form: RW1 name1 RW2 name2 ... where RW is a reserved word (all capital letters)
      /// and name is an ID (starting with lowercase and can contain special characters).
      /// </summary>
      /// <param name="focusString">String in format "UPPERlowerUPPERlower"</param>
      /// <returns>True if focus was successfully set, false otherwise</returns>
      public static bool SetFocus(string focusString,out string errorMessage) {
         errorMessage = "";
         if (string.IsNullOrWhiteSpace(focusString)) return true; // Empty focus string means no change to focus, but this is OK
         Selection selection = new(focusString);
         if (selection.IsInvalid) {
            errorMessage = selection.ErrorMessage;
            return false;
         } else if (selection.IsFocusable) {
            Database.Instance.FocusStack.Push(new Focus(selection));
            Database.Instance.CLI?.SetStatus(selection.First().Object);
            return true;
         } else {
            errorMessage = "Attempt to set focus to a non-focusable object";
            return false;
         }
      }
      public static bool SetFocus(NamedElement elem) => SetFocus(new SingleSelection(elem));
      public static bool SetFocus(SingleSelection selection) {
         if (selection.Object is null)
            return false;
         if (selection.IsFocusable) {
            Database.Instance.FocusStack.Push(new Focus(selection));
            Database.Instance.CLI?.SetStatus(selection.Object);
            return true;
         } else {
            Logger.Log($"Attempt to set focus to a non-focusable object: {selection.Object}");
            return false;
         }
      }
      public static bool SetFocus(Guid guid) {
         if (guid == Guid.Empty)
            return false;
         NamedElement? elem = NamedElement.From<NamedElement>(guid);
         if (elem is null)
            return false;
         return SetFocus(elem);
      }

      /// <summary>
      /// Set the focus to the next element in the Siblings list after this one, or to the one before it if it is the last one.
      /// If this is the only element, set the focus to the parent of the element, or to an empty selection if there is no parent.
      /// </summary>
      /// <param name="elem"></param>
      /// <param name="siblings"></param>
      public static void MoveFocusFrom(ISibling elem) {
         Debug.Assert(elem.Siblings.Contains(elem.GUID),"MoveFocusFrom called with an element that is not among its siblings");
         if (Focus.Current.Object is not null && Focus.Current.Object.GUID == elem.GUID) {
            if (elem.TryGetAdjacentSibling(out Guid adjacentSiblingGuid)) {
               SetFocus(adjacentSiblingGuid);
            } else if (elem.Parent != Guid.Empty) {
               SetFocus(elem.Parent);
            } else {
               SetFocus(SingleSelection.Empty);
            }
         }
      }

      /// <summary>
      /// The currently focused NamedElement
      /// </summary>
      [JsonIgnore]
      public NamedElement? Object {
         get => Selection.Object;
         set => Selection.Object = value;
      }
      /// <summary>
      /// Get the container of the currently selected object.
      /// </summary>
      public Container? Container => Object is Container cont ? cont : Object?.Section;

      public override string ToString() {
         if (Object == null)
            return "Nothing";
         string focusString = Object.FQDN();
         // TODO: Add more details based on SubObjectDepth and SubObjectOrdinal
         return focusString;
      }

      /// <summary>
      /// Move the focus n items in the Siblings list. Return false if this is not possible.
      /// However, if limits are exceeded, the move is to the first or last sibling.
      /// If the focus is on an object that does not have siblings, return false.
      /// </summary>
      /// <param name="args">These are interpreted as an optionally signed integer.</param>
      /// <param name="direction"></param>
      /// <returns></returns>
      /// <exception cref="NotImplementedException"></exception>
      /// <param name="msg"></param>
      /// <param name="severity"></param>
      internal bool MoveFocus(string args,MoveDirection direction,out string msg,out Severity severity) {
         (msg,severity) = ("Invalid command",Severity.Error);
         SelectorType moveToElementType;
         if (Object is null) return false; // Note that there is no need to check focusability here, as the focus is always on a focusable object.

         int moveCount = 1;
         int newIndex = -1;
         if (args.IsNullEmptyOrWhitespace || int.TryParse(args.Trim(),out moveCount)) {
            int currentIndex = CurrentIndex(Object);
            int maxIndex = Object.Siblings.Count - LudeCount(Object) - 1;
            if (direction == MoveDirection.first || direction == MoveDirection.last) {
               if (moveCount != 1) {
                  msg = $"Count invalid for {direction}";
                  return false;
               }
               newIndex = direction == MoveDirection.first ? 0 : maxIndex;
            } else {
               newIndex = (currentIndex + moveCount * (int)direction).ConstrainedTo(0,maxIndex);
            }
            if (newIndex == currentIndex) {
               (msg,severity) = ("Already there",Severity.Info);
               return false;
            } else {
               return Focus.SetFocus(Object.Siblings[newIndex]);
            }
         } else if ((moveToElementType = TypeOnlySelector(ref msg)) == SelectorType.INVALID) {
            msg = "Invalid selector";
            return false;
         } else {
            // The args are not a valid integer, but they are a valid type-only selector, so we will try to move to the next/previous/first/last sibling of that type.
            moveToElementType = TypeOnlySelector(ref msg);
            if (moveToElementType == SelectorType.INVALID) return false;
            // First, try on the siblings of the current Object
            if (FindSiblingWithType(Object,CurrentIndex(Object),ref newIndex)) {
               return Focus.SetFocus(Object.Siblings[newIndex]);
            }
            // If the focus is on an element that can contain elements of the movetype, then try that.
            // TODO: Only the first child is considered so previous cannot work.
            if (direction != MoveDirection.previous && Abbreviation<SelectorType>.AncestorFocusTypeOf(ancestor: Object.FocusType,child: moveToElementType)) {
               NamedElement? elem = Object;
               // For now, consider only the first child.
               while (elem is not null && Abbreviation<SelectorType>.AncestorFocusTypeOf(ancestor: elem.FocusType,child: moveToElementType)) {
                  elem = elem.ChildElements().FirstOrDefault();
               }
               // elem is now a sibling of the requested type
               if (elem is not null && FindSiblingWithType(elem,0,ref newIndex,includeCurrent: true)) {
                  return Focus.SetFocus(elem.Siblings[newIndex]);
               }
            }
            (msg,severity) = ($"{direction} {moveToElementType} not found in context",Severity.Warning);
            return false;
         }

         #region MoveFocus local functions
         //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
         // MoveFocus Local functions
         //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
         
         // Parse a type-only selector from the args.
         SelectorType TypeOnlySelector(ref string msg) {
            Selection sels = new(args,typeOnly: true);
            if (sels.IsValid && sels.Count == 1) {
               SingleSelection sel = sels.First();
               if (sel.ListType == SelectorType.INVALID && sel.ObjectGuid == Guid.Empty) {
                  return sel.Kind;
               } else {
                  msg = "Invalid selector: must have object type only";
                  return SelectorType.INVALID;
               }
            } else {
               msg = "Invalid selector";
               return SelectorType.INVALID;
            }
         }

         // Count the number of ludes among the siblings.
         int LudeCount(NamedElement elem) => elem.Siblings.ToSyntheticCDL2Objects().Count;

         // Get the current index of the element among its siblings.
         int CurrentIndex(NamedElement elem) => elem.Siblings.IndexOf(elem.GUID);

         // Check if the sibling for a sibling of the specified type exists in the given direction starting fron index.
         // Returns the index in foundIndex if found
         bool FindSiblingWithType(NamedElement? elem,int index,ref int foundIndex,bool includeCurrent=false) {
            if (elem == null) return false;
            switch (direction) {
               case MoveDirection.next:
                  if (includeCurrent && SiblingHasType(elem,index)) {foundIndex = index; return true;}
                  while (++index <= elem.Siblings.Count - LudeCount(elem) - 1) if (SiblingHasType(elem,index)) {foundIndex = index; return true;}
                  break;
               case MoveDirection.previous:
                  if (includeCurrent && SiblingHasType(elem,index)) {foundIndex = index; return true;}
                  while (--index >= 0) if (SiblingHasType(elem,index)) {foundIndex = index; return true;}
                  break;
               case MoveDirection.first:
                  index = -1;
                  while (++index <= elem.Siblings.Count - LudeCount(elem) - 1) if (SiblingHasType(elem,index)) {foundIndex = index; return true;}
                  break;
               default: // MoveDirection.Last
                  index = elem.Siblings.Count - LudeCount(elem);
                  while (--index >= 0) if (SiblingHasType(elem,index)) {foundIndex = index; return true;}
                  break;
            }
            return false;
            // Local function to check if the sibling at the specified index has the specified type. Used to avoid code duplication in the move logic above.
            bool SiblingHasType(NamedElement obj,int index) => obj.Siblings[index].TryGetNamedElement(out NamedElement? elem) && elem!.FocusType == moveToElementType;
         }
         #endregion // MoveFocus local functions
      }
   }

   /// <summary>
   /// Used to let the parser know where to insert newly created objects.
   /// </summary>
   public class ParsingContext(Focus? focus = null,InsertLocation location = InsertLocation.Last) {
      public readonly InsertLocation Location = location;
      public readonly Focus Focus = focus ?? Focus.Current;

      public RW LudeType = RW.NONE;

      public override string ToString() => $"{Location} {Focus}";
   }
}

