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
      // FUNCTION date+date>.
      // FUNCTION time+time>.
      // FUNCTION version+version>.
      // FUNCTION option*name+value>.
      // FUNCTION environment variable*name+value>.

      // TEST     is option*name.                      // Can be used for conditional compilation.
      // TEST     is option value*name*value.          // Can be used for conditional compilation..
      // TEST     is environment variable*name.       // Is it defined?
      // TEST     is target*target

      private static readonly Set<string> BuiltinFunctions = ["date","time","version","option","environmentvariable"];
      private static readonly Set<string> BuiltinTests = ["isoption","isoptionvalue","isenvironmentvariable","istarget"];

      public static bool IsFunction(Call call) => BuiltinFunctions.Contains(call.id.CanonicalName);
      public static bool IsTest(Call call) => BuiltinTests.Contains(call.id.CanonicalName);

      public static string EvalFunction(Call call) {
         switch (call.id.CanonicalName) {
            case "datestring":
               return DateTime.Now.ToString("yyyy-MM-dd");
            case "timestring":
               return DateTime.Now.ToString("HH:mm:ss");
            case "versionstring":
               return CDL2.Version;
            case "option":
               if (call.Args.FirstOrDefault() is STRING option) {
                  return Settings.TryGetSettingValue(option.value,out string? value) ? value : "";
               } else {
                  return "";
               }
            case "environmentvariable":
               if (call.Args.FirstOrDefault() is STRING envName) {
                  return Environment.GetEnvironmentVariable(envName.value) ?? "";
               } else {
                  return "";
               }
            default:
               throw new NotImplementedException($"Builtin function {call.id.CanonicalName} not implemented.");
         }
      }
      public static bool EvalTest(Call call) {
         switch (call.id.CanonicalName) {
            case "isoption":
               if (call.Args.FirstOrDefault() is STRING option) {
                  return Settings.TryGetSettingValue(option.value,out _);
               } else {
                  return false;
               }
            case "isoptionvalue":
               if (call.Args.FirstOrDefault() is STRING option1 && call.Args.Skip(1).FirstOrDefault() is STRING value) {
                  return Settings.TryGetSettingValue(option1.value,out string? settingValue) && settingValue == value.value;
               } else {
                  return false;
               }
            case "isenvironmentvariable":
               if (call.Args.FirstOrDefault() is STRING envName) {
                  return Environment.GetEnvironmentVariable(envName.value) != null;
               } else {
                  return false;
               }
            case "istarget":
               if (call.Args.FirstOrDefault() is STRING target) {
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

