// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.VisualXpress
{
	[PluginCommand("diffhave", "Diff a file against the #have revision")]
	[PluginCommandOption(PluginCommandDiffHave.OptionNameUseP4V, "Use p4vc to perform diffhave", 0, PlugCommandOptionFlag.Optional)]
	public class PluginCommandDiffHave : PluginCommand
	{
		public const string OptionNameUseP4V = "-p4v";

		public override bool Execute(PluginCommandOptions options)
		{
			List<Perforce.FStatResult.Node> fileNodes = new List<Perforce.FStatResult.Node>();

			Perforce.FStatResult fstat = Perforce.Process.FStat(options.Arguments);
			foreach (var file in options.Arguments)
			{
				Perforce.FStatResult.Node node = fstat.FindNode(file);
				if (node == null || node.InDepot == false)
					Log.Error("File not found in depot: {0}", file);
				else
					fileNodes.Add(node);
			}

			foreach (var fileNode in fileNodes)
			{
				// Unlikely to be configured to use P4vc diffhave
				if (options.HasFlag(OptionNameUseP4V))
				{
					PluginCommandCompare.ExecuteP4vcFileCommand("diffhave", fileNode.ClientFile);
					continue;
				}

				// If this is not the first revision, simply diff versus #have
				if (fileNode.HaveRev > 0)
				{
					PluginCommandCompare.ExecuteFileCompare(String.Format("{0}#have", fileNode.ClientFile), false, fileNode.ClientFile, true);
					continue;
				}

				// If this a resolved w/edit of branch, merge, or copy, then we find the resolved history and diff versus the first fromFile
				Perforce.ResolvedResult.Node resolvedNode = Perforce.Process.Execute<Perforce.ResolvedResult>("resolved", fileNode.ClientFile)?.Nodes?.FirstOrDefault();
				if (resolvedNode != null && String.IsNullOrEmpty(resolvedNode.FromFile) == false && String.IsNullOrEmpty(resolvedNode.EndFromRev) == false)
				{
					PluginCommandCompare.ExecuteFileCompare(String.Format("{0}#{1}", resolvedNode.FromFile, resolvedNode.EndFromRev.TrimStart('#')), false, fileNode.ClientFile, true);
					continue;
				}

				// If this has pending resolves of integrate after add, then force preview a resolve to find history and diff versus the fromFile
				Perforce.ResolveResult.Node resolveNode = Perforce.Process.Execute<Perforce.ResolveResult>("resolve -N -o -f", fileNode.ClientFile)?.Nodes?.FirstOrDefault();
				if (resolveNode != null && String.IsNullOrEmpty(resolveNode.FromFile) == false && String.IsNullOrEmpty(resolveNode.EndFromRev) == false)
				{
					PluginCommandCompare.ExecuteFileCompare(String.Format("{0}#{1}", resolveNode.FromFile, resolveNode.EndFromRev.TrimStart('#')), false, fileNode.ClientFile, true);
					continue;
				}
			}

			return fileNodes.Any();
		}
	}
}
