// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.CommandBars;
using EnvDTE;
using EnvDTE80;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualXpress
{
	[ProvideMenuResource("visualxpress_vsct", 1)]
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[ProvideAutoLoad(UIContextGuids.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideAutoLoad(UIContextGuids.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideAutoLoad(UIContextGuids.SolutionHasSingleProject, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideAutoLoad(UIContextGuids.SolutionHasMultipleProjects, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideOptionPage(typeof(UserDialogPage<UserOptionsControl>), "VisualXpress", "General", 0, 0, false)]
	[InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
	[Guid(GuidList.GuidVisualXpressPkgString)]
	public sealed class Package : AsyncPackage, IVsSolutionLoadManager, IOleCommandTarget, IVsPersistSolutionOpts, IDisposable
	{
		private const string SettingsFileName = "VisualXpressSettings.xml";
		private const string ExtensionGalleryProperty = "VisualXpressExtensionGallery";
		private const string ToolbarName = "VisualXpress";
		private const string KeyBindingName = "VisualXpress.KeyBinding";
		public const string SolutionOptionsKey = "VisualXpress.Solution";

		private Dictionary<string, string> m_Properties = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
		private List<CommandBarControl> m_Controls = new List<CommandBarControl>();
		private List<CommandBar> m_CommandBars = new List<CommandBar>();
		private List<KeyBindingInfo> m_KeyBindings = new List<KeyBindingInfo>();
		private List<IPluginService> m_PluginServices = new List<IPluginService>();
		private DocTableEvents m_DocTableEvents;

		protected override async Task InitializeAsync(System.Threading.CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			await base.InitializeAsync(cancellationToken, progress);
			Log.Intitialize();
			LoadUserOptions();
			
			Log.Verbose("VisualXpressPackage.Initialize: BEGIN");
			try
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
				DTE2 dte = this.GetServiceOfType<DTE2>(typeof(DTE));
				dte.Events.DTEEvents.OnStartupComplete += OnDTEEventsStartupComplete;

				IVsRunningDocumentTable vsDocTable = this.GetServiceOfType<IVsRunningDocumentTable>(typeof(IVsRunningDocumentTable));
				m_DocTableEvents = DocTableEvents.New(vsDocTable);

				IVsSolution vsSolution = this.GetServiceOfType<IVsSolution>(typeof(SVsSolution));
				vsSolution.SetProperty((int)__VSPROPID4.VSPROPID_ActiveSolutionLoadManager, this);
				
				LoadSolutionUserOptions();
				RefreshInternal();
			}
			catch (Exception e) 
			{
				Log.Error("VisualXpressPackage.Initialize: FAILED exception {0}\n{1}", e.Message, e.StackTrace);
			}
		}

		protected override int QueryClose(out bool canClose)
		{
			SaveUserOptions();
			return base.QueryClose(out canClose);
		}

		public void Refresh()
		{
			ThreadHelper.JoinableTaskFactory.Run(async delegate {
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
				RefreshInternal();
			});
		}

		private void RefreshInternal()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			for (int i = m_Controls.Count-1; i >= 0; --i)
			{
				CommandBarControl control = m_Controls[i];
				if (control != null)
					control.Delete(false);
			}

			for (int i = m_CommandBars.Count-1; i >= 0; --i)
			{
				CommandBar commandBar = m_CommandBars[i];
				if (commandBar != null)
					commandBar.Delete();
			}

			m_Controls.Clear();
			m_CommandBars.Clear();
			m_Properties.Clear();
			m_PluginServices.Clear();
			
			this.ResetShortcutKeyBindings();
			this.AddSettings(this.GlobalSettingsPath, this.UserSettingsPath);
			this.VerifyShortcutKeyBindings();
			this.VerifyExtensionGallery();
		}

		private void LoadUserOptions()
		{
			UserOptions.Instance.Load(this);
		}

		private void SaveUserOptions()
		{
			try
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				CommandBar commandBar = this.FindCommandBar(this.ActiveDTE2.CommandBars as CommandBars, Package.ToolbarName);
				if (commandBar != null)
					UserOptions.Instance.IsToolbarVisible = commandBar.Visible;		

				UserOptions.Instance.Save(this);
			}
			catch (Exception e)
			{
				Log.Error("VisualXpressPackage.SaveUserOptions failed with exception: {0}", e);
			}
		}

		private void OnDTEEventsStartupComplete()
		{
			Log.Verbose("VisualXpressPackage.OnDTEEventsStartupComplete: Begin");
			this.VerifyShortcutKeyBindings();
		}

		int IVsSolutionLoadManager.OnBeforeOpenProject(ref Guid guidProjectID, ref Guid guidProjectType, string pszFileName, IVsSolutionLoadManagerSupport pSLMgrSupport)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			string folder = "";
			EnvDTE.Solution solution = this.ActiveDTE2.Solution;
			if (solution != null && String.IsNullOrEmpty(solution.FullName) == false)
				folder = Path.GetDirectoryName(solution.FullName);
			else if (String.IsNullOrEmpty(pszFileName) == false)
				folder = Path.GetDirectoryName(pszFileName);

			Perforce.Process.GlobalConfig.ConfigDirectory = folder;
			return Microsoft.VisualStudio.VSConstants.S_OK;
		}

		int IVsSolutionLoadManager.OnDisconnect()
		{
			return Microsoft.VisualStudio.VSConstants.S_OK;
		}

		int IOleCommandTarget.Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
		{
			try
			{
				if (pguidCmdGroup == GuidList.GuidVisualXpressCmdSet)
				{
					Log.Verbose("IOleCommandTarget.Exec Command={0} Id={1}", pguidCmdGroup, nCmdID);
					if (nCmdID < m_KeyBindings.Count && m_KeyBindings[(int)nCmdID].Handler != null)
						m_KeyBindings[(int)nCmdID].Handler();
					return VSConstants.S_OK;
				}
			}
			catch (Exception e)
			{
				Log.Error("IOleCommandTarget.Exec failed: {0}", e.Message);
			}
			return VSConstants.E_FAIL;
		}

		int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
		{
			if (pguidCmdGroup == GuidList.GuidVisualXpressCmdSet && cCmds == 1)
			{
				prgCmds[0].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
				return VSConstants.S_OK;
			}
			return VSConstants.E_FAIL;
		}

        private void LoadSolutionUserOptions()
        {
            try
            {
				ThreadHelper.ThrowIfNotOnUIThread();
				IVsSolutionPersistence vsSolutionPersistence = GetServiceOfType<IVsSolutionPersistence>(typeof(SVsSolutionPersistence));
				IVsPersistSolutionOpts vsSolutionOps = ((object)this) as IVsPersistSolutionOpts;
				vsSolutionPersistence.LoadPackageUserOpts(vsSolutionOps, SolutionOptionsKey);
            }
            catch (Exception e)
            {
				Log.Error("LoadSolutionUserOptions failed: {0}", e.Message);
            }
        }

		int IVsPersistSolutionOpts.SaveUserOptions(IVsSolutionPersistence vsSolutionPersistence)
		{
			try
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				Log.Verbose("VsPersistSolutionOpts.SaveUserOptions");
				IVsPersistSolutionOpts vsSolutionOps = ((object)this) as IVsPersistSolutionOpts;
				vsSolutionPersistence.SavePackageUserOpts(vsSolutionOps, SolutionOptionsKey);
			}
			catch (Exception e)
			{
				Log.Error("IVsPersistSolutionOpts.SaveUserOptions failed: {0}", e.Message);
			}
			return VSConstants.S_OK;
		}

		int IVsPersistSolutionOpts.LoadUserOptions(IVsSolutionPersistence vsSolutionPersistence, uint grfLoadOpts)
		{
			try
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				Log.Verbose("VsPersistSolutionOpts.LoadUserOptions grfLoadOpts={0}", grfLoadOpts);
				IVsPersistSolutionOpts vsSolutionOps = ((object)this) as IVsPersistSolutionOpts;
				vsSolutionPersistence.LoadPackageUserOpts(vsSolutionOps, SolutionOptionsKey);
			}
			catch (Exception e)
			{
				Log.Error("IVsPersistSolutionOpts.LoadUserOptions failed: {0}", e.Message);
			}
			return VSConstants.S_OK;
		}

		int IVsPersistSolutionOpts.WriteUserOptions(IStream pOptionsStream, string pszKey)
		{
			if (pszKey == SolutionOptionsKey)
				SolutionOptions.Instance.WriteUserOptions(this, pOptionsStream);
			return VSConstants.S_OK;
		}

		int IVsPersistSolutionOpts.ReadUserOptions(IStream pOptionsStream, string pszKey)
		{
			if (pszKey == SolutionOptionsKey)
				SolutionOptions.Instance.ReadUserOptions(this, pOptionsStream);
			return VSConstants.S_OK;
		}

		private void AddSettings(params string[] fileNames)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			IList<Settings> settingsList = fileNames.Select(f => Settings.LoadFromFile(f)).Where(s => s != null).ToList();

			// Merge and expand all PropertyGroup's
			foreach (Settings settings in settingsList)
			{
				if (settings.PropertyGroup != null && settings.PropertyGroup.Properties != null)
				{
					foreach (var property in settings.PropertyGroup.Properties)
					{
						Dictionary<string,string> properties = new Dictionary<string,string>(StringComparer.CurrentCultureIgnoreCase);
						if (m_Properties.ContainsKey(property.Name))
							properties[property.Name] = m_Properties[property.Name];

						string text = this.ExpandText(Regex.Replace(property.Text, @"\s+", " "), properties:properties);
						m_Properties[property.Name] = text;
					}
				}
			}

			// Merge all MenuItemCommand's
			for (int settingsIndex = 1; settingsIndex < settingsList.Count; ++settingsIndex)
			{
				Settings dstSettings = settingsList[0];
				if (dstSettings.Menu == null)
					continue;

				Settings srcSettings = settingsList[settingsIndex];
				if (srcSettings.Menu == null)
					continue;
				if (String.IsNullOrEmpty(srcSettings.Menu.Title))
					srcSettings.Menu.Title = dstSettings.Menu.Title;

				MenuItemHandle[] dstItems = dstSettings.Menu.InnerItems;
				MenuItemHandle[] srcItems = srcSettings.Menu.InnerItems;
				for (int srcItemsIndex = 0; srcItemsIndex < srcItems.Length; ++srcItemsIndex)
				{
					MenuItemCommand srcItemCommand = srcItems[srcItemsIndex].Item as MenuItemCommand;
					if (srcItemCommand == null)
						continue;

					for (int dstItemsIndex = 0; dstItemsIndex < dstItems.Length; ++dstItemsIndex)
					{
						MenuItemCommand dstItemCommand = dstItems[dstItemsIndex].Item as MenuItemCommand;
						if (dstItemCommand == null || dstItemCommand.Title != srcItemCommand.Title)
							continue;

						if (srcItemCommand.GetType() != dstItemCommand.GetType())
						{
							dstItemCommand = dstItemCommand.Clone(srcItemCommand.GetType()) as MenuItemCommand;
							dstItems[dstItemsIndex].Item = dstItemCommand;
						}

						dstItemCommand.Merge(srcItemCommand);
						srcItemCommand.Enabled = PropertyBool.False;
					}
				}
			}

			// Add all Command's
			foreach (Settings settings in settingsList)
			{
				if (settings.Menu == null)
					continue;

				CommandBars commandBars = this.ActiveDTE2.CommandBars as CommandBars;
				CommandBar menuBar = commandBars["MenuBar"];
				int childIndex = 1;
				this.AddSettingsMenuItem(menuBar.Controls, settings.Menu, ref childIndex, false, ContextType.Document|ContextType.MainMenu);

				bool contextMenuBeginGroup = true;
				foreach (MenuItemHandle itemHandle in settings.Menu.InnerItems)
				{
					MenuItemCommand itemCommand = itemHandle.Item as MenuItemCommand;
					if (itemCommand == null)
						continue;

					if (itemCommand.ShowInContextMenus.ToBool(false))
					{
						this.AddContextMenuItem(itemCommand, contextMenuBeginGroup);
						contextMenuBeginGroup = false;
					}

					if (itemCommand.ShowInToolbar.ToBool(false))
					{
						this.AddToolbarItem(itemCommand);
					}
				}
			}
		}

		private void AddSettingsMenuItem(CommandBarControls parentControls, MenuItem item, ref int index, bool beginGroup, ContextType context)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (parentControls == null)
				return;
			if (item.Enabled.ToBool(true) == false)
				return;
			if (item.Hidden.ToBool(false))
				return;
			if (context.HasFlag(ContextType.ContextMenu) && item.ShowInContextMenus.ToBool(false) == false)
				return;
			if (context.HasFlag(ContextType.Toolbar) && item.ShowInToolbar.ToBool(false) == false)
				return;
			if (context.HasFlag(ContextType.MainMenu) && item.ShowInMainMenu.ToBool(true) == false)
				return;

			bool beginGroupAfter = false;
			if (String.IsNullOrEmpty(item.InsertBefore) == false || String.IsNullOrEmpty(item.InsertAfter) == false)
			{
				bool isInsertBefore = String.IsNullOrEmpty(item.InsertAfter);
				string insertLocation = isInsertBefore ? item.InsertBefore : item.InsertAfter;
				if (Regex.Match(insertLocation, @"^\d+$").Success)
				{
					index = Math.Max(0, Math.Min(Int32.Parse(insertLocation), parentControls.Count))+1;
				}
				else
				{
					bool indexFound = false;
					for (int i = parentControls.Count; i > 0; --i)
					{
						if (String.Compare(parentControls[i].Caption.Replace("&",""), insertLocation, StringComparison.InvariantCultureIgnoreCase) == 0)
						{
							index = i + 1;
							indexFound = true;
							break;
						}
					}
					if (indexFound == false)
						return;
				}

				if (isInsertBefore)
					index = Math.Max(1, index-1);

				CommandBarControl nextChildControl = index <= parentControls.Count ? parentControls[index] : null;
				if (nextChildControl != null && nextChildControl.BeginGroup)
				{
					nextChildControl.BeginGroup = false;
					if (isInsertBefore)
						beginGroup = true;
					else
						beginGroupAfter = true;
				}
			}

			string itemTitle = String.IsNullOrEmpty(item.Title) ? "Unknown" : item.Title;
			if (item is MenuItemSubMenu)
			{
				MenuItemSubMenu itemSubMenu = item as MenuItemSubMenu;
				CommandBarPopup itemPopup = this.FindCommandBarControl(parentControls, itemTitle) as CommandBarPopup;
				if (itemPopup == null)
				{
					itemPopup = this.AddCommandBarControl(parentControls, MsoControlType.msoControlPopup, Type.Missing, Type.Missing, index++) as CommandBarPopup;
					itemPopup.BeginGroup = beginGroup;

					if (beginGroupAfter)
					{
						CommandBarControl nextChildControl = index <= parentControls.Count ? parentControls[index] : null;
						if (nextChildControl != null)
							nextChildControl.BeginGroup = true;
					}
				}
				
				itemPopup.Caption = itemTitle;
				int childIndex = itemPopup.Controls.Count+1;
				for (int childItemIndex = 0; childItemIndex < itemSubMenu.Items.Length; ++childItemIndex)
				{
					bool childBeginGroup = (childItemIndex > 0 && itemSubMenu.Items[childItemIndex-1] is MenuItemSeparator ? true : false);
					this.AddSettingsMenuItem(itemPopup.Controls, itemSubMenu.Items[childItemIndex], ref childIndex, childBeginGroup, context);
				}
			}

			if (item is MenuItemPluginCommand)
			{
				MenuItemPluginCommand itemCommand = item as MenuItemPluginCommand;
				PluginCommand command = PluginCommand.Create(this, itemCommand.Name);
				if (command != null && command.InitializeCommandBar(parentControls, item, ref index, beginGroup))
					return;
			}

			if (item is MenuItemCommand)
			{
				MenuItemCommand itemCommand = item as MenuItemCommand;
				CommandBarButton itemButton = this.AddCommandBarControl(parentControls, MsoControlType.msoControlButton, Type.Missing, Type.Missing, index++) as CommandBarButton;
				itemButton.Caption = itemTitle;
				itemButton.BeginGroup = beginGroup;
				itemButton.Picture = Resource.LoadPicture(this.ExpandText(itemCommand.Image)) as stdole.StdPicture;
				itemButton.Tag = itemCommand.Tag;

				UpdateButtonStyle(itemButton);

				if (beginGroupAfter)
				{
					CommandBarControl nextChildControl = index <= parentControls.Count ? parentControls[index] : null;
					if (nextChildControl != null)
						nextChildControl.BeginGroup = true;
				}

				Action handler = null;
				if (itemCommand is MenuItemSystemCommand)
					handler = () => this.OnClickMenuItemSystemCommand(context, itemCommand as MenuItemSystemCommand);
				else if (itemCommand is MenuItemPluginCommand)
					handler = () => this.OnClickMenuItemPluginCommand(context, itemCommand as MenuItemPluginCommand);

				if (handler != null)
				{
					itemButton.Click += (CommandBarButton c, ref bool cd) => handler();
					this.AddShortcutKeyBinding(itemCommand.ShortcutKey, itemButton, handler);
				}
			}
		}

		public void UpdateButtonStyle(CommandBarButton item)
		{
			if (item == null)
			{
				return;
			}

			if (item.Picture != null)
			{
				if (item.Parent != null && item.Parent.Position == MsoBarPosition.msoBarTop)
					item.Style = MsoButtonStyle.msoButtonIcon;
				else
					item.Style = MsoButtonStyle.msoButtonIconAndCaption;
			}
			else
			{
				item.Style = MsoButtonStyle.msoButtonCaption;
			}
		}

		private void ResetShortcutKeyBindings()
		{
			try
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				m_KeyBindings.Clear();
				foreach (Command command in this.ActiveDTE2.Commands)
				{
					if (command != null && command.Name.StartsWith(Package.KeyBindingName))
					{
						Array bindings = command.Bindings as Array;
						if (bindings != null && bindings.Length > 0)
							command.Bindings = new object[0];
					}
				}
			}
			catch (Exception e)
			{
				Log.Error("VisualXpressPackage.ResetShortcutKeyBindings failed: {0}", e.Message);
			}
		}

		private void VerifyShortcutKeyBindings()
		{
			try
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				Commands commands = this.ActiveDTE2.Commands;
				foreach (KeyBindingInfo keyBinding in m_KeyBindings)
				{
					Command keyBindingCommand = commands.Item(keyBinding.CommandName);
					if (keyBindingCommand == null)
					{
						Log.Error("VisualXpressPackage.VerifyShortcutKeyBindings failed to find command '{0}' with key '{1}'", keyBinding.CommandName, keyBinding.ShortcutKey);
					}
					else if (VerifyShortcutKeyBinding(keyBindingCommand.Bindings as object[], keyBinding) == false)
					{
						keyBindingCommand.Bindings = new object[] { keyBinding.ShortcutKey };
					}
				}
			}
			catch (Exception e)
			{
				Log.Error("VisualXpressPackage.VerifyShortcutKeyBindings exception: {0}", e.Message);
			}
		}

		private bool VerifyShortcutKeyBinding(object[] bindings, KeyBindingInfo keyBinding)
		{
			if (bindings == null)
			{
				Log.Error("VisualXpressPackage.VerifyShortcutKeyBindings null Bindings for command '{0}' with key '{1}'", keyBinding.CommandName, keyBinding.ShortcutKey);
				return false;
			}

			if (bindings.Length != 1)
			{
				Log.Error("VisualXpressPackage.VerifyShortcutKeyBindings unexpected number of Bindings {2} for command '{0}' with key '{1}'", keyBinding.CommandName, keyBinding.ShortcutKey, bindings.Length);
				return false;
			}

			string shortcutKey = bindings[0] as string;
			if (String.IsNullOrEmpty(shortcutKey))
			{
				Log.Error("VisualXpressPackage.VerifyShortcutKeyBindings empty Binding for command '{0}' with key '{1}'", keyBinding.CommandName, keyBinding.ShortcutKey, bindings.Length);
				return false;
			}

			if (String.Compare(shortcutKey, keyBinding.ShortcutKey, StringComparison.InvariantCultureIgnoreCase) != 0 && String.Compare(shortcutKey, keyBinding.LocalizedShortcutKey, StringComparison.InvariantCultureIgnoreCase) != 0)
			{
				Log.Error("VisualXpressPackage.VerifyShortcutKeyBindings overriding conflicting Bindings for command '{0}' with key '{1}'", keyBinding.CommandName, keyBinding.ShortcutKey);
				return false;
			}

			return true;
		}

		private void AddShortcutKeyBinding(string shortcutKey, CommandBarButton button, Action handler)
		{
			try
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				if (String.IsNullOrEmpty(shortcutKey) == false && handler != null)
				{
					if (Regex.Match(shortcutKey, @"^.+::").Success == false)
						shortcutKey = String.Format("Global::{0}", shortcutKey);

					if (m_KeyBindings.Any(info => info.ShortcutKey == shortcutKey) == false)
					{
						Commands commands = this.ActiveDTE2.Commands;
						string keyBindingName = String.Format("{0}{1:D2}", Package.KeyBindingName, m_KeyBindings.Count);
						Command keyBindingCommand = commands.Item(keyBindingName);
						if (keyBindingCommand == null)
							throw new Exception(String.Format("Named command {0} does not exist to bind {1}", keyBindingName, shortcutKey));
					
						keyBindingCommand.Bindings = new object[]{ shortcutKey };
						object[] localizedBindings = keyBindingCommand.Bindings as object[];
						string localizedShortcutKey = localizedBindings != null && localizedBindings.Length == 1 ? localizedBindings[0] as string : null;

						m_KeyBindings.Add(new KeyBindingInfo{ ShortcutKey=shortcutKey, LocalizedShortcutKey=localizedShortcutKey, Handler=handler, CommandName=keyBindingName });
					}

					button.ShortcutText = Regex.Replace(shortcutKey, @"^.*::", "");
				}
			}
			catch (Exception e)
			{
				Log.Error("Failed to bind shortcut key {0}: {1}", shortcutKey, e.Message);
			}
		}

		private void VerifyExtensionGallery()
		{
			try
			{
				string galleryKeyName = String.Format("ExtensionManager\\Repositories\\{{{0}}}", GuidList.GuidVisualXpressExtensionGalleryString);
				string galleryFile = this.GetPropertyValue(Package.ExtensionGalleryProperty);
				if (String.IsNullOrEmpty(galleryFile))
				{
					Log.Verbose("VerifyExtensionGallery skipping unspecified gallery file {0}", Package.ExtensionGalleryProperty);	
					this.UserRegistryRoot.DeleteSubKeyTree(galleryKeyName, false);
					return;
				}

				RegistryKey galleryKey = this.UserRegistryRoot.CreateSubKey(galleryKeyName);
				if (galleryKey == null)
				{
					Log.Error("VerifyExtensionGallery failed to create extension gallery entry");
					return;
				}

				galleryKey.SetValue(null, galleryFile, RegistryValueKind.String);
				galleryKey.SetValue("Priority", 0x64, RegistryValueKind.DWord);
				galleryKey.SetValue("Protocol", "Atom Feed", RegistryValueKind.String);
				galleryKey.SetValue("DisplayName", "VisualXpress", RegistryValueKind.String);
			}
			catch (Exception e)
			{
				Log.Error("VerifyExtensionGallery failed with exception: {0}\n{1}", e.Message, e.StackTrace);
			}
		}

		private void AddContextMenuItem(MenuItemCommand itemCommand, bool beginGroup)
		{
			CommandBarInfo[] info = new CommandBarInfo[]{
				new CommandBarInfo { Name="Easy MDI Document Window", Context=ContextType.Document },
				new CommandBarInfo { Name="Item", Context=ContextType.Solution },
				new CommandBarInfo { Name="Project", Context=ContextType.Solution },
				new CommandBarInfo { Name="Solution", Context=ContextType.Solution },
				new CommandBarInfo { Name="Cross Project Multi Project", Context=ContextType.Solution },
				new CommandBarInfo { Name="Cross Project Solution Project", Context=ContextType.Solution },
				new CommandBarInfo { Name="Cross Project Project Item", Context=ContextType.Solution },
				new CommandBarInfo { Name="Code Window", Context=ContextType.Document }
			};

			CommandBars commandBars = this.ActiveDTE2.CommandBars as CommandBars;
			foreach (var i in info)
			{
				try
				{
					CommandBar cb = this.FindCommandBar(commandBars, i.Name);
					if (cb != null)
					{
						int index = cb.Controls.Count+1;
						this.AddSettingsMenuItem(cb.Controls, itemCommand, ref index, beginGroup, i.Context|ContextType.ContextMenu);
					}
				}
				catch (Exception e)
				{
					Log.Error("VisualXpressPackage.AddContextMenuItem: Unhandled exception adding to '{0}': {1}", i.Name, e.Message);
				}
			}
		}

		private void AddToolbarItem(MenuItemCommand itemCommand, CommandBar commandBar = null)
		{
			try
			{
				if (commandBar == null)
				{
					commandBar = this.FindAddCommandBar(this.ActiveDTE2.CommandBars as CommandBars, Package.ToolbarName, MsoBarPosition.msoBarTop);
					if (commandBar == null)
					{
						Log.Error("VisualXpressPackage.AddToolbarItem: Failed to add default toolbar: {0}", Package.ToolbarName);
						return;
					}
				}

				int index = commandBar.Controls.Count+1;
				this.AddSettingsMenuItem(commandBar.Controls, itemCommand, ref index, false, ContextType.Document|ContextType.Toolbar);
			}
			catch (Exception e)
			{
				Log.Error("VisualXpressPackage.AddToolbarItem: Unhandled exception adding to '{0}': {1}", itemCommand.Title, e.Message);
			}
		}

		public CommandBarControl AddCommandBarControl(CommandBarControls parentControls, MsoControlType type, object id = null, object parameter = null, object before = null)
		{
			CommandBarControl control = parentControls.Add(type, id??Type.Missing, parameter??Type.Missing, before??Type.Missing, true);
			m_Controls.Add(control);
			return control;
		}

		public CommandBarControl FindCommandBarControl(CommandBarControls controls, string name)
		{
			foreach (CommandBarControl control in controls)
			{
				if (control.Caption.Replace("&", "") == name)
					return control;
			}
			return null;
		}

		public CommandBarControl FindCommandBarControl(string tag)
		{
			return this.FindCommandBarControl(control => control.Tag == tag);
		}

		public CommandBarControl FindCommandBarControl(Func<CommandBarControl, bool> predicate)
		{
			return m_Controls.FirstOrDefault(predicate);
		}

		public bool IsExistingCommandBarControl(CommandBarControl control)
		{
			return control != null && m_Controls.Contains(control);
		}

		public void RemoveCommandBarControl(CommandBarControl control)
		{
			if (control != null && m_Controls.Remove(control))
				control.Delete(false);
		}

		public CommandBar FindAddCommandBar(CommandBars commandBars, string name, MsoBarPosition position)
		{
			if (commandBars == null || String.IsNullOrEmpty(name))
				return null;
			CommandBar commandBar = this.FindCommandBar(commandBars, name);
			if (commandBar == null)
			{
				commandBar = commandBars.Add(name, position, System.Type.Missing, true);
				commandBar.RowIndex = 1;
				m_CommandBars.Add(commandBar);				
			}
			commandBar.Visible = UserOptions.Instance.IsToolbarVisible;
			return commandBar;
		}

		public CommandBar FindCommandBar(CommandBars commandBars, string name)
		{
			if (commandBars == null || String.IsNullOrEmpty(name))
				return null;
			foreach (CommandBar cb in commandBars)
			{
				if (cb.NameLocal == name)
					return cb;
			}
			return null;			
		}

		public void RegisterPluginService(IPluginService service)
		{
			m_PluginServices.Add(service);
		}

		public bool UnregisterPluginService(IPluginService service)
		{
			return m_PluginServices.Remove(service);
		}

		public IReadOnlyList<IPluginService> PluginServices
		{
			get { return m_PluginServices; }
		}

		private void AddDebugContextMenuItems()
		{
			CommandBars commandBars = this.ActiveDTE2.CommandBars as CommandBars;
			foreach (CommandBar cc in commandBars)
			{
				try
				{
					if (cc != null && cc.Controls != null && cc.Index >= 1 && cc.Index <= 456)
					{
						CommandBarButton itemButton = this.AddCommandBarControl(cc.Controls, MsoControlType.msoControlButton) as CommandBarButton;
						if (itemButton != null)
							itemButton.Caption = "VX_DEBUG: " + cc.NameLocal;
					}
				}
				catch {}
			}
		}

		private void OnClickMenuItemSystemCommand(ContextType context, MenuItemSystemCommand command)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (command.SaveAllDocs.ToBool(false))
				this.SaveAllDocuments();

			IEnumerable<ContextItem> selection = this.GetSelectedItems(context);
			foreach (ContextItem item in selection)
			{
				try
				{
					Log.Info("VisualXpressPackage.OnClickMenuItemCommand: Item [{0}] Executing [{1}]", item, command.Title);
					string cmdFileName = this.ExpandText(command.FileName, item);
					string cmdArguments = this.ExpandText(command.Arguments, item);
					string cmdDirectory = this.ExpandText(command.InitialDirectory, item);
					Dictionary<string, string> cmdEnv = Perforce.Process.GetPerforceEnvironmentVariables();

					DataReceivedEventHandler outputReceiver = null;
					if (command.UseOutputWindow.ToBool(false))
					{
						outputReceiver = Log.ReceivedEventHandler(LogChannel.Info);
					}
					else if (command.CloseOnExit.ToBool(true) == false)
					{
						cmdArguments = String.Format("/s /c \"\"{0}\" {1} & pause\"", cmdFileName, cmdArguments);
						cmdFileName = "cmd.exe";
					}

					Log.Info("{0} {1}", cmdFileName, cmdArguments);
					this.ExecuteAction(() => Utilities.ExecuteWait(cmdFileName, cmdArguments, outputReceiver, cmdDirectory, environment:cmdEnv), command.WaitForExit.ToBool(false));
				}
				catch (Exception e)
				{
					Log.Error("VisualXpressPackage.OnClickMenuItemSystemCommand: Unhandled exception {0}", e.Message);
				}
			}
		}

		private void OnClickMenuItemPluginCommand(ContextType context, MenuItemPluginCommand command)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (command.SaveAllDocs.ToBool(false))
				this.SaveAllDocuments();

			IEnumerable<ContextItem> selection = this.GetSelectedItems(context);
			foreach (ContextItem item in selection)
			{
				try
				{
					Log.Info("VisualXpressPackage.OnClickMenuItemCommand: Item [{0}] Executing [{1}]", item, command.Title);
					string cmdArguments = this.ExpandText(command.Arguments, item);
					string[] args = Utilities.CommandLineToArgs(cmdArguments);

					Log.Info("VisualXpress {0} {1}", command.Name, (args.Length > 0 ? String.Format("\"{0}\"", String.Join("\" \"", args)) : ""));
					this.ExecuteAction(() => PluginCommandManager.Execute(this, command.Name, args), command.WaitForExit.ToBool(false));
				}
				catch (Exception e)
				{
					Log.Error("VisualXpressPackage.OnClickMenuItemSystemCommand: Unhandled exception {0}", e.Message);
				}
			}
		}

		private void ExecuteAction(Action action, bool wait)
		{
			if (wait)
				action();
			else
				Task.Run(action);
		}

		public string ExpandText(string text, ContextItem item = default(ContextItem), HashSet<string> visited = null, Dictionary<string,string> properties = null)
		{
			return Regex.Replace(text??"", @"\$\((.+?)\)", (Match m) => {
				string name = m.Groups[1].Value;
				string result = "";
				if (properties != null)
				{
					string value = "";
					result = properties.TryGetValue(name, out value) ? value : String.Format("$({0})", name);
				}
				else
				{
					result = this.ExpandProperty(name, item, visited); 
				}
				return result == null ? "" : result.Trim();
			}).Trim();
		}

		private string ExpandProperty(string name, ContextItem item = default(ContextItem), HashSet<string> visited = null)
		{
			try
			{
				if (String.IsNullOrEmpty(name))
					return "";
			
				visited = new HashSet<string>(visited ?? new HashSet<string>(), StringComparer.CurrentCultureIgnoreCase);
				if (visited.Contains(name))
					return "";
			
				visited.Add(name);

				if (String.IsNullOrEmpty(item.Path) == false)
				{
					if (String.Compare(name, Properties.ItemPath, true) == 0)
						return item.Path;
					if (String.Compare(name, Properties.ItemPaths, true) == 0)
						return String.Join(" ", this.GetActiveItemPaths(item.Context).Select((s) => String.Format("\"{0}\"", s)));
					if (String.Compare(name, Properties.ItemFiles, true) == 0)
						return String.Join(" ", this.GetActiveItemFiles(item.Context).Select((s) => String.Format("\"{0}\"", s)));
					if (String.Compare(name, Properties.ItemFolders, true) == 0)
						return String.Join(" ", this.GetActiveItemFolders(item.Context).Select((s) => String.Format("\"{0}\"", s)));
					if (String.Compare(name, Properties.ItemDir, true) == 0)
						return Utilities.GetDirectoryName(item.Path);
					if (String.Compare(name, Properties.ItemFileName, true) == 0)
						return Utilities.GetFileName(item.Path);
					if (String.Compare(name, Properties.ItemFileNameNoExt, true) == 0)
						return Utilities.GetFileNameWithoutExtension(item.Path);
					if (String.Compare(name, Properties.ItemExt, true) == 0)
						return Utilities.GetFileExtension(item.Path);
				}

				if (String.Compare(name, Properties.ProjectDir, true) == 0)
					return Utilities.GetDirectoryName(this.ActiveProjectPath);
				if (String.Compare(name, Properties.ProjectFileName, true) == 0)
					return Utilities.GetFileName(this.ActiveProjectPath);
				if (String.Compare(name, Properties.ProjectPath, true) == 0)
					return this.ActiveProjectPath;
				if (String.Compare(name, Properties.ProjectTitle, true) == 0)
					return Utilities.GetFileNameWithoutExtension(this.ActiveProjectPath);
				if (String.Compare(name, Properties.ProjectFolders, true) == 0)
					return String.Join(" ", this.GetProjectFolders().Select((s) => String.Format("\"{0}\"", s)));
				if (String.Compare(name, Properties.ProjectFiles, true) == 0)
					return String.Join(" ", this.GetProjectFiles().Select((s) => String.Format("\"{0}\"", s)));

				if (String.Compare(name, Properties.SolutionDir, true) == 0)
					return Utilities.GetDirectoryName(this.ActiveSolutionPath);
				if (String.Compare(name, Properties.SolutionFileName, true) == 0)
					return Utilities.GetFileName(this.ActiveSolutionPath);
				if (String.Compare(name, Properties.SolutionPath, true) == 0)
					return this.ActiveSolutionPath;
				if (String.Compare(name, Properties.SolutionTitle, true) == 0)
					return Utilities.GetFileNameWithoutExtension(this.ActiveSolutionPath);

				if (String.Compare(name, Properties.UserSettingsPath, true) == 0)
					return this.UserSettingsPath;
				if (String.Compare(name, Properties.GlobalSettingsPath, true) == 0)
					return this.GlobalSettingsPath;

				if (String.Compare(name, Properties.P4USER, true) == 0)
					return Perforce.Process.GetPerforceEnvironmentVariable("P4USER", Perforce.Process.GlobalDirectoryConfig(item.Path));
				if (String.Compare(name, Properties.P4HOST, true) == 0)
					return Perforce.Process.GetPerforceEnvironmentVariable("P4HOST", Perforce.Process.GlobalDirectoryConfig(item.Path));
				if (String.Compare(name, Properties.P4PORT, true) == 0)
					return Perforce.Process.GetPerforceEnvironmentVariable("P4PORT", Perforce.Process.GlobalDirectoryConfig(item.Path));
				if (String.Compare(name, Properties.P4CLIENT, true) == 0)
					return Perforce.Process.GetPerforceEnvironmentVariable("P4CLIENT", Perforce.Process.GlobalDirectoryConfig(item.Path));

				if (String.Compare(name, Properties.VisualStudioDevEnvPath, true) == 0)
					return this.ActiveDTE2.FullName;
				if (String.Compare(name, Properties.VisualStudioDevEnvFolder, true) == 0)
					return Utilities.GetDirectoryName(this.ActiveDTE2.FullName);
				if (String.Compare(name, Properties.VisualStudioInstallFolder, true) == 0)
					return Utilities.GetFullPath(String.Format("{0}\\..\\..", Utilities.GetDirectoryName(this.ActiveDTE2.FullName)));
				if (String.Compare(name, Properties.VisualStudioEditionYear, true) == 0)
					return Regex.Match(this.GetVsShellProperty<string>((int)__VSSPROPID5.VSSPROPID_SKUInfo) ?? "", @"\d{4}")?.Value;

				if (String.Compare(name, Properties.ContextDir, true) == 0)
					return this.ExpandProperty(this.GetPropertyContextDir(item.Context), item, visited);
				if (String.Compare(name, Properties.ContextFileName, true) == 0)
					return this.ExpandProperty(this.GetPropertyContextFileName(item.Context), item, visited);
				if (String.Compare(name, Properties.ContextPath, true) == 0)
					return this.ExpandProperty(this.GetPropertyContextPath(item.Context), item, visited);
				if (String.Compare(name, Properties.ContextTitle, true) == 0)
					return this.ExpandProperty(this.GetPropertyContextTitle(item.Context), item, visited);
				if (String.Compare(name, Properties.ContextFolders, true) == 0)
					return this.ExpandProperty(this.GetPropertyContextFolders(item.Context), item, visited);
				if (String.Compare(name, Properties.ContextFiles, true) == 0)
					return this.ExpandProperty(this.GetPropertyContextFiles(item.Context), item, visited);

				string value;
				if (m_Properties.TryGetValue(name, out value))
					return this.ExpandText(value ?? "", item, visited);
			
				value = System.Environment.GetEnvironmentVariable(name);
				if (value != null)
					return value;
			}
			catch (Exception e)
			{
				Log.Verbose("ExpandProperty exception: {0}", e); 
			}
			return "";
		}

		public string GetActiveItemPath(ContextType contextType)
		{
			try
			{
				ThreadHelper.ThrowIfNotOnUIThread();

				// No active items if IDE is not created yet
				DTE2 dte = this.ActiveDTE2;
				if (dte.Windows.Count == 0)
					return "";

				// First return result from selection in active window (text editor, solution explorer, etc)
				if (dte.ActiveWindow != null)
				{
					if (contextType.HasFlag(ContextType.Document))
					{
						if (dte.ActiveWindow.Document != null && String.IsNullOrEmpty(dte.ActiveWindow.Document.FullName) == false)
							return dte.ActiveWindow.Document.FullName;
					}

					if (contextType.HasFlag(ContextType.Project) || contextType.HasFlag(ContextType.Solution))
					{
						EnvDTE.UIHierarchy ui = dte.ActiveWindow.Object as EnvDTE.UIHierarchy;
						if (ui != null && ui.SelectedItems is Array)
						{
							foreach (UIHierarchyItem item in ((Array)ui.SelectedItems).OfType<UIHierarchyItem>().Where(i => i.IsSelected && i.Object != null))
							{
								if (contextType.HasFlag(ContextType.Project) && item.Object is EnvDTE.ProjectItem)
								{
									EnvDTE.ProjectItem projectItem = (EnvDTE.ProjectItem)item.Object;
									if (projectItem.FileCount > 0 && String.IsNullOrEmpty(projectItem.get_FileNames(0)) == false)
										return projectItem.get_FileNames(0);
								}
								else if (contextType.HasFlag(ContextType.Project) && item.Object is EnvDTE.Project)
								{
									EnvDTE.Project project = (EnvDTE.Project)item.Object;
									if (String.IsNullOrEmpty(project.FullName) == false)
										return project.FullName;
								}
								else if (contextType.HasFlag(ContextType.Solution) && item.Object is EnvDTE.Solution)
								{
									EnvDTE.Solution solution = (EnvDTE.Solution)item.Object;
									if (String.IsNullOrEmpty(solution.FullName) == false)
										return solution.FullName;
								}
							}
						}
					}
				}

				// Return selected or active results not in focus
				if (contextType.HasFlag(ContextType.Project))
				{
					foreach (SelectedItem item in dte.SelectedItems)
					{
						if (item != null && item.Project != null && String.IsNullOrEmpty(item.Project.FullName) == false)
							return item.Project.FullName;
					}

					Array activeProjects = dte.ActiveSolutionProjects as Array;
					if (activeProjects != null)
					{
						foreach (var item in activeProjects)
						{
							if (item is EnvDTE.ProjectItem)
							{
								EnvDTE.ProjectItem projectItem = (EnvDTE.ProjectItem)item;
								if (projectItem.FileCount > 0 && String.IsNullOrEmpty(projectItem.get_FileNames(0)) == false)
									return projectItem.get_FileNames(0);
							}
							else if (item is EnvDTE.Project)
							{
								EnvDTE.Project project = (EnvDTE.Project)item;
								if (String.IsNullOrEmpty(project.FullName) == false)
									return project.FullName;
							}
						}
					}
				}

				// Return the current document being edited
				if (contextType.HasFlag(ContextType.Document))
				{
					if (dte.ActiveDocument != null && String.IsNullOrEmpty(dte.ActiveDocument.FullName) == false)
						return dte.ActiveDocument.FullName;
				}

				// Return the open solution
				if (contextType.HasFlag(ContextType.Solution))
				{
					if (dte.Solution != null && String.IsNullOrEmpty(dte.Solution.FullName) == false)
						return dte.Solution.FullName;
				}
			}
			catch (Exception e)
			{
				Log.Error("Failed GetActiveItemPath for contextType [{0}] {1}", contextType, e.Message);
			}
			return "";
		}

		public bool SaveAllDocuments()
		{
			try
			{
				this.ActiveDTE2.Documents.SaveAll();
			}
			catch (Exception e)
			{
				Log.Error("Unable to Save All Documents: [{0}]", e.Message);
				return false;
			}
			return true;
		}

		public TService GetServiceOfType<TService>(Type serviceType) where TService : class
		{
			return base.GetServiceAsync(serviceType)?.Result as TService;
		}

		public DTE2 ActiveDTE2
		{
			get { return GetServiceOfType<DTE2>(typeof(DTE)); }
		}

		public string ActiveItemPath
		{
			get { return this.GetActiveItemPath(ContextType.Document|ContextType.Project|ContextType.Solution); }
		}

		public string ActiveProjectPath
		{
			get { return this.GetActiveItemPath(ContextType.Project); }
		}

		public string ActiveSolutionPath
		{
			get { return this.GetActiveItemPath(ContextType.Solution); }
		}

		public IEnumerable<string> ActiveModifiedItemPaths
		{
			get { return this.ActiveDTE2.Documents.OfType<EnvDTE.Document>().Where(d => d.Saved == false).Select(d => d.FullName); }
		}

		public IEnumerable<ContextItem> GetSelectedItems(ContextType context)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			List<ContextItem> selection = new List<ContextItem>();
			if (context.HasFlag(ContextType.Document))
			{
				selection.Add(new ContextItem(){ Context = context, Path = this.ActiveItemPath });
			}
			if (context.HasFlag(ContextType.Project) || context.HasFlag(ContextType.Solution))
			{
				SelectedItems items = this.ActiveDTE2.SelectedItems;
				foreach (SelectedItem item in items)
				{
					if (item.Project != null)
					{
						selection.Add(new ContextItem(){ Context = context, Path = item.Project.FullName });
					}
					else if (item.ProjectItem != null)
					{
						for (int fileIndex = 1; fileIndex <= item.ProjectItem.FileCount; ++fileIndex)
							selection.Add(new ContextItem(){ Context = context, Path = item.ProjectItem.get_FileNames((short)fileIndex) });
					}
				}
				if (items.SelectionContainer != null && selection.Count == 0)
				{
					foreach (var item in items.SelectionContainer)
					{
						try { selection.Add(new ContextItem(){ Context = context, Path = ((dynamic)item).Path }); }
						catch {}
					}
				}
			}
			return selection;
		}

		public string UserSettingsPath
		{
			get { return String.Format("{0}\\{1}", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), SettingsFileName); }
		}

		public string GlobalSettingsPath
		{
			get { return String.Format("{0}\\{1}", Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), SettingsFileName); }
		}

		public void FindProjectFiles(EnvDTE.ProjectItems projectItems, SortedSet<string> result)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (projectItems == null)
				return;
			foreach (EnvDTE.ProjectItem item in projectItems)
			{
				if (item == null)
					continue;
				for (int fileIndex = 1; fileIndex <= item.FileCount; ++fileIndex)
				{
					string itemFileName = item.get_FileNames((short)fileIndex);
					if (String.IsNullOrEmpty(itemFileName) == false && Path.IsPathRooted(itemFileName))
						result.Add(itemFileName);
				}
				this.FindProjectFiles(item.ProjectItems, result);
				this.FindProjectFiles(item.SubProject, result);
			}
		}

		public void FindProjectFiles(EnvDTE.Project project, SortedSet<string> result)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (project == null)
				return;
			string projectKind = project.Kind;
			if (projectKind == EnvDTE.Constants.vsProjectKindMisc)
				return;
			string projectFullName = project.FullName;
			if (String.IsNullOrEmpty(projectFullName) == false)
				result.Add(projectFullName);
			this.FindProjectFiles(project.ProjectItems, result);
		}

		public SortedSet<string> FindProjectFiles()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			SortedSet<string> result = new SortedSet<string>();
			EnvDTE.Solution solution = this.ActiveDTE2.Solution;
			if (solution != null && solution.IsOpen)
			{
				string solutionFullName = solution.FullName;
				if (String.IsNullOrEmpty(solutionFullName) == false)
					result.Add(solutionFullName);
				foreach (EnvDTE.Project project in solution.Projects)
				{
					try { this.FindProjectFiles(project, result); }
					catch {}
				}
			}
			return result;
		}

		public IEnumerable<string> GetRootFolders(IEnumerable<string> paths)
		{
			SortedSet<string> folders = new SortedSet<string>(Comparer<string>.Create((a, b) => String.Compare(a, b, StringComparison.CurrentCultureIgnoreCase)));
			foreach (string path in paths)
			{
				if (File.Exists(path))
					folders.Add(Path.GetDirectoryName(Path.GetFullPath(path)));
				else if (Directory.Exists(path))
					folders.Add(Path.GetFullPath(path).TrimEnd('\\'));
			}

			// folders is sorted by shortest path first, so we can easily disregard sub-folders
			Stack<string> result = new Stack<string>();
			foreach (string path in folders)
			{
				if (result.Count == 0 || String.Format("{0}\\", path).StartsWith(String.Format("{0}\\", result.Peek()), StringComparison.CurrentCultureIgnoreCase) == false)
					result.Push(path);
			}
			return result;
		}

		public IEnumerable<string> GetProjectFolders()
		{
			// Include all of the root folders referenced by the solution
			return this.GetRootFolders(this.FindProjectFiles());
		}

		public IEnumerable<string> GetProjectFiles()
		{
			// Include all of the files referenced by the solution
			return this.FindProjectFiles().Where(path => File.Exists(path)).Select(file => Path.GetFullPath(file));
		}

		public IEnumerable<string> GetActiveItemPaths(ContextType context)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Include the active document if needed
			SortedSet<string> selection = new SortedSet<string>(Comparer<string>.Create((a, b) => String.Compare(a, b, StringComparison.CurrentCultureIgnoreCase)));
			if (context.HasFlag(ContextType.Document))
			{
				selection.Add(this.ActiveItemPath);
			}

			// Include all of the files referenced by selected project or solution
			if (context.HasFlag(ContextType.Project) || context.HasFlag(ContextType.Solution))
			{
				DTE2 dte = this.ActiveDTE2;
				for (int selectedItemIndex = 1; selectedItemIndex <= dte.SelectedItems.Count; ++selectedItemIndex)
				{
					SelectedItem item = dte.SelectedItems.Item(selectedItemIndex);
					if (item != null)
					{
						if (item.Project != null)
						{
							selection.Add(item.Project.FullName);
							try { this.FindProjectFiles(item.Project, selection); }
							catch {}
						}
						else if (item.ProjectItem != null && item.ProjectItem.ProjectItems.Count == 0)
						{
							for (int fileIndex = 1; fileIndex <= item.ProjectItem.FileCount; ++fileIndex)
								selection.Add(item.ProjectItem.get_FileNames((short)fileIndex));
						}
					}
				}
			}
			return selection;
		}

		public IEnumerable<string> GetActiveItemFolders(ContextType context)
		{
			// Include all of the active root folders
			return this.GetRootFolders(this.GetActiveItemPaths(context));
		}

		public IEnumerable<string> GetActiveItemFiles(ContextType context)
		{
			// Include all of the active files
			return this.GetActiveItemPaths(context).Where(path => File.Exists(path)).Select(file => Path.GetFullPath(file));
		}

		public ValueType GetVsShellProperty<ValueType>(int vsPropId) where ValueType : class
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			IVsShell vsShell = this.GetServiceOfType<IVsShell>(typeof(SVsShell));
			if (vsShell.GetProperty(vsPropId, out object result) == VSConstants.S_OK)
				return result as ValueType;
			return null;
		}

		public string[] GetPropertyNames()
		{
			return m_Properties.Keys.ToArray();
		}

		public string GetPropertyValue(string name)
		{
			string value;
			if (m_Properties.TryGetValue(name, out value))
				return value;
			return "";
		}

		public string GetPropertyContextDir(ContextType contextType)
		{
			if (contextType.HasFlag(ContextType.Solution))
				return Properties.SolutionDir;
			if (contextType.HasFlag(ContextType.Project))
				return Properties.ProjectDir;
			return Properties.ItemDir;
		}

		public string GetPropertyContextFileName(ContextType contextType)
		{
			if (contextType.HasFlag(ContextType.Solution))
				return Properties.SolutionFileName;
			if (contextType.HasFlag(ContextType.Project))
				return Properties.ProjectFileName;
			return Properties.ItemFileName;
		}

		public string GetPropertyContextPath(ContextType contextType)
		{
			if (contextType.HasFlag(ContextType.Solution))
				return Properties.SolutionPath;
			if (contextType.HasFlag(ContextType.Project))
				return Properties.ProjectPath;
			return Properties.ItemPath;
		}

		public string GetPropertyContextTitle(ContextType contextType)
		{
			if (contextType.HasFlag(ContextType.Solution))
				return Properties.SolutionTitle;
			if (contextType.HasFlag(ContextType.Project))
				return Properties.ProjectTitle;
			return Properties.ItemFileNameNoExt;
		}

		public string GetPropertyContextFolders(ContextType contextType)
		{
			if (contextType.HasFlag(ContextType.Project) || contextType.HasFlag(ContextType.Solution))
				return Properties.ProjectFolders;
			return Properties.ItemFolders;
		}

		public string GetPropertyContextFiles(ContextType contextType)
		{
			if (contextType.HasFlag(ContextType.Project) || contextType.HasFlag(ContextType.Solution))
				return Properties.ProjectFolders;
			return Properties.ItemFolders;
		}

		[Flags]
		public enum ContextType
		{
			None		 = 0,
			Document	 = (1<<0),
			Solution	 = (1<<1),
			Project		 = (1<<2),
			Toolbar		 = (1<<3),
			ContextMenu	 = (1<<4),
			MainMenu	 = (1<<5),
		}

		public struct ContextItem
		{
			public string Path;
			public ContextType Context;

			public override string ToString()
			{
				return String.Format("Path=\"{0}\",Context={1}", Path, Context);
			}
		}

		public struct CommandBarInfo
		{
			public string Name;
			public ContextType Context;

			public override string ToString()
			{
				return String.Format("Name=\"{0}\",Context={1}", Name, Context);
			}
		}

		public struct KeyBindingInfo
		{
			public string ShortcutKey;
			public string LocalizedShortcutKey;
			public Action Handler;
			public string CommandName;

			public override string ToString()
			{
				return String.Format("ShortcutKey=\"{0}\",ShortcutKey=\"{1}\",Handler={2},CommandName={3}", ShortcutKey, LocalizedShortcutKey, Handler, CommandName);
			}
		}

		public void Dispose()
		{
			m_DocTableEvents?.Dispose();
		}
	}
}

