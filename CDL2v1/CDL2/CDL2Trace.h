#ifndef VALUE
#define VALUE long
#define BOOL VALUE
#define TRUE 1
#define FALSE 0
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <signal.h>

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

#undef ADD_LIST
#undef ADD_VAR
#define ADD_LIST(name,value,lwb,upb) c2AddList(name,value,lwb,upb)
#define ADD_VAR(name,value) c2AddVar(name,value)

#define VAL(aff) {.val=aff}
#define PTR(aff) {.ptr=aff}
#define STR(aff) {.str=aff}

/////////////////////////////////////////////////////////////////////////////////
// Types and data structures
/////////////////////////////////////////////////////////////////////////////////
typedef enum { C2_ALG_PREDICATE, C2_ALG_TEST, C2_ALG_ACTION, C2_ALG_FUNCTION } C2AlgType;
typedef enum { C2_DATA_VAR, C2_DATA_LIST } C2DataType;
typedef enum { C2_AFF_INPUT, C2_AFF_OUTPUT, C2_AFF_TRANSPUT, C2_AFF_STRING } C2AffType;
typedef union { VALUE val; VALUE* ptr; char* str; } C2DataValue;
typedef enum { TRACE_ENTER, TRACE_EXIT, TRACE_FAIL, TRACE_ABORT } TraceExitType;

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

typedef struct C2ProcInfo {
   char* name;
   char* code;
   BOOL spypoint;
   BOOL steppoint;
   struct C2ProcInfo* next;
} C2ProcInfo;

typedef struct C2VarInfo {
   char* name;
   C2DataType type;
   VALUE* value;
   struct C2VarInfo* next;
} C2VarInfo;
typedef struct C2ListInfo {
   char* name;
   C2DataType type;
   VALUE* value;
   int lwb;
   int upb;
   struct C2ListInfo* next;
} C2ListInfo;

/////////////////////////////////////////////////////////////////////////////////
// Globals
/////////////////////////////////////////////////////////////////////////////////
C2ProcInfo* C2Procs = NULL;
C2VarInfo* C2Vars = NULL;
C2ListInfo* C2Lists = NULL;
#define CALL_STACK_MAX_DEPTH 10000
C2StackFrame C2Stack[CALL_STACK_MAX_DEPTH];
int C2SP = -1;

BOOL C2HonorSpyPoints = TRUE;
BOOL C2Jumping = FALSE; // Used to skip any entry that is not a steppoint
BOOL C2TraceWhileJumping = FALSE; // Used to show trace output for intermediate calls while jumping or ...


/////////////////////////////////////////////////////////////////////////////////
// Forward declarations
/////////////////////////////////////////////////////////////////////////////////
void c2PrintStackFrame(int depth, BOOL indent, char* marker, BOOL newline, TraceExitType type);
void c2TraceEnter();
int  c2TraceExit(int v);
void c2TraceExitAbort();
void c2traceREPL(int depth,TraceExitType type);
void c2PrintAff(int depth,int i, TraceExitType type);
void c2Backtrace();
void c2AddProc(char* name, char* code);
void c2AddList(char* name, VALUE* value, int lwb, int upb);
void c2AddVar(char* name, VALUE* value);
C2ProcInfo* c2FindProc(char* name);
C2VarInfo* c2FindVar(char* name);
C2ListInfo* c2FindList(char* list);
BOOL c2SetSpyPoint(char* procName, BOOL set);

BOOL c2StartsWith(char* str, char* prefix);
BOOL c2MatchName(char* name1, char* name2);
char* c2RemoveBlanks(char* str);
void c2SignalHhandler(int sig);
void c2IinitTrace();
void c2Exit(int code);

/////////////////////////////////////////////////////////////////////////////////
// Signal handler and exit wrapper - defined early so ALL code uses them
/////////////////////////////////////////////////////////////////////////////////
void c2SignalHhandler(int sig) {
   fprintf(stderr, "\n*** CDL2 Runtime Error: Caught signal %d ***\n", sig);
   c2Backtrace();
   _Exit(sig);  // Use _Exit directly to avoid recursion
}

