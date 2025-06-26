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

   public class SelectionSegment {
      public SelectionType SegmentType => this is UnitSegment unit ? unit.Type : SelectionType.INVALID;
      public string SegmentName => this is NameSegment id ? id.Name : "";
   }
   public class  UnitSegment(SelectionType type) : SelectionSegment {
      public SelectionType Type { get; set; } = type;
   }
   public class NameSegment(string name) : SelectionSegment {
      public string Name { get; set; } = name;
   }

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
      /// <summary>
      /// Create a new empty selection.
      /// </summary>
      public Selection() : base() { }
      /// <summary>
      /// Create a new selection with the given object.
      /// </summary>
      /// <param name="obj">The object to select.</param>
      public Selection(string focusString) : base() {
         if (string.IsNullOrWhiteSpace(focusString)) return;

         // Match alternating patterns of uppercase and lowercase segments
         Regex regex = new Regex(@"([A-Z][A-Za-z]*)|(/.*)|([a-z][a-z\s]*)", RegexOptions.Compiled);
         MatchCollection matches = regex.Matches(focusString);

         List<SelectionSegment> parts = [];
         bool expectUppercase = true; // Start expecting uppercase

         foreach (Match match in matches) {
            string segment = match.Value;
            bool isUppercase = char.IsUpper(segment[0]);

            // Check if we're following the alternating pattern
            if ((expectUppercase && !isUppercase) || (!expectUppercase && isUppercase)) {
               // Pattern violation - not alternating as expected
               return;
            }

            if (expectUppercase) {
               SelectionType type = Abbreviation<SelectionType>.Identify(segment.ToUpper());
               if (type == SelectionType.INVALID) return;
               parts.Add(new UnitSegment(type));
            } else {
               parts.Add(new NameSegment(segment)); // Add as name segment
            }
            expectUppercase = !expectUppercase; // Toggle for next iteration
         }

         // Using the parts locate the actual focus
         // Simplistic for now to enable testing of commands that need the focus
         if (parts.Count == 0) return; // No valid parts found
         if (parts.Count % 2 == 1) parts.Add(new NameSegment("")); // Add an empty name segment
         int segNo = 0;
         SelectionType segmentType = parts[segNo++].SegmentType;
         string segmentName = parts[segNo].SegmentName;
         switch (segmentType) {
            case SelectionType.PROGRAM:
               AddSelected<Program>(segmentName);
               break;
            case SelectionType.MODULE:
               AddSelected<Module>(segmentName);
               break;
            case SelectionType.LAYER:
               AddSelected<Layer>(segmentName);
               break;
            case SelectionType.SECTION:
               AddSelected<Section>(segmentName);
               break;
            case SelectionType.ALGORITHM:
               AddSelected<Algorithm>(segmentName,alg => !alg.IsImported);
               break;
            case SelectionType.PROCEDURE:
               AddSelected<Procedure>(segmentName,alg=>!alg.IsImported);
               break;
            case SelectionType.MACRO:
               AddSelected<Macro>(segmentName,alg=>!alg.IsImported);
               break;
            case SelectionType.FUNCTION:
               AddSelected<Algorithm>(segmentName, alg => alg.IsFunction && !alg.IsImported);
               break;
            case SelectionType.ACTION:
               AddSelected<Algorithm>(segmentName, alg => alg.IsAction && !alg.IsImported);
               break;
            case SelectionType.TEST:
               AddSelected<Algorithm>(segmentName, alg => alg.IsTest && !alg.IsImported);
               break;
            case SelectionType.PREDICATE:
               AddSelected<Algorithm>(segmentName, alg => alg.IsPredicate && !alg.IsImported);
               break;
            case SelectionType.CONST:
               AddSelected<Const>(segmentName,con=>!con.IsImported);
               break;
            case SelectionType.VAR:
               AddSelected<Var>(segmentName);
               break;
            case SelectionType.LIST:
               AddSelected<LIST>(segmentName);
               break;
            case SelectionType.IMPORTED:
               AddSelected<CDL2Object>(segmentName,obj=>obj.IsImported);
               break;
            default:
               return; // Unsupported type for now
         }

         return;
      }

      private void AddSelected<T>(string segmentName) where T : NamedElement {
         if (TryGetSelectionElements<T>(segmentName, out IEnumerable<T>? elements)) {
            if (elements is not null) foreach (T mod in elements) Add(new SingleSelection(mod));
         }
      }
      private void AddSelected<T>(string segmentName,Func<T,bool> pred) where T : NamedElement {
         if (TryGetSelectionElements<T>(segmentName, out IEnumerable<T>? elements)) {
            if (elements is not null) foreach (T mod in elements.Where(pred)) Add(new SingleSelection(mod));
         }
      }

      private static bool TryGetSelectionElements<T>(string segmentName, out IEnumerable<T>? elements) where T : NamedElement {
         elements = null;
         if (segmentName != "") {
            if (Database.Instance.TryGetNamedElements<T>(segmentName, out elements)) {
               return true;
            } else {
               return false; // element not found
            }
         } else {
            // TODO Go up the focus chain if it is higher than the current focus, or go down otherwise
            elements = Database.NamedElementsOfType<T>(asList: false);
            return true;
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
      public static bool SetFocus(string focusString) {
         if (string.IsNullOrWhiteSpace(focusString)) return false;

         Selection selection = new(focusString);
         if (selection.Count == 0) return false;
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
