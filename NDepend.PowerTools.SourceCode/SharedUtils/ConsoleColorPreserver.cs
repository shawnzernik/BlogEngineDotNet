using System;
using System.Diagnostics;


namespace NDepend.PowerTools.SharedUtils {

   internal enum ColorSet {
      //Success,   not needed in PowerTools
      //Warning,   not needed in PowerTools
      Error,
   }

   internal sealed class ConsoleColorPreserver : IDisposable {

      internal ConsoleColorPreserver(ColorSet colorSet) {
         ConsoleColor fore, back;
         switch (colorSet) {
            default:
               Debug.Assert(colorSet == ColorSet.Error);
               fore = ConsoleColor.White;
               back = ConsoleColor.Red;
               break;
            //case ColorSet.Success:
            //   fore = ConsoleColor.White;
            //   back = ConsoleColor.Green;
            //   break;
            //case ColorSet.Warning:
            //   fore = ConsoleColor.Black;
            //   back = ConsoleColor.Yellow;
            //   break;
         }

         m_PreviousFore = fore;
         m_PreviousBack = back;

         Console.ForegroundColor = fore;
         Console.BackgroundColor = back;
      }
      private readonly ConsoleColor m_PreviousFore;
      private readonly ConsoleColor m_PreviousBack;

      public void Dispose() {
         // 27Oct2021: Cannot get Console.ForegroundColor and Console.BackgroundColor on Linux/Mac
         // https://docs.microsoft.com/en-us/dotnet/api/system.console.backgroundcolor?view=net-5.0
         // "Unix systems don't provide any general mechanism to fetch the current console colors. Because of that, BackgroundColor returns (ConsoleColor)-1 until it is set in explicit way (using the setter)."
         // Hopefully   ResetColor()  works on Linux/MacOS as explained here:   https://stackoverflow.com/a/69739225/27194
         if ((int)m_PreviousBack == -1 || (int)m_PreviousFore == -1) {
            Console.ResetColor();
            return;
         }
         Console.ForegroundColor = m_PreviousFore;
         Console.BackgroundColor = m_PreviousBack;
      }
   }
}
