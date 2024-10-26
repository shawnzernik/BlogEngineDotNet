using System;
using System.Diagnostics;
using System.Linq;
using NDepend.PowerTools.Base;
using NDepend.PowerTools.SharedUtils;
using NDepend.Analysis;
using NDepend.CodeModel;
using NDepend.Issue;
using NDepend.TechnicalDebt;

namespace NDepend.PowerTools.TechnicalDebt {
   class TechnicalDebtPowerTool : IPowerTool {
      public string Name {
         get { return "Technical Debt Evolution Since Baseline"; }
      }

      public bool AvailableOnLinuxMacOS => true;

      public string[] Description {
         get {
            return new[] {
                  "Load the current snapshot and the baseline snapshot of a project",
                  "and compute issues and technical-debt on both snapshots and then show results.",
                  "The source code of this Power Tool is a good start for any custom program",
                  "that would aim at chruning the technical-debt and the issues.",
               };
         }
      }



      public void Run() {
        
         
         // Choose project and analysis result
         IAnalysisResult analysisResult;
         if (!ProjectAnalysisUtils.TryChooseAnalysisResult(out analysisResult)) { return; }
         Debug.Assert(analysisResult != null);
         var project = analysisResult.AnalysisResultRef.Project;

         // Try load the project baseline
         ICompareContext compareContext;
         string baselineDesc;
         if (!ProjectAnalysisUtils.TryGetCompareContextDefinedByBaseline(analysisResult, out compareContext, out baselineDesc)) {
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Cannot load a baseline for the project {" + project.Properties.Name+ "}");
            return;
         }
         Debug.Assert(compareContext != null);
         SetRegularColors();
         Console.WriteLine(baselineDesc);

         // Compute issues-set-diff
         // To compute issue only on a single analysisResult just use:  analysisResult.ComputeIssues()
         SetRegularColors();
         Console.WriteLine("Computing issues on both now and baseline snapshots.");
         var issuesSetDiff = analysisResult.ComputeIssuesDiff(compareContext);
         var debtFormatter = project.DebtSettings.Values.CreateDebtFormatter();

         //
         // Technical Debt
         //
         WriteTitle("Technical Debt");
         ShowFromToLine("Technical debt",
            issuesSetDiff,
            issuesSet => debtFormatter.ToManDayString(issuesSet.AllDebt));

         ShowFromToLine("Technical debt Rating",
            issuesSetDiff,
            issuesSet => {
               DebtRating? debtRatingNullable = issuesSet.DebtRating(issuesSet.CodeBase);
               if(!debtRatingNullable.HasValue) { return "N/A"; }
               DebtRating debtRating = debtRatingNullable.Value;

               // Possible to change color in this getValueProc because it is called just before outputing the value on console
               // However cannot use debtRating.ToForeColor() and debtRating.ToBackColor() to change the console 
               // since console colors are fixed with the enum ConsoleColor.
               ConsoleColor foreColor, backColor;
               switch (debtRating) {
                  default: backColor = ConsoleColor.Green; foreColor = ConsoleColor.White; break; // Case DebtRating.A
                  case DebtRating.B: backColor = ConsoleColor.DarkGreen; foreColor = ConsoleColor.White; break;
                  case DebtRating.C: backColor = ConsoleColor.Yellow; foreColor = ConsoleColor.Black; break;
                  case DebtRating.D: backColor = ConsoleColor.DarkRed; foreColor = ConsoleColor.White; break;
                  case DebtRating.E: backColor = ConsoleColor.Red; foreColor = ConsoleColor.White; break;
               }
               SetColors(backColor, foreColor);

               return debtRatingNullable.Value.ToString();
            });

         
         ShowFromToLine("Annual Interest",
            issuesSetDiff,
            issuesSet => debtFormatter.ToManDayString(issuesSet.AllAnnualInterest));

         //
         // Quality Gates
         //
         WriteTitle("Quality Gates");
         ShowFromToLine("# Quality Gates Fail",
            issuesSetDiff,
            issuesSet => issuesSet.AllQualityGates.Count(qg => qg.Fail).ToString(), SetValueRedColors);

         ShowFromToLine("# Quality Gates Warn",
            issuesSetDiff,
            issuesSet => issuesSet.AllQualityGates.Count(qg => qg.Warn).ToString(), SetValueYellowColors);

         ShowFromToLine("# Quality Gates Pass",
            issuesSetDiff,
            issuesSet => issuesSet.AllQualityGates.Count(qg => qg.Pass).ToString(), SetValueGreenColors);

         SetInfoColors();
         Console.WriteLine("Quality Gates that rely on diff since baseline are not counted in the 'from' number.");


         //
         // Rules
         //
         WriteTitle("Rules");
         ShowFromToLine("# Critical Rules violated",
            issuesSetDiff,
            issuesSet => issuesSet.AllRules.Count(r => r.IsCritical && issuesSet.IsViolated(r)).ToString(), SetValueRedColors);

         ShowFromToLine("# Rules violated",
            issuesSetDiff,
            issuesSet => issuesSet.AllRules.Count(issuesSet.IsViolated).ToString(), SetValueDarkRedColors);

         ShowFromToLine("# Rules non violated",
            issuesSetDiff,
            issuesSet => issuesSet.AllRules.Count(r => !issuesSet.IsViolated(r)).ToString(), SetValueGreenColors);

         SetInfoColors();
         Console.WriteLine("Rules that rely on diff since baseline are not counted in the 'from' number.");

         //
         // Issues
         //
         WriteTitle("Issues");
         ShowFromToLineForIssues("# issues",
            issuesSetDiff,
            issuesSet => issuesSet.AllIssues.ToArray());

         foreach (var severity in new[] { Severity.Blocker, Severity.Critical, Severity.High, Severity.Medium, Severity.Low }) {
            var severityTmp = severity; // Needed to avoid access foreach variable in closure!
            ShowFromToLineForIssues("# " + Enum.GetName(typeof(Severity), severityTmp) + " issues",
               issuesSetDiff,
               issuesSet => issuesSet.AllIssues.Where(i => i.Severity == severityTmp).ToArray());
         }

         SetInfoColors();
         Console.WriteLine("In red # issues added since baseline, in green # issues fixed since baseline.");
         Console.WriteLine("The severity of an issue can change. Hence a change in # total issues for a certain severity level");
         Console.WriteLine("doesn't necessarily correspond to a change in # added / # fixed.");


         //
         // Quality Gates Details
         //
         WriteTitle("Quality Gates Details");
         foreach (var qualityGate in issuesSetDiff.NewerIssuesSet.AllQualityGates) {
            WriteQualityGateDetails(qualityGate, issuesSetDiff.OlderVersion(qualityGate));
         }
         SetInfoColors();
         Console.WriteLine("Quality Gates that rely on diff don't have a value computed on baseline.");
      }


