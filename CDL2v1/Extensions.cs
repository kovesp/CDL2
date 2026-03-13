// <auto-gen>
//=======================================================================
// <copyright file="Extensions.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project rootFextension
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-02-18</creation-date>
// 
// <summary>
//   Contains a few support classes and extension methods for the rest of the project.
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

using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CDL2v1 {
   public class Set<T> : HashSet<T>, ISet<T> {
      public Set() { }
      public Set(ICollection<T> collection) : base(collection) { }
      public Set(IEnumerable<T> enumerable) {
         foreach (T item in enumerable) Add(item);
      }
      public bool TryGetActualValue(T searchValue,[MaybeNullWhen(false)] out T actualValue) => TryGetValue(searchValue,out actualValue);
      public T GetActualValue(T searchValue) => TryGetActualValue(searchValue,out T? actualValue) ? actualValue : throw new KeyNotFoundException();
   }

   public class GuidList<T> : ICollection<T> where T : NamedElement {
      [JsonInclude]
      public List<Guid> guids = [];
      public GuidList() { }

      public int Count => guids.Count;

      public bool IsReadOnly => false;

      public void Add(T element) {
         ArgumentNullException.ThrowIfNull(element);
         guids.Add(element.GUID);
      }

      public void Clear() => guids.Clear();
      public bool Contains(T item) => guids.Contains(item.GUID);
      public void CopyTo(T[] array,int arrayIndex) => throw new NotImplementedException();
      public IEnumerator<T> GetEnumerator() {
         foreach (Guid guid in guids) {
            if (Database.Instance.NamedElements.TryGetValue(guid,out NamedElement? namedElement) && namedElement is T value) {
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
      public ModifiableStack(int minimum = 0) : base() => Minimum = minimum;
      public ModifiableStack(int capacity,int minimum) : base(capacity) => Minimum = minimum;
      public ModifiableStack(IEnumerable<int> collection,int minimum = 0) : base(collection) => Minimum = minimum;
      private readonly int Minimum;

      /// <summary>
      /// Increment the top element of the stack.
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
         if (stack.Peek() > 0) stack.Push(Math.Max(stack.Minimum,stack.Pop() + v));
         return stack;
      }
      public static ModifiableStack operator -(ModifiableStack stack,int v) {
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
      public static bool operator >=(ModifiableStack stack,int value) {
         if (stack.Count == 0) {
            throw new InvalidOperationException("Cannot compare to empty stack.");
         }
         return stack.Peek() >= value;
      }
      public static bool operator >(ModifiableStack stack,int value) {
         if (stack.Count == 0) {
            throw new InvalidOperationException("Cannot compare to empty stack.");
         }
         return stack.Peek() > value;
      }
      public static bool operator <=(ModifiableStack stack,int value) {
         if (stack.Count == 0) {
            throw new InvalidOperationException("Cannot compare to empty stack.");
         }
         return stack.Peek() <= value;

      }
      public static bool operator <(ModifiableStack stack,int value) {
         if (stack.Count == 0) {
            throw new InvalidOperationException("Cannot compare to empty stack.");
         }
         return stack.Peek() < value;
      }
      public static bool operator ==(ModifiableStack stack,int value) {
         if (stack.Count == 0) {
            throw new InvalidOperationException("Cannot compare to empty stack.");
         }
         return stack.Peek() == value;
      }
      public static bool operator !=(ModifiableStack stack,int value) {
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
      internal void SetTop(int? v = null) {
         if (Count > 0) Pop();
         Push(Math.Max(Minimum,v ?? Minimum));
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
      public T[] _items;
      public int _size;
      public int _head;  // Points to the next position to insert an item
      public int _tail;  // Points to the oldest item in the circular buffer

      /// <summary>
      /// Gets or sets the maximum number of elements the <see cref="BoundedStack{T}"/> can hold.
      /// When reducing capacity, excess elements from the bottom of the stack will be removed.
      /// </summary>
      public int Capacity {
         get => _items.Length;
         set {
            if (value < 1)
               throw new ArgumentOutOfRangeException(nameof(value),"Capacity must be positive.");

            if (value == _items.Length)
               return;

            T[] newItems = new T[value];

            if (_size > 0) {
               int elementsToCopy = Math.Min(_size,value);

               // Dispose items that will be discarded (from the bottom of the stack)
               int itemsToDiscard = _size - elementsToCopy;
               for (int i = 0 ; i < itemsToDiscard ; i++) {
                  int discardIndex = (_tail + i) % _items.Length;
                  DisposeIfDisposable(_items[discardIndex]);
               }

               // Copy the elements to keep (from newest to oldest)
               for (int i = 0 ; i < elementsToCopy ; i++) {
                  int sourceIndex = (_head - 1 - i + _items.Length) % _items.Length;
                  newItems[value - 1 - i] = _items[sourceIndex];
               }

               _size = elementsToCopy;
               _tail = value - _size;
               _head = value;
            } else {
               _tail = 0;
               _head = 0;
            }

            _items = newItems;
         }
      }

      /// <summary>
      /// Searches for an element that matches the conditions defined by the specified predicate and returns the
      /// zero-based index of the first occurrence within the collection.
      /// </summary>
      /// <remarks>The search starts from the most recently added element and proceeds in reverse order of
      /// insertion. If the collection is empty, the method returns –1.</remarks>
      /// <param name="predicate">The delegate that defines the conditions of the element to search for. Cannot be null.</param>
      /// <returns>The zero-based index of the first element that matches the predicate; otherwise, –1 if no such element is
      /// found.</returns>
      public int FindIndex(Predicate<T> predicate) {
         if (IsEmpty) return -1;
         int current = _head;
         for (int i = 0 ; i < _size ; i++) {
            current = (current - 1 + _items.Length) % _items.Length;
            if (predicate(_items[current])) {
               return i;
            }
         }
         return -1;
      }

      /// <summary>
      /// Gets the number of elements contained in the <see cref="BoundedStack{T}"/>.
      /// </summary>
      public int Count => _size;

      /// <summary>
      /// Gets a value indicating whether the <see cref="BoundedStack{T}"/> is empty.
      /// </summary>
      public bool IsEmpty => _size == 0;
      public bool IsNonEmpty => _size > 0;

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
            throw new ArgumentOutOfRangeException(nameof(capacity),"Capacity must be positive.");

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
      public BoundedStack(int capacity,IEnumerable<T> collection) : this(capacity) {
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
      public virtual bool Push(T item) {
         bool itemRemoved = false;

         if (IsFull) {
            DisposeIfDisposable(_items[_tail]);
            _items[_tail] = default!;
            _tail = (_tail + 1) % _items.Length;
            itemRemoved = true;
         } else {
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
         if (IsEmpty) throw new InvalidOperationException("The stack is empty.");

         _head = (_head - 1 + _items.Length) % _items.Length;
         T item = _items[_head];
         _items[_head] = default!;  // Clear reference to help GC
         _size--;

         return item;
      }
      /// <summary>
      /// Moves the element at the specified index to the top of the stack.
      /// </summary>
      /// <remarks>This method reorders the stack such that the element at the specified index becomes the
      /// topmost element. The relative order of other elements is preserved, with all elements above the specified
      /// index shifted down by one position. 
      /// If the specified index is out of bounds or is 0 (the top element), the method performs no operation.
      /// </remarks>
      /// <param name="index">The zero-based index of the element to move. Must be within the bounds of the stack.</param>
      /// stack.</exception>
      public void Surface(int index) {
         if (index <= 0 || index >= _size) return; // Continue to use the current top element

         // Get the element at the specified index
         int targetIndex = (_head - 1 - index + _items.Length) % _items.Length;
         T item = _items[targetIndex];

         // Shift elements down to fill the gap
         for (int i = index ; i > 0 ; i--) {
            int currentIndex = (_head - 1 - i + _items.Length) % _items.Length;
            int nextIndex = (_head - 1 - (i - 1) + _items.Length) % _items.Length;
            _items[currentIndex] = _items[nextIndex];
         }

         // Place the surfaced item at the top
         int topIndex = (_head - 1 + _items.Length) % _items.Length;
         _items[topIndex] = item;
      }
      /// <summary>
      /// Moves the first element matching the predicate to the top of the stack.
      /// </summary>
      /// <param name="predicate">The predicate to match elements against.</param>
      /// <returns>True if an element was found and surfaced, otherwise false.</returns>
      public void Surface(Func<T,bool> predicate) {
         if (IsEmpty) return;

         int matchIndex = -1;
         int currentIndex = 0;

         foreach (T item in this) {
            if (predicate(item)) {
               matchIndex = currentIndex;
               break;
            }
            currentIndex++;
         }

         if (matchIndex == -1 || matchIndex == 0) return;

         Surface(matchIndex);
      }
      /// <summary>
      /// Gets the element at the specified index from the top of the stack.
      /// </summary>
      /// <param name="index">The zero-based index from the top of the stack.</param>
      /// <returns>The element at the specified index, or default(T) if the index is out of bounds.</returns>
      public T? this[int index] {
         get {
            if (index < 0 || index >= _size) return default;

            int actualIndex = (_head - 1 - index + _items.Length) % _items.Length;
            return _items[actualIndex];
         }
      }

      /// <summary>
      /// Removes all items from the stack.
      /// </summary>
      public void Clear() {
         for (int i = 0 ; i < _items.Length ; i++) DisposeIfDisposable(_items[i]);
         Array.Clear(_items,0,_items.Length);
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
         for (int i = 0 ; i < _size ; i++) {
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

         StringBuilder sb = new("[");
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

      private static void DisposeIfDisposable(T item) {
         if (item is IDisposable disposable) disposable.Dispose();
      }

   }

   public static partial class Extensions {

      //public static bool TRUE<T>(T _) => true;

      extension<T>(Predicate<T> predicate) {
         public static bool TRUE => true;
         public static bool FALSE => false;
      }

      /// <summary>
      /// Equivalent to pred ?? _ => true, but that syntax doesn't work.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="predicate"></param>
      /// <returns></returns>
      //public static Predicate<T> OrTrue<T>(this Predicate<T> predicate) => predicate ?? TRUE;
      //public static Func<T, bool> OrTrue<T>(this Func<T, bool> predicate) => predicate ?? TRUE;

      extension<T>(IEnumerable<T> source) {
         public IEnumerable<T> OptMap(bool condition,Func<IEnumerable<T>,IEnumerable<T>> func) => condition ? func(source) : source;
         public IEnumerable<T> OptWhere(Func<T,bool>? pred = null) => pred is not null ? source.Where(pred) : source;
         public Set<T> ToSet => [.. source];

         /// <summary>
         /// Executes the specified action on each element of the sequence, inserting a joiner action between each element.
         /// </summary>
         /// <param name="action">The action to execute on each element.</param>
         /// <param name="joiner">The action to execute between elements.</param>
         public void GenerateJoinedSequence(Action<T> action,Action joiner,Action? terminator=null) {
            if (source.Any()) {
               action(source.First());
               foreach (T item in source.Skip(1)) {
                  joiner();
                  action(item);
               }
            }
            terminator?.Invoke();
         }


         /// <summary>
         /// Deconstructs an enumerable into a tuple of two elements.
         /// </summary>
         /// <param name="first">The first element or default if not present.</param>
         /// <param name="second">The second element or default if not present.</param>
         public void Deconstruct(out T first,out T second) {
            T[] array = [.. source];
            first = array.Length > 0 ? array[0] : default!;
            second = array.Length > 1 ? array[1] : default!;
         }
      }

      extension(IEnumerable<string> source) {
         /// <summary>
         /// Deconstructs an enumerable into a tuple of two elements.
         /// </summary>
         /// <param name="first">The first element or default if not present.</param>
         /// <param name="second">The second element or default if not present.</param>
         public void Deconstruct(out string first,out string second) {
            string[] array = [.. source];
            first = array.Length > 0 ? array[0].Trim() : "";
            second = array.Length > 1 ? array[1].Trim() : "";
         }
      }

      extension (string? s) {
         /// <summary>
         /// Verify that the string is null, empty or whitespace.
         /// </summary>
         /// <returns></returns>
         public bool IsNullEmptyOrWhitespace => s is null || s.All(char.IsWhiteSpace);
         /// <summary>
         /// ... or it's not.
         /// </summary>
         public bool IsNotNullEmptyOrWhitespace => !s.IsNullEmptyOrWhitespace;
      }

      extension(string s) {
         /// <summary>
         /// Gets the resolved <see cref="Type"/> represented by the stored type name.
         /// </summary>
         /// <remarks>If the stored type name does not include a namespace, the namespace of the <see
         /// cref="Module"/> type is used as a fallback. This property attempts to resolve the type using <see
         /// cref="Type.GetType(string)"/>. If the type cannot be found, an exception is thrown.</remarks>
         public Type AsType {
            get {
               if (string.IsNullOrWhiteSpace(s)) {
                  throw new ArgumentException("Type name cannot be null or whitespace.",nameof(s));
               }
               Type? type = Type.GetType(s);
               type ??= Type.GetType($"{typeof(Module).Namespace}.{s}");
               if (type == null) {
                  throw new TypeLoadException($"Could not load type '{s}'.");
               }
               return type;
            }
         }
         /// <summary>
         /// Returns true if the current string is a valid file name on the current platform.
         /// </summary>
         /// <remarks>A valid file name is non-empty, contains no whitespace only, and does not include
         /// any characters that are invalid for file names as defined by the operating system. This property does not
         /// check for file path validity or reserved file names.</remarks>
         public bool IsValidFileName => !string.IsNullOrWhiteSpace(s) && s.All(ch => !Path.GetInvalidFileNameChars().Contains(ch));
         /// <summary>
         /// Convert the string to the case of the first letter.
         /// </summary>
         /// <param name="str"></param>
         /// <returns></returns>         
         public string ToFirstLetterCase() => string.IsNullOrEmpty(s) ? s : char.IsUpper(s[0]) ? s.ToUpper() : s.ToLower();
         /// <summary>
         /// Return the index of the first non-blank character in the string.
         /// </summary>
         /// <param name="text"></param>
         /// <returns></returns>
         public int FindIndex(Func<char,bool> predicate) {
            for (int i = 0 ; i < s.Length ; i++) if (predicate(s[i])) return i;
            return -1;
         }
         /// <summary>
         /// Return true if the string is composed of alphanumeric characters only.
         /// </summary>
         /// <returns></returns>
         public bool IsAlphanumeric {
            get {
               if (string.IsNullOrEmpty(s)) return false;
               foreach (char c in s) if (!char.IsLetterOrDigit(c)) return false;
               return true;
            }
         }

         /// <summary>
         /// Return the string with whitespaces removed.
         /// </summary>
         /// <returns></returns>
         public string WithNoWhitespace => string.IsNullOrEmpty(s) ? s : Regex.Replace(s,@"\s+","",RegexOptions.Compiled);

         /// <summary>
         /// Returns the string enclosed in quotes, optionally escaping special characters.
         /// </summary>
         /// <param name="quote">The quote character(s) to use. Default is double quote.</param>
         /// <param name="escape">If true, escapes special characters like backslash, quotes, newlines, etc.</param>
         /// <returns>The quoted string with optional escaping.</returns>
         public string Quoted(string quote="\"",bool escape=false) => escape ? $"{quote}{s.Escape()}{quote}" : $"{quote}{s}{quote}";

         /// <summary>
         /// Escapes special characters in the string for use in string literals.
         /// </summary>
         /// <returns>The escaped string.</returns>
         public string Escape() {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("\\","\\\\")
                    .Replace("\"","\\\"")
                    .Replace("\n","\\n")
                    .Replace("\r","\\r")
                    .Replace("\t","\\t");
         }

         public string IntensifyColor(double factor) {
            if (factor < 1) throw new ArgumentOutOfRangeException(nameof(factor),"IntensifyColor: Factor must be greater than or equal to 1.");
            return ScaleColor(s,factor);
         }
         public string DimColor(double factor) {
            if (factor < 0 || factor > 1) throw new ArgumentOutOfRangeException(nameof(factor),"DimColor: Factor must be between 0 and 1.");
            return ScaleColor(s,factor);
         }

         /// <summary>
         /// Returns the pluralized form of the string based on the specified count.
         /// </summary>
         /// <param name="count">The numeric value that determines whether the singular or plural form should be used.</param>
         /// <param name="plural">The plural form to use. If null, a default pluralization rule is applied.</param>
         /// <returns>The pluralized form of the string if the count does not equal 1; otherwise, the original string.</returns>
         public string Plural(int count,string? plural = null) => count.Plural(s,plural);

         public string Capitalize => s.Length > 1 ? s[0..1].ToUpper() + s[1..].ToLower() : s.ToUpper();

         /// <summary>
         /// Normalize a string to a valid identifier.
         /// </summary>
         /// <param name="str"></param>
         /// <param name="prefix"></param>
         /// <param name="spaceReplacement"></param>
         /// <param name="camelCase"></param>
         /// <param name="literalObjectName"></param>
         /// <returns></returns>
         public string AsIdentifier(string prefix = "",string spaceReplacement = "",bool camelCase = false,bool literalObjectName = false) {
            if (literalObjectName) return s.Replace(" ","");
            if (prefix != "") prefix += "_";
            s = Regex.Replace(s,@"[^\p{L}\d\s]+","_",RegexOptions.Compiled).Trim();
            if (camelCase) {
               return prefix.ToLower() + s.Split(" ").Select((word,i) => i == 0 ? word.ToLower() : char.ToUpper(word[0]) + word[1..].ToLower()).Aggregate((a,b) => a + b);
            } else {
               return prefix.ToLower() + s.ToLower().Replace(" ",spaceReplacement);
            }
         }

         /// <summary>
         /// Ensures the file path is writable by checking directory existence and file permissions.
         /// </summary>
         /// <param name="createDirectory">If true, creates the directory if it doesn't exist.</param>
         /// <param name="errorMessage">Error message if the file is not writable.</param>
         /// <returns>True if the file can be written to, false otherwise.</returns>
         public bool EnsureFileWritable(out string errorMessage,out string fileName,string defaultdDirectory = "",bool createDirectory = true) {
            errorMessage = string.Empty;
            fileName = s;

            // Get directory from file path
            string? directory = Path.GetDirectoryName(s);
            if (string.IsNullOrEmpty(directory)) {
               directory = string.IsNullOrEmpty(defaultdDirectory) ? Directory.GetCurrentDirectory() : defaultdDirectory;
               fileName = s = Path.Combine(directory,s);
            }

            // Check/create directory
            if (!Directory.Exists(directory)) {
               if (createDirectory) {
                  try {
                     Directory.CreateDirectory(directory);
                  } catch (Exception ex) {
                     errorMessage = $"Cannot create directory '{directory}': {ex.Message}";
                     return false;
                  }
               } else {
                  errorMessage = $"Directory '{directory}' does not exist.";
                  return false;
               }
            }

            // If file doesn't exist, directory writability is sufficient
            if (!File.Exists(s)) return true;

            // Check if existing file is read-only
            FileAttributes attributes = File.GetAttributes(s);
            if (attributes.HasFlag(FileAttributes.ReadOnly)) {
               errorMessage = $"File '{s}' is read-only.";
               return false;
            }

            // Try to open for writing to detect locks
            try {
               using FileStream fs = File.Open(s,FileMode.Open,FileAccess.Write,FileShare.ReadWrite);
               return true;
            } catch (UnauthorizedAccessException) {
               errorMessage = $"Access denied to file '{s}'.";
               return false;
            } catch (IOException ex) {
               errorMessage = $"File '{s}' is locked or inaccessible: {ex.Message}";
               return false;
            }
         }

         /// <summary>
         /// Returns a new string consisting of the specified string repeated a given number of times.
         /// </summary>
         /// <param name="src">The string to be repeated. If null or empty, the result is an empty string.</param>
         /// <param name="n">The number of times to repeat the string. Must be zero or greater.</param>
         /// <returns>A new string that consists of the input string repeated the specified number of times. Returns an empty
         /// string if the input is null, empty, or if the repeat count is zero.</returns>
         /// 
         /// <remarks>
         /// It isn't possible to overload * for char and uint because char implicitly converts to uint.
         /// </remarks>
         public static string operator *(string src,uint n) {
            if (string.IsNullOrEmpty(src) || n == 0) return "";
            StringBuilder sb = new();
            for (int i = 0 ; i < n ; i++) sb.Append(src);
            return sb.ToString();
         }

         /// <summary>
         /// Removes all occurrences of a specified substring from the source string.
         /// </summary>
         /// <remarks>This operator performs a case-sensitive removal of all non-overlapping occurrences
         /// of the specified substring. If either parameter is null, an ArgumentNullException is thrown.</remarks>
         /// <param name="src">The string from which to remove occurrences of the specified substring. If this is null, the empty string is returned.</param>
         /// <param name="rem">The substring to remove from the source string. If this value is empty or null, the source string is returned
         /// unchanged.</param>
         /// <returns>A new string that is equivalent to the source string except for all instances of the specified substring,
         /// which are removed. If the substring to remove is not found, the original string is returned.</returns>
         public static string operator -(string src,string oldValue) => (oldValue ?? "").Length == 0 ? src ?? "" : (src ?? "").Replace(oldValue!,"");
         public static string operator -(string src,char oldChar) => src - oldChar.ToString();

         /// <summary>
         /// Creates a compiled regular expression from the specified pattern using the bitwise complement operator (~).
         /// </summary>
         /// <remarks>This operator provides a concise syntax for creating compiled regular expressions.
         /// The resulting <see cref="Regex"/> instance uses <see cref="RegexOptions.Compiled"/>. If <paramref
         /// name="pattern"/> is invalid, a <see cref="ArgumentException"/> is thrown by the <see cref="Regex"/>
         /// constructor.</remarks>
         /// <param name="pattern">The regular expression pattern to compile. Cannot be null.</param>
         /// <returns>A <see cref="Regex"/> object that represents the compiled regular expression defined by <paramref
         /// name="pattern"/>.</returns>
         /// <example>
         /// "test string" - ~@"[st]+" --> "e ring"
         /// </example>
         public static Regex operator ~(string pattern) => (pattern ?? "").Length == 0 ? NeverMatches : new(pattern!,RegexOptions.Compiled);
         public static string operator -(string src,Regex re) => re.Replace(src ?? "","");

         /// <summary>
         /// Replaces all occurrences of a specified regular expression pattern in the input string with a replacement
         /// string.
         /// </summary>
         /// <remarks>This operator uses regular expression matching, which may affect performance for
         /// large input strings or complex patterns. The replacement is performed using the rules of
         /// System.Text.RegularExpressions.Regex.Replace. If the pattern is invalid, a RegexParseException may be
         /// thrown.</remarks>
         /// <param name="input">The string to search for matches of the regular expression pattern.</param>
         /// <param name="repl">A tuple containing the regular expression pattern to match and the replacement string. The first element is
         /// the pattern; the second element is the replacement.</param>
         /// <returns>A new string that is equivalent to the input string, except that all substrings matching the regular
         /// expression pattern are replaced with the replacement string.</returns>
         /// <example>
         /// "test string" >> (@"[st]+","X") --> "XeXX XXring"
         /// Regex re = ~@"[st]+";
         /// "test string" >> (re,"X") --> "XeXX XXring"
         /// </example>
         public static string operator >>(string input,(string re, string replacement) repl) => Regex.Replace(input,repl.re,repl.replacement);
         public static string operator >>(string input,(Regex re, string replacement) repl) => repl.re.Replace(input,repl.replacement);

         /// <summary>
         /// Attempts to locate a file by checking the specified path and, if not found, by appending each of the
         /// provided extensions.
         /// </summary>
         /// <param name="fullPath">When this method returns, contains the full path to the located file if found; otherwise, an empty string.</param>
         /// <param name="extensions">An array of file extensions to try if the file is not found at the original path. Each extension should
         /// include the leading period (e.g., ".txt").</param>
         /// <returns>true if a file is found at the original path or with one of the specified extensions; otherwise, false.</returns>
         public bool TryGetFile(out string fullPath,string[] extensions) {
            if (Path.Exists(s)) {
               fullPath = s;
               return true;
            } else {
               foreach (string ext in extensions) {
                  string candidate = Path.ChangeExtension(s,ext);
                  if (Path.Exists(candidate)) {
                     fullPath = candidate;
                     return true;
                  }
               }
               fullPath = "";
               return false;
            }
         }

      }

      private static string ScaleColor(string s,double factor) {
         Match match = Regex.Match(s,@"^#(?<r>[0-9A-Fa-f]{2})(?<g>[0-9A-Fa-f]{2})(?<b>[0-9A-Fa-f]{2})$",RegexOptions.Compiled);
         string Scale(string c) => ((byte)Math.Min(255,int.Parse(match.Groups[c].Value,NumberStyles.HexNumber)*factor)).ToString("x2");
         if (!match.Success) throw new ArgumentException($"Invalid hex color string: {s}");
         return "#" + Scale("r") + Scale("g") + Scale("b");
      }

      private static readonly Regex NeverMatches = new(@"(?!.*)",RegexOptions.Compiled);

      extension(int i) {
         /// <summary>
         /// Return the plural of the word word for count.
         /// Does NOT handle words that are not pluralizable, such as "fish" or "sheep".
         /// </summary>
         /// <param name="word">The item name.</param>
         /// <param name="plural">If given the plural of word. Otherwise an s, es, or ies is added as ap appropriate.</param>
         /// <param name="pad">If given, the plural is padded with spaces to the extent of what would be added after inserting the pad.</param>
         /// <param name="countWidth">Width of the count in characters. Default is 3.</param>
         /// <param name="addSpace">Whether to add a space after the word if count is 1. Default is true.</param>
         /// <returns></returns>
         public string Plural(string word,string? pad = null,string? plural = null,int countWidth = 3,bool addSpace=true) {
            string suffix;
            if (plural is null) {
               if (Regex.IsMatch(word,@"(s|sh|ch|x|z)$",RegexOptions.IgnoreCase | RegexOptions.Compiled)) {
                  suffix = "es";
               } else if (Regex.IsMatch(word,@"[^aeiou]y$",RegexOptions.IgnoreCase | RegexOptions.Compiled)) {
                  suffix = "ies";
               } else {
                  suffix = "s";
               }
               if (i == 1) {
                  plural = $"{word}{(pad??"")}{(addSpace ? new string(' ',suffix.Length) : "")}";
               } else {
                  if (suffix == "ies") {
                     plural = Regex.Replace(word,"y$","ies",RegexOptions.IgnoreCase | RegexOptions.Compiled);
                  } else {
                     plural = word + suffix;
                  }
                  plural += pad is not null ? pad : "";
               }
            }
            return $"{string.Format($"{{0,{countWidth}:N0}}",i)} {plural}";
         }

         /// <summary>
         /// Constrains the current value to be within the specified minimum and maximum bounds.
         /// </summary>
         /// <param name="min">The inclusive lower bound to which the value will be constrained.</param>
         /// <param name="max">The inclusive upper bound to which the value will be constrained.</param>
         /// <returns>The constrained value, which will be no less than <paramref name="min"/> and no greater than <paramref
         /// name="max"/>.</returns>
         public int ConstrainedTo(int min,int max) => Math.Min(Math.Max(i,min),max);
      }

      extension(long l) {
         /// <summary>
         /// Formats a byte count with appropriate unit suffix
         /// </summary>
         /// <param name="bytes">Number of bytes</param>
         /// <param name="useDecimalUnits">If true, uses decimal units (KB, MB, GB), otherwise binary units (KiB, MiB, GiB)</param>
         /// <returns>Formatted string representing size with appropriate unit</returns>
         public string HumanReadableSize(bool useDecimalUnits = false) {
            string[] binarySuffixes = ["bytes","KiB","MiB","GiB","TiB","PiB","EiB"];
            string[] decimalSuffixes = ["bytes","KB","MB","GB","TB","PB","EB"];

            string[] suffixes = useDecimalUnits ? decimalSuffixes : binarySuffixes;
            int factor = useDecimalUnits ? 1000 : 1024;

            if (l == 0) return "0 bytes";

            int place = Convert.ToInt32(Math.Floor(Math.Log(l,factor)));
            double num = Math.Round(l / Math.Pow(factor,place),0);

            // Don't use the "bytes" suffix for values larger than 1
            if (place == 0 && num > 1)
               return $"{num} bytes";
            else if (place == 0)
               return $"{num} byte";
            else
               return $"{num} {suffixes[place]}";
         }
      }

      extension(NamedElement? element) {
         public void SetFocus() { if (element is not null) Focus.SetFocus(element); }
      }

      extension(ITopLevelContainer element) {
         public void SetFocus() { if (element is not null) Focus.SetFocus((NamedElement)element); }
      }

      extension(List<Guid> guids) {
         /// <summary>
         /// Returns a list of CDL2Object instances corresponding to the stored GUIDs.
         /// </summary>
         /// <returns>A list of CDL2Object objects that match the stored GUIDs. The list will be empty if no matching objects are
         /// found.</returns>
         public List<CDL2Object> ToCDL2Objects()
            => [.. guids.Select(guid => Database.Instance.NamedElements.GetValueOrDefault(guid)).OfType<CDL2Object>()];
         public List<CDL2Object> ToNonSynthetiCDL2Objectsc()
            => [.. guids.ToCDL2Objects().Where(obj => obj is not Algorithm alg || !obj.IsSynthetic)];
         public List<CDL2Object> ToSyntheticCDL2Objects()
            => [.. guids.ToCDL2Objects().Where(obj => obj is not Algorithm alg && obj.IsSynthetic)];
      }
      extension(Guid guid) {
         /// Returns the CDL2Object instance corresponding to the stored GUID.
         /// </summary>
         /// <returns>The CDL2Object object that matches the stored GUID, or null if no matching object is found.</returns>
         public T? ToCDL2Object<T>() where T : CDL2Object
            => Database.Instance.NamedElements.GetValueOrDefault(guid) as T;
         /// <summary>
         /// Returns true if the GUID corresponds to a non-synthetic CDL2Object.
         /// </summary>
         /// <returns></returns>
         public bool IsNonSyntheticCDL2Object() {
            CDL2Object? obj = guid.ToCDL2Object<CDL2Object>();
            return obj is not null && !obj.IsSynthetic;
         }
      }
 
      extension(ParsingContext? context) {
         public ParsingContext AsParsingContext => context ?? new ParsingContext(location: InsertLocation.After);
      }


      extension<T>(List<T> list) where T : notnull {
         /// <summary>
         /// Returns a new list containing the elements of the original list combined with the specified items,  ensuring
         /// no duplicate elements are added.
         /// </summary>
         /// <typeparam name="T">The type of elements in the list. Must be a non-nullable type.</typeparam>
         /// <param name="items">The collections of items to add to the list. Duplicate elements will not be added.</param>
         /// <returns>A new list containing the elements of the original list and the specified items, without duplicates.</returns>
         public List<T> With(params T[] items) => items.All(item => list.Contains(item)) ? list : [.. list.Union(items)];
         public List<T> With(T item) => list.Contains(item) ? list : [.. list,item];

         /// <summary>
         /// Adds the specified item to the collection if it is not null.
         /// </summary>
         /// <param name="item">The item to add to the collection. The item is only added if it is not null.</param>
         public void AddNonNull(T? item) {
            if (item is not null) list.Add(item);
         }
      }
      extension<T>(List<T> list) where T : NamedElement {
         public bool TryGetValueWithId(ID id,[MaybeNullWhen(false)] out T? value) {
            value = list.FirstOrDefault(item => item.Id == id);
            return value is not null;
         }
      }
      extension<T>(Set<T> set) where T : notnull {
         /// <summary>
         /// Returns a new set containing the elements of the original set combined with the specified items,  ensuring
         /// no duplicate elements are added.
         /// </summary>
         /// <typeparam name="T">The type of elements in the set. Must be a non-nullable type.</typeparam>
         /// <param name="items">The collections of items to add to the set. Duplicate elements will not be added.</param>
         /// <returns>A new set containing the elements of the original set and the specified items, without duplicates.</returns>
         public Set<T> With(params T[] items) => items.All(item => set.Contains(item)) ? set : [.. set.Union(items)];
         public Set<T> With(T item) => set.Contains(item) ? set : [.. set,item];
         /// <summary>
         /// Adds the specified item to the collection if it is not null.
         /// </summary>
         /// <param name="item">The item to add to the collection. The item is only added if it is not null.</param>
         public void AddNonNull(T? item) {
            if (item is not null) set.Add(item);
         }
      }
      extension<T>(Set<T> set) where T : NamedElement {
         public bool TryGetValueWithId(ID id,[MaybeNullWhen(false)] out T? value) {
            value = set.FirstOrDefault(item => item.Id == id);
            return value is not null;
         }
      }

      /// <summary>
      /// Return the types that implement the given interface.
      /// </summary>
      /// <typeparam name="TInterface"></typeparam>
      /// <returns></returns>
      public static IEnumerable<Type> GetImplementorsOfInterface<TInterface>() => Assembly.GetExecutingAssembly().GetTypes()
             .Where(type => typeof(TInterface).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);

      extension(IEnumerable<NamedElement> objects) {
         /// <summary>
         /// If all elements of objects are siblings of this one, then return the objects in the order of their siblings.
         /// </summary>
         /// <returns></returns>
         public IEnumerable<NamedElement> OrderedAsSiblings {
            get {
               //Debug.Assert(objects.Any(),"The collection of objects must not be empty.");
               if (!objects.Any()) return objects;
               List<Guid> siblings = objects.First().Siblings;
               if (objects.Skip(1).All(obj => siblings.Contains(obj.GUID))) {
                  return objects.OrderBy(obj => siblings.IndexOf(obj.GUID));
               } else {
                  return objects;
               }
            }
         }
      }

      extension<T>(ISet<T> collection) {
         public void AddAll(IEnumerable<T> items) {
            foreach (T item in items) collection.Add(item);
         }
      }

      extension<T>(T? obj) {
         public string WithSpace => obj == null ? "" : obj.ToString() + " ";
      }

      /// <summary>
      /// Decorate a string with the given decoration.
      /// This means encapsulating the string in a span tag with the given style and the foreground and background colors.
      /// This is only done if the emitter supports decoration.
      /// </summary>
      /// <param name="str"></param>
      /// <param name="emitter"></param>
      /// <param name="element"></param>
      /// <param name="decoration"></param>
      /// <returns></returns>
      internal static string Decorate(this string str,Emitter emitter,SE element,PrettyPrinter.Decoration? decoration = null) {
         if (str == null) return "";
         if (emitter.SupportsDecoration) {
            if (element != SE.AlgorithmName) {
               decoration = PrettyPrinter.Decorators[element];
            } else if (decoration == null) {
            }
            Debug.Assert(decoration != null,$"No decoration for {element}");
            return string.Join("\n",NewlineRegex().Split(str)
                           .Select(str => $"<span fg='{decoration.FG}' bg='{decoration.BG}' style='{decoration.Style}'>{str}</span>"));
         } else {
            return str;
         }
      }
      internal static string Decorate(this RW rw,Emitter emitter,SE element) => rw.ToString().Decorate(emitter,element);
      internal static string Decorate(this Token token,Emitter emitter,SE element) => token.TokenString.Decorate(emitter,element);
      internal static string Decorate(this ID id,Emitter emitter,SE element)
         => /*Id.Comments!.Decorate(Emitter,SE.Comment) +*/ id.Name.Decorate(emitter,element);
      internal static string Decorate(this long i,Emitter emitter) => i.ToString().Decorate(emitter,SE.Number);
      internal static string Decorate(this double d,Emitter emitter) => d.ToString().Decorate(emitter,SE.Number);
      internal static string Decorate(this ID algorithmId,Emitter emitter,PrettyPrinter.Decoration decoration) => algorithmId.ToString().Decorate(emitter,SE.AlgorithmName,decoration);


      [GeneratedRegex(@"\r\n|\r|\n",RegexOptions.Compiled)]
      private static partial Regex NewlineRegex();
   }

   /// <summary>
   /// Here as a reminder. Not really useful.
   /// Even more complicated than the Curry version.
   /// They are problematic only when the method being parametrized has overloads.
   /// 
   /// Both work in all cases if the specific overload is fixed, e.g.,
   /// Action<T1,T2> action = A;
   /// ... F(A.Partial(x)) 
   /// </summary>
   public static class PartialExtensions {
      // For void-returning methods
      public static Action<T2> Partial<T1, T2>(this Action<T1,T2> func,T1 arg1) => t2 => func(arg1,t2);

      // For returning methods
      public static Func<T2,TResult> Partial<T1, T2, TResult>(this Func<T1,T2,TResult> func,T1 arg1) => arg2 => func(arg1,arg2);
   }

   /// <summary>
   /// Here as a reminder. Not really useful.
   /// Used as            F(Curry.Partial((Action<T1,T2>)Action, T1 fixedValue)) to create a new Action<T2> that calls the original action with the fixed value for T1.
   /// Easier to just say F(x=> Action(fixedValue,x)) in the code.
   /// </summary>
   public static class Curry {
      // For void-returning two-parameter actions
      public static Action<T2> Partial<T1, T2>(Action<T1,T2> action,T1 fixedValue) => t2 => action(fixedValue,t2);

      // For two-parameter functions returning a value
      public static Func<T2,TResult> Partial<T1, T2, TResult>(Func<T1,T2,TResult> func,T1 fixedValue) => t2 => func(fixedValue,t2);
   }

}

