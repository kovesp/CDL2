using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata.Ecma335;

namespace CDL2v1 {
   internal class SymbolTable {
      public Container? Owner;   // The COntainer that owns this symboll table.
      private Dictionary<ID,NamedElement> table = [];

      public NamedElement this[ID id] {
         get => table[id];
         set {
            if (table.TryGetValue(id,out NamedElement? currentValue)) {
               if (currentValue is Undeclared) {
                  table[id] = value;
               } else {
                  throw new Exception($"ID {id} already declared as {currentValue}");
               }
            } else {
               table[id] = value;
            }
            id.owner = this;
         }
      }

      public bool ContainsKey(ID id) => table.ContainsKey(id);

      public Set<T> AsSet<T>() where T : NamedElement => new(table.Values.OfType<T>());

      /// <summary>
      /// Check if the symbol table contains the given ID. If not, add an Undeclared to the symbol table.
      /// Used when a reference to an ID is encountered before the ID is declared.
      /// </summary>
      /// <param id="token">An ID token.</param>
      /// <returns>The ID that was constructed from the token.</returns>
      public ID Reference(ID id) {
         if (!table.ContainsKey(id)) {          // If the ID is not in the symbol table
            table[id] = new Undeclared(id); // Add an Undeclared to the symbol table
         }
         return id;
      }
      /// <summary>
      /// If the id is in the table, Return true if the id is undeclared.
      /// If it is not in the table, insert is as undeclard and return true.
      /// </summary>
      /// <param id="id"></param>
      /// <returns></returns>
      public bool IsUndeclared(ID id) => table[Reference(id)] is Undeclared;
      /// <summary>
      /// Check if the ID is declared in the symbol table. If is is not in the table, enter it as undeclared.
      /// </summary>
      /// <param id="id">The id to check.</param>
      /// <param id="v">The declared element if found, undeclared otherwise.</param>
      /// <returns></returns>
      public bool IsDeclared(ID id,out NamedElement? v) => table.TryGetValue(Reference(id),out v) && v is not Undeclared;

      public override string ToString() => $"SymbolTable[{(Owner is null ? "Global" : Owner.ToString())}]";
      internal  bool TryGetValue(ID id,[MaybeNullWhen(false)] out NamedElement ne) => table.TryGetValue(id,out ne);
   }   
}
