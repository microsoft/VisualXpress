// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.VisualXpress.Perforce
{
	public class IntegResult : Result
	{
		private List<Node> m_Nodes = new List<Node>();

		public class Node : Result.NodeBase
		{
			public string TargetFile	{ get { return this.GetValue<string>("depotFile"); } }
			public string SourceFile	{ get { return this.GetValue<string>("fromFile"); } }
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
