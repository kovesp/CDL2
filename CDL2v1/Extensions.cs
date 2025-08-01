// <auto-gen>
//=======================================================================
// <copyright file="Extensions.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-02-18</creation-date>
// 
// <summary>
//   Contains a few support clases and extension methods for the rest of the project.
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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.CommandLine.Parsing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Windows.Media;
using System.Reflection.Emit;
using System.Diagnostics.Metrics;
using System.Xml.Serialization;
using System.Windows;
using System.Text.Json.Serialization;
using System.Collections;
using System.Text.Json;

namespace CDL2v1 {  
   public class Set<T> : HashSet<T> {
      public Set() { }
      public Set(ICollection<T> collection) : base(collection) { }
      public Set(IEnumerable<T> enumerable) {
         foreach (T item in enumerable) Add(item);
      }
   }

   public class GuidList<T> : ICollection<T> where T : NamedElement {
      [JsonInclude]
      public List<Guid> guids = [];
      public GuidList() { }

      public int Count => guids.Count;

      public bool IsReadOnly => false;

      public void Add(T element) {
         if (element is null) throw new ArgumentNullException(nameof(element));
         guids.Add(element.GUID);
      }

      public void Clear() => guids.Clear();
      public bool Contains(T item) => guids.Contains(item.GUID);
      public void CopyTo(T[] array, int arrayIndex) => throw new NotImplementedException();
      public IEnumerator<T> GetEnumerator() {
         foreach (Guid guid in guids) {
            if (Database.Instance.NamedElements.TryGetValue(guid, out NamedElement? namedElement) && namedElement is T value) {
               yield return value;
            }
         }
      }
      public bool Remove(T item) => guids.Remove(item.GUID);
      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
   }

   /// <summary>
   /// A stack of int-s whose top element can be modified and compared to an int.
   /// </summary>
   public class ModifiableStack : Stack<int> {
      public ModifiableStack(int minimum=0) : base() => Minimum = minimum;
      public ModifiableStack(int capacity,int minimum) : base(capacity) => Minimum = minimum;
      public ModifiableStack(IEnumerable<int> collection,int minimum=0) : base(collection) => Minimum = minimum;
      private readonly int Minimum;

      /// <summary>
      /// Increment the top elment of the stack.
      /// </summary>
      /// <param name="stack"></param>
      /// <returns></returns>
      public static ModifiableStack operator ++(ModifiableStack stack) {
         stack.Push(stack.Pop() + 1);
         return stack;
      }
      /// <summary>
      /// Decrement the top element of the stack, but do not allow it to go below Minimum.
      /// </summary>
      /// <param name="stack"></param>
      /// <returns></returns>
      public static ModifiableStack operator --(ModifiableStack stack) {
         if (stack.Peek() > 0) stack.Push(Math.Max(stack.Minimum,stack.Pop() - 1));
         return stack;
      }
      public static ModifiableStack operator +(ModifiableStack stack,int v) {
         if (stack.Peek() > 0) stack.Push(Math.Max(stack.Minimum, stack.Pop() + v));
         return stack;
      }
      public static ModifiableStack operator -(ModifiableStack stack, int v) {
         if (stack.Peek() > 0) stack.Push(Math.Max(stack.Minimum,stack.Pop() - v));
         return stack;
      }