void c2InitTrace() {
   // Set up signal handlers to catch runtime errors
   signal(SIGSEGV, c2_signal_handler);  // Segmentation fault
   signal(SIGABRT, c2_signal_handler);  // Abort
   signal(SIGFPE,  c2_signal_handler);  // Floating point exception
   signal(SIGILL,  c2_signal_handler);  // Illegal instruction
#ifndef _WIN32
   signal(SIGBUS,  c2_signal_handler);  // Bus error (POSIX)
#endif
}

static void c2Exit(int code) {
   static BOOL exiting = FALSE;
   if (!exiting && code != 0 && C2SP >= 0) {
      exiting = TRUE;
      fprintf(stderr, "\n*** CDL2 Program exiting with code %d ***\n", code);
      c2Backtrace();
   }
   _Exit(code);
}

// Redefine exit() so all code below shows backtraces on errors
#define exit(code) c2Exit(code)

/////////////////////////////////////////////////////////////////////////////////
// Implementation
/////////////////////////////////////////////////////////////////////////////////
void c2AddProc(char* name, char* code) {
   C2ProcInfo* newNode = malloc(sizeof(C2ProcInfo));
   if (newNode == NULL) {
      fprintf(stderr, "Out of memory\n");
      exit(1);
   }

   newNode->name = name;
   newNode->code = code;
   newNode->spypoint = FALSE;
   newNode->steppoint = FALSE;
   newNode->next = NULL;

   if (C2Procs == NULL) {
      C2Procs = newNode;
   } else {
      C2ProcInfo* p = C2Procs;
      while (p->next != NULL) p = p->next;
      p->next = newNode;
   }
}

void c2AddList(char* name, VALUE* value, int lwb, int upb) {
   C2ListInfo* newNode = malloc(sizeof(C2ListInfo));
   if (newNode == NULL) {
      fprintf(stderr, "Out of memory\n");
      exit(1);
   }

   newNode->name = name;
   newNode->type = C2_DATA_LIST;
   newNode->value = value;
   newNode->lwb = lwb;
   newNode->upb = upb;
   newNode->next = NULL;

   if (C2Lists == NULL) {
      C2Lists = newNode;
   } else {
      C2ListInfo* p = C2Lists;
      while (p->next != NULL) p = p->next;
      p->next = newNode;
   }
}

void c2AddVar(char* name, VALUE* value) {
   C2VarInfo* newNode = malloc(sizeof(C2VarInfo));
   if (newNode == NULL) {
      fprintf(stderr, "Out of memory\n");
      exit(1);
   }

   newNode->name = name;
   newNode->type = C2_DATA_VAR;
   newNode->value = value;
   newNode->next = NULL;

   if (C2Vars == NULL) {
      C2Vars = newNode;
   } else {
      C2VarInfo* p = C2Vars;
      while (p->next != NULL) p = p->next;
      p->next = newNode;
   }
}

C2ProcInfo* c2FindProc(char* name) {
   if (C2Procs == NULL) {
      fprintf(stderr, "C2Procs==NULL ... this is not possible");
      exit(1);
   }
   for (C2ProcInfo* p = C2Procs; p != NULL; p = p->next) {
      if (c2MatchName(name, p->name)) return p;
   }
   return NULL;
}

C2VarInfo* c2FindVar(char* name) {
   if (C2Vars == NULL) {
      fprintf(stderr, "C2Vars==NULL ... this is not possible");
      exit(1);
   }
   for (C2VarInfo* p = C2Vars; p != NULL; p = p->next) {
      if (c2MatchName(name, p->name)) return p;
   }
   return NULL;
}

C2ListInfo* c2FindList(char* name) {
   if (C2Lists == NULL) {
      fprintf(stderr, "C2Lists==NULL ... this is not possible");
      exit(1);
   }
   for (C2ListInfo* p = C2Lists; p != NULL; p = p->next) {
      if (c2MatchName(name, p->name)) return p;
   }
   return NULL;
}

BOOL c2StartsWith(char* str, char* prefix) {
   while (*prefix != '\0') {
      if (*str != *prefix) {
         return FALSE;
      }
      str++;
      prefix++;
   }
   return TRUE;
}

BOOL c2MatchName(char* name1, char* name2) {
   char* clean1 = c2RemoveBlanks(name1);
   char* clean2 = c2RemoveBlanks(name2);
   BOOL result = c2StartsWith(clean1, clean2);
   free(clean1);
   free(clean2);
   return result;
}

