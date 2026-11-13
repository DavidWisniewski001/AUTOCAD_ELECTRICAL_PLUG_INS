using System.Runtime.InteropServices;
using System.Windows.Forms;
using Inv = Inventor;
namespace LSE.InventorFootprintTools;
[ComVisible(true)][Guid("55D0C0CC-1B4A-49A9-8008-A4FBFF38A7AC")]
public sealed class StandardAddInServer:Inv.ApplicationAddInServer{
 public const string ClientId="{55D0C0CC-1B4A-49A9-8008-A4FBFF38A7AC}";
 Inv.Application? app; Inv.ButtonDefinition? wizardButton,settingsButton; FootprintWizardForm? wizard;
 public void Activate(Inv.ApplicationAddInSite site,bool firstTime){app=site.Application;var defs=app.CommandManager.ControlDefinitions;
  wizardButton=defs.AddButtonDefinition("LSE Footprint","LSE_FootprintTools_Wizard",Inv.CommandTypesEnum.kNonShapeEditCmdType,ClientId,"Import an STL/STEP model, pick the view to project, generate an AutoCAD Electrical footprint and issue an LSE part number.","LSE footprint + part number",null,null,Inv.ButtonDisplayEnum.kDisplayTextInLearningMode);wizardButton.OnExecute+=_=>Run(OpenWizard);
  settingsButton=defs.AddButtonDefinition("LSE Settings","LSE_FootprintTools_Settings",Inv.CommandTypesEnum.kNonShapeEditCmdType,ClientId,"Catalog, footprint library, naming standard and Vault settings for LSE Footprint Tools.","LSE Footprint Tools settings",null,null,Inv.ButtonDisplayEnum.kDisplayTextInLearningMode);settingsButton.OnExecute+=_=>Run(OpenSettings);
  if(!firstTime)return;foreach(string ribbon in new[]{"ZeroDoc","Part","Assembly","Drawing"})try{var tab=app.UserInterfaceManager.Ribbons[ribbon].RibbonTabs["id_TabTools"];var panel=tab.RibbonPanels.Add("LSE","LSE_FootprintTools_Panel_"+ribbon,ClientId,"",false);panel.CommandControls.AddButton(wizardButton,true,true,"",false);panel.CommandControls.AddButton(settingsButton,false,true,"",false);}catch{}}
 public void Deactivate(){try{wizard?.Close();}catch{}wizard=null;Ace.Release(wizardButton);Ace.Release(settingsButton);wizardButton=null;settingsButton=null;app=null;GC.Collect();GC.WaitForPendingFinalizers();}
 public void ExecuteCommand(int commandId){}
 public object Automation=>null!;
 static void Run(Action a){try{a();}catch(Exception ex){MessageBox.Show(ex.ToString(),"LSE Footprint Tools");}}
 void OpenWizard(){if(wizard is {IsDisposed:false}){wizard.Activate();return;}wizard=new FootprintWizardForm(app!);wizard.FormClosed+=(_,_)=>wizard=null;wizard.Show(new InventorWindow(app!));}
 void OpenSettings(){using var f=new SettingsForm();f.ShowDialog(new InventorWindow(app!));}
}
