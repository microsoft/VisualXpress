// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.XPath;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using EnvDTE;
using EnvDTE80;

namespace Microsoft.VisualXpress
{
	public class SettingsManager
	{
		private ProcessSettingsStore m_ProcessStore;

		public ValueType GetValue<ValueType>(string name, ValueType defaultValue = default(ValueType), SettingsType stype = SettingsType.User)
		{
			IVsSettingsStore store = GetReadOnlySettingsStore(stype);
			if (store == null)
				return defaultValue;

			SettingsStorePath path = new SettingsStorePath(name);
			if (String.IsNullOrEmpty(path.Property))
				return defaultValue;

			uint propertyType = 0;
			if (store.GetPropertyType(path.Collection, path.Property, out propertyType) != VSConstants.S_OK)
				return defaultValue;

			switch (propertyType)
			{
				case (int)VisualStudio.Settings.SettingsType.Int32:
				{
					int value = 0;
					if (store.GetInt(path.Collection, path.Property, out value) == VSConstants.S_OK)
						return Converters.ParseType<ValueType>(value, defaultValue);
					break;
				}
				case (int)VisualStudio.Settings.SettingsType.String:
				{
					string value = null;
					if (store.GetString(path.Collection, path.Property, out value) == VSConstants.S_OK)
						return Converters.ParseType<ValueType>(value, defaultValue);
					break;
				}
			}
			return defaultValue;
		}

		public bool SetValue<ValueType>(string name, ValueType value, SettingsType stype = SettingsType.User)
		{
			IVsWritableSettingsStore store = GetWritableSettingsStore(stype);
			if (store == null)
				return false;

			SettingsStorePath path = new SettingsStorePath(name);
			if (String.IsNullOrEmpty(path.Property))
				return false;

			uint propertyType = 0;
			if (store.GetPropertyType(path.Collection, path.Property, out propertyType) != VSConstants.S_OK)
				return false;

			switch (propertyType)
			{
				case (int)VisualStudio.Settings.SettingsType.Int32:
				{
					int storeValue = 0;
					return Int32.TryParse(Converters.ToString(value), out storeValue) && store.SetInt(path.Collection, path.Property, storeValue) == VSConstants.S_OK;
				}
				case (int)VisualStudio.Settings.SettingsType.String:
				{
					string storeValue = Converters.ToString(value);
					return storeValue != null && store.SetString(path.Collection, path.Property, storeValue) == VSConstants.S_OK;
				}
			}
			return false;
		}

		public string[] GetSubCollectionNames(string collection)
		{
			IVsSettingsStore store = GetReadOnlySettingsStore(SettingsType.User);
			if (store == null)
				return null;

			uint subCollectionCount = 0;
			if (store.GetSubCollectionCount(collection, out subCollectionCount) != VSConstants.S_OK)
				return null;

			string[] names = new string[subCollectionCount];
			for (uint subCollectionIndex = 0; subCollectionIndex < subCollectionCount; ++subCollectionIndex)
			{
				string subCollectionName;
				if (store.GetSubCollectionName(collection, subCollectionIndex, out subCollectionName) == VSConstants.S_OK && String.IsNullOrEmpty(subCollectionName) == false)
					names[subCollectionIndex] = String.Join("\\", new[]{collection, subCollectionName}.Where(n => !String.IsNullOrEmpty(n)));
			}
			return names;
		}

		public bool Refresh(IEnumerable<string> names)
		{
			IVsSettingsStore store = GetReadOnlySettingsStore(SettingsType.User);
			if (store == null)
				return false;
			
			XmlDocument document = new XmlDocument();
			XmlElement rootElem = document.AppendChild(document.CreateElement("UserSettings")) as XmlElement;
			XmlElement appElem = rootElem.AppendChild(document.CreateElement("ApplicationIdentity")) as XmlElement;
			appElem.SetAttribute("version", "15.0");
			foreach (string name in names)
			{
				SettingsStorePath path = new SettingsStorePath(name);
				if (String.IsNullOrEmpty(path.Property) || String.IsNullOrEmpty(path.Collection))
					return false;
				XmlElement colElem = appElem;
				string[] colPath = path.Collection.Split(new[]{'\\'}, StringSplitOptions.RemoveEmptyEntries);
				for (int depth = 0; depth < colPath.Length; ++depth)
				{
					XmlElement childElem = colElem.SelectSingleNode(String.Format("Category[@name=\"{0}\"]", colPath[depth])) as XmlElement;
					if (childElem == null)
					{
						childElem = colElem.AppendChild(document.CreateElement("Category")) as XmlElement;
						childElem.SetAttribute("name", colPath[depth]);
						childElem.SetAttribute("RegisteredName", colPath[depth]);
					}

					colElem = childElem;
					if (depth+1 == colPath.Length)
					{
						XmlElement propElem = colElem.AppendChild(document.CreateElement("PropertyValue")) as XmlElement;
						propElem.SetAttribute("name", path.Property);
						propElem.InnerText = GetValue<string>(name) ?? "";
					}
				}
			}

			try
			{
				using (TempFile file = new TempFile())
				{
					document.Save(file.FilePath);
					DTE2 dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
					dte.ExecuteCommand("Tools.ImportandExportSettings", String.Format("/import:\"{0}\"", file.FilePath));
				}
			}
			catch (Exception e)
			{
				Log.Error("SettingsManager.Refresh exception: {0}", e.Message);
				return false;
			}
			return true;
		}

