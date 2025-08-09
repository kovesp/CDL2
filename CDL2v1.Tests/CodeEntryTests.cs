using Xunit;

namespace CDL2v1.Tests {
   public class CodeEntryTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture> {
      private readonly DatabaseFixture _fixture = fixture.SetName(typeof(CodeEntryTests));
      private readonly CommandInterpreter CLI = Database.Instance.CLI;

      [Fact]
      public void DeclareProgram() {
         // Add a program.
         Assert.True(CLI.EnterRawCode("Prog test1."));
         Program? test1 = Database.Instance.ProgramByName("test1");
         Assert.NotNull(test1);
         // Add some parts.
         Assert.True(CLI.EnterRawCode("Part aa,bb."));
         // Check that the part was added.
         Assert.True(test1.Parts.Where(part => part.CanonicalName == "bb").Any()); 
      }
   }
}
