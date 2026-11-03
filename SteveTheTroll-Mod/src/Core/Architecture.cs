namespace SteveTheTroll.Core
{
    /// <summary>
    /// D-169: which architecture this DLL is built on. The marker lets the lab wrapper (_agent/tools/lab-v2.ps1, a byte search in the deployed DLL), the TestLab hook
    /// stt.architecture and the in-game test Startup_Architecture prove that the game runs the V2 build, never the pre-refactor DLL ("V1", 8DD0E9D3B1ED). Raise Version only
    /// when the architecture changes again, together with the lab test and the wrapper.
    /// </summary>
    internal static class Architecture
    {
        internal const int Version = 2;
        /// Searched as UTF-16 in the DLL by lab-v2.ps1 (string constants are stored as UTF-16).
        internal const string Marker = "STT-ARCH-V2";
        /// The KydlDev spec layout (MIGRATION_DECISIONS, T-210); lab-guarded.ps1 -Marker searches it too.
        internal static readonly string Tag = "SteveTheTroll-KYDL-SPEC-V1";
    }
}
