// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Microsoft.VisualXpress
{
    public sealed class TempFile : IDisposable
    {
		private string m_FilePath;

		public TempFile(string filePath = null)
		{
			if (String.IsNullOrEmpty(filePath) == false)
				m_FilePath = filePath;	
			else
				m_FilePath = Path.GetFullPath(Path.GetTempFileName());
		}

		public TempFile(IEnumerable<string> lines) : this()
		{
			if (lines?.Any() == true)
			{
				using (var stream = new StreamWriter(m_FilePath))
				{
					foreach (string line in lines)
						stream.WriteLine(line);
				}
			}
		}

		public TempFile(TempFile rhs)
		{
			m_FilePath = rhs.m_FilePath;
			rhs.m_FilePath = null;
		}

		public void Dispose()
		{
			try 
			{ 
				Utilities.DeleteFile(m_FilePath);
			}
			catch
			{
				Log.Error("Failed to delete file: {0}", m_FilePath);
			}
		}

		public string FilePath
		{
			get { return m_FilePath; }
		}
	}
}
