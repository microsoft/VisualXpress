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
			worker.RunWorkerCompleted += (object s, RunWorkerCompletedEventArgs e) => { try { this.RefreshConnectionItems(e.Result as ConnectionsInfo); } catch {} };
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
				foreach (var config in connectionsInfo.Connections)
				{
					CommandBarButton itemButton = this.Package.AddCommandBarControl(m_ConnectionPopup.Controls, MsoControlType.msoControlButton, Type.Missing, Type.Missing, m_ConnectionPopup.Controls.Count + 1) as CommandBarButton;
					itemButton.Caption = String.Join(", ", new string[]{config.Port, config.User, config.Client}.Where(s => !String.IsNullOrEmpty(s)));
					itemButton.Click += (CommandBarButton c, ref bool cd) => this.OnClickConnectionCommand(c, config);
					if (MatchConnectionConfig(connectionsInfo.CurrentConnection, config))
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
			openConnectionButton.Click += this.OnClickOpenConnection;
		}

		protected void OnClickOpenConnection(CommandBarButton button, ref bool cancel)
		{
			Log.Verbose("OnClickOpenConnection");
            var window = new ConnectionWindow() { Topmost = true };
			window.Config = Perforce.Process.Connection();

            if (window.ShowDialog() == true)
				this.SetCurrentConnection(window.Config);
		}

		protected void OnClickConnectionCommand(CommandBarButton button, Perforce.Config config)
		{
			Log.Verbose("OnClickConnectionCommand");
			this.SetCurrentConnection(config);			
		}

		protected void SetCurrentConnection(Perforce.Config config)
		{
			Log.WriteLine(LogChannel.Info|LogChannel.Status, String.Format("SetCurrentConnection: {0}", config.ToConnectionString()));
			Perforce.Process.GlobalConfig.ApplyConnection(config);
			this.TaskRefreshConnectionItems();
		}

		public static bool MatchConnectionConfig(Perforce.Config rhs, Perforce.Config lhs)
		{
			if (rhs == null || lhs == null)
				return false;
			if (String.Compare(rhs.User ?? "", lhs.User ?? "", StringComparison.InvariantCultureIgnoreCase) != 0)
				return false;
			if (String.Compare(NameIndex(rhs.Port, 0), NameIndex(lhs.Port, 0), StringComparison.InvariantCultureIgnoreCase) != 0)
				return false;
			if (String.Compare(rhs.Client ?? "", lhs.Client ?? "", StringComparison.InvariantCultureIgnoreCase) != 0)
				return false;
			return true;
		}

		protected static string NameIndex(string name, uint index)
		{
			name = name ?? "";
			string[] items = name.Split(new char[]{':','.'}, StringSplitOptions.RemoveEmptyEntries);
			return (index < items.Length ? items[index] : "");
		}

		protected static ConnectionsInfo CreateConnectionsInfo()
		{
			return new ConnectionsInfo { Connections = Perforce.Process.KnownConnections, CurrentConnection = Perforce.Process.Connection() };
		}

		protected class ConnectionItem
		{
			public CommandBarButton Button;
			public Perforce.Config Config;
		}

		protected class ConnectionsInfo
		{
			public Perforce.Config[] Connections;
			public Perforce.Config CurrentConnection;
		}
	}
}
