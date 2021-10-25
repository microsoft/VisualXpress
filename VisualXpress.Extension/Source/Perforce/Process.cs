// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Reflection;

namespace Microsoft.VisualXpress.Perforce
{
	public static class Process
	{
		private static Config m_GlobalConfig = new Config();
		private const string P4Exe = "p4.exe";
		private const string P4vExe = "p4v.exe";
		private const string P4vcExe = "p4vc.exe";

		public static ResultType Execute<ResultType>(string args, IEnumerable<string> files = null, ProcessFlags flags = ProcessFlags.None, Config config = null) where ResultType : Result, new()
		{
			ResultType result = new ResultType();
			if (result.UseZTag())
				args = String.Format("-ztag {0}", args);

			bool redirectStandardInput = (files != null && files.Any() == true);
			if (redirectStandardInput)
				args = String.Format("-x - {0}", args);

			if (config == null)
				config = Process.GlobalDirectoryConfig(files);
			
			string configArgs = config.ToString();
			if (String.IsNullOrEmpty(configArgs) == false)
				args = String.Format("{0} {1}", configArgs, args);

			ProcessStartInfo si = new ProcessStartInfo();
			si.FileName = P4Exe;
			si.Arguments = args;
			si.CreateNoWindow = true;
			si.UseShellExecute = false;
			si.RedirectStandardError = true;
			si.RedirectStandardOutput = true;
			si.RedirectStandardInput = redirectStandardInput;

			if (System.IO.Directory.Exists(config.ConfigDirectory))
				si.WorkingDirectory = config.ConfigDirectory;

			if (String.IsNullOrEmpty(config.Ignore) == false)
				si.EnvironmentVariables["P4IGNORE"] = config.Ignore;

			List<OutputLine> output = new List<OutputLine>();
			int exitCode = 1;

			using (System.Diagnostics.Process p = new System.Diagnostics.Process())
			{
				try
				{
					var handler = new Action<DataReceivedEventArgs, Action<string>, OutputChannel>((e, writer, channel) => 
					{
						if (e.Data == null) 
							return;
						lock (output)
						{
							output.Add(new OutputLine{ Text=e.Data, Channel = channel }); 
							if (flags.HasFlag(ProcessFlags.Echo))
								writer(e.Data);
						}
					});

					p.StartInfo = si;
					p.ErrorDataReceived += new DataReceivedEventHandler((s, e) => handler(e, Log.Error, OutputChannel.StdErr));
					p.OutputDataReceived += new DataReceivedEventHandler((s, e) => handler(e, Log.Info, OutputChannel.StdOut));
					p.Start();
					p.BeginErrorReadLine();
					p.BeginOutputReadLine();

					if (si.RedirectStandardInput == true)
					{
						foreach (string file in files)
							p.StandardInput.WriteLine(file);
						p.StandardInput.Close();
					}

					p.WaitForExit();
					exitCode = p.ExitCode;
				}
				catch
				{
				}
			}

			result.ExitCode = exitCode;
			result.Parse(output.ToArray());
			return result;
		}

		public static FStatResult FStat(IEnumerable<string> files, ProcessFlags flags = ProcessFlags.None, Config config = null)
		{
			return Execute<FStatResult>("fstat", files, flags, config);
		}

		public static WhereResult Where(IEnumerable<string> files, ProcessFlags flags = ProcessFlags.None, Config config = null)
		{
			return Execute<WhereResult>("where", files, flags, config);
		}

		public static InfoResult Info(Config config = null)
		{
			return Execute<InfoResult>("info", config: config);
		}

		public static string GetClientFile(string path, ProcessFlags flags = ProcessFlags.None, Config config = null)
		{
			return Where(new string[] { path }, flags, config).Nodes.Select<WhereResult.Node, string>(n => n.ClientFile).FirstOrDefault();
		}

		public static string GetDepotFile(string path, ProcessFlags flags = ProcessFlags.None, Config config = null)
		{
			return Where(new string[] { path }, flags, config).Nodes.Select<WhereResult.Node, string>(n => n.DepotFile).FirstOrDefault();
		}

		public static Result Execute(string args, string file, ProcessFlags flags = ProcessFlags.None, Config config = null)
		{
			return Execute<Result>(args, file, flags, config);
		}

		public static Result Execute(string args, IEnumerable<string> files = null, ProcessFlags flags = ProcessFlags.None, Config config = null)
		{
			return Execute<Result>(args, files, flags, config);
		}

