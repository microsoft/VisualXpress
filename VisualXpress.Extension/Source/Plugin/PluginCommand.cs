// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.CommandBars;

namespace Microsoft.VisualXpress
{
	[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple=false)]
	public class PluginCommandAttribute : Attribute
	{
		public PluginCommandAttribute(string name, string description)
		{
			this.Name = name;
			this.Description = description;
		}

		public string Name 
		{ 
			get; 
			private set;
		}

		public string Description
		{ 
			get; 
			private set;
		}
	}

	[Flags]
	public enum PlugCommandOptionFlag
	{
		Required,
		Optional,
	};

	[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple=true)]
	public class PluginCommandOptionAttribute : Attribute
	{
		public PluginCommandOptionAttribute(string name, string description = "", int argumentCount = 0, PlugCommandOptionFlag flags = PlugCommandOptionFlag.Optional)
		{
			this.Name = name;
			this.Description = description;
			this.ArgumentCount = argumentCount;
			this.Flags = flags;
		}

		public string Name 
		{ 
			get; 
			private set;
		}

		public string Description
		{ 
			get; 
			private set;
		}

		public int ArgumentCount
		{ 
			get; 
			private set;
		}

		public PlugCommandOptionFlag Flags
		{ 
			get;
			private set;
		}
	}

	public class PluginCommand
	{
		public virtual bool Execute(PluginCommandOptions options)
		{
			return false;
		}

		public virtual bool InitializeCommandBar(CommandBarControls controls, MenuItem item, ref int index, bool beginGroup)
		{
			return false;
		}
		
		public Package Package 
		{ 
			get; 
			private set; 
		}

		public static PluginCommand Create(Package package, string name)
		{
			if (String.IsNullOrEmpty(name))
			{
				return null;
			}

			Type cmdType = Assembly.GetExecutingAssembly().GetTypes().FirstOrDefault(t => 
				t.GetCustomAttributes(false).OfType<PluginCommandAttribute>().Any(
				at => String.Compare(at.Name, name, StringComparison.InvariantCultureIgnoreCase) == 0));

			if (cmdType == null)
			{
				Log.Error("Invalid command name {0}", name);
				return null;
			}
				
			PluginCommand cmd = cmdType.GetConstructor(System.Type.EmptyTypes).Invoke(null) as PluginCommand;
			if (cmd == null)
			{
				Log.Error("Invalid command handler for {0}", name);
				return null;
			}

			cmd.Package = package;
			return cmd;
		}
	}

	public class PluginCommandOptions
	{
		private List<string> m_Arguments;
		private Dictionary<string, List<string>> m_Flags;

		private PluginCommandOptions()
		{
		}

		public List<string> Arguments
		{
			get { return m_Arguments; }
		}

		public Dictionary<string, List<string>> Flags
		{
			get { return m_Flags; }
		}

		public bool HasFlag(string flag)
		{
			return m_Flags.ContainsKey(flag);
		}

		public string GetFlagValue(string flag, int index = 0)
		{
			List<string> values = GetFlagValues(flag);
			return values?.Count > index ? values[index] : null;
		}

		public int GetFlagValueCount(string flag)
		{
			List<string> values = GetFlagValues(flag);
			return values != null ? values.Count : 0;
		}

		private List<string> GetFlagValues(string flag)
		{
			List<string> values;
			if (m_Flags.TryGetValue(flag, out values))
			{
				return values;
			}
			return null;
		}

		public static PluginCommandOptions Create(PluginCommand cmd, string[] args)
		{
			var ops = new PluginCommandOptions();
			ops.m_Flags = new Dictionary<string, List<string>>();
			ops.m_Arguments = new List<string>();

			var optionAttributes = new Dictionary<string, PluginCommandOptionAttribute>();
			foreach (var optionAttribute in cmd.GetType().GetCustomAttributes(true).OfType<PluginCommandOptionAttribute>())
			{
				optionAttributes[optionAttribute.Name] = optionAttribute;
			}

			int index = 0;
			while (index < args.Length)
			{
				string flagName = args[index];
				PluginCommandOptionAttribute optionAttribute;
				if (optionAttributes.TryGetValue(flagName, out optionAttribute) == false)
				{
					break;
				}

				index++;
				int argEnd = index+optionAttribute.ArgumentCount;
				if (argEnd > args.Length)
				{
					Log.Error("Invalid number of arguments for command {0} parameter -{1}. Expecting {2}", cmd.GetType().Name, flagName, optionAttribute.ArgumentCount);
					return null;
				}
				
				List<string> flagArgs = new List<string>();
				while (index < argEnd)
				{
					flagArgs.Add(args[index++].Trim());
				}

				ops.m_Flags[flagName] = flagArgs;
			}

			ops.m_Arguments.AddRange(args.Skip(index).Select(s => s.Trim()));
			return ops;
		}
	}

	public static class PluginCommandManager
	{
		public static bool Execute(Package package, string name, string[] args)
		{
			try
			{
				PluginCommand command = PluginCommand.Create(package, name);
				if (command == null)
				{
					Log.Error("Failed to find command: {0}", name);
					return false;
				}

				PluginCommandOptions options = PluginCommandOptions.Create(command, args);
				if (options == null)
				{
					Log.Error("Failed to parse options for command: {0}", name);
					return false;
				}

				if (command.Execute(options) == false)
				{
					Log.Error("Failed to execute command");
					return false;
				}
			}
			catch (Exception e)
			{
				Log.Error("Failed to handle exception: {0}", e.Message);
				return false;
			}
			return true;
		}
	}
}
