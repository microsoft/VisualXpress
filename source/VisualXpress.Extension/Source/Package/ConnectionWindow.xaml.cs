// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Linq;
using System.Text;
using System.Xml;
using System.Windows;

namespace Microsoft.VisualXpress
{
	public partial class ConnectionWindow : Window
	{
		public ConnectionWindow()
		{
			InitializeComponent();
		}

		public ConnectionViewModel Model
		{
			get { return DataContext as ConnectionViewModel; }
			set { DataContext = value; }
		}

		private void OnClickButtonClose(object sender, RoutedEventArgs e)
		{
			this.DialogResult = false;
		}

		private void OnClickButtonOK(object sender, RoutedEventArgs e)
		{
			this.DialogResult = true;
		}
	}

	public class ConnectionViewModel : ViewModel
	{
		public Perforce.Config Config
		{
			get
			{
				return new Perforce.Config
				{
					Port = this.Port,
					Client = this.Client,
					User = this.User,
				};
			}
			set
			{
				Port = value?.Port;
				Client = value?.Client;
				User = value?.User;
			}
		}

		public string Port
		{
			get
			{
				return m_Port;
			}
			set
			{
				if (String.Equals(m_Port, value) == false)
				{
					m_Port = value;
					RaisePropertyChanged();
				}
			}
		}
		private string m_Port;

		public string[] KnownPorts
		{
			get
			{
				return m_KnownPorts;
			}
			set
			{
				if (Array.Equals(m_KnownPorts, value) == false)
				{
					m_KnownPorts = value;
					RaisePropertyChanged();
				}
			}
		}
		private string[] m_KnownPorts;

		public string Client
		{
			get
			{
				return m_Client;
			}
			set
			{
				if (String.Equals(m_Client, value) == false)
				{
					m_Client = value;
					RaisePropertyChanged();
				}
			}
		}
		private string m_Client;

		public string[] KnownClients
		{
			get
			{
				return m_KnownClients;
			}
			set
			{
				if (Array.Equals(m_KnownClients, value) == false)
				{
					m_KnownClients = value;
					RaisePropertyChanged();
				}
			}
		}
		private string[] m_KnownClients;

		public string User
		{
			get
			{
				return m_User;
			}
			set
			{
				if (String.Equals(m_User, value) == false)
				{
					m_User = value;
					RaisePropertyChanged();
				}
			}
		}
		private string m_User;

		public string[] KnownUsers
		{
			get
			{
				return m_KnownUsers;
			}
			set
			{
				if (Array.Equals(m_KnownUsers, value) == false)
				{
					m_KnownUsers = value;
					RaisePropertyChanged();
				}
			}
		}
		private string[] m_KnownUsers;
	}
}
