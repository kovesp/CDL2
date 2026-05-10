# Implementation Notes
## Semantic Analysis
Semantic analysis is done on the current or selected `program` and its `module`s.
   - Verify all `program` `part`s are existing `module`s.
      - Lude verification: 
         - Each `program` lude entry is a `part` (i.e., a `module`). 
         - If a `module` is referenced in a `program` lude, then its corresponding
           `module` lude must be non-empty.
         - If a `module` lude has an element, it must refer to a `section` in the `module` that has a
           corresponding lude.
   - For each `part` (`module`) verify interface consistency.
     - In each `layer` construct a `visible` table that contains all IDs in `ext`s in
       the `sections` and all IDs in `abstr`s in every `section` in the `layer` below.
       There can be no duplicates.
 	 - In each `section` of the `module` verify that every ID in `inv`s is in the `visible` table
       of the `layer`.
   - Import/Export verification: 
     - Collect all `export`s in the `program` `part`s (`module`s) into a `program` `exports` table
       verifying that there are no duplicates.
     - In each `module` verify that
    	- All `import`s occur in the `program` `exports` table.
		- The `import` specifications match the `export`s (i.e., both are `consts` or both
          are algorithms of the same type (`function` / `action` / `test` / `predicate`) and
          have the same number of arguments with matching types (input/output/transput).
   - Perform semantic analysis of each
     - Macro: verify that every macro element that is an ID is either in the declarations
       table as a `const`, `list` or `var` and similarly for IDs that are in the `inv` list.
     - Procedure: perform the detailed analysis (leave as is for now).