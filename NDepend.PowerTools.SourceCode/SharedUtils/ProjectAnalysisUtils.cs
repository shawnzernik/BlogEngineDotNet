using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NDepend.Analysis;
using NDepend.CodeModel;
using NDepend.Helpers;
using NDepend.Path;
using NDepend.Project;

#if !NETCORE
using System.Windows.Forms;
using NDepend.DotNet.VisualStudio;
#endif

namespace NDepend.PowerTools.SharedUtils {
   static class ProjectAnalysisUtils {


      internal static IAnalysisResult RunAnalysisShowProgressOnConsole(IProject project) {
         Debug.Assert(project != null);
         var cursorTop = Console.CursorTop;
         Console.CursorVisible = false;
         var stopwatch = new Stopwatch();
         stopwatch.Start();
         var analysisResult = project.RunAnalysis( // AndBuildReport eventually
            analysisLog => { },
            progressLog => ShowProgressDescriptionAndProgressBar(progressLog, cursorTop));
         stopwatch.Stop();
         EraseProgressLogTrace(cursorTop);
         Console.WriteLine("Analysis duration:" + stopwatch.Elapsed.ToString());
         Console.CursorVisible = true;
         return analysisResult;
      }


      internal static IAnalysisResult LoadAnalysisShowProgressOnConsole(IAnalysisResultRef analysisResultRef) {
         Debug.Assert(analysisResultRef != null);
         var cursorTop = Console.CursorTop;
         Console.CursorVisible = false;
         var analysisResult = analysisResultRef.Load(progressLog => ShowProgressDescriptionAndProgressBar(progressLog, cursorTop));
         EraseProgressLogTrace(cursorTop);
         Console.CursorVisible = true;
         return analysisResult;
      }


      private static void ShowProgressDescriptionAndProgressBar(IProgressLog progressLog, int cursorTop) {
         Debug.Assert(progressLog != null);
         try {
            Console.CursorTop = cursorTop;
            var windowWidth = Console.WindowWidth - 1 /* -1 to avoid shaking effect when Console has a small width */;

            // Show progress description
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.CursorTop = cursorTop;
            Console.CursorLeft = 0;

            var description = progressLog.EstimatedPercentageDone + "% " + progressLog.Description;
            if (description.Length > windowWidth) { description = description.Substring(0, windowWidth); }
            Console.Write(description);
            if (description.Length < windowWidth) {
               Console.Write(new string(' ', windowWidth - description.Length));
            }

            // Show progress bar
            Console.CursorTop = cursorTop + 1;
            Console.CursorLeft = 0;
            var progressBarWidth = progressLog.EstimatedPercentageDone * windowWidth / 100;
            Console.BackgroundColor = ConsoleColor.Green;
            Console.Write(new string(' ', progressBarWidth));
            Console.BackgroundColor = ConsoleColor.DarkGray;
            Console.Write(new string(' ', windowWidth - progressBarWidth));
            Console.BackgroundColor = ConsoleColor.Black;

            Console.CursorTop = cursorTop;
         } catch(ArgumentOutOfRangeException) {
            // 28Feb2023: it is possible that set Console.CursorTop throws ArgumentOutOfRangeException
            //            if the user changes the Console windows height while running an analysis
         }
      }


      private static void EraseProgressLogTrace(int cursorTop) {
         Console.CursorTop = cursorTop;
         Console.CursorLeft = 0;
         ConsoleUtils.ShowNLinesOnConsole(4, ConsoleColor.Black);
         Console.CursorTop = cursorTop;
         Console.CursorLeft = 0;
      }



