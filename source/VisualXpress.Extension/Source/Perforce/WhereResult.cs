// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.VisualXpress.Perforce
{
	public class WhereResult : Result
	{
		private List<Node> m_Nodes = new List<Node>();

		public class Node : Result.NodeBase
		{
			public string ClientFile		{ get { return this.GetValue<string>("path"); } }
			public string DepotFile			{ get { return this.GetValue<string>("depotFile"); } }
			public string WorkspaceFile		{ get { return this.GetValue<string>("clientFile"); } }
		}

		public ReadOnlyCollection<Node> Nodes
		{
			get { return m_Nodes.AsReadOnly(); }
		}

		public Node FindNode(string path)
		{
			path = Process.NormalizePath(path);
			return m_Nodes.FirstOrDefault(n => path == Process.NormalizePath(n.DepotFile) || path == Process.NormalizePath(n.ClientFile));
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
