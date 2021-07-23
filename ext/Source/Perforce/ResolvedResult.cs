// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.VisualXpress.Perforce
{
	public class ResolvedResult : Result
	{
		private List<Node> m_Nodes = new List<Node>();

		public class Node : Result.NodeBase
		{
			public string Path			{ get { return this.GetValue<string>("path"); } }
			public string ToFile		{ get { return this.GetValue<string>("toFile"); } }
			public string FromFile		{ get { return this.GetValue<string>("fromFile"); } }
			public string StartToRev	{ get { return this.GetValue<string>("startToRev"); } }
			public string EndToRev		{ get { return this.GetValue<string>("endToRev"); } }
			public string StartFromRev	{ get { return this.GetValue<string>("startFromRev"); } }
			public string EndFromRev	{ get { return this.GetValue<string>("endFromRev"); } }
			public string How			{ get { return this.GetValue<string>("how"); } }
			public string ResolveType	{ get { return this.GetValue<string>("resolveType"); } }
		}

		public ReadOnlyCollection<Node> Nodes
		{
			get { return m_Nodes.AsReadOnly(); }
		}

		public Node FindNode(string path)
		{
			path = Process.NormalizePath(path);
			return m_Nodes.FirstOrDefault(n => path == Process.NormalizePath(n.ToFile) || path == Process.NormalizePath(n.Path));
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