      internal static void ShowConsoleAnalysisResult(ICodeBase codeBase) {
         Debug.Assert(codeBase != null);
         Console.ForegroundColor = ConsoleColor.White;
         Console.WriteLine("  # Application Assemblies " + codeBase.Application.Assemblies.Count().Format1000());
         Console.WriteLine("  # Third-Party Assemblies " + codeBase.ThirdParty.Assemblies.Count().Format1000());
         Console.WriteLine("  # Application Namespaces " + codeBase.Application.Namespaces.Count().Format1000());
         Console.WriteLine("  # Third-Party Namespaces " + codeBase.ThirdParty.Namespaces.Count().Format1000());
         ShowTypeCount("  # Types ", codeBase, t => true);
         Console.WriteLine("  # Methods " + codeBase.Methods.Count().Format1000());
         Console.WriteLine("  # Fields " + codeBase.Fields.Count().Format1000());

         Console.WriteLine("  # Line of Codes " + codeBase.Assemblies.Sum(asm => asm.NbLinesOfCode).Format1000());
         if (codeBase.Application.Assemblies.Where(a => a.NbLinesOfCode == null).Count() > 0) {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Under estimated # Lines of Code (LoC). Some assemblies PDB cannot be found and thus LoC cannot be computed for them.");
            Console.ForegroundColor = ConsoleColor.White;
         }

         Console.WriteLine("  # IL instructions " + codeBase.Assemblies.Sum(asm => asm.NbILInstructions).Format1000());
         ShowTypeCount("  # Classes ", codeBase, t => t.IsClass);
         ShowTypeCount("  # Structures ", codeBase, t => t.IsStructure);
         ShowTypeCount("  # Interfaces ", codeBase, t => t.IsInterface);
         ShowTypeCount("  # Enumerations ", codeBase, t => t.IsEnumeration);
         ShowTypeCount("  # Delegate classes ", codeBase, t => t.IsDelegate);
         ShowTypeCount("  # Exception classes ", codeBase, t => t.IsExceptionClass);
         ShowTypeCount("  # Attribute classes ", codeBase, t => t.IsAttributeClass);
      }
      private static void ShowTypeCount(string metricName, ICodeBase codeBase, Predicate<IType> predicate) {
         Debug.Assert(!string.IsNullOrEmpty(metricName));
         Debug.Assert(codeBase != null);
         Debug.Assert(predicate != null);
         var applicationTypes = codeBase.Application.Types;
         Console.WriteLine(metricName + applicationTypes.Count(t => predicate(t)).Format1000() + 
                           "     public " + applicationTypes.Count(t => t.IsPublic && predicate(t)).Format1000());
      }



      internal static bool TryChooseOrBuildProject(out IProject project) {

         var ndependServicesProvider = new NDependServicesProvider();
         var projectManager = ndependServicesProvider.ProjectManager;
         var visualStudioManager = ndependServicesProvider.VisualStudioManager;

#if NETCORE
         project = null;

         // projectManager.ShowDialogXXX() methods are not supported on .NET Core, 
         // hence the user need to copy/paste a NDepend project (.ndproj) file path
         // or a Visual Studio solution (.sln) file path
         Console.WriteLine("Please type or paste");
         Console.WriteLine("- an NDepend project (.ndproj) file path");
         Console.WriteLine("- or a Visual Studio solution (.sln) file path");
         Console.WriteLine("then press ENTER");
         Console.Write(">");
         string text = Console.ReadLine();

         // Validate the file path provided as input
         string failureReason = null;
         if (text == null|| !text.TryGetAbsoluteFilePath(out IAbsoluteFilePath filePath, out failureReason)) {
            using (new ConsoleColorPreserver(ColorSet.Error)) {
               Console.WriteLine($@"Invalid file path entered. {(string.IsNullOrEmpty(failureReason) ? "" : failureReason)}");
               return false;
            }
         }

         if (!filePath.Exists) {
            using (new ConsoleColorPreserver(ColorSet.Error)) {
               Console.WriteLine($@"File does not exist {filePath.ToString()}");
               return false;
            }
         }

         string fileExtension = filePath.FileExtension;
         switch (fileExtension) {
            default:
               using (new ConsoleColorPreserver(ColorSet.Error)) {
                  Console.WriteLine($@"Invalid file extension {fileExtension}. Only .ndproj and .sln files are supported.");
                  return false;
               }

            case ".sln":
               var assemblies = new List<IAbsoluteFilePath>();
               assemblies.AddRange(visualStudioManager.GetAssembliesFromVisualStudioSolutionOrProject(filePath));
               project = projectManager.CreateTemporaryProject(assemblies, TemporaryProjectMode.Temporary);
               break;
            case ".ndproj":
               project = projectManager.LoadProject(filePath);
               break;
         }
#else


CHOOSE_PROJECT:
         var top = Console.CursorTop;
         Console.CursorLeft = 0;
         
         Console.WriteLine("Please choose...");
         Console.WriteLine("  a) an existing NDepend project");
         Console.WriteLine("  b) one or several Visual Studio solutions to analyze");
         Console.WriteLine("  c) one or several .NET assemblies to analyze");
         Console.WriteLine("");
         var c = Char.ToLower(Console.ReadKey().KeyChar);
         Console.WriteLine();

         switch (c) {
            case 'a':
               if (!projectManager.ShowDialogChooseAnExistingProject(ConsoleUtils.MainWindowHandle, out project)) { goto TRY_AGAIN; }
               break;
            case 'b': {
                  ICollection<IAbsoluteFilePath> solutions;
                  if (!visualStudioManager.ShowDialogSelectVisualStudioSolutionsOrProjects(ConsoleUtils.MainWindowHandle, out solutions)) { goto TRY_AGAIN; }
                  var assemblies = new List<IAbsoluteFilePath>();
                  foreach (var solution in solutions) {
                     assemblies.AddRange(visualStudioManager.GetAssembliesFromVisualStudioSolutionOrProject(solution));
                  }
                  project = projectManager.CreateTemporaryProject(assemblies, TemporaryProjectMode.Temporary);
                  break;
               }
            case 'c': {
                  ICollection<IAbsoluteFilePath> assemblies;
                  if (!projectManager.ShowDialogSelectAssemblies(ConsoleUtils.MainWindowHandle, out assemblies)) { goto TRY_AGAIN; }
                  project = projectManager.CreateTemporaryProject(assemblies, TemporaryProjectMode.Temporary);
                  break;
               }

            case (char)Keys.Escape:  // ESC to exit!
               project = null;
               return false;

            default:
         TRY_AGAIN:
               var nbLinesToErase = Console.CursorTop - top;
               Console.CursorTop = top;
               Console.CursorLeft = 0;
               ConsoleUtils.ShowNLinesOnConsole(nbLinesToErase, ConsoleColor.Black);
               Console.WriteLine("(ESC to exit)");
               Console.CursorTop = top;
               Console.CursorLeft = 0;
               goto CHOOSE_PROJECT;
         }
         Debug.Assert(project != null);
#endif

         Console.ForegroundColor = ConsoleColor.DarkGray;
         Console.Write("Project selected: ");
         Console.ForegroundColor = ConsoleColor.White;
         Console.WriteLine(project.Properties.Name);
         Console.WriteLine();
         return true;
      }



