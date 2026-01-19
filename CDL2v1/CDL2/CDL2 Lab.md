# CDL2 Lab Commands

## Grammar

The grammar of commands if given by the vWG, a two level grammar.
The following productions (aka meta rules) are used in the more specific rules.

### Meta rules
```
EMPTY :: .
NOTION :: ALPHA ; NOTION ALPHA.
ALPHA :: a ; b ; c ; d ; e ; f ; g ; h ; i ; j ; k ; l ; m ; n ; o ; p ; q ; r ; s ; t ; u ; v ; w ; x ; y ; z.
DIGIT :: 0 ; 1 ; 2 ; 3 ; 4 ; 5 ; 6 ; 7 ; 8 ; 9.
SPECIAL :: + ; - ; ... . # ... is any unicode character not listed in ALPHA and DIGIT.
GLYPH :: ALPHA ; DIGIT ; SPECIAL.
STRINGGLYPH :: GLYPH.  # But white-space, control characters, " and $ are excluded from GLYPH
NOTETY :: NOTION ; EMPTY .
NOTION1 or NOTION2 :: NOTION1 ; NOTION2.
NOTION1 and NOTION2 :: NOTON1 , NOTION2.

NOTION option : NOTION ; EMPTY.
NOTION sequence : NOTION1 ; NOTION1, NOTION2 sequence.
```

## Selectors

Selectors are used to select syntactic items in the code.
They can be used to select a single item or multiple items. 

Selectors are composed of a syntactic unit type followed by an absolute (no sign) or
relative (with a `+` or `-` sign) offset or the name of the slected unit (e.g., the
name of an algorithm); a regular expression may be used here.

### Selector Syntax
```
UNIT : program ; module ; layer ; section ; 
       abstr ; ext ; inv ; import ; imported ; export ;
       algorithm ; procedure ; macro ; function ; action ; test ; predicate ;
       variable ; constant ; list ;
       GUNIT.
GUNIT : any ; container ; data ; face ; object.
SUNIT : UNIT ; affix ; local ; call.
single selector : unit type, name selector option.
number : DIGIT token sequence.
offset : plus token or minus token option, number.
name selector : ALPHA sequence option ; regex token, regular expression.
selector : top token option, single selector sequence, ordinal option.
ordinal : colon token, offset.
unit type : SUNIT token.
top token : ^.
regex token : /.
```
#### Notes

- The distinction between UNIT and SUNIT is that SUNITs may be used to
  select objects in non-focus type commands, such as `print`, `list`, etc.
  UNITs are used when a focus setting command is used, such as `focus`,
  `next`, `previous`, etc. For examle one cant set the focus to a call
  (with `focus Call incr`), but one cal list all calls of incr in the
  current module (with `list Mod Call incr`).
- GUNITs are generic selectors.
  `ANY` will match any unit type, `CONTAINER` will match any container
  type (i.e., `MODULE`, `SECTION`, `LAYER`, `PROGRAM`), `FACE` will 
   match any interface, `DATA` will match
  `CONST`, `VAR`, or `LIST`), and `OBJECT` will match
  `ALGORITHM` or `DATA`.  
- The `top token` is used to indicate that the current focus is to be ignored.  
  Thus `^ ALG le` will select all algorithms that have *le* in their name, regardless
  of the current focus.
- If the `top token` is not given, then the current focus is used as the starting point
  for the selection. Thus `ALG le` will select all algorithms that have *le* in their name
  in the current module, section, layer, etc. If the current focus is a module, then it will
  select all algorithms in that module that have *le* in their name.
- One instance of `imported` may be placed before anywhere in the selector.
  Only imported algorithms and/or constants will then be selected. Other
  selectors will suppress instances of import declarations.
- The `name selector` matches any name that contains these characters. So `ALG le` matches
  any algorithm that has *le* in its name, e.g., `less`, `less than`, `clear`, etc. Use a regular
  expression to be more specific, e.g., `ALG /^le` matches only algorithms that start with *le*.
- A regular expression is what is allowed in dotnet regular expressions. An example is
  `ALG /^(less|greater)` which, for example, matches *less*, *less or equal*, *greater*,
  *greater or equal*, and possibly others.
- `Single selectors` must be given in hierarchical sequence. Thus `MOD sort SEC arith` is valid, 
   but `SEC arith MOD sort` is not.