		public string[] GetPropertyNames(string collection)
		{
			IVsSettingsStore store = GetReadOnlySettingsStore(SettingsType.User);
			if (store == null)
				return null;

			uint propertyCount = 0;
			if (store.GetPropertyCount(collection, out propertyCount) != VSConstants.S_OK)
				return null;

			string[] names = new string[propertyCount];
			for (uint propertyIndex = 0; propertyIndex < propertyCount; ++propertyIndex)
			{
				string propertyName;
				if (store.GetPropertyName(collection, propertyIndex, out propertyName) == VSConstants.S_OK && String.IsNullOrEmpty(propertyName) == false)
					names[propertyIndex] = String.Join("\\", new[]{collection, propertyName}.Where(n => !String.IsNullOrEmpty(n)));
			}
			return names;
		}

		private IVsWritableSettingsStore GetWritableSettingsStore(SettingsType stype)
		{
			switch (stype)
			{
				case SettingsType.User:
				case SettingsType.Config:
				{
					IVsSettingsManager manager = Package.GetGlobalService(typeof(SVsSettingsManager)) as IVsSettingsManager;
					IVsWritableSettingsStore store = null;
					if (manager != null && manager.GetWritableSettingsStore((uint)stype.ToSettingsScope(), out store) == VSConstants.S_OK)
						return store;
					break;
				}
			}
			return null;
		}
		
		private IVsSettingsStore GetReadOnlySettingsStore(SettingsType stype)
		{
			switch (stype)
			{
				case SettingsType.User:
				case SettingsType.Config:
				{
					IVsSettingsManager manager = Package.GetGlobalService(typeof(SVsSettingsManager)) as IVsSettingsManager;
					IVsSettingsStore store = null;
					if (manager != null && manager.GetReadOnlySettingsStore((uint)stype.ToSettingsScope(), out store) == VSConstants.S_OK)
						return store;
					break;
				}
				case SettingsType.Process:
				{
					if (m_ProcessStore == null)
						m_ProcessStore = new ProcessSettingsStore();
					return m_ProcessStore;
				}
			}
			return null;
		}
	}

	public enum SettingsType
	{
		Config,
		User,
		Process,
	}

	public static class SettingsTypeExtensions
	{
		public static VisualStudio.Settings.SettingsScope ToSettingsScope(this SettingsType stype)
		{
			switch (stype)
			{
				case SettingsType.Config:	return VisualStudio.Settings.SettingsScope.Configuration;
				case SettingsType.User:		return VisualStudio.Settings.SettingsScope.UserSettings;
			}
			return 0;
		}
	}

	public class SettingsStorePath
	{
		public SettingsStorePath(string name)
		{
			Match m = Regex.Match(name ?? "", @"^(?<collection>.+)\\(?<property>.+)$");
			if (m != null)
			{
				Property = m.Groups["property"].Value.Trim();
				Collection = m.Groups["collection"].Value.Trim();
			}
		}

		public SettingsStorePath(string collection = null, string property = null)
		{
			Property = property ?? "";
			Collection = collection ?? "";
		}

		public string Property
		{
			get; 
			private set;
		}

		public string Collection
		{
			get; 
			private set;
		}
	}

	public sealed class ProcessSettingsStore : IVsSettingsStore
	{
		private XPathDocument m_Document;

		public ProcessSettingsStore()
		{
			try
			{
				using (TempFile file = new TempFile())
				{
					DTE2 dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
					dte.ExecuteCommand("Tools.ImportandExportSettings", String.Format("/export:\"{0}\"", file.FilePath));
					using (FileStream stream = OpenFileRead(file.FilePath))
					{
						if (stream == null)
							Log.Error("ProcessSettingsStore failed to open settings file");
						else
							m_Document = new XPathDocument(stream);
					}
				}
			}
			catch (Exception e)
			{
				Log.Error("ProcessSettingsStore() exception: {0}", e.Message);
			}
		}

		private FileStream OpenFileRead(string filePath, int retryCount = 10, int retryDelayMs = 100)
		{
			for (int retry = 0; retry < retryCount; ++retry)
			{
				try
				{
					return File.OpenRead(filePath);
				}
				catch (IOException)
				{
					System.Threading.Thread.Sleep(retryDelayMs);
				}
				catch (Exception)
				{
					break;
				}
			}
			return null;
		}