      internal static bool TryChooseExistingProject(out IProject project) {

         var ndependServicesProvider = new NDependServicesProvider();
         var projectManager = ndependServicesProvider.ProjectManager;

#if NETCORE
         project = null;

         // projectManager.ShowDialogXXX() methods are not supported on .NET Core, 
         // hence the user need to copy/paste a NDepend project (.ndproj) file path
         // or a Visual Studio solution (.sln) file path
         Console.WriteLine("Please type or paste an NDepend project (.ndproj) file path");
         Console.WriteLine("then press ENTER");
         Console.Write(">");
         string text = Console.ReadLine();

         // Validate the file path provided as input
         string failureReason = null;
         if (text == null || !text.TryGetAbsoluteFilePath(out IAbsoluteFilePath filePath, out failureReason)) {
            using (new ConsoleColorPreserver(ColorSet.Error)) {
               Console.WriteLine($@"Invalid file path entered. {(string.IsNullOrEmpty(failureReason) ? "" : failureReason)}");
               return false;
            }
         }

         if (!filePath.Exists) {
            using (new ConsoleColorPreserver(ColorSet.Error)) {
               Console.WriteLine($@"File does not exist {filePath.ToString()}");
               return false;
            }
         }

         string fileExtension = filePath.FileExtension;
         switch (fileExtension) {
            default:
               using (new ConsoleColorPreserver(ColorSet.Error)) {
                  Console.WriteLine($@"Invalid file extension {fileExtension}. Only .ndproj files are supported.");
                  return false;
               }

            case ".ndproj":
               project = projectManager.LoadProject(filePath);
               break;
         }

#else
         var top = Console.CursorTop;
         Console.CursorLeft = 0;
         
         Console.WriteLine("Please choose an existing NDepend project");
         Console.WriteLine();

         if (!projectManager.ShowDialogChooseAnExistingProject(ConsoleUtils.MainWindowHandle, out project)) { return false; }
#endif
         Console.ForegroundColor = ConsoleColor.DarkGray;
         Console.Write("Project selected: ");
         Console.ForegroundColor = ConsoleColor.White;
         Console.WriteLine(project.Properties.Name);
         Console.WriteLine();
         return true;
      }


      internal static bool TryChooseAnalysisResult(out IAnalysisResult analysisResult) {
         IProject project;
         if (!TryChooseOrBuildProject(out project)) {
            analysisResult = null;
            return false;
         }
         Debug.Assert(project != null);

         IAnalysisResultRef analysisResultRef;
         if (project.TryGetMostRecentAnalysisResultRef(out analysisResultRef)) {
            // Most recent analysis result
            analysisResult = LoadAnalysisShowProgressOnConsole(analysisResultRef);
            return true;
         }
         // No analysis result available => Run analysis to obtain one
         analysisResult = RunAnalysisShowProgressOnConsole(project);
         return true;
      }

