// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Reflection;

namespace Microsoft.VisualXpress
{
	[XmlRoot("Settings", Namespace="")]
	public class Settings
	{
		[XmlElement("Menu")]
		public MenuItemSubMenu Menu;

		[XmlElement("PropertyGroup")]
		public PropertyGroup PropertyGroup;

		public static Settings LoadFromFile(string fileName)
		{
			try
			{
				if (File.Exists(fileName) == false)
					return null;
				using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
				{
					if (fs.Length == 0)
						return null;
					XmlSerializer xml = new XmlSerializer(typeof(Settings));
					Settings settings = xml.Deserialize(fs) as Settings;
					return settings;
				}
			}
			catch (Exception e)
			{
				Log.Error("Faild to load file [{0}] with exception: {1}", fileName, e.Message);
			}
			return null;
		}
	}

	public class MenuItem
	{
		[XmlAttribute]
		public string Title;
		[XmlAttribute]
		public string InsertAfter;
		[XmlAttribute]
		public string InsertBefore;
		[XmlAttribute]
		public string Image;
		[XmlAttribute]
		public PropertyBool ShowInContextMenus;
		[XmlAttribute]
		public PropertyBool ShowInToolbar;
		[XmlAttribute]
		public PropertyBool ShowInMainMenu;
		[XmlAttribute]
		public string Tag;
		[XmlAttribute]
		public PropertyBool Enabled;
		[XmlAttribute]
		public PropertyBool Hidden;

		public void Merge(object srcItem)
		{
			if (srcItem == null)
				return;

			Type baseType = this.GetType();
			for (; baseType != null; baseType = baseType.BaseType)
			{
				if (baseType.IsAssignableFrom(srcItem.GetType()))
					break;
			}
			if (baseType == null)
				return;

			object defaultItem = System.Activator.CreateInstance(srcItem.GetType());
			foreach (FieldInfo field in baseType.GetFields(BindingFlags.Public|BindingFlags.Instance))
			{
				if (field.GetCustomAttribute<XmlAttributeAttribute>() != null && Object.Equals(field.GetValue(defaultItem), field.GetValue(srcItem)) == false)
					field.SetValue(this, field.GetValue(srcItem));
			}
		}

		public MenuItem Clone(Type dstType)
		{
			MenuItem dstItem = System.Activator.CreateInstance(dstType) as MenuItem;
			dstItem.Merge(this);
			return dstItem;
		}
	}

	public class MenuItemSubMenu : MenuItem
	{
		[XmlElement("SubMenu", typeof(MenuItemSubMenu))]
		[XmlElement("SystemCommand", typeof(MenuItemSystemCommand))]
		[XmlElement("PluginCommand", typeof(MenuItemPluginCommand))]
		[XmlElement("Separator", typeof(MenuItemSeparator))]
		public MenuItem[] m_Items;

		[XmlIgnore]
		public MenuItem[] Items
		{
			get { return m_Items != null ? m_Items : new MenuItem[0]; }
		}

		[XmlIgnore]
		public MenuItemHandle[] InnerItems
		{
			get
			{
				if (m_Items == null)
					return new MenuItemHandle[0];
				List<MenuItemHandle> outer = new List<MenuItemHandle>();
				for (int index = 0; index < m_Items.Length; ++index)
				{
					outer.Add(new MenuItemHandle(this, index));
					MenuItemSubMenu itemSubMenu = m_Items[index] as MenuItemSubMenu;
					if (itemSubMenu != null)
						outer.AddRange(itemSubMenu.InnerItems);
				}
				return outer.ToArray();
			}
		}
	}

	public class MenuItemHandle
	{
		public MenuItemHandle(MenuItemSubMenu parent, int index)
		{
			Parent = parent;
			Index = index;
		}

		public MenuItem Item
		{
			get { return IsValid ? Parent.Items[Index] : null; }
			set { Parent.Items[Index] = value; }
		}

		public MenuItemSubMenu Parent
		{
			get;
			private set;
		}

		public int Index
		{
			get;
			private set;
		}

		public bool IsValid
		{
			get { return Parent != null && Index < Parent.Items.Length; }
		}
	}

	public class MenuItemCommand : MenuItem
	{
		[XmlAttribute] 
		public string Arguments;
		[XmlAttribute]
		public PropertyBool WaitForExit;
		[XmlAttribute]
		public PropertyBool SaveAllDocs;
		[XmlAttribute]
		public string ShortcutKey;
	}

	public class MenuItemSystemCommand : MenuItemCommand
	{
		[XmlAttribute] 
		public string FileName;
		[XmlAttribute]
		public PropertyBool CloseOnExit;
		[XmlAttribute]
		public PropertyBool UseOutputWindow;
		[XmlAttribute]
		public string InitialDirectory;
	}

	public class MenuItemPluginCommand : MenuItemCommand
	{
		[XmlAttribute] 
		public string Assembly;
		[XmlAttribute] 
		public string Name;
	}

	public class MenuItemSeparator : MenuItem
	{
	}

	public class PropertyGroup
	{
		[XmlElement("Property", typeof(Property))]
		public Property[] Properties = new Property[0];
	}

	public class Property
	{
		[XmlAttribute]
		public string Name;
		[XmlText]
		public string Text;
	}

	public enum PropertyBool
	{
		[XmlEnum(Name="undefined")]
		Undefined,
		[XmlEnum(Name="false")]
		False,
		[XmlEnum(Name="true")]
		True
	}

	public static class PropertyExtensions
	{
		public static bool ToBool(this PropertyBool property, bool defaultValue = false)
		{
			if (property == PropertyBool.False)
				return false;
			if (property == PropertyBool.True)
				return true;
			return defaultValue;
		}

		public static void FromBool(this PropertyBool property, bool value)
		{
			property = value ? PropertyBool.True : PropertyBool.False;
		}

		public static Property FindByName(this Property[] properties, string name)
		{
			return properties?.FirstOrDefault(p => String.Compare(p.Name, name, StringComparison.InvariantCultureIgnoreCase) == 0);
		}
	}
}

