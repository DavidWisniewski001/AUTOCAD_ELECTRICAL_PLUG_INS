using Autodesk.AutoCAD.Runtime;using System.Windows.Forms;using AcApp=Autodesk.AutoCAD.ApplicationServices.Application;
[assembly:ExtensionApplication(typeof(LSE.AutoCADElectrical.Librarian.Plugin))][assembly:CommandClass(typeof(LSE.AutoCADElectrical.Librarian.Commands))]
namespace LSE.AutoCADElectrical.Librarian;
public sealed class Plugin:IExtensionApplication{public void Initialize(){}public void Terminate(){}}
public sealed class Commands{
 [CommandMethod("LSELIBRARIAN",CommandFlags.Modal)]public void Librarian(){Open(null);}
 [CommandMethod("LSELIBRARYAUDIT",CommandFlags.Modal)]public void Audit(){try{string? pick;using(var f=new AuditForm()){AcApp.ShowModalDialog(f);pick=f.OpenInLibrarian;}if(pick is not null)Open(pick);}catch(System.Exception ex){MessageBox.Show(ex.ToString(),"LSE Librarian");}}
 [CommandMethod("LSECATALOGADD",CommandFlags.Modal)]public void CatalogAdd(){try{using var f=new CatalogRecordForm(SettingsService.Load(),false,null,null);AcApp.ShowModalDialog(f);}catch(System.Exception ex){MessageBox.Show(ex.ToString(),"LSE Librarian");}}
 [CommandMethod("LSEFOOTPRINTMAP",CommandFlags.Modal)]public void FootprintMap(){try{using var f=new FootprintMapForm(SettingsService.Load());AcApp.ShowModalDialog(f);}catch(System.Exception ex){MessageBox.Show(ex.ToString(),"LSE Librarian");}}
 [CommandMethod("LSELIBRARYSETTINGS",CommandFlags.Modal)]public void Settings(){try{using var f=new LibrarySettingsForm();AcApp.ShowModalDialog(f);}catch(System.Exception ex){MessageBox.Show(ex.ToString(),"LSE Librarian");}}
 [CommandMethod("LSELIBRARYDIAG",CommandFlags.Modal)]public void Diag(){try{MessageBox.Show(DiagnosticsService.BuildReport(),"LSE Librarian Diagnostics");}catch(System.Exception ex){MessageBox.Show(ex.ToString(),"LSE Librarian");}}
 static void Open(string? source){try{using var f=new LibrarianForm(source);AcApp.ShowModalDialog(f);}catch(System.Exception ex){MessageBox.Show(ex.ToString(),"LSE Librarian");}}
}
