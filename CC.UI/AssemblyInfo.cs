using System.Reflection;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("CC.UI")]
[assembly: AssemblyDescription("UI logic assembly")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("CC.UI")]
[assembly: AssemblyCopyright("Copyright © Allis Tauri 2020")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
#if NIGHTBUILD
[assembly: AssemblyVersion("1.0.*")]
#else
[assembly: AssemblyVersion("1.0.0.0")]
#endif

