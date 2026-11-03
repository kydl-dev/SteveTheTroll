using Kydl.Common;
using SteveTheTroll.Core;

namespace SteveTheTroll.Testing.TestHooks
{
    /// <summary>D-169: TestLab proof that the running DLL is the Architecture V2 build (suite test architecture-v2 runs first; the V1 DLL has no such hook, so the test fails).</summary>
    internal static class ArchitectureTestHooks
    {
        /// The architecture version of the loaded DLL (2 = V2). Expected outcome: Architecture.Version.
        [TestHook("stt.architecture")]
        static int Version()
        {
            ModLog.Info("ArchitectureTestHooks: running DLL is architecture V" + Architecture.Version + " (" + Architecture.Marker + ")");
            return Architecture.Version;
        }

        /// The marker string ("STT-ARCH-V2").
        [TestHook("stt.architectureMarker")]
        static string Marker() { return Architecture.Marker; }
    }
}
