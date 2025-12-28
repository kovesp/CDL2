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
      public RW ListType = RW.NONE;


      private static readonly Type[] NonFocusableTypes = [typeof(Affix), typeof(Local), typeof(Call)];
      [JsonIgnore]
      public bool IsFocusable => ! NonFocusableTypes.Contains(Object?.GetType() ?? typeof(NamedElement));
      public override string ToString() => $"SingleSelection<{Object}>";
   }

   /// <summary>
   /// Represents the objects selected by a selector. A valid selector will always select at least one object.
   /// </summary>
   public class Selection : List<SingleSelection> {
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
         public override string ToString() => "SelectionSegments<" + (this.Aggregate("", (a, b) => $"{a} {b}")).TrimStart() + (Index > 0 ? $" : {Index}>" : ">");
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
      public Selection(string selectionString) : base() {
         if (string.IsNullOrWhiteSpace(selectionString)) return;
         selectionString = selectionString.Trim();

         SelectionSegments segments = [];

         bool isRooted = false;
         if (selectionString.StartsWith('^')) {
            isRooted = true; // The selection is rooted
            selectionString = selectionString[1..].Trim();
         }

         if (selectionString == string.Empty) {
            Add(SingleSelection.Empty);
            return;
         }

         Regex regex = new(@"([A-Z][A-Za-z]*)|(/.*)|(:\s*(?<index>\d+)$)|([+-]\s*\d+)|([a-z][a-z\s]*)", RegexOptions.Compiled);

         bool previousSegmentWasUnit = false;
         bool previousSegmentWasNameOrOffset = false;
         bool importedSeen = false; // Used to track if an IMPORTED segment was seen
         while (selectionString.Length > 0) {
            Match match = regex.Match(selectionString);
            if (!match.Success) break; // No more matches, exit loop
            selectionString = selectionString[match.Length..].Trim(); // Remove the matched segment from the string
            string segment = match.Value.Trim();
            if (char.IsAsciiLetterUpper(segment[0])) {
               // Uppercase segment, identify as a unit type
               SelectorType type = Abbreviation<SelectorType>.Identify(segment.ToUpper());
               if (type == SelectorType.INVALID) {
                  ErrorMessage = $"Invalid selector type: {segment}";
                  return;
               } else if (type == SelectorType.IMPORTED) {
                  if (importedSeen) {
                     ErrorMessage = $"Invalid selection: multiple IMPORTED segments are not allowed";
                     return; // Multiple IMPORTED segments are not allowed
                  } else {
                     importedSeen = true; // Mark that an IMPORTED segment was seen
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
               return; // Invalid sequence, can't have adjacent name and offset segments
            } else if (char.IsAsciiLetterLower(segment[0]) || segment[0]=='/') { // Name segment
               segments.Add(new NameSegment(segment));
               previousSegmentWasUnit = false;
               previousSegmentWasNameOrOffset = true;
            } else {
               segments.Add(new OffsetSegment(segment));
               previousSegmentWasUnit = false;
               previousSegmentWasNameOrOffset = true;
            }
         }
         if (segments.Count == 0) return; // No valid parts found
         if (previousSegmentWasUnit) segments.Add(new NameSegment("")); // Add empty name segment if the last was a unit, ensure an even number of elements
         if (segments.Count > 0 && segments.Count % 2 == 1) {
            ErrorMessage = $"Unable to parse selector";
            return;
         }
         // Verify that the types are in hierarchical order
         for (int i = 0; i < segments.Count - 2; i += 2) {
            if (segments[i].SegmentType == SelectorType.IMPORTED) continue; // Skip IMPORTED segment
            if (!Abbreviation<SelectorType>.AncestorFocusTypeOf(ancestor:segments[i].SegmentType,child:segments[i + 2].SegmentType)) {
               // The types are not in hierarchical order, return without setting selection
               ErrorMessage = $"Invalid selection: {segments[i+2].SegmentType} cannot follow {segments[i].SegmentType}";
               return;
            }
         }

         IEnumerable<NamedElement> candidateObjects;
         IEnumerable<NamedElement> selectedObjects = [];

         if (!isRooted && segments[1].SegmentName == "" && Abbreviation<SelectorType>.AncestorFocusTypeOf(segments[0].SegmentType,Focus.Current.FocusType)) {
            // The initial segment is just a type without a name, and the focus is on an object that is a descendant of that type. e.g. "Module" when the focus is on a Layer.
            candidateObjects = [ Focus.Current.Object!.GetAncestorOfType(segments[0].SegmentType) ];
         } else if (isRooted || !Abbreviation<SelectorType>.AncestorFocusTypeOf(Focus.Current.FocusType,segments[0].SegmentType)) {
            candidateObjects = Database.Instance.NamedElements.Values;
         } else {
            candidateObjects = Focus.Current.Object!.DescendantElements();
            // The selection is relative to the current focus. TODO: sub elements are being ignored
         }

         // Use the segments to successively narrow down the selection.
         for (int segNo = 0 ; segNo < segments.Count; segNo += 2) {

            // Narrow the selection using the type and name from the current segment
            void NarrowSelection<T>(Func<T, bool>? pred = null) where T : NamedElement {
               if (Database.TryGetNamedElements<T>(candidateObjects,segments[segNo + 1].SegmentName,out IEnumerable<T>? elements) && elements is not null) {
                  if (pred is not null) elements = elements.Where(e => pred((T)e));
                  elements = elements.Where(e => importedSeen == e.IsImported);
                  selectedObjects = elements;
                  if (segNo < segments.Count - 2) candidateObjects = elements.SelectMany(e => e.DescendantElements());              
               }
            }
            void NarrowSelectionToNonFocusable<T>(SelectorType elementType) where T : Algorithm {
               // TODO: this implementaton should also work for CALLs in ludes since they are synthetic PROCEDUREs. Verification needed.
               NarrowSelection<T>(); // Narrow down to algorithms for AFFIX and LOCAL any, for CALL PROCEDUREs only.
               void NarrowToHeaderSubComponent<U>() where U : NamedElement {
                  selectedObjects = 
                     selectedObjects.SelectMany(obj
                        => Database.TryGetNamedElements<U>(((Algorithm)obj).Affixes,segments[segNo+1].SegmentName,out IEnumerable<U>? affixes) ? affixes : []);
               }
               switch (elementType) {
                  case SelectorType.AFFIX:
                     NarrowToHeaderSubComponent<Affix>();
                     break;
                  case SelectorType.LOCAL:
                     NarrowToHeaderSubComponent<Local>();
                     break;
                  case SelectorType.CALL:
                     /// TODO: Implement later. Needs to somwhow collect all calls in each algorithm and the filter by name.
                     break;
                  default:
                     ErrorMessage = $"NarrowSelectionToNonFocusable: Unrecognized element type {elementType}";
                     return;
               }
            }

            void NarrowSelectionToLude() => throw new NotImplementedException();

            void NarrowSelectionToList() => throw new NotImplementedException();

            switch (segments[segNo].SegmentType) {
               // Generic types
               case SelectorType.ANY:          NarrowSelection<NamedElement> (); break; // Excludes ludes, parts and interfaces for now.
               case SelectorType.CONTAINER:    NarrowSelection<Container>    (); break;
               case SelectorType.DATA:         NarrowSelection<CDL2Object>   (obj=>obj is IDataElement); break;
               case SelectorType.FACE:         NarrowSelectionToList         (); break;
               case SelectorType.OBJECT:       NarrowSelection<CDL2Object>   (); break;

               // Specific containers
               case SelectorType.PROGRAM:      NarrowSelection<Program>      (); break;
               case SelectorType.MODULE:       NarrowSelection<Module>       (); break;
               case SelectorType.LAYER:        NarrowSelection<Layer>        (); break;
               case SelectorType.SECTION:      NarrowSelection<Section>      (); break;

               // Specific OBJECTS
               case SelectorType.ALGORITHM:    NarrowSelection<Algorithm>    (); break;
               case SelectorType.PROCEDURE:    NarrowSelection<Procedure>    (); break;
               case SelectorType.MACRO:        NarrowSelection<Macro>        (); break;
               case SelectorType.FUNCTION:     NarrowSelection<Algorithm>    (alg => alg.IsFunction); break;
               case SelectorType.ACTION:       NarrowSelection<Algorithm>    (alg => alg.IsAction); break;
               case SelectorType.TEST:         NarrowSelection<Algorithm>    (alg => alg.IsTest); break;
               case SelectorType.PREDICATE:    NarrowSelection<Algorithm>    (alg => alg.IsPredicate); break;
               case SelectorType.CONST:        NarrowSelection<Const>        (); break;
               case SelectorType.VAR:          NarrowSelection<Var>          (); break;
               case SelectorType.LIST:         NarrowSelection<LIST>         (); break;

               // Lists where the selection is the entire list (for now)
               case SelectorType.ABSTR:
               case SelectorType.EXT:
               case SelectorType.INV:
               case SelectorType.IMPORT:
               case SelectorType.EXPORT:
               case SelectorType.PART:
                  NarrowSelectionToList();
                  break;

               // Non-focusable types
               case SelectorType.AFFIX: 
               case SelectorType.LOCAL:
                  NarrowSelectionToNonFocusable<Algorithm>(segments[segNo].SegmentType);
                  break;
               case SelectorType.CALL:
                  NarrowSelectionToNonFocusable<Procedure>(SelectorType.CALL);
                  break;
               // Ludes
               case SelectorType.PRELUDE:    NarrowSelectionToLude           (); break;
               case SelectorType.ROOT:       NarrowSelectionToLude           (); break;
               case SelectorType.POSTLUDE:   NarrowSelectionToLude           (); break;

               // NOTE selection. Not clear yet whether this should be supported.
               case SelectorType.NOTE: goto default;

               // Special prefix that is used to selected imported CONSTs and ALGORITHMs. Handled during segment construction above
               case SelectorType.IMPORTED:
                  ErrorMessage = $"Fapipa: Unfiltered IMPORTED which is not possible"; // Hommage à Mihályi Kati 
                  break;
               case SelectorType.INVALID:   ErrorMessage = $"Unrecognized selection type"; break;
               default:                     ErrorMessage = $"Unimplemented selection type: {segments[segNo].SegmentType}"; break;
            }        
         }

         if (segments.Index < 1) {
            // If the selectedObjects are all siblings, add them in sibling order, otherwise OrderedAsSiblings leaves the order unchanged.
            AddRange(selectedObjects.OrderedAsSiblings.Select(obj => new SingleSelection(obj)));
         } else {
            Add(new SingleSelection(selectedObjects.ElementAt(Math.Min(segments.Index,segments.Count)-1)));
         }
      }     
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
      internal bool Move(string args,FocusMoveDirection direction,out string msg,out Severity severity) {
         (msg,severity) = ("Invalid command", Severity.Error);
         if (Object is null) return false; // Note that there is no need to check focusability here, as the focus is always on a focusable object.
         // TODO: Check if the object has siblings, if not, return false. Interface list do not have siblings.
         int newIndex;
         int currentIndex = Object.Siblings.IndexOf(Object.GUID);
         int ludeCount = Object.Siblings.ToSyntheticCDL2Objects().Count();
         switch (direction) {
            case FocusMoveDirection.First:
               if (args.IsNotEmptyOrWhitespace) return false;
               newIndex = 0;
               break;
            case FocusMoveDirection.Last:
               if (args.IsNotEmptyOrWhitespace) return false;
               newIndex = Object.Siblings.Count -ludeCount - 1;
               break;
            default:
               int focusMoveCount = 1;
               if (args.IsNotEmptyOrWhitespace && !int.TryParse(args.Trim(),out focusMoveCount)) return false;
               newIndex = (currentIndex + focusMoveCount*(int)direction).ConstrainedTo(0,Object.Siblings.Count-ludeCount-1);
               break;
         }
         if (newIndex == currentIndex) {
            (msg,severity) = ("Already there", Severity.Info);
            return false;
         } else {
            return Focus.SetFocus(Object.Siblings[newIndex]);
         }
      }
   }
}

