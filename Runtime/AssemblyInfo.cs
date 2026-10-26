using System.Runtime.CompilerServices;
using UnityEngine.Scripting;

[assembly: InternalsVisibleTo("Javienne.StartupDiagnostics.Editor")]
[assembly: InternalsVisibleTo("Javienne.StartupDiagnostics.Tests")]

// The timeline starts and ends by itself, from methods run on load: without this, managed code stripping removes the
// whole assembly from the players of a project that never calls the package.
[assembly: AlwaysLinkAssembly]