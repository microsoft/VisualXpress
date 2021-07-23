// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.CommandBars;

namespace Microsoft.VisualXpress
{
	public interface IPluginService
	{
	}

	public interface IPluginServiceConnection : IPluginService
	{
		void OnConnectionChanged();
	}
}