      /// <summary>
      /// Compare the top element of the stack with a value.
      /// </summary>
      /// <param name="stack"></param>
      /// <param name="value"></param>
      /// <returns></returns>
      /// <exception cref="InvalidOperationException"></exception>
      public static bool operator >=(ModifiableStack stack, int value) {
         if (stack.Count == 0) {
            throw new InvalidOperationException("Cannot compare to empty stack.");
         }
         return stack.Peek() >= value;
      }
      public static bool operator >(ModifiableStack stack, int value) {
         if (stack.Count == 0) {
            throw new InvalidOperationException("Cannot compare to empty stack.");
         }
         return stack.Peek() > value;
      }
      public static bool operator <=(ModifiableStack stack, int value) {
         if (stack.Count == 0) {
            throw new InvalidOperationException("Cannot compare to empty stack.");
         }
         return stack.Peek() <= value;

      }
      public static bool operator <(ModifiableStack stack, int value) {
         if (stack.Count == 0) {
            throw new InvalidOperationException("Cannot compare to empty stack.");
         }
         return stack.Peek() < value;
      }
      public static bool operator ==(ModifiableStack stack, int value) {
         if (stack.Count == 0) {
            throw new InvalidOperationException("Cannot compare to empty stack.");
         }
         return stack.Peek() == value;
      }
      public static bool operator !=(ModifiableStack stack, int value) {
         if (stack.Count == 0) {
            throw new InvalidOperationException("Cannot compare to empty stack.");
         }
         return stack.Peek() != value;
      }
      public override bool Equals(object? obj) {
         if (obj is ModifiableStack stack) {
            return this.SequenceEqual(stack);
         }
         return false;
      }
      public override int GetHashCode() {
         int hash = 17;
         foreach (int i in this) {
            hash = hash * 31 + i.GetHashCode();
         }
         return hash;
      }
      /// <summary>
      /// Set the top element of the stack to a value.
      /// </summary>
      /// <param name="v">The value >= Minimum to set, default is 0.</param>
      internal void SetTop(int? v=null) {
         if (Count > 0) Pop();
         Push(Math.Max(Minimum,v??Minimum));
      }
      /// <summary>
      /// Reset the top element of the stack to 0.
      /// </summary>
      internal void ResetTop() => SetTop();
   }

   /// <summary>
   /// Represents a stack with a bounded maximum capacity. When the stack reaches 
   /// its capacity and a new item is pushed, the oldest item is removed.
   /// </summary>
   /// <typeparam name="T">The type of elements in the stack.</typeparam>
   /// <remarks>Generated by Copilot.</remarks>
   public class BoundedStack<T> : IEnumerable<T> {
      private T[] _items;
      private int _size;
      private int _head;  // Points to the next position to insert an item
      private int _tail;  // Points to the oldest item in the circular buffer

      /// <summary>
      /// Gets or sets the maximum number of elements the <see cref="BoundedStack{T}"/> can hold.
      /// When reducing capacity, excess elements from the bottom of the stack will be removed.
      /// </summary>
      public int Capacity {
         get => _items.Length;
         set {
            if (value < 1)
               throw new ArgumentOutOfRangeException(nameof(value), "Capacity must be positive.");
            
            // If new capacity is the same, no need to resize
            if (value == _items.Length)
               return;
            
            // Create new array with the desired capacity
            T[] newItems = new T[value];
         
            if (_size > 0) {
               // Copy elements, starting from the newest (top of stack)
               int elementsToCopy = Math.Min(_size, value);
            
               // Start copying from the top element (_head - 1) moving down
               for (int i = 0; i < elementsToCopy; i++) {
                  int sourceIndex = (_head - 1 - i + _items.Length) % _items.Length;
                  newItems[value - 1 - i] = _items[sourceIndex];
               }
            
               // Update size and pointers
               _size = elementsToCopy;
               _tail = value - _size;
               _head = value;  // Head points to the position after the newest item
            } else {
               // Stack is empty
               _tail = 0;
               _head = 0;
            }
         
            _items = newItems;
         }
      }
   
      /// <summary>
      /// Gets the number of elements contained in the <see cref="BoundedStack{T}"/>.
      /// </summary>
      public int Count => _size;
   
      /// <summary>
      /// Gets a value indicating whether the <see cref="BoundedStack{T}"/> is empty.
      /// </summary>
      public bool IsEmpty => _size == 0;
   
      /// <summary>
      /// Gets a value indicating whether the <see cref="BoundedStack{T}"/> is at full capacity.
      /// </summary>
      public bool IsFull => _size == _items.Length;

