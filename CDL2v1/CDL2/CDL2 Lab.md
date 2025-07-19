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
       variable ; constant ; list ; any ; container ; object.
SUNIT : UNIT ; affix ; local ; call.
single selector : unit type, name selector option.
offset : plus token or minus token option, DIGIT sequence.
name selector : ALPHA sequence option ; regex token, regular expression.
selector : top token option, single selector sequence, ordinal option.
ordinal : colon token, offset.
unit type : SUNIT token.
top token : ^.
regex token : /.
setting : plus or minus token, setting name, setting argument option.
```
#### Notes

- The distinction between UNIT and SUNIT is that SUNITs may be used to
  select objects in non-focus type commands, such as `print`, `list`, etc.
  UNITs are used when a focus setting command is used, such as `focus`,
  `next`, `previous`, etc. For examle one cant set the focus to a call
  (with `focus Call incr`), but one cal list all calls of incr in the
  current module (with `list Mod Call incr`).
- `ANY` will match any unit type, `CONTAINER` will match any container
  type (i.e., `MODULE`, `SECTION`, `LAYER`, `PROGRAM`), `DATA` will match
  `CONST`, `VAR`, or `LIST`), and `OBJECT` will match
  `ALGORITHM` or `DATA`.  
- The `top token` is used to indicate that the current focus is to be ignored.  
  Thus `^ ALG le` will select all algorithms that have *le* in their name, regardless
  of the current focus.
- If the `top token` is not given, then the current focus is used as the starting point
  for the selection. Thus `ALG le` will select all algorithms that have *le* in their name
  in the current module, section, layer, etc. If the current focus is a module, then it will
  select all algorithms in that module that have *le* in their name.
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
PRElude
PROCedure
PROGram
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

## Commands

## Command Settings

Settings may be set globally (see the `set` command) or locally
(using the the `setting sequence option` in the commands below). When using the latter, the setting
is changed for the duration of the command, in the former case it is changed globally.
Any setting may be specified for commands, tough of course not all are relevant for all commands.
For boolean settings the leading + or - determines whether the setting is on or off. For
other settings the setting is followed by the value of the setting, for
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
print command : print token, setting sequence option, selector option.
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
If the depth is such that printing of structures is supressed, then the ENDMOD, ENDLAY and ENDSEC
is omitted as in the above example.
Here is another example: `print -print-depth 3 ALG`. This will print the above structure, but also
add all the algorithm headers without the locals if any.

### Settings

#### Set

```
set command : set token, setting sequence option.
```

If no settings are given, all current settings are listed. The following is a list of settings.

##### General Settings

| Setting Name | Type | Default | Description |
|--------------|------|---------|-------------|
| auto-print   | bool | false   | If true, then the focus is printed after the command. |
| print-depth  | int  | -1      | The depth of printing. If -1, then the entire object is printed. |
| auto-save-count  | int | 10    | The database is saved after this many commands that modify it (the editing commands).
| auto-save-interval | int | 60 | The database is saved after this many seconds if there are any modifications. |

##### Command Line Settings

| Setting Name | Type | Default | Description |
|--------------|------|---------|-------------|
| verbose | int  | 0    | Logging verbosity |
| debug-log | int  | 0 | Debug logging verbosity |
| target | string | "PowerShell" | The target code generator |
| program | string | "" | The default program to generate |
| stop-on-warmings | bool | false | If true, then code code cannot be generated if the program or any parts have warnings. |
| allow-errors | bool | false | If true, then code code can be generated even if there are errors. |
| gen-debug-info | bool | false | If true, then debug information is generated. Not implemented. |
| output-dir | string | "" | The directory where the generated code is written. If empty, then the current directory is used. |
| lab | string | "" | The name of the lab database file. it is in output-dir with extension `.lab.gz`. |
| no-macro-inlining | bool | false | If true, then macros are not inlined. This is useful for debugging. |
| no-proc-inlining | bool | false | If true, then procedures are not inlined. This is useful for debugging. |
| messages | string | "all" | The messages to show. Can be "all", "errors", "warnings", "info", or "none". |
| report-all | bool | false | If true, then all messages are reported, otherwise only those that pertain to reachable objects. |


#### Editing Commands

#### Code Generation

####  General Commands

##### Help

```
help command : help token, command name option.
```

Displays the list of commands, or the help for the given command.

##### Quit
```
quit command : quit token.
```

Exits the CDL2 Lab after saving the database.

### Command names and abreviations

Comands are always given in all lower case, but may be abbreviated. In the following list,
the minimal apreviation is given in ***bold italic***.


***f***ocus  
***n***ext  
***p***rev  
***l***ist  
***pr***int  
***set***  
***r***eplace  
***a***ppend  
***i***nsert  
**e***dit  
***u***ndo  
***g***enerate  
***quit***
***h***elp
