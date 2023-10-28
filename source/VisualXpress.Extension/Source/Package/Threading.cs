// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualXpress
{
	public static class Threading
	{
		public static AwaitExtensions.TaskSchedulerAwaitable SwitchToTaskPoolThreadAsync(this JoinableTaskFactory _)
		{
			return AwaitExtensions.SwitchTo(TaskScheduler.Default);
		}

#if VISUALXPRESS_VS_2017
		public static void FileAndForget(this JoinableTask joinableTask, string eventFaultName)
		{
		}
#endif
	}
}
