// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.CommandBars;
using System.Windows.Interop;

namespace Microsoft.VisualXpress
{
	[PluginCommand("connection", "Perforce connection options")]
	public class PluginCommandConnection : PluginCommand, IPluginServiceConnection
	{
		private CommandBarPopup m_ConnectionPopup = null;

		public override bool InitializeCommandBar(CommandBarControls controls, MenuItem item, ref int index, bool beginGroup)
		{
			m_ConnectionPopup = this.Package.AddCommandBarControl(controls, MsoControlType.msoControlPopup, Type.Missing, Type.Missing, index++) as CommandBarPopup;
			m_ConnectionPopup.Caption = item.Title;
			m_ConnectionPopup.BeginGroup = beginGroup;

			this.TaskRefreshConnectionItems();
			this.Package.RegisterPluginService(this);
			return true;
		}

		public void OnConnectionChanged()
		{
			this.TaskRefreshConnectionItems();
		}

		protected void TaskRefreshConnectionItems()
		{
			this.RefreshConnectionItems(null);

			BackgroundWorker worker = new BackgroundWorker();
			worker.DoWork += (object s, DoWorkEventArgs e) => { e.Result = CreateConnectionsInfo(); };
			worker.RunWorkerCompleted += (object s, RunWorkerCompletedEventArgs e) => { try { this.RefreshConnectionItems(e.Result as ConnectionsInfo); } catch { } };
			worker.RunWorkerAsync();
		}

		protected void RefreshConnectionItems(ConnectionsInfo connectionsInfo)
		{
			if (this.Package.IsExistingCommandBarControl(m_ConnectionPopup) == false)
				return;

			for (int i = m_ConnectionPopup.Controls.Count; i > 0; --i)
				this.Package.RemoveCommandBarControl(m_ConnectionPopup.Controls[i]);

			if (connectionsInfo != null)
			{
				Perforce.Config currentConnection = FindBestMatchingConnection(connectionsInfo.CurrentConnection, connectionsInfo.KnownConnections);
				foreach (var config in connectionsInfo.KnownConnections)
				{
					CommandBarButton itemButton = this.Package.AddCommandBarControl(m_ConnectionPopup.Controls, MsoControlType.msoControlButton, Type.Missing, Type.Missing, m_ConnectionPopup.Controls.Count + 1) as CommandBarButton;
					itemButton.Caption = String.Join(", ", new string[] { config.Port, config.User, config.Client }.Where(s => !String.IsNullOrEmpty(s)));
					itemButton.Click += (CommandBarButton c, ref bool cd) => this.OnClickConnectionCommand(c, config);
					if (config == currentConnection)
					{
						itemButton.Picture = Resource.PictureCheck as stdole.StdPicture;
						itemButton.Style = MsoButtonStyle.msoButtonIconAndCaption;
					}
					else
					{
						itemButton.Picture = null;
						itemButton.Style = MsoButtonStyle.msoButtonCaption;
					}
				}
			}

			CommandBarButton openConnectionButton = this.Package.AddCommandBarControl(m_ConnectionPopup.Controls, MsoControlType.msoControlButton, Type.Missing, Type.Missing, m_ConnectionPopup.Controls.Count + 1) as CommandBarButton;
			openConnectionButton.BeginGroup = true;
			openConnectionButton.Caption = "Open Connection ...";
			openConnectionButton.Click += (CommandBarButton button, ref bool cancel) =>
			{
				this.OnClickOpenConnection(button, ref cancel, connectionsInfo);
			};
		}

		protected void OnClickOpenConnection(CommandBarButton button, ref bool cancel, ConnectionsInfo connectionsInfo)
		{
			Log.Verbose("OnClickOpenConnection");

			Perforce.Config currentConnection = Perforce.Process.Connection();

			var model = new ConnectionViewModel
			{
				Port = currentConnection.Port,
				KnownPorts = connectionsInfo?.PossibleClientConnections?
					.Select(x => x.Port)
					.Distinct(StringComparer.InvariantCultureIgnoreCase)
					.OrderBy(x => x)
					.ToArray(),

				Client = currentConnection.Client,
				KnownClients = connectionsInfo?.PossibleClientConnections?
					.Select(x => x.Client)
					.Distinct(StringComparer.InvariantCultureIgnoreCase)
					.OrderBy(x => x)
					.ToArray(),

				User = currentConnection.User,
				KnownUsers = connectionsInfo?.PossibleClientConnections?
					.Select(x => x.User)
					.Distinct(StringComparer.InvariantCultureIgnoreCase)
					.OrderBy(x => x)
					.ToArray(),
			};

			var window = new ConnectionWindow() { Topmost = true, Model = model };
			if (window.ShowDialog() == true)
			{
				this.SetCurrentConnection(window.Model.Config);
			}
		}

		protected void OnClickConnectionCommand(CommandBarButton button, Perforce.Config config)
		{
			Log.Verbose("OnClickConnectionCommand");
			this.SetCurrentConnection(config);
		}

		protected void SetCurrentConnection(Perforce.Config config)
		{
			Log.WriteLine(LogChannel.Info | LogChannel.Status, String.Format("SetCurrentConnection: {0}", config.ToConnectionString()));
			Perforce.Process.GlobalConfig.ApplyConnection(config);
			this.TaskRefreshConnectionItems();
		}

		public static Perforce.Config FindBestMatchingConnection(Perforce.Config findConfig, IEnumerable<Perforce.Config> configs)
		{
			if (findConfig == null)
			{
				return null;
			}

			int matchingScore = 0;
			Perforce.Config matchingConfig = null;
			foreach (Perforce.Config config in configs)
			{
				int score = 0;
				if (String.Compare(config?.User ?? "", findConfig.User ?? "", StringComparison.InvariantCultureIgnoreCase) == 0 &&
					String.Compare(config?.PortName ?? "", findConfig.PortName ?? "", StringComparison.InvariantCultureIgnoreCase) == 0 &&
					String.Compare(config?.Client ?? "", findConfig.Client ?? "", StringComparison.InvariantCultureIgnoreCase) == 0)
				{
					score++;
					if (String.Compare(config?.Port ?? "", findConfig.Port ?? "", StringComparison.InvariantCultureIgnoreCase) == 0)
					{
						score++;
					}
				}
				if (score > matchingScore)
				{
					matchingScore = score;
					matchingConfig = config;
				}
			};

			return matchingConfig;
		}

		protected static string NameIndex(string name, uint index)
		{
			name = name ?? "";
			string[] items = name.Split(new char[] { ':', '.' }, StringSplitOptions.RemoveEmptyEntries);
			return (index < items.Length ? items[index] : "");
		}

		protected static ConnectionsInfo CreateConnectionsInfo()
		{
			return new ConnectionsInfo 
			{ 
				PossibleClientConnections = Perforce.Process.GetKnownConnections(includeAllClients:true), 
				KnownConnections = Perforce.Process.GetKnownConnections(), 
				CurrentConnection = Perforce.Process.Connection() 
			};
		}

		protected class ConnectionItem
		{
			public CommandBarButton Button;
			public Perforce.Config Config;
		}

		protected class ConnectionsInfo
		{
			public Perforce.Config[] PossibleClientConnections;
			public Perforce.Config[] KnownConnections;
			public Perforce.Config CurrentConnection;
		}
	}
}
