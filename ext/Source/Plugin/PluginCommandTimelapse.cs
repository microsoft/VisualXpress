// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.VisualXpress
{
	[PluginCommand("timelapse", "Display the p4v time-lapse window")]
	[PluginCommandOption(PluginCommandCompare.OptionNameSearch, "Search regex pattern against path for deriving target path", 1, PlugCommandOptionFlag.Optional)]
	[PluginCommandOption(PluginCommandCompare.OptionNameReplace, "Replace regex pattern against path for deriving target path", 1, PlugCommandOptionFlag.Optional)]
	public class PluginCommandTimeLapse : PluginCommand
	{
		public override bool Execute(PluginCommandOptions options)
		{
			foreach (string itemPath in options.Arguments)
			{
				PluginCommandCompare.ExecuteP4vcFileCommand("timelapse", itemPath, options);
			}
			return true;
		}
	}
}

