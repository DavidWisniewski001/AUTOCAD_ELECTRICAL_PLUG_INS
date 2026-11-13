namespace LSE.AutoCADElectrical.WireToFrom;
// The release tag of this build. Shown at the end of every window title, in the error dialog, the error log and
// LSETOFROMDIAG, so that a stale LSE_ToFrom.dll left in a bundle folder is recognised at a glance.
public static class BuildInfo{
 public const string Release="2026-11-13-r1";
 public static string Label=>"LSE_ToFrom "+Release;
 public static string Title(string window)=>"LSE_ToFrom: "+window+"   ["+Release+"]";
}
