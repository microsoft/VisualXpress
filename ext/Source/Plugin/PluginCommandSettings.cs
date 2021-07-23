// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Microsoft.VisualXpress
{
	[PluginCommand("settings", "Displays current Visual Studio settings store")]
	[PluginCommandOption(OptionNameFilter, "Filter regex pattern against settings to display", 1, PlugCommandOptionFlag.Optional)]
	public class PluginCommandSettings : PluginCommand
	{
		public const string OptionNameFilter = "-filter";
		private string m_Filter = null;
		private SettingsManager m_Settings;

		public override bool Execute(PluginCommandOptions options)
		{
			m_Filter = options.GetFlag<string>(OptionNameFilter);
			m_Settings = new SettingsManager();

			LogSeparator("Begin");
			LogCollections(m_Settings.GetSubCollectionNames(""));
			LogSeparator("End");
			return true;
		}

		public void LogSeparator(string id)
		{
			Log.Info("------ VisualXpress Settings Store {0} ------", id);
		}

		public void LogCollections(string[] collectionNames)
		{
			if (collectionNames == null)
				return;
			foreach (string collectionName in collectionNames.Where(n => !String.IsNullOrEmpty(n)))
			{
				if (IsVisible(collectionName))
					Log.Info(collectionName);

				LogProperties(m_Settings.GetPropertyNames(collectionName));
				LogCollections(m_Settings.GetSubCollectionNames(collectionName));
			}
		}

		public void LogProperties(string[] propertyNames)
		{
			if (propertyNames == null)
				return;
			foreach (string propertyName in propertyNames.Where(n => IsVisible(n)))
			{
				string value = m_Settings.GetValue<string>(propertyName, null, SettingsType.Process);
				if (value != null)
					Log.Info("{0} = {1}", propertyName, value);
			}
		}

		public bool IsVisible(string text)
		{
			if (String.IsNullOrEmpty(text))
				return false;
			if (String.IsNullOrEmpty(m_Filter) == false && Regex.IsMatch(text, m_Filter) == false && Regex.IsMatch(text.Replace('\\','/'), m_Filter) == false)
				return false;
			return true;
		}
	}
}
