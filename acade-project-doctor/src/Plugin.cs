using Autodesk.AutoCAD.Runtime;using System.Windows.Forms;using AcApp=Autodesk.AutoCAD.ApplicationServices.Application;
[assembly:ExtensionApplication(typeof(LSE.AutoCADElectrical.ProjectDoctor.Plugin))][assembly:CommandClass(typeof(LSE.AutoCADElectrical.ProjectDoctor.Commands))]
namespace LSE.AutoCADElectrical.ProjectDoctor;
public sealed class Plugin:IExtensionApplication{public void Initialize(){}public void Terminate(){}}
public sealed class Commands{
 [CommandMethod("LSEDOCTOR",CommandFlags.Modal)]public void Doctor(){try{var s=SettingsService.Load();string active=AcadeBridgeService.TryGetActiveProject(s);string start=active.Length>0?active:AcadeBridgeService.GuessProjectNearActiveDrawing();string? reload;using(var f=new DoctorForm(start,active)){AcApp.ShowModalDialog(f);reload=f.ReloadProjectPath;}if(reload is not null)AcadeBridgeService.ReloadProject(reload,SettingsService.Load());}catch(System.Exception ex){MessageBox.Show(ex.ToString(),"LSE_Doctor");}}
 [CommandMethod("LSEDOCTORSCAN",CommandFlags.Modal)]public void Scan(){try{var s=SettingsService.Load();string active=AcadeBridgeService.TryGetActiveProject(s);string? reload;using(var f=new ScanForm(active)){AcApp.ShowModalDialog(f);reload=f.ReloadProjectPath;}if(reload is not null)AcadeBridgeService.ReloadProject(reload,SettingsService.Load());}catch(System.Exception ex){MessageBox.Show(ex.ToString(),"LSE_Doctor");}}
 [CommandMethod("LSEDOCTORSETTINGS",CommandFlags.Modal)]public void Settings(){try{using var f=new DoctorSettingsForm();AcApp.ShowModalDialog(f);}catch(System.Exception ex){MessageBox.Show(ex.ToString(),"LSE_Doctor");}}
 [CommandMethod("LSEDOCTORHELP",CommandFlags.Modal)]public void Help(){try{using var f=new TextReportForm("LSE_Doctor - help",HelpText.All,true);AcApp.ShowModalDialog(f);}catch(System.Exception ex){MessageBox.Show(ex.ToString(),"LSE_Doctor");}}
 [CommandMethod("LSEDOCTORDIAG",CommandFlags.Modal)]public void Diag(){try{using var f=new TextReportForm("LSE_Doctor Diagnostics",DiagnosticsService.BuildReport());AcApp.ShowModalDialog(f);}catch(System.Exception ex){MessageBox.Show(ex.ToString(),"LSE_Doctor");}}
}
