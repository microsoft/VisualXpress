// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Xml;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.VisualStudio.Setup.Configuration;

namespace Microsoft.VisualXpress.PostBuild
{
	public class Program
	{
		public static int Main(string[] args)
		{
			try
			{
				//System.Diagnostics.Debugger.Launch();
				Console.WriteLine("PostBuild Command: {0}", System.Environment.CommandLine);
				Console.WriteLine("PostBuild Args: {0}", (args.Length == 0 ? "" : String.Format("[\"{0}\"]", String.Join("\",\"", args))));

				string targetName	  = args[0];
				string configuration  = args[1];
				string projectDir	  = Path.GetFullPath(args[2].Replace('/','\\').TrimEnd('\\'));
				string buildDir		  = Path.GetFullPath(args[3].Replace('/','\\').TrimEnd('\\'));
				string devEnvDir	  = Path.GetFullPath(args[4].Replace('/','\\').TrimEnd('\\'));
				string deployDir	  = Path.GetFullPath(args[5].Replace('/','\\').TrimEnd('\\'));

				System.Environment.SetEnvironmentVariable("TEMP", buildDir);
				System.Environment.SetEnvironmentVariable("TMP", buildDir);

				bool DoInstall = configuration.Contains("Install");
				bool DoUpdate = configuration.Contains("Update");

				VsixManifest manifest = LoadVsixManifestFile(String.Format("{0}\\source.extension.vsixmanifest", projectDir));
				Console.WriteLine("PostBuild PackageId: {0}", manifest.PackageId);
				Console.WriteLine("PostBuild PackageVersion: {0}", manifest.PackageVersion);

				string srcAtomFile = String.Format("{0}\\GalleryTemplate.atom", projectDir);
				string dstAtomFile = String.Format("{0}\\Gallery.atom", buildDir);
				BuildIndexAtom(srcAtomFile, dstAtomFile, manifest.PackageId, manifest.PackageVersion);

				string srcVsixFile = String.Format("{0}\\{1}.vsix", buildDir, targetName);
				Console.WriteLine("PostBuild Deploy To: {0}", deployDir);
				CopyFileVerbose(srcVsixFile, deployDir);
				CopyFileVerbose(dstAtomFile, deployDir);

				if (DoUpdate)
				{
					string[] vsVersions = {"15.0", "16.0", "17.0"};
					foreach (string vsVersion in vsVersions)
					{
						bool doUpdateConfiguration = false;
						foreach (string packageInstallFolder in GetVsVersionPackageInstallFolders(vsVersion, manifest.PackageId, manifest.PackageVersion))
						{
							if (Directory.Exists(packageInstallFolder) == false)
								continue;

							Console.WriteLine("PostBuild Installing To: {0}", packageInstallFolder);
							CopyFileVerbose(projectDir+"\\"+targetName+"Settings.xml", packageInstallFolder);
							CopyFileVerbose(buildDir+"\\extension.vsixmanifest", packageInstallFolder);
							CopyFileVerbose(buildDir+"\\"+targetName+".pkgdef", packageInstallFolder);
							CopyFileVerbose(buildDir+"\\"+targetName+".dll", packageInstallFolder);
							CopyFileVerbose(buildDir+"\\"+targetName+".pdb", packageInstallFolder);
							doUpdateConfiguration = true;
						}

						if (doUpdateConfiguration)
						{
							string vsVersionDevEnvFolder = GetVsVersionDevEnvFolder(vsVersion);
							string vsVersionDevEnvExe = String.Format("{0}\\devenv.exe", vsVersionDevEnvFolder);
							if (Directory.Exists(vsVersionDevEnvFolder) == false || File.Exists(vsVersionDevEnvExe) == false)
								Console.WriteLine("Skipping configuration update for missing Visual Studio version {0}", vsVersion);
							else
								ExecuteProcess(vsVersionDevEnvExe, "/updateconfiguration");
						}
					}
				}

				if (DoInstall)
				{
					LaunchProcess(String.Format("{0}\\VSIXInstaller.exe", devEnvDir), String.Format("/logFile:VSIXInstaller.log \"{0}\\{1}.vsix\"", buildDir, targetName));
				}
			}
			catch (Exception e)
			{
				Console.WriteLine("PostBuild Exception: {0}\n{1}", e.Message, e.StackTrace);
				return 1;
			}
			return 0;
		}

