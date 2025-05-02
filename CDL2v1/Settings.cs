using System.CommandLine;
using System.ComponentModel;

namespace CDL2v1 {
   public interface ISetting {
      string Name { get; }
      int Index { get; set; }
      Option Option { get; set; }
   }

   public class Setting<T> : ISetting {
      public string Name { get; }
      public int Index { get; set; }
      public T? Value { get; set; } = default;
      public Option Option { get; set; }

      public Setting(string name, string optionName, T defaultValue, string description, ArgumentArity? arity = null)
         : this(name, [optionName], defaultValue, description, arity) { }

      public Setting(string name, string[] optionName, T defaultValue, string description, ArgumentArity? arity = null) {
         Name = name;
         Option = new Option<T>(optionName, () => defaultValue, description);
         if (arity != null) Option.Arity = (ArgumentArity)arity;
      }
      public Setting(string name, string optionName, string description, ArgumentArity? arity = null) {
         Name = name;
         Option = new Option<T>(optionName, description);
      }
      public string LongOption => Option.Aliases.OrderByDescending(s => s.Length).First();
      public override string ToString() => $"{Name}: {LongOption}";
   }

   public class Settings {
      private readonly List<ISetting> SettingsList = [
         new Setting<string[]>("Sources",            "--sources","The source files to compile"),
         new Setting<int>(     "VerbosityLevel",     ["-v", "--verbose"],   -1,            "Set the verbosity level (0-3)"),
         new Setting<int>(     "DebugVerbosityLevel",["-d", "--debug-log"], -1,            "Set the debug verbosity level (0-3)"),
         new Setting<string>(  "Target",             ["-t","--target"],     "PowerShell",  "Generate code for the specified target language. Default is PowerShell."),
         new Setting<string>(  "ProgramName",        ["-p","--program"],    "",            "Make program the one for which code is generated. The default is the first or only program that has been read."),
         new Setting<bool>(    "SaveDB",              "--save",             false,         "Save the parsed code to a file using JSON"),
         new Setting<bool>(    "ParseOnly",           "--parse-only",       false,         "Do not generate code. Verifies whether the source is syntactically and semantically valid."),
         new Setting<bool>(    "StopOnWarnings",      "--stop-on-warnings", false,         "Stop processing if any warnings are generated."),
         new Setting<bool>(    "AllowErrors",         "--allow-errors",     false,         "Continue even if there are errors. Mainly for debugging the compiler."),
         new Setting<string?>( "PrettyPrint",         "--pretty-print",     "",            "Pretty print the parsed code. If a value is given, it is assumed to be a file-name, Otherwise output goes to the Debugger.",ArgumentArity.ZeroOrOne),
         new Setting<bool>(    "GenerateDebugInfo",   "--gen-debug-info",   false,         "Generate debug information"),
         new Setting<string?>( "OutputDirectory",     "--output-dir",       null,          "Specify output directory for generated code"),
         new Setting<bool>(    "NoMacroInlining",     "--no-macro-inlining",false,         "Do not inline macros, generae them as procedures"),
         new Setting<NoteType>("Messages",            "--messages",         NoteType.Error,"Which messages should be shown: Error, Warning, Info. Default is errors only"),
      ];
      private readonly Dictionary<string, ISetting> SettingsDict = [];

      public static readonly Settings Instance = new();

      private Settings() {
         for (int i = 0; i < SettingsList.Count; i++) {
            SettingsList[i].Index = i;
            SettingsDict[SettingsList[i].Name] = SettingsList[i];
         }
      }
      public static T? SettingValue<T>(string name) => Setting<T>(name)!.Value;
      public static Setting<T>? Setting<T>(string name) {
         if (Instance.SettingsDict.TryGetValue(name, out ISetting? setting) && setting is Setting<T> typedSetting) {
            return typedSetting;
         }
         throw new KeyNotFoundException($"Setting with name '{name}' not found or of incorrect type.");
      }
      public static bool TryGetSettingValue(string name,out string value) {
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
         RootCommand rootCommand = new() { Description = "CDL2 Compiler" };
         for (int i=0; i < Instance.SettingsList.Count; i++) rootCommand.AddOption(Instance.SettingsList[i].Option);
         rootCommand.SetHandler((context) => {
            foreach (ISetting setting in Instance.SettingsList) {
               switch (setting) {
                  case Setting<string[]> saSetting: saSetting.Value = context.ParseResult.GetValueForOption<string[]>((Option<string[]>)setting.Option)!; break;                     
                  case Setting<int> iSetting:       iSetting.Value  = context.ParseResult.GetValueForOption<int>((Option<int>)setting.Option); break;
                  case Setting<string> sSetting:    sSetting.Value  = context.ParseResult.GetValueForOption<string>((Option<string>)setting.Option)!; break;
                  case Setting<bool> bSetting:      bSetting.Value  = context.ParseResult.GetValueForOption<bool>((Option<bool>)setting.Option); break;
                  case Setting<NoteType> nSetting:  nSetting.Value  = context.ParseResult.GetValueForOption<NoteType>((Option<NoteType>)setting.Option); break;
                  default: throw new InvalidEnumArgumentException($"Unknown setting type {setting.GetType()}");
               }
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

   }
}
