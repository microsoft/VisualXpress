// Copyright Microsoft Corp. All Rights Reserved.
using System;

namespace Microsoft.VisualXpress
{
	public static class Telemetry
	{
		public static string EventFaultName(string title)
		{
			return String.Format("VS/VisualXpress/{0}", title);
		}
	}
}
