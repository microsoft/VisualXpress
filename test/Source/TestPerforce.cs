using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualXpress.UnitTest
{
	[TestClass]
	public class TestPerforce : TestBase
	{
		[TestMethod]
		public void TestEndpoint()
		{
			Assert(Perforce.Endpoint.Parse("").PortName == "");
			Assert(Perforce.Endpoint.Parse("").PortNumber == "");
			Assert(Perforce.Endpoint.Parse("").Scheme == "");

			Assert(Perforce.Endpoint.Parse(null).PortName == "");
			Assert(Perforce.Endpoint.Parse(null).PortNumber == "");
			Assert(Perforce.Endpoint.Parse(null).Scheme == "");

			Assert(Perforce.Endpoint.Parse("p4-quebec:1666").PortName == "p4-quebec");
			Assert(Perforce.Endpoint.Parse("p4-quebec:1666").PortNumber == "1666");
			Assert(Perforce.Endpoint.Parse("p4-quebec:1666").Scheme == "");

			Assert(Perforce.Endpoint.Parse("p4-quebec").PortName == "p4-quebec");
			Assert(Perforce.Endpoint.Parse("p4-quebec").PortNumber == "");
			Assert(Perforce.Endpoint.Parse("p4-quebec").Scheme == "");

			Assert(Perforce.Endpoint.Parse("ssl:p4-quebec").PortName == "p4-quebec");
			Assert(Perforce.Endpoint.Parse("ssl:p4-quebec").PortNumber == "");
			Assert(Perforce.Endpoint.Parse("ssl:p4-quebec").Scheme == "ssl");

			Assert(Perforce.Endpoint.Parse("p4-quebec.contoso.com:1667").PortName == "p4-quebec.contoso.com");
			Assert(Perforce.Endpoint.Parse("p4-quebec.contoso.com:1667").PortNumber == "1667");
			Assert(Perforce.Endpoint.Parse("p4-quebec.contoso.com:1667").Scheme == "");

			Assert(Perforce.Endpoint.Parse("ssl:p4-quebec.contoso.com:1667").PortName == "p4-quebec.contoso.com");
			Assert(Perforce.Endpoint.Parse("ssl:p4-quebec.contoso.com:1667").PortNumber == "1667");
			Assert(Perforce.Endpoint.Parse("ssl:p4-quebec.contoso.com:1667").Scheme == "ssl");
		}

		[TestMethod]
		public void TestBasicConnection()
		{
			Settings settings = GetUnitTestSettings();
			Assert(settings != null);

			foreach (string variable in new[]{ Properties.P4USER, Properties.P4PORT, Properties.P4CLIENT, Properties.P4HOST, Properties.P4CONFIG })
			{
				Assert(Perforce.Process.Execute(String.Format("set {0}=", Properties.P4USER)).Success);
			}

			for (int index = 0;; ++index)
			{
				string suffix = $"[{index}]";
				Perforce.Config config = new Perforce.Config();
				config.User = settings.PropertyGroup.Properties.FindByName(Properties.P4USER+suffix)?.Text;
				config.Port = settings.PropertyGroup.Properties.FindByName(Properties.P4PORT+suffix)?.Text;
				config.Client = settings.PropertyGroup.Properties.FindByName(Properties.P4CLIENT+suffix)?.Text;
				if (config.User == null && config.Port == null)
				{
					break;
				}

				Assert(String.IsNullOrEmpty(config.User) == false, Properties.P4PORT+suffix);
				Assert(String.IsNullOrEmpty(config.Port) == false, Properties.P4USER+suffix);
				Assert(Perforce.Process.Info(config).Success);
				Perforce.Config connection = Perforce.Process.Connection(config);
				Assert(Perforce.Process.Info(connection).Success);

				using (new Utilities.SetEnvironmentVariableScope(Properties.P4USER, config.User))
				using (new Utilities.SetEnvironmentVariableScope(Properties.P4PORT, config.Port))
				using (new Utilities.SetEnvironmentVariableScope(Properties.P4CLIENT, config.Client))
				{
					Assert(Perforce.Process.Info().Success);
					Perforce.Config envConnection = Perforce.Process.Connection();
					Assert(Perforce.Process.Info(envConnection).Success);
				}
			}
		}
	}
}
