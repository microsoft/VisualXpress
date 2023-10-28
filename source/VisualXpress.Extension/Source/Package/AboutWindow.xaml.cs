// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Windows;

namespace Microsoft.VisualXpress
{
	public partial class AboutWindow : Window, System.Windows.Forms.IWin32Window
	{
		public AboutWindow()
		{
			InitializeComponent();
			m_Description.Text = this.GetReleaseNotes();
			m_Version.Text = String.Format("Version {0}", this.GetPackageVersion());
		}

		private void OnClickCloseButton(object sender, RoutedEventArgs e)
		{
			this.DialogResult = true;
		}

		private string GetReleaseNotes()
		{
			try
			{
				var assembly = System.Reflection.Assembly.GetExecutingAssembly();
				var stream = assembly.GetManifestResourceStream("Microsoft.VisualXpress.ReleaseNotes.txt");
				if (stream != null)
				{
					var reader = new System.IO.StreamReader(stream);
					return reader.ReadToEnd();
				}
			}
			catch {}
			return "";
		}

		private string GetPackageVersion()
		{
			try
			{
				string manifestFilePath = String.Format("{0}\\extension.vsixmanifest", System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
				if (System.IO.File.Exists(manifestFilePath))
				{
					XmlDocument manifest = new XmlDocument();
					using (XmlTextReader manifestReader = new XmlTextReader(manifestFilePath))
					{
						manifestReader.Namespaces = false;
						manifest.Load(manifestReader);
						XmlElement element = manifest.SelectSingleNode("PackageManifest/Metadata/Identity") as XmlElement;
						if (element != null)
							return element.GetAttribute("Version");
					}
				}
			}
			catch {}
			return "<unknown>";
		}

		public IntPtr Handle
		{
			get { return (new System.Windows.Interop.WindowInteropHelper(this)).Handle; }
		}
	}
}
