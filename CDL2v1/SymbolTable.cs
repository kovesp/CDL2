namespace CDL2v1 {

   internal class SymbolTable : Dictionary<ID,NamedElement> {
      public bool ContainsKey(Token token) => ContainsKey(new ID(token));
      // Indexer to access elements using ID
      public NamedElement this[Token token] {
         get => base[new ID(token)];
         // This is used to add elements when being declared. So if it already exists, then
         // - if the current value is an Undeclared, then replace it with the new value.
         // - if the current value is not an Undeclared, then throw an exception.
         set {
            ID id = new(token);
            if (TryGetValue(id,out NamedElement? currentValue)) {
               if (currentValue is Undeclared) {
                  base[id] = value;
                  return;
               }
               throw new Exception($"ID {id} already declared as {currentValue}");
            }
         }
      }
      /// <summary>
      /// Check if the symbol table contains the given ID. If not, add an Undeclared to the symbol table.
      /// Used when a reference to an ID is encountered before the ID is declared.
      /// </summary>
      /// <param name="token">An ID token.</param>
      /// <returns>The ID that was constructed from the token.</returns>
      public ID Reference(ID id) {
         if (TryGetValue(id,out NamedElement? currentValue)) { // If the ID is already in the symbol table
            if (currentValue is Undeclared) return id; // If the ID is already an Undeclared, then do nothing               
         } else { // If the ID is not in the symbol table
            base[id] = new Undeclared(id); // Add an Undeclared to the symbol table
         }
         return id;
      }
   }   
}
