

using System.Runtime.InteropServices;


static class OSHelper {
   internal static readonly bool IsOnWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
}