      private void WriteTitle(string title) {
         Debug.Assert(!string.IsNullOrEmpty(title));
         SetRegularColors();
         Console.WriteLine();
         Console.WriteLine();
         SetTitleColors();

         // 21June2021: need t write title with background color this way to paliate a .NET 5 console bug!
         Console.Write(title);
         SetRegularColors();
         Console.WriteLine();
      }

      const int END_TITLE_COLUMN = 27;
      const int VALUE_WIDTH = 8;

      private static void ShowFromToLine(string title, IIssuesSetDiff issuesSetDiff, Func<IIssuesSet, string> getValueProc) {
         Debug.Assert(!string.IsNullOrEmpty(title));
         Debug.Assert(issuesSetDiff != null);
         Debug.Assert(getValueProc != null);
         ShowFromToLine(title, issuesSetDiff, getValueProc, SetValueColors);
      }

      private static void ShowFromToLine(string title, IIssuesSetDiff issuesSetDiff, Func<IIssuesSet,string> getValueProc, Action setValuesColorsProc) {
         Debug.Assert(!string.IsNullOrEmpty(title));
         Debug.Assert(issuesSetDiff != null);
         Debug.Assert(getValueProc != null);
         Debug.Assert(setValuesColorsProc != null);

         
         SetRegularColors();
         Console.Write(title + ":");
         AlignToColumn(END_TITLE_COLUMN);
         Console.Write("from ");

         setValuesColorsProc();
         var fromValue = getValueProc(issuesSetDiff.OlderIssuesSet);
         WriteAlignedRight(fromValue,VALUE_WIDTH);

         SetRegularColors();
         Console.Write(" to ");

         setValuesColorsProc();
         var toValue = getValueProc(issuesSetDiff.NewerIssuesSet);
         WriteAlignedRight(toValue,VALUE_WIDTH);

         AppendLineToConsole();
      }




