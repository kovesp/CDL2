# CDL2 Lab Commands

## Grammar

The grammar of commands if given by the vWG, a two level grammar.
The following productions (aka meta rules) are used in the more specific rules.

### Meta rules
```
EMPTY :: .
NOTION :: ALPHA ; NOTION ALPHA.
ALPHA :: a ; b ; c ; d ; e ; f ; g ; h ; i ; j ; k ; l ; m ; n ; o ; p ;
         q ; r ; s ; t ; u ; v ; w ; x ; y ; z.
DIGIT :: 0 ; 1 ; 2 ; 3 ; 4 ; 5 ; 6 ; 7 ; 8 ; 9.
SPECIAL :: + ; - ; ... .  # any unicode character not in ALPHA and DIGIT.
GLYPH :: ALPHA ; DIGIT ; SPECIAL.
STRINGGLYPH :: GLYPH.  # But white-space, control characters,
                       # " and $ are excluded from GLYPH
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

From the syntax it is clear that actual commands start with a command token and that all command tokens
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
example `print -printdepth 3`.

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



#### Focus Movement

```
next command : next token, selector option.
previous command : previous token, selector option.
first command : first token, selector option.
last command : last token, selector option.
```

The `next` and `previous` commands move the focus in a relative way. If the command is given without a selector, then the
focus moves by one object in the given direction. If the selector is given, then
the focus moves the the first object that matches the selector in the given direction.

The `first` and `last` commands move the focus to the first or last object that matches the selector. For
example, `first ALG` moves the focus to the first algorithm in the current section
`next Object /^n` moves the focus to the next object that whose name starts with `n`.

#### Object Movement

```
up command : up token, number option.
down command : down token, number option.
top command : top token.
bottom command : bottom token.
move command : move token, setting sequence option, selector.
```

The `up` and `down` commands move the focused object up or down in the list of sibling objects.
The `top` and `bottom` commands move the focused object to the first or last position among its siblings.

The `move` command moves the focused object before or after the object selected by the selector. The `before`
and `-after` (default) settings may be used to specify the position relative to the selected object.
What happens depends on what the current selection is as well as the selector:

    * If the focus is on a module or program, then the selector must select a module or program.
      The focused module/program is moved before or after the selected module/program.
    * If the focus is on a layer, then the selector must select 
        * A layer in the current or another module ... the selected layer is moved there. 
        * A layer in another module ... the selected layerr is moved there.
        * Another module ... the focused layer is moved to the module as the last layer.
    * If the focus is on a section, then the selector must select a module, layer or section.
      The section is moved similarly to the layer case above.       
    * If the focus is on an algorithm, constant, variable, or list, then the selector must select
      a section or an object in a section in a module (including the current one). the selected
      object is moved there as eithe the last object in the layer or adjacent to the object.
        
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
    an error is generated.

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

Display current settings or change them globally.

   * If no settings are given, all current settings are listed with their current values in tabular form.
   * In this command _only_ the otherwise required `-` may be omitted from the sattings name.
   * If the first setting is `-list` (or just `list`), then rest of the command line should contain the
     names of the settings whose values are to be listed. Example: 
     `set -list nomacroinlining -NoProcInlining target`. If `list` itself is specified among the names
      it is ignored since in this context it would always be shown as `true` instead of its actual global value.

The following is a list of settings.

##### Command Settings

| Setting Name | Type   | Default | Description |
|--------------|--------|---------|-------------|
| list         | bool   | false  | Commands that support this option will list appropriate object instead of taking action. See the undo and redo commands.
| inv          | bool   | false  | Apply the command to the `INV` list entry of the object.
| ext          | bool   | false  | Apply the command to the `EXT` list entry of the object.
| abstr        | bool   | false  | Apply the command to the `ABSTR` list entry of the object.
| import       | bool   | false  | Apply the command to the `IMPORT` list entry of the object.
| export       | bool   | false  | Apply the command to the `EXPORT` list entry of the object.
| prompt       | bool   | false  | For destructive commands, prompt before making changes.
| settag       | string |        | Sets a tag on an undo or redo entry.
| tag          | string |        | Selects the undo or redo entry with the given tag.
| separate     | bool   | false  | For code generation, see the `generate` command.
| before       | bool   | false  | Apply the command to the objectbefore the selected object.
| refs         | bool   | true   | For the `rename` command, rename all references to this object.
| file         | string | ""     | The file to use. Used by commands that read or write files.


##### Settings that Can Also Be Used on the Lab Invocation Command Line

| Setting Name | Type | Default | Description |
|--------------|------|---------|-------------|
| verbose | int  | 0    | Logging verbosity |
| debug-log | int  | 0 | Debug logging verbosity |
| target | string | "PowerShell" | The target code generator |
| program | string | "" | The default program to generate. **Special case:** If the focus is on a program, issuing `set program` will set the value from the name of the program.|
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

Performs semantic analysis on the selected program(s). If no selector is given, or the selection is not on a program, the
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

*The only `-target` settings currently implemented are `powershell` (the default),
CSHarp and C.*

For programs, the `-separate` setting may be used to generate code only for the program, but
not its modules. These then must be generated separartely. The default is to generated a single
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

##### Status
```
status command : status token, selector option.
```

Displays the Lab version, the number of modules and programs the current database
contains, and the number and types of objects in the current program or the program(s)
selected by the selector.


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

***bot***tom

***bye***

***c***onsult

***del***ete

***down***

***e***dit

***exit***

***f***ocus

***g***enerate

***h***elp

***i***nsert

***last***

***l***ist

***m***ove
 
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

***top***
  
***u***ndo

***up***

## Supported Builtin Procedures

The CDL2 Lab supports the following builtin procedures that may
be used in CDL2 code. 

* Tests. These succeed or fail like other tests and their name conventionally
  starts with `is`. They serve as conditional compilation tests. Example: the test
  `BUILTIN is target+"CSharp"` tests whether the current code generation
  target is `C#`. This could be used to conditionally select code
  depending on the target language. These tests are evaluated when
  semantic analysis is performed in compiler mode or when generating code.

