// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualXpress
{
	[PluginCommand("openfile", "Open a file in Visual Studio")]
	[PluginCommandOption(PluginCommandOpenFile.OptionNameCreate, "Create file if it doesn't exist")]
	public class PluginCommandOpenFile : PluginCommand
	{
		public const string OptionNameCreate = "-c";

		public override bool Execute(PluginCommandOptions options)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			string[] files = options.Arguments.ToArray();
			foreach (string file in files)
			{
				if (System.IO.File.Exists(file) == false && options.HasFlag(OptionNameCreate))
					using (System.IO.File.Create(file)) {}

				if (System.IO.File.Exists(file) == false)
					Log.Error("VisualXpress PluginCommandOpenFile failed to find file: {0}", file);
				else
					this.Package.ActiveDTE2.ItemOperations.OpenFile(file, EnvDTE.Constants.vsViewKindTextView);
			}
			return true;
		}
	}
}
