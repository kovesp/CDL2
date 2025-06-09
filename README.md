# CDL2 Compiler V1

1. Tokenizes the CDL2 program (possibly contained in multiple files) into a list of tokens.
   - ~~Tokens support retaining file and line # info, but not yet implemented.~~
   - ~~Comments are retained as tokens, but are currently discarded before parsing.~~
   - Comments are retatined as tokens.
   - Comments can be placed on a `NOTE.` syntax element extension. 
2. Parses a CDL2 program into a syntax tree.
   - Error handling very primitive currently.
   - Syntax extended slightly.
     - `NOTE.` element can occur in many places.
     - Constant and macro elements may be separated by a semi-colon which is ignored (not in syntax tree). This is
       to support adjacent identifiers. The pretty printer generates semi-colons between identifiers as necessary.
3. Semantic analysis framework does most semantic checks, e.g.,
   - Checks for procedure defects (procdure has effect when failing) and effects (TEST/FUNCTION has an effect).
   - Checks that constants not be passed to output or transput affixes. Checks that only affixes, VARs and CONSTs
     (i.e., not LISTs) can be passed as parameters. Verification of string affix (`*string`) rules.
   - Data flow of affixes, that is, generating warnings when, for example, the value of an output or transput affix
     is not consumed before being overwritten. This needs more work.
   - Consistency of import declarations with corresponding exports.
   - Consistency of EXT/ABSTR with declarations in SECTION.
   - Consistency of EXT/ABSTR with INV lists.
   - No undefined identifiers.
   - Rudimentary support for conditional compilation via `TEST cond: +.` and `TEST cond: -.`. Single level only,
     i.e., does not recursively propagate.
     Detection of unused data (CONST/LIST/VAR) and algorithms.
4. Syntax tree can be printed with the Pretty Printer. Includes ability to display in window using syntax colouring and
   other typographic devices.
5. Code Generation framework essentially complete, as well as can be verified by a single target code generator.
   - Target code generator selectable via command line.
   - First codegenerator (for PowerShell) complete. Code generated for CDL2 quicksort program runs correctly.
   - Working together, correctly implements the requirement that outut/transput affixed cannot be modified if the
     algorithm fails.
   - Separate compilation of modules not currently supported, tough some hooks are there in the target-independent code generator.
6. The database (syntax tree resulting from parsing) can be serialized to a JSON and gzipped into a file. It can be
   be uncompressed and deserialized at a later time without impacting function. This is in preparation for implementing
   a **CDL2 Laboratory** like IDE. 
