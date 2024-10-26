using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NDepend.Analysis;
using NDepend.CodeModel;
using NDepend.PowerTools.Base;
using NDepend.PowerTools.SharedUtils;


namespace NDepend.PowerTools.DependencyReport
{
    internal class DependencyReportPowerTool : IPowerTool
    {
        public string Name
        {
            get { return "Dependency Report"; }
        }

        public bool AvailableOnLinuxMacOS => false;

        public string[] Description
        {
            get
            {
                return new[] {
                  "Generates a report of objects, files, lines, referenced, dependencies"
               };
            }
        }


        private const ConsoleColor COLOR_WARNING = ConsoleColor.Yellow;
        private const ConsoleColor COLOR_NO_WARNING = ConsoleColor.Green;
        private const ConsoleColor COLOR_DEFAULT = ConsoleColor.White;

        public void Run()
        {
            IAnalysisResult analysisResult;
            if (!ProjectAnalysisUtils.TryChooseAnalysisResult(out analysisResult))
                return;

            Debug.Assert(analysisResult != null);

            var codeBase = analysisResult.CodeBase;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("file,class,lines,used_count,using_me_count");

            foreach (var type in codeBase.Types) {
                if (type.SourceFileDeclAvailable)
                {
                    int typesUsedCount = 0;
                    foreach (var checkType in type.TypesUsed)
                        if (checkType.SourceFileDeclAvailable)
                            typesUsedCount++;

                    int typesUsingMeCount = 0;
                    foreach(var checkType in type.TypesUsingMe)
                        if(checkType.SourceFileDeclAvailable)
                            typesUsingMeCount++;

                    Console.WriteLine($@"{type.FullName} - {type.SourceDecls.FirstOrDefault().SourceFile.FilePathString}");

                    sb.Append("\"" + type.SourceDecls.FirstOrDefault().SourceFile.FilePathString.Replace("\"", "\"\"") + "\"");
                    sb.Append(",");
                    sb.Append("\"" + type.FullName.Replace("\"", "\"\"") + "\"");
                    sb.Append(",");
                    sb.Append(type.NbLinesOfCode);
                    sb.Append(",");
                    sb.Append(typesUsedCount);
                    sb.Append(",");
                    sb.Append(typesUsingMeCount);
                    sb.Append("\n");
                }
            }

            System.IO.File.WriteAllText("c:\\code\\dependencies.csv", sb.ToString());
        }
    }
}
