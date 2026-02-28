#ifndef VALUE
#define VALUE long
#define BOOL VALUE
#define TRUE 1
#define FALSE 0
#include <stdio.h>
#include <stdlib.h>

#ifdef _WIN32
    #include <conio.h>
#else
    #include <termios.h>
    #include <unistd.h>
#endif
#endif

#undef RETURNV
#undef RETURN
#define RETURNV(res) return c2TraceExit(res)
#define RETURN {c2TraceExit(TRUE);return;}

#define VAL(aff) {.val=aff}
#define PTR(aff) {.ptr=aff}
#define STR(aff) {.str=aff}

#define CALL_STACK_MAX_DEPTH 10000

typedef enum { C2_ALG_PREDICATE, C2_ALG_TEST, C2_ALG_ACTION, C2_ALG_FUNCTION } C2AlgType;
typedef enum { C2_DATA_CONST, C2_DATA_VAR, C2_DATA_LIST, C2_DATA_STRING } C2DataType;
typedef enum { C2_AFF_INPUT, C2_AFF_OUTPUT, C2_AFF_TRANSPUT, C2_AFF_STRING } C2AffType;

typedef union { VALUE val; VALUE* ptr; char* str; } C2DataValue;

typedef enum { TRACE_ENTER, TRACE_EXIT, TRACE_FAIL } TraceExitType;

typedef struct {
    C2AlgType type;
    char* name;
    int nargs;
    C2DataValue* args;
    char** argnames;
    C2AffType* affTypes;
    int nlocals;
    VALUE** locals;
    char** localnames;
} C2StackFrame;

C2StackFrame C2Stack[CALL_STACK_MAX_DEPTH];
int C2SP = -1;

// Cross-platform immediate character input
char c2_getch() {
#ifdef _WIN32
    return _getch();
#else
    struct termios oldattr, newattr;
    char ch;
    tcgetattr(STDIN_FILENO, &oldattr);
    newattr = oldattr;
    newattr.c_lflag &= ~(ICANON | ECHO);
    tcsetattr(STDIN_FILENO, TCSANOW, &newattr);
    ch = getchar();
    tcsetattr(STDIN_FILENO, TCSANOW, &oldattr);
    return ch;
#endif
}

//struct C2Data {
//
//};

char* c2AlgType(int depth) {
   switch (C2Stack[depth].type) {
      case C2_ALG_PREDICATE: return "PREDICATE";
      case C2_ALG_TEST: return "TEST";
      case C2_ALG_ACTION: return "ACTION";
      case C2_ALG_FUNCTION: return "FUNCTION";
      default: return "UNKNOWN";
   }
}  

void c2push_callstack_frame(C2AlgType type, char* name, 
   int nargs, C2DataValue args[], char* argnames[], C2AffType affTypes[],
   int nlocals, VALUE* locals[], char* localnames[]) {
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

char * C2Marker(TraceExitType type) {
   switch (type) {
      case TRACE_ENTER: return ENTER_MARKER;
      case TRACE_EXIT: return SUCCEED_MARKER;
      case TRACE_FAIL: return FAIL_MARKER;
      default: return NO_MARKER;
   }
}

typedef enum { ENTER, EXIT, FAIL, ABORT } TraceExitType;

// Forward declarations
void c2PrintStackFrame(int depth, BOOL indent, char* marker, BOOL newline);
void c2TraceEnter();
int  c2TraceExit(int v);
void c2TraceExitAbort();
void c2traceREPL(int depth,TraceExitType type);
void c2PrintAff(int depth,int i);

// Function declarations
void c2Backtrace() {
   fprintf(stderr, "Call stack (most recent call last):\n");
   for (int i = C2SP - 1; i >= 0; i--) {
      c2PrintStackFrame(i,FALSE,NO_MARKER,TRUE);
   }
}   
   
void c2PrintStackFrame(int depth,BOOL indent, char* marker,BOOL newline) {
   static char indentation_buffer[256];
   char * indentation = "";
   if (indent) {
      sprintf(indentation_buffer, "%*s", depth, "");
      indentation = indentation_buffer;
   }
   if (!newline) fprintf(stderr,"\n"); // Print a newline before the stack frame when used as debugger prompt
   fprintf(stderr, "%s%s%s %s",indentation,marker,c2AlgType(depth),C2Stack[depth].name);
   for (int i = 0; i < C2Stack[depth].nargs; i++) c2PrintAff(depth, i);
   fprintf(stderr,newline ? "\n" : ": ");
}

void c2PrintAff(int depth,int i) {
   char* name = C2Stack[depth].argnames[i];
      switch (C2Stack[depth].affTypes[i]) {
      case C2_AFF_INPUT:    fprintf(stderr, "+>%s=%ld", name,C2Stack[depth].args[i].val); break;
      case C2_AFF_OUTPUT:   fprintf(stderr, "+%s>=%ld", name,*C2Stack[depth].args[i].ptr); break;
      case C2_AFF_TRANSPUT: fprintf(stderr, "+>%s>=%ld",name,*C2Stack[depth].args[i].ptr); break;
      case C2_AFF_STRING:   fprintf(stderr, "*%s=%s",   name,C2Stack[depth].args[i].str); break;
      default: fprintf(stderr, "+??");
   }
}

// Called after c2_push_callstack_frame so call information is on the stack.
BOOL firstEntry = TRUE;
void c2TraceEnter() {
   if (firstEntry) {
      fprintf(stderr, "CDL2 debugger v1.0, h for help\n\n");
      firstEntry = FALSE;
   }  
   c2traceREPL(C2SP, TRACE_ENTER);
}
// Called before the corresponding c2_pop_callstack_frame so call information is still on the stack.
int c2TraceExit(int v) {
   if (v == FALSE) {
      c2PrintStackFrame(C2SP, TRUE, FAIL_MARKER,TRUE);
   } else {
      c2PrintStackFrame(C2SP, TRUE, SUCCEED_MARKER,TRUE);
   }
   c2traceREPL(C2SP, v==FALSE ? TRACE_FAIL : TRACE_EXIT);
   return v;
}
// Called for the abort operator
void c2TraceExitAbort() {
      fprintf(stderr, "Abort called\n");
      c2Backtrace();
      exit(1);
}

void c2traceREPL(int depth,TraceExitType type) {
   c2PrintStackFrame(depth,TRUE,C2Marker(type),FALSE);
   char command = c2_getch();
   switch (command) {
      case 'h':
         fprintf(stderr, "Commands:\n");
         fprintf(stderr, "  c - continue execution until the next trace point\n");
         fprintf(stderr, "  s - step to the next trace point\n");
         fprintf(stderr, "  b - show a backtrace of the call stack\n");
         fprintf(stderr, "  q - quit the program\n");
         c2traceREPL(depth,type); // After showing help, ask for command again
         break;
      case '\n':
         break;
      case 'c':
         break;
      case 's':
         // Step: just return and let the next trace point be hit
         break;
      case 'b':
         c2Backtrace();
         c2traceREPL(depth,type); // After showing backtrace, ask for command again
         break;
      case 'q':
         exit(0);
         break;
      default:
         fprintf(stderr, "Invalid command. enter 'h' for help.\n");
         c2traceREPL(depth,type); // After invalid command, ask for command again
         break;
   }
}
