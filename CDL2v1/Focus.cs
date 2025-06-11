using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CDL2v1 {

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
      public Guid ElementGuid = Guid.Empty;
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

      [JsonIgnore]
      public NamedElement? Element {
         get => ElementGuid == Guid.Empty ? null : NamedElement.From<NamedElement>(ElementGuid);
         set => ElementGuid = value?.GUID ?? Guid.Empty;
      }
   }
}
