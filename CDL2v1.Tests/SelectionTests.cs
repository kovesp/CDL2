using Xunit;

namespace CDL2v1.Tests {
   public class SelectionTests : IClassFixture<DatabaseFixture> {
      private readonly DatabaseFixture _fixture;
      private readonly CommandInterpreter CLI = Database.Instance.CLI;


      public SelectionTests(DatabaseFixture fixture) {
         _fixture = fixture.SetName(typeof(SelectionTests));
      }

      [Fact]
      public void Selection_EmptyString_IsValidAndEmpty() {
         Selection selection = new Selection("");
         Assert.True(selection.IsValid);
         Assert.Empty(selection);
      }

      [Fact]
      public void Selection_InvalidType_IsInvalid() {
         Selection selection = new Selection("INVALIDTYPE");
         Assert.False(selection.IsValid);
         Assert.NotEmpty(selection.ErrorMessage);
      }

      [Fact]
      public void Selection_Correct_TopLevel_Container_Counts() {
         Selection selection = new Selection("Prog");
         Assert.True(selection.IsValid);
         Assert.Equal(selection.Count,Database.Instance.Programs.Count);

         selection = new Selection("Mod");
         Assert.True(selection.IsValid);
         Assert.Equal(selection.Count,Database.Instance.Modules.Count);
      }
      [Fact]
      public void Selection_Correct_Named_Module_Selection() {
         Selection selection = new ("Mod powershell");
         Assert.True(selection.IsValid);
         Assert.True(selection.Count == 1);
         Assert.IsType<Module>(selection.First().Object);
         Assert.Equal("powershellkernel", selection.First().Object!.Id.CanonicalName);
      }
      [Fact]
      public void Selection_Correct_Named_Program_Selection() {
         Selection selection = new ("Prog so");
         Assert.True(selection.IsValid);
         Assert.True(selection.Count == 1);
         Assert.IsType<Program>(selection.First().Object);
         Assert.Equal("sort", selection.First().Object!.Id.CanonicalName);
      }

      [Fact]
      public void Selection_Omitted_Names() {
         Selection selection = new ("Mod Sec");
         Assert.True(selection.IsValid);
         Assert.True(selection.Count > 0);
         foreach (SingleSelection item in selection) {
            Assert.IsType<Section>(item.Object);
         }
      }

      /// <summary>
      /// Check wether the focused element is of type T and has the expected canonical name (no spaces!).
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="name"></param>
      /// <returns></returns>
      private static bool IsFocusedElement<T>(string name) where T : NamedElement => Focus.Current.Object is T element && element.Id.CanonicalName == name;

      /// <summary>
      /// Sets the focus to a known section, then moves it to verify movement commands.
      /// </summary>
      [Fact]
      public void Selection_Focus_Movement_Container() {
         static bool IsFocusedSection(string name) => IsFocusedElement<Section>(name);

         Assert.True(Focus.SetFocus("Mod quicksort Sec",out _));
         Assert.True(IsFocusedSection("parametrization"));

         CLI.InterpretCommand("next 2");
         Assert.True(IsFocusedSection("touch"));

         CLI.InterpretCommand("previous");
         Assert.True(IsFocusedSection("arithmetic"));

         CLI.InterpretCommand("first");
         Assert.True(IsFocusedSection("parametrization"));

         CLI.InterpretCommand("last");
         Assert.True(IsFocusedSection("messages"));
      }

      /// <summary>
      /// Sets the focus to a known Algorithm, then moves it to verify movement commands.
      /// </summary>
      [Fact]
      public void Selection_Focus_Movement_CDL2Object() {
         static bool IsFocusedSection(string name) => IsFocusedElement<Section>(name);
         static bool IsFocusedCDL2Object(string name) => IsFocusedElement<CDL2Object>(name);

         Assert.True(Focus.SetFocus("Mod powershell kernel Lay kernel Sec arithmetic",out _));
         Assert.True(IsFocusedSection("arithmetic"));

         Assert.True(Focus.SetFocus("Alg",out _));
         Assert.True(IsFocusedCDL2Object("incr"));

         CLI.InterpretCommand("next 2");
         Assert.True(IsFocusedCDL2Object("incrwith"));

         CLI.InterpretCommand("previous");
         Assert.True(IsFocusedCDL2Object("decr"));

         CLI.InterpretCommand("first");
         Assert.True(IsFocusedCDL2Object("incr"));

         CLI.InterpretCommand("last");
         Assert.True(IsFocusedCDL2Object("random"));
      }
   }
}