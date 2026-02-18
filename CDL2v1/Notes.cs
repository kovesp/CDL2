// <auto-gen>
//=======================================================================
// <copyright file="Notes.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-04-24</creation-date>
// 
// <summary>
//   Notes are Annotations that can be attached to NamedElements.
//   They are used to annotate objects with error/warning/info messages.
//   The name Note is used to avoid confusion with the Annotation class, which is used for other purposes.
// </summary>
// <attribution>
//   This file is part of the clean room reimplementation of the
//      CDL2 Compiler
//      CDL2 Laboratory
//      CDL2 Target Code Generators
//
//    Based on original work on CDL and CDL2 led by C. H. A. Koster
//    and the CDL2 team at the Universities of Berlin, Germany and
//    Nijmegen, The Netherlands.
//
//    The CDL2 Laboratory was the work of Epsilon GmbH, Berlin.
//    H. M. Stahl, H. Feuerhahn, JP. Dehotay, B. Böhringer
//    (and others I don't remember ... sorry).
//
//    This project is not affiliated with the original CDL2 project.
// </attribution>
//=======================================================================
// </auto-gen>

// Ignore Spelling: Transput CDL abstr ext inv ludes lude lwb upb FQN



using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

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
      public Set<Note> NotesWithSeverity(Severity severity) => this.Where(note => note.NoteType == severity).ToSet;

      private static Severity[] compilerNotes = [Severity.Error,Severity.Warning,Severity.Info];

      public void ClearCompilerNotes() {
         IEnumerable<Note> remove = [.. this.Where(note => compilerNotes.Contains(note.NoteType)) ];
         foreach (Note note in remove) this.Remove(note);
      }

      public static Notes Empty => [];
   }

   /// <summary>
   /// Notes can be attached to NamedElements. The primary use is annotate objects with error/warning/info messages.
   /// The PrettyPrinter will these with the object as a comment starting with the <see cref="Marker"/>.
   /// If LexAnal (<see cref="Token.TryCreateToken(ref string, out Token)"/>) observes such comments, it will not preserve them.
   /// </summary>
   /// <param name="type"></param>
   /// <param name="text"></param>
   /// <param name="number"></param>
   public class Note : SerializationBase, IEquatable<Note?> {
      [JsonInclude][JsonPropertyOrder(1)] public Severity NoteType;
      [JsonInclude][JsonPropertyOrder(2)] public string Text = "";
      [JsonInclude][JsonPropertyOrder(3)] public int Number;
      [JsonInclude][JsonPropertyOrder(4)] public string PhaseName = "";
      [JsonInclude][JsonPropertyOrder(5)] public Guid Owner = Guid.Empty;
      public Note(Severity type,int number,string text,string phaseName = "") {
         NoteType = type;
         Text = text;
         Number = number;
         PhaseName = phaseName;
      }
      [JsonConstructor]
      public Note() { }
      public Note(Note template,string phaseName,NamedElement? owner,params object[] args)
         : this(template.NoteType,template.Number,
              string.Format(template.Text,[.. args.Select(arg => arg is Affix aff ? $"<{aff.Id}>" : arg is Local loc ? $"<{loc.Id}>" : $"<{arg}>")]),
              phaseName) => Owner = owner?.GUID ?? Guid.Empty;

      public string FormattedText(params object[] args) => string.Format(Text,args);

      public static readonly string Marker = " >>> ";

      public static readonly Note Defect = new(Severity.Error,001,"Procedure has defect (has an effect tough tough declared as {0})");
      public static readonly Note Effect = new(Severity.Error,002,"Procedure has effect tough declared as {0}");
      public static readonly Note CanFail = new(Severity.Error,003,"Procedure can fail tough declared as {0}");
      public static readonly Note CannotFail = new(Severity.Error,004,"Procedure cannot fail tough declared as {0}");
      public static readonly Note LabelNotFound = new(Severity.Error,005,"*{0}: label not found in Procedure");
      public static readonly Note DuplicateLabel = new(Severity.Error,006,"{0}: duplicate label in group hierarchy");
      public static readonly Note IllegalFailOperator = new(Severity.Error,007,"Procedure has FAIL operator (-) tough declared as {0}");
      public static readonly Note UndeclaredAlgorithmCall = new(Severity.Error,008,"Call of undeclared Algorithm {0}");
      public static readonly Note ArgumentCountMismatch = new(Severity.Error,009,"Argument count mismatch. {0} has {1} affixes, but called with {2}");
      public static readonly Note WrongTypeOfargumentForStringAffix = new(Severity.Error,010,"Only a literal string or a CONST can be passed to a string affix for call {0}");
      public static readonly Note InvalidListBounds = new(Severity.Error,011,"LIST {0} has invalid bounds. Only CONSTs are allowed");
      public static readonly Note UnexpectedToken = new(Severity.Error,012,$"Expected token {{0}}. Found {{1}}. Skipping to next '{Token.TokenType2Glyph[TT.END]}'");
      public static readonly Note ConstPassedToOutput = new(Severity.Error,013,"CONST {0} passed to output or transput in call {0}");
      public static readonly Note ConstPassedToTransput = new(Severity.Error,014,"CONST {0} passed to transput in call {1}");
      public static readonly Note DuplicateDeclaration = new(Severity.Error,015,"{0} has been already declared as {1} in this section");
      public static readonly Note InvalidConstElement = new(Severity.Error,016,"References {0}. Only other constants may be referenced.");
      public static readonly Note UnresolvedConstElement = new(Severity.Error,017,"References undefined id {0}.");
      public static readonly Note MissingImportSpec = new(Severity.Error,018,"{0} is imported in section {1} but has no specification");
      public static readonly Note ObjectNotImported = new(Severity.Error,019,"{0} has no body but is not imported");
      public static readonly Note ObjectImportedButHasBody = new(Severity.Error,020,"{0} is imported but has a body");
      public static readonly Note InterfaceElementNotProvidable = new(Severity.Error,021,"Only CONSTs and Algorithms may be in {1} declarations. {0} is of type {2}");
      public static readonly Note InvalidInputArg = new(Severity.Error,022,"Only CONST, VAR, AFFIX (input or transput) or LOCAL may be passed to an input affix {0}");
      public static readonly Note InvalidOutputArg = new(Severity.Error,023,"Only VAR, AFFIX (output or transput) or LOCAL may be passed to an output affix {0}");
      public static readonly Note InvalidStringArg = new(Severity.Error,024,"Only CONST, literal string, string affix, or BUILTIN result can be passed to {1}, not {0}");
      public static readonly Note InvalidTransputArg = new(Severity.Error,025,"Only VAR, AFFIX (output or transput) or LOCAL may be passed to a transput affix {0}");
      public static readonly Note InvalidArgumentType = new(Severity.Error,026,"Argument must reference an affix, const or var, not {0}");
      public static readonly Note UnresolvedArgument = new(Severity.Error,027,"Argument could not be resolved {0}");
      public static readonly Note InvalidToken = new(Severity.Error,028,"Invalid token during lexical analysis. Skipping {0}");
      public static readonly Note DuplicateInterfaceElement = new(Severity.Error,029,"{0} {1} by {2} was already {1} by {3}");
      public static readonly Note MissingImport = new(Severity.Error,030,"{0} is imported but was not exported by any program part");
      public static readonly Note ImpexMismatch = new(Severity.Error,031,"Import {0} does not match export {1} ({2})");
      public static readonly Note InterfaceElementMissing = new(Severity.Error,032,"{0} is in {1} list, but not declared in section");
      public static readonly Note MissingInvoke = new(Severity.Error,033,"{0} is invoked in {1}, but is not extended or abstracted from anywhere");
      public static readonly Note ModuleNotFound = new(Severity.Error,034,"Part {0} not found among modules");
      public static readonly Note LudeNotFound = new(Severity.Error,035,"{2} references {0} {1}, but this does not have a {2}");
      public static readonly Note InvalidListBound = new(Severity.Error,036,"Invalid list {0} {1}. Must be CONST, but is {2}");
      public static readonly Note UnresolvedListBound = new(Severity.Error,037,"Undefined list {0} {1}.");
      public static readonly Note DuplicateContainer = new(Severity.Error,038,"{0} already exists.");
      public static readonly Note EmptyLude = new(Severity.Error,039,"{0} is empty.");
      public static readonly Note UnresolvedMacroElement = new(Severity.Error,040,"References undefined id {0}.");
      public static readonly Note ImportIsExported = new(Severity.Error,041,"An imported object cannot be exported: {0}.");
      public static readonly Note InvalidMacroElement = new(Severity.Error,042,"IDs in macros must refer to a Const, Var, List, Affix, or Local: {0}.");
      public static readonly Note ParseArgStringWithDirection = new(Severity.Error,043,"String formal parameters cannot have a direction: {0}.");
      public static readonly Note ParseArgStdArgHasNoDirection = new(Severity.Error,044,"Standard formal parameters must have a direction: {0}.");
      public static readonly Note ParseArgDuplicateArg = new(Severity.Error,045,"Duplicate formal parameter: {0}.");
      public static readonly Note ParseArgMissingId = new(Severity.Error,046,"Expected formal parameter id.");
      public static readonly Note PareseDuplicateLocal = new(Severity.Error,047,"Duplicate local: {0}.");
      public static readonly Note ParseExpectedProcBody = new(Severity.Error,048,"Expected procedure body.");
      public static readonly Note ParseExpectedMacroBody = new(Severity.Error,049,"Expected macro body.");
      public static readonly Note ExpectedId = new(Severity.Error,050,"Expected identifier.");
      public static readonly Note NoSectionForObject = new(Severity.Error,051,"Unable to determine section for object or element. Context is {0}.");
      public static readonly Note InvalidLudeContext = new(Severity.Error,052,"Layers don't have {0}s.");
      public static readonly Note ExpectedCall = new(Severity.Error,053,"Expected call.");
      public static readonly Note ExpectedPeriod = new(Severity.Error,054,"Expected statement end.");
      public static readonly Note DuplicateInterfaceElementInSection = new(Severity.Error,055,"Duplicate {0} {1} in section");
      public static readonly Note DuplicateLude = new(Severity.Error,056,"Duplicate {0} {1} in {3}");
      public static readonly Note NotImplemented = new(Severity.Error,057,"Not implemented: {0}");
      public static readonly Note CannotDelete = new(Severity.Error,058,"Cannot remove {0} from {1}");
      public static readonly Note InvalidLude = new(Severity.Error,059,"Invalid lude {0}");
      public static readonly Note TestContainsFail = new(Severity.Error,060,"{0} contains a fail operator");
      public static readonly Note UnexpectedSeparator = new(Severity.Error,061,"Unexpected comma");
      public static readonly Note ExpectedBuiltinId = new(Severity.Error,062,"Expected built-in ID");
      public static readonly Note ExpectedGroup = new(Severity.Error,063,"Expected group");
      public static readonly Note ExpectedLastCall = new(Severity.Error,064,"Expected last call: ID, +, -, ?, or *");
      public static readonly Note CannotChangeIdentity = new(Severity.Error,065,"The edit command cannot be used to change the type or identity of an object");
      public static readonly Note UnknownBuiltin = new(Severity.Error,066,"Unknown built-in function or wrong number of arguments: {0}");
      public static readonly Note BuiltinResultReused = new(Severity.Error,067,"Local {0} used in more than one BUILTIN call");
      public static readonly Note BuiltinResultAsOutput = new(Severity.Error,068,"BUILTIN result {1} used as {0} in {2}");
      public static readonly Note NoProgram = new(Severity.Error,069,"No main program to generate code for");
      public static readonly Note NoCodeGenerator = new(Severity.Error,070,"No code generator available for target {0}");
      public static readonly Note CodeGenCreationError = new(Severity.Error,071,"Error creating code generator for target {0} with Data type {1}: {2}");
      public static readonly Note CodeGenError = new(Severity.Error,072,"Error during code generation: {0} {1}");
      public static readonly Note InvalidSetting = new(Severity.Error,073,"Unknown setting: {0}");
      public static readonly Note InvalidSettingValue = new(Severity.Error,074,"Invalid setting value for {0} setting {1}={2}");
      public static readonly Note BoolSettingHasValue = new(Severity.Error,075,"Boolean settings cannot have a value: {0} = {1}");
      public static readonly Note MissingSettingValue = new(Severity.Error,076,"Missing or too many setting values: {0}");
      public static readonly Note InvalidSettingSuffix = new(Severity.Error,077,"The {0} setting {1} cannot have a suffix");
      public static readonly Note AmbiguousSettingName = new(Severity.Error,078,"'{0}' matches {{{1}}}");

      public static readonly Note NoEffect = new(Severity.Warning,101,"Procedure has no effect tough is declared as {0}");
      public static readonly Note OutputAffixOverwritten = new(Severity.Warning,102,"Output affix {0} whose value has not been read passed to output in {1}");
      public static readonly Note TransputAffixOverwritten = new(Severity.Warning,103,"Transput affix (0} whose value has not been read passed to output in {1}");
      public static readonly Note VariableNotRead = new(Severity.Warning,104,"Variable {0} was assigned a value which was never read");
      public static readonly Note VariableNotWritten = new(Severity.Warning,105,"Variable {0} was read, but never assigned a value");
      public static readonly Note VariableMayNotHaveBeenRead = new(Severity.Warning,106,"Variable {0} was assigned a value, but may not have been read");
      public static readonly Note AbstractionsInTopLayer = new(Severity.Warning,107,"There are abstractions in the top layer of the module");
      public static readonly Note UninitializedOutputPassedAsInput = new(Severity.Warning,110,"Output affix {0} that has not been set passed as input or transput in call {1}");
      public static readonly Note InputAffixPassedToOutput = new(Severity.Warning,111,"Input affix {0} passed to output or transput in call {0}");
      public static readonly Note OutputAffixNotAssigned = new(Severity.Warning,112,"Output affix {0} that has not been set passed as input in call {1}");
      public static readonly Note LocalNotAssigned = new(Severity.Warning,113,"Local {0} that has not been set passed as input in call {1}");
      public static readonly Note LocalOverwritten = new(Severity.Warning,114,"Local {0} whose value has not been read passed to output in call {1}");

      public static readonly Note AffixNotRefeenced = new(Severity.Info,201,"Affix {0} was not used in procedure {1}");
      public static readonly Note LocalNotReferenced = new(Severity.Info,202,"Local {0} was not used in procedure {1}");
      public static readonly Note UnreferenceObject = new(Severity.Info,203,"Object is defined but not used in program. This may be due to conditional compilation");
      public static readonly Note CodeGenDone = new(Severity.Info,204,"{0} code generated for {1} into {2}");

#if DEBUG
      static Note() {
         // Verify that there are no duplicate Note numbers
         HashSet<int> noteIds = [];
         foreach (FieldInfo field in typeof(Note).GetFields(BindingFlags.Public | BindingFlags.Static)) {
            if (field.FieldType == typeof(Note)) {
               Note note = (Note)field.GetValue(null)!;
               if (!noteIds.Add(note.Number)) {
                  throw new Exception($"Duplicate Note number in: {note}");
               }
            }
         }
      }
#endif 

      public override string ToString() => $"{NoteType} {Number}: {Text}";
      public override bool Equals(object? obj) => Equals(obj as Note);
      public bool Equals(Note? other) => other is not null && NoteType == other.NoteType && Text == other.Text && Number == other.Number;
      public override int GetHashCode() => HashCode.Combine(NoteType,Text,Number);

      public static bool operator ==(Note? left,Note? right) => EqualityComparer<Note>.Default.Equals(left,right);
      public static bool operator !=(Note? left,Note? right) => !(left == right);
   }

}