- The `unit type`-s must start with a capital letter, but unlike in the code, they need not be
  all caps. This is also true when entering code. Notice that some of the unit types are *not*
  CDL2 reserved words, rather they extend the syntax to enable sub units.
- In a `selector` one needs specify only those units that select what you are looking for. There
  is a concept of *current unit* or *focus* ... many commands change the focus, about which
  more later.
- When the `offset or name selector` is omitted, then any unit of the given type is matches.\
  Note that measn that the `unit type` is also redundant. Thus `MOD sort LAY ALG le` is the same as
  `MOD sort ALG le` and `LAY ALG le` is the same as `ALG le`.
- A selector clearly may select multiple objects of the same type (this type is the last `unit type`
  in the sequence.). If the contxt requires a single object, then the first one is selected, otherwise
  all are (e.g., this is the case in the `print` command). 
- The ordinal may be used to select a specific object from what is selected by the rest of the
  selector. It is OK to give an ordinal that is greater than the number of selected objects, in
  which case the last object is selected. For example, `SEC arithmetic ALG less : 100` selects the
  last algorithm in the section `arithmetic` that has less in its name. If the ordinal is not given,
  then the first object is selected. If the ordinal is given, then it must be a positive integer.
- Just to be clear, the `ordinal` is not a part of the last `single selector`, but rather is
  applied to the whole `selector`.

#### List of Unit Types

In the following list of unit types captialization shows the minimal abreviation that is allowed.
So for example, the selector `S /^a Te /^le : 100` will select the last `TEST` whose name starts
with *le* in from all the such algorithms in any section whose name starts with *a*. Note however that this is
not deterministic because the order of `MODULE`s in the database is undefined. As well, this selector selccts
a single object, not the last object in each matching section.

UNITs:
```
ABSTR
ACtion
ALGorithm
ANY
CONst
CONTAINER
DATA
EXPort
EXT
FACE
FUnction
IMPort
INV
LAYer
LIST
MACro
Module
NOTE
OBJECT
PART
POSTlude
PRedicate
PRELude
PROCedure
Program
ROOT
Section
TEst
VAR
```
SUNITs:
```
AFFix
Call
LOCal
```

### Settings Syntax

```
setting : minus token, actual setting.
setting in set: minus token option, actual setting.
actual setting : boolean setting ; numeric setting ; string setting.
boolean setting : setting name, plus token or minus token option.
numeric setting : setting name, value indicator, number.
string setting  : setting name, value indicator, STRINGGLYPH sequence or string.
setting name : ALPHA token sequence.
value indicator : colon token ; equals token.
```

#### Notes

1. For boolean settings there are three possibilities:
    * The setting name is followed by a +: the setting is set to true.
    * The setting name is followed by a -: the setting is set to false.
    * There is neither a + or a -: the global value of the setting value is flipped.
2. The `string` in `string setting` is a CDL2 string as specified in the CDL2 grammar. It is a double
   quoted string which may contain escapes. Quoting is only required if the string contains
   white space, control characters, dollar or double quote.

Examples:

  * Boolean: `-list`, `-list-`, `-list+`.
  * Numeric: `-autoSaveCount:10`, `-autoSaveInterval=60`.
  * String:  `-target:C#`, `-file=C:\CDL2\Tests.cdl2`, `-title:"$"Importable$" Modules"`.

## Non-Commands

From the syntax it is clear that actuall commands start with a command token and that all command tokens
are lower case. Anything that does not start with a command token is treated as a CDL2 code snipet.

A code snipet always begins with one of the CDL2 reserved words that start a syntactic unit 
(see the list under UNITs above). These *do **not*** include the ones added by the Lab: `ALGORITHM`, `ANY`,
`CONTAINER`, `DATA`, `FACE`, `MACRO`, `OBJECT`, `PROCEDURE`.

Code snipets behave more or less as if preceeded by the `add` command except as described here.

If the code snipet specifies a module, layer, section, program then it is added

  * The code snipet is considered to be a single line and edit mode is **not** entered.
  * If the focus is on a unit of the same kind, then after the focused unit.
  * Otherwise it is added at the end of the parent unit or at the end of the list of modules or programs.

If the code snipet is a part, then the focus must be on a program.

