using CDL2v1;

namespace CDL2v1.Tests {
   public class DatabaseFixture : IDisposable {
      // One-time setup for all tests using this fixture
      public DatabaseFixture() => Database.InitializeForTests();

      public void Dispose() {
         // Cleanup if needed
      }
   }
}
