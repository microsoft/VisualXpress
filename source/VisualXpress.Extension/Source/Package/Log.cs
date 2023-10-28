// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows.Threading;
using System.Xml.Serialization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualXpress
{
	[Flags]
	public enum LogChannel
	{
		Verbose	= (1<<0),
		Info	= (1<<1),
		Error	= (1<<2),
		Status	= (1<<3),
	}

	public interface ILogDevice
	{
		void WriteDevice(LogChannel channel, string text);
	}

	public class LogDeviceConsole : ILogDevice
	{
		public void WriteDevice(LogChannel channel, string text)
		{
			if ((channel & LogChannel.Error) != 0)
				System.Console.Error.Write(text);
			else if ((channel & (LogChannel.Verbose|LogChannel.Info)) != 0)
				System.Console.Out.Write(text);
		}
	}

	public class LogDeviceFile : ILogDevice
	{
		private string m_FilePath;

		public LogDeviceFile(string filePath)
		{
			m_FilePath = filePath;
		}

		public void WriteDevice(LogChannel channel, string text)
		{
			if ((channel & (LogChannel.Verbose|LogChannel.Info|LogChannel.Error)) != 0)
			{
				string folderPath = Path.GetDirectoryName(m_FilePath);
				if (Directory.Exists(folderPath) == false)
					Directory.CreateDirectory(folderPath);

				File.AppendAllText(m_FilePath, text);
			}
		}
	}

	public class LogDeviceDebugger : ILogDevice
	{
		public void WriteDevice(LogChannel channel, string text)
		{
			System.Diagnostics.Debug.Write(text);
		}
	}

	public class LogDeviceOutputPane : ILogDevice
	{
		private string m_PaneName;
		private Guid m_PaneGuid;

		public LogDeviceOutputPane(string name, Guid guid)
		{
			m_PaneName = name;
			m_PaneGuid = guid;
		}

		public void WriteDevice(LogChannel channel, string text)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if ((channel & (LogChannel.Verbose|LogChannel.Info|LogChannel.Error)) != 0)
			{
				IVsOutputWindowPane pane = GetPane();
				if (pane != null)
				{
					pane.OutputStringThreadSafe(text);
					if (UserOptions.Instance.ActivateOutputWindow)
						pane.Activate();
				}
			}
		}

		private IVsOutputWindowPane GetPane()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			IVsOutputWindowPane pane = null;
			IVsOutputWindow window = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
			if (window != null)
			{
				window.GetPane(ref m_PaneGuid, out pane);
				if (pane == null)
				{
					window.CreatePane(ref m_PaneGuid, m_PaneName, 1, 0);
					window.GetPane(ref m_PaneGuid, out pane);
				}
			}
			return pane;
		}
	}

	public class LogDeviceStatusBar : ILogDevice
	{
		private const int TextFreezeTimeoutMs = 5000;
		private LatentAction m_UnfreezeAction;

		public void WriteDevice(LogChannel channel, string text)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if ((channel & LogChannel.Status) != 0)
			{
				IVsStatusbar bar = Package.GetGlobalService(typeof(SVsStatusbar)) as IVsStatusbar;
				if (bar != null)
				{
					if (m_UnfreezeAction != null)
					{
						m_UnfreezeAction.Flush();
						m_UnfreezeAction = null;
					}

					bar.SetText(text.Trim());
					bar.FreezeOutput(1);
					m_UnfreezeAction = new LatentAction(TextFreezeTimeoutMs, (context) => 
					{
						ThreadHelper.JoinableTaskFactory.RunAsync(async delegate {
							await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
							bar.FreezeOutput(0);
						}).FileAndForget(Telemetry.EventFaultName("LogDeviceStatusBar.WriteDevice"));
					});
				}
			}
		}
	}

	public sealed class Log
	{
		private static Log m_Instance;
		private List<ILogDevice> m_Devices;

		private Log()
		{
			m_Devices = new List<ILogDevice>();

			AddDevice(new LogDeviceConsole());
			AddDevice(new LogDeviceFile(String.Format("{0}\\Log\\{1:yyyy-MM-dd}\\VisualXpress-{1:HH-mm-ss}.txt", Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), DateTime.Now)));
			AddDevice(new LogDeviceDebugger());
			AddDevice(new LogDeviceOutputPane("VisualXpress", GuidList.GuidVisualXpressOutputPane));
			AddDevice(new LogDeviceStatusBar());
		}

		public static void Intitialize()
		{
			if (m_Instance == null)
				m_Instance = new Log();
		}

		public static Log Instance
		{
			get { return m_Instance; }
		}

		public static Dispatcher Dispatcher
		{
			get { return System.Windows.Application.Current.Dispatcher; }
		}

		public void AddDevice(ILogDevice device)
		{
			m_Devices.Add(device);
		}

		public void RemoveDevice(ILogDevice device)
		{
			m_Devices.Remove(device);
		}

		public static void Verbose(string text)
		{
			if (UserOptions.Instance.VerboseLogging)
				WriteLine(LogChannel.Verbose, text);
		}

		public static void Verbose(string format, params object[] args)
		{
			if (UserOptions.Instance.VerboseLogging)
				WriteLine(LogChannel.Verbose, String.Format(format, args));
		}

		public static void Info(string text)
		{
			WriteLine(LogChannel.Info, text);
		}

		public static void Info(string format, params object[] args)
		{
			WriteLine(LogChannel.Info, String.Format(format, args));
		}

		public static void Status(string text)
		{
			WriteLine(LogChannel.Status, text);
		}

		public static void Status(string format, params object[] args)
		{
			WriteLine(LogChannel.Status, String.Format(format, args));
		}

		public static void Error(string text)
		{
			WriteLine(LogChannel.Error, text);
		}

		public static void Error(string format, params object[] args)
		{
			WriteLine(LogChannel.Error, String.Format(format, args));
		}

		public static DataReceivedEventHandler ReceivedEventHandler(LogChannel channel)
		{
			return (object p, DataReceivedEventArgs e) => 
			{ 
				if (e.Data != null) 
				{ 
					WriteLine(channel, e.Data); 
				} 
			};
		}

		public static void WriteLine(LogChannel channel, string text)
		{
			try
			{
				ThreadHelper.JoinableTaskFactory.RunAsync(async delegate {
					await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
					Log.Instance.WriteLineInternal(channel, text);
				}).FileAndForget(Telemetry.EventFaultName("Log.WriteLine"));
			}
			catch (Exception e)
			{
				System.Console.WriteLine(String.Format("Log.WriteLine exception dispatching text: {0}", e.Message));	
			}
		}

		public void WriteLineInternal(LogChannel channel, string text)
		{
			try
			{
				if ((channel & LogChannel.Verbose) != 0 && UserOptions.Instance.VerboseLogging == false)
					return;
				if (text == null)
					return;
				if (String.IsNullOrEmpty(text))
					text = "\n";
				else if (text.EndsWith("\n") == false)
					text += "\n";
				foreach (var device in Log.Instance.m_Devices)
					device.WriteDevice(channel, text);
			}
			catch (Exception e)
			{
				System.Console.WriteLine(String.Format("Log.WriteLineInternal exception writing text: {0}", e.Message));	
			}
		}
	}
}
