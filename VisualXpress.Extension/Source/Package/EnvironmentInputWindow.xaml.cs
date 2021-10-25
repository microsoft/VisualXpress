// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text;
using System.Xml;
using System.Threading.Tasks;
using System.Windows;

namespace Microsoft.VisualXpress
{
	public partial class EnvironmentInputWindow : Window
	{	
		public EnvironmentInputWindow(EnvironmentVariableItem item = null, string description = "")
		{
			InitializeComponent();
			m_VariableNameTextBox.IsReadOnly = (item != null && String.IsNullOrEmpty(item.Name) == false);
			Item = item;
			Description = description;
			DataContext = this;
		}

		public string Description
		{
			get;
			set;
		}

		public string VariableName
		{
			get;
			set;
		}

		public string VariableValue
		{
			get;
			set;
		}

		public EnvironmentVariableItem Item
		{
			get 
			{ 
				return new EnvironmentVariableItem(VariableName, VariableValue); 
			}
			set
			{
				if (value != null)
				{
					VariableName = value.Name ?? "";
					VariableValue = value.Value ?? "";
				}
			}
		}

		private void OnClickButtonCancel(object sender, RoutedEventArgs e)
		{
			this.DialogResult = false;
		}

		private void OnClickButtonOK(object sender, RoutedEventArgs e)
		{
			this.DialogResult = true;
		}

		private void OnWindowLoaded(object sender, RoutedEventArgs e)
		{
			MinHeight = ActualHeight;
			MaxHeight = ActualHeight;
		}
	}
}