      /// <summary>
      /// Initializes a new instance of the <see cref="BoundedStack{T}"/> class with the specified capacity.
      /// </summary>
      /// <param name="capacity">The maximum capacity of the stack.</param>
      /// <exception cref="ArgumentOutOfRangeException">
      /// Thrown when capacity is less than 1.
      /// </exception>
      public BoundedStack(int capacity) {
         if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
      
         _items = new T[capacity];
         _size = 0;
         _head = 0;
         _tail = 0;
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="BoundedStack{T}"/> class with 
      /// the specified capacity and items.
      /// </summary>
      /// <param name="capacity">The maximum capacity of the stack.</param>
      /// <param name="collection">The collection to copy elements from.</param>
      /// <exception cref="ArgumentOutOfRangeException">
      /// Thrown when capacity is less than 1.
      /// </exception>
      public BoundedStack(int capacity, IEnumerable<T> collection) : this(capacity) {
         foreach (T item in collection) {
            Push(item);
         }
      }
   
      /// <summary>
      /// Pushes an item onto the stack. If the stack is at capacity, 
      /// the oldest item is removed.
      /// </summary>
      /// <param name="item">The item to push onto the stack.</param>
      /// <returns>True if an item was removed from the bottom of the stack, otherwise false.</returns>
      public bool Push(T item) {
         bool itemRemoved = false;
      
         if (IsFull) {
            // When full, the oldest item gets overwritten
            _items[_tail] = default!;  // Clear reference to help GC
            _tail = (_tail + 1) % _items.Length;
            itemRemoved = true;
         } else {
            // Increase size when not full
            _size++;
         }
      
         _items[_head] = item;
         _head = (_head + 1) % _items.Length;
      
         return itemRemoved;
      }
   
      /// <summary>
      /// Returns the item at the top of the stack without removing it.
      /// </summary>
      /// <returns>The item at the top of the stack.</returns>
      /// <exception cref="InvalidOperationException">
      /// Thrown when the stack is empty.
      /// </exception>
      public T Peek() {
         if (IsEmpty)
            throw new InvalidOperationException("The stack is empty.");
      
         int topIndex = (_head - 1 + _items.Length) % _items.Length;
         return _items[topIndex];
      }
   
      /// <summary>
      /// Removes and returns the item at the top of the stack.
      /// </summary>
      /// <returns>The item removed from the top of the stack.</returns>
      /// <exception cref="InvalidOperationException">
      /// Thrown when the stack is empty.
      /// </exception>
      public T Pop() {
         if (IsEmpty)
            throw new InvalidOperationException("The stack is empty.");
      
         _head = (_head - 1 + _items.Length) % _items.Length;
         T item = _items[_head];
         _items[_head] = default!;  // Clear reference to help GC
         _size--;
      
         return item;
      }
   
      /// <summary>
      /// Removes all items from the stack.
      /// </summary>
      public void Clear() {
         Array.Clear(_items, 0, _items.Length);
         _size = 0;
         _head = 0;
         _tail = 0;
      }

      /// <summary>
      /// Returns an enumerator that iterates through the stack from top to bottom.
      /// </summary>
      /// <returns>An enumerator for the stack.</returns>
      public IEnumerator<T> GetEnumerator() {
         if (IsEmpty)
            yield break;
      
         int current = _head;
         for (int i = 0; i < _size; i++) {
            current = (current - 1 + _items.Length) % _items.Length;
            yield return _items[current];
         }
      }
   
      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
   
      /// <summary>
      /// Copies the stack elements to a new array in top-to-bottom order.
      /// </summary>
      /// <returns>A new array containing the stack elements.</returns>
      public T[] ToArray() {
         T[] result = new T[_size];
         int index = 0;
      
         foreach (T item in this) {
            result[index++] = item;
         }
      
         return result;
      }
   
      /// <summary>
      /// Returns a string representation of the stack.
      /// </summary>
      /// <returns>A string representation of the stack.</returns>
      public override string ToString() {
         if (IsEmpty)
            return "[]";
      
         StringBuilder sb = new StringBuilder("[");
         bool first = true;
      
         foreach (T item in this) {
            if (!first) {
               sb.Append(", ");
            }
            sb.Append(item);
            first = false;
         }
      
         sb.Append(']');
         return sb.ToString();
      }
   }

   public static class Extensions {
      public static bool TRUE<T>(T _) => true;