		private static string[] GetVsVersionPackageInstallFolders(string vsVersion, string packageId, string packageVersion = null)
		{
			if (String.IsNullOrEmpty(packageVersion) == false)
			{
				string packageInstallKey = String.Format("HKEY_CURRENT_USER\\Software\\Microsoft\\VisualStudio\\{0}\\ExtensionManager\\EnabledExtensions", vsVersion);
				string packageInstallKeyValue = String.Format("{0},{1}", packageId, packageVersion);
				string packageInstallFolder = Microsoft.Win32.Registry.GetValue(packageInstallKey, packageInstallKeyValue, null) as string;
				if (String.IsNullOrEmpty(packageInstallFolder) == false)
					return new string[]{ packageInstallFolder };
			}

			List<string> packageInstallFolders = new List<string>();
			string vsAppDataFolder = String.Format("{0}\\Microsoft\\VisualStudio", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
			foreach (string vsAppDataVersionFolder in GetDirectories(vsAppDataFolder, String.Format("{0}*", vsVersion)))
			{
				foreach (string vsExtensionPackageFolder in GetDirectories(String.Format("{0}\\Extensions", vsAppDataVersionFolder), "*"))
				{
					VsixManifest manifest = LoadVsixManifestFile(String.Format("{0}\\extension.vsixmanifest", vsExtensionPackageFolder));
					if (String.IsNullOrEmpty(manifest.PackageId) == false && String.Compare(manifest.PackageId, packageId, StringComparison.InvariantCultureIgnoreCase) == 0)
						packageInstallFolders.Add(vsExtensionPackageFolder);
				}
			}
			return packageInstallFolders.ToArray();
		}

		private static string GetVsVersionDevEnvFolder(string vsVersion)
		{
			string vsMajorVersion = vsVersion.Split('.')[0];
			if (String.IsNullOrEmpty(vsMajorVersion) == false)
			{
				SetupConfiguration query = new SetupConfiguration();
				IEnumSetupInstances setupInstances = query.EnumAllInstances();
				while (true)
				{
					ISetupInstance[] instance = new ISetupInstance[1];
					setupInstances.Next(1, instance, out int fetched);
					if (fetched <= 0)
						break;
					string installVersion = instance[0].GetInstallationVersion();
					if (vsMajorVersion == installVersion.Split('.')?.FirstOrDefault())
					{
						string installPath = instance[0].GetInstallationPath();
						if (String.IsNullOrEmpty(installPath) == false)
							return String.Format("{0}\\Common7\\IDE", installPath);
					}
				}
			}

			string[] vsRootKeys = new[] { 
				"HKEY_LOCAL_MACHINE\\Software\\Microsoft\\VisualStudio\\SxS\\VS7", 
				"HKEY_LOCAL_MACHINE\\Software\\Wow6432Node\\Microsoft\\VisualStudio\\SxS\\VS7" 
			};
			foreach (string vsRootKey in vsRootKeys)
			{
				string vsRootFolder = Microsoft.Win32.Registry.GetValue(vsRootKey, vsVersion, null) as string;
				if (String.IsNullOrEmpty(vsRootFolder) == false)
					return String.Format("{0}\\Common7\\IDE", vsRootFolder);
			}
			return null;
		}

		private static string GetXmlAttributeValue(XmlDocument document, string xpath, string attribute)
		{
			XmlElement element = document.SelectSingleNode(xpath) as XmlElement;
			if (element == null)
				throw new Exception(String.Format("PostBuild Unable to find element [{0}]", xpath));
			string value = element.GetAttribute(attribute);
			if (String.IsNullOrEmpty(value))
				throw new Exception(String.Format("PostBuild Unable to find element [{0}] attribute [{1}]", xpath, attribute));
			return value;
		}

		private static void LaunchProcess(string fileName, string arguments)
		{
			Console.WriteLine("PostBuild Launching: {0} {1}", fileName, arguments);
			System.Diagnostics.Process.Start(fileName, arguments);
		}

		private static void ExecuteProcess(string fileName, string arguments)
		{
			Console.WriteLine("PostBuild Executing: {0} {1}", fileName, arguments);
			System.Diagnostics.Process.Start(fileName, arguments).WaitForExit();
		}

		private static bool CopyFileVerbose(string srcFile, string dstFolder, bool forceOverwrite = true)
		{
			bool result = CopyFile(srcFile, dstFolder, forceOverwrite);
			Console.WriteLine(String.Format("PostBuild Copy {0}: {1} -> {2}", result ? "[SUCCESS]" : "[FAILED]", Path.GetFullPath(srcFile), Path.GetFullPath(dstFolder+"\\"+Path.GetFileName(srcFile))));
			if (result == false)
				throw new Exception("PostBuild Copy Failed");
			return result;
		}

		private static bool CopyFile(string srcFile, string dstFolder, bool forceOverwrite)
		{
			try
			{
				if (String.IsNullOrEmpty(srcFile) || String.IsNullOrEmpty(dstFolder))
					return false;
				if (File.Exists(srcFile) == false)
					return false;
				if (Directory.Exists(dstFolder) == false)
				{
					DirectoryInfo di = Directory.CreateDirectory(dstFolder);
					if (di == null || di.Exists == false)
						return false;
				}

				string dstFile = String.Format("{0}\\{1}", dstFolder, Path.GetFileName(srcFile));
				if (forceOverwrite)
					SetFileWritable(dstFile);

				File.Copy(srcFile, dstFile, true);
				return true;
			}
			catch {}
			return false; 
		}

		private static void BuildIndexAtom(string srcAtomFile, string dstAtomFile, string packageId, string version)
		{
			if (File.Exists(srcAtomFile) == false)
				throw new Exception(String.Format("PostBuild UpdateIndexAtom failed to find file: {0}", srcAtomFile));
			XmlDocument document = new XmlDocument();
			document.Load(srcAtomFile);
			XmlElement entryElem = FindAtomVisualXpressEntry(document, packageId);
			if (entryElem == null)
				throw new Exception(String.Format("PostBuild UpdateIndexAtom failed to find entry element in file: {0}", srcAtomFile));
			XmlElement versionElem = entryElem.SelectSingleNode("*[local-name()='Vsix']/*[local-name()='Version']") as XmlElement;
			if (versionElem == null)
				throw new Exception(String.Format("PostBuild UpdateIndexAtom failed to find entry version in file: {0} at {1}", srcAtomFile, entryElem));
			if (versionElem.InnerText == version)
				return;
			Console.WriteLine(String.Format("PostBuild UpdateIndexAtom {0} version {1}", dstAtomFile, version));
			versionElem.InnerText = version;
			SetFileWritable(dstAtomFile);
			document.Save(dstAtomFile);
		}

		private static XmlElement FindAtomVisualXpressEntry(XmlDocument document, string packageId)
		{
			foreach (XmlElement entryElem in document.SelectNodes("/*[local-name()='feed']/*[local-name()='entry']").OfType<XmlElement>())
			{
				XmlElement titleElem = entryElem.SelectSingleNode("*[local-name()='title']") as XmlElement;
				if (titleElem == null || titleElem.InnerText != "VisualXpress")
					continue;
				XmlElement idElem = entryElem.SelectSingleNode("*[local-name()='Vsix']/*[local-name()='Id']") as XmlElement;
				if (idElem == null)
					continue;
				if (String.Compare(idElem.InnerText, packageId, StringComparison.CurrentCultureIgnoreCase) == 0)
					return entryElem;
			}
			return null;
		}

		private static void SetFileWritable(string srcFile)
		{
			if (File.Exists(srcFile) == false)
				return;
			FileAttributes attrib = File.GetAttributes(srcFile);
			if ((attrib & FileAttributes.ReadOnly) != 0)
				File.SetAttributes(srcFile, attrib & ~FileAttributes.ReadOnly);
		}

		private static string[] GetDirectories(string srcFolder, string srcPattern = "*", SearchOption srcOption = SearchOption.TopDirectoryOnly)
		{
			if (Directory.Exists(srcFolder) == false)
				return new string[0];
			return Directory.GetDirectories(srcFolder, srcPattern, srcOption);
		}		

		private static VsixManifest LoadVsixManifestFile(string srcFile)
		{
			VsixManifest manifest = new VsixManifest();
			if (File.Exists(srcFile))
			{
				XmlDocument document = new XmlDocument();
				using (XmlTextReader reader = new XmlTextReader(srcFile))
				{
					reader.Namespaces = false;
					document.Load(reader);
				}

				string identityXPath = "PackageManifest/Metadata/Identity";
				manifest.PackageId = GetXmlAttributeValue(document, identityXPath, "Id");
				manifest.PackageVersion = GetXmlAttributeValue(document, identityXPath, "Version");
			}
			return manifest;
		}

		private class VsixManifest
		{
			public string PackageId;
			public string PackageVersion;
		}
	}
}

