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

		public UserOptionsControl()
		{
			InitializeComponent();
			DataContext = this;
		}

		private void OnClickCreateNewConnection(object sender, RoutedEventArgs e)
		{
			var window = new ConnectionWindow();
			var connectionItem = m_ComboBoxConnections.SelectedItem as ComboBoxConnectionItem;
			if (connectionItem != null)
				window.Config = connectionItem.Config;

			if (window.ShowDialog() == true)
			{
				UserOptions.Instance.CurrentConfig = window.Config;
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
			return new OptionsInfo { Connections = Perforce.Process.KnownConnections, CurrentConnection = Perforce.Process.Connection() };
		}

		private void ApplyOptionsInfo(OptionsInfo options)
		{
			m_ComboBoxConnections.IsEnabled = false;
			m_ComboBoxConnections.Items.Clear();
			if (options == null)
				return;

			int currentIndex = -1;
			foreach (var config in options.Connections)
			{
				m_ComboBoxConnections.Items.Add(new ComboBoxConnectionItem{ Config = config });
				if (currentIndex < 0 && PluginCommandConnection.MatchConnectionConfig(options.CurrentConnection, config))
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
