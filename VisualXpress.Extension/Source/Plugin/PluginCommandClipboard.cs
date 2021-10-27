// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualXpress
{
	[PluginCommand("clipboard", "Performs clipboard operations")]
	[PluginCommandOption(PluginCommandClipboard.OptionNameCopy, "Copies text to clipboard. Will use arguments if provided or selected text.")]
	[PluginCommandOption(PluginCommandClipboard.OptionNameUnformat, "Removes formating from clipboard text")]
	[PluginCommandOption(PluginCommandClipboard.OptionNameUncolour, "Removes colour from clipboard text")]
	[PluginCommandOption(PluginCommandClipboard.OptionNameGuid, "Copy a new guid to the clipboard with optional format argument")]
	[PluginCommandOption(PluginCommandClipboard.OptionNameDepotPath, "Copy the perforce depot path of the file arguments to the clipboard")]

	public class PluginCommandClipboard : PluginCommand
	{
		public const string OptionNameCopy = "-copy";
		public const string OptionNameUnformat = "-unformat";
		public const string OptionNameUncolour = "-uncolour";
		public const string OptionNameGuid = "-guid";
		public const string OptionNameDepotPath = "-depotPath";

		public override bool Execute(PluginCommandOptions options)
		{
			if (options.HasFlag(OptionNameCopy))
			{
				#pragma warning disable VSTHRD010
				ExecuteCopy(options);
				#pragma warning restore
			}
			return true;
		}

		private void ExecuteCopy(PluginCommandOptions options)
		{
			if (options.HasFlag(OptionNameGuid))
			{
				string copyText = Guid.NewGuid().ToString(options.Arguments.FirstOrDefault() ?? "");
				ExecuteActionSTA(() => SetClipboardText(copyText));
			}
			else if (options.HasFlag(OptionNameDepotPath))
			{
				Perforce.WhereResult where = Perforce.Process.Where(options.Arguments);
				string copyText = String.Join("\n", where.Nodes.Select(n => n.DepotFile).Where(s => !String.IsNullOrEmpty(s)));
				ExecuteActionSTA(() => SetClipboardText(copyText));
			}
			else if (options.Arguments.Count > 0)
			{
				string copyText = String.Join("\n", options.Arguments.Where(s => !String.IsNullOrEmpty(s)));
				ExecuteActionSTA(() => SetClipboardText(copyText));
			}
			else
			{
				ThreadHelper.JoinableTaskFactory.Run(async delegate
				{
					await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
					this.Package.ActiveDTE2.ExecuteCommand("Edit.Copy");
				});

				if (options.HasFlag(OptionNameUnformat))
					ExecuteActionSTA(() => ApplyClipboardUnformat());
				else if (options.HasFlag(OptionNameUncolour))
					ExecuteActionSTA(() => ApplyClipboardUncolour());
			}
		}

		private void ExecuteActionSTA(Action action)
		{
			try
			{
				var thread = new System.Threading.Thread(new ThreadStart(action));
				thread.SetApartmentState(ApartmentState.STA);
				thread.Start();
				thread.Join();
			}
			catch (Exception e)
			{
				Log.Error("PluginCommandClipboard.ExecuteActionSTA failed: {0}", e.Message);
			}
		}

		private static void SetClipboardText(string text)
		{
			try
			{
				System.Windows.Clipboard.SetText(text);
			}
			catch (Exception e)
			{
				Log.Error("PluginCommandClipboard.SetClipboardText failed: {0}", e.Message);
			}
		}

		private static void ApplyClipboardUnformat()
		{
			try
			{
				string text = System.Windows.Clipboard.GetText();
				System.Windows.Clipboard.SetText(text);
			}
			catch (Exception e)
			{
				Log.Error("PluginCommandClipboard.ApplyClipboardUnformat failed: {0}", e.Message);
			}
		}

		private static void ApplyClipboardUncolour()
		{
			try
			{
				System.Windows.IDataObject srcData = System.Windows.Clipboard.GetDataObject();
				object srcRtfObj = srcData.GetData(System.Windows.DataFormats.Rtf);
				if (srcRtfObj == null)
					return;

				string srcRtf = srcRtfObj.ToString();
				var fd = new System.Windows.Documents.FlowDocument();
				fd.FontFamily = new System.Windows.Media.FontFamily();
				var tr = new System.Windows.Documents.TextRange(fd.ContentStart, fd.ContentEnd);
			
				tr.Load(new System.IO.MemoryStream(Encoding.UTF8.GetBytes(srcRtf)), System.Windows.DataFormats.Rtf);
				tr.ApplyPropertyValue(System.Windows.Documents.TextElement.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
				tr.ApplyPropertyValue(System.Windows.Documents.TextElement.ForegroundProperty, System.Windows.Media.Brushes.Black);

				var os = new System.IO.MemoryStream();
				tr.Save(os, System.Windows.DataFormats.Rtf);
				var ba = os.GetBuffer();
				string dstRtf = Encoding.UTF8.GetString(ba);

				var dstData = new System.Windows.DataObject();
				dstData.SetData(System.Windows.DataFormats.Text, srcData.GetData(System.Windows.DataFormats.Text));
				dstData.SetData(System.Windows.DataFormats.Rtf, dstRtf);
				System.Windows.Clipboard.SetDataObject(dstData, true);
			}
			catch (Exception e)
			{
				Log.Error("PluginCommandClipboard.ApplyClipboardUncolour failed: {0}", e.Message);
			}
		}
	}
}
