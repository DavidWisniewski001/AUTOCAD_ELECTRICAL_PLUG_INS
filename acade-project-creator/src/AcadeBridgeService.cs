using Autodesk.AutoCAD.ApplicationServices;using AcApp=Autodesk.AutoCAD.ApplicationServices.Application;
namespace LSE.AutoCADElectrical.ProjectTools;
public static class AcadeBridgeService{
 public static string LispFor(string wdpPath,string template)=>template.Replace("{WDP}",wdpPath.Replace('\\','/').Replace("\"","\\\""),StringComparison.OrdinalIgnoreCase);
 public static void ActivateProject(string wdpPath){if(!File.Exists(wdpPath))throw new FileNotFoundException("Project file was not found.",wdpPath);var s=SettingsService.Load();if(string.IsNullOrWhiteSpace(s.ActivateProjectLisp))throw new InvalidOperationException("The activation LISP expression is blank (LSEPROJECTSETTINGS).");Document d=AcApp.DocumentManager.MdiActiveDocument??throw new InvalidOperationException("No active drawing. Open any drawing, then run LSEPROJECTACTIVATE.");d.SendStringToExecute(LispFor(wdpPath,s.ActivateProjectLisp)+"\n",true,false,true);}
 public static void OpenDrawing(string path){if(!File.Exists(path))throw new FileNotFoundException("Drawing was not found.",path);AcApp.DocumentManager.Open(path,false);}
}
