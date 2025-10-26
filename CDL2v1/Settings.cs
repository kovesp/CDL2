// <auto-gen>
//=======================================================================
// <copyright file="Settings.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-03-30</creation-date>
// 
// <summary>
//   Maintains setting for the system  both command line options and others.
//   The settings are stored in a static class so that they can be accessed from anywhere in the system.
//   The settings are also saved to a file in the output directory so that they can be restored on next run.
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

using System.CommandLine;
using System.ComponentModel;
using System.IO;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace CDL2v1 {
   public interface ISetting {
      string Name { get; }
      int Index { get; set; }
      Type Type { get; set; }
      Option Option { get; set; }
      string LongOption { get; }
      bool CommandOverride { get; set; }
      string ToTabularString(bool title=false);
   }

   public class Setting<T> : ISetting {
      public string Name { get; }
      public int Index { get; set; }
      public Type Type { get; set; }
      public T? Value { get; set; } = default;
      public Option Option { get; set; }
      public bool IsSaved { get; set; } = false; // Whether this setting should be saved to a file
      public bool CommandOverride { get; set; } = false; // Whether this setting was specified from a lab command

      public Setting(string name, string optionName, T defaultValue, string description, ArgumentArity? arity = null, bool saved = false,string disjoint = "")
         : this(name, [optionName], defaultValue, description, arity, saved:saved,disjoint:disjoint) { }

      public Setting(string name, string[] optionName, T defaultValue, string description, ArgumentArity? arity = null, bool saved = false,string disjoint = "") {
         Name = name;
         Type = typeof(T);
         Option = new Option<T>(optionName, () => defaultValue, description);
         if (arity != null) Option.Arity = (ArgumentArity)arity;
         IsSaved = saved;
         if (disjoint != "") {
            Settings.DisjointSettings[disjoint] = Name;
            Settings.DisjointSettings[Name] = disjoint;
         }
      }
      public Setting(string name, string optionName, string description, ArgumentArity? arity = null, bool saved = false,string disjoint = "") {
         Name = name;
         Type = typeof(string[]);
         Option = new Option<T>(optionName, description);
         if (arity != null) Option.Arity = (ArgumentArity)arity;
         IsSaved = saved;
         if (disjoint != "") {
            Settings.DisjointSettings[disjoint] = Name;
            Settings.DisjointSettings[Name] = disjoint;
         }
      }
      public string LongOption => Option.Aliases.OrderByDescending(s => s.Length).First();
      public override string ToString() =>
         $"{Name}: {LongOption} => {(Value is null ? "" : Value is string[] sa ? string.Join(",",sa) : Value.ToString())}";
      private const char HorizontalBar = '\u2501'; // BOX DRAWINGS HEAVY HORIZONTAL
      public string ToTabularString(bool title = false) {
         if (title) {
            string titleString = $"{"Name".PadRight(Settings.Instance.MaxNameLength)} {"Type",-8} {"Command Line Option".PadRight(Settings.Instance.MaxOptionLength)} Value";
            return $"{titleString}\n{new string(HorizontalBar, titleString.Length)}";
         } else {
            string longOption = (LongOption.StartsWith("--NA") ? "" : LongOption).PadRight(Settings.Instance.MaxOptionLength);
            string type = Type.Name switch { "Int32" => "int", /*"Severity" => "string",*/ _ => Type.Name.ToLower() };
            return $"{Name.PadRight(Settings.Instance.MaxNameLength)} {type,-8} {longOption} {(Value is null ? "" : Value is string[] sa ? string.Join(",",sa) : Value.ToString())}";
         }
      }
   }

   public class Settings {

      public static readonly Dictionary<string,string> DisjointSettings = [];
      private readonly List<ISetting> SettingsList = [
         new Setting<string[]>("Sources",            "--sources",                          "The source files to compile. Ignored if the --lab option is given."),
         new Setting<int>(     "VerbosityLevel",     ["-v", "--verbose"],   -1,            "Set the verbosity level (0-3)."),
         new Setting<int>(     "DebugVerbosityLevel",["-d", "--debug-log"], -1,            "Set the debug verbosity level (0-3)."),
         new Setting<string>(  "Target",             ["-t","--target"],     "PowerShell",  "Generate code for the specified target language. Default is PowerShell."),
         new Setting<string>(  "ProgramName",        ["-p","--program"],    "",            "Make program the one for which code is generated. The default is the first\n"+"" +
                                                                                           "or only program that has been read."),
         new Setting<bool>(    "Lab",                 "--lab",              false,         "Run in CDL2 Lab mode. The database is opened from the file specified in the\n"+
                                                                                           "--db option in --output-dir and the lab prompt is shown.",disjoint:"ParseOnly"),
         new Setting<bool>(    "ParseOnly",           "--parse-only",       false,         "Do not generate code. Verifies whether the source is syntactically and\n"+
                                                                                           "semantically valid. Also, do not enter Lab mode."),
         new Setting<bool>(    "StopOnWarnings",      "--stop-on-warnings", false,         "Stop processing if any warnings are generated."),
         new Setting<bool>(    "AllowErrors",         "--allow-errors",     false,         "Continue even if there are errors. Mainly for debugging the Compiler."),
         new Setting<string?>( "PrettyPrint",         "--pretty-print",     "",            "Pretty print the parsed code. If a value is given, it is assumed to be\n"+
                                                                                           "a file-name, Otherwise output goes to the Debugger.",ArgumentArity.ZeroOrOne),
         new Setting<bool>(    "GenerateDebugInfo",   "--gen-debug-info",   false,         "Generate debug information."),
         new Setting<string?>( "OutputDirectory",     "--output-dir",       null,          "Specify output directory for generated code."),
         new Setting<bool>(    "NoMacroInlining",     "--no-macro-inlining",false,         "Do not inline macros, generate them as procedures."),
         new Setting<bool>(    "NoProcInlining",      "--no-proc-inlining", false,         "Do not inline procedures."),
         new Setting<bool>(    "NoSave",              "--no-save",          false,         "Do not save the database when exiting."),
         new Setting<int>(     "MaxInlineCalls",      "--max-inline-calls", 9,             "Maximum number of calls that can be inlined. This is a product of the\n"+
                                                                                           "number of calls in the procedure and the number of times the procedure\n"+
                                                                                           "is called. However, if the procedure contains a single call, it is always\n"+
                                                                                           "inlineable."),
         new Setting<bool>(    "ReportAll",           "--report-all",       false,         "Report all messages (subject to --messages). Normally messages for\n"+
                                                                                           "non-reachable objects are suppressed."),
         new Setting<Severity>("Messages",            "--messages",         Severity.Error,"Which messages should be shown: Error, Warning, Info. Default is errors only."),
         new Setting<string>(  "DB",                  "--db",               "CDL2v1",      "The filename in --output-dir that contains the serialized lab data.\n"+
                                                                                           "The extension is .lab.gz. At exit the current parse tree is always saved."),
         new Setting<int>(     "Backups",             "--backups",          3,             "The number of backups of the lab file to keep. Extensions are .lab.gz.1.\n"+
                                                                                           "...NOT IMPLEMENTED."),
         new Setting<double>(  "WindowLeft",          "--window-left",     -1.0,           "Last window left position.",saved:true),
         new Setting<double>(  "WindowTop",           "--window-top",      -1.0,           "Last window top position.",saved:true),
         new Setting<double>(  "WindowWidth",         "--window-width",     800.0,         "Last window width.",saved:true),
         new Setting<double>(  "WindowHeight",        "--window-height",    1200.0,        "Last window height.",saved:true),
         new Setting<bool>(    "PPSorted",            "--pretty-print-sorted",false,       "When printing sections, print its objects collected by type.",saved:true),
         new Setting<int>(     "PrintDepth",          "--print-depth",      -1,            "Depth of printing. -1 means full. Applicable to containers.",saved:true),
         new Setting<bool>(    "AutoPrint",           "--auto-print",      false,          "The focused object is printed after a coomand when set.",saved:true),
         new Setting<int>(     "AutoSaveCount",       "--autosave-count",  10,             "The database is saved after this many commands that modify it.",saved:true),
         new Setting<int>(     "AutoSaveInterval",    "--autosave-interval",300,           "The database is saved after this many seconds if modified.",saved:true),


         // Settings that cannot be used from the lab command line. A dummy option is generated for each
         new Setting<bool>(    "list",                NoOption,             false,         "Modify a command to list available objects. Used by Undo and redo."),
         new Setting<bool>(    "inv",                 NoOption,             false,         "Modify the command to affect only the INV list entry of the object if any. Applies to delete and add."),
         new Setting<bool>(    "ext",                 NoOption,             false,         "Modify the command to affect only the EXT list entry of the object if any. Applies to delete and add."),
         new Setting<bool>(    "abstr",               NoOption,             false,         "Modify the command to affect only the ABSTR list entry of the object if any. Applies to delete and add."),
         new Setting<bool>(    "import",              NoOption,             false,         "Modify the command to affect only the IMPORT list entry of the object if any. Applies to delete and add."),
         new Setting<bool>(    "export",              NoOption,             false,         "Modify the command to affect only the EXPORT list entry of the object if any. Applies to delete and add."),

         new Setting<string>(  "setTag",              NoOption,             "",            "Sets a tag on a given item. Used with the undo/redo commands."),
         new Setting<string>(  "tag",                 NoOption,             "",            "Selects the (first) tagged item. Used with the undo/redo commands to undo/redo the tagged item."),
         new Setting<bool>(    "DebugCommands",       NoOption,             false,         "Display the parsed command."),
      ];

      public static List<ISetting> AllSettings => Instance.SettingsList;

      private static int NoOptionCounter = 1;
      private static string NoOption => $"--NA{NoOptionCounter++}";

      private readonly Dictionary<string, ISetting> SettingsDict = [];

      public static readonly Settings Instance = new();
      public int MaxNameLength = 0;
      public int MaxOptionLength = 0;
      private Settings() {
         for (int i = 0 ; i < SettingsList.Count ; i++) {
            SettingsList[i].Index = i;
            SettingsDict[SettingsList[i].Name] = SettingsDict[SettingsList[i].Name.ToLower()] = SettingsList[i];
            MaxNameLength = Math.Max(MaxNameLength, SettingsList[i].Name.Length);
            MaxOptionLength = Math.Max(MaxOptionLength, SettingsList[i].LongOption.Length);
         }
      }

      public static bool Verbosity(int level) => SettingValue<int>("VerbosityLevel") >= level;
      public static bool DebugVerbosity(int level) => SettingValue<int>("DebugVerbosityLevel") >= level;
      public static bool AnyVerbosity(int level) => Verbosity(level) || DebugVerbosity(level);

      public static string OutputDirectory => SettingValue<string>("OutputDirectory") ?? Directory.GetCurrentDirectory();
      public static string LabDBName => Path.ChangeExtension(SettingValue<string>("DB") ?? "CDL2v1", Serializer.DBExtension);
      public static string LabDBPath => Path.Combine(OutputDirectory, LabDBName);

      public static bool LabMode => SettingValue<bool>("Lab") && ! SettingValue<bool>("ParseOnly");

      public static T? SettingValue<T>(string name) => Setting<T>(name)!.Value;
      public static object? SettingValue(string name) => Setting<object>(name);

      public static void SettingValue<T>(string name,T value) => Setting<T>(name)!.Value = value;

      public static void SettingValue(string name,SettingType type,object? value,bool CommandOverride=false) {
         switch (type) {
            case SettingType.Boolean: SettingValue<bool>(name,value is null ? !SettingValue<bool>(name) : (bool)value); break;
            case SettingType.Integer: SettingValue<int>(name,(int)value!); break;
            case SettingType.String:  SettingValue<string>(name,(string)value!); break;
            default: throw new InvalidEnumArgumentException($"Unknown setting type {type}");
         }
         SetCommandOverride(name,CommandOverride);
      }

      public static Setting<T>? Setting<T>(string name) {
         if (Instance.SettingsDict.TryGetValue(name, out ISetting? setting) && setting is Setting<T> typedSetting) {
            return typedSetting;
         }
         throw new KeyNotFoundException($"Setting with name '{name}' not found or of incorrect type.");
      }

      public static void SetCommandOverride(string name,bool commandOverride) {
         if (Instance.SettingsDict.TryGetValue(name,out ISetting? setting)) {
            setting.CommandOverride = commandOverride;
         } else {
            throw new KeyNotFoundException($"Setting with name '{name}' not found.");
         }
      }
      public static bool IsValidSetting(string name) => Instance.SettingsDict.ContainsKey(name);
      public static bool TryGetSettingValue(string name, out string value) {
         if (Instance.SettingsDict.TryGetValue(name, out ISetting? setting)) {
            if (setting is Setting<string> sSetting) {
               value = sSetting.Value!;
               return true;
            }
            if (setting is Setting<bool> bSetting) {
               value = bSetting.Value.ToString();
               return true;
            }
            if (setting is Setting<int> iSetting) {
               value = iSetting.Value.ToString();
               return true;
            }
         }
         value = "";
         return false;
      }

      public static void ProcessCommandLine(string[] commandLine) {
         // Create a HashSet of explicitly provided options from the raw command line
         HashSet<string> explicitlyProvidedOptions = [];
         Dictionary<string, HashSet<string>> optionAliasMap = [];
         
         // Build a map of option names to all their aliases
         foreach (ISetting setting in Instance.SettingsList) {
             string longName = setting.Name;
             var allAliases = new HashSet<string>(setting.Option.Aliases);
             optionAliasMap[longName] = allAliases;
         }
         
         // Parse raw command line to identify which options are explicitly provided
         for (int i = 0; i < commandLine.Length; i++) {
            string arg = commandLine[i];
            if (arg.StartsWith('-')) {
               string option = arg;
               string? shortOptionWithValue = null;

               // Handle --option=value and --option:value format
               if (arg.Contains('=')) {
                  option = arg.Split('=',2)[0];
               } else if (arg.Contains(':')) {
                  option = arg.Split(':',2)[0];
               } else if (arg.Length > 2 && arg[0] == '-' && arg[1] != '-') {
                  // Handle combined short options like -v3
                  // This could be a short option with attached value
                  string shortOption = arg[..2]; // e.g., "-v"
                  
                  // Check if this is a known short option
                  bool isKnownShortOption = Instance.SettingsList.Any(s => 
                     s.Option.Aliases.Any(a => a == shortOption));
                     
                  if (isKnownShortOption) {
                     option = shortOption;
                     shortOptionWithValue = arg;
                  }
               }
               
               // Add the identified option
               explicitlyProvidedOptions.Add(option);
               
               // Skip the value if this option takes one and it's not combined or in --option=value format
               if (shortOptionWithValue == null && !arg.Contains('=') && i + 1 < commandLine.Length && !commandLine[i + 1].StartsWith('-')) {
                  i++; // Skip the next arg which is the value
               }
            }
         }
         
         // Debug output
         if (AnyVerbosity(2)) {
            System.Diagnostics.Debug.WriteLine("Command line: " + string.Join(" ", commandLine));
            System.Diagnostics.Debug.WriteLine("Explicitly provided options:");
            foreach (var opt in explicitlyProvidedOptions) {
               System.Diagnostics.Debug.WriteLine($"  {opt}");
            }
         }
         
         // Now process using regular System.CommandLine but only override settings for explicitly provided options
         RootCommand rootCommand = new() { Description = "CDL2 Compiler" };
         
         // Add options as before
         for (int i = 0; i < Instance.SettingsList.Count; i++) 
            rootCommand.AddOption(Instance.SettingsList[i].Option);
         
         rootCommand.SetHandler((context) => {
            System.CommandLine.Parsing.ParseResult parseResult = context.ParseResult;
            
            foreach (ISetting setting in Instance.SettingsList) {
               // Check if any alias of this option was explicitly provided
               bool wasExplicitlyProvided = setting.Option.Aliases.Any(alias => 
                     explicitlyProvidedOptions.Contains(alias));
               
               if (wasExplicitlyProvided) {
                  switch (setting) {
                     case Setting<string[]> saSetting: saSetting.Value = parseResult.GetValueForOption<string[]>((Option<string[]>)setting.Option)!; break;                     
                     case Setting<int> iSetting:       iSetting.Value  = parseResult.GetValueForOption<int>((Option<int>)setting.Option); break;
                     case Setting<double> dSetting:    dSetting.Value  = parseResult.GetValueForOption<double>((Option<double>)setting.Option); break;
                     case Setting<string> sSetting:    sSetting.Value  = parseResult.GetValueForOption<string>((Option<string>)setting.Option)!; break;
                     case Setting<bool> bSetting:      bSetting.Value  = parseResult.GetValueForOption<bool>((Option<bool>)setting.Option); break;
                     case Setting<Severity> nSetting:  nSetting.Value  = parseResult.GetValueForOption<Severity>((Option<Severity>)setting.Option); break;
                     default: throw new InvalidEnumArgumentException($"Unknown setting type {setting.GetType()}");
                  }

                  // If this setting has a disjoint setting, set it to false
                  if (Settings.DisjointSettings.TryGetValue(setting.Name,out string? disjointSettingName)) {
                     if (Settings.Instance.SettingsDict.TryGetValue(disjointSettingName, out ISetting? disjointSetting) && disjointSetting is Setting<bool> boolSetting) {
                        boolSetting.Value = false; // Set the other setting to false
                     }
                  }

                  // Debug output
                  if (AnyVerbosity(1)) {
                     System.Diagnostics.Debug.WriteLine($"Setting overridden: {setting.Name} = {setting}");
                  }
               }
            }


            if (explicitlyProvidedOptions.Contains("Lab")) {
               Setting<bool>("ParseOnly")!.Value = false;
            } else if (explicitlyProvidedOptions.Contains("ParseOnly")) {
               Setting<bool>("Lab")!.Value = false;
            }
         });
         
         rootCommand.Invoke(commandLine);
      }

      public static string BoolOption(string option) {
         Setting<bool> setting = Settings.Setting<bool>(option)!;
         return setting.Value ? setting.LongOption + " " : "";
      }
      public static string IntOption(string option) {
         Setting<int> setting = Settings.Setting<int>(option)!;
         return setting.Value > 0 ? $"{setting.LongOption} {setting.Value} " : "";
      }
      public static string StringOption(string option) {
         Setting<string> setting = Settings.Setting<string>(option)!;
         return setting.Value == null || setting.Value.IsWhiteSpace() ? "" : $"{setting.LongOption} {setting.Value} ";
      }

      private const string SettingsFileName = "cdl2settings.json";

      // Save settings to a file
      public static void SaveSettings() {
         try {
            var settingsToSave = new Dictionary<string, object>();
            foreach (ISetting setting in Instance.SettingsDict.Values) {
               switch (setting) {
                  case Setting<double> doubleSetting:
                     settingsToSave[setting.Name] = doubleSetting.Value!;
                     break;
                  case Setting<int> intSetting:
                     settingsToSave[setting.Name] = intSetting.Value;
                     break;
                  case Setting<string> stringSetting:
                     if (stringSetting.Value != null)
                        settingsToSave[setting.Name] = stringSetting.Value;
                     break;
                  case Setting<bool> boolSetting:
                     settingsToSave[setting.Name] = boolSetting.Value;
                     break;
                     // Add other types as needed
               }
            }

            string json = System.Text.Json.JsonSerializer.Serialize(settingsToSave);
            string settingsPath = Path.Combine(OutputDirectory, SettingsFileName);
            File.WriteAllText(settingsPath, json);
         } catch (Exception ex) {
            // Log error but don't crash
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
         }
      }

      // Load settings from a file
      public static void LoadSettings() {
         try {
            string settingsPath = Path.Combine(OutputDirectory, SettingsFileName);
            if (File.Exists(settingsPath)) {
               string json = File.ReadAllText(settingsPath);
               Dictionary<string, object>? loadedSettings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

               if (loadedSettings != null) {
                  foreach (KeyValuePair<string, object> kvp in loadedSettings) {
                     if (Instance.SettingsDict.TryGetValue(kvp.Key, out ISetting? setting)) {
                        // Use JsonElement conversion because the Dictionary deserializes as JsonElement objects
                        if (kvp.Value is System.Text.Json.JsonElement element) {
                           switch (setting) {
                              case Setting<double> doubleSetting:
                                 if (element.TryGetDouble(out double doubleValue))
                                    doubleSetting.Value = doubleValue;
                                 break;
                              case Setting<int> intSetting:
                                 if (element.TryGetInt32(out int intValue))
                                    intSetting.Value = intValue;
                                 break;
                              case Setting<string> stringSetting:
                                 if (element.ValueKind == System.Text.Json.JsonValueKind.String)
                                    stringSetting.Value = element.GetString();
                                 break;
                              case Setting<bool> boolSetting:
                                 if (element.ValueKind == System.Text.Json.JsonValueKind.True)
                                    boolSetting.Value = true;
                                 else if (element.ValueKind == System.Text.Json.JsonValueKind.False)
                                    boolSetting.Value = false;
                                 break;
                                 // Add other types as needed
                           }
                        }
                     }
                  }
               }
            }
         } catch (Exception ex) {
            // Log error but don't crash
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
         }
      }
   }
}

