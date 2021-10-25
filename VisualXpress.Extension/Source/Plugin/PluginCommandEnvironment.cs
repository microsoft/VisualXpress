// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Interop;

namespace Microsoft.VisualXpress
{
	[PluginCommand("environment", "Show VisualXpress process environment variables")]
	public class PluginCommandEnvironment : PluginCommand
	{
		public override bool Execute(PluginCommandOptions options)
		{
			var thread = new System.Threading.Thread(new ThreadStart(() => {
				var window = new EnvironmentWindow();
				window.ShowDialog(); 
			}));

			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			return true;
		}
	}
}
