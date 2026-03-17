// <auto-gen>
//=======================================================================
// <copyright file="GenerateXMIModel.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>GitHub Copilot</author>
// <creation-date>2025-03-13</creation-date>
// 
// <summary>
//   Simple utility to generate XMI model from CDL2v1 classes.
//   Run this as a console application or call from your existing code.
// </summary>
//=======================================================================
// </auto-gen>

using System.Diagnostics;
using System.IO;

namespace CDL2v1 {
   /// <summary>
   /// Utility class to generate XMI model files for Sparx Enterprise Architect.
   /// </summary>
   public static class GenerateXMIModel {
      /// <summary>
      /// Generates XMI model file at the specified path.
      /// </summary>
      /// <param name="outputPath">Path where the XMI file will be saved. If null, saves to CDL2Model.xmi in the current directory.</param>
      public static void Generate(string? outputPath = null) {
         outputPath ??= Path.Combine(Environment.CurrentDirectory,"CDL2Model.xmi");
         
         try {
            Debug.WriteLine($"Generating XMI model to: {outputPath}");
            SparxModelGenerator.GenerateCDL2Model(outputPath);
            Debug.WriteLine($"XMI model successfully generated at: {outputPath}");
            Debug.WriteLine("\nTo import into Sparx EA:");
            Debug.WriteLine("1. Open Sparx Enterprise Architect");
            Debug.WriteLine("2. Right-click on a package");
            Debug.WriteLine("3. Select 'Import Model' > 'Import XMI'");
            Debug.WriteLine($"4. Browse to: {outputPath}");
         } catch (Exception ex) {
            Debug.WriteLine($"Error generating XMI model: {ex.Message}");
            Debug.WriteLine(ex.StackTrace);
         }
      }

      // Note: Main method removed to avoid multiple entry points.
      // Call Generate() method directly from your code or create a separate console project.
   }
}
