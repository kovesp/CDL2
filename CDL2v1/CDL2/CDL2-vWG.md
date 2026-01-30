#cspell:ignore NOTETY endmod endlay endsec endprog abstr inout

# Meta rules
```
EMPTY :: .
NOTION :: ALPHA ; NOTION ALPHA.
ALPHA :: a ; b ; c ; d ; e ; f ; g ; h ; i ; j ; k ; l ; m ; n ; o ; p ; q ; r ; s ; t ; u ; v ; w ; x ; y ; z.
DIGIT :: 0 ; 1 ; 2 ; 3 ; 4 ; 5 ; 6 ; 7 ; 8 ; 9.
SPECIAL :: + ; - ; ... . # ... is any unicode character not listed in ALPHA and DIGIT.
GLYPH :: ALPHA ; DIGIT ; SPECIAL.
NOTETY :: NOTION ; EMPTY .
NOTION1 or NOTION2 :: NOTION1 ; NOTION2.
```

# Generic Rules
```
NOTION option : NOTION ; EMPTY.
NOTION sequence : NOTION ; NOTION, NOTION sequence.
NOTION1s separated by NOTION2s: NOTION1 ; NOTION1, NOTION2 token, NOTION1s separated by NOTION2s.
NOTION1s separated by optional NOTION2s: NOTION1 ; NOTION1, NOTION2 token option, NOTION1s separated by NOTION2s.
NOTION1s separated by NOTION2s ending with NOTION3: NOTION3 ; NOTION1, NOTION2 token, NOTION1s separated by NOTION2s ending with NOTION3.  
NOTION list : NOTIONs separated by commas.
NOTION1 or NOTION2 : NOTION1 ; NOTION2.
NOTION sentence : NOTION, period token.
NAME : identifier.
NOTION identifier: identifier.
NOTION NAME: NOTION token, NAME.
letter ALPHA token: small letter ALPHA token ; capital letter ALPHA token.
NOTION pack: open token, NOTION, close token.
any GLYPH except glyph exceptions: ... . # ... is any GLYPH except those listed in exceptions
glyph exceptions: GLYPH ; GLYPH or glyph exceptions.
```

# CDL2 Syntax

## Program Structure
```
program unit: module sequence, program.
NAME: identifier.
program: 
   program NAME sentence, 
      part declaration of modules, 
      lude of modules,
   endprog NAME sentence.
NOTION1 declaration of NOTION2s: NOTION token, NOTION2 identifier list sentence. 

LUDE: prelude ; root ; postlude.
lude of NOTIONs : NOTION prelude option, NOTION root option, NOTION postlude option.
NOTION LUDE: LUDE token, NOTION identifier list sentence.

module:
   module NAME sentence,
      layer sequence,
   endmod NAME sentence,
   lude of sections.
layer:
   layer NAME sentence,
      section sequence option,
   endlay NAME sentence.
section:
   section NAME sentence,
      interface declarations,
      section element sequence,
   endsec NAME sentence,
   lude of procedures.
INTERFACE: abstr ; ext ; inv ; import ; export.
INTERFACE declaration: INTERFACE declaration of procedures option.
interface declarations: abstr declaration, ext declaration, inv declaration, export declaration, import declaration.
section element: var declaration of identifiers, const declaration, list declaration, procedure.
```
## Object Declarations
```
const declaration: const token, const declaration item list sentence.
const declaration item: const identifier, equal token, const elements separated by optional semicolons.
const element: const identifier ; string.

list declaration: list token, list declaration item list sentence.
list declaration item: list identifier, open token,list lower bound,colon token,list upper bound,close token.
list NOTION bound: list bound.
list bound: decimal integer ; const identifier.

procedure: procedure head, procedure body.
procedure head: procedure type, procedure identifier, procedure argument sequence option, procedure local sequence option.
procedure type: function token ; action token ; test token ; predicate token.
procedure argument: input argument ; output argument ; inout argument.
input argument: plus token, greater than token, argument identifier.
output argument: plus token, argument identifier, grater than token.
inout argument: plus token, greater than token, argument identifier, grater than token.
procedure local: minus sign, local identifier.
procedure body: code body ; macro body.

code body: code body delimiter, alternatives separated by semicolons sentence.
code body delimiter: colon token, equal token option.
alternative: calls separated by commas ending with last call.
call: builtin token option,procedure identifier, actual parameter sequence option.
actual parameter: plus token, actual argument.
actual argument: const identifier ; list identifier ; var identifier ; argument identifier ; local identifier ; string.
last call: call ; group pack ; plus token ; minus token ; question mark token ; star token, group identifier option.
group: group label option, alternatives separated by semicolons.
group label: group identifier, colon token.

macro body: macro body delimiter, macro elements separated by optional semicolons sentence.
macro body delimiter: equal token, colon token option.
macro element: string ; actual argument.
```