      private static void ShowFromToLineForIssues(string title, IIssuesSetDiff issuesSetDiff, Func<IIssuesSet, IIssue[]> getIssuesProc) {
         Debug.Assert(!string.IsNullOrEmpty(title));
         Debug.Assert(issuesSetDiff != null);
         Debug.Assert(getIssuesProc != null);

         var oldIssues = getIssuesProc(issuesSetDiff.OlderIssuesSet);
         var newIssues = getIssuesProc(issuesSetDiff.NewerIssuesSet);

         SetRegularColors();
         Console.Write(title + ":");
         AlignToColumn(END_TITLE_COLUMN);
         Console.Write("from ");

         SetValueColors();
         WriteAlignedRight(oldIssues.Length.ToString(), VALUE_WIDTH);

         SetRegularColors();
         Console.Write(" to ");

         SetValueColors();
         WriteAlignedRight(newIssues.Length.ToString(), VALUE_WIDTH);

         var nbAddedIssues = newIssues.Count(issuesSetDiff.WasAdded);
         if (nbAddedIssues > 0) { SetValueRedColors(); } else { SetValueColors(); }
         WriteAlignedRight("+" + nbAddedIssues, VALUE_WIDTH);

         var nbFixedIssues = oldIssues.Count(issuesSetDiff.WasFixed);
         if (nbFixedIssues > 0) { SetValueGreenColors(); } else { SetValueColors(); }
         WriteAlignedRight("-" + nbFixedIssues, VALUE_WIDTH);

         AppendLineToConsole();
      }


      private static void WriteQualityGateDetails(IQualityGate qualityGate, IQualityGate qualityGateBaseline) {
         Debug.Assert(qualityGate != null);
         // qualityGateBaseline can be null in case the qualityGate relies on diff!

         const int QUALITY_GATE_NAME_MAX_LENGTH = 32;
         const int PADDING = 4;
         const int STATUS_LENGTH = 4;  // Fail Warn Pass
         const int VALUE_MAX_LENGTH = 7;
         const int UNIT_MAX_LENGTH = 10;
         const string FROM = "from ";
         const string TO = " to ";

         int xPos = 0;

         //
         // Write quality gate name
         var name = TruncateIfNeeded(qualityGate.Name, QUALITY_GATE_NAME_MAX_LENGTH);
         SetRegularColors();
         Console.Write(name);
         xPos += QUALITY_GATE_NAME_MAX_LENGTH + PADDING;
         AlignToColumn(xPos);

         //
         // Write Status from .. to
         if (qualityGateBaseline != null) {
            Console.Write(FROM);
            WriteQualityGateStatus(qualityGateBaseline.Status);
            Console.Write(TO);
         }
         xPos += FROM.Length + STATUS_LENGTH + TO.Length;
         AlignToColumn(xPos);
         WriteQualityGateStatus(qualityGate.Status);
         xPos += STATUS_LENGTH + PADDING;
         AlignToColumn(xPos);

         //
         // Write Value from .. to
         if (qualityGateBaseline != null) {
            Console.Write(FROM);
            WriteQualityValue(qualityGateBaseline, VALUE_MAX_LENGTH);
            Console.Write(TO);
         }
         xPos += FROM.Length + VALUE_MAX_LENGTH + TO.Length;
         AlignToColumn(xPos);
         WriteQualityValue(qualityGate, VALUE_MAX_LENGTH);
         xPos += VALUE_MAX_LENGTH + 1;
         AlignToColumn(xPos);

         //
         // Write Unit
         string unit = TruncateIfNeeded(qualityGate.Unit, UNIT_MAX_LENGTH);
         SetValueColors();
         WriteAlignedLeft(unit, UNIT_MAX_LENGTH);
         SetRegularColors();
         xPos += UNIT_MAX_LENGTH + PADDING;
         AlignToColumn(xPos);
         

         // Write Getting Better / Worst
         if (qualityGateBaseline != null && qualityGate.Value != null && qualityGateBaseline.Value != null) {
            var newVal = qualityGate.Value.Value;
            var oldVal = qualityGateBaseline.Value.Value;
            var increasing = newVal > oldVal;
            var decreasing = newVal < oldVal;
            var moreIsBad = qualityGate.MoreIsBad;
            if ((moreIsBad && increasing) || (!moreIsBad && decreasing)) {
               SetValueRedColors();
               Console.Write("Getting Worst");
            } else if ((moreIsBad && decreasing) || (!moreIsBad && increasing)) {
               SetValueGreenColors();
               Console.Write("Getting Better");
            }
         }

         AppendLineToConsole();
      }




