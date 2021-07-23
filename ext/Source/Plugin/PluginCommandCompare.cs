// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.VisualXpress
{
	[PluginCommand("compare", "Execute enhanced P4Compare which allows depot paths")]
	[PluginCommandOption(OptionNameLocalLeft, "Use local version of left file", 0, PlugCommandOptionFlag.Optional)]
	[PluginCommandOption(OptionNameLocalRight, "Use local version of right file", 0, PlugCommandOptionFlag.Optional)]
	[PluginCommandOption(OptionNameLocalBase, "Use local version of base file", 0, PlugCommandOptionFlag.Optional)]
	[PluginCommandOption(OptionNameSearch, "Search regex pattern against right depot path for deriving left path", 1, PlugCommandOptionFlag.Optional)]
	[PluginCommandOption(OptionNameReplace, "Replace regex pattern against right depot path for deriving left path", 1, PlugCommandOptionFlag.Optional)]
	[PluginCommandOption(OptionNameBranch, "Specify the name for a Perforce branchspec for mapping to left depot path", 1, PlugCommandOptionFlag.Optional)]
	public class PluginCommandCompare : PluginCommand
	{
		public const string OptionNameLocalLeft = "-ll";
		public const string OptionNameLocalRight = "-lr";
		public const string OptionNameLocalBase = "-lb";
		public const string OptionNameSearch = "-search";
		public const string OptionNameReplace = "-replace";
		public const string OptionNameBranch = "-branch";

		public override bool Execute(PluginCommandOptions options)
		{
			switch (options.Arguments.Count)
			{
				case 0:
				{
					Log.Error("Missing at file arguments for merging");
					return false;
				}
				case 1:
				{
					List<DepotPathMapping> mappings = new List<DepotPathMapping>();
					mappings.AddRange(this.GetSearchReplacePathMappings(options));
					mappings.AddRange(this.GetBranchPathMappings(options));

					if (mappings.Count == 0)
					{
						Log.Error("Merging one file must include branch, stream, or search and replace");
						return false;
					}

					ExecutePathMappingCompare(mappings, options.HasFlag(OptionNameLocalLeft), options.Arguments[0], options.HasFlag(OptionNameLocalRight));
					break;
				}
				case 2:
				{
					ExecuteFileCompare(options.Arguments[0], options.HasFlag(OptionNameLocalLeft), options.Arguments[1], options.HasFlag(OptionNameLocalRight));
					break;
				}
				case 3:
				{
					ExecuteFileCompare(options.Arguments[0], options.HasFlag(OptionNameLocalBase), options.Arguments[1], options.HasFlag(OptionNameLocalLeft), options.Arguments[2], options.HasFlag(OptionNameLocalRight));
					break;
				}
			}
			return true;
		}

		private void ExecutePathMappingCompare(IEnumerable<DepotPathMapping> mappings, bool leftLocal, string rightFile, bool rightLocal)
		{
			string depotRightFile = Perforce.Process.GetDepotFile(rightFile);
			if (String.IsNullOrEmpty(depotRightFile))
				depotRightFile = rightFile.Replace('\\', '/');

			string depotLeftFile = null;
			foreach (DepotPathMapping mapping in mappings.Reverse())
			{
				if (Regex.IsMatch(depotRightFile, mapping.Search, RegexOptions.IgnoreCase))
				{
					string depotLeftFileCandidate = Regex.Replace(depotRightFile, mapping.Search, mapping.Replace, RegexOptions.IgnoreCase);
					if (String.Compare(depotLeftFileCandidate, rightFile, StringComparison.CurrentCultureIgnoreCase) != 0)
					{
						depotLeftFile = depotLeftFileCandidate;
						Log.Verbose("Mapping: {0} -> {1}", depotRightFile, depotLeftFile);
						break;
					}
				}
			}

			if (String.IsNullOrEmpty(depotLeftFile))
			{
				Log.Error("Failed to map file to compare: {0}", depotRightFile);
				return;
			}

			ExecuteFileCompare(depotLeftFile, leftLocal, depotRightFile, rightLocal);
		}

		private bool ExecuteSearchReplaceCompare(string search, string replace, bool leftLocal, string rightFile, bool rightLocal)
		{
			string depotRightFile = Perforce.Process.GetDepotFile(rightFile);
			if (String.IsNullOrEmpty(depotRightFile))
				depotRightFile = rightFile.Replace('\\', '/');

			string leftFile = Regex.Replace(depotRightFile, search, replace, RegexOptions.IgnoreCase);
			if (String.Compare(leftFile, rightFile, StringComparison.CurrentCultureIgnoreCase) == 0)
				return false;

			ExecuteFileCompare(leftFile, leftLocal, rightFile, rightLocal);
			return true;
		}

		public static void ExecuteFileCompare(string leftFile, bool leftLocal, string rightFile, bool rightLocal)
		{
			ExecuteFileCompare(null, false, leftFile, leftLocal, rightFile, rightLocal);
		}

		public static void ExecuteFileCompare(string baseFile, bool baseLocal, string leftFile, bool leftLocal, string rightFile, bool rightLocal)
		{
			VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.Run(async delegate 
			{ 
				await ExecuteFileCompareAsync(baseFile, baseLocal, leftFile, leftLocal, rightFile, rightLocal); 
			});
		}

		private static async Task<bool> ExecuteFileCompareAsync(string baseFile, bool baseLocal, string leftFile, bool leftLocal, string rightFile, bool rightLocal)
		{
			using (var files = new CompareFileInfoList())
			{
				CompareFileInfo info;
				Perforce.FileCompareParams cmpParams = new Perforce.FileCompareParams();

				// Add left file info
				info = CompareFileInfo.Create(leftFile, leftLocal);
				if (info == null)
				{
					Log.Error("Failed to find left file: {0}", leftFile);
					return false;
				}
				cmpParams.LeftFilePath = info.LocalPath;
				cmpParams.LeftDisplayPath = info.DisplayPath;
				files.Add(info);
				
				// Add right file info
				info = CompareFileInfo.Create(rightFile, rightLocal);
				if (info == null)
				{
					Log.Error("Failed to find right file: {0}", rightFile);
					return false;
				}
				cmpParams.RightFilePath = info.LocalPath;
				cmpParams.RightDisplayPath = info.DisplayPath;
				files.Add(info);

				// Add base file info if it exists
				info = CompareFileInfo.Create(baseFile, baseLocal);
				if (info == null && String.IsNullOrEmpty(baseFile) == false)
				{
					Log.Error("Failed to find base file: {0}", baseFile);
					return false;
				}
				if (info != null)
				{
					cmpParams.BaseFilePath = info.LocalPath;
					cmpParams.BaseDisplayPath = info.DisplayPath;
					files.Add(info);
				}

				try
				{
					DateTime cmpStartTime = DateTime.Now;
					Perforce.Process.ExecuteFileCompare(cmpParams)?.WaitForExit();
					TimeSpan cmpWaitSpan = DateTime.Now - cmpStartTime;
					TimeSpan cmpDelaySpan = TimeSpan.FromMinutes(1) - cmpWaitSpan;
					
					// Ensure that we've waited a little before removing the temp file. 
					// This allows for a diff tool to startup and hand-off to another process.
					if (cmpDelaySpan > TimeSpan.Zero)
						await Task.Delay(cmpDelaySpan);
				}
				catch 
				{
					Log.Error("Failed executing compare");
					return false;
				}
			}
			return true;
		}

		public static bool ExecuteP4vcFileCommand(string cmd, string filePath, PluginCommandOptions options = null)
		{
			if (options != null)
			{
				string search = options.GetFlag<string>(OptionNameSearch);
				string replace = options.GetFlag<string>(OptionNameReplace);
				if (String.IsNullOrEmpty(search) == false && String.IsNullOrEmpty(replace) == false)
				{
					string depotFilePath = Perforce.Process.GetDepotFile(filePath);
					if (String.IsNullOrEmpty(depotFilePath))
						depotFilePath = filePath.Replace('\\', '/');

					filePath = Regex.Replace(depotFilePath, search, replace, RegexOptions.IgnoreCase);
				}
			}

			try
			{
				string[] argList = new string[]{ Perforce.Process.Connection().ToP4vString(), String.Format("{0} \"{1}\"", cmd, filePath) };
				string args = String.Join(" ", argList.Where(s => !String.IsNullOrWhiteSpace(s)));
				DataReceivedEventHandler outputReceiver = Log.ReceivedEventHandler(LogChannel.Info);

				Perforce.Process.ExecuteP4vc(args, outputReceiver)?.WaitForExit();
			}
			catch 
			{
				Log.Error("Failed executing p4vc command: {0}", cmd);
				return false;
			}
			return true;
		}

		public IEnumerable<DepotPathMapping> GetBranchPathMappings(PluginCommandOptions options)
		{
			string branch = options.GetFlag<string>(OptionNameBranch);
			if (String.IsNullOrEmpty(branch) == false)
			{
				Perforce.BranchResult branchResult = Perforce.Process.Execute<Perforce.BranchResult>(String.Format("branch -o {0}", branch));
				foreach (Perforce.BranchResult.Node node in branchResult.Nodes)
				{
					foreach (string view in node.View)
					{
						DepotPathMapping mapping = this.GetViewPathMapping(view);
						if (mapping != null)
							yield return mapping;
					}
				}
			}
		}

		public IEnumerable<DepotPathMapping> GetSearchReplacePathMappings(PluginCommandOptions options)
		{
			string search = options.GetFlag<string>(OptionNameSearch);
			string replace = options.GetFlag<string>(OptionNameReplace);
			if (String.IsNullOrEmpty(search) == false && String.IsNullOrEmpty(replace) == false)
				yield return new DepotPathMapping { Search=search, Replace=replace };
		}

		public DepotPathMapping GetViewPathMapping(string view)
		{
			string[] args = Utilities.CommandLineToArgs(view);
			if (args?.Length != 2)
				return null;
			if (args[0].StartsWith("-"))
				return null;
			
			int index = 1;
			DepotPathMapping mapping = new DepotPathMapping { Search=args[0], Replace=args[1] };
			mapping.Search = Regex.Escape(mapping.Search);
			mapping.Search = Regex.Replace(mapping.Search, "(\\\\.\\\\.\\\\.|\\\\\\*)", "(.+)");
			mapping.Replace = Regex.Replace(mapping.Replace, "(\\.\\.\\.|\\*)", m => String.Format("${0}", index++));
			return mapping;
		}

		public sealed class CompareFileInfo : IDisposable
		{
			public string LocalPath { get; private set; }
			public string DisplayPath { get; private set; }
			public Perforce.TempFile TempFile { get; private set; }
		
			public static CompareFileInfo Create(string filePath, bool local)
			{
				if (String.IsNullOrEmpty(filePath))
					return null;

				if (local == true)
				{
					string clientFilePath = Perforce.Process.GetClientFile(filePath, config:Perforce.Process.GlobalConfig);
					if (String.IsNullOrEmpty(clientFilePath))
					{
						clientFilePath = Perforce.Process.GetClientFile(filePath, config:Perforce.Process.EmptyConfig);
						if (String.IsNullOrEmpty(clientFilePath))
							clientFilePath = filePath;
					}
					return new CompareFileInfo { LocalPath = clientFilePath };
				}

				using (Perforce.TempFile tmp = new Perforce.TempFile())
				{
					Perforce.PrintResult print = Perforce.Process.Execute<Perforce.PrintResult>(String.Format("print -q -o \"{0}\"", tmp.Path), filePath);
					if (print.Success == false)
						return null;

					CompareFileInfo info = new CompareFileInfo { LocalPath = tmp.Path, DisplayPath = filePath, TempFile = new Perforce.TempFile(tmp) };
					return info;
				}
			}

			public void Dispose()
			{
 				if (TempFile != null)
				{
					TempFile.Dispose();
					TempFile = null;
				}
			}
		}

		public sealed class CompareFileInfoList : List<CompareFileInfo>, IDisposable
		{
			public void Dispose()
			{
 				foreach (var file in this)
					file.Dispose();
				this.Clear();
			}
		}

		public enum IntegSpecType
		{
			Branch,
			Stream
		}

		public class DepotPathMapping
		{
			public string Search;
			public string Replace;
		}
	}
}