		public static ResultType Execute<ResultType>(string args, string file, ProcessFlags flags = ProcessFlags.None, Config config = null) where ResultType : Result, new()
		{
			return Execute<ResultType>(args, (String.IsNullOrEmpty(file) ? null : new string[] { file }), flags, config);
		}

		public static System.Diagnostics.Process ExecuteP4vc(string args, DataReceivedEventHandler outputReceiver = null)
		{
			System.Diagnostics.Process p = new System.Diagnostics.Process();
			try	
			{
				if (System.IO.File.Exists(System.IO.Path.Combine(P4vInstallFolder, P4vcExe)))
				{
					p.StartInfo.FileName = P4vcExe;
					p.StartInfo.Arguments = args;
				}
				else
				{
					p.StartInfo.FileName = P4vExe;
					p.StartInfo.Arguments = String.Format("-p4vc {0}", args);
				}

				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.UseShellExecute = false;

				if (outputReceiver != null)
				{
					p.StartInfo.RedirectStandardError = true;
					p.StartInfo.RedirectStandardOutput = true;
					p.ErrorDataReceived += outputReceiver;
					p.OutputDataReceived += outputReceiver;
				}

				Log.Info("Executing: {0} {1}", p.StartInfo.FileName, p.StartInfo.Arguments);
				p.Start();

				if (outputReceiver != null)
				{
					p.BeginErrorReadLine();
					p.BeginOutputReadLine();
				}
				return p;
			}
			catch (Exception e)
			{
				Log.Error("Process.ExecuteP4vc exception: {0}", e.Message);
				try
				{
					if (p.HasExited == false)
						p.Kill();
				}
				catch {}
			}
			return null;
		}

		public static System.Diagnostics.Process ExecuteFileCompare(string filePath0, string filePath1)
		{
			FileCompareParams cmpParams = new FileCompareParams();
			cmpParams.LeftFilePath = filePath0;
			cmpParams.RightFilePath = filePath1;
			return ExecuteFileCompare(cmpParams);
		}

		public static System.Diagnostics.Process ExecuteFileCompare(FileCompareParams cmpParams)
		{
			try	
			{ 
				string cmpExe = FileCompareExe;
				string cmpArgs = cmpParams.ToArgs(cmpExe);
				Log.Info("Executing: {0} {1}", cmpExe, cmpArgs);
				return System.Diagnostics.Process.Start(cmpExe, cmpArgs); 
			} 
			catch (Exception e)
			{
				Log.Error("Process.ExecuteFileCompare exception: {0}", e.Message);
			}
			return null;
		}

		public static string FileCompareExe
		{
			get
			{
				string settingsFile = Process.P4vApplicationSettingsFile;
				if (System.IO.File.Exists(settingsFile))
				{
					var document = new System.Xml.XPath.XPathDocument(settingsFile);
					var diffNode = document.CreateNavigator().SelectSingleNode("/PropertyList[@varName=\"ApplicationSettings\"]/Associations[@varName=\"DiffAssociations\"]");
					if (diffNode != null)
					{
						var runExtNode = diffNode.SelectSingleNode("RunExternal");
						bool runExt = false;
						if (runExtNode != null && String.IsNullOrEmpty(runExtNode.Value) == false && Boolean.TryParse(runExtNode.Value, out runExt) && runExt)
						{
							var appNode = diffNode.SelectSingleNode("Association[@varName=\"Default Association\"]/Application");
							if (appNode != null && String.IsNullOrEmpty(appNode.Value) == false)
								return System.IO.Path.GetFullPath(appNode.Value);
						}
					}
				}
				return "p4merge.exe";
			}
		}

		public static string P4vInstallFolder
		{
			get
			{
				foreach (string path in (Environment.GetEnvironmentVariable("PATH")??"").Split(new[]{';'}, StringSplitOptions.RemoveEmptyEntries))
				{
					string expandedPath = Environment.ExpandEnvironmentVariables(path);
					if (System.IO.Directory.Exists(expandedPath))
					{
						string folderPath = System.IO.Path.GetFullPath(expandedPath).TrimEnd('\\');
						if (System.IO.File.Exists(String.Format("{0}\\{1}", folderPath, P4Exe)))
							return folderPath;
					}
				}
				return String.Empty;
			}
		}

