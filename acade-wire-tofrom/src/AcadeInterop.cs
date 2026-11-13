using Autodesk.AutoCAD.ApplicationServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
namespace LSE.AutoCADElectrical.WireToFrom;
// Every call into AutoCAD Electrical's LISP API goes through here. Each expression is a setting with a boundp guard and
// hands its result back through the USERS1 system variable. All three function names are unverified on a 2026 seat.
public static class AcadeInterop{
 static string Lisp(string expr){try{Document? d=AcApp.DocumentManager.MdiActiveDocument;if(d is null||string.IsNullOrWhiteSpace(expr))return "";AcApp.SetSystemVariable("USERS1","");d.Editor.Command(expr);string v=(AcApp.GetSystemVariable("USERS1") as string??"").Trim();AcApp.SetSystemVariable("USERS1","");return v;}catch{return "";}}
 public static string TryGetActiveProject(AppSettings s){string v=Lisp(s.ActiveProjectLisp);return v.EndsWith(".wdp",StringComparison.OrdinalIgnoreCase)&&File.Exists(v)?Path.GetFullPath(v):"";}
 public static string TryGetScratchDbPath(AppSettings s){string v=Lisp(s.ScratchDbLisp);return v.Length>0&&File.Exists(v)?Path.GetFullPath(v):v;}
 public static string RefreshDatabase(AppSettings s){if(string.IsNullOrWhiteSpace(s.RefreshDbLisp))return "No refresh LISP is configured (LSETOFROMSETTINGS); the project database is read as it is. Run a native report (Wire From/To) first if drawings changed.";
  try{Document? d=AcApp.DocumentManager.MdiActiveDocument;if(d is null)return "No active document; refresh skipped.";d.Editor.Command(s.RefreshDbLisp);return "Refresh LISP ran: "+s.RefreshDbLisp;}catch(System.Exception ex){return "Refresh LISP failed: "+ex.Message;}}
 public static string GuessProjectNearActiveDrawing(){try{Document? d=AcApp.DocumentManager.MdiActiveDocument;if(d is null||!d.IsNamedDrawing)return "";string folder=Path.GetDirectoryName(d.Name)??"";if(!Directory.Exists(folder))return "";var w=Directory.GetFiles(folder,"*.wdp");return w.Length==1?w[0]:"";}catch{return "";}}
 public static string CurrentDrawingPath(){try{Document? d=AcApp.DocumentManager.MdiActiveDocument;return d is not null&&d.IsNamedDrawing?Path.GetFullPath(d.Name):"";}catch{return "";}}
 public static HostBridge Bridge(ToFromConfig c)=>new(){ReadLabels=p=>LabelWriter.Read(p,c.Schematic),WriteLabels=LabelWriter.Write};
}
