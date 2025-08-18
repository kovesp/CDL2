When generating code, please follow these guidelines:
  1. Adhere to the formatting conventions of the solution. Especially pay attention
     to keeping the opening brace on the same line as the construct it belongs to.
  2. Keep methods and functions short, introduce helper methods with descriptive names
     if necessary. Maximum method length is around 50 lines.
  3. Use local functions only when they capture one or more local variables keeping
     passed parameters to the minimum. Place them all at the end of the containing method
     with a separator comment block of the form
```
/////////////////////
// Local functions //
/////////////////////
```
  4. **Never** generate partial code that is not complete and has comments
     of the form      
    `// Rest of your existing code here`.
  5. When generating methods, use expression body syntax for single-line methods.
  6. Always place single statements on the same line as their governing construct (e.g., if, for, foreach,etc.).
     Never enclose them in braces. Specifically do this for return statements as well.
  7. Never use var, always specify the type explicitly.
  8. Avoid if statements that end with a return or break statement. Instead, use an if-else if-else structures.