		private XPathNavigator FindProperty(string collectionPath, string propertyName)
		{
			if (m_Document != null && String.IsNullOrEmpty(collectionPath) == false && String.IsNullOrEmpty(propertyName) == false)
			{
				string[] categoryNames = collectionPath.Split('\\','/');
				string categoryPath = String.Join("/", categoryNames.Select(n => String.Format("Category[@name='{0}']", n)));
				string path = String.Format("/UserSettings/{0}/PropertyValue[@name='{1}']", categoryPath, propertyName);
				return m_Document.CreateNavigator().SelectSingleNode(path);
			}
			return null;
		}

		private int GetValue<ValueType>(string collectionPath, string propertyName, out ValueType value)
		{
			value = default(ValueType);
			XPathNavigator prop = FindProperty(collectionPath, propertyName);
			if (prop != null)
			{
				value = Converters.ParseType<ValueType>(prop.Value, value);
				return VSConstants.S_OK;
			}
			return VSConstants.S_FALSE;
		}

		int IVsSettingsStore.GetInt(string collectionPath, string propertyName, out int value)
		{
			return GetValue(collectionPath, propertyName, out value);
		}

		int IVsSettingsStore.GetString(string collectionPath, string propertyName, out string value)
		{ 
			return GetValue(collectionPath, propertyName, out value);
		}

		int IVsSettingsStore.PropertyExists(string collectionPath, string propertyName, out int pfExists) 
		{ 
			pfExists = FindProperty(collectionPath, propertyName) == null ? 0 : 1;
			return VSConstants.S_OK;
		}

		int IVsSettingsStore.GetPropertyType(string collectionPath, string propertyName, out uint type) 
		{ 
			if (FindProperty(collectionPath, propertyName) != null)
			{
				type = (uint)VisualStudio.Settings.SettingsType.String;
				return VSConstants.S_OK;
			}
			type = (uint)VisualStudio.Settings.SettingsType.Invalid;
			return VSConstants.S_FALSE;
		}

		int IVsSettingsStore.GetBool(string collectionPath, string propertyName, out int value) { value = 0; return VSConstants.S_FALSE; }
		int IVsSettingsStore.GetUnsignedInt(string collectionPath, string propertyName, out uint value) { value = 0; return VSConstants.S_FALSE; }
		int IVsSettingsStore.GetInt64(string collectionPath, string propertyName, out long value) { value = 0; return VSConstants.S_FALSE; }
		int IVsSettingsStore.GetUnsignedInt64(string collectionPath, string propertyName, out ulong value) { value = 0; return VSConstants.S_FALSE; }
		int IVsSettingsStore.GetBinary(string collectionPath, string propertyName, uint byteLength, byte[] pBytes, uint[] actualByteLength) { return VSConstants.S_FALSE; }
		int IVsSettingsStore.GetBoolOrDefault(string collectionPath, string propertyName, int defaultValue, out int value) { value = 0; return VSConstants.S_FALSE; }
		int IVsSettingsStore.GetIntOrDefault(string collectionPath, string propertyName, int defaultValue, out int value) { value = 0; return VSConstants.S_FALSE; }
		int IVsSettingsStore.GetUnsignedIntOrDefault(string collectionPath, string propertyName, uint defaultValue, out uint value) { value = 0; return VSConstants.S_FALSE; }
		int IVsSettingsStore.GetInt64OrDefault(string collectionPath, string propertyName, long defaultValue, out long value) { value = 0; return VSConstants.S_FALSE; }
		int IVsSettingsStore.GetUnsignedInt64OrDefault(string collectionPath, string propertyName, ulong defaultValue, out ulong value) { value = 0; return VSConstants.S_FALSE; }
		int IVsSettingsStore.GetStringOrDefault(string collectionPath, string propertyName, string defaultValue, out string value) { value = null; return VSConstants.S_FALSE; }
		int IVsSettingsStore.CollectionExists(string collectionPath, out int pfExists) { pfExists = 0; return VSConstants.S_FALSE; }
		int IVsSettingsStore.GetSubCollectionCount(string collectionPath, out uint subCollectionCount) { subCollectionCount = 0; return VSConstants.S_FALSE; }
		int IVsSettingsStore.GetPropertyCount(string collectionPath, out uint propertyCount) { propertyCount = 0; return VSConstants.S_FALSE; }
		int IVsSettingsStore.GetLastWriteTime(string collectionPath, SYSTEMTIME[] lastWriteTime) { return VSConstants.S_FALSE; }
		int IVsSettingsStore.GetSubCollectionName(string collectionPath, uint index, out string subCollectionName) { subCollectionName = null; return VSConstants.S_FALSE; }
		int IVsSettingsStore.GetPropertyName(string collectionPath, uint index, out string propertyName) { propertyName = null; return VSConstants.S_FALSE; }
	}

	public static class SettingsStoreProperties
	{
		public const string SymbolUseExcludeList = "Debugger\\SymbolUseExcludeList";
	}
}

