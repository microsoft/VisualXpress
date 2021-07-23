// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.VisualXpress.Perforce
{
	public class ReconcileResult : Result
	{
		private List<Node> m_Nodes = new List<Node>();

		public class Node : Result.NodeBase
		{
			public string DepotFile		{ get { return this.GetValue<string>("depotFile"); } }
			public string ClientFile	{ get { return this.GetValue<string>("clientFile"); } }
			public int WorkRev			{ get { return this.GetValue<int>("workRev"); } }
			public string Action		{ get { return this.GetValue<string>("action"); } }
			public string Type			{ get { return this.GetValue<string>("type"); } }
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
