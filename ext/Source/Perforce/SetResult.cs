// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Microsoft.VisualXpress.Perforce
{
	public class SetResult : Result
	{
		private Node m_Node = null;

		public class Node : Result.NodeBase
		{
			public string P4HOST			{ get { return this.GetValue<string>("P4HOST"); } }
			public string P4USER			{ get { return this.GetValue<string>("P4USER"); } }
			public string P4PORT			{ get { return this.GetValue<string>("P4PORT"); } }
			public string P4CLIENT			{ get { return this.GetValue<string>("P4CLIENT"); } }
			public string P4CONFIG			{ get { return this.GetValue<string>("P4CONFIG"); } }
			public string P4EDITOR			{ get { return this.GetValue<string>("P4EDITOR"); } }
			public string P4PASSWD			{ get { return this.GetValue<string>("P4PASSWD"); } }
		}

		public Perforce.Config Config
		{
			get
			{
				var config = new Perforce.Config();
				if (m_Node != null)
				{
					config.Host = m_Node.P4HOST;
					config.User = m_Node.P4USER;
					config.Port = m_Node.P4PORT;
					config.Client = m_Node.P4CLIENT;
				}
				return config;
			}
		}

		public Node InternalNode
		{
			get { return m_Node; }
		}

		public override void Parse(OutputLine[] output)
		{
			m_Node = new Node();
			foreach (var line in output.Where(i => i.Channel == OutputChannel.StdOut))
			{
				Match m = Regex.Match(line.Text, @"^(?<name>\w\S+)\s*=\s*(?<value>.*?)(\s+\((?<type>.+)\))?$");
				if (m.Success)
					m_Node.Fields[m.Groups["name"].Value] = m.Groups["value"].Value;
			}
		}

		public override bool UseZTag()
		{
			return false;
		}
	}
}