		public static Config[] P4vRecentConnections
		{
			get
			{
				SortedDictionary<string, Config> connections = new SortedDictionary<string, Config>();
				try
				{
					string settingsFile = Process.P4vApplicationSettingsFile;
					if (System.IO.File.Exists(settingsFile))
					{
						var document = new System.Xml.XPath.XPathDocument(settingsFile);
						string[] connectionPaths = new string[]{
							"/PropertyList[@varName=\"ApplicationSettings\"]/PropertyList[@varName=\"Connection\"]/StringList[@varName=\"OpenWorkspaces\"]",
							"/PropertyList[@varName=\"ApplicationSettings\"]/PropertyList[@varName=\"Connection\"]/StringList[@varName=\"RecentConnections\"]"
						};
						foreach (string path in connectionPaths)
						{
							var connectionsNode = document.CreateNavigator().SelectSingleNode(path);
							if (connectionsNode == null)
								continue;
							foreach (System.Xml.XPath.XPathNavigator strNode in connectionsNode.Select("String"))
							{
								Config c = Config.FromConnectionString(strNode.Value);
								if (c != null)
									connections[c.ToConnectionString()] = c;
							}
						}
					}
				}
				catch (Exception e)
				{
					Log.Error("Exception reading p4v RecentConnections: {0}", e.Message);
				}
				return connections.Values.ToArray();
			}
		}

		public static Config[] P4vConnections
		{
			get
			{
				SortedDictionary<string, Config> connections = new SortedDictionary<string, Config>();
				foreach (var c in P4vRecentConnections)
					connections[c.ToConnectionString()] = c;
				return connections.Values.ToArray();			
			}
		}

		public static Config[] KnownConnections
		{
			get
			{
				// Append connections from P4V settings
				var connections = new SortedDictionary<string, Config>(StringComparer.CurrentCultureIgnoreCase);
				foreach (var c in P4vConnections)
					connections[c.ToConnectionString()] = c;
				
				// Append current global connection
				Perforce.Config config = Perforce.Process.Connection();
				if (String.IsNullOrEmpty(config.Port) == false && String.IsNullOrEmpty(config.Client) == false && String.IsNullOrEmpty(config.User) == false)
					connections[config.ToConnectionString()] = config;

				var ports = new SortedDictionary<string, Config>(StringComparer.CurrentCultureIgnoreCase);
				foreach (var c in connections.Values)
					ports[c.Port] = c;
				
				// Append all known clients for this user & host from all ports
				foreach (var port in ports)
				{
					Config c = port.Value;
					ClientsResult clients = Execute<ClientsResult>(String.Format("clients -u \"{0}\"", c.User), config: c);
					foreach (var n in clients.Nodes)
					{
						if (String.IsNullOrEmpty(n.Client))
							continue;
						if (String.IsNullOrEmpty(n.Host) == false && String.Compare(n.Host, System.Environment.MachineName, StringComparison.CurrentCultureIgnoreCase) != 0)
							continue;
						Config clientConfig = new Config(c);
						clientConfig.Client = n.Client;
						connections[clientConfig.ToConnectionString()] = clientConfig;
					}
				}

				// Ensure the connection names are consistent, possibly removing duplicates
				var normConnections = new SortedDictionary<string, Config>(StringComparer.CurrentCultureIgnoreCase);
				foreach (var c in connections.Values)
				{
					Config nc = NormalizeConnection(c);
					normConnections[nc.ToConnectionString()] = nc;
				}
				return normConnections.Values.ToArray();
			}
		}

		public static string P4vApplicationSettingsFile
		{
			get { return String.Format("{0}\\.p4qt\\ApplicationSettings.xml", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)); }
		}

		public static Config Connection(Config config = null)
		{
			Config currentConfig = new Config(config);
			currentConfig.InheritConnection(Execute<SetResult>("set").Config);
			currentConfig.InheritConnection(NormalizeConnection(Info(config).Config));
			currentConfig.ApplyConnection(config ?? GlobalConfig);
			return currentConfig;
		}

		public static Config NormalizeConnection(Config config)
		{
			try
			{
				Config result = new Config(config);
				Config enviro = Execute<SetResult>("set").Config;
				if (String.IsNullOrEmpty(enviro.Port) == false && result.PortNumber == enviro.PortNumber)
				{
					string resultAddr = result.PortAddress;
					string enviroAddr = enviro.PortAddress;
					if (enviroAddr == resultAddr || (resultAddr == "" && enviroAddr != ""))
						result.Port = enviro.Port;
				}
				return result;
			}
			catch {}
			return new Config(config);
		}

