// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.VisualXpress
{
	public class LatentAction
	{
		private Task _Task;
		private int _IsDone;
		private Action<LatentActionContext> _Action;
		private Exception _ActionException;

		public LatentAction(int millisecondsDelay, Action<LatentActionContext> action)
		{
			_Action = action;
			_Task = Task.Delay(millisecondsDelay).ContinueWith((task) => Flush(LatentActionContext.Timeout), TaskScheduler.Current);
		}

		public bool Flush(LatentActionContext context = LatentActionContext.Immediate)
		{
			bool actioned = false;
			if (System.Threading.Interlocked.Exchange(ref _IsDone, 1) == 0)
			{
				try
				{
					actioned = true;
					_Action(context);
				}
				catch (Exception e)
				{
					_ActionException = e;
				}
			}
			return actioned;
		}

		public bool IsDone
		{
			get { return _IsDone != 0; }
		}

		public Exception ActionException
		{
			get { return _ActionException; }
		}
	}

	public enum LatentActionContext
	{
		Timeout,
		Immediate,
	}
}