If the code snipet is an interface declaration, then the focus must be on or in a section.

If the code snipet is a lude (i.e., prelude, root, postlude), then the focus must be

  * On a program.
  * On a module: the lude is added to the module.
  * On or in a section: the lude is added to the section.

Otherwise the snipet is a CDL2 object (i.e., algorithm, constant, variable, or list), in which case the behaviour
is as if an `add` command were used. That is:
  * Edit mode is entered with the code snipet in the input area.
  * When editing is complete, the object is added either at the end of the current section or after the
    focused object.
  * This will fail if the focus is not in a section.

## Commands

Commands are typed into the input area of the CDL2 Lab. The lab maintains a command history acrsoss sessions. Entries in the
history may be recalled using the up and down arrow keys and edited before re-submission.

### Comments

Lab input lines that start with `!` are comments and are ignored. Note that the CDL2 comment character `#` cannot be used
for this purpose, since CDL2 code snipets may contain comments.

### Command Settings

Settings may be set globally (see the `set` command) or locally
(using the the `setting sequence option` in the commands below). When using the latter, the setting
is changed for the duration of the command, in the former case it is changed globally.
Any setting may be specified for commands, tough of course not all are relevant for all commands.

For other settings the setting is followed by the value of the setting, for
example `print -print-depth 3`.

### Focus Change Commands

These commands change the focus of the commands that follow. The focus is the current unit.

#### Focus

```
focus command : focus token, setting sequence option, selector option.
```

Sets the focus to the first object selected by the selector. If the selector is omitted,
the focus does not change. In all cases the focus is printed. If the auto-print options is set,
then if the focus is a "small" unit (i.e., a variable, list, constant, algorithm, of any subunit
thereof), then the focus is printed in detail, otherwise only the name of the object is printed.
Using this command with just a number will move the focus to the object of the same kind in the
sequence of objects with that number, or relatively. Note that this meand that `f +2` is equivalent
to `n 2` and `f -1` is equivalent to `p 1`.



#### Next and Previous

```
next command : next token, setting sequence option, selector option.
previous command : previous token, setting sequence option, selector option.
```

Moves the focus in a relative way. If the command is given without a slector, then the
focus moves by one object in the given direction. If the selector is given, then
the focus moves the the first object that matches the selector in the given direction.

### Listing and Printing Commands

#### List

```
list command : list token, setting sequence option, selector option.
```

The command list all the objects that match the selector. The list command with no selector is the
same as the focus command with no selector, except the auto-print setting is ignored.

#### Print

```
print command : print token or type token, setting sequence option, selector option.
```

The command prints all the objects that match the selector. The print command with no selector
prints the focused object. The command is is subject the the ``print-depth`` setting. 
For eaxmple, ``print -print-depth 2 MOD`` will print the current module and all its sections. It
will look something like this:
```
MOD myModule
  LAY layer1
    SEC section1
    SEC section2
   LAY layer2
      SEC section3
      SEC section4
```
Notice that if the ``print-depth`` is set to -1 (the default), then the entire object is printed.
If the depth is such that printing of structures is suppressed, then the ENDMOD, ENDLAY and ENDSEC
is omitted as in the above example.
Here is another example: `print -print-depth 3 ALG`. This will print the above structure, but also
add all the algorithm headers without the locals if any.

The `-file` setting may be used to print to a file instead of the output area. The settings may used as follows:

 * `-file=filename`. The output is written to the given file. If the file exists, it is overwritten.
 * `-file=filename::append`. The output is appended to the given file. If the file does not exist, it is created.
 * `-file=::append`. The output is appended to the current file. If there is no current file yet
    an error is genrated.

If the filname has no path (directory) component, then the file is created in the current output directory.
If the filename has no extension, then `.cdl2` is used.

For example, `print -file=output::append MOD` will append the printed module to the file `output.cdl2`.