* Functions. These return values in an output affix. Example:
  `BUILTIN date+date string` sets the output affix to the current date
  as a string. These functions are evaluated when code is generated.


#### Notes
   * All input arguments are specified as `*arg` and must be passed as literal strings.
   * The functions return their result in the last argument as an output affix.
   * The value is always returned as a string. This means that it must be
     passed to other algorithms in a string parameter position, i.e.,
     an affix declared as `*affix`.
   * The output affix must be
     * Local to the procedure that makes the call.
     * It can be used only in a single builtin call. The parser verifies this.
     * It _should_ not be used in any call prior to the builtin call. The parser will not try to verify this.
       Using it prior to the builtin call is **undefined**.
     * Currently, violating the above constraint will work, but this is not guaranteed in future and is counterintuitive.   
     * Subsequent to the call, it may only be used in a string position.
     * _Note: There will be no code generated for this local. It is just a convention for passing the builtin function value._

For example, the first version will work, but obviously you should not do it that way:
```
ACTION bad use of builtin -today: 
   print+"The current date is: ", print+today, BUILTIN date+today.
ACTION correct use of builtin -today: 
   BUILTIN date+today, print+"The current date is: ", print+today.
```

```
TEST is option*name
```
Succeeds if the given setting is true. `name` must be a valid boolean setting.
```
TEST is option value*name*value
```
Succeeds if the given setting has the given value. `name` must be a valid string
or numeric setting.
```
TEST is environment variable*name
```
Succeeds if the given environment variable is defined in the host operating system.
```
TEST is environment variable value*name*value
```
Succeeds if the given environment variable is defined in the host operating system
and has the given value.
```
TEST is target*target
```
Succeeds if the current code generation target is `target`.

### Functions
```
FUNCTION date+date>
```
The output affix is set to the current date as a string in the format `YYYY-MM-DD`.
```
FUNCTION time+time>.
```
The output affix is set to the current time as a string in the format `HH:MM:SS`.
```
FUNCTION version+version>.
```
The output affix is set to the current version of the CDL2 Lab as a string.
```
FUNCTION option*name+value>.
```
The output affix is set to the value of the given setting. `name` must be a valid
setting name. If the setting is

