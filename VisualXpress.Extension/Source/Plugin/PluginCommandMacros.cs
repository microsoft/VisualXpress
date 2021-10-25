// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualXpress
{
	[PluginCommand("macros", "Displays VisualXpress macro names and values")]
	public class PluginCommandMacros : PluginCommand
	{
		public override bool Execute(PluginCommandOptions options)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			LogSeparator("Begin");
			List<string> macroNames = new List<string>();
			macroNames.AddRange(this.BuiltinMacroNames);
			macroNames.AddRange(this.LocalMacroNames);
			
			foreach (var context in Enum.GetValues(typeof(Package.ContextType)).OfType<Package.ContextType>())
			{
				var items = this.Package.GetSelectedItems(context).ToArray();
				for (int itemIndex = 0; itemIndex < items.Length; ++itemIndex)
				{
					Package.ContextItem item = items[itemIndex];
					foreach (var name in macroNames.Select(n => String.Format("$({0})", n)))
						Log.Info("Context=[{0}] Item=[{1}][{2}] Name=[{3}] Value=[{4}]", context, itemIndex, item.Path, name, this.Package.ExpandText(name, item));
				}
			}
			LogSeparator("End");
			return true;
		}

		public void LogSeparator(string id)
		{
			Log.Info("------ VisualXpress Macros {0} ------", id);
		}

		public string[] BuiltinMacroNames
		{
			get 
			{
				List<string> names = new List<string>();
				foreach (FieldInfo fieldInfo in typeof(Microsoft.VisualXpress.Properties).GetFields(BindingFlags.Static|BindingFlags.Public))
				{
					if (fieldInfo.FieldType == typeof(string) && fieldInfo.IsLiteral)
						names.Add(fieldInfo.GetValue(null) as string);
				}
				return names.ToArray();
			}
		}

		public string[] LocalMacroNames
		{
			get { return this.Package.GetPropertyNames(); }
		}
	}
}
