// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Microsoft.VisualXpress
{
	public partial class ConnectionWindow : Window
	{
		public ConnectionWindow()
		{
			InitializeComponent();
		}

		public Perforce.Config Config
		{
			get
			{
				var c = new Perforce.Config();
				c.Port = m_Port.Text;
				c.User = m_User.Text;
				c.Client = m_Client.Text;
				return c;
			}
			set
			{
				m_Port.Text = value.Port;
				m_User.Text = value.User;
				m_Client.Text = value.Client;
			}
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
}