		public static Config GlobalConfig
		{
			get { return m_GlobalConfig; }
		}

		public static Config EmptyConfig
		{
			get { return new Config(); }
		}

		public static Dictionary<string, string> GetPerforceEnvironmentVariables(Config config = null)
		{
			Dictionary<string, string> enviro = new Dictionary<string, string>();
			Config localConfig = Process.Connection(config);
			
			if (String.IsNullOrEmpty(localConfig.User) == false)
				enviro["P4USER"] = localConfig.User;
			if (String.IsNullOrEmpty(localConfig.Host) == false)
				enviro["P4HOST"] = localConfig.Host;
			if (String.IsNullOrEmpty(localConfig.Port) == false)
				enviro["P4PORT"] = localConfig.Port;
			if (String.IsNullOrEmpty(localConfig.Client) == false)
				enviro["P4CLIENT"] = localConfig.Client;
			return enviro;
		}

		public static string GetPerforceEnvironmentVariable(string name, Config config = null)
		{
			string value = String.Empty;	
			if (String.IsNullOrEmpty(name) == false)
			{
				if (String.Compare(name, "P4USER", true) == 0)
					value = Process.Connection(config).User;
				else if (String.Compare(name, "P4HOST", true) == 0)
					value = Process.Connection(config).Host;
				else if (String.Compare(name, "P4PORT", true) == 0)
					value = Process.Connection(config).Port;
				else if (String.Compare(name, "P4CLIENT", true) == 0)
					value = Process.Connection(config).Client;
				else if (String.Compare(name, "P4PASSWD", true) == 0)
					value = config != null ? config.Passwd : "";

				if (String.IsNullOrEmpty(value))
					value = System.Environment.GetEnvironmentVariable(name);
			}
			return value;
		}

		public static Config GlobalDirectoryConfig(string file)
		{
			return GlobalDirectoryConfig(new string[]{ file });
		}

		public static Config GlobalDirectoryConfig(IEnumerable<string> files)
		{
			Config config = new Perforce.Config(Perforce.Process.GlobalConfig);
			if (files != null)
			{
				foreach (var file in files)
				{
					string configDirectory = FindConfigDirectory(NormalizePath(file));
					if (String.IsNullOrEmpty(configDirectory) == false)
					{
						config.ConfigDirectory = configDirectory;
						break;
					}
				}
			}
			return config;
		}

		public static string FindConfigDirectory(string path)
		{
			if (String.IsNullOrEmpty(path) || System.IO.Path.IsPathRooted(path) == false)
				return null;
			if (Regex.Match(path, "^[\\/]{2}").Success)
				return null;
			if (System.IO.Directory.Exists(path))
				return path;
			return FindConfigDirectory(System.IO.Path.GetDirectoryName(path));
		}

