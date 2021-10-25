// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Interop;

namespace Microsoft.VisualXpress
{
	[PluginCommand("about", "Show VisualXpress information")]
	public class PluginCommandAbout : PluginCommand
	{
		public override bool Execute(PluginCommandOptions options)
		{
			var thread = new System.Threading.Thread(new ThreadStart(() => {
				var window = new AboutWindow() { Topmost = true };

                window.ShowDialog(); 
			}));

			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			return true;
		}
	}
}
