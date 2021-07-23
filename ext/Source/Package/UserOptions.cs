// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Win32;

namespace Microsoft.VisualXpress
{
	public sealed class UserOptions
	{
		private static UserOptions m_Instance = new UserOptions();
		private const string UserOptionsSubKeyName = "VisualXpress";
		private Package m_Package = null;

		private UserOptions()
		{
			this.ActivateOutputWindow = true;
			this.VerboseLogging = false;
			this.UseSolutionSymbolSettings = false;
			this.IsToolbarVisible = true;
		}

		public static UserOptions Instance
		{
			get { return m_Instance; }
		}

		[SerializableUserOption]
		public bool ActivateOutputWindow
		{
			get; set;
		}

		[SerializableUserOption]
		public bool VerboseLogging
		{
			get; set;
		}

		[SerializableUserOption]
		public bool UseSolutionSymbolSettings
		{
			get; set;
		}

        [SerializableUserOption]
        public bool AutoCheckoutOnSave
        {
            get; set;
        }

		[SerializableUserOption]
        public bool IsToolbarVisible
        {
            get; set;
        }

        public Perforce.Config CurrentConfig
		{
			get
			{
				return Perforce.Process.GlobalConfig.Clone();
			}
			set
			{
				Perforce.Process.GlobalConfig.ApplyConnection(value);
				if (m_Package != null)
				{
					foreach (IPluginServiceConnection service in m_Package.PluginServices.OfType<IPluginServiceConnection>())
						service.OnConnectionChanged();
				}
			}
		}

		public bool Load(Package package)
		{
			m_Package = package;
			return Serialize((property, key, name) => {
				object value = key.GetValue(name);
				if (value != null)
				{	
					if (property.PropertyType == typeof(bool) && value is int)
						property.SetValue(this, ((int)value) != 0);
				}
			});
		}

		public bool Save(Package package)
		{
			m_Package = package;
			return Serialize((property, key, name) => {
				if (property.PropertyType == typeof(bool))
					key.SetValue(name, ((bool)property.GetValue(this)) ? 1 : 0, RegistryValueKind.DWord);
			});
		}

		private bool Serialize(Action<PropertyInfo, RegistryKey, string> serializeProperty)
		{
			try
			{
				using (RegistryKey rootKey = m_Package.UserRegistryRoot)
				{
					using (RegistryKey baseKey = rootKey.CreateSubKey(UserOptionsSubKeyName))
					{
						foreach (PropertyInfo property in this.GetType().GetProperties(BindingFlags.GetProperty|BindingFlags.SetProperty|BindingFlags.Instance|BindingFlags.Public))
						{
							if (property.GetCustomAttribute<SerializableUserOptionAttribute>() != null)
								serializeProperty(property, baseKey, String.Format("{0}.{1}", this.GetType().Name, property.Name));
						}
					}
				}
			}
			catch (Exception e) 
			{
				Log.Error("UserOptions.Serialize failed with exception: {0}", e.Message);
				return false;
			}
			return true;
		}
	}

	public class SerializableUserOptionAttribute : Attribute
	{
	};
}

