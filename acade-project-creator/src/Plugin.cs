using Autodesk.AutoCAD.Runtime;using System.Windows.Forms;using AcApp=Autodesk.AutoCAD.ApplicationServices.Application;
[assembly:ExtensionApplication(typeof(LSE.AutoCADElectrical.ProjectTools.Plugin))][assembly:CommandClass(typeof(LSE.AutoCADElectrical.ProjectTools.Commands))]
namespace LSE.AutoCADElectrical.ProjectTools;
public sealed class Plugin:IExtensionApplication{public void Initialize(){}public void Terminate(){}}
public sealed class Commands{
 [CommandMethod("LSENEWPROJECT",CommandFlags.Modal|CommandFlags.Session)]public void NewProject(){try{ProjectResult? r;using(var f=new NewProjectForm()){AcApp.ShowModalDialog(f);r=f.Result;}if(r is null)return;if(r.Request.ActivateProject)AcadeBridgeService.ActivateProject(r.WdpPath);if(r.Request.OpenFirstDrawing&&r.DrawingPaths.Count>0)AcadeBridgeService.OpenDrawing(r.DrawingPaths[0]);}catch(System.Exception ex){MessageBox.Show(ex.ToString(),"LSE Project Tools");}}
 [CommandMethod("LSEPROJECTACTIVATE",CommandFlags.Modal|CommandFlags.Session)]public void Activate(){try{using var d=new OpenFileDialog{Title="Activate AutoCAD Electrical project",Filter="AutoCAD Electrical project (*.wdp)|*.wdp"};if(d.ShowDialog()!=DialogResult.OK)return;AcadeBridgeService.ActivateProject(d.FileName);}catch(System.Exception ex){MessageBox.Show(ex.ToString(),"LSE Project Tools");}}
 [CommandMethod("LSEPROJECTSETTINGS",CommandFlags.Modal)]public void Settings(){try{using var f=new ProjectSettingsForm();AcApp.ShowModalDialog(f);}catch(System.Exception ex){MessageBox.Show(ex.ToString(),"LSE Project Tools");}}
 [CommandMethod("LSEPROJECTDIAG",CommandFlags.Modal)]public void Diag(){try{MessageBox.Show(DiagnosticsService.BuildReport(),"LSE Project Tools Diagnostics");}catch(System.Exception ex){MessageBox.Show(ex.ToString(),"LSE Project Tools");}}
}
