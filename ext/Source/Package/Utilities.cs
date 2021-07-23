// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Microsoft.VisualXpress
{
	static class Utilities
	{
        public static void DeleteFile(string filePath, bool echo = false)
        {
            if (File.Exists(filePath) == true)
            {
				if (echo)
					Log.Info("DeleteFile: {0}", filePath);

                File.SetAttributes(filePath, FileAttributes.Normal);
                File.Delete(filePath);
            }
        }

        public static void DeleteFiles(string folderPath, string filePattern, bool echo = false)
        {
            if (Directory.Exists(folderPath))
            {
				if (echo)
					Log.Info("DeleteFiles: {0}\\{1}", folderPath, filePattern);

                foreach (var filePath in Directory.GetFiles(folderPath, filePattern, SearchOption.TopDirectoryOnly))
                {
                    File.SetAttributes(filePath, FileAttributes.Normal);
                    File.Delete(filePath);
                }
            }
        }

		public static void DeleteDirectory(string folderPath, bool echo = false)
        {
            if (Directory.Exists(folderPath))
            {
				if (echo)
					Log.Info("DeleteDirectory: {0}", folderPath);

                foreach (var filePath in Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(filePath, FileAttributes.Normal);
                    File.Delete(filePath);
                }
				Directory.Delete(folderPath, true);
            }
        }

		public static void CreateDirectory(string folderPath, bool echo = false)
		{
			if (Directory.Exists(folderPath) == false)
            {
				if (echo)
					Log.Info("DeleteDirectory: {0}", folderPath);

				Directory.CreateDirectory(folderPath);
			}
		}

		public static string GetRelativePath(string sourceFolder, string path)
		{
			if (String.IsNullOrEmpty(sourceFolder) || String.IsNullOrEmpty(path))
				return "";
			string[] sourceFolderTokens = System.IO.Path.GetFullPath(sourceFolder).Split(new char[]{'\\'}, StringSplitOptions.RemoveEmptyEntries);
			string[] pathTokens = System.IO.Path.GetFullPath(path).Split(new char[]{'\\'}, StringSplitOptions.RemoveEmptyEntries);
			int i = 0;
			for (; i < sourceFolderTokens.Length && i < pathTokens.Length; ++i)
			{
				if (String.Compare(pathTokens[i], sourceFolderTokens[i], StringComparison.CurrentCultureIgnoreCase) != 0)
					break;
			}
			if (i > 0)
			{
				string relPath = String.Join("\\", pathTokens, i, pathTokens.Length-i);
				for (; i < sourceFolderTokens.Length; ++i)
					relPath = String.Format("..\\{0}", relPath);
				return relPath;
			}
			return path;
		}

		public static string GetDirectoryName(string path)
		{
			return String.IsNullOrEmpty(path) ? path : Path.GetDirectoryName(path);
		}

		public static string GetFileName(string path)
		{
			return String.IsNullOrEmpty(path) ? path : Path.GetFileName(path);
		}

		public static string GetFileNameWithoutExtension(string path)
		{
			return String.IsNullOrEmpty(path) ? path : Path.GetFileNameWithoutExtension(path);
		}

		public static string GetFullPath(string path)
		{
			return String.IsNullOrEmpty(path) ? path : Path.GetFullPath(path);
		}
		
		public static string GetFileExtension(string path)
		{
			return String.IsNullOrEmpty(path) ? path : Path.GetExtension(path);
		}

		public static bool CopyFilesParallel(string srcFolder, string dstFolder, string fileExtension, Action<string, string> copyAction = null)
		{
			if (System.IO.Directory.Exists(srcFolder) == false)
				return false;
			if (System.IO.Path.GetFullPath(srcFolder).ToLower() == System.IO.Path.GetFullPath(dstFolder).ToLower())
				return false;
			if (copyAction == null)
				copyAction = (string srcFile, string dstFile) => { System.IO.File.Copy(srcFile, dstFile, true); };
			
			Utilities.CreateDirectory(dstFolder);
			
			string[] srcFiles = System.IO.Directory.GetFiles(srcFolder, String.Format("*.{0}", fileExtension));
			System.Threading.Tasks.Parallel.ForEach(srcFiles, srcFile => 
			{
				string dstFile = String.Format("{0}\\{1}", dstFolder, System.IO.Path.GetFileName(srcFile));
				try 
				{ 
					copyAction(srcFile, dstFile); 
				}
				catch (Exception e) 
				{ 
					Log.Error("CopyFilesParallel exception copying: {0} -> {1} [{2}]", srcFile, dstFile, e.Message); 
				}
			});
			return true;
		}

		public static int ExecuteWait(string fileName, string arguments, DataReceivedEventHandler outputReceiver = null, string directory = "", bool shell = false, IReadOnlyDictionary<string, string> environment = null)
		{
			using (var process = new System.Diagnostics.Process())
			{
				process.StartInfo.FileName = fileName;
				process.StartInfo.Arguments = arguments ?? "";
				process.StartInfo.UseShellExecute = shell;
				if (System.IO.Directory.Exists(directory))
					process.StartInfo.WorkingDirectory = directory;

				if (outputReceiver != null)
				{
					process.StartInfo.CreateNoWindow = true;
					process.StartInfo.RedirectStandardOutput = true;
					process.StartInfo.RedirectStandardError = true;
					process.OutputDataReceived += outputReceiver;
					process.ErrorDataReceived += outputReceiver;
				}

				if (environment != null && shell == false)
				{
					foreach (KeyValuePair<string, string> variable in environment)
						process.StartInfo.EnvironmentVariables[variable.Key] = variable.Value;
				}

				try
				{
					process.Start();
					if (outputReceiver != null)
					{
						process.BeginOutputReadLine();
						process.BeginErrorReadLine();
					}
					process.WaitForExit();
					return process.ExitCode;
				}
				catch
				{
					try
					{
						if (process.HasExited == false)
							process.Kill();
					}
					catch {}
				}
			}
			return -1;
		}

		public static string[] CommandLineToArgs(string commandLine)
		{
			IntPtr argv = IntPtr.Zero; 
			string[] result = null;
			try
			{
				int argc = 0;
				if (commandLine != null)
					commandLine = commandLine.Trim();
				if (String.IsNullOrEmpty(commandLine) == false)
					argv = NativeMethods.CommandLineToArgvW(commandLine, out argc);
				if (argv != IntPtr.Zero)
				{
					string[] args = new string[argc];
					for (var i = 0; i < argc; i++)
					{
						IntPtr p = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
						args[i] = Marshal.PtrToStringUni(p);
					}
					result = args;
				}
			}
			catch {}
			finally
			{
				Marshal.FreeHGlobal(argv);
			}
			return result ?? new string[0];
		}
	}
}

