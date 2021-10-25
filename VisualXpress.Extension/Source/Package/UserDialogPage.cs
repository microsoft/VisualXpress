// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualXpress
{
	public class UserDialogPage<UserControlType> : DialogPage where UserControlType : UserControl, IUserDialogControl, new()
	{
		public System.Windows.Forms.IWin32Window m_Window;
		public UserControlType m_UserControl;

		public UserDialogPage()
		{
			m_UserControl = new UserControlType();
			var host = new System.Windows.Forms.Integration.ElementHost();
			host.Dock = System.Windows.Forms.DockStyle.Fill;
		    host.Child = m_UserControl;
			m_Window = host;
		}

		protected override System.Windows.Forms.IWin32Window Window
		{
			get	{ return m_Window; }
		}

		protected IUserDialogControl UserDialogControl
		{
			get { return m_UserControl as IUserDialogControl; }
		}

		protected override void OnActivate(CancelEventArgs e)
		{
			base.OnActivate(e);
			this.UserDialogControl.OnActivate(e);
		}

		protected override void OnApply(DialogPage.PageApplyEventArgs e)
		{
			base.OnApply(e);
			this.UserDialogControl.OnApply(e, e.ApplyBehavior);
		}

		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);
			this.UserDialogControl.OnClosed(e);
		}

		protected override void OnDeactivate(CancelEventArgs e)
		{
			base.OnDeactivate(e);
			this.UserDialogControl.OnDeactivate(e);
		}
	}

	public interface IUserDialogControl
	{
		void OnActivate(CancelEventArgs e);
		void OnApply(EventArgs e, DialogPage.ApplyKind kind);
		void OnClosed(EventArgs e);
		void OnDeactivate(CancelEventArgs e);
	}
}
