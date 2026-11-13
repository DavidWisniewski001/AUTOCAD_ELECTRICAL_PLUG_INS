using FakeVault=Autodesk.Connectivity.WebServices.FakeVault; using ServerIdentities=Autodesk.Connectivity.WebServices.ServerIdentities; using LicensingAgent=Autodesk.Connectivity.WebServices.LicensingAgent;
namespace LSE.AutoCADElectrical.CatalogTools;
public sealed class AppSettings{ public string CatalogPath{get;set;}=""; public string VaultExplorerPath{get;set;}=""; public string VaultServer{get;set;}="http://lse-cep-sql"; public string VaultName{get;set;}="Lake_Shore_E_Vault"; public string CatalogVaultPath{get;set;}=@"$\x\default_cat.mdb"; }
public static class Program{
 static int fails=0; static void Check(string name,bool ok){Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(!ok)fails++;}
 static string Calls()=>string.Join(" | ",FakeVault.Calls);
 public static int Main(string[] args){
  string explorer=args.Length>0?args[0]:AppContext.BaseDirectory;string work=Path.Combine(Path.GetTempPath(),"lse-vault-faketest");if(Directory.Exists(work)){foreach(var f in Directory.GetFiles(work,"*",SearchOption.AllDirectories))File.SetAttributes(f,FileAttributes.Normal);Directory.Delete(work,true);}Directory.CreateDirectory(work);
  string server=Path.Combine(work,"server");Directory.CreateDirectory(server);FakeVault.ServerCopy=Path.Combine(server,"default_cat.mdb");File.WriteAllText(FakeVault.ServerCopy,"VERSION3");File.SetLastWriteTime(FakeVault.ServerCopy,new DateTime(2026,11,1,8,0,0));
  string local=Path.Combine(work,"wf","default_cat.mdb");Directory.CreateDirectory(Path.GetDirectoryName(local)!);
  var s=new AppSettings{CatalogPath=local,VaultExplorerPath=explorer};
  // ---------- scenario A: no add-in session, no Forms dll => Windows authentication; working copy is a stale read-only Vault copy
  File.WriteAllText(local,"VERSION2");File.SetLastWriteTime(local,new DateTime(2026,10,1,8,0,0));File.SetAttributes(local,FileAttributes.ReadOnly);
  var log=new List<string>();
  VaultCatalogService.Checkout(s,log);Console.WriteLine(string.Join("\n  ",log));Console.WriteLine("  calls: "+Calls());
  Check("A checkout: WinAuth credentials built with cleaned server and Client agent",FakeVault.Calls.Any(c=>c=="WinAuthCredentials lse-cep-sql Lake_Shore_E_Vault null Client"));
  Check("A checkout: Connection built with user id 42 and WindowsAuthentication",FakeVault.Calls.Any(c=>c=="Connection Lake_Shore_E_Vault 42 lse-cep-sql WindowsAuthentication"));
  Check("A checkout: normalized vault path used",FakeVault.Calls.Any(c=>c=="FindLatestFilesByPaths $/x/default_cat.mdb"));
  Check("A checkout: AcquireFiles with Checkout+Download, children off, local folder",FakeVault.Calls.Any(c=>c.StartsWith("AcquireFiles Download, Checkout children=False local="+Path.GetDirectoryName(local))));
  Check("A checkout: vault now checked out to me",FakeVault.CkOutUser==42);
  Check("A checkout: working copy refreshed to Vault version and writable",File.ReadAllText(local)=="VERSION3"&&(File.GetAttributes(local)&FileAttributes.ReadOnly)==0);
  Check("A checkout: log says differs + downloaded",log.Any(l=>l.Contains("differs from the Vault version"))&&log.Any(l=>l.Contains("download")&&l.Contains("PASS")));
  File.WriteAllText(local,"VERSION3+NEWROW");
  log.Clear();VaultCatalogService.Checkin(s,"Add electrical catalog component TEST 123",log);Console.WriteLine(string.Join("\n  ",log));Console.WriteLine("  calls: "+Calls());
  Check("A checkin: FilePathAbsolute overload, comment in place, None classification",FakeVault.Calls.Any(c=>c.StartsWith("CheckinFile 'Add electrical catalog component TEST 123' keep=False cls=None newName=null path="+local)));
  Check("A checkin: server copy updated, version 4, not checked out",File.ReadAllText(FakeVault.ServerCopy)=="VERSION3+NEWROW"&&FakeVault.Version==4&&FakeVault.CkOutUser==0);
  Check("A checkin: log reports version 4",log.Any(l=>l.Contains("Vault check-in: PASS")&&l.Contains("version 4")));
  Check("A session released after checkin (LogOut)",FakeVault.Calls.Count(c=>c=="ConnectionManager.LogOut")==1);
  // ---------- scenario B: identical working copy => checkout without download, then undo
  FakeVault.Calls.Clear();File.SetAttributes(local,FileAttributes.ReadOnly);File.SetLastWriteTime(local,File.GetLastWriteTime(FakeVault.ServerCopy));
  log.Clear();VaultCatalogService.Checkout(s,log);Console.WriteLine(string.Join("\n  ",log));
  Check("B checkout: no download when size+date match",FakeVault.Calls.Any(c=>c.StartsWith("AcquireFiles Checkout children=False"))&&log.Any(l=>l.Contains("already current")));
  Check("B checkout: a second session was opened (first was released)",FakeVault.Calls.Count(c=>c=="WebServiceManager(WinAuth)")==1);
  log.Clear();VaultCatalogService.UndoCheckout(s,log);Console.WriteLine(string.Join("\n  ",log));
  Check("B undo: FileManager.UndoCheckoutFile with out param handled",FakeVault.Calls.Contains("FileManager.UndoCheckoutFile")&&FakeVault.CkOutUser==0);
  // ---------- scenario C: checked out by someone else => refuse, nothing changed
  FakeVault.Calls.Clear();FakeVault.CkOutUser=7;FakeVault.CkOutMach="PC-OTHER";log.Clear();
  try{VaultCatalogService.Checkout(s,log);Check("C refuses when checked out by another user",false);}catch(InvalidOperationException ex){Console.WriteLine("  "+ex.Message);Check("C refuses when checked out by another user, names them",ex.Message.Contains("other.user")&&ex.Message.Contains("PC-OTHER")&&ex.Message.Contains("Nothing was changed"));}
  Check("C session released after the refusal",FakeVault.Calls.Count(c=>c=="ConnectionManager.LogOut")==1);
  FakeVault.CkOutUser=0;
  // ---------- scenario D: wrong vault path => clear error
  FakeVault.Calls.Clear();var s2=new AppSettings{CatalogPath=local,VaultExplorerPath=explorer,CatalogVaultPath="$/wrong/default_cat.mdb"};
  try{VaultCatalogService.Checkout(s2,new List<string>());Check("D wrong path",false);}catch(FileNotFoundException ex){Check("D wrong path gives FileNotFound with the setting hint",ex.Message.Contains("$/wrong/default_cat.mdb")&&ex.Message.Contains("Catalog Vault path"));}
  // ---------- scenario E: TestReport end to end
  FakeVault.Calls.Clear();string rep=VaultCatalogService.TestReport(s);Console.WriteLine(rep);
  Check("E TestReport PASS with version and 'not checked out'",rep.Contains("Vault test: PASS")&&rep.Contains("version 4")&&rep.Contains("not checked out")&&rep.Contains("Windows authentication"));
  Check("E TestReport released its session",FakeVault.Calls.Count(c=>c=="ConnectionManager.LogOut")==1);
  // ---------- scenario G: working copy stale AND the Vault Framework fails => refuse (no DocumentService fallback), nothing checked out
  FakeVault.Calls.Clear();Autodesk.DataManagement.Client.Framework.Vault.Services.FileManager.FailAcquire=true;File.SetAttributes(local,FileAttributes.Normal);File.WriteAllText(local,"OLD");File.SetLastWriteTime(local,new DateTime(2026,9,1));File.SetAttributes(local,FileAttributes.ReadOnly);log.Clear();
  try{VaultCatalogService.Checkout(s,log);Check("G stale + VDF failure refused",false);}catch(InvalidOperationException ex){Console.WriteLine("  "+ex.Message);Check("G stale + VDF failure refused with Get Latest hint, nothing checked out",ex.Message.Contains("Get Latest Version")&&FakeVault.CkOutUser==0&&!FakeVault.Calls.Any(c=>c.StartsWith("DocumentService.CheckoutFile")));}
  // ---------- scenario H: working copy current AND the Vault Framework fails => DocumentService fallback is safe
  FakeVault.Calls.Clear();File.SetAttributes(local,FileAttributes.Normal);File.Copy(FakeVault.ServerCopy,local,true);File.SetLastWriteTime(local,File.GetLastWriteTime(FakeVault.ServerCopy));File.SetAttributes(local,FileAttributes.ReadOnly);log.Clear();
  VaultCatalogService.Checkout(s,log);Console.WriteLine(string.Join(" / ",log));
  Check("H current + VDF failure falls back to DocumentService.CheckoutFile with Master/machine/path/comment",FakeVault.Calls.Any(c=>c=="DocumentService.CheckoutFile 1001 Master "+Environment.MachineName+" "+local+" "+VaultCatalogService.AppName)&&FakeVault.CkOutUser==42&&(File.GetAttributes(local)&FileAttributes.ReadOnly)==0);
  Autodesk.DataManagement.Client.Framework.Vault.Services.FileManager.FailAcquire=false;log.Clear();VaultCatalogService.UndoCheckout(s,log);
  Check("H undo after raw checkout",FakeVault.CkOutUser==0);
  // ---------- scenario F: the Vault add-in session is present => it is used, no login, no logout
  FakeVault.Calls.Clear();SetupAddin(server);
  Console.WriteLine(VaultProbe.AddinLine());
  Check("F probe sees the add-in session",VaultProbe.AddinLine().Contains("logged in as dwisniewski in vault Lake_Shore_E_Vault on lse-cep-sql"));
  log.Clear();VaultCatalogService.Checkout(s,log);VaultCatalogService.Checkin(s,"via add-in",log);Console.WriteLine(string.Join("\n  ",log));
  Check("F add-in session used, no WinAuth, no LogOut",log.Any(l=>l.Contains("Vault add-in's session"))&&!FakeVault.Calls.Any(c=>c.StartsWith("WinAuth"))&&!FakeVault.Calls.Contains("ConnectionManager.LogOut")&&FakeVault.Version==5);
  Console.WriteLine(fails==0?"ALL PASS":fails+" FAILED");return fails;
 }
 static void SetupAddin(string server){
  var ids=new ServerIdentities{DataServer="lse-cep-sql",FileServer="lse-cep-sql"};var wsm=new Autodesk.Connectivity.WebServicesTools.WebServiceManager(new Autodesk.Connectivity.WebServicesTools.WinAuthCredentials(ids,"Lake_Shore_E_Vault",null,LicensingAgent.Client));
  FakeVault.Calls.Clear();
  Connectivity.Application.VaultBase.ConnectionManager.Instance.Connection=new Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections.Connection(wsm,"Lake_Shore_E_Vault",42,"lse-cep-sql",Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections.AuthenticationFlags.Standard);
  FakeVault.Calls.Clear();
 }
}
