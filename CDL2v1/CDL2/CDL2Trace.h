#ifndef VALUE
#define VALUE long
#define BOOL VALUE
#define TRUE 1
#define FALSE 0
#include <stdio.h>
#include <stdlib.h>
#endif

#undef RETURNV
#undef RETURN
#define RETURNV(res) return c2TraceExit(res)
#define RETURN {c2TraceExit(TRUE);return;}

#define CALL_STACK_MAX_DEPTH 10000

typedef enum { C2_ALG_PREDICATE, C2_ALG_TEST, C2_ALG_ACTION, C2_ALG_FUNCTION } C2AlgType;
typedef enum { C2_DATA_CONST, C2_DATA_VAR, C2_DATA_LIST, C2_DATA_STRING } C2DataType;
typedef enum { C2_AFF_INPUT, C2_AFF_OUTPUT, C2_AFF_TRANSPUT, C2_AFF_STRING } C2AffType;

typedef struct {
    C2AlgType type;
    char* name;
    int nargs;
    VALUE* args;
    char** argnames;
    C2AffType* affTypes;
    int nlocals;
    VALUE** locals;
    char** localnames;
} C2StackFrame;

C2StackFrame C2Stack[CALL_STACK_MAX_DEPTH];
int C2SP = -1;

//struct C2Data {
//
//};

const char* c2AlgType(int depth) {
   switch (C2Stack[depth].type) {
      case C2_ALG_PREDICATE: return "PREDICATE";
      case C2_ALG_TEST: return "TEST";
      case C2_ALG_ACTION: return "ACTION";
      case C2_ALG_FUNCTION: return "FUNCTION";
      default: return "UNKNOWN";
   }
}  

void c2push_callstack_frame(C2AlgType type, char* name, 
                              int nargs, VALUE* args, char** argnames, C2AffType* affTypes,
                              int nlocals, VALUE** locals, char** localnames) {
   if (C2SP >= CALL_STACK_MAX_DEPTH-1) {
       fprintf(stderr, "Call stack overflow\n");
       exit(1);
   }
   C2SP++;
   C2Stack[C2SP].type = type;
   C2Stack[C2SP].name = name;
   C2Stack[C2SP].nargs = nargs;
   C2Stack[C2SP].args = args;
   C2Stack[C2SP].argnames = argnames;
   C2Stack[C2SP].affTypes = affTypes;
   C2Stack[C2SP].nlocals = nlocals;
   C2Stack[C2SP].locals = locals;
   C2Stack[C2SP].localnames = localnames;
}
void c2pop_callstack_frame() {
   if (C2SP < 0) {
       fprintf(stderr, "Call stack underflow\n");
       exit(1);
   }
   C2SP--;
}

#define ENTER_MARKER ">"
#define SUCCEED_MARKER "+"
#define FAIL_MARKER "-"
#define NO_MARKER ""

typedef enum { ENTER, EXIT, FAIL, ABORT } TraceExitType;

// Forward declarations
void c2PrintStackFrame(int depth, BOOL indent, char* marker);
void c2TraceEnter();
int  c2TraceExit(int v);
void c2TraceExitAbort();
void c2traceREPL();

// Function declarations
void c2Backtrace() {
    fprintf(stderr, "Call stack (most recent call last):\n");
    for (int i = C2SP - 1; i >= 0; i--) {
      c2PrintStackFrame(i,FALSE,NO_MARKER);
   }
   fprintf(stderr, ")\n");
}


void c2PrintStackFrame(int depth,BOOL indent, char* marker) {
   static char indentation_buffer[256];
   char * indentation = "";
   if (indent) {
       sprintf(indentation_buffer, "%*s", depth * 2, "");
       indentation = indentation_buffer;
   }

   fprintf(stderr, "%s%s%s%s(",indentation,marker,c2AlgType(depth),C2Stack[depth].name);
   for (int i = 0; i < C2Stack[depth].nargs; i++) {
      fprintf(stderr, "%s=%ld", C2Stack[depth].argnames[i], C2Stack[depth].args[i]);
      if (i < C2Stack[depth].nargs - 1) {
         fprintf(stderr, ", ");
      }
   }
   fprintf(stderr, ")\n");
}

// Called after c2_push_callstack_frame so call information is on the stack.
void c2TraceEnter() {
   c2PrintStackFrame(C2SP,TRUE,ENTER_MARKER);
   c2traceREPL();
}
// Called before the corresponding c2_pop_callstack_frame so call information is still on the stack.
int c2TraceExit(int v) {
   if (v == FALSE) {
      c2PrintStackFrame(C2SP, FALSE, FAIL_MARKER);
   } else {
      c2PrintStackFrame(C2SP, FALSE, FAIL_MARKER);
   }
   c2traceREPL();
   return v;
}
// Called for the abort operator
void c2TraceExitAbort() {
      fprintf(stderr, "Abort called\n");
      c2Backtrace();
      exit(1);
}

void c2traceREPL() {
   fprintf(stderr, "Enter 'c' to continue, 's' to step, 'b' to backtrace, or 'q' to quit: ");
   char command = getchar();
   while (command != 'c' && command != 's' && command != 'b' && command != 'q') {
      fprintf(stderr, "Invalid command. Enter 'c' to continue, 's' to step, 'b' to backtrace, or 'q' to quit: ");
      command = getchar();
   }
   switch (command) {
      case 'c':
         break;
      case 's':
         // Step: just return and let the next trace point be hit
         break;
      case 'b':
         c2Backtrace();
         c2traceREPL(); // After showing backtrace, ask for command again
         break;
      case 'q':
         exit(0);
         break;
   }
}
