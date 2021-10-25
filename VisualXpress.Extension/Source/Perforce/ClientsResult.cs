// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.VisualXpress.Perforce
{
	public class ClientsResult : Result
	{
		private List<Node> m_Nodes = new List<Node>();

		public class Node : Result.NodeBase
		{
			public string Client			{ get { return this.GetValue<string>("client"); } }
			public string Update			{ get { return this.GetValue<string>("Update"); } }
			public string Access			{ get { return this.GetValue<string>("Access"); } }
			public string Owner				{ get { return this.GetValue<string>("Owner"); } }
			public string Options			{ get { return this.GetValue<string>("Options"); } }
			public string SubmitOptions		{ get { return this.GetValue<string>("SubmitOptions"); } }
			public string LineEnd			{ get { return this.GetValue<string>("LineEnd"); } }
			public string Root				{ get { return this.GetValue<string>("Root"); } }
			public string Host				{ get { return this.GetValue<string>("Host"); } }
			public string Description		{ get { return this.GetValue<string>("Description"); } }
		}

		public ReadOnlyCollection<Node> Nodes
		{
			get { return m_Nodes.AsReadOnly(); }
		}

		public override void Parse(OutputLine[] output)
		{
			m_Nodes = this.ParseNodes<Node>(output);
		}

		public override bool UseZTag()
		{
			return true;
		}
	}
}
