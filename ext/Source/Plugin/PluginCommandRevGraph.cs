// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.VisualXpress
{
	[PluginCommand("revgraph", "Display the p4v revision graph window")]
	[PluginCommandOption(PluginCommandCompare.OptionNameSearch, "Search regex pattern against path for deriving target path", 1, PlugCommandOptionFlag.Optional)]
	[PluginCommandOption(PluginCommandCompare.OptionNameReplace, "Replace regex pattern against path for deriving target path", 1, PlugCommandOptionFlag.Optional)]
	public class PluginCommandRevGraph : PluginCommand
	{
		public override bool Execute(PluginCommandOptions options)
		{
			foreach (string itemPath in options.Arguments)
			{
				PluginCommandCompare.ExecuteP4vcFileCommand("revgraph", itemPath, options);
			}
			return true;
		}
	}
}