      /// <summary>
      /// Eauivalent to pred ?? _ => true, but that syntax doesn't work.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="predicate"></param>
      /// <returns></returns>
      public static Predicate<T> OrTrue<T>(this Predicate<T> predicate) => predicate ?? TRUE;
      public static Func<T, bool> OrTrue<T>(this Func<T, bool> predicate) => predicate ?? TRUE;

      public static IEnumerable<T> OptMap<T>(this IEnumerable<T> source,bool cond, Func<IEnumerable<T>, IEnumerable<T>> func) => cond ? func(source) : source;
      public static IEnumerable<T> OptWhere<T>(this IEnumerable<T> source, Func<T,bool>? pred=null) => pred is not null ? source.Where(pred) : source;

      public static Type AsType(this string typeName) {
         if (string.IsNullOrWhiteSpace(typeName)) {
            throw new ArgumentException("Type name cannot be null or whitespace.", nameof(typeName));
         }
         Type? type = Type.GetType(typeName);
         type ??= Type.GetType($"{typeof(Module).Namespace}.{typeName}");
         if (type == null) {
            throw new TypeLoadException($"Could not load type '{typeName}'.");
         }
         return type;
      }

      public static bool IsValidFileName(this string? fileName) {
         if (string.IsNullOrWhiteSpace(fileName)) {
            return false;
         } else {
            return fileName.All(ch => !Path.GetInvalidFileNameChars().Contains(ch));
         }
      }

      /// <summary>
      /// Convert the strig to the case of the first letter.
      /// </summary>
      /// <param name="str"></param>
      /// <returns></returns>
      public static string ToFirstLetterCase(this string str) => string.IsNullOrEmpty(str) ? str : char.IsUpper(str[0]) ? str.ToUpper() : str.ToLower();

      /// <summary>
      /// Return true if the string is composed of alphanumeric characters only.
      /// </summary>
      /// <param name="input"></param>
      /// <returns></returns>
      public static bool IsAlphanumeric(this string input) {
         if (string.IsNullOrEmpty(input)) return false;
         foreach (char c in input) if (!char.IsLetterOrDigit(c)) return false;
         return true;
      }
      /// <summary>
      /// Return the string with whitespece removed.
      /// </summary>
      /// <param name="input"></param>
      /// <returns></returns>
      public static string RemoveWhitespace(this string input) => string.IsNullOrEmpty(input) ? input : Regex.Replace(input, @"\s+", "", RegexOptions.Compiled);

      /// <summary>
      /// Formats a byte count with appropriate unit suffix
      /// </summary>
      /// <param name="bytes">Number of bytes</param>
      /// <param name="useDecimalUnits">If true, uses decimal units (KB, MB, GB), otherwise binary units (KiB, MiB, GiB)</param>
      /// <returns>Formatted string representing size with appropriate unit</returns>
      public static string FormatByteSize(this long bytes, bool useDecimalUnits = false) {
         string[] binarySuffixes = { "bytes", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB" };
         string[] decimalSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB" };

         string[] suffixes = useDecimalUnits ? decimalSuffixes : binarySuffixes;
         int factor = useDecimalUnits ? 1000 : 1024;

         if (bytes == 0)
            return "0 bytes";

         int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, factor)));
         double num = Math.Round(bytes / Math.Pow(factor, place), 0);

         // Don't use the "bytes" suffix for values larger than 1
         if (place == 0 && num > 1)
            return $"{num} bytes";
         else if (place == 0)
            return $"{num} byte";
         else
            return $"{num} {suffixes[place]}";
      }

      /// <summary>
      /// Returns a new list containing the elements of the original list combined with the specified items,  ensuring
      /// no duplicate elements are added.
      /// </summary>
      /// <typeparam name="T">The type of elements in the list. Must be a non-nullable type.</typeparam>
      /// <param name="list">The original list to which the items will be added.</param>
      /// <param name="items">The collections of items to add to the list. Duplicate elements will not be added.</param>
      /// <returns>A new list containing the elements of the original list and the specified items, without duplicates.</returns>
      public static List<T> With<T>(this List<T> list, params IEnumerable<T> items) where T : notnull 
         => items.All(item => list.Contains(item)) ? list : [.. list.Union(items)];

