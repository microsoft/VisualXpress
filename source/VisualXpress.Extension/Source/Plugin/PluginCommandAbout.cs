// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualXpress
{
	[PluginCommand("about", "Show VisualXpress information")]
	public class PluginCommandAbout : PluginCommand
	{
		public override bool Execute(PluginCommandOptions options)
		{
			ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
				var window = new AboutWindow() { Topmost = true };
				window.ShowDialog(); 
			}).FileAndForget(Telemetry.EventFaultName("PluginCommandAbout"));
			return true;
		}
	}
}
