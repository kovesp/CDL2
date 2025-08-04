using Xunit;

namespace CDL2v1.Tests {
   public class SelectionTests : IClassFixture<DatabaseFixture> {
      private readonly DatabaseFixture _fixture;

      public SelectionTests(DatabaseFixture fixture) {
         _fixture = fixture.SetName(typeof(SelectionTests));
      }

      [Fact]
      public void Selection_EmptyString_IsValidAndEmpty() {
         var selection = new Selection("");
         Assert.True(selection.IsValid);
         Assert.Empty(selection);
      }

      [Fact]
      public void Selection_InvalidType_IsInvalid() {
         var selection = new Selection("INVALIDTYPE");
         Assert.False(selection.IsValid);
         Assert.NotEmpty(selection.ErrorMessage);
      }

      [Fact]
      public void Selection_Correct_TopLevel_Container_Counts() {
         var selection = new Selection("Prog");
         Assert.True(selection.IsValid);
         Assert.True(selection.Count == Database.Instance.Programs.Count);
         selection = new Selection("Mod");
         Assert.True(selection.IsValid);
         Assert.True(selection.Count == Database.Instance.Modules.Count);
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
   }
}