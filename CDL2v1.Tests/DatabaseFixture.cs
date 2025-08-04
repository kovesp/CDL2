using CDL2v1;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CDL2v1.Tests {
   public class DatabaseFixture : IDisposable {
      // One-time setup for all tests using this fixture
      public DatabaseFixture() => Database.InitializeForTests();

      private bool _notInitialized = true;
      /// <summary>
      /// Set the name of the database instance as well as the current thread name to the name of the test class.
      /// </summary>
      /// <param name="testClass"></param>
      /// <returns></returns>
      public DatabaseFixture SetName(Type testClass) {
         if (_notInitialized) {
            _notInitialized = false;
            Thread.CurrentThread.Name = testClass.Name;
            Database.Instance.Name = testClass.Name;
            Debug.WriteLine($"Database instance added for thread {testClass.Name}({Environment.CurrentManagedThreadId})");
         }
         return this;
      }
      public void Dispose() {
         // Cleanup if needed
      }
   }
}
