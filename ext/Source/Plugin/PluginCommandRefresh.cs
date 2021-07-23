// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.VisualXpress
{
	[PluginCommand("refresh", "Refresh the VisualXpress settings and interface")]
	public class PluginCommandRefresh : PluginCommand
	{
		public override bool Execute(PluginCommandOptions options)
		{
			this.Package.Refresh();
			return true;
		}
	}
}
