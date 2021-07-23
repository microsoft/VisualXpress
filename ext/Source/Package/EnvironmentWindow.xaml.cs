// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Microsoft.VisualXpress
{
	public partial class EnvironmentWindow : Window, INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged = delegate {};

		public EnvironmentWindow()
		{
			InitializeComponent();
			m_ListView.DataContext = this;
		}

		protected override void OnActivated(EventArgs e)
		{
			UpdateItems();
			base.OnActivated(e);
		}

		protected virtual void RaisePropertyChanged([CallerMemberName] String property = "")
		{
			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(property));
		}

		private void OnListViewSizeChanged(object sender, SizeChangedEventArgs e)
		{
			GridView gridView = m_ListView.View as GridView;
			if (gridView != null && gridView.Columns.Count > 0)
			{
				double leftWidth = 0;
				int columnIndex = 0;
				for (; columnIndex < gridView.Columns.Count-1; ++columnIndex)
					leftWidth += gridView.Columns[columnIndex].ActualWidth;

				gridView.Columns[columnIndex].Width = Math.Max(16, m_ListView.ActualWidth - leftWidth - SystemParameters.VerticalScrollBarWidth - 20);
			}
		}

		private void OnClickButtonAdd(object sender, RoutedEventArgs e)
		{
			Log.Verbose("OnClickButtonAdd");
			ShowEnvironmentInputDialog(null, "Add Process Variable");
		}

		private void OnClickButtonEdit(object sender, RoutedEventArgs e)
		{
			Log.Verbose("OnClickButtonEdit");
			ShowEnvironmentInputDialog(SelectedItem, "Edit Process Variable");
		}

		private void OnClickButtonDelete(object sender, RoutedEventArgs e)
		{
			EnvironmentVariableItem item = SelectedItem;
			SetEnvironmentVariable(new EnvironmentVariableItem(item?.Name, null));
		}

		private void OnClickButtonClose(object sender, RoutedEventArgs e)
		{
			this.DialogResult = false;
		}

		private void ShowEnvironmentInputDialog(EnvironmentVariableItem item, string description)
		{
			EnvironmentInputWindow window = new EnvironmentInputWindow(item, description);
			window.Item = item;
			window.Description = description;
			if (window.ShowDialog() == true)
				SetEnvironmentVariable(window.Item);
		}

		private void UpdateItems()
		{
			Items = new List<EnvironmentVariableItem>();
			foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
				Items.Add(new EnvironmentVariableItem(entry.Key.ToString(), entry.Value.ToString()));

			Items.Sort(new Comparison<EnvironmentVariableItem>((a, b) => String.Compare(a.Name, b.Name, true)));
			RaisePropertyChanged("Items");
		}

		private void SetEnvironmentVariable(EnvironmentVariableItem item)
		{
			if (item != null && String.IsNullOrEmpty(item.Name) == false)
			{
				Environment.SetEnvironmentVariable(item.Name, item.Value);
				UpdateItems();
			}
		}
				
		public List<EnvironmentVariableItem> Items
		{
			get;
			private set;
		}

		public EnvironmentVariableItem SelectedItem
		{
			get { return m_ListView.SelectedItem as EnvironmentVariableItem; }
		}

		private void OnListViewMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			Log.Verbose("OnListViewMouseDoubleClick");
			ShowEnvironmentInputDialog(SelectedItem, "Edit Process Variable");			
		}

		private void OnListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			bool hasSelection = SelectedItem != null;
			m_ButtonEdit.IsEnabled = hasSelection;
			m_ButtonDelete.IsEnabled = hasSelection;
		}
	}

	public class EnvironmentVariableItem
	{
		public EnvironmentVariableItem(string name = null, string value = null)
		{
			Name = name;
			Value = value;
		}

		public string Name 
		{ 
			get; 
		}

		public string Value 
		{ 
			get; 
		}
	}
}
