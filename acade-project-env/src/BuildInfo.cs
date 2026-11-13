namespace LSE.AutoCADElectrical.ProjectEnvironment;

/// <summary>The release tag of this build. Shown in every window title, the error dialog, the error log and
/// LSEENVDIAG so that a stale DLL left in the bundle folder is recognised at a glance.</summary>
public static class BuildInfo
{
    public const string Release = "2026-11-12-r7";

    public static string Label => "LSE_Env " + Release;

    public static string Title(string window) => "LSE_Env: " + window + "   [" + Release + "]";
}
