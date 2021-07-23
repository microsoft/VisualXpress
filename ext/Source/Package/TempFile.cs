// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.IO;
using System.Linq;

namespace Microsoft.VisualXpress
{
    public sealed class TempFile : IDisposable
    {
		private string m_FilePath;

		public TempFile(string filePath = null)
		{
			if (String.IsNullOrEmpty(filePath))
				filePath = Path.GetTempFileName();

			m_FilePath = filePath;
		}

		public string FilePath
		{
			get { return m_FilePath; }
		}

		public void Dispose()
		{
			Utilities.DeleteFile(m_FilePath);
		}
	}
}
