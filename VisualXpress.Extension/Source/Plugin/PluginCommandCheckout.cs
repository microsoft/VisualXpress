// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualXpress
{
	[PluginCommand("checkout", "Check out a file")]
	[PluginCommandOption(PluginCommandCheckout.OptionNameChangelist, "Changelist number to open to", 1, PlugCommandOptionFlag.Optional)]
	[PluginCommandOption(PluginCommandCheckout.OptionNameModifiedItems, "Checkout modified document items", 0, PlugCommandOptionFlag.Optional)]
	public class PluginCommandCheckout : PluginCommand
	{
		public const string OptionNameChangelist = "-c";
		public const string OptionNameModifiedItems = "-m";

		public override bool Execute(PluginCommandOptions options)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			string[] arguments = options.Arguments.ToArray();
			if (arguments.Length == 0 && options.HasFlag(OptionNameModifiedItems))
				arguments = this.Package.ActiveModifiedItemPaths.ToArray();

			string changelist = options.GetFlag<string>(OptionNameChangelist);
			return CheckOutFiles(arguments, changelist);
		}

		public static bool CheckOutFiles(string[] filesToCheckOut, string changelistNumber = "", bool logExistingCheckout = true)
		{
			List<string> openAdd = new List<string>();
			List<string> openEdit = new List<string>();

			Perforce.Config config = Perforce.Process.GlobalDirectoryConfig(filesToCheckOut);
			string[] files = filesToCheckOut.Select(s => Perforce.Process.NormalizePath(s)).ToArray();
			Perforce.FStatResult fstat = Perforce.Process.FStat(files, Perforce.ProcessFlags.None, config);

			foreach (string file in files)
			{
				Perforce.FStatResult.Node node = fstat.FindNode(file);
				if (!AddFileToAppropriateActionList(node, file, ref openAdd, ref openEdit) && logExistingCheckout)
					Log.Info("{0} is already opened for edit/add", node.DepotFile);
			}

			string args = String.Empty;
			if (String.IsNullOrEmpty(changelistNumber) == false)
				args += String.Format(" -c {0}", changelistNumber);

			bool success = true;
			if (openAdd.Count > 0)
				success &= Perforce.Process.Execute(String.Format("add {0}", args), openAdd, Perforce.ProcessFlags.Echo, config).Success;
			if (openEdit.Count > 0)
				success &= Perforce.Process.Execute(String.Format("edit {0}", args), openEdit, Perforce.ProcessFlags.Echo, config).Success;

			if (files.Length > 0 && (openAdd.Count > 0 || openEdit.Count > 0))
				Log.Status("[{0}] checkout: {1}", success ? "SUCCESS" : "FAILED", String.Join(" ", files));
			return success;
		}

		private static bool AddFileToAppropriateActionList(Perforce.FStatResult.Node fstatNode, string file, ref List<string> openAdd, ref List<string> openEdit)
		{
			bool fileAdded = false;
			if (fstatNode == null || fstatNode.InDepot == false)
			{
				if (!DoesNodeContainAction(fstatNode, "add"))
				{
					openAdd.Add(file);
					fileAdded = true;
				}
			}
			else
			{
				if (!DoesNodeContainAction(fstatNode, "edit"))
				{
					openEdit.Add(file);
					fileAdded = true;
				}
			}
			return fileAdded;
		}

		private static bool DoesNodeContainAction(Perforce.FStatResult.Node node, string action)
		{
			return node?.Action != null && node.Action.Equals(action, StringComparison.OrdinalIgnoreCase);
		}
	}
}
