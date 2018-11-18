//   AssemblyInfo.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Reflection;

// Information about this assembly is defined by the following attributes.
// Change them to the values specific to your project.

[assembly: AssemblyTitle("ConfigurableContainers")]
[assembly: AssemblyDescription("Plugin for developers of part addons for Kerbal Space Program")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("")]
[assembly: AssemblyCopyright("Allis Tauri")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// The assembly version has the format "{Major}.{Minor}.{Build}.{Revision}".
// The form "{Major}.{Minor}.*" will automatically update the build and revision,
// and "{Major}.{Minor}.{Build}.*" will update just the revision.

#if NIGHTBUILD
[assembly: AssemblyVersion("2.4.*")]
#else
[assembly: AssemblyVersion("2.4.3.4")]
#endif
[assembly: KSPAssembly("ConfigurableContainers", 2, 4)]

// The following attributes are used to specify the signing key for the assembly,
// if desired. See the Mono documentation for more information about signing.

//[assembly: AssemblyDelaySign(false)]
//[assembly: AssemblyKeyFile("")]

namespace AT_Utils
{
    public class CCModInfo : KSP_AVC_Info
    {
        public CCModInfo()
        {
            MinKSPVersion = new Version(1,4,5);
            MaxKSPVersion = new Version(1,4,5);

            VersionURL   = "https://github.com/allista/ConfigurableContainers/tree/master/GameData/ConfigurableContainers/ConfigurableContainers.version";
            UpgradeURL   = "https://spacedock.info/mod/1002/Configurable%20Containers";
        }
    }
}
