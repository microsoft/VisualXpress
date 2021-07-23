// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.VisualXpress.Perforce
{
	public class ResolveResult : Result
	{
		private List<Node> m_Nodes = new List<Node>();

		public class Node : Result.NodeBase
		{
			public string ClientFile			{ get { return this.GetValue<string>("clientFile"); } }
			public string FromFile				{ get { return this.GetValue<string>("fromFile"); } }
			public string StartFromRev			{ get { return this.GetValue<string>("startFromRev"); } }
			public string EndFromRev			{ get { return this.GetValue<string>("endFromRev"); } }
			public string BaseFile				{ get { return this.GetValue<string>("baseFile"); } }
			public string BaseRev				{ get { return this.GetValue<string>("baseRev"); } }
			public string ResolveType			{ get { return this.GetValue<string>("resolveType"); } }
			public string ResolveFlag			{ get { return this.GetValue<string>("resolveFlag"); } }
			public string ContentResolveType	{ get { return this.GetValue<string>("contentResolveType"); } }
		}

		public ReadOnlyCollection<Node> Nodes
		{
			get { return m_Nodes.AsReadOnly(); }
		}

		public Node FindNode(string path)
		{
			path = Process.NormalizePath(path);
			return m_Nodes.FirstOrDefault(n => path == Process.NormalizePath(n.ClientFile));
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