Note: The keyword `BUILTIN` is used to indicate calls to builtin procedures.
This is an extension of the original CDL2 language.
The token must be followed by the name of a known builtin procedure; these are
documented in the CDL2 Lab Manual. There are two types of builtin procedures:

* Tests. These succeed or fail like other tests and their name conventionally
  starts with `is`. They serve as conditional compilation tests. Example: the test
  `BUILTIN is target+"CSharp"` tests whether the current code gneration
  target is `C#`. This could be used to conditionally select code
  depending on the target language. These tests are evaluated when
  semantic analysis is performed in compiler mode or when generating code.
* Functions. These return values in an output affix. Example:
  `BUILTIN date+date string` sets the output affix to the current date
  as a string. These functions are evaluated when code is generated.


# Lexical units
A not completely accurate syntax of where comments or NOTEs can be placed.
```
lexical unit: comment or note sequence option, some lexical unit.
some lexical unit: identifier ; integer ; float ; string ; special token ; code body delimiter ; macro body delimiter.

identifier : first glyph, subsequent glyph sequence option, last glyph.
first glyph : small letter ALPHA token.
subsequent glyph : small letter ALPHA token ; digit DIGIT token ; space token.
last glyph : small letter ALPHA token ; digit DIGIT token.

integer : decimal integer ; hexadecimal integer.
decimal integer : plus minus option, digit DIGIT sequence.
plus minus : plus token or minus token.
hexadecimal integer : digit 0 token, letter x token, hexadecimal digit sequence.
hexadecimal digit : digit DIGIT token ; letter a token ; letter b token ; letter c token ; letter d token ; letter e token ; letter f token.
float : decimal integer, decimal fraction, decimal exponent option ; decimal integer, decimal fraction option, decimal exponent.
decimal fraction : decimal separator token, decimal integer.
decimal exponent : letter e token, decimal integer.

string: quote token, string element sequence option, quote token.
string element: letter ALPHA token ; digit DIGIT token ; other glyph token except quote and dollar ; string escape.
string escape: string escape token, escaped item.
escaped item: letter l token ; letter t token ; string escape token ; quote token.

comment: line comment ; block comment.
line comment: line comment mark, comment body, line comment mark or eol token.
block comment: block comment mark, comment body, block comment mark or eol token.
comment body: any GLYPH except hash or eol sequence.
line comment mark: hash token.
block comment mark: hash token, hash token, hash token.
note: note token sentence. 
```

Comments can be placed almost anywhere between tokens. However they are collected and attched
to the nearest subsequent allowed attachment point.
They are also normalized during lexical analysis as follows:

- Adjacent line comments have the trailing # added if missing and their width is adjusted
  by inserting spaces to the longest comment in the group.
- Block comments are adjusted in a similar way, using ###. As well a line of #-s is
  added before and after. The lexical analyzer is careful to recognize already
  formatted block comments and to leave them unchanged.

# Representation
```
space token: ' '.
small letter a token: 'a'.
small letter z token: 'z'.
capital letter a token: 'A'.
capital letter z token: 'Z'.
digit 0 token: '0'.
digit 9 token: '9'.
comma token: ','.
semicolon token: ';'.
end token: '.'.
decimal separator token: '.'.
plus token: '+'.
minus token: '-'.
equal token: '='.
open token: '('.
close token: ')'.
colon token: ':'.
start token: "*".
greater than token: '>'.
SPECIAL: comma ; semicolon ; end ; plus ; minus ; equal ; open ; close ; start ; greater than.
special token: SPECIAL token.
quote token: '"'.
hash token: '#'.
eol token:  . # end of line character
string escape token: '$'.
other glyph token: NOTION symbol token.
any glyph: 'a' ; 'b' ; ...
at symbol token: '@'.
...
program token: 'PROGRAM'.
endprog token: 'ENDPROG'.
module token: 'MODULE'.
endmod token: 'ENDMOD'.
layer token: 'LAYER'.
endlay token: 'ENDLAY'.
section token: 'SECTION'.
endsec token: 'ENDSEC'.
function token: 'FUNCTION'.
action token: 'ACTION'
test token: 'TEST'.
predicate token: 'PREDICATE'.
builtin token: 'BUILTIN'.
prelude token: 'PRELUDE'.
root token: 'ROOT'.
postlude token: 'POSTLUDE'.
part token: 'PART'.
abstr token: 'ABSTR'.
ext token: 'EXT'.
inv token: 'INV'.
export token: 'EXPORT'.
import token: 'IMPORT'.
RESERVED: program ; endprog ; module ; endmod; layer ; endlay ; section ; endsec ; 
            function ; action ; test ; predicate ; prelude ; root ; postlude ; part ; abstr ; ext ; inv ; export ; import.
reserved token: RESERVED token.
note token: 'NOTE'.
```
