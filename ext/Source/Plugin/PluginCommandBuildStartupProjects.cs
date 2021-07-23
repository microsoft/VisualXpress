// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.VisualXpress
{
    [PluginCommand("buildstartupprojects", "Build startup project(s) using the active build configuration")]
    public class PluginCommandBuildStartupProjects : PluginCommand
    {
        public override bool Execute(PluginCommandOptions options)
        {
            try
            {
                EnvDTE80.SolutionBuild2 sb = (EnvDTE80.SolutionBuild2)Package.ActiveDTE2.Solution.SolutionBuild;
                EnvDTE80.SolutionConfiguration2 sc = (EnvDTE80.SolutionConfiguration2)sb.ActiveConfiguration;

                Package.ActiveDTE2.ExecuteCommand("View.Output");

                foreach (String project in (Array)sb.StartupProjects)
                {
                    Package.ActiveDTE2.Solution.SolutionBuild.BuildProject(sc.Name + "|" + sc.PlatformName, project, false);
                }

            }
            catch (Exception e)
            {
                Log.Error("PluginCommandBuildStartupProjects failed: {0}", e.Message);
            }
            return true;
        }
    }
}
