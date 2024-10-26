
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NDepend.CodeModel;
using NDepend.DotNet;
using NDepend.Path;
using NDepend.PowerTools.APIChanges;
using NDepend.PowerTools.Base;
using NDepend.PowerTools.SharedUtils;
using NDepend.Project;

namespace NDepend.PowerTools.DotNetCoreAPIChanges {
   class DotNetCoreAPIChangesPowerTool : IPowerTool {

      private Version[] s_Versions = new[] { new Version(5, 0), new Version(6, 0) };

      public string Name {
         get {
            return $".NET v{s_Versions[0].ToString()} and v{s_Versions[1].ToString()} installed Public API Changes";
         }
      }

      public bool AvailableOnLinuxMacOS => true;

      public string[] Description {
         get {
            return new string[] {
            $"Analyze .NET v{s_Versions[0].ToString()} and v{s_Versions[1].ToString()} installed, core assemblies",
            "and report API changes.",
            "Note that some API Breaking Changes might be due to types moved from one assembly to another."
         };
         }
      }



      public void Run() {
         var projectManager = new NDependServicesProvider().ProjectManager;
         var dotNetManager = new NDependServicesProvider().DotNetManager;

         // Check installation
         
         foreach (var version in s_Versions) {
            if (dotNetManager.IsInstalled(DotNetProfile.DotNet, version)) { continue; }
            Console.WriteLine(".NET v" + version.ToString() + " not installed!");
            return;
         }

         //
         // Gather core directories paths
         //
         if (!TryGetCoreAssembliesPath(s_Versions[0], dotNetManager, out IAbsoluteDirectoryPath[] dirs0)) { return; }
         if (!TryGetCoreAssembliesPath(s_Versions[1], dotNetManager, out IAbsoluteDirectoryPath[] dirs1)) { return; }


         //
         // Do analysis
         //
         Console.WriteLine($".NET v{s_Versions[0].ToString()}");
         IAbsoluteFilePath[] olderAssembliesPaths = GetAssembliesInDirs(dirs0, dotNetManager);
         var olderCodeBase = DoAnalysisGetCodeBase(s_Versions[0], olderAssembliesPaths, TemporaryProjectMode.TemporaryOlder, projectManager);

         Console.WriteLine($@"
.NET v{s_Versions[1].ToString()}");
         IAbsoluteFilePath[] newerAssembliesPaths = GetAssembliesInDirs(dirs1, dotNetManager);
         var newerCodeBase = DoAnalysisGetCodeBase(s_Versions[1], newerAssembliesPaths, TemporaryProjectMode.TemporaryNewer, projectManager);

         //
         // Create compare context
         //
         var compareContext = newerCodeBase.CreateCompareContextWithOlder(olderCodeBase);

         ConsoleUtils.ShowNLinesOnConsole(3, ConsoleColor.Black);
         Console.WriteLine("2 temporary projects have been created.");
         Console.WriteLine($"The .NET v{s_Versions[0].ToString()} and v{s_Versions[1].ToString()} analysis results can now be used live from the NDepend UI.");

         //
         // Show API Changes!
         //
         APIChangesDisplayer.Go(compareContext);
      }

      private IAbsoluteFilePath[] GetAssembliesInDirs(IAbsoluteDirectoryPath[] dirs, IDotNetManager dotNetManager) {
         var result = dirs.SelectMany(d => d.ChildrenFilesPath).Where(dotNetManager.IsAssembly).ToArray();

         Console.WriteLine($@"{result.Length} assemblies in these directories are about to be analyzed:{dirs.Select(d => d.ToString()).Aggregate(
            "", // start with empty string to handle empty list case.
            (current, next) => current + @"
" + next)}");

         return result;
      }

      private static bool TryGetCoreAssembliesPath(Version version, IDotNetManager dotNetManager, out IAbsoluteDirectoryPath[] dirs) {
         Debug.Assert(version != null);
         Debug.Assert(dotNetManager != null);
         
         // Expect dirs like
         
         dirs = dotNetManager.GetDotNetProfileDirectories(DotNetProfile.DotNet, version)
            .Where(d => d.Exists && d.ToString().ToLower().Contains("shared")).ToArray();

         if (dirs.Length == 0) {
            if (OSHelper.IsOnWindows) {
               Console.WriteLine($@"Cannot find directories like:
C:\Program Files\dotnet\shared\Microsoft.NETCore.App\{version.ToString()}
C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App\{version.ToString()}
C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\{version.ToString()}");
            } else {
               Console.WriteLine($@"Cannot find directories like:
/usr/share/dotnet/shared/Microsoft.NETCore.App/{version.ToString()}
/usr/share/dotnet/shared/Microsoft.AspNetCore.App/{version.ToString()}");
            }

            return false;
         }

         return true;
      }

      private static ICodeBase DoAnalysisGetCodeBase(
            Version version,
            ICollection<IAbsoluteFilePath> assembliesPaths,
            TemporaryProjectMode temporaryProjectMode,
            IProjectManager projectManager) {
         Debug.Assert(version != null);
         Debug.Assert(assembliesPaths != null);
         Debug.Assert(projectManager != null);
         Console.WriteLine($"Analyze .NET v{version.ToString()} assemblies");
         var project = projectManager.CreateTemporaryProject(assembliesPaths, temporaryProjectMode);
         project.Trend.CustomLogRecurrence = LogRecurrence.Never;
         project.Trend.UseCustomLogFrequencyAndLabelValues = true;
         project.Properties.Name = $"{version.ToString()} assemblies";
         projectManager.SaveProject(project);
         var analysisResult = ProjectAnalysisUtils.RunAnalysisShowProgressOnConsole(project);
         return analysisResult.CodeBase;
      }



   }
}
