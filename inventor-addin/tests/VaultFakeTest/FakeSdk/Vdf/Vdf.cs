using Autodesk.Connectivity.WebServices; using Autodesk.Connectivity.WebServicesTools; using WsFile=Autodesk.Connectivity.WebServices.File; using Autodesk.DataManagement.Client.Framework.Currency;
namespace Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections{
 public enum AuthenticationFlags{Standard=0,WindowsAuthentication=1,ReadOnly=2}
 public sealed class Connection{ public WebServiceManager WebServiceManager{get;} public string Vault{get;} public long UserId{get;} public string Server{get;} public string UserName=>"dwisniewski"; public Services.FileManager FileManager{get;}
  public Connection(WebServiceManager wsm,string vaultName,long userId,string server,AuthenticationFlags flags){WebServiceManager=wsm;Vault=vaultName;UserId=userId;Server=server;FileManager=new(this);FakeVault.Calls.Add($"Connection {vaultName} {userId} {server} {flags}");} }
}
namespace Autodesk.DataManagement.Client.Framework.Vault.Currency.Entities{
 public sealed class FileIteration{ public Connections.Connection Connection; public WsFile File; public long VersionNumber=>File.VerNum; public FileIteration(Connections.Connection c,WsFile f){Connection=c;File=f;} }
}
namespace Autodesk.DataManagement.Client.Framework.Vault.Settings{
 public sealed class AcquireFilesSettings{
  [Flags] public enum AcquisitionOption{None=0,Download=1,Checkout=2}
  public sealed class RelOpts{ public sealed class FileRel{ public bool IncludeChildren=true,IncludeParents=true,IncludeAttachments=true; } public FileRel FileRelationshipSettings=new(); }
  public Currency.Connections.Connection Connection; public List<object> Entities=new(); public AcquisitionOption DefaultAcquisitionOption; public FolderPathAbsolute? LocalPath; public RelOpts OptionsRelationshipGathering=new();
  public AcquireFilesSettings(Currency.Connections.Connection c){Connection=c;}
  public void AddEntityToAcquire(object e)=>Entities.Add(e);
 }
}
namespace Autodesk.DataManagement.Client.Framework.Vault.Results{
 public sealed class FileAcquisitionResult{ public Exception? Exception; public FilePathAbsolute? LocalPath; public Currency.Entities.FileIteration? File; }
 public sealed class AcquireFilesResults{ public List<FileAcquisitionResult> FileResults=new(); }
}
namespace Autodesk.DataManagement.Client.Framework.Vault.Services{
 using Currency.Entities; using Settings; using Results;
 public sealed class FileManager{ readonly Currency.Connections.Connection c; public FileManager(Currency.Connections.Connection c){this.c=c;}
  public static bool FailAcquire; public AcquireFilesResults AcquireFiles(AcquireFilesSettings s){ if(FailAcquire)throw new InvalidOperationException("simulated VDF failure"); var o=s.DefaultAcquisitionOption; FakeVault.Calls.Add($"AcquireFiles {o} children={s.OptionsRelationshipGathering.FileRelationshipSettings.IncludeChildren} local={(s.LocalPath?.FullPath??"(working folder)")}"); var r=new AcquireFilesResults(); foreach(FileIteration it in s.Entities){ var fr=new FileAcquisitionResult{File=it}; if(o.HasFlag(AcquireFilesSettings.AcquisitionOption.Checkout)){ if(FakeVault.CkOutUser!=0){fr.Exception=new InvalidOperationException("already checked out");r.FileResults.Add(fr);continue;} FakeVault.CkOutUser=FakeVault.Me; FakeVault.CkOutMach=Environment.MachineName; } if(o.HasFlag(AcquireFilesSettings.AcquisitionOption.Download)){ string dir=s.LocalPath?.FullPath??Path.GetTempPath(); string dst=Path.Combine(dir,"default_cat.mdb"); if(System.IO.File.Exists(dst))System.IO.File.SetAttributes(dst,FileAttributes.Normal); System.IO.File.Copy(FakeVault.ServerCopy,dst,true); System.IO.File.SetLastWriteTime(dst,System.IO.File.GetLastWriteTime(FakeVault.ServerCopy)); System.IO.File.SetAttributes(dst,FileAttributes.ReadOnly); fr.LocalPath=new FilePathAbsolute(dst);} r.FileResults.Add(fr);} return r; }
  public FileIteration CheckinFile(FileIteration it,string comment,bool keepCheckedOut,IEnumerable<object>? associations,object? bom,bool copyBom,string? newFileName,FileClassification cls,bool hidden,FilePathAbsolute path){ FakeVault.Calls.Add($"CheckinFile '{comment}' keep={keepCheckedOut} cls={cls} newName={newFileName??"null"} path={path.FullPath}"); if(FakeVault.CkOutUser==0)throw new InvalidOperationException("not checked out"); System.IO.File.Copy(path.FullPath,FakeVault.ServerCopy,true); FakeVault.Version++; FakeVault.CkOutUser=0; System.IO.File.SetAttributes(path.FullPath,FileAttributes.ReadOnly); return new FileIteration(c,FakeVault.Snapshot()); }
  public FileIteration CheckinFile(FileIteration it,string comment,bool keepCheckedOut,DateTime lastWrite,IEnumerable<object>? associations,object? bom,bool copyBom,string? newFileName,FileClassification cls,bool hidden,Stream stream)=>throw new InvalidOperationException("stream overload must not be chosen");
  public FileIteration UndoCheckoutFile(FileIteration it,out Stream? stream){ FakeVault.Calls.Add("FileManager.UndoCheckoutFile"); stream=null; FakeVault.CkOutUser=0; return new FileIteration(c,FakeVault.Snapshot()); }
 }
}
namespace Autodesk.DataManagement.Client.Framework.Vault.Library{
 public static class ConnectionManager{ public static bool LogOut(Currency.Connections.Connection c){FakeVault.Calls.Add("ConnectionManager.LogOut");return true;} }
}