		public static string NormalizePath(string path)
		{
			if (path == null)
				return "";
			path = path.Trim();
			if (path == "")
				return path;
			string[] rsplit = path.Split(new char[]{'\\','/'}, StringSplitOptions.RemoveEmptyEntries);
			List<string> split = new List<string>();
			for (int i = rsplit.Length-1, rel = 0; i >= 0; --i)
			{
				if (rsplit[i] == "..")
					rel++;
				else if (rel > 0)
					rel--;
				else
					split.Insert(0, rsplit[i]);
			}
			if (split.Count > 0)
			{
				if (path.StartsWith("//"))
				{
					path = String.Format("//{0}", String.Join("/", split.ToArray()));
				}
				else
				{
					if (Regex.Match(split[0], "^[a-z]:$").Success)
						split[0] = split[0].ToUpper();
					path = String.Join("\\", split.ToArray());
				}
			}
			return path;
		}
	}

	public class Result
	{
		public virtual void Parse(OutputLine[] output)
		{
		}

		public virtual bool UseZTag()
		{
			return false;
		}

		public virtual bool UseSingleNode()
		{
			return false;
		}

		public int ExitCode
		{
			get; 
			set;
		}

		public bool Success
		{
			get { return this.ExitCode == 0; }
		}

		protected List<NodeType> ParseNodes<NodeType>(OutputLine[] output) where NodeType : Result.NodeBase, new()
		{
			List<NodeType> nodes = new List<NodeType>();
			NodeType node = null;
			foreach (var line in output.Where(i => i.Channel == OutputChannel.StdOut))
			{
				Match m = Regex.Match(line.Text, @"^(\.\.\.\s)+(?<name>\w\S+)\s*(?<value>.*)");
				if (m.Success == false)
				{
					if (UseSingleNode() == false)
						node = null;
					continue;
				}

				if (node == null)
				{
					node = new NodeType();
					nodes.Add(node);
				}

				node.Fields[m.Groups["name"].Value] = m.Groups["value"].Value;
			}
			return nodes;
		}

		public class NodeBase
		{
			protected Dictionary<string, string> m_Fields = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);

			public T GetValue<T>(string field)
			{
				string value;
				if (m_Fields.TryGetValue(field, out value) == true && String.IsNullOrEmpty(value) == false)
					try { return (T)Convert.ChangeType(value, typeof(T)); } catch {}
				return default(T);
			}

			public Dictionary<string, string> Fields
			{
				get { return m_Fields; }
			}
		}
	}

	[Flags]
	public enum ProcessFlags
	{
		None = 0,
		Echo = 1<<0,
	}

	public class Config
	{
		public string Host { get; set; }
		public string Port { get; set; }
		public string Client { get; set; }
		public string User { get; set; }
		public string Passwd { get; set; }
		public string Ignore { get; set; }
		public string ConfigDirectory { get; set; }

		public Config(Config rhs = null)
		{
			if (rhs != null)
			{
				foreach (PropertyInfo property in typeof(Config).GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p => p.CanWrite))
					property.SetValue(this, property.GetValue(rhs));
			}
		}

		public Config Clone()
		{
			return new Config(this);
		}

		public override string ToString()
		{
			List<string> args = new List<string>();
			if (String.IsNullOrEmpty(this.Host) == false)
				args.Add(String.Format("-H \"{0}\"", this.Host));
			if (String.IsNullOrEmpty(this.Port) == false)
				args.Add(String.Format("-p \"{0}\"", this.Port));
			if (String.IsNullOrEmpty(this.Client) == false)
				args.Add(String.Format("-c \"{0}\"", this.Client));
			if (String.IsNullOrEmpty(this.User) == false)
				args.Add(String.Format("-u \"{0}\"", this.User));
			if (String.IsNullOrEmpty(this.Passwd) == false)
				args.Add(String.Format("-P \"{0}\"", this.Passwd));
			return String.Join(" ", args);
		}

		public string ToP4vString()
		{
			List<string> args = new List<string>();
			if (String.IsNullOrEmpty(this.Port) == false)
				args.Add(String.Format("-p \"{0}\"", this.Port));
			if (String.IsNullOrEmpty(this.Client) == false)
				args.Add(String.Format("-c \"{0}\"", this.Client));
			if (String.IsNullOrEmpty(this.User) == false)
				args.Add(String.Format("-u \"{0}\"", this.User));
			return String.Join(" ", args);
		}

		public string ToConnectionString()
		{
			return String.Join(", ", new string[] { this.Port, this.User, this.Client }.Where(s => !String.IsNullOrEmpty(s)));
		}

		public static Config FromConnectionString(string text)
		{
			if (String.IsNullOrEmpty(text))
				return null;
			string[] tokens = text.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			if (tokens.Length != 3)
				return null;
			Config c = new Config();
			c.Port = tokens[0].Trim();
			c.User = tokens[1].Trim();
			c.Client = tokens[2].Trim();
			return c;
		}

		public void ApplyConnection(Config config)
		{
			if (config == null)
				return;
			if (String.IsNullOrEmpty(config.Port) == false)
				Port = config.Port;
			if (String.IsNullOrEmpty(config.User) == false)
				User = config.User;
			if (String.IsNullOrEmpty(config.Client) == false)
				Client = config.Client;
		}

		public void InheritConnection(Config config)
		{
			if (config == null)
				return;
			if (String.IsNullOrEmpty(Port) && String.IsNullOrEmpty(config.Port) == false)
				Port = config.Port;
			if (String.IsNullOrEmpty(User) && String.IsNullOrEmpty(config.User) == false)
				User = config.User;
			if (String.IsNullOrEmpty(Client) && String.IsNullOrEmpty(config.Client) == false)
				Client = config.Client;
		}

		public string PortAddress
		{
			get
			{
				try
				{
					string portName = this.PortName;
					if (String.IsNullOrEmpty(portName) == false)
					{
						IEnumerable<IPAddress> addrs = Dns.GetHostAddresses(this.PortName).Where(addr => addr != null && addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
						if (addrs.Any())
							return addrs.First().ToString();
					}
				}
				catch {}
				return "";
			}
		}

		public string PortName
		{
			get { return Endpoint.Parse(Port).PortName; }
		}

		public string PortNumber
		{
			get { return Endpoint.Parse(Port).PortNumber; }
		}
	}

	public class FileCompareParams
	{
		public string LeftFilePath { get; set; }
		public string LeftDisplayPath { get; set; }
		public string RightFilePath { get; set; }
		public string RightDisplayPath { get; set; }
		public string BaseFilePath { get; set; }
		public string BaseDisplayPath { get; set; }

		public string ToArgs(string cmpExe)
		{
			List<string> result = new List<string>();
			switch (System.IO.Path.GetFileNameWithoutExtension(cmpExe).ToLower())
			{
				case "p4merge":
				{
					if (String.IsNullOrEmpty(this.BaseDisplayPath) == false)
						result.Add(String.Format("-nb \"{0}\"", this.BaseDisplayPath));
					if (String.IsNullOrEmpty(this.LeftDisplayPath) == false)
						result.Add(String.Format("-nl \"{0}\"", this.LeftDisplayPath));
					if (String.IsNullOrEmpty(this.RightDisplayPath) == false)
						result.Add(String.Format("-nr \"{0}\"", this.RightDisplayPath));

					if (String.IsNullOrEmpty(this.BaseFilePath) == false)
						result.Add(String.Format("\"{0}\"", this.BaseFilePath));
					if (String.IsNullOrEmpty(this.LeftFilePath) == false)
						result.Add(String.Format("\"{0}\"", this.LeftFilePath));
					if (String.IsNullOrEmpty(this.RightFilePath) == false)
						result.Add(String.Format("\"{0}\"", this.RightFilePath));
					break;
				}
				case "bcomp":
				case "bcompare":
				{
					if (String.IsNullOrEmpty(this.LeftDisplayPath) == false)
						result.Add(String.Format("\"/title1={0}\"", this.LeftDisplayPath));
					if (String.IsNullOrEmpty(this.RightDisplayPath) == false)
						result.Add(String.Format("\"/title2={0}\"", this.RightDisplayPath));
					if (String.IsNullOrEmpty(this.BaseDisplayPath) == false)
						result.Add(String.Format("\"/title3={0}\"", this.BaseDisplayPath));
					
					if (String.IsNullOrEmpty(this.LeftFilePath) == false)
						result.Add(String.Format("\"{0}\"", this.LeftFilePath));
					if (String.IsNullOrEmpty(this.RightFilePath) == false)
						result.Add(String.Format("\"{0}\"", this.RightFilePath));
					if (String.IsNullOrEmpty(this.BaseFilePath) == false)
						result.Add(String.Format("\"{0}\"", this.BaseFilePath));
					break;
				}
				default:
				{
					if (String.IsNullOrEmpty(this.BaseFilePath) == false)
						result.Add(String.Format("\"{0}\"", this.BaseFilePath));
					if (String.IsNullOrEmpty(this.LeftFilePath) == false)
						result.Add(String.Format("\"{0}\"", this.LeftFilePath));
					if (String.IsNullOrEmpty(this.RightFilePath) == false)
						result.Add(String.Format("\"{0}\"", this.RightFilePath));
					break;
				}
			}
			return String.Join(" ", result);
		}
	}

	public enum OutputChannel
	{
		StdOut,
		StdErr
	}

	public struct OutputLine
	{
		public string Text;
		public OutputChannel Channel; 

		public override string ToString()
		{
			return String.Format("{0}: {1}", Channel, Text);
		}
	}

	public struct Endpoint
	{
		public string Scheme;
		public string PortName;
		public string PortNumber;

		public static Endpoint Parse(string port)
		{
			Endpoint result = new Endpoint{ Scheme="", PortName="", PortNumber="" };
			Match m = Regex.Match(port ?? "", @"^((?<scheme>\w+):)?(?<name>[^:]+)(:(?<number>\d+))?$");
			if (m.Success)
			{
				result.Scheme = m.Groups["scheme"].Value ?? "";
				result.PortName = m.Groups["name"].Value ?? "";
				result.PortNumber = m.Groups["number"].Value ?? "";
			}
			return result;
		}
	}
}
