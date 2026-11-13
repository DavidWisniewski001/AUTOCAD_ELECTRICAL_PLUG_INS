namespace LSE.AutoCADElectrical.ProjectDoctor;

public static class BuildInfo
{
    public const string Release = "2026-11-12-r7";

    public static string Label => "LSE_Doctor " + Release;

    public static string Title(string window) => "LSE_Doctor: " + window + "   [" + Release + "]";
}
