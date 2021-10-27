// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualXpress
{
	[PluginCommand("refresh", "Refresh the VisualXpress settings and interface")]
	public class PluginCommandRefresh : PluginCommand
	{
		public override bool Execute(PluginCommandOptions options)
		{
			return ThreadHelper.JoinableTaskFactory.Run(async delegate
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
				this.Package.Refresh();
				return true;
			});
		}
	}
}
