

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NDepend.DotNet;
using NDepend.Path;
using NDepend.PowerTools.Base;
using NDepend.PowerTools.SharedUtils;
using NDepend.Project;

#if !NETCORE
using System.Windows.Forms;
#endif


namespace NDepend.PowerTools.AnalyzeAssembliesInFolder {

   class AnalyzeAssembliesInFolder : IPowerTool {
      public string Name { get { return "Analyze Assemblies in Folder"; } }

      public bool AvailableOnLinuxMacOS => true;

      public string[] Description {
         get {
            return new[] {
            "Gather .NET assemblies under the folder specified by the user and analyze them.",
            "User can choose to do a recursive search in the folder."
         };
         }
      }

      public void Run() {

         //
         // Get dir
         //

         // projectManager.ShowDialogXXX() methods are not supported on .NET Core, 
         // hence the user need to copy/paste a NDepend project (.ndproj) directory path
         // or a Visual Studio solution (.sln) directory path
         Console.WriteLine("Please type or paste a directory absolute path");
         Console.WriteLine("then press ENTER");
         Console.Write(">");
         string text = Console.ReadLine();

         // Validate the directory path provided as input
         string failureReason = null;
         if (text == null || !text.TryGetAbsoluteDirectoryPath(out IAbsoluteDirectoryPath dir, out failureReason)) {
            using (new ConsoleColorPreserver(ColorSet.Error)) {
               Console.WriteLine($@"Invalid directory path entered. {(string.IsNullOrEmpty(failureReason) ? "" : failureReason)}");
               return;
            }
         }

         if (!dir.Exists) {
            using (new ConsoleColorPreserver(ColorSet.Error)) {
               Console.WriteLine($@"Directory does not exist {dir.ToString()}");
               return;
            }
         }


         // 22Jun2021: this FolderBrowserDialog is not convenient,
         //            better ask the user to copy/paste a folder path in the console
         //var folderBrowserDialog = new FolderBrowserDialog {ShowNewFolderButton = false};
         //if (folderBrowserDialog.ShowDialog() != DialogResult.OK) { return; }
         //IAbsoluteDirectoryPath dir = folderBrowserDialog.SelectedPath.ToAbsoluteDirectoryPath();


         //
         // Get recursive
         //
         bool recursive = false;
         Console.BackgroundColor = ConsoleColor.Black;
         Console.ForegroundColor = ConsoleColor.Yellow;
         Console.WriteLine("Search assemblies recursive under {" + dir.ToString() + "} ?");
         Console.WriteLine("Yes?  y    or No? (another key)");
         var consoleKeyInfo = Console.ReadKey();
         if (consoleKeyInfo.KeyChar == 'y') {
            recursive = true;
         }
         Console.ForegroundColor = ConsoleColor.White;


         //
         // Get assembliesPath
         //
         var dotNetManager = new NDependServicesProvider().DotNetManager;
         var assembliesPath = new List<IAbsoluteFilePath>();
         var cursorTop = Console.CursorTop;
         if (!recursive) {
            ScanDir(dir, assembliesPath, dotNetManager, cursorTop);
         } else {
            ScanDirRecursive(dir, assembliesPath, dotNetManager, Console.CursorTop);
         }
         Console.CursorTop = cursorTop;
         Console.CursorLeft = 0;
         ConsoleUtils.ShowNLinesOnConsole(10, ConsoleColor.Black);
         Console.CursorTop = cursorTop;
         Console.CursorLeft = 0;

         //
         // Get project
         //
         Console.ForegroundColor = ConsoleColor.Yellow;
         Console.WriteLine(assembliesPath.Count + " assemblies found");
         Console.WriteLine(assembliesPath.Select(path => path.FileNameWithoutExtension).Distinct(StringComparer.OrdinalIgnoreCase).Count() + " assemblies with distint names found");
         Console.WriteLine("Create the NDepend temporary project.");
         var projectManager = new NDependServicesProvider().ProjectManager;
         var project = projectManager.CreateTemporaryProject(assembliesPath, TemporaryProjectMode.Temporary);
         project.Trend.CustomLogRecurrence = LogRecurrence.Never;
         project.Trend.UseCustomLogFrequencyAndLabelValues = true;

         //
         // Run analysis
         //
         Console.ForegroundColor = ConsoleColor.Yellow;
         Console.WriteLine("Run Analysis!");
         var analysisResult = ProjectAnalysisUtils.RunAnalysisShowProgressOnConsole(project);

         //
         // Show results
         //
         ProjectAnalysisUtils.ShowConsoleAnalysisResult(analysisResult.CodeBase);

         Console.WriteLine("---------------------------------------------");
      }

      private static void ScanDirRecursive(IAbsoluteDirectoryPath dir, List<IAbsoluteFilePath> assembliesPath, IDotNetManager dotNetManager, int cursorTop) {
         Debug.Assert(dir != null);
         Debug.Assert(dir.Exists);
         Debug.Assert(assembliesPath != null);
         Debug.Assert(dotNetManager != null);
         ScanDir(dir, assembliesPath, dotNetManager, cursorTop);
         foreach (var dirChild in dir.ChildrenDirectoriesPath) {
            ScanDirRecursive(dirChild, assembliesPath, dotNetManager,cursorTop);
         }
      }


      private static void ScanDir(IAbsoluteDirectoryPath dir, List<IAbsoluteFilePath> assembliesPath, IDotNetManager dotNetManager, int cursorTop) {
         Debug.Assert(dir != null);
         Debug.Assert(dir.Exists);
         Debug.Assert(assembliesPath != null);
         Debug.Assert(dotNetManager != null);

         Console.CursorTop = cursorTop;
         Console.CursorLeft = 0;
         Console.WriteLine(assembliesPath.Count + " assemblies found");
         Console.Write("Scanning {" + dir.ToString() + "} ");
         foreach (var filePath in dir.ChildrenFilesPath) {
            if (!dotNetManager.IsAssembly(filePath)) { continue; }
            assembliesPath.Add(filePath);
         }
         ConsoleUtils.ShowNLinesOnConsole(Console.CursorTop - cursorTop +1, ConsoleColor.Black);
      }

   }
}