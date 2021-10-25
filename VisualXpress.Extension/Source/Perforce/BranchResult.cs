// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.VisualXpress.Perforce
{
	public class BranchResult : Result
	{
		private List<Node> m_Nodes = new List<Node>();

		public class Node : Result.NodeBase
		{
			public string Branch			{ get { return this.GetValue<string>("Branch"); } }
			public string Update			{ get { return this.GetValue<string>("Update"); } }
			public string Access			{ get { return this.GetValue<string>("Access"); } }
			public string Owner				{ get { return this.GetValue<string>("Owner"); } }
			public string Description		{ get { return this.GetValue<string>("Description"); } }
			public string[] View			{ get { return this.GetView().ToArray(); } }
			
			public IEnumerable<string> GetView()
			{ 
				for (int i = 0; m_Fields.TryGetValue(String.Format("View{0}", i), out string mapping); ++i)
					yield return mapping;
			}
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

		public override bool UseSingleNode()
		{
			return true;
		}
	}
}