      private static string TruncateIfNeeded(string str, int maxLength) {
         Debug.Assert(!string.IsNullOrEmpty(str));
         Debug.Assert(maxLength > 0);
         if (str.Length > maxLength) {
            str = str.Substring(0, maxLength);
         }
         return str;
      }

      private static void WriteQualityValue(IQualityGate qualityGate, int VALUE_MAX_LENGTH) {
         Debug.Assert(qualityGate != null);
         double? valueNullable = qualityGate.Value;
         string valueString = "null";
         if (valueNullable != null) {
            valueString = valueNullable.Value.ToString();
         }
         valueString = TruncateIfNeeded(valueString, VALUE_MAX_LENGTH);
         SetValueColors();
         WriteAlignedRight(valueString, VALUE_MAX_LENGTH);
         SetRegularColors();
      }

      private static void WriteQualityGateStatus(QualityGateStatus status) {
         switch (status) {
            case QualityGateStatus.Fail: SetValueRedColors(); break;
            case QualityGateStatus.Warn: SetValueYellowColors(); break;
            case QualityGateStatus.Pass: SetValueGreenColors(); break;
         }
         var statusString = Enum.GetName(typeof(QualityGateStatus), status);
         Debug.Assert(statusString.Length == 4);
         Console.Write(statusString);
         SetRegularColors();
      }



      private static void AppendLineToConsole() {
         // 21Jun2021: this is needed else in .NET Core the remaining of the line is drawn with a back color!
         SetRegularColors();
         Console.WriteLine();
      }




      private static void SetRegularColors() { SetColors(ConsoleColor.Black, ConsoleColor.White); }
      private static void SetInfoColors() { SetColors(ConsoleColor.Black, ConsoleColor.Cyan); }
      private static void SetTitleColors() { SetColors(ConsoleColor.DarkCyan, ConsoleColor.White); }
      private static void SetValueColors() { SetColors(ConsoleColor.DarkGray, ConsoleColor.White); }
      private static void SetValueRedColors() { SetColors(ConsoleColor.Red, ConsoleColor.White); }
      private static void SetValueDarkRedColors() { SetColors(ConsoleColor.DarkRed, ConsoleColor.White); }
      private static void SetValueYellowColors() { SetColors(ConsoleColor.Yellow, ConsoleColor.Black); }
      private static void SetValueGreenColors() { SetColors(ConsoleColor.DarkGreen, ConsoleColor.White); }

      private static void SetColors(ConsoleColor backColor, ConsoleColor foreColor) {
         Console.BackgroundColor = backColor;
         Console.ForegroundColor = foreColor;
      }

      

      private static void AlignToColumn(int columnPos) {
         var cursorLeft = Console.CursorLeft;
         if(cursorLeft >= columnPos) { return; }
         Console.Write(new string(' ', columnPos - cursorLeft));
      }

      private static void WriteAlignedRight(string str, int width) {
         var strLength = str.Length;
         if (strLength >= width) { goto WRITE_STR; }
         var previousBackColor = Console.BackgroundColor;
         var previousForeColor = Console.ForegroundColor;
         SetRegularColors();
         Console.Write(new string(' ', width - strLength));
         SetColors(previousBackColor, previousForeColor);
WRITE_STR:
         Console.Write(str);
      }

      private static void WriteAlignedLeft(string str, int width) {
         Console.Write(str);
         var strLength = str.Length;
         if (strLength >= width) { return; }
         SetRegularColors();
         Console.Write(new string(' ', width - strLength));
      }
   }
}
