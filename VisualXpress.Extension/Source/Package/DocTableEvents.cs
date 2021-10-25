using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualXpress
{
	public sealed class DocTableEvents : IVsRunningDocTableEvents3, IDisposable
	{
		private readonly IVsRunningDocumentTable m_DocumentTable;
		private uint m_Cookie;

		private DocTableEvents(IVsRunningDocumentTable documentTable)
		{
			m_DocumentTable = documentTable;
		}

		public static DocTableEvents New(IVsRunningDocumentTable documentTable)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var docTableEvents = new DocTableEvents(documentTable);
			documentTable.AdviseRunningDocTableEvents(docTableEvents, out uint cookie);
			docTableEvents.SetCookie(cookie);
			return docTableEvents;
		}

		public void SetCookie(uint cookie)
		{
			m_Cookie = cookie;
		}

		int IVsRunningDocTableEvents.OnAfterFirstDocumentLock(
			uint docCookie,
			uint dwRDTLockType,
			uint dwReadLocksRemaining,
			uint dwEditLocksRemaining)
		{
			return VSConstants.S_OK;
		}

		int IVsRunningDocTableEvents3.OnBeforeLastDocumentUnlock(
			uint docCookie,
			uint dwRDTLockType,
			uint dwReadLocksRemaining,
			uint dwEditLocksRemaining)
		{
			return VSConstants.S_OK;
		}

		int IVsRunningDocTableEvents3.OnAfterSave(uint docCookie)
		{
			return VSConstants.S_OK;
		}

		int IVsRunningDocTableEvents3.OnAfterAttributeChange(uint docCookie, uint grfAttribs)
		{
			return VSConstants.S_OK;
		}

		int IVsRunningDocTableEvents3.OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
		{
			return VSConstants.S_OK;
		}

		int IVsRunningDocTableEvents3.OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
		{
			return VSConstants.S_OK;
		}

		int IVsRunningDocTableEvents3.OnAfterAttributeChangeEx(
			uint docCookie,
			uint grfAttribs,
			IVsHierarchy pHierOld,
			uint itemidOld,
			string pszMkDocumentOld,
			IVsHierarchy pHierNew,
			uint itemidNew,
			string pszMkDocumentNew)
		{
			return VSConstants.S_OK;
		}

		public int OnBeforeSave(uint docCookie)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			m_DocumentTable.GetDocumentInfo(
				docCookie,
				out uint flags,
				out uint readLocks,
				out uint editLocks,
				out string document,
				out IVsHierarchy hierarchy,
				out uint itemId,
				out IntPtr docData);

			if (UserOptions.Instance.AutoCheckoutOnSave)
			{
				return PluginCommandCheckout.CheckOutFiles(new[] {document}, logExistingCheckout:false) ? VSConstants.S_OK : VSConstants.E_FAIL;
			}

			return VSConstants.S_OK;
		}

		int IVsRunningDocTableEvents3.OnAfterFirstDocumentLock(
			uint docCookie,
			uint dwRDTLockType,
			uint dwReadLocksRemaining,
			uint dwEditLocksRemaining)
		{
			return VSConstants.S_OK;
		}

		int IVsRunningDocTableEvents2.OnBeforeLastDocumentUnlock(
			uint docCookie,
			uint dwRDTLockType,
			uint dwReadLocksRemaining,
			uint dwEditLocksRemaining)
		{
			return VSConstants.S_OK;
		}

		int IVsRunningDocTableEvents2.OnAfterSave(uint docCookie)
		{
			return VSConstants.S_OK;
		}

		int IVsRunningDocTableEvents2.OnAfterAttributeChange(uint docCookie, uint grfAttribs)
		{
			return VSConstants.S_OK;
		}

		int IVsRunningDocTableEvents2.OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
		{
			return VSConstants.S_OK;
		}

		int IVsRunningDocTableEvents2.OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
		{
			return VSConstants.S_OK;
		}

		int IVsRunningDocTableEvents2.OnAfterAttributeChangeEx(
			uint docCookie,
			uint grfAttribs,
			IVsHierarchy pHierOld,
			uint itemidOld,
			string pszMkDocumentOld,
			IVsHierarchy pHierNew,
			uint itemidNew,
			string pszMkDocumentNew)
		{
			return VSConstants.S_OK;
		}

		int IVsRunningDocTableEvents2.OnAfterFirstDocumentLock(
			uint docCookie,
			uint dwRDTLockType,
			uint dwReadLocksRemaining,
			uint dwEditLocksRemaining)
		{
			return VSConstants.S_OK;
		}

		int IVsRunningDocTableEvents.OnBeforeLastDocumentUnlock(
			uint docCookie,
			uint dwRDTLockType,
			uint dwReadLocksRemaining,
			uint dwEditLocksRemaining)
		{
			return VSConstants.S_OK;
		}

		int IVsRunningDocTableEvents.OnAfterSave(uint docCookie)
		{
			return VSConstants.S_OK;
		}

		int IVsRunningDocTableEvents.OnAfterAttributeChange(uint docCookie, uint grfAttribs)
		{
			return VSConstants.S_OK;
		}

		int IVsRunningDocTableEvents.OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
		{
			return VSConstants.S_OK;
		}

		int IVsRunningDocTableEvents.OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
		{
			return VSConstants.S_OK;
		}

		public void Dispose()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (m_Cookie != 0)
			{
				m_DocumentTable.UnadviseRunningDocTableEvents(m_Cookie);
			}
		}
	}
}