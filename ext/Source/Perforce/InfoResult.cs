// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Microsoft.VisualXpress.Perforce
{
	public class InfoResult : Result
	{
		private Node m_Node = null;

		public class Node : Result.NodeBase
		{
			public string UserName			{ get { return this.GetValue<string>("userName"); } }
			public string ClientName		{ get { return this.GetValue<string>("clientName"); } }
			public string ClientRoot		{ get { return this.GetValue<string>("clientRoot"); } }
			public string ClientLock		{ get { return this.GetValue<string>("clientLock"); } }
			public string ClientCwd			{ get { return this.GetValue<string>("clientCwd"); } }
			public string ClientHost		{ get { return this.GetValue<string>("clientHost"); } }
			public string PeerAddress		{ get { return this.GetValue<string>("peerAddress"); } }
			public string ClientAddress		{ get { return this.GetValue<string>("clientAddress"); } }
			public string ServerName		{ get { return this.GetValue<string>("serverName"); } }
			public string ServerAddress		{ get { return this.GetValue<string>("serverAddress"); } }
			public string ServerRoot		{ get { return this.GetValue<string>("serverRoot"); } }
			public string ServerDate		{ get { return this.GetValue<string>("serverDate"); } }
			public string ServerUptime		{ get { return this.GetValue<string>("serverUptime"); } }
			public string ServerVersion		{ get { return this.GetValue<string>("serverVersion"); } }
			public string ServerServices	{ get { return this.GetValue<string>("serverServices"); } }
			public string ServerLicense		{ get { return this.GetValue<string>("serverLicense"); } }
			public string CaseHandling		{ get { return this.GetValue<string>("caseHandling"); } }
			public string BrokerAddress		{ get { return this.GetValue<string>("brokerAddress"); } }
			public string BrokerVersion		{ get { return this.GetValue<string>("brokerVersion"); } }
		}

		public Perforce.Config Config
		{
			get
			{
				var config = new Perforce.Config();
				if (m_Node != null)
				{
					config.Host = Normalize(m_Node.ClientHost);
					config.User = Normalize(m_Node.UserName);
					config.Port = String.IsNullOrEmpty(m_Node.BrokerAddress) == false ? m_Node.BrokerAddress : m_Node.ServerAddress;
					config.Client = Normalize(m_Node.ClientName);
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
			m_Node = this.ParseNodes<Node>(output).FirstOrDefault();
		}

		public override bool UseZTag()
		{
			return true;
		}

		private static string Normalize(string name)
		{
			if (String.IsNullOrEmpty(name) == false && name.Contains('*') == false)
				return name;
			return String.Empty;
		}
	}
}
