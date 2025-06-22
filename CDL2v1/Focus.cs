using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CDL2v1 {

   public class FocusSegment {
      public FocusType SegmentType => this is UnitSegment unit ? unit.Type : FocusType.INVALID;
      public string SegmentName => this is NameSegment id ? id.Name : "";
   }
   public class  UnitSegment(FocusType type) : FocusSegment {
      public FocusType Type { get; set; } = type;
   }
   public class NameSegment(string name) : FocusSegment {
      public string Name { get; set; } = name;
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

      /// <summary>
      /// The currently focused NamedElement represented as it's Guid.
      /// null when nothing is focused.      /// 
      /// </summary>
      /// <remarks>
      /// When the element is a procedure, then the entire procedure is focusesed.
      /// To dive in, the top level group will be the element. In deeper groups that group will be the element.
      /// </remarks>
      [JsonInclude, JsonPropertyOrder(0)]
      public Guid ObjectGuid = Guid.Empty;
      /// <summary>
      /// The currently focused suebelment.
      /// 0 if the focus is on the entrie NamedElement, > 0 otherwise.
      /// For Const and Macro, this will be 1 if a const or macro element is focused and SubObjectOrdinal contains the position within elements with the first element being 0.
      /// For a List, this will be 1, with SubObjectOrdinal 0 for the LWB and 1 for the UPB.
      /// For a Group,
      ///    0 for the group itself,
      ///    1 for the alterantives of the group, the ordinal is the alternative number (0 for the first alternative, 1 for the second, etc.)      ///    
      ///    2 for the calls in the alternative, the ordinal is the call number (0 for the first call, 1 for the second, etc.)
      ///    3 for the actual args of a call, the ordinal is the argument number (0 for the first argument, 1 for the second, etc.)
      ///    3 also when the ordi
      /// </summary>
      [JsonInclude, JsonPropertyOrder(1)]
      public int SubObjectDepth = 0;
      /// <summary>
      /// The ordianl of the focused sub element for types where this makes sense, 0 otherwise.
      /// </summary>
      [JsonInclude, JsonPropertyOrder(2)]
      public int SubObjectOrdinal = 0;
      [JsonInclude, JsonPropertyOrder(3)]
      public int ActualArgOrdinal = -1; // -1 means not applicable, 0 means the first actual argument, 1 the second, etc.
      /// <summary>
      /// Specifies the focused list type when the NamedElement is a Container.
      /// When used, Depth will be set to 1 (but not relevant) and Ordinal to the postion in the list.
      /// It may be
      /// NONE when other then a PROGRAM, MODULE, or SECTION is in focus.
      /// PRELUDE, ROOT, POSTLUDE for PROGRAM, MODULE and SECTION
      /// ABSTR, EXT, INV, IMPORT, EXPORT for SECTION.
      /// </summary>
      [JsonInclude, JsonPropertyOrder(4)]
      public RW ListType = RW.NONE;

      /// <summary>
      /// Parse the focus string and set the focus if it is valid.
      /// Currently supports format of the form: RW1 name1 RW2 name2 ... where RW is a reserved word (all capital letters)
      /// and name is an ID (starting with lowercase and can contain special characters).
      /// </summary>
      /// <param name="focusString">String in format "UPPERlowerUPPERlower"</param>
      /// <returns>True if focus was successfully set, false otherwise</returns>
      public static bool SetFocus(string focusString) {
         if (string.IsNullOrWhiteSpace(focusString)) return false;
         
         // Match alternating patterns of uppercase and lowercase segments
         Regex regex = new Regex(@"([A-Z][a-z]+)|([a-z][a-z\s]*)", RegexOptions.Compiled);
         MatchCollection matches = regex.Matches(focusString);

         List<FocusSegment> parts = [];
         bool expectUppercase = true; // Start expecting uppercase
         
         foreach (Match match in matches) {
            string segment = match.Value;
            bool isUppercase = char.IsUpper(segment[0]);
            
            // Check if we're following the alternating pattern
            if ((expectUppercase && !isUppercase) || (!expectUppercase && isUppercase)) {
               // Pattern violation - not alternating as expected
               return false;
            }
            
            if (expectUppercase) {
               FocusType type = Abbreviation<FocusType>.Identify(segment.ToUpper());
               if (type == FocusType.INVALID) return false;
               parts.Add(new UnitSegment(type));
            } else {
               parts.Add(new NameSegment(segment)); // Add as name segment
            }
            expectUppercase = !expectUppercase; // Toggle for next iteration
         }

         // Using the parts locate the actual focus
         // Simplistic for now to enable testing of commands that need the focus
         if (parts.Count == 0) return false; // No valid parts found
         if (parts.Count % 2 == 1) parts.Add(new NameSegment("")); // Add an empty name segment
         Focus newFocus = new();
         int segNo = 0;
         FocusType segmentType = parts[segNo++].SegmentType;
         string segmentName = parts[segNo].SegmentName;
         switch (segmentType) {
            case FocusType.PROGRAM:
               if (TryGetFocusElement<Program>(segmentName,Database.Instance.FirstProgram,out Program? prog)) {
                  newFocus.Object = prog;
               } else {
                  return false;
               }
               break;
            case FocusType.MODULE:
               if (TryGetFocusElement<Module>(segmentName,Database.NamedElementsOfType<Module>(asList:false).First(), out Module? mod)) {
                  newFocus.Object = mod;
               } else {
                  return false;
               }
               break;
            case FocusType.SECTION:
               break;
            default:
               return false; // Unsupported type for now
         }

         // Add the new focus to the stack
         Stack.Push(newFocus);
         return true;
      }

      private static bool TryGetFocusElement<T>(string segmentName,T? defaultElement,out T? element) where T : NamedElement {
         element = null;
         if (segmentName != "") {
            if (Database.Instance.TryGetNamedElements<T>(segmentName, out IEnumerable<T>? elements)) {
               element = elements.First(); // Get the first matching element
               return true;
            } else {
               return false; // element not found
            }
         } else {
            // TODO Go up the focus chain if it is higher than the currnet focus, or go down otherwise
            element = defaultElement; // No specific name, return the default element  
            return true;
         }
      }

      /// <summary>
      /// Try to find a named element based on the uppercase type and lowercase name
      /// </summary>
      private static bool TryGetObjectFromPattern(string upperType, string lowerName, out NamedElement? element) {
         element = null;
         
         // Map the uppercase part to a specific type of element to search for
         switch (upperType) {
            case "PROG":
               element = Database.Instance.ProgramByName(lowerName);
               break;
            case "MOD":
               element = Database.Instance.ModuleByName(lowerName);
               break;
            case "LAY":
               // Would need to search through layers of relevant module
               // This is a simplification - you'd need proper hierarchy traversal
               break;
            case "SEC":
               // Would need to search through sections in a layer
               break;
            // Add other cases as needed
         }
         
         return element != null;
      }

      /// <summary>
      /// Process the second pair of uppercase/lowercase parts to set additional focus properties
      /// </summary>
      private static void ProcessSecondPair(Focus focus, string upperType, string lowerDetail) {
         // Based on the uppercase type, set appropriate properties
         if (Enum.TryParse<RW>(upperType, out RW listType)) {
            focus.ListType = listType;
            focus.SubObjectDepth = 1;
            
            // Try to parse the lowercase detail as an index/ordinal if applicable
            if (int.TryParse(lowerDetail, out int ordinal)) {
               focus.SubObjectOrdinal = ordinal;
            }
         }
      }

      /// <summary>
      /// The currently focused NamedElement
      /// </summary>
      [JsonIgnore]
      public NamedElement? Object {
         get => ObjectGuid == Guid.Empty ? null : NamedElement.From<NamedElement>(ObjectGuid);
         set => ObjectGuid = value?.GUID ?? Guid.Empty;
      }

      public override string ToString() {
         if (Object == null) return "Nothing";
         string focusString = Object.FQDN();
         // TODO: Add more details based on SubObjectDepth and SubObjectOrdinal
         return focusString;
      }
   }
}
