// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;


namespace Microsoft.VisualXpress
{
	public partial class UserOptionsControl : UserControl, IUserDialogControl
	{
		private bool m_UpdateOptionsPending = false;
		private OptionsInfo m_LastOptions = null;

		public UserOptionsControl()
		{
			InitializeComponent();
			DataContext = this;
		}

		private void OnClickCreateNewConnection(object sender, RoutedEventArgs e)
		{
			Perforce.Config currentConnection = null;
			if (m_ComboBoxConnections.SelectedItem is ComboBoxConnectionItem connectionItem)
				currentConnection = connectionItem.Config;
			if (currentConnection == null)
				currentConnection = m_LastOptions?.CurrentConnection ?? Perforce.Process.Connection();

			Perforce.Config[] possibleConnections = m_LastOptions?.Connections ?? Perforce.Process.GetKnownConnections();
			var model = new ConnectionViewModel
			{
				Port = currentConnection.Port,
				KnownPorts = possibleConnections
					.Select(x => x.Port)
					.Distinct(StringComparer.InvariantCultureIgnoreCase)
					.OrderBy(x => x)
					.ToArray(),

				Client = currentConnection.Client,
				KnownClients = possibleConnections?
					.Select(x => x.Client)
					.Distinct(StringComparer.InvariantCultureIgnoreCase)
					.OrderBy(x => x)
					.ToArray(),

				User = currentConnection.User,
				KnownUsers = possibleConnections?
					.Select(x => x.User)
					.Distinct(StringComparer.InvariantCultureIgnoreCase)
					.OrderBy(x => x)
					.ToArray(),
			};

			var window = new ConnectionWindow() { Topmost = true, Model = model };
			if (window.ShowDialog() == true)
			{
				UserOptions.Instance.CurrentConfig = window.Model.Config;
				this.TaskUpdateOptions();
			}
		}

		private void OnClickAbout(object sender, RoutedEventArgs e)
		{
			PluginCommandAbout about = new PluginCommandAbout();
			about.Execute(null);
		}

		public void OnActivate(CancelEventArgs e)
		{
			this.TaskUpdateOptions();
			m_CheckBoxActivateOutputWindow.IsChecked = UserOptions.Instance.ActivateOutputWindow;
			m_CheckBoxUseSolutionSymbolSettings.IsChecked = UserOptions.Instance.UseSolutionSymbolSettings;
			m_CheckBoxVerboseLogging.IsChecked = UserOptions.Instance.VerboseLogging;
			m_CheckBoxAutoCheckout.IsChecked = UserOptions.Instance.AutoCheckoutOnSave;
		}

		public void OnApply(EventArgs e, VisualStudio.Shell.DialogPage.ApplyKind kind)
		{
			if (kind == VisualStudio.Shell.DialogPage.ApplyKind.Apply)
			{
				UserOptions.Instance.ActivateOutputWindow = m_CheckBoxActivateOutputWindow.IsChecked == true;
				UserOptions.Instance.UseSolutionSymbolSettings = m_CheckBoxUseSolutionSymbolSettings.IsChecked == true;
				UserOptions.Instance.VerboseLogging = m_CheckBoxVerboseLogging.IsChecked == true;
				UserOptions.Instance.AutoCheckoutOnSave = m_CheckBoxAutoCheckout.IsChecked == true;
				if (m_ComboBoxConnections.SelectedItem is ComboBoxConnectionItem connectionItem)
				{
					Log.Info("ApplyConnection: {0}", connectionItem.Config.ToConnectionString());
					UserOptions.Instance.CurrentConfig = connectionItem.Config;
				}
			}
		}

		public void OnClosed(EventArgs e)
		{
			m_UpdateOptionsPending = false;
		}

		public void OnDeactivate(CancelEventArgs e)
		{
		}

		private void TaskUpdateOptions()
		{
			if (m_UpdateOptionsPending == false)
			{
				m_UpdateOptionsPending = true;
				this.ApplyOptionsInfo(null);

				BackgroundWorker worker = new BackgroundWorker();
				worker.DoWork += (object s, DoWorkEventArgs e) => 
				{ 
					e.Result = this.CreateOptionsInfo(); 
				};

				worker.RunWorkerCompleted += (object s, RunWorkerCompletedEventArgs e) => 
				{ 
					if (m_UpdateOptionsPending)
					{
						m_UpdateOptionsPending = false;
						this.ApplyOptionsInfo(e.Result as OptionsInfo); 
					}
				};

				worker.RunWorkerAsync();
			}
		}

		private OptionsInfo CreateOptionsInfo()
		{
			return new OptionsInfo 
			{ 
				Connections = Perforce.Process.GetKnownConnections(), 
				CurrentConnection = Perforce.Process.Connection() 
			};
		}

		private void ApplyOptionsInfo(OptionsInfo options)
		{
			m_ComboBoxConnections.IsEnabled = false;
			m_ComboBoxConnections.Items.Clear();
			m_LastOptions = options;
			if (options == null)
				return;

			int currentIndex = -1;
			Perforce.Config currentConnection = PluginCommandConnection.FindBestMatchingConnection(options.CurrentConnection, options.Connections);
			foreach (var config in options.Connections)
			{
				m_ComboBoxConnections.Items.Add(new ComboBoxConnectionItem{ Config = config });
				if (currentConnection == config)
					currentIndex = m_ComboBoxConnections.Items.Count-1;
			}

			if (currentIndex >= 0)
				m_ComboBoxConnections.SelectedIndex = currentIndex;

			m_ComboBoxConnections.IsEnabled = true;
		}

		private class ComboBoxConnectionItem
		{
			public Perforce.Config Config;

			public override string ToString()
			{
				return Config != null ? Config.ToConnectionString() : String.Empty;
			}
		}

		private class OptionsInfo
		{
			public Perforce.Config[] Connections;
			public Perforce.Config CurrentConnection;
		}
	}
}
