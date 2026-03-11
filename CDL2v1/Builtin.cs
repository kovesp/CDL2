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


using System.Diagnostics.CodeAnalysis;

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

      /// <summary>
      /// Map of built-in functions and their argument counts.
      /// </summary>
      private static readonly Dictionary<string,int> Functions = new() {
         { "date", 1 },
         { "time", 1 },
         { "version", 1 },
         { "option", 2 },
         { "environmentvariable", 2 },
      };
      /// <summary>
      /// Provides a set of built-in test names recognized by the system.
      /// </summary>
      private static readonly HashSet<string> Tests = [
         "isoption",
         "isoptionvalue",
         "isenvironmentvariable",
         "istarget",
         "isdebug",
         "isos",
      ];

      /// <summary>
      /// Is this a built-in function call? For this to be true,
      /// <list type="bullet">
      ///   <item>The call must be to a known built-in function with the correct number of arguments</item>
      ///   <item>The last argument must be a Local (i.e., a variable to store the result)</item>
      /// </list>
      /// </summary>
      /// <param name="call"></param>
      /// <returns></returns>
      public static bool IsFunction(Call call,[MaybeNullWhen(false)] out Local? loc) {
         loc = Functions.TryGetValue(call.id.CanonicalName,out int argCount) && call.Args.Count == argCount && call.Args.Last() is Local l ? l: null;
         return loc is not null;
      }
      public static bool IsFunction(Call call) => IsFunction(call,out _);
      public static bool IsTest(Call call) => Tests.Contains(call.id.CanonicalName);

      /// <summary>
      /// Evaluates a built-in function call and returns the result based on the specified function name and arguments.
      /// </summary>
      /// <remarks>Supported function names include "date", "time", "version", "option", and
      /// "environmentvariable". The behavior and return value vary depending on the function. For unrecognized function
      /// names, an exception is thrown.</remarks>
      /// <param name="call">The function call to evaluate, including the function identifier and any arguments. Cannot be null.</param>
      /// <returns>The result of the evaluated function returned as a string.</returns>
      /// <exception cref="NotImplementedException">Thrown if the specified function name is not recognized or not implemented.</exception>
      public static string EvalFunction(Call call) {
         switch (call.id.CanonicalName) {
            case "date":
               return DateTime.Now.ToString("yyyy-MM-dd");
            case "time":
               return DateTime.Now.ToString("HH:mm:ss");
            case "version":
               return CDL2.Version;
            case "option":
               if (call.TryGetActual<STRING>(out STRING? option)) {
                  return Settings.TryGetSettingValue(option.value,out object? value) ? value?.ToString() ?? "" : "";
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
      public static string EvalFunction(Guid callGuid) => NamedElement.From<Call>(callGuid) is Call call ? EvalFunction(call) : "";

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
                  return Settings.TryGetSettingValue(option1.value,out object? v) && v is bool b && b;
               } else {
                  return false;
               }
            case "isdebug":               
               return Settings.SettingValue<bool>("Debug"); 
            case "isoptionvalue":
               if (call.TryGetActual(out STRING? option2) && call.TryGetActual(out STRING? value,1)) {
                  return Settings.TryGetSettingValue(option2.value,out object? settingValue) && (string)(settingValue??"") == value.value;
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
            case "is os":
               if (call.TryGetActual(out STRING? os)) {
                  return os.value.ToLower() switch {
                     "windows" => OperatingSystem.IsWindows(),
                     "linux" => OperatingSystem.IsLinux(),
                     "macos" => OperatingSystem.IsMacOS(),
                     _ => false
                  };
               }
               return false;
            default:
               throw new NotImplementedException($"Builtin test {call.id.CanonicalName} not implemented.");
         }
      }
   }
}