      /// <summary>
      /// Return the types that implement the given interface.
      /// </summary>
      /// <typeparam name="TInterface"></typeparam>
      /// <returns></returns>
      public static IEnumerable<Type> GetImplementorsOfInterface<TInterface>() => Assembly.GetExecutingAssembly().GetTypes()
             .Where(type => typeof(TInterface).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);

      /// <summary>
      /// If all elements of objects are siblings of this one, then return the objects in the order of their siblings.
      /// </summary>
      /// <param name="objects"></param>
      /// <returns></returns>
      public static IEnumerable<NamedElement> OrderedAsSiblings(this IEnumerable<NamedElement> objects) {
         Debug.Assert(objects.Any(),"The collection of objects must not be empty.");
         List<Guid> siblings = objects.First().Siblings;
         if (objects.Skip(1).All(obj => siblings.Contains(obj.GUID))) {
            return objects.OrderBy(obj => siblings.IndexOf(obj.GUID));
         } else {
            return objects;
         }
      }


      /// <summary>
      /// Normalize a string to a valid identifier.
      /// </summary>
      /// <param name="str"></param>
      /// <param name="prefix"></param>
      /// <param name="replacement"></param>
      /// <param name="camelCase"></param>
      /// <param name="literalObjectName"></param>
      /// <returns></returns>
      public static string AsIdentifier(this string str,string prefix = "",string replacement = "",bool camelCase = false,bool literalObjectName = false) {
         if (literalObjectName) return str.Replace(" ","");
         if (prefix != "") prefix += "_";
         str = Regex.Replace(str,@"[^\p{L}\d\s]+","_",RegexOptions.Compiled).Trim();
         if (camelCase) {
            return prefix.ToLower() + str.Split(" ").Select((word,i) => i == 0 ? word.ToLower() : char.ToUpper(word[0]) + word[1..].ToLower()).Aggregate((a,b) => a + b);
         } else {
            return prefix.ToLower() + str.ToLower().Replace(" ",replacement);
         }
      }

