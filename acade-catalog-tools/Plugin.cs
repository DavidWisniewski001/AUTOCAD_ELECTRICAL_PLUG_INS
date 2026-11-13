using Autodesk.AutoCAD.Runtime;using System.Windows.Forms;using AcApp=Autodesk.AutoCAD.ApplicationServices.Application;
[assembly:ExtensionApplication(typeof(LSE.AutoCADElectrical.CatalogTools.Plugin))][assembly:CommandClass(typeof(LSE.AutoCADElectrical.CatalogTools.Commands))]
namespace LSE.AutoCADElectrical.CatalogTools;
public sealed class Plugin:IExtensionApplication{public void Initialize(){}public void Terminate(){}}
public sealed class Commands{
 [CommandMethod("LSECATALOG",CommandFlags.Modal)]public void Browser(){Open(null);}
 [CommandMethod("LSEASSIGNCOMPONENT",CommandFlags.Modal)]public void Assign(){try{var b=AutoCadComponentService.PromptForBlock();if(b!=null)Open(b);}catch(System.Exception ex){MessageBox.Show(ex.ToString(),"LSE Catalog Tools");}}
 void Open(SelectedBlockInfo? b){try{using var f=new CatalogBrowserForm(b);AcApp.ShowModalDialog(f);}catch(System.Exception ex){MessageBox.Show(ex.ToString(),"LSE Catalog Tools");}}
 [CommandMethod("LSECATALOGSETTINGS",CommandFlags.Modal)]public void Settings(){using var f=new SettingsForm();AcApp.ShowModalDialog(f);}
 [CommandMethod("LSECATALOGADMIN",CommandFlags.Modal)]public void Admin(){using var f=new CatalogAdminForm();AcApp.ShowModalDialog(f);}
 [CommandMethod("LSECATALOGDIAG",CommandFlags.Modal)]public void Diag(){MessageBox.Show(DiagnosticsService.BuildReport(),"Diagnostics");}
}