#### Consult
```
consult command : consult token, setting sequence option, filename.
```
The command operates in two modes depending on the content of the file selected by the argument (this
is a character sequence that is valid as a file name on the host operating system). The file extension
may be omitted; if so, `.labc` (i.e, lab commands) and `.cdl2` are tried in that order. However the
extension may be any valid file extension and in no case determines the mode of operation.
  * If the file contains (starts with) one or more CDL2 containers (currently only `MODULE` and `PROGRAM`
    are supported) then the code is parsed and added to the database as if
    the code were read via `--sources` on the command line in compiler mode. Note that this means that
    the containers must have their terminating keywords.
  * Otherwise the file is treated as a sequence of lab commands, oen per line. These are executed in
    order. Blank lines and lines starting with `!` are ignored. Note that it makes no sense to
    have `edit` commands in such a file as that would switch to input mode and hang. As wll, nested `consult`s
    are not supported.

### Settings

#### Set

```
set command : set token, setting sequence option. 
```

If no settings are given, all current settings are listed. The following is a list of settings.

##### Command Settings

| Setting Name | Type   | Default | Description |
|--------------|--------|---------|-------------|
| list         | bool   | false  | Commands that support this option will list appropriate object instead of taking action. See the undo and redo commands.
| inv          | bool   | false  | Apply the command to the INV list entry of the object.
| ext          | bool   | false  | Apply the command to the EXT list entry of the object.
| abstr        | bool   | false  | Apply the command to the ABSTR list entry of the object.
| import       | bool   | false  | Apply the command to the IMPORT list entry of the object.
| export       | bool   | false  | Apply the command to the EXPORT list entry of the object.
| prompt       | bool   | false  | For destructive commands, prompt before making changes.
| settag       | string |        | Sets a tag on an undo or redo entry.
| tag          | string |        | Selects the undo or redo entry with the given tag.
| separate     | bool   | false  | For code generation, see the `generate` command.
| before       | bool   | false  | Apply the command before the selected object(s).
| refs         | bool   | true   | For the `rename` command, rename all referrences to this object.
| file         | string | ""     | The file to use. Used by commands that read or write files.


##### Settings that Can Also Be Used on the Lab Invocation Command Line

| Setting Name | Type | Default | Description |
|--------------|------|---------|-------------|
| verbose | int  | 0    | Logging verbosity |
| debug-log | int  | 0 | Debug logging verbosity |
| target | string | "PowerShell" | The target code generator |
| program | string | "" | The default program to generate |
| stop-on-warnings | bool | false | If true, then code code cannot be generated if the program or any parts have warnings. |
| allow-errors | bool | false | If true, then code code can be generated even if there are errors. |
| gen-debug-info | bool | false | If true, then debug information is generated. Not implemented. |
| output-dir | string | "" | The directory where the generated code is written. If empty, then the current directory is used. |
| lab | string | "" | The name of the lab database file. it is in output-dir with extension `.lab.gz`. |
| no-macro-inlining | bool | false | If true, then macros are not inlined. This is useful for debugging. |
| no-proc-inlining | bool | false | If true, then procedures are not inlined. This is useful for debugging. |
| messages | string | "all" | The messages to show. Can be "all", "errors", "warnings", "info", or "none". |
| report-all | bool | false | If true, then all messages are reported, otherwise only those that pertain to reachable objects. |
| auto-print   | bool | false   | If true, then the focused object is printed after the command. |
| print-depth  | int  | -1      | The depth of printing. If -1, then the entire object is printed. |
| auto-save-number  | int | 3   | The number of autosaves kept. Treated as 1 if <= 1. Older ones are removed.|
| auto-save-count  | int | 20   | The database is saved after this many commands that modify it (the editing commands). Set to 0 to disable. |
| auto-save-interval | int | 0| The database is saved after this many seconds if there are any modifications. Set to 0 to disable. |
| auto-analyze | bool | false| Run the semantic analyzer after each change. |
| command-history-size | int | 100 | The number of commands to keep in the command history. |

The names of the command line settings are in most cases the same as listed in thsi table, but with
each word capitalized and dashes removed. For example, `--command-hisotry-Size` becomes `CommandHistorySize`.
There are some exceptions:

| Command Line Name | Setting Name |
|-------------------|--------------|
| --verbose         | VerbosityLevel |
| --debug-log       | DebugVerbosityLevel |
| --program         | ProgramName |
| --output-dir      | OutputDirectory |


#### Editing Commands

##### Add

```
add command : add token, setting sequence option, selector option.
```