char* c2RemoveBlanks(char* str) {
   // First pass: count non-blank characters
   int count = 0;
   for (char* src = str; *src != '\0'; src++) {
      if (*src != ' ') count++;
   }

   // Allocate exactly what we need
   char* result = (char*)malloc(count + 1);
   if (result == NULL) {
      return NULL; // Memory allocation failed
   }

   // Second pass: copy non-blank characters
   char* dst = result;
   for (char* src = str; *src != '\0'; src++) {
      if (*src != ' ') *dst++ = *src;
   }
   *dst = '\0';
   return result;
}


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

void c2Backtrace() {
   fprintf(stderr, "Call stack (most recent call last):\n");
   for (int i = C2SP - 1; i >= 0; i--) {
      c2PrintStackFrame(i,FALSE,NO_MARKER,TRUE,TRACE_EXIT);
   }
}   
   
void c2PrintStackFrame(int depth,BOOL indent, char* marker,BOOL newline,TraceExitType type) {
   static char indentation_buffer[256];
   char * indentation = "";
   if (indent) {
      sprintf(indentation_buffer, "%*s", depth, "");
      indentation = indentation_buffer;
   }
   if (!newline) fprintf(stderr,"\n"); // Print a newline before the stack frame when used as debugger prompt
   fprintf(stderr, "%s%s%s %s",indentation,marker,c2AlgType(depth),C2Stack[depth].name);
   if (type != TRACE_FAIL) for (int i = 0; i < C2Stack[depth].nargs; i++) c2PrintAff(depth, i,type);
   fprintf(stderr,newline ? "\n" : ": ");
}

void c2PrintAff(int depth,int i, TraceExitType type) {
   char* name = C2Stack[depth].argnames[i];
      switch (C2Stack[depth].affTypes[i]) {
      case C2_AFF_INPUT:    fprintf(stderr, "+>%s=%ld", name,C2Stack[depth].args[i].val); break;
      case C2_AFF_OUTPUT:   
         if (type == TRACE_ENTER) {
            fprintf(stderr, "+%s>",name);
         } else {
            fprintf(stderr, "+%s>=%ld",name,*C2Stack[depth].args[i].ptr);
         }
         break;
      case C2_AFF_TRANSPUT: fprintf(stderr, "+>%s>=%ld",name,*C2Stack[depth].args[i].ptr); break;
      case C2_AFF_STRING:   fprintf(stderr, "*%s=%s",   name,C2Stack[depth].args[i].str); break;
      default: fprintf(stderr, "+??");
   }
}

// Called after c2_push_callstack_frame so call information is on the stack.
BOOL firstEntry = TRUE;
void c2TraceEnter() {
   if (firstEntry) {
      c2_init_trace();  // Initialize signal handlers on first entry
      fprintf(stderr, "CDL2 debugger v1.0, h for help\n");
      firstEntry = FALSE;
   }
   if (C2Skipping) {
      if (C2Stack[C2SP].steppoint) {
         C2Skipping = FALSE;
         C2TraceWhileJumping = FALSE;
         C2Stack[C2SP].steppoint = FALSE;
      } else {
         if (C2TraceWhileJumping)
            c2PrintStackFrame(C2SP, TRUE, C2Marker(TRACE_ENTER), FALSE, TRACE_ENTER);
         return; // Skip this trace point
      }
   } else if (!C2HonorSpyPoints || !C2Stack[C2SP].spypoint) {
      return; 
   }
   c2traceREPL(C2SP, TRACE_ENTER);
}
// Called without popping the stack, so it must do it.
int c2TraceExit(int v) {
   c2traceREPL(C2SP, v==FALSE ? TRACE_FAIL : TRACE_EXIT);
   C2SP--; // Pop the call stack frame after the trace REPL so that the current frame is still visible in the REPL
   return v;
}
// Called for the abort operator
void c2TraceExitAbort() {
      fprintf(stderr, "Abort called\n");
      c2Backtrace();
      exit(1);
}

void c2traceREPL(int depth,TraceExitType type) {
   c2PrintStackFrame(depth,TRUE,C2Marker(type),FALSE,type);
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
      case '\r':
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