      internal static bool TryGetAssembliesToCompareAndAnalyzeThem(out ICompareContext compareContext) {
         var analysisManager = new NDependServicesProvider().AnalysisManager;

         var top = Console.CursorTop;
         Console.CursorLeft = 0;

         IProject projectOlder, projectNewer;
         IAnalysisResultRef analysisResultRefOlder = null, analysisResultRefNewer = null;
         
         

#if NETCORE
         compareContext = null;
         Console.WriteLine("Please choose the solution or project used for baseline");
         Console.WriteLine("-------------------------------------------------------");
         if (!TryChooseOrBuildProject(out projectOlder)) {
            return false;
         }

         Console.WriteLine(" ");
         Console.WriteLine("Please choose the solution or project used for newer snapshot");
         Console.WriteLine("-------------------------------------------------------------");
         if (!TryChooseOrBuildProject(out projectNewer)) {
            return false;
         }
#else
         Console.WriteLine("Please choose older and newer versions of the code base...");
         bool dialogOk = analysisManager.ShowDialogBuildComparison(
            ConsoleUtils.MainWindowHandle, 
            out projectOlder, 
            out analysisResultRefOlder, 
            out projectNewer, 
            out analysisResultRefNewer);
         if (!dialogOk) {
            compareContext = null;
            return false;
         }
#endif


         var nbLinesToErase = Console.CursorTop - top;
         Console.CursorTop = top;
         Console.CursorLeft = 0;
         ConsoleUtils.ShowNLinesOnConsole(nbLinesToErase, ConsoleColor.Black);
         Console.CursorTop = top;
         Console.CursorLeft = 0;
         

         //
         // Load or analyze
         //
         IAnalysisResult analysisResultOlder, analysisResultNewer;
         if (analysisResultRefOlder == null) {
            Debug.Assert(projectOlder != null);
            Console.WriteLine("Analyze older version of assemblies");
            analysisResultOlder = RunAnalysisShowProgressOnConsole(projectOlder);
         } else {
            Console.WriteLine("Load older analysis result");
            analysisResultOlder = LoadAnalysisShowProgressOnConsole(analysisResultRefOlder);
         }

         if (analysisResultRefNewer == null) {
            Debug.Assert(projectNewer != null);
            Console.WriteLine("Analyze newer version of assemblies");
            analysisResultNewer = RunAnalysisShowProgressOnConsole(projectNewer);
         } else {
            Console.WriteLine("Load newer analysis result");
            analysisResultNewer = LoadAnalysisShowProgressOnConsole(analysisResultRefNewer);
         }

         //
         // Re-erase
         //
         var nbLinesToErase2 = Console.CursorTop - top;
         Console.CursorTop = top;
         Console.CursorLeft = 0;
         ConsoleUtils.ShowNLinesOnConsole(nbLinesToErase2, ConsoleColor.Black);
         Console.CursorTop = top;
         Console.CursorLeft = 0;


         //
         // Show compare description
         //
         Console.ForegroundColor = ConsoleColor.DarkGray;
         Console.Write("Comparing: ");
         Console.ForegroundColor = ConsoleColor.White;
         ShowAnalysisResultRefDescription(analysisResultOlder.AnalysisResultRef);
         Console.WriteLine();

         Console.ForegroundColor = ConsoleColor.DarkGray;
         Console.Write("     with: ");
         Console.ForegroundColor = ConsoleColor.White;
         ShowAnalysisResultRefDescription(analysisResultNewer.AnalysisResultRef);

         compareContext = analysisResultNewer.CodeBase.CreateCompareContextWithOlder(analysisResultOlder.CodeBase);
         return true;
      }

      private static void ShowAnalysisResultRefDescription(IAnalysisResultRef analysisResultRef) {
         Debug.Assert(analysisResultRef != null);
         Console.Write("Project ");
         Console.Write(analysisResultRef.Project.Properties.Name);
         Console.Write("    analysis done at " + analysisResultRef.Date.ToString());
      }




      internal static bool TryGetCompareContextDefinedByBaseline(IAnalysisResult analysisResult, out ICompareContext compareContext, out string baselineDesc) {
         Debug.Assert(analysisResult != null);
         var project = analysisResult.AnalysisResultRef.Project;

         foreach (var projectBaseline in new[] {project.BaselineInUI, project.BaselineDuringAnalysis}) {
            IAnalysisResultRef analysisResultRefBaseline;
            if (projectBaseline.TryGetAnalysisResultRefToCompareWith(out analysisResultRefBaseline) == TryGetAnalysisResultRefToCompareWithResult.DoCompareWith) {
               Debug.Assert(analysisResultRefBaseline != null);
               baselineDesc = "Baseline computed " + ((int) (DateTime.Now - analysisResultRefBaseline.Date).TotalDays).ToString() + "d ago";
               compareContext = LoadCompareContext(analysisResultRefBaseline, analysisResult);
               return true;
            }
         }

         compareContext = null;
         baselineDesc = null;
         return false;
      }

      private static ICompareContext LoadCompareContext(IAnalysisResultRef analysisResultRef, IAnalysisResult analysisResult) {
         Debug.Assert(analysisResultRef != null);
         Debug.Assert(analysisResult != null);
         var otherAnalysisResult = LoadAnalysisShowProgressOnConsole(analysisResultRef);
         return analysisResult.CodeBase.CreateCompareContextWithOlder(otherAnalysisResult.CodeBase);
      }
   }
}
