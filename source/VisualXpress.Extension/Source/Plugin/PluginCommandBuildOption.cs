// Copyright Microsoft Corp. All Rights Reserved.
using Microsoft.VisualStudio.CommandBars;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Microsoft.VisualXpress
{
    [PluginCommand("buildoption", "Enables or disables specific build options")]
    [PluginCommandOption(PluginCommandBuildOption.OptionNameBuildTag, "The name of the build option tag", 1, PlugCommandOptionFlag.Required)]
    [PluginCommandOption(PluginCommandBuildOption.OptionNameDefaultValue, "The default value of the build option", 1, PlugCommandOptionFlag.Required)]
    [PluginCommandOption(PluginCommandBuildOption.OptionNameDisablesValue, "Any build options that this option disables", 1, PlugCommandOptionFlag.Optional)]
    [PluginCommandOption(PluginCommandBuildOption.OptionNameEnablesValue, "Any build options that this option enables", 1, PlugCommandOptionFlag.Optional)]
    public class PluginCommandBuildOption : PluginCommand
    {
        public const string OptionNameBuildTag = "-tag";
        public const string OptionNameDefaultValue = "-default";
        public const string OptionNameDisablesValue = "-disables";
        public const string OptionNameEnablesValue = "-enables";

        public const string CheckedItemResourceFileName = "check.png";

        private const String ConfigurationElementName = "Configuration";
        private const String BuildConfigurationElementName = "BuildConfiguration";
        private const String ConfigurationNamespace = "https://www.unrealengine.com/BuildConfiguration";

        private const Char _MetadataDelimiter = ':';

        private static XNamespace Namespace { get { return XNamespace.Get(ConfigurationNamespace); } }

        private static String ConfigurationPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Unreal Engine", "UnrealBuildTool", "BuildConfiguration.xml");
            }
        }

        private static XElement LoadOrCreateConfiguration()
        {
            XDocument Document;
            XElement Element;

            try
            {
                if (!File.Exists(ConfigurationPath))
                {
                    Element = new XElement(Namespace + BuildConfigurationElementName);

                    Document = new XDocument(
                        new XElement(Namespace + ConfigurationElementName,
                            Element));

                    Log.Verbose($"\"{ConfigurationPath}\" does not exist, creating new document.");
                }
                else
                {
                    Document = XDocument.Load(ConfigurationPath);
                    Element = Document.Root.Element(Namespace + BuildConfigurationElementName);

                    Log.Verbose($"Loaded \"{ConfigurationPath}\" successfully.");
                }
            }
            catch (Exception e)
            {
                Log.Error($"Failed to load configuration from, \"{ConfigurationPath}\".. {e.Message}");
                return null;
            }

            return Element;
        }

        private static bool GetValue(String tag, bool defaultValue = false)
        {
            var BuildConfigurationElement = LoadOrCreateConfiguration();

            if (BuildConfigurationElement == null)
            {
                return defaultValue;
            }

            var TagElement = BuildConfigurationElement.Element(Namespace + tag);

            if (TagElement == null || String.IsNullOrEmpty(TagElement.Value))
            {
                return defaultValue;
            }

            var Value = defaultValue;
            bool.TryParse(TagElement.Value, out Value);

            return Value;
        }

        private static void SetValue(String tag, bool value, bool defaultValue = false)
        {
            var BuildConfigurationElement = LoadOrCreateConfiguration();

            if (BuildConfigurationElement == null)
            {
                return;
            }

            var TagElement = BuildConfigurationElement.Element(Namespace + tag);

            if (TagElement == null)
            {
                TagElement = new XElement(Namespace + tag);
                BuildConfigurationElement.Add(TagElement);
            }

            if (value != defaultValue)
            {
                TagElement.SetValue(value);
            }
            else
            {
                TagElement.Remove();
            }

            Log.Info($"SetValue: {tag}={value} (Default: {defaultValue})");

            try
            {
                var ConfigurationDirectory = Path.GetDirectoryName(ConfigurationPath);

                if (!Directory.Exists(ConfigurationDirectory))
                {
                    Directory.CreateDirectory(ConfigurationDirectory);
                }

                BuildConfigurationElement.Document.Save(ConfigurationPath);
                Log.Info($"Saved \"{ConfigurationPath}\" successfully.");
            }
            catch (Exception e)
            {
                Log.Error($"Failed to save configuration to, \"{ConfigurationPath}\".. {e.Message}");
            }
        }

        private static bool ToggleValue(String tag, bool defaultValue = false)
        {
            var Value = !GetValue(tag, defaultValue);
            SetValue(tag, Value, defaultValue);

            return Value;
        }

        public override bool InitializeCommandBar(CommandBarControls controls, MenuItem item, ref int index, bool beginGroup)
        {
            var MenuItemPluginCommand = item as MenuItemPluginCommand;
            var Args = Utilities.CommandLineToArgs(MenuItemPluginCommand.Arguments);

            String Tag;
            bool DefaultValue;

            if (!GetTagDefault(PluginCommandOptions.Create(this, Args),
                out Tag, out DefaultValue))
            {
                return false;
            }

            var Value = GetValue(Tag, DefaultValue);

            item.Image = Value ? CheckedItemResourceFileName : String.Empty;
            item.Tag = SerializeMetadata(Tag, DefaultValue);

            return base.InitializeCommandBar(controls, item, ref index, beginGroup);
        }

        private static String SerializeMetadata(String tag, bool defaultValue)
        {
            return $"{tag}{_MetadataDelimiter}{defaultValue}";
        }

        private static String GetMetadataTag(String metadata)
        {
            bool DefaultValue;
            return DeserializeMetadata(metadata, out DefaultValue);
        }

        private static String DeserializeMetadata(String metadata, out bool defaultValue)
        {
            defaultValue = false;

            if (String.IsNullOrEmpty(metadata))
            {
                return null;
            }

            var Index = metadata.IndexOf(_MetadataDelimiter);

            if (Index < 0)
            {
                return null;
            }

            bool.TryParse(metadata.Substring(Index + 1), out defaultValue);
            return metadata.Substring(0, Index);
        }

        private CommandBarButton FindCommandBarButton(String tag)
        {
            return Package.FindCommandBarControl((x) => GetMetadataTag(x.Tag) == tag) as CommandBarButton;
        }

        private void SetControlCheckedState(String tag, bool defaultValue = false)
        {
            var Source = FindCommandBarButton(tag);
            if (Source == null)
            {
                Log.Error($"Failed to find {typeof(CommandBarButton)} using tag, '{tag}'");
                return;
            }

            bool Value = GetValue(tag, defaultValue);
            Source.Picture = Value ? Resource.PictureCheck as stdole.StdPicture : null;
            Package.UpdateButtonStyle(Source);
        }

        private bool GetTagDefault(PluginCommandOptions options, out String tag, out bool defaultValue)
        {
            tag = options.GetFlagValue(OptionNameBuildTag);
            defaultValue = false;

            if (String.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            var DefaultValueRaw = options.GetFlagValue(OptionNameDefaultValue);
            if (!String.IsNullOrEmpty(DefaultValueRaw))
            {
                bool.TryParse(DefaultValueRaw, out defaultValue);
            }

            return true;
        }

        private IEnumerable<String> GetFlagValues(PluginCommandOptions options, String flag)
        {
            if (options == null)
            {
                yield break;
            }

            var Values = options.GetFlagValue(flag);
            if (String.IsNullOrEmpty(Values))
            {
                yield break;
            }

            foreach (var Value in Values.Split(new Char[] { ',' }, 
                StringSplitOptions.RemoveEmptyEntries))
            {
                yield return Value;
            }
        }

        private void SetTagValues(IEnumerable<String> tags, bool value)
        {
            if (tags != null)
            {
                foreach (var Tag in tags)
                {
                    var Source = FindCommandBarButton(Tag);

                    if (Source == null)
                    {
                        continue;
                    }

                    bool DefaultValue;
                    DeserializeMetadata(Source.Tag, out DefaultValue);

                    SetValue(Tag, value, DefaultValue);
                    SetControlCheckedState(Tag, DefaultValue);
                }
            }
        }

        public override bool Execute(PluginCommandOptions options)
        {
            String Tag;
            bool DefaultValue;
            if (!GetTagDefault(options, out Tag, out DefaultValue))
            {
                return false;
            }

            var Value = ToggleValue(Tag, DefaultValue);
            SetControlCheckedState(Tag, DefaultValue);

            if (Value)
            {
                SetTagValues(GetFlagValues(options, OptionNameDisablesValue), false);
                SetTagValues(GetFlagValues(options, OptionNameEnablesValue), true);
            }

            return true;
        }
    }
}
