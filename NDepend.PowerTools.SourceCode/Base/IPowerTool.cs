

namespace NDepend.PowerTools.Base {
   interface IPowerTool {
      string Name { get; }
      bool AvailableOnLinuxMacOS { get; }
      string[] Description { get; }
      void Run();
   }
}
