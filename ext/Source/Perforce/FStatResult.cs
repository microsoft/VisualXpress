// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.VisualXpress.Perforce
{
	public class FStatResult : Result
	{
		private List<Node> m_Nodes = new List<Node>();

		public class Node : Result.NodeBase
		{
			public bool InDepot			{ get { return String.IsNullOrEmpty(this.DepotFile) == false && this.HeadAction != "delete"; } }
			public string ClientFile	{ get { return this.GetValue<string>("clientFile"); } }
			public string DepotFile		{ get { return this.GetValue<string>("depotFile"); } }
			public string MovedFile		{ get { return this.GetValue<string>("movedFile"); } }
			public string Path			{ get { return this.GetValue<string>("path"); } }
			public bool IsMapped		{ get { return this.Fields.ContainsKey("isMapped"); } }
			public bool Shelved			{ get { return this.Fields.ContainsKey("shelved"); } }
			public string HeadAction	{ get { return this.GetValue<string>("headAction"); } }
			public int HeadChange		{ get { return this.GetValue<int>("headChange"); } }
			public int HeadRev			{ get { return this.GetValue<int>("headRev"); } }
			public string HeadType		{ get { return this.GetValue<string>("headType"); } }
			public int HeadTime			{ get { return this.GetValue<int>("headTime"); } }
			public int HeadModTime		{ get { return this.GetValue<int>("headModTime"); } }
			public int MovedRev			{ get { return this.GetValue<int>("movedRev"); } }
			public int HaveRev			{ get { return this.GetValue<int>("haveRev"); } }
			public string Desc			{ get { return this.GetValue<string>("desc"); } }
			public string Digest		{ get { return this.GetValue<string>("digest"); } }
			public long FileSize		{ get { return this.GetValue<long>("fileSize"); } }
			public string Action		{ get { return this.GetValue<string>("action"); } }
			public string Type			{ get { return this.GetValue<string>("type"); } }
			public string ActionOwner	{ get { return this.GetValue<string>("actionOwner"); } }
			public int Change			{ get { return this.GetValue<int>("change"); } }
			public string Resolved		{ get { return this.GetValue<string>("resolved"); } }
			public string Unresolved	{ get { return this.GetValue<string>("unresolved"); } }
			public bool OtherOpen		{ get { return this.Fields.ContainsKey("otherOpen"); } }
			public bool OtherLock		{ get { return this.Fields.ContainsKey("otherLock"); } }
			public bool OurLock			{ get { return this.Fields.ContainsKey("ourLock"); } }
		}

		public ReadOnlyCollection<Node> Nodes
		{
			get { return m_Nodes.AsReadOnly(); }
		}

		public Node FindNode(string path)
		{
			path = Process.NormalizePath(path);
			return m_Nodes.FirstOrDefault(n => String.Compare(path, Process.NormalizePath(n.DepotFile), StringComparison.CurrentCultureIgnoreCase) == 0 || 
											   String.Compare(path, Process.NormalizePath(n.ClientFile), StringComparison.CurrentCultureIgnoreCase) == 0);
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