boolean
... the value is 0 or 1. Example:
```
FUNCTION print compilation option -stop:
   BUILTIN option+"StopOnWarnings"+stop,
     (equal+stop+one, print+"Stop on Warnings was on.";
      print+"Stop on Warnings was off.").
```
int
... the value is the integer value of the setting.
```
FUNCTION print compilation option -verb:
   BUILTIN option+"VerbosityLevel"+verb,
     (gt+verb+five, print+"High verbosity was enabled: ",print number+verb;
      print+"Low verbosity was set.").
```

string
... the value is the string value of the setting.
```
FUNCTION print compilation target directory -dir: 
   BUILTIN option+"OutputDirectory"+dir,print+dir).
```

```
FUNCTION environment variable*name+value>.
```
The output affix is set to the value of the given environment variable. If the variable is not defined,
the output affix is set to the empty string. The builtin `is option` can be used
to distinguish between a variable being undefined or set to the empty string.

## CDL2 Debugger and Backtrace Facility

Code generators may implement the CDL2 Debugger described in this section. Currently only the C code
generator supports it. Similarly, the backtrace facility described in this section is only supported
by the C code generator. Note however, that for PowerShell and C#, the generated code uses
the builtin language mechanism to generate a backtrace.

Backtrace in C is enabled by setting the `-backtrace` setting which is set by default.
Turning it off will result in somewhat smaller code size and a bit faster code, 
but you will not get a backtrace when an error occurs. Note that the backtrace is generated
for all run-time errors caught by the language libraries and the OS, the abort operator,
and any call on the C `exit` function with a non-zero exit code. 
 
The CDL2 Debugger is a simple command line debugger that allows you to set spy points
(aka breakpoints),
step through code, and inspect variables and lists. 
It is invoked by setting the `-trace` setting in addition to `-backtrace` (it is actually enough to set
`-trace` as that will force `-backtrace`). Note that the `-trace` setting also forces `-nomacroinlining`
and `-noprocinlining`.

The debugger will stop at the first Lude of the program and accept commands.

The debugger will prompt with the type, name and affixes (together with their values) as apporpiate. The
first character of the prompt is 

* `>` for algorithm entry, e.g., `>FUNCTION subtract+>a=1000000+>b=1+c>:`,
* `+` for successful exit, e.g., `+FUNCTION subtract+>a=1000000+>b=1+c>=999999`, and
* `-` for failure exit from PREDICATEs and TESTs, e.g., `- TEST gt`.

Note that by default the debugger does _not_ stop at the exit ports , but this can be changed.

At the prompt, commands can be entered. All commands consist of a single characterThere are two kinds:
* Some commands are acted on directly without waiting for anything else to be typed.
* Others take arguments and are executed when the Enter key is pressed. These commands are recorded
  in a command history and may be recalled using the up and down arrow keys.
  When a command is recalled, it can be edited before re-submission.



Note that what other debuggers call breakpoints, sw call spy points; what other debuggers call stack trace,
we call backtrace.

### Debugger Commands


| Command | Name | I | Details |
|---------|------|---|---------|
| > ENTER | next | Y | Step into. Continue until the text port
| s TAB   | step | Y | Step over. Continue until the exit port of the current call is reached. If exit ports are not being stopped at, continue to the next entry port.
| j SPC   | jump | Y | Continue until the next spy point.
| g END   | go   | Y | Continue until the end of the program.
| b       | backtrace | Y | Print the backtrace.
| q ESC   | quit  | Y | Exit the debugger and terminate the program.
|   UP/DOWN | history | Y | Recall the previous/next command from the command history. The command may be edited before re-submission using HOME, END, LEFT, RIGHT, DELETE, BACKSPACE.
| d       |decimal| N | Print value(s) in decimal. See below for arguments.
| x       |hex    | N | Print value(s) in hexadecimal. See below for arguments.
| c       |char   | N | Print value(s) as characters. See below for arguments.
| l       |list   | N | List objects in the program. See below for arguments.
| t       |type   | N | Print the source code of the algorithm at the current trace point (no argument), or the source for the algorithm(s) selected by the argument prefix. __Note__ that the source code is embeded in the generated code, thus always corresponds to what is executing ... the Laborczi effect is ruled out.
| +       |set    | N | Set spy points or options. See below for arguments.
| -       |clear  | N | Clear spy points or options. See below for arguments.|

