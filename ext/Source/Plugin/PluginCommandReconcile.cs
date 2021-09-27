// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.VisualXpress
{
	[PluginCommand("reconcile", "Performs perforce reconcile on specified files or folders")]
	[PluginCommandOption(PluginCommandReconcile.OptionNameChangelist, "Changelist number to open to", 1, PlugCommandOptionFlag.Optional)]
	public class PluginCommandReconcile : PluginCommand
	{
		public const string OptionNameChangelist = "-c";
		public const string PropertyNameP4Ignore = "P4IGNORE";
		public bool UsingRegexP4Ignore = true;

		public override bool Execute(PluginCommandOptions options)
		{
			string[] paths = options.Arguments.Select(s => Perforce.Process.NormalizePath(String.Format("{0}/...", s))).ToArray();
			if (paths.Length == 0)
				return true;

			string args = String.Empty;
			string changelist = options.GetFlag<string>(OptionNameChangelist);
			if (String.IsNullOrEmpty(changelist) == false)
				args += String.Format(" -c {0}", changelist);

			System.Threading.Tasks.Task.Run(() => this.ExecuteReconcile(args, paths));
			return true;
		}

		protected void ExecuteReconcile(string args, string[] paths)
		{
			Perforce.Config config = Perforce.Process.GlobalDirectoryConfig(paths);
			string[] ignorePatterns = this.Package.GetPropertyValue(PropertyNameP4Ignore).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
			foreach (var path in paths)
				Log.Info("reconcile: {0}", path);

			if (UsingRegexP4Ignore)
			{
				Perforce.ReconcileResult reconcile = Perforce.Process.Execute<Perforce.ReconcileResult>(String.Format("reconcile -n {0}", args), paths, Perforce.ProcessFlags.None, config);
				if (reconcile.Nodes.Any())
				{
					Regex ignoreRegex = null;
					if (ignorePatterns.Length > 0)
					{
						try { ignoreRegex = new Regex(String.Format("({0})", String.Join(")|(", ignorePatterns)), RegexOptions.IgnoreCase|RegexOptions.Compiled); }
						catch (Exception e) { Log.Error("Failed to generate P4IGNORE regex: {0}", e.Message); return; }
					}

					List<string> openAdd = new List<string>();
					List<string> openEdit = new List<string>();
					List<string> openDelete = new List<string>();
					foreach (Perforce.ReconcileResult.Node node in reconcile.Nodes)
					{
						List<string> openList = null;
						switch (node.Action)
						{
							case "add":
							case "move/add":
								openList = openAdd;
								break;
							case "edit":
								openList = openEdit;
								break;
							case "delete":
							case "move/delete":
								openList = openDelete;
								break;
						}

						if (openList == null)
						{
							Log.Error("Failed to reconcile action \"{0}\" for file: {1}", node.Action, node.ClientFile);
							continue;
						}

						if (System.IO.Directory.Exists(node.ClientFile))
							continue;
						if (ignoreRegex != null && (ignoreRegex.Match(node.ClientFile).Success || ignoreRegex.Match(node.DepotFile).Success))
							continue;

						openList.Add(node.ClientFile);
					}

					if (openAdd.Count > 0)
						Perforce.Process.Execute(String.Format("add {0}", args), openAdd, Perforce.ProcessFlags.Echo, config);
					if (openEdit.Count > 0)
						Perforce.Process.Execute(String.Format("edit {0}", args), openEdit, Perforce.ProcessFlags.Echo, config);
					if (openDelete.Count > 0)
						Perforce.Process.Execute(String.Format("delete {0}", args), openDelete, Perforce.ProcessFlags.Echo, config);
				}
			}
			else
			{
				using (TempFile ignoreFile = new TempFile(ignorePatterns))
				{
					config.Ignore = ignoreFile.FilePath;
					Perforce.Process.Execute(String.Format("reconcile {0}", args), paths, Perforce.ProcessFlags.Echo, config);
				}
			}
			Log.Info("reconcile: done");
		}
	}
}
