// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.VisualXpress.Perforce
{
	public class PrintResult : Result
	{
		public string[] Output
		{
			get;
			private set;
		}

		public override void Parse(OutputLine[] output)
		{
			this.Output = output.Where(i => i.Channel == OutputChannel.StdOut).Select<OutputLine, string>(i => i.Text).ToArray();
			if (this.Output.Length > 0)
			{
				byte[] line = Encoding.Default.GetBytes(this.Output[0]);
				byte[] bom = Encoding.UTF8.GetPreamble();
				if (line.Length >= bom.Length && Enumerable.SequenceEqual(line.Take(bom.Length), bom))
					this.Output[0] = Encoding.UTF8.GetString(line, bom.Length, line.Length-bom.Length);
			}
		}

		public override bool UseZTag()
		{
			return false;
		}

		public TempFile CreateTempFile()
		{
			return new TempFile(this.Output);
		}
	}
}
