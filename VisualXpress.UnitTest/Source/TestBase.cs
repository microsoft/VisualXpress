using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualXpress.UnitTest
{
	[TestClass]
	public abstract class TestBase
	{
		public static void Assert(bool value, string message = "")
		{
			if (value == false)
			{
				if (System.Diagnostics.Debugger.IsAttached)
					System.Diagnostics.Debugger.Break();

				System.Diagnostics.StackTrace stack = new System.Diagnostics.StackTrace(true);
				System.Diagnostics.StackFrame[] frames = stack.GetFrames();
				int frameIndex = Array.FindIndex(frames, f => f.GetMethod().Name == "Assert");
				string frameText = frameIndex >= 0 && frameIndex+1 < frames.Length ? frames[frameIndex+1].ToString().Trim() : "<unknown>";

				VisualStudio.TestTools.UnitTesting.Assert.IsTrue(value, String.Format("{0} [{1}]", frameText, message));
			}
		}

		public static void AssertLambda(Func<bool> expression, string message = "")
		{
			try { Assert(expression(), message); } 
			catch { Assert(false, message); }
		}

		public static void AssertLambda(Action expression, string message = "")
		{
			try { expression(); } 
			catch { Assert(false, message); }
		}

		public static string UnitTestSettingsPath
		{
			get { return String.Format("{0}\\VisualXpress.UnitTest.xml", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)); }
		}

		public static Settings GetUnitTestSettings()
		{
			Assert(File.Exists(UnitTestSettingsPath), String.Format("Failed to find UnitTest settings file: '{0}'", UnitTestSettingsPath));
			Settings settings = Settings.LoadFromFile(UnitTestSettingsPath);
			Assert(settings != null, String.Format("Failed to load UnitTest settings file: '{0}'", UnitTestSettingsPath));
			return settings;
		}
	}
}
