using Xunit;

namespace CDL2v1.Tests {
   public class CodeEntryTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture> {
      private readonly DatabaseFixture _fixture = fixture.SetName(typeof(CodeEntryTests));

      [Fact]
      public void Test() {
         Assert.True(true);
      }
   }
}
