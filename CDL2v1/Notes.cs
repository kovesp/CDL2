// Ignore Spelling: Transput CDL abstr ext inv ludes lude lwb upb FQN



namespace CDL2v1 {
   /// <summary>
   /// Notes are used to annotate objects with error/warning/info messages.
   /// A subclass of Set is used to avoid duplicate notes. For example, there may be multiple calls
   /// to the same undefined algorithm within a procedure. In that case, the note is only added once.
   /// </summary>
   public class Notes : Set<Note> {
      internal void ForEach(Action<Note> action) {
         foreach (Note note in this) action(note);
      }
   }

   /// <summary>
   /// Notes can be attached to NamedElements. The primary use is annotate objects with error/warning/info messages.
   /// The PrettyPrinter will these with the object as a comment starting with the <see cref="Marker"/>.
   /// If LexAnal (<see cref="Token.TryCreateToken(ref string, out Token)"/>) observes such comments, it will not preserve them.
   /// </summary>
   /// <param name="type"></param>
   /// <param name="text"></param>
   /// <param name="number"></param>
   public class Note : IEquatable<Note?> {
      public readonly NoteType Type;
      public readonly string Text;
      public readonly int Number;
      public readonly string PhaseName;
      public NamedElement? Owner = null;
      public Note(NoteType type, int number, string text, string phaseName = "") {
         Type = type;
         Text = text;
         Number = number;
         PhaseName = phaseName;
      }
      public Note(Note template, string phaseName, NamedElement owner, params object[] args)
         : this(template.Type, template.Number, 
              string.Format(template.Text, [.. args.Select(arg => arg is Affix aff ? $"<{aff.Id}>" : arg is Local loc ? $"<{loc.Id}>" : $"<{arg}>")]), 
              phaseName) => Owner = owner;

      public static readonly string Marker = " >>> ";

      public static readonly Note Defect                            = new(NoteType.Error  , 001, "Procedure has defect (has an effect tough tough declared as {0})");
      public static readonly Note Effect                            = new(NoteType.Error  , 002, "Procedure has effect tough declared as {0}");
      public static readonly Note CanFail                           = new(NoteType.Error  , 003, "Procedure can fail tough declared as {0}");
      public static readonly Note CannotFail                        = new(NoteType.Error  , 004, "Procedure cannot fail tough declared as {0}");
      public static readonly Note LabelNotFound                     = new(NoteType.Error  , 005, "*{0}: label not found in Procedure");
      public static readonly Note DuplicateLabel                    = new(NoteType.Error  , 006, "{0}: duplicate label in Procedure");
      public static readonly Note IllegalFailOperator               = new(NoteType.Error  , 007, "Procedure has FAIL operator (-) tough declared as {0}");
      public static readonly Note UndeclaredAlgorithmCall           = new(NoteType.Error  , 008, "Call of undeclared Algorithm {0}");
      public static readonly Note ArgumentCountMismatch             = new(NoteType.Error  , 009, "Argument count mismatch. {0} has {1} affixes, but called with {2}");
      public static readonly Note WrongTypeOfargumentForStringAffix = new(NoteType.Error  , 010, "Only a literal string or a CONST can be passed to a string affix for call {0}");
      public static readonly Note ConstPassedToOutput               = new(NoteType.Error  , 013, "CONST {0} passed to output or transput in call {0}");
      public static readonly Note ConstPassedToTransput             = new(NoteType.Error  , 014, "CONST {0} passed to transput in call {1}");
      public static readonly Note DuplicateDeclaration              = new(NoteType.Error,   015, "{0} has been already declared as {1} in this section");
      public static readonly Note InvalidConstElement               = new(NoteType.Error,   016, "References {0}. Only other constants may be referenced.");
      public static readonly Note UnresolvedConstElement            = new(NoteType.Error,   017, "References undefined id {0}.");
      public static readonly Note MissingImportSpec                 = new(NoteType.Error  , 018, "{0} is imported in section {1} but has no specificaion");
      public static readonly Note ObjectNotImported                 = new(NoteType.Error  , 019, "{0} has no body but is not imported");
      public static readonly Note ObjectImportedButHasBody          = new(NoteType.Error  , 020, "{0} is imported but has a body");
      public static readonly Note InterfaceElementNotProvidable     = new(NoteType.Error,   021, "Only CONSTs and Algorithms may be in {1} declarations. {0} is of type {2}");
      public static readonly Note InvalidInputArg                   = new(NoteType.Error  , 022, "Only CONST, VAR, AFFIX (input or transput) or LOCAL may be passed to an input affix {0}");
      public static readonly Note InvalidOutputArg                  = new(NoteType.Error  , 023, "Only VAR, AFFIX (output or transput) or LOCAL may be passed to an output affix {0}");
      public static readonly Note InvalidStringArg                  = new(NoteType.Error  , 024, "Only CONST, literal string, or string affix can be passed to {1}, not {0}");
      public static readonly Note InvalidTransputArg                = new(NoteType.Error  , 025, "Only VAR, AFFIX (output or transput) or LOCAL may be passed to a transput affix {0}");
      public static readonly Note InvalidArgumentType               = new(NoteType.Error  , 026, "Argument must reference an affix, const or var, not {0}");
      public static readonly Note UnresolvedArgument                = new(NoteType.Error  , 027, "Argument could not be resolved {0}");
      public static readonly Note DuplicateInterfaceElement         = new(NoteType.Error  , 029, "{0} {1} by {2} was already {1} by {3}");
      public static readonly Note MissingImport                     = new(NoteType.Error  , 030, "{0} is imported but was not exported by any program part");
      public static readonly Note ImpexMismatch                     = new(NoteType.Error  , 031, "Import {0} does not match export {1} ({2})");
      public static readonly Note InterfaceElementMissing           = new(NoteType.Error  , 032, "{0} is in {1} list, but not declared in section");
      public static readonly Note MissingInvoke                     = new(NoteType.Error,   033, "{0} is invoked in {1}, but is not extended or abstacted from anywhere");
      public static readonly Note ModuleNotFound                    = new(NoteType.Error,   034, "Part {0} not found among modules");
      public static readonly Note LudeNotFound                      = new(NoteType.Error,   035, "{2} references {0} {1}, but this does not have a {2}");
      public static readonly Note InvalidListBound                  = new(NoteType.Error,   036, "Invalid list {0} {1}. Must be CONST, but is {2}");
      public static readonly Note UnresolvedListBound               = new(NoteType.Error,   037, "Undefined list {0} {1}.");


