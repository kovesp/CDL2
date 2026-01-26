// <auto-gen>
//=======================================================================
// <copyright file="Builtin.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-04-03</creation-date>
// 
// <summary>
//   Implements the built-in functions that can be used in conditional compilation.
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

namespace CDL2v1 {
   internal static class Builtin {
      // The Builtin facility is an extension to CDL2.
      //
      // FUNCTION date+date>.
      // FUNCTION time+time>.
      // FUNCTION version+version>.
      // FUNCTION option*name+value>.
      // FUNCTION environment variable*name+value>.

      // TEST     is option*name.                      // Can be used for conditional compilation.
      // TEST     is option value*name*value.          // Can be used for conditional compilation..
      // TEST     is environment variable*name.       // Is it defined?
      // TEST     is environment variable value*name*value. // Is defined and has the value
      // TEST     is target*target

      private static readonly Set<string> BuiltinFunctions = [
         "date",
         "time",
         "version",
         "option",
         "environmentvariable",
      ];
      private static readonly Set<string> BuiltinTests = [
         "isoption",
         "isoptionvalue",
         "isenvironmentvariable",
         "istarget",
      ];

      public static bool IsFunction(Call call) => BuiltinFunctions.Contains(call.id.CanonicalName);
      public static bool IsTest(Call call) => BuiltinTests.Contains(call.id.CanonicalName);

      /// <summary>
      /// Evaluates a built-in function call and returns the result based on the specified function name and arguments.
      /// </summary>
      /// <remarks>Supported function names include "date", "time", "version", "option", and
      /// "environmentvariable". The behavior and return value vary depending on the function. For unrecognized function
      /// names, an exception is thrown.</remarks>
      /// <param name="call">The function call to evaluate, including the function identifier and any arguments. Cannot be null.</param>
      /// <returns>The result of the evaluated function. The return type and value depend on the function specified in the call.
      /// For example, returns the current date as a string for the "date" function, but will be an int for a setting that is an int.
      /// It is up to the code generator to decide how to generate the result.
      /// </returns>
      /// <exception cref="NotImplementedException">Thrown if the specified function name is not recognized or not implemented.</exception>
      public static object EvalFunction(Call call) {
         switch (call.id.CanonicalName) {
            case "date":
               return DateTime.Now.ToString("yyyy-MM-dd");
            case "time":
               return DateTime.Now.ToString("HH:mm:ss");
            case "version":
               return CDL2.Version;
            case "option":
               if (call.TryGetActual<STRING>(out STRING? option)) {
                  return Settings.TryGetSettingValue(option.value,out object? value) ? value : "";
               } else {
                  return "";
               }
            case "environmentvariable":
               if (call.TryGetActual(out STRING? envName)) {
                  return Environment.GetEnvironmentVariable(envName.value) ?? "";
               } else {
                  return "";
               }
            default:
               throw new NotImplementedException($"Builtin function {call.id.CanonicalName} not implemented.");
         }
      }
      /// <summary>
      /// 
      /// </summary>
      /// <param name="call"></param>
      /// <returns></returns>
      /// <exception cref="NotImplementedException"></exception>
      public static bool EvalTest(Call call) {
         switch (call.id.CanonicalName) {
            case "isoption":
               if (call.TryGetActual(out STRING? option1)) {
                  return Settings.TryGetSettingValue(option1.value,out _);
               } else {
                  return false;
               }
            case "isoptionvalue":
               if (call.TryGetActual(out STRING? option2) && call.TryGetActual(out STRING? value,1)) {
                  return Settings.TryGetSettingValue(option2.value,out object? settingValue) && (string)settingValue == value.value;
               } else {
                  return false;
               }
            case "isenvironmentvariable":
               if (call.TryGetActual(out STRING? envName1)) {
                  return Environment.GetEnvironmentVariable(envName1.value) != null;
               } else {
                  return false;
               }
            case "isenvironmentvariablevalue":
               if (call.TryGetActual(out STRING? envName2) && call.TryGetActual(out STRING? envValue2,1)) {
                  return Environment.GetEnvironmentVariable(envName2.value) == envValue2.value;
               } else {
                  return false;
               }
            case "istarget":
               if (call.TryGetActual(out STRING? target)) {
                  return target.value == Settings.SettingValue<string>("Target");
               } else {
                  return false;
               }
            default:
               throw new NotImplementedException($"Builtin test {call.id.CanonicalName} not implemented.");
         }
      }
   }
}