The command history, spy points and some settings are saved into a file with the name of the executing program
and extension `.cdl2debug` (e.g., `csort.exe.cdl2debug`). Whever the debugger is invoked, these are reloaded
from the file.

#### Arguments for the print commands

| Argument | Description |
|----------|-------------|
| name    | The name of an affix, or variable, or a list. The value is printed. For a list, the first N items are printed as per the current option setting.
| list[...]| Elements of the list are printed in compact form. The ... specifies which elements to print. 
| list(...) | Print one element per line with its index.
| | Forms of ...: index, start:N, start:, :end, start-end, start-, -end. N is the number of elements. If any part of the range is omitted, the default number of elements are printed.
| | list[] is the same as list, and list() is the same as list but in per line format.

#### Arguments for the list command

| Argument | Description |
|----------|-------------|
| a pref | List algorithms whose name starts with the given prefix or all if prefix is omitted.
| v pref | List variables whose name starts with the given prefix or all if prefix is omitted.
| l pref | List lists whose name starts with the given prefix or all if prefix is omitted.
| s pref | List spy points whose name starts with the given prefix or all if prefix is omitted.
| o      | List the current options and their values.
| h      | List the command history.

#### Arguments for the set and clear commands

| Argument | Description |
|----------|-------------|
| alg pref | Set/Clear spy points on algorithms whose name starts with the given prefix.
| +        | Set or clear the option to stop at success exit ports. Default is set.
| -        | Set or clear the option to stop at failure exit ports. Default is set.
| =        | Set or clear the option to show objects with fully qualified names. Default is clear. Ludes are always show fully qualified.
| # N      | For the `+` command, set the default number of items when printing lists. The `- #` command reset this to 10.
| .        | Only for - (i.e., `- .`), clear the command history.

## CDL2 Code Generator Specifics

### C Code Generator

The C code genrator is geared to generate code in a target independent way. 
This governed by pre-processor definitions in `cdl2.h` that are used in the genrated
code as well as in the debugger. It looks like this:

```c
#include <stdint.h>
#define VALUE int64_t
#define VALUE_MAX INT64_MAX   
#define VALUE_MIN INT64_MIN
#define VALUE_UNDEFINED VALUE_MIN
#define VALUE_FMT "I64"

#define VALUE_DEC_FORMAT "%" VALUE_FMT "d"
#define VALUE_HEX_FORMAT "%" VALUE_FMT "x"
#define VALUE_DEC_FMT(n) "%0" #n VALUE_FMT "d"
#define VALUE_HEX_FMT(n) "%0" #n VALUE_FMT "x"
```

What is happenning here? `stdint.h` defines the integar types supported by the
target platfrom on which the code is compiled. `VALUE` is defined as a 64 bit signed
integer type. The requirement is that the type defined as `VALUE` must be able
to contain a pointer on the target platform, hence it has to be 64 bits 
on current Windows, Linux and MacOS platforms. The generated code declares
VARs, LISTs, affixes and locals to be of type `VALUE`. 
The `VALUE_MAX` and `VALUE_MIN` are provided for convenience.
The `VALUE_UNDEFINED` is used to initialize VARs, LISTs, locals and output
affixes.
 
The `VALUE_FMT` is the format specifier for printing and scanning `VALUE`s.
The normal way to use these is via the `VALUE_base_fmt` macros.
For eaxmple, `VALUE_DEC_FMT(10)` expands to `"%010I64d"` which is the format specifier for printing
a `VALUE` in decimal with at least 10 digits, padding with zeros if necessary.
Here is an example of a macro that prints its argument in decimal with 5 
zero padded digits:

```
ACTION show number +>number = 
    "printf(VALUE_DEC_FMT(5) $", $"" number ");".
```

Here is the equivalent macro for hex printing that uses just the base `VALUE_FMT`:

```
ACTION show number hex 16+>number = 
    "printf($"%016$" VALUE_FMT $"x$", " number ");".
```