      public static readonly Note NoEffect                          = new(NoteType.Warning, 101, "Procedure has no effect tough is declared as {0}");
      public static readonly Note OutputAffixOverwritten            = new(NoteType.Warning, 102, "Output affix {0} whose action has not been read passed to output in {1}");
      public static readonly Note TransputAffixOverwritten          = new(NoteType.Warning, 103, "Transput affix (0} whose action has not been read passed to output in {1}");
      public static readonly Note VariableNotRead                   = new(NoteType.Warning, 104, "Variable {0} was assigned a action which was never read");
      public static readonly Note VariableNotWritten                = new(NoteType.Warning, 105, "Variable {0} was read, but never assigned a action");
      public static readonly Note VariableMayNotHaveBeenRead        = new(NoteType.Warning, 106, "Variable {0} was assigned a action, but may not have been read");
      public static readonly Note AbstractionsInTopLayer            = new(NoteType.Warning, 107, "There are abstractions in the top layer of the module");

      public static readonly Note UninitializedOutputPassedAsInput  = new(NoteType.Warning, 110, "Output affix {0} that has not been set passed as input or transput in call {1}");
      public static readonly Note InputAffixPassedToOutput          = new(NoteType.Warning, 111, "Input affix {0} passed to output or transput in call {0}");
      public static readonly Note OutputAffixNotAssigned            = new(NoteType.Warning, 112, "Output affix {0} that has not been set passed as input in call {1}");
      public static readonly Note LocalNotAssigned                  = new(NoteType.Warning, 113, "Local {0} that has not been set passed as input in call {1}");
      public static readonly Note LocalOverwritten                  = new(NoteType.Warning, 114, "Local {0} whose value has not been read passed to output in call {1}");

      public static readonly Note AffixNotRefeenced                 = new(NoteType.Info   , 201, "Affix {0} was not used in procedure {1}");
      public static readonly Note LocalNotReferenced                = new(NoteType.Info   , 202, "Local {0} was not used in procedure {1}");
      public static readonly Note UnreferenceObject                 = new(NoteType.Info   , 203, "Object is defined but not used in program");

      public override string ToString() => $"{Type} {Number}: {Text}";
      public override bool Equals(object? obj) => Equals(obj as Note);
      public bool Equals(Note? other) => other is not null && Type == other.Type && Text == other.Text && Number == other.Number;
      public override int GetHashCode() => HashCode.Combine(Type, Text, Number);

      public static bool operator ==(Note? left, Note? right) => EqualityComparer<Note>.Default.Equals(left, right);
      public static bool operator !=(Note? left, Note? right) => !(left == right);
   }

}
