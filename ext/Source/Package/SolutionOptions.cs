// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Serialization;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Microsoft.VisualXpress
{
	public sealed class SolutionOptions
	{
		private static SolutionOptions m_Instance = new SolutionOptions();

		private SolutionOptions()
		{
		}

		public static SolutionOptions Instance
		{
			get { return m_Instance; }
		}

		private SolutionSettings CreateSettings()
		{
			SolutionSettings settings = new SolutionSettings();
			settings.ApplyConnection(Perforce.Process.GlobalConfig);
			settings.MegaXgeConfig = PluginCommandBuildMegaXge.GlobalConfig;
			settings.SymbolSettings = GetCurrentSymbolSettings();
			return settings;
		}

		public bool WriteUserOptions(Package package, IStream pOptionsStream)
		{
			try
			{
				Log.Verbose("SolutionSettings.WriteUserOptions writing to stream");
				using (MemoryStream stream = new MemoryStream())
				{
					SolutionSettings settings = CreateSettings();
					Log.Verbose("SolutionSettings.WriteUserOptions settings: {0}", settings);
					XmlSerializer xml = new XmlSerializer(typeof(SolutionSettings));
					xml.Serialize(stream, settings);
					stream.Seek(0, SeekOrigin.Begin);
					WriteStream(pOptionsStream, stream.ToArray());
				}
			}
			catch (Exception e)
			{
				Log.Error("SolutionSettings.WriteUserOptions failed: {0}", e.Message);
			}
			return true;
		}

		public bool ReadUserOptions(Package package, IStream pOptionsStream)
		{
			try
			{
				Log.Verbose("SolutionSettings.ReadUserOptions reading from stream");
				using (MemoryStream stream = new MemoryStream(ReadStream(pOptionsStream)))
				{
					if (stream.Length > 0)
					{
						XmlSerializer xml = new XmlSerializer(typeof(SolutionSettings));
						SolutionSettings settings = xml.Deserialize(stream) as SolutionSettings;
						Log.Verbose("SolutionSettings.ReadUserOptions settings: {0}", settings);
						if (settings != null)
						{
							Perforce.Config config = settings.ToPerforceConfig();
							if (String.IsNullOrWhiteSpace(config.ToConnectionString()) == false)
							{
								Log.Info("ApplyConnection: {0}", config.ToConnectionString());
								Perforce.Process.GlobalConfig.ApplyConnection(config);
								if (package != null)
								{
									foreach (IPluginServiceConnection service in package.PluginServices.OfType<IPluginServiceConnection>())
										service.OnConnectionChanged();
								}
							}

							PluginCommandBuildMegaXge.GlobalConfig = settings.MegaXgeConfig;

							if (UserOptions.Instance.UseSolutionSymbolSettings)
								SetCurrentSymbolSettings(settings.SymbolSettings);
						}
					}
				}
			}
			catch (Exception e)
			{
				Log.Error("SolutionSettings.ReadUserOptions failed: {0}", e.Message);
			}
			return true;
		}

		private SolutionSymbolSettings GetCurrentSymbolSettings()
		{
			try
			{
				IVsDebuggerSymbolSettingsManager120A dbgSymManager = Package.GetGlobalService(typeof(SVsShellDebugger)) as IVsDebuggerSymbolSettingsManager120A;
				if (dbgSymManager == null)
					return null;
				IVsDebuggerSymbolSettings120A dbgSym = dbgSymManager.GetCurrentSymbolSettings();
				if (dbgSym == null)
					return null;

				SolutionSymbolSettings symbolSettings = new SolutionSymbolSettings {
					IsLoadAdjacent = dbgSym.IsLoadAdjacent.ToString(),
					IsManualLoad = dbgSym.IsManualLoad.ToString(),
					UseMSSymbolServers = dbgSym.UseMSSymbolServers.ToString(),
				};
				Log.Verbose("GetCurrentSymbolSettings: {0}", symbolSettings);
				return symbolSettings;
			}
			catch (Exception e)
			{
				Log.Error("SolutionSettings.GetCurrentSymbolSettings failed: {0}", e.Message);
			}
			return null;
		}

		private bool SetCurrentSymbolSettings(SolutionSymbolSettings symbolSettings)
		{
			try
			{
				if (symbolSettings == null)
					return false;
				IVsDebuggerSymbolSettingsManager120A dbgSymManager = Package.GetGlobalService(typeof(SVsShellDebugger)) as IVsDebuggerSymbolSettingsManager120A;
				if (dbgSymManager == null)
					return false;
				IVsDebuggerSymbolSettings120A dbgSym = dbgSymManager.GetCurrentSymbolSettings();
				if (dbgSym == null)
					return false;

				if (Boolean.TryParse(symbolSettings.IsLoadAdjacent, out bool bIsLoadAdjacent))
					dbgSym.IsLoadAdjacent = bIsLoadAdjacent;
				if (Boolean.TryParse(symbolSettings.IsManualLoad, out bool bIsManualLoad))
					dbgSym.IsManualLoad = bIsManualLoad;
				if (Boolean.TryParse(symbolSettings.UseMSSymbolServers, out bool bUseMSSymbolServers))
					dbgSym.UseMSSymbolServers = bUseMSSymbolServers;

				Log.Verbose("SetCurrentSymbolSettings: {0}", symbolSettings);
				dbgSymManager.SaveSymbolSettings(dbgSym);
				return true;
			}
			catch (Exception e)
			{
				Log.Error("SolutionSettings.GetCurrentSymbolSettings failed: {0}", e.Message);
			}
			return false;
		}

		private byte[] ReadStream(IStream pOptionsStream)
		{
			if (pOptionsStream == null)
				return new byte[0];
			using (MemoryStream data = new MemoryStream())
			{
				byte[] readData = new byte[64];
				while (true)
				{
					uint readCount;
					pOptionsStream.Read(readData, (uint)readData.Length, out readCount);
					if (readCount == 0)
						break;
					data.Write(readData, 0, (int)readCount);
				}
				return data.ToArray();
			}
		}

		private void WriteStream(IStream pOptionsStream, byte[] data)
		{
			if (pOptionsStream != null && data != null && data.Length > 0)
			{
				uint written;
				pOptionsStream.Write(data, (uint)data.Length, out written);
				if (written != (uint)data.Length)
					throw new Exception(String.Format("Failed writing only {0} of {1} bytes", written, data.Length));
			}
		}
	}

	[XmlRoot("SolutionSettings", Namespace="")]
	public class SolutionSettings
	{
		[XmlElement("P4Port")]
		public string P4Port { get; set; }

		[XmlElement("P4Client")]
		public string P4Client { get; set; }

		[XmlElement("P4User")]
		public string P4User { get; set; }

		[XmlElement("MegaXgeOptions")]
		public string MegaXgeConfig { get; set; }

		[XmlElement("SymbolSettings")]
		public SolutionSymbolSettings SymbolSettings { get; set; }

		public void ApplyConnection(Perforce.Config config)
		{
			if (config == null)
				return;
			if (String.IsNullOrEmpty(config.Port) == false)
				P4Port = config.Port;
			if (String.IsNullOrEmpty(config.User) == false)
				P4User = config.User;
			if (String.IsNullOrEmpty(config.Client) == false)
				P4Client = config.Client;
		}

		public Perforce.Config ToPerforceConfig()
		{
			return new Perforce.Config() { Port = P4Port, Client = P4Client, User = P4User };
		}

		public override string ToString()
		{
			return String.Join(", ", new string[] { this.P4Port, this.P4User, this.P4Client }.Where(s => !String.IsNullOrEmpty(s)));
		}
	}

	public class SolutionSymbolSettings
	{
		[XmlElement("IsManualLoad")]
		public string IsManualLoad { get; set; }

		[XmlElement("IsLoadAdjacent")]
		public string IsLoadAdjacent { get; set; }

		[XmlElement("UseMSSymbolServers")]
		public string UseMSSymbolServers { get; set; }

		public override string ToString()
		{
			return $"IsManualLoad={IsManualLoad}, IsLoadAdjacent={IsLoadAdjacent}, UseMSSymbolServers={UseMSSymbolServers}";
		}
	}
}

