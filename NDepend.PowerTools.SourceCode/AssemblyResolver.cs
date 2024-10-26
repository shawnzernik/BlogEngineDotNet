using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;


namespace NDepend.PowerTools {
   internal class AssemblyResolver {

     internal AssemblyResolver(string relativePathToLib) {
         // Assert we have a relative path to the NDepend lib folder!
         Debug.Assert(!string.IsNullOrEmpty(relativePathToLib));
         Debug.Assert(relativePathToLib.Length >= 5);
         Debug.Assert(relativePathToLib[0] == '.');
         Debug.Assert(relativePathToLib.ToLower().EndsWith(System.IO.Path.DirectorySeparatorChar + @"lib"));

         relativePathToLib += System.IO.Path.DirectorySeparatorChar;
         m_RelativePathToLib = relativePathToLib;
      }

      private readonly string m_RelativePathToLib;

      // 8Sept2022: https://stackoverflow.com/a/71510333/27194
      private static readonly bool s_IsRunningDotNetFx = System.Runtime.InteropServices.RuntimeInformation
         .FrameworkDescription.ToLower().Contains(".net framework");

      internal Assembly AssemblyResolveHandler(object sender,ResolveEventArgs args) {
         Debug.Assert(args != null);

         var assemblyName = new AssemblyName(args.Name);
         Debug.Assert(assemblyName != null);

         var assemblyNameString = assemblyName.Name;
         Debug.Assert(!string.IsNullOrEmpty(assemblyNameString));
         var assemblyNameStringLower = assemblyNameString.ToLower();


         string relativePath = m_RelativePathToLib;
         if (!s_IsRunningDotNetFx && assemblyNameStringLower == "system.management") {
            // 8Sept2022: to make work net6.0 PowerTools on Windows,
            //            we need this special resolution of System.Management.dll that is in the same directory.
            relativePath = "";
         }
         // Special treatment for NDepend.API and NDepend.Core because they are defined in $NDependInstallDir$\Lib
         else if(assemblyNameStringLower != "ndepend.api" &&
             assemblyNameStringLower != "ndepend.core") {
            return null;
         }

         var location = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
         Debug.Assert(!string.IsNullOrEmpty(location));

         // Notice that the relative dirs "..\" and ".\" get resolved only when browsing the path, 
         // in the methods  File.Exists()  and  Assembly.LoadFrom() 
         // http://stackoverflow.com/a/6875932/27194
         var asmFilePath = System.IO.Path.Combine(location, relativePath + assemblyName.Name + ".dll");

         if (!File.Exists(asmFilePath)) { return null; }

         var assembly = Assembly.LoadFrom(asmFilePath);
         return assembly;
      }
   }
}
