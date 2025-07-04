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

using System;
using System.Collections;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
   ///         <item>Local</item>
   ///         <item>LastCall</item>
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

      public SingleSelection(NamedElement? obj) {
         Object = obj;
      }

      public static SingleSelection Empty => new ();
      /// <summary>
      /// The Guid of a NamedElement in the selection.
      /// For "simple" objects (e.g., Vars, Layers) this uniquely identifiest the objct.
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
      /// The ordianl of the selection sub element for types where this makes sense, -1 otherwise.
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
         public SelectionType SegmentType => this is UnitSegment unit ? unit.Type : SelectionType.INVALID;
         public string SegmentName => this is NameSegment id ? id.Name : "";
         public int SegmentOffset => this is OffsetSegment offset ? offset.Offset : 0;
      }
      private class UnitSegment(SelectionType type) : SelectionSegment {
         public SelectionType Type { get; private set; } = type;
         public override string ToString() => Type.ToString();
      }
      private class NameSegment(string name) : SelectionSegment {
         public string Name { get; private set; } = name;
         public override string ToString() => Name.ToString();

      }
      private class OffsetSegment(string offset) : SelectionSegment {
         public int Offset { get; private set; } = int.Parse(offset.RemoveWhitespace());
         public override string ToString() => Offset.ToString("+#;-#;0");
      }

      /// <summary>
      /// Collects the segments into a list.
      /// </summary>
      /// <remarks>
      /// Construction of instance will guarantted that there is always an even number of elements in the list.
      /// The elments alternate between a Unit and a Nameor Offset segment.
      /// The Index is normaly 0, but can be set by the optional ": <index>" segment at the end of the selection string.
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
      /// <summary>
      /// Create a new empty selection.
      /// </summary>
      public Selection() : base() { }
      private static readonly List<SelectionType> ImportableSelectionType = [ST.CONST,ST.ALGORITHM,ST.MACRO,ST.PROCEDURE,ST.FUNCTION,ST.ACTION,ST.TEST,ST.PREDICATE];
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

         Regex regex = new(@"([A-Z][A-Za-z]*)|(/.*)|(:\s*(?<index>\d+)$)|([+-]\s*\d+)|([a-z][a-z\s]*)", RegexOptions.Compiled);

         bool previousSegmentWasUnit = false;
         bool previousSegmentWasNameOrOffset = false;
         while (selectionString.Length > 0) {
            Match match = regex.Match(selectionString);
            if (!match.Success) break; // No more matches, exit loop
            selectionString = selectionString[match.Length..].Trim(); // Remove the matched segment from the string
            string segment = match.Value.Trim();
            if (char.IsAsciiLetterUpper(segment[0])) {
               // Uppercase segment, identify as a unit type
               SelectionType type = Abbreviation<SelectionType>.Identify(segment.ToUpper());
               if (type == SelectionType.INVALID) {
                  ErrorMessage = $"Invalid selector type: {segment}";
                  return;
               } else {
                  segments.Add(new UnitSegment(type));
                  if (previousSegmentWasUnit) segments.Add(new NameSegment("")); // Add empty name segment if previous was uppercase
                  previousSegmentWasUnit = true;
                  previousSegmentWasNameOrOffset = false;
               }
            } else if (segment.StartsWith(':')) {  // index into the selections
               segments.Index = int.Parse(match.Groups["index"].Value.Trim()); // Parse the index from the segment
               //if (Count == 0) return; // No selections to index into
               //SingleSelection item;
               //if (index > Count) {
               //   item = this.Last(); // If index is out of bounds, select the last item
               //} else {
               //   item = this[index-1]; // Select the item at the specified index (which is 1-based)
               //}
               //Clear();
               //Add(item); // keep only the indexed item
            } else if (previousSegmentWasNameOrOffset) {
               ErrorMessage = $"Invalid selection: {segment} after a name or offset segment";
               return; // Invalid sequence, can't have adjacent name and offsset segments
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
         Debug.Assert(segments.Count > 0 && segments.Count % 2 == 0, "No valid segments found in selection string, or number of selection segments is odd");
         // Verify that the types are in hierarchical order
         for (int i = 0; i < segments.Count - 2; i += 2) {
            if (segments[i].SegmentType == SelectionType.IMPORTED) continue; // Skip IMPORTED segment
            if (!Abbreviation<SelectionType>.AncestorSelectionType(ancestor:segments[i].SegmentType,child:segments[i + 2].SegmentType)) {
               // The types are not in hierarchical order, return without setting selection
               ErrorMessage = $"Invalid selection: {segments[i+2].SegmentType} cannot follow {segments[i].SegmentType}";
               return;
            }
         }

         IEnumerable<NamedElement>? rootObjects;
         // 1. If the selection was rooted, then the starting point is telative to the top Containers (Programs and Modules).
         // 2. Otherwise it is (in principle) relative to the previous focus. Let F be the prevous focus object and let
         //    S0 be the type of the first segment. There are two cases.
         //    a. F is higher in the type hierachy than S0. In this case, the search starts from F.
         //    b. F is lower in the type hierarcy than S0. F is ignored and this case is equivalent to 1.
         if (isRooted || ! Abbreviation<SelectionType>.AncestorSelectionType(Focus.Current.SelectionType, segments[0].SegmentType)) {
            rootObjects = null;
         } else {
            rootObjects = Focus.Current.Object!.DescendantElements(); 
            // The selection is relative to the current focus. TODO: subelements are being ignored
         }

         // Use the segments to succesively narrow down the selection.

         for (int segNo = 0; segNo < segments.Count; segNo += 2) {
            SelectionType segmentType = segments[segNo].SegmentType;
            string segmentName = segments[segNo + 1].SegmentName;
            switch (segmentType) {
               case SelectionType.PROGRAM:
                  RestrictSelected<Program>(segmentName);
                  break;
               case SelectionType.MODULE:
                  RestrictSelected<Module>(segmentName);
                  break;
               case SelectionType.LAYER:
                  RestrictSelected<Layer>(segmentName);
                  break;
               case SelectionType.SECTION:
                  RestrictSelected<Section>(segmentName);
                  break;
               case SelectionType.ALGORITHM:
                  RestrictSelected<Algorithm>(segmentName,alg => !alg.IsImported);
                  break;
               case SelectionType.PROCEDURE:
                  RestrictSelected<Procedure>(segmentName,alg=>!alg.IsImported);
                  break;
               case SelectionType.MACRO:
                  RestrictSelected<Macro>(segmentName,alg=>!alg.IsImported);
                  break;
               case SelectionType.FUNCTION:
                  RestrictSelected<Algorithm>(segmentName, alg => alg.IsFunction && !alg.IsImported);
                  break;
               case SelectionType.ACTION:
                  RestrictSelected<Algorithm>(segmentName, alg => alg.IsAction && !alg.IsImported);
                  break;
               case SelectionType.TEST:
                  RestrictSelected<Algorithm>(segmentName, alg => alg.IsTest && !alg.IsImported);
                  break;
               case SelectionType.PREDICATE:
                  RestrictSelected<Algorithm>(segmentName, alg => alg.IsPredicate && !alg.IsImported);
                  break;
               case SelectionType.CONST:
                  RestrictSelected<Const>(segmentName,con=>!con.IsImported);
                  break;
               case SelectionType.VAR:
                  RestrictSelected<Var>(segmentName);
                  break;
               case SelectionType.LIST:
                  RestrictSelected<LIST>(segmentName);
                  break;
               case SelectionType.IMPORTED:
                  RestrictSelected<CDL2Object>(segmentName,obj=>obj.IsImported);
                  break;
               case SelectionType.INVALID:
                  ErrorMessage = $"Unrecognized selection type";
                  return;
               default:
                  ErrorMessage = $"Unimplemented selection type: {segmentType}";
                  return; // Unsupported type for now
            }
         }

         if (rootObjects is not null) {
            if (segments.Index < 1) {
               foreach (NamedElement? obj in rootObjects) if (obj is not null) Add(new SingleSelection(obj));
            } else {
               Add(new SingleSelection(rootObjects.ElementAt(Math.Min(segments.Index,segments.Count)-1)));
            }
         }

         // Restrict the selection using the type and name from the current segment
         // TODO: Add problem reporting?
         void RestrictSelected<T>(string segmentName, Func<T, bool>? pred = null) where T : NamedElement {
            if (rootObjects is null) {
               if (Database.TryGetNamedElements<T>(segmentName, out IEnumerable<T>? roots) && roots is not null) {
                  rootObjects = roots;
               }
            } else if (Database.TryGetNamedElements<T>(rootObjects, segmentName, out IEnumerable<T>? elements) && elements is not null) {
               rootObjects = elements;               
            }
            if (rootObjects is not null && pred is not null) rootObjects = rootObjects.Where(e => pred((T)e));
         }
      }
   }

   /// <summary>
   /// Provides fuctionality releated to the current object, the Focus, of the CDL2 Laboratory
   /// </summary>
   public class Focus {
      /// <summary>
      /// The focus can be pushed or popded to allow for easier navigation.
      /// It is not preserved accross sessions.
      /// </summary>
      public static readonly Stack<Focus> Stack = [];
      static Focus() => Stack.Push(new Focus());

      public static Focus Current => Stack.Peek();

      public static void Push() => Stack.Push(new Focus());
      public static void Pop() => Stack.Pop();

      public static void SetBookmark(string bookmarkName) {
         if (string.IsNullOrWhiteSpace(bookmarkName)) return;
         if (Database.Instance.Bookmarks.ContainsKey(bookmarkName)) {
            Database.Instance.Bookmarks[bookmarkName] = Current;
         } else {
            Database.Instance.Bookmarks.Add(bookmarkName, Current);
         }
      }
      public static bool RestoreBookmark(string bookmarkName, bool push = false) {
         if (string.IsNullOrWhiteSpace(bookmarkName)) return false;
         if (Database.Instance.Bookmarks.TryGetValue(bookmarkName, out Focus? bookmarkedFocus)) {
            if (!push) Stack.Pop();
            Stack.Push(bookmarkedFocus);
            return true;
         }
         return false;
      }
      public static void RemoveBookmark(string bookmarkName) {
         if (string.IsNullOrWhiteSpace(bookmarkName)) return;
         Database.Instance.Bookmarks.Remove(bookmarkName);
      }
      public static void ClearBookmarks() => Database.Instance.Bookmarks.Clear();

      [JsonInclude, JsonPropertyOrder(0)]
      public SingleSelection Selection = SingleSelection.Empty;
      [JsonIgnore]
      public SelectionType SelectionType => Selection.Object?.SelectionType ?? SelectionType.INVALID;

      [JsonConstructor]
      public Focus() { }
      public Focus(SingleSelection selection) => Selection = selection;
      public Focus(Selection selection) => Selection = selection.Count > 0 ? selection.First() : SingleSelection.Empty;

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
         }
         Stack.Push(new Focus(selection));
         return true;
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
         if (Object == null) return "Nothing";
         string focusString = Object.FQDN();
         // TODO: Add more details based on SubObjectDepth and SubObjectOrdinal
         return focusString;
      }
   }
}