      /// <summary>
      /// Return the plural of the word word for count.
      /// Does NOT handle words that are not pluralizable, such as "fish" or "sheep".
      /// </summary>
      /// <param name="count">Number of items.</param>
      /// <param name="word">The item name.</param>
      /// <param name="plural">If given the plural of word. Otherwise an s, es, or ies is added as ap appropriate.</param>
      /// <param name="pad">If given, the plural is padded with spaces to the extent of what would be added after inserting the pad.</param>
      /// <param name="countWidth">Width of the count in characters. Default is 3.</param>
      /// <returns></returns>
      public static string Plural(this int count, string word, string? pad = null, string ? plural = null,int countWidth=3) {
         string suffix;
         if (plural is null) {
            if (Regex.IsMatch(word, @"(s|sh|ch|x|z)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)) {
               suffix = "es";
            } else if (Regex.IsMatch(word, @"[^aeiou]y$", RegexOptions.IgnoreCase | RegexOptions.Compiled)) {
               suffix = "ies";
            } else {
               suffix = "s";
            }
            if (count == 1) {
               plural = $"{word}{(pad is not null?pad:"")}{new string(' ',suffix.Length)}";
            } else {
               if (suffix == "ies") {
                  plural = Regex.Replace(word, "y$", "ies", RegexOptions.IgnoreCase | RegexOptions.Compiled);
               } else {
                  plural = word + suffix;
               }
               plural += pad is not null ? pad : "";
            }  
         }
         return $"{string.Format($"{{0,{countWidth}:N0}}", count)} {plural}";
      }
      public static string Plural(this string word,int count,string? plural=null) => count.Plural(word, plural);
      /// <summary>
      /// Dim a color by a factor.
      /// </summary>
      /// <param name="color"></param>
      /// <param name="factor">The 0 <= factor <= 1 to use.</param>
      /// <returns></returns>
      /// <exception cref="ArgumentOutOfRangeException"></exception>
      public static Color DimColor(this Color color,double factor) {
         if (factor < 0 || factor > 1)
            throw new ArgumentOutOfRangeException(nameof(factor),"Factor must be between 0 and 1.");

         return Color.FromArgb(
             color.A,
             (byte)(color.R * factor),
             (byte)(color.G * factor),
             (byte)(color.B * factor)
         );
      }
      /// <summary>
      /// Intensify a color by a factor.
      /// </summary>
      /// <param name="color"></param>
      /// <param name="factor">The factor >= 1 to use.</param>
      /// <returns></returns>
      /// <exception cref="ArgumentOutOfRangeException"></exception>
      public static Color IntensifyColor(this Color color,double factor) {
         if (factor < 1)
            throw new ArgumentOutOfRangeException(nameof(factor),"Factor must be greater than or equal to 1.");

         return Color.FromArgb(
             color.A,
             (byte)Math.Min(255,color.R * factor),
             (byte)Math.Min(255,color.G * factor),
             (byte)Math.Min(255,color.B * factor)
         );
      }
      public static string IntensifyColor(this string color,double factor) => FromHex(color).IntensifyColor(factor).ToHex();
      public static Color DimColor(this string color,double factor) => FromHex(color).DimColor(factor);

      /// <summary>
      /// Convert a hex string to a Color object.
      /// </summary>
      /// <param name="hex"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentException"></exception>
      public static Color FromHex(this string hex) {
         if (string.IsNullOrWhiteSpace(hex))
            throw new ArgumentException("Invalid hex color string",nameof(hex));

         // Ensure the hex string starts with '#'
         if (hex[0] != '#')
            hex = "#" + hex;

         // Use ColorConverter to convert the hex string to a Color object
         return (Color)ColorConverter.ConvertFromString(hex);
      }
      /// <summary>
      /// Convert a Color to a hex string.
      /// </summary>
      /// <param name="color"></param>
      /// <returns></returns>
      public static string ToHex(this Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

      /// <summary>
      /// Convert an IEnumerable to a Set.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="enumerable"></param>
      /// <returns></returns>
      public static Set<T> ToSet<T>(this IEnumerable<T> enumerable) => [.. enumerable];

      public static string WithSpace<T>(this T? obj) => obj == null ? "" : obj.ToString() + " ";

      /// <summary>
      /// Decorate a string with the given decoration.
      /// This means encpsulating the string in a span tag with the given style and the foreground and background colors.
      /// This is only done if the emitter supports decoration.
      /// </summary>
      /// <param name="str"></param>
      /// <param name="emitter"></param>
      /// <param name="element"></param>
      /// <param name="decoration"></param>
      /// <returns></returns>
      internal static string Decorate(this string str,Emitter emitter,SE element,PrettyPrinter.Decoration? decoration=null) {
         if (str == null) return "";
         if (emitter.SupportsDecoration) {
            if (element != SE.AlgorithmName) {
               decoration = PrettyPrinter.Decorators[element];
            } else if (decoration == null) {
            }
            Debug.Assert(decoration != null,$"No decoration for {element}");
            return string.Join("\n",Regex.Split(str,@"\r\n|\r|\n",RegexOptions.Compiled)
                           .Select(str => $"<span fg='{decoration.FG}' bg='{decoration.BG}' style='{decoration.Style}'>{str}</span>"));
         } else {
            return str;
         }
      }
      internal static string Decorate(this RW rw,Emitter emitter,SE element) => rw.ToString().Decorate(emitter,element);
      //internal static string Decorate(this string str,EmitterBase Emitter,SE element) =>str.Decorate(Emitter,element);
      internal static string Decorate(this Token token,Emitter emitter,SE element) => token.TokenString.Decorate(emitter,element);
      internal static string Decorate(this ID id,Emitter emitter,SE element) 
         => /*Id.Comments!.Decorate(Emitter,SE.Comment) +*/ id.Name.Decorate(emitter,element);
      internal static string Decorate(this long i,Emitter emitter) => i.ToString().Decorate(emitter,SE.Number);
      internal static string Decorate(this double d,Emitter emitter) => d.ToString().Decorate(emitter,SE.Number);
      internal static string Decorate(this ID algorithmId,Emitter emitter,PrettyPrinter.Decoration decoration) => algorithmId.ToString().Decorate(emitter,SE.AlgorithmName,decoration);
   }
}

