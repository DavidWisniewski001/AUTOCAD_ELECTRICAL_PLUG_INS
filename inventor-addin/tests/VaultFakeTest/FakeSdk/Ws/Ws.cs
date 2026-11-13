namespace Autodesk.Connectivity.WebServices{
 public sealed class ServerIdentities{ public string DataServer{get;set;}=""; public string FileServer{get;set;}=""; }
 public enum LicensingAgent{None,Client,Server}
 public enum FileClassification{None,DesignVisualization,DesignDocument}
 public enum CheckoutFileOptions{Master,Branch}
 public sealed class ByteArray{ public byte[] Bytes=Array.Empty<byte>(); }
 public sealed class User{ public long Id; public string Name=""; }
 public sealed class Session{ public User User=new(); }
 public sealed class File{ public long Id,MasterId,CkOutUserId,FileSize,VerNum; public string Name="",CkOutMach=""; public DateTime ModDate; }
 // in-memory vault: one file, with a "server copy" on disk so download/check-in can be verified
 public static class FakeVault{ public static string VaultPath="$/x/default_cat.mdb"; public static string ServerCopy=""; public static long CkOutUser; public static string CkOutMach=""; public static int Version=3; public static readonly List<string> Calls=new(); public static long Me=42;
  public static File Snapshot(){ var fi=new FileInfo(ServerCopy); return new File{Id=1001,MasterId=501,Name="default_cat.mdb",VerNum=Version,CkOutUserId=CkOutUser,CkOutMach=CkOutMach,FileSize=fi.Exists?fi.Length:0,ModDate=fi.Exists?fi.LastWriteTime:DateTime.MinValue}; } }
 public sealed class SecurityService{ public Session Session=new(){User=new(){Id=FakeVault.Me,Name="dwisniewski"}}; }
 public sealed class AdminService{ public User GetUserByUserId(long id)=>new(){Id=id,Name=id==FakeVault.Me?"dwisniewski":"other.user"}; }
 public sealed class DocumentService{
  public File[] FindLatestFilesByPaths(string[] paths){ FakeVault.Calls.Add("FindLatestFilesByPaths "+string.Join(",",paths)); return paths.Select(p=>p==FakeVault.VaultPath?FakeVault.Snapshot():new File{Id=-1}).ToArray(); }
  public File CheckoutFile(long fileId,CheckoutFileOptions opt,string machine,string localPath,string comment,out ByteArray ticket){ FakeVault.Calls.Add($"DocumentService.CheckoutFile {fileId} {opt} {machine} {localPath} {comment}"); if(FakeVault.CkOutUser!=0)throw new InvalidOperationException("already checked out"); FakeVault.CkOutUser=FakeVault.Me; FakeVault.CkOutMach=machine; ticket=new ByteArray(); return FakeVault.Snapshot(); }
  public File UndoCheckoutFile(long masterId,out ByteArray ticket){ FakeVault.Calls.Add("DocumentService.UndoCheckoutFile "+masterId); FakeVault.CkOutUser=0; ticket=new ByteArray(); return FakeVault.Snapshot(); }
 }
}
namespace Autodesk.Connectivity.WebServicesTools{
 using Autodesk.Connectivity.WebServices;
 public sealed class WinAuthCredentials{ public ServerIdentities Ids; public string Vault; public LicensingAgent Agent; public WinAuthCredentials(ServerIdentities ids,string vaultName,object? licensingAgreement,LicensingAgent agent){Ids=ids;Vault=vaultName;Agent=agent;FakeVault.Calls.Add($"WinAuthCredentials {ids.DataServer} {vaultName} {licensingAgreement??"null"} {agent}");} }
 public sealed class WebServiceManager:IDisposable{ public DocumentService DocumentService=new(); public SecurityService SecurityService=new(); public AdminService AdminService=new(); public bool Disposed; public WebServiceManager(WinAuthCredentials c){ if(c.Agent!=LicensingAgent.Client)throw new InvalidOperationException("bad agent"); FakeVault.Calls.Add("WebServiceManager(WinAuth)"); } public void Dispose(){Disposed=true;FakeVault.Calls.Add("WebServiceManager.Dispose");} }
}