The command has two forms. 

  1. One or more of the `-inv`, `-ext`, `-abstr`, `-import`, or `-export` settings are
     given. In that case the selected object(s) is (are) added to the respective interface lists
     of the section and no other action happens.
  2. Otherwise the command will add some kind of CDL2 object, if possible. The command may 
     have the `-before` setting. Edit mode is entered in the input area which is
     initially empty. When editing is complete, the object is parsed and furhter action is determined
     by the result as follows:
     * If there are syntax errors, an error message is displayed and no further action is taken.
     * If the parsed object already exists (an object with the same name in the same scope),
       then the user is prompted to confirm that the object is to be replaced. If the
       `-prompt-` settings was given, the prompt is suppressed.  
     * The parsed object is a container. The appropriate terminating keyword
       (`ENDPROG`, `ENDMOD`, `ENDLAY`, `ENDSEC`) is supplied automatically. Then,
       * If it is a Program or Module, it is added before or after the selected program/module,
         or at the end of the list of programs/modules if no program/module is selected.
       * If it is a Layer/Section then the selection must be on or inside a Module/Layer. The object
         is added before or after the selected Layer/Section, or at the end of the Module/Layer if no
         Layer/Section is selected.
       * The parsed object is a lude. The selection must be on or inside a Program/Module/Section.
         There are two cases.
         * If the parsed lude is a single item, then it is added to the current lude at the end.
         * If the parsed lude contains more than one item (i.e., it is a comma separated list,
           then the items replace the current lude. This is the only way to change
           the ordering of items in the lude or delete items from it.
        * The parsed object is an intrface declaration. The selection must be on or inside a Section.
          The undo stack is updated. The interface declaration is added to the respective
          interface list. _Order is irrelevant here, since interface lists
          are always alphabetically sorted. Hence the `-before` setting is ignored._
        * The parsed object is a CDL2 object (i.e., algorithm, constant, variable, or list). The
          selection must be on or inside a Section. The undo stack is updated. If
          the object exists, the user is prompted as noted above.
          The object replaces the existing object, is added before or after the selected object,
          or at the end of the Section as appropriate.

##### Edit

```
edit command : edit token, setting sequence option, selector option.
```

The command enters edit mode with the selected object in the input area. When editing is complete,
the object is parsed. If this succeeds, the object replaces the existing object. _Note: if an attempt is made to
change the identity or type of the object, the command will fail._

The edit command supports the editing of:
  * algorithms, and constants.
  * Section ludes (treated as algorithms).

Not supported:
  * Containers ... too big. Support is planned for containers later by invoking an external editor
    on the container via a temprary file. As a stop-gap solution, one may use the `print` command to
    print the container to a file (not yet implemented), edit it externally, and then use the `consult`
    command to reload (not yet implemented for replacing an existing container).
  * Variables, lists, module and program ludes ... these are just lists of ids, delete or add items. 
  * Interface declarations ... used the add and delete command with the appropriate settings.

##### Rename
```
rename command : rename token, setting sequence option, selector option, new name.
new name : equals token, ALPHA token sequence.
```

Renames the selected object to the new name. 

   * The new name may be the same as the current name with different distribution of white spaces.
     Example: `rename ALG myAlg = my Alg`.
   * The new name must be a valid CDL2 identifier which is not currently in use in the same scope.     
   * If `refs` is true (the default) all references to the renamed object are updated automatically.
     An example of when you might use the `-refs-` setting is when you want to rename an object and
     then supply a different object with the original name.

##### Delete/Remove
```
delete command : delete token, setting sequence option, selector option.
remove command : remove token, setting sequence option, selector option.
```
Deletes the objects selected by the selector (or the focused object if no selector is given)
from the database. The deleted objects are placed on the undo stack, so they may be
restored using the undo command.

When an object is removed, its position among its siblings is **not** retained, see the
`undo` command for details.

If the object occurs in interface lists (i.e., it is an algorithm or constant) it is removed from
these lists as well. However, the undo record will retain the information needed to restore
the object to these lists by `undo`.

If one or more of the `inv`, `ext`, `abstr`, `import`, or `export` settings are given,
then the selected object(s) is (are) removed from the respective interface lists of the section.

If the `list` setting is given, then no objects are removed, instead the objects that
would be removed are listed.

If the `prompt` setting is given, then the user is prompted to confirm the deletion
of each object. If the user tries to delete a container (i.e., a module, layer,
or section) that contains anything or a program, then the user is prompted unless `-prompt-` is given.

##### Undo/Redo

```
undo command : undo token, setting sequence option, undo redo select option.
redo command : redo token, setting sequence option, undo redo select option.
undo redo select: colon token option, number.
```
The undo command undoes the change(s) made to the database. The redo command redoes
the change(s) that were undone. 
If no number is given, then a single change is undone or redone.

Relevant settings:

   * `-list`. If given, no changes are made, instead the contents of the undo or redo stack is listed.
   * `-settag:tag`. The selected undo or redo entry has its tag set. If the tag is `-' then the tag is cleared.
   * `-tag:tag`. The undo/redo entry with the given tag is selected and performed. In other words, this
     form may be used to undo or redo a specific change by its tag as with the `:n` argument. 

Currently the changes to the folowing can be undone/redone:

* CDL2 objects (i.e., algorithms, constants, variables, and lists).
* Interface list entries.

No supported (yet?):

  * Containers (i.e., programs, modules, layers, sections).
  * Ludes.
  * Program parts.

The arugment of these commands takes two forms:
   * A number `n` is given. In this case the last `n` changes are undone or redone.
   * A number prefixed by a colon is given. In this case the undo/redo item with the given
     number (as listed by the `-list` setting) is undone or redone.

When an object is removed, its position among its siblings is **not** retained.
When the object is restored using `undo`, it is added at the end of the list of siblings. 

#### Semantic Analysis
```
analyze command : analyze token, setting sequence option, selector option.
```

Performs semantic analysis on the selected program (the selector or focus must select
a single program). If no selector is given, or the selection is not on a program, the
default program specified by the `-program` setting is analyzed.

Notes:

   * Code generation automatically performs semantic analysis.
   * As code is edited, added or removed, or when code is consulted fron a file
     semantic analysis is **_not_** performed automatically. This means that
     proper notes will not be attached to objects and the syntax highlighting when
     pretty printed may be incorrect. The user must explicitly invoke the `analyze` command.
     * The `autoAnalyze` setting may be used to have semantic analysis performed
       after each change. This may slow down editing significantly, however. _Not implemented._

#### Code Generation
```
generate command : generate token, setting sequence option, selector option.
```

Generates code for the selected program or module (the selector or focus must select one or
more programs or modules)

The `-target` setting may be used to specify the target code generator.

*The only `-target` setting currently implemented is `powershell` and is the default.*

For programs, the `-separate` setting may be used to generate code only for the program, but
not its modules. These then must be generated separately. The default is to generated a single
target program for the entire program including all objects that are actually used from all its
modules.

For modules, the `-separate` setting may be used to generate code only for the module, without
inlining objects from other modules.

*The `-separate` setting is not yet implemented.*

####  General Commands

##### Help

```
help command : help token, command name option.
command name : valid command name ; selectors token ; settings token.
```

Displays the list of commands, or the help for the given command.
If `selectors` is given, then the list of valid selectors is displayed.
If `settings` is given, then the list of valid settings is displayed.

##### Shell
```
shell command : shell token, GLYPH sequence.
```

Executes the given command line in the host operating system shell. 
For example, on Windows: `shell dir C:\CDL2`.
If you need to pass switches to the shell command escape the minus sign with a backslash.
For example, `shell --shell=bash ls \-l` will execute `ls -l` in bash.


##### Quit/Exit/Bye/Abort
```
quit command : quit token.
exit command : exit token.
bye command : bye token.
abort command : abort token.
```

Exits the CDL2 Lab. `quit`, `exit` and `bye` are identical: the lab database is saved
before exiting. The `abort` command exits without saving the database.

### Command names and abreviations

Comands are always given in all lower case, but may be abbreviated. In the following list,
the minimal apreviation is given in ***bold italic***.

***abort***

***a***pend

***a***dd

***bye***

***c***onsult

***del***ete

***e***dit

***exit***

***f***ocus

***g***enerate

***h***elp

***i***nsert

***last***

***l***ist
 
***n***ext 

***p***revious

***pr***int

***re***do

***rem***ove

***ren***ame

***r***eplace

***quit***

***s***ave

***set***

***sh***ell

***stat***us

***t***ype
 
***set***

***stat***us

***t***ype
  
***u***ndo

 
 


