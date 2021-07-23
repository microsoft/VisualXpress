// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Runtime.InteropServices;

namespace Microsoft.VisualXpress
{
	public class NativeMethods
	{
		public const int MAX_PATH = 260;

		[DllImport("shell32.dll")]
		public static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);
		
        [DllImport("kernel32.dll")]
        public static extern IntPtr LocalFree(IntPtr hMem);

        [DllImport("shlwapi.dll", CharSet=CharSet.Auto, SetLastError=false)]
        public static extern bool PathFindOnPath([In, Out] System.Text.StringBuilder pszFile, [In] string[] ppszOtherDirs);
	}
}

