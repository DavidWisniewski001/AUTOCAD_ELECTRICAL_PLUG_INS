using System.Runtime.InteropServices;
using Autodesk.AutoCAD.ApplicationServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
namespace LSE.AutoCADElectrical.WireToFrom;
// Every call into AutoCAD Electrical's LISP API goes through here. Each expression is a setting with boundp guards and hands
// its result back through the USERS1 system variable. Two ways of evaluating an expression are tried in turn: the command
// line (Editor.Command) and acedEvaluateLisp from accore.dll; whichever fills USERS1 first wins and LastMethod names it.
// LSETOFROMDIAG and LSETOFROMDISCOVER run ProbeBridge so a seat shows which path works and what the functions return.
// All function names in the default expressions are unverified on a 2026 seat; the expressions are user settings.
public static class AcadeInterop{
 public const string ProbeValue="LSE_TOFROM_OK";const int RtNorm=5100;
 [DllImport("accore.dll",CharSet=CharSet.Unicode,CallingConvention=CallingConvention.Cdecl,EntryPoint="?acedEvaluateLisp@@YAHPEB_WAAPEAUresbuf@@@Z")]static extern int acedEvaluateLisp(string expression,out IntPtr result);
 [DllImport("accore.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="?acutRelRb@@YAHPEAUresbuf@@@Z")]static extern int acutRelRb(IntPtr result);
 public static string LastMethod{get;private set;}="";
 static string Users1(){try{return (AcApp.GetSystemVariable("USERS1") as string??"").Trim();}catch{return "";}}
 static void ClearUsers1(){try{AcApp.SetSystemVariable("USERS1","");}catch{}}
 public static string EvalByCommand(string expr,out string error){error="";try{Document? d=AcApp.DocumentManager.MdiActiveDocument;if(d is null){error="no active document";return "";}ClearUsers1();d.Editor.Command(expr);return Users1();}catch(System.Exception ex){error=ex.Message;return "";}}
 public static string EvalByLisp(string expr,out string error){error="";IntPtr rb=IntPtr.Zero;try{ClearUsers1();int rc=acedEvaluateLisp(expr,out rb);string v=Users1();if(v.Length==0&&rc!=RtNorm)error="acedEvaluateLisp returned "+rc;return v;}catch(System.Exception ex){error=ex.GetType().Name+": "+ex.Message;return "";}finally{if(rb!=IntPtr.Zero)try{acutRelRb(rb);}catch{}}}
 static string Lisp(string expr){LastMethod="";if(string.IsNullOrWhiteSpace(expr))return "";string v=EvalByCommand(expr,out _);if(v.Length>0){LastMethod="Editor.Command";ClearUsers1();return v;}v=EvalByLisp(expr,out _);if(v.Length>0)LastMethod="acedEvaluateLisp";ClearUsers1();return v;}
 public static string TryGetActiveProject(AppSettings s){string v=Lisp(s.ActiveProjectLisp);return v.EndsWith(".wdp",StringComparison.OrdinalIgnoreCase)&&File.Exists(v)?Path.GetFullPath(v):"";}
 public static string TryGetScratchDbPath(AppSettings s){string v=Lisp(s.ScratchDbLisp);return v.Length>0&&File.Exists(v)?Path.GetFullPath(v):v;}
 public static string RefreshDatabase(AppSettings s){if(string.IsNullOrWhiteSpace(s.RefreshDbLisp))return "No refresh LISP is configured (LSETOFROMSETTINGS); the project database is read as it is. Run a native report (Wire From/To) first if drawings changed.";
  try{Document? d=AcApp.DocumentManager.MdiActiveDocument;if(d is null)return "No active document; refresh skipped.";d.Editor.Command(s.RefreshDbLisp);return "Refresh LISP ran: "+s.RefreshDbLisp;}catch(System.Exception ex){return "Refresh LISP failed: "+ex.Message;}}
 public static List<string> ProbeBridge(AppSettings s){var x=new List<string>();string expr="(setvar \"USERS1\" \""+ProbeValue+"\")";
  string a=EvalByCommand(expr,out string ea);x.Add("  Editor.Command path: "+(a==ProbeValue?"works":"no result"+(ea.Length>0?" ("+ea+")":"")));
  string b=EvalByLisp(expr,out string eb);x.Add("  acedEvaluateLisp path: "+(b==ProbeValue?"works":"no result"+(eb.Length>0?" ("+eb+")":"")));ClearUsers1();
  if(a!=ProbeValue&&b!=ProbeValue)x.Add("  Neither path can run LISP from this command; the active project must be browsed to and the database path comes from the config pattern and search folders.");
  string p=Lisp(s.ActiveProjectLisp);x.Add("  Active project LISP returned: "+(p.Length>0?p+"  (via "+LastMethod+(File.Exists(p)?", file exists)":", file NOT found)"):"(blank: no active project, or none of the functions named in the expression exist in this release)"));
  string d=Lisp(s.ScratchDbLisp);x.Add("  Scratch database LISP returned: "+(d.Length>0?d+"  (via "+LastMethod+(File.Exists(d)?", file exists)":", file NOT found)"):"(blank: the function is not loaded in this release, or there is no project database yet; the config path pattern and search folders are used instead)"));
  return x;}
 public static string GuessProjectNearActiveDrawing(){try{Document? d=AcApp.DocumentManager.MdiActiveDocument;if(d is null||!d.IsNamedDrawing)return "";string folder=Path.GetDirectoryName(d.Name)??"";if(!Directory.Exists(folder))return "";var w=Directory.GetFiles(folder,"*.wdp");return w.Length==1?w[0]:"";}catch{return "";}}
 public static string CurrentDrawingPath(){try{Document? d=AcApp.DocumentManager.MdiActiveDocument;return d is not null&&d.IsNamedDrawing?Path.GetFullPath(d.Name):"";}catch{return "";}}
 public static HostBridge Bridge(ToFromConfig c)=>new(){ReadLabels=p=>LabelWriter.Read(p,c.Schematic),WriteLabels=LabelWriter.Write};
}
