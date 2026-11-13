using Autodesk.AutoCAD.ApplicationServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
namespace LSE.AutoCADElectrical.ProjectEnvironment;
public static class AcadeBridgeService{
 public static string LispFor(string wdpPath,string template)=>template.Replace("{WDP}",wdpPath.Replace('\\','/').Replace("\"","\\\""),StringComparison.OrdinalIgnoreCase);
 public static string TryGetActiveProject(AppSettings s){try{Document? d=AcApp.DocumentManager.MdiActiveDocument;if(d is null||string.IsNullOrWhiteSpace(s.ActiveProjectLisp))return "";AcApp.SetSystemVariable("USERS1","");d.Editor.Command(s.ActiveProjectLisp);string v=(AcApp.GetSystemVariable("USERS1") as string??"").Trim();AcApp.SetSystemVariable("USERS1","");return v.EndsWith(".wdp",StringComparison.OrdinalIgnoreCase)&&File.Exists(v)?Path.GetFullPath(v):"";}catch{return "";}}
 public static string GuessProjectNearActiveDrawing(){try{Document? d=AcApp.DocumentManager.MdiActiveDocument;if(d is null||!d.IsNamedDrawing)return "";string folder=Path.GetDirectoryName(d.Name)??"";if(!Directory.Exists(folder))return "";var w=Directory.GetFiles(folder,"*.wdp");return w.Length==1?w[0]:"";}catch{return "";}}
 public static void ReloadProject(string wdpPath,AppSettings s){if(!File.Exists(wdpPath)||string.IsNullOrWhiteSpace(s.ActivateProjectLisp))return;Document? d=AcApp.DocumentManager.MdiActiveDocument;if(d is null)return;d.SendStringToExecute(LispFor(wdpPath,s.ActivateProjectLisp)+"\n",true,false,true);}
 public static void QueueRetag(AppSettings s){if(string.IsNullOrWhiteSpace(s.RetagLisp))return;Document? d=AcApp.DocumentManager.MdiActiveDocument;if(d is null)return;d.SendStringToExecute(s.RetagLisp+"\n",true,false,true);}
 public static HostBridge Bridge(EnvRules r)=>new(){Inspect=p=>DrawingSettingsService.Inspect(p,r.WdmBlockName),ReadWires=p=>WireNumberDrawingService.Read(p,r),ApplyDrawing=q=>DrawingSettingsService.Apply(q,r)};
}
