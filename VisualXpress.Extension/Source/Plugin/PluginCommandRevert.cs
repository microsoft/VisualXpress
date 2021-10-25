// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.VisualXpress
{
	[PluginCommand("revert", "Revert a file")]
	[PluginCommandOption(PluginCommandRevert.OptionNameForce, "Force sync and revert")]
	public class PluginCommandRevert : PluginCommand
	{
		public const string OptionNameForce = "-f";

		public override bool Execute(PluginCommandOptions options)
		{
			bool success = true;
			Perforce.Config config = Perforce.Process.GlobalDirectoryConfig(options.Arguments);
			string[] files = options.Arguments.Select(s => Perforce.Process.NormalizePath(s)).ToArray();
			if (files.Length > 0)
			{
				if (options.HasFlag(OptionNameForce))
					Perforce.Process.Execute("sync -f", files.Select(n => String.Format("{0}#have", n)), Perforce.ProcessFlags.Echo, config);
				
				success &= Perforce.Process.Execute("revert", files, Perforce.ProcessFlags.Echo, config).Success;
				Log.Status("[{0}] revert: {1}", success ? "SUCCESS" : "FAILED", String.Join(" ", files));
			}
			return success;
		}
	}
}
