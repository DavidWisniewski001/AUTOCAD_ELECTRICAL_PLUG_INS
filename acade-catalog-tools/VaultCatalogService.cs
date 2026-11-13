using System.Reflection;
using Microsoft.CSharp.RuntimeBinder;
namespace LSE.AutoCADElectrical.CatalogTools;

// Vault 2026 bridge for default_cat.mdb. Nothing from the Vault SDK is referenced at compile time: the assemblies are taken
// from the running process (the Autodesk Vault add-in for AutoCAD / Inventor already has them loaded) or loaded from the
// Vault Client Explorer folder, and every call goes through reflection / dynamic.
//
// Connection order (Vault 2025/2026 shapes, after Autodesk's API-Onboarding-Sample--Vault-Logins, branch 2025):
//   1. the session the Vault add-in already holds: Connectivity.Application.VaultBase.ConnectionManager.Instance.Connection
//   2. the Vault login dialog: Autodesk.DataManagement.Client.Framework.Vault.Forms.Library.Login(LoginSettings)
//      (handles Vault accounts, Windows accounts and Autodesk ID; remembers the last server and vault)
//   3. Windows authentication without a dialog: new WinAuthCredentials(ServerIdentities, vault, null, LicensingAgent.Client)
// File operations: FileManager.AcquireFiles (check out + download the latest version), FileManager.CheckinFile,
// FileManager.UndoCheckoutFile, each with the raw DocumentService call as the fallback. Out parameters are handled through
// MethodInfo.Invoke, never through dynamic. Every value handed to a dynamic call is itself dynamic so the binder sees its
// real type (an object-typed argument does not bind).
public static class VaultCatalogService{
 public const string AppName="LSE Catalog Administration";
 const string WsName="Autodesk.Connectivity.WebServices",FwName="Autodesk.DataManagement.Client.Framework",VdfName=FwName+".Vault",FormsName=FwName+".Vault.Forms";
 static VaultSession? cached;
 public static bool Enabled(AppSettings s)=>!string.IsNullOrWhiteSpace(s.CatalogVaultPath);
 public static string NormalizeVaultPath(string p){p=(p??"").Trim().Replace('\\','/');if(p.Length==0)return "";if(p.StartsWith("$/"))return p;if(p.StartsWith("/"))return "$"+p;if(p.StartsWith("$"))return "$/"+p[1..].TrimStart('/');return "$/"+p;}
 static string CleanServer(string s){s=(s??"").Trim();foreach(string pre in new[]{"https://","http://"})if(s.StartsWith(pre,StringComparison.OrdinalIgnoreCase))s=s[pre.Length..];return s.TrimEnd('/');}

 // ------------------------------------------------------------------ public operations (called by the transactions) ----
 // Checks the catalog out to this user and makes sure the working copy is the latest Vault version, so a stale working copy
 // can never be checked in over someone else's newer catalog.
 public static void Checkout(AppSettings s,List<string>? log=null){
  log??=new();var v=Begin(s,log);
  try{
   string vaultPath=NormalizeVaultPath(s.CatalogVaultPath);dynamic f=FindFile(v,vaultPath);long me=MyId(v),ck=Convert.ToInt64(f.CkOutUserId);
   log.Add($"Vault file: {vaultPath} version {Str(f,"VerNum")} ({Str(f,"FileSize")} bytes, modified {Str(f,"ModDate")})");
   if(ck>0&&ck!=me)throw new InvalidOperationException($"The catalog is checked out in Vault by {UserName(v,ck)} (machine {Str(f,"CkOutMach")}). Wait until it is checked in, then try again. Nothing was changed.");
   bool download=NeedsDownload(f,s.CatalogPath,log);
   if(ck>0){
    log.Add("Vault check-out: the catalog was already checked out to you; that check-out is used");
    if(download)try{Acquire(v,f,s.CatalogPath,false,true,log);}catch(Exception ex){throw new InvalidOperationException("The working copy is older than the Vault version and the download failed ("+Root(ex).Message+"). In Vault Explorer do Get Latest Version on the catalog, then try again. Nothing was changed.",ex);}
   }
   else{
    try{Acquire(v,f,s.CatalogPath,true,download,log);}
    catch(Exception ex){
     if(download)throw new InvalidOperationException("Vault check-out through the Vault Framework failed ("+Root(ex).Message+") and the working copy is older than the Vault version, so the catalog was not checked out another way. In Vault Explorer do Get Latest Version (or Check Out) on the catalog, then try again. Nothing was changed.",ex);
     log.Add("Vault check-out through the Vault Framework failed ("+Root(ex).Message+"); the working copy is current, so the DocumentService check-out is used instead");RawCheckout(v,f,s.CatalogPath,log);}
    dynamic again=FindFile(v,vaultPath);if(Convert.ToInt64(again.CkOutUserId)<=0)throw new InvalidOperationException("Vault returned without an error but the catalog is still not checked out. Nothing was changed.");
   }
   if(File.Exists(s.CatalogPath)&&(File.GetAttributes(s.CatalogPath)&FileAttributes.ReadOnly)!=0)File.SetAttributes(s.CatalogPath,File.GetAttributes(s.CatalogPath)&~FileAttributes.ReadOnly);
   cached=v;
  }
  catch{End(v);throw;}
 }
 public static void Checkin(AppSettings s,string comment,List<string>? log=null){
  log??=new();var v=Begin(s,log);
  try{
   dynamic f=FindFile(v,NormalizeVaultPath(s.CatalogVaultPath));if(Convert.ToInt64(f.CkOutUserId)<=0)throw new InvalidOperationException("The catalog is not checked out in Vault, so it cannot be checked in. The working copy holds the new record; check it in by hand from Vault Explorer.");
   object fm=(object)v.Connection.FileManager;object it=NewFileIteration(v,f);object path=Activator.CreateInstance(v.Fw.GetType(FwName+".Currency.FilePathAbsolute")!,s.CatalogPath)!;
   Type clsT=v.Ws.GetType(WsName+".FileClassification")!;object none=Enum.Parse(clsT,"None");Exception? last=null;
   foreach(MethodInfo m in fm.GetType().GetMethods().Where(x=>x.Name=="CheckinFile"&&x.GetParameters().Length>0&&x.GetParameters()[^1].ParameterType.Name=="FilePathAbsolute").OrderBy(x=>x.GetParameters().Length)){
    var ps=m.GetParameters();var args=new object?[ps.Length];bool firstString=true;
    for(int i=0;i<ps.Length;i++){Type t=ps[i].ParameterType;string n=ps[i].Name??"";
     if(Fits(t,it))args[i]=it;else if(Fits(t,path))args[i]=path;else if(t==clsT)args[i]=none;
     else if(t==typeof(string)){if(n.Contains("comment",StringComparison.OrdinalIgnoreCase)||firstString){args[i]=comment;firstString=false;}else args[i]=null;}
     else if(t==typeof(bool))args[i]=false;else if(t==typeof(DateTime))args[i]=File.GetLastWriteTime(s.CatalogPath);else if(t.IsValueType)args[i]=Activator.CreateInstance(t);else args[i]=null;}
    try{object? r=m.Invoke(fm,args);log.Add("Vault check-in: PASS"+(r is null?"":" (now version "+Str(r,"VersionNumber")+")"));End(v);return;}
    catch(TargetInvocationException tx){last=tx.InnerException??tx;}catch(Exception ex){last=ex;}
   }
   throw new InvalidOperationException("Vault check-in failed: "+(last is null?"no CheckinFile(... FilePathAbsolute) method was found on the Vault FileManager":Root(last).Message)+". The working copy holds the new record and is still checked out to you: check it in from Vault Explorer.",last);
  }
  catch{End(v);throw;}
 }
 public static void UndoCheckout(AppSettings s,List<string>? log=null){
  log??=new();var v=Begin(s,log);
  try{
   dynamic f=FindFile(v,NormalizeVaultPath(s.CatalogVaultPath));if(Convert.ToInt64(f.CkOutUserId)<=0){log.Add("Vault undo check-out: the catalog is not checked out; nothing to undo");return;}
   Exception? last=null;object it=NewFileIteration(v,f);
   try{object fm=(object)v.Connection.FileManager;
    foreach(MethodInfo m in fm.GetType().GetMethods().Where(x=>x.Name=="UndoCheckoutFile").OrderBy(x=>x.GetParameters().Length)){
     var ps=m.GetParameters();var args=ps.Select(p=>Fits(p.ParameterType,it)?it:p.IsOut?null:p.ParameterType==typeof(bool)?false:p.ParameterType.IsValueType?Activator.CreateInstance(p.ParameterType):null).ToArray();
     try{m.Invoke(fm,args);log.Add("Vault undo check-out: PASS");return;}catch(TargetInvocationException tx){last=tx.InnerException??tx;}catch(Exception ex){last=ex;}}}
   catch(Exception ex){last=ex;}
   object doc=(object)v.Wsm.DocumentService;long master=Convert.ToInt64(f.MasterId);
   foreach(MethodInfo m in doc.GetType().GetMethods().Where(x=>x.Name=="UndoCheckoutFile").OrderBy(x=>x.GetParameters().Length)){
    var ps=m.GetParameters();var args=ps.Select(p=>p.IsOut?null:p.ParameterType==typeof(long)?(object)master:p.ParameterType==typeof(string)?Environment.MachineName:p.ParameterType.IsValueType?Activator.CreateInstance(p.ParameterType):null).ToArray();
    try{m.Invoke(doc,args);log.Add("Vault undo check-out (DocumentService): PASS");return;}catch(TargetInvocationException tx){last=tx.InnerException??tx;}catch(Exception ex){last=ex;}}
   throw new InvalidOperationException("Vault undo check-out failed: "+(last is null?"no UndoCheckoutFile method was found":Root(last).Message)+". Undo the check-out of the catalog in Vault Explorer by hand.",last);
  }
  finally{End(v);}
 }
 // Call when a transaction ends without Checkin/UndoCheckout (e.g. Vault was never reached) to drop any session this class opened.
 public static void EndSession(){var v=cached;cached=null;v?.Release();}

 // ------------------------------------------------------------------------------------------------- diagnostics ----
 // Never throws. Used by the Settings "Test" button and the diagnostics command.
 public static string TestReport(AppSettings s,bool connect=true){
  var x=new List<string>{VaultProbe.RuntimeLine(),VaultProbe.ClientLine(s.VaultExplorerPath),VaultProbe.AddinLine()};
  x.Add("Vault server: "+CleanServer(s.VaultServer)+"   vault: "+s.VaultName);
  if(!Enabled(s)){x.Add("Catalog Vault path: (blank) - Vault is OFF: the catalog is written in place without check-out/check-in.");x.Add("Working copy: "+s.CatalogPath+(File.Exists(s.CatalogPath)?(VaultProbe.IsReadOnly(s.CatalogPath)?" (READ-ONLY: Vault has it locked; check it out before adding records, or fill in the Catalog Vault path so the tools do it)":" (writable)"):" (MISSING)"));return string.Join(Environment.NewLine,x);}
  x.Add("Catalog Vault path: "+NormalizeVaultPath(s.CatalogVaultPath));x.Add("Working copy: "+s.CatalogPath+(File.Exists(s.CatalogPath)?(VaultProbe.IsReadOnly(s.CatalogPath)?" (read-only, as Vault leaves it when not checked out)":" (writable)"):" (MISSING)"));
  if(!connect)return string.Join(Environment.NewLine,x);
  var log=new List<string>();VaultSession? v=null;
  try{v=Connect(s,log);x.AddRange(log);x.Add("Vault connection: PASS ("+v.Source+", "+v.Describe()+")");
   dynamic f=FindFile(v,NormalizeVaultPath(s.CatalogVaultPath));long ck=Convert.ToInt64(f.CkOutUserId);
   x.Add($"Catalog in Vault: PASS - version {Str(f,"VerNum")}, {Str(f,"FileSize")} bytes, modified {Str(f,"ModDate")}, "+(ck>0?"CHECKED OUT by "+UserName(v,ck)+" on "+Str(f,"CkOutMach"):"not checked out"));
   var note=new List<string>();bool stale=NeedsDownload(f,s.CatalogPath,note);x.AddRange(note);x.Add(stale?"Working copy vs Vault: differs (the Vault version will be downloaded at the next check-out)":"Working copy vs Vault: same size and date");
   x.Add("Vault test: PASS");}
  catch(Exception ex){x.AddRange(log);x.Add("Vault test: FAIL - "+Root(ex).Message);}
  finally{if(v is not null&&!ReferenceEquals(v,cached))v.Release();}
  return string.Join(Environment.NewLine,x);
 }

 // ------------------------------------------------------------------------------------------------- connection ----
 public sealed class VaultSession{
  public dynamic Connection=null!;public string Source="";public bool Owned;public Assembly Ws=null!,Fw=null!,Vdf=null!;
  public dynamic Wsm=>Connection.WebServiceManager;
  public string Describe()=>VaultProbe.Describe((object)Connection);
  public void Release(){if(!Owned)return;Owned=false;try{var cm=Vdf.GetType(VdfName+".Library.ConnectionManager");var m=cm?.GetMethods(BindingFlags.Public|BindingFlags.Static).FirstOrDefault(x=>x.Name=="LogOut"&&x.GetParameters().Length==1);if(m is not null){m.Invoke(null,new object[]{(object)Connection});return;}}catch{}try{Wsm.Dispose();}catch{}}
 }
 static VaultSession Begin(AppSettings s,List<string> log){if(cached is not null){try{_=cached.Wsm;return cached;}catch{cached=null;}}return Connect(s,log);}
 static void End(VaultSession v){if(ReferenceEquals(v,cached))cached=null;v.Release();}
 public static VaultSession Connect(AppSettings s,List<string> log){
  if(!Enabled(s))throw new InvalidOperationException("Catalog Vault path is blank (Settings), so Vault is off.");
  // 1. the Vault add-in's own session
  object? addin=VaultProbe.AddinConnection();
  if(addin is not null){var v=new VaultSession{Connection=addin,Source="Vault add-in session",Owned=false,Vdf=addin.GetType().Assembly};v.Ws=((object)v.Wsm).GetType().Assembly;v.Fw=VaultProbe.Loaded(FwName)??v.Vdf;log.Add("Vault connection: using the Vault add-in's session ("+v.Describe()+")");return v;}
  // SDK assemblies: from the process when the host already has them, else from the Vault Client Explorer folder
  string explorer=VaultProbe.DiscoverExplorerFolder(s.VaultExplorerPath);
  Assembly ws=Need(WsName,explorer,log),fw=Need(FwName,explorer,log),vdf=Need(VdfName,explorer,log);
  // 2. the Vault login dialog
  Assembly? forms=TryLoad(FormsName,explorer,log);
  if(forms is not null){
   Type? lib=forms.GetType(FwName+".Vault.Forms.Library");
   if(lib is not null){
    try{lib.GetMethods(BindingFlags.Public|BindingFlags.Static).FirstOrDefault(m=>m.Name=="Initialize"&&m.GetParameters().Length==0)?.Invoke(null,null);}catch{}
    var login=lib.GetMethods(BindingFlags.Public|BindingFlags.Static).FirstOrDefault(m=>m.Name=="Login"&&m.GetParameters().Length==1);
    if(login is not null){
     object? settings=null;Type? lsT=forms.GetType(FwName+".Vault.Forms.Settings.LoginSettings");
     if(lsT is not null&&login.GetParameters()[0].ParameterType.IsAssignableFrom(lsT))try{settings=Activator.CreateInstance(lsT);Set(settings!,"ServerName",CleanServer(s.VaultServer));Set(settings!,"VaultName",s.VaultName);}catch{settings=null;}
     object? conn;try{conn=login.Invoke(null,new[]{settings});}catch(TargetInvocationException tx){throw new InvalidOperationException("The Vault login dialog failed: "+Root(tx).Message,tx.InnerException);}
     if(conn is null)throw new OperationCanceledException("Vault login was cancelled. Nothing was changed.");
     var v=new VaultSession{Connection=conn,Source="Vault login dialog",Owned=true,Ws=ws,Fw=fw,Vdf=vdf};log.Add("Vault connection: logged in through the Vault login dialog ("+v.Describe()+")");return v;
    }
   }
   log.Add("Vault login dialog not available in "+forms.GetName().Name+"; using Windows authentication");
  }
  // 3. Windows authentication without a dialog
  return WinAuth(s,ws,fw,vdf,log);
 }
 static VaultSession WinAuth(AppSettings s,Assembly ws,Assembly fw,Assembly vdf,List<string> log){
  string server=CleanServer(s.VaultServer);if(server.Length==0||string.IsNullOrWhiteSpace(s.VaultName))throw new InvalidOperationException("Vault server and vault name are required (Settings).");
  Type wsmT=ws.GetType(WsName+"Tools.WebServiceManager")??throw new MissingMemberException("WebServiceManager not found in "+ws.Location);
  Type credT=ws.GetType(WsName+"Tools.WinAuthCredentials")??throw new MissingMemberException("WinAuthCredentials not found in "+ws.Location);
  object ids=ServerIdentities(ws,wsmT,server);Type? agentT=ws.GetType(WsName+".LicensingAgent");object? agent=agentT is null?null:Enum.Parse(agentT,"Client");
  object? creds=null;Exception? last=null;
  foreach(var c in credT.GetConstructors().Where(c=>c.GetParameters().Length>0&&Fits(c.GetParameters()[0].ParameterType,ids)).OrderByDescending(c=>c.GetParameters().Length)){
   var ps=c.GetParameters();var args=new object?[ps.Length];bool first=true;
   for(int i=0;i<ps.Length;i++){Type t=ps[i].ParameterType;if(Fits(t,ids))args[i]=ids;else if(t==typeof(string)&&first){args[i]=s.VaultName.Trim();first=false;}else if(agentT is not null&&t==agentT)args[i]=agent;else if(t==typeof(bool))args[i]=false;else if(t.IsValueType)args[i]=Activator.CreateInstance(t);else args[i]=null;}
   try{creds=c.Invoke(args);break;}catch(TargetInvocationException tx){last=tx.InnerException??tx;}catch(Exception ex){last=ex;}
  }
  if(creds is null)throw new InvalidOperationException("Windows authentication to Vault failed: "+(last is null?"no usable WinAuthCredentials constructor":Root(last).Message)+". Log in on the Vault tab (or in Vault Explorer) and try again.",last);
  dynamic wsm;try{wsm=Activator.CreateInstance(wsmT,creds)!;}catch(TargetInvocationException tx){throw new InvalidOperationException("Windows authentication to Vault "+server+" / "+s.VaultName+" failed: "+Root(tx).Message+". Log in on the Vault tab and try again.",tx.InnerException);}
  long userId=0;try{userId=Convert.ToInt64(wsm.SecurityService.Session.User.Id);}catch{try{userId=Convert.ToInt64(wsm.WebServiceCredentials.SecurityHeader.UserId);}catch{}}
  Type connT=vdf.GetType(VdfName+".Currency.Connections.Connection")!,flagsT=vdf.GetType(VdfName+".Currency.Connections.AuthenticationFlags")!;
  object flags=Enum.IsDefined(flagsT,"WindowsAuthentication")?Enum.Parse(flagsT,"WindowsAuthentication"):Enum.Parse(flagsT,"Standard");object wsmObj=(object)wsm;
  object? conn=null;
  foreach(var c in connT.GetConstructors().OrderByDescending(c=>c.GetParameters().Length)){
   var ps=c.GetParameters();if(!ps.Any(p=>Fits(p.ParameterType,wsmObj)))continue;var args=new object?[ps.Length];int str=0;
   for(int i=0;i<ps.Length;i++){Type t=ps[i].ParameterType;if(Fits(t,wsmObj))args[i]=wsmObj;else if(t==typeof(string))args[i]=str++==0?s.VaultName.Trim():server;else if(t==typeof(long)||t==typeof(int))args[i]=Convert.ChangeType(userId,t);else if(t==flagsT)args[i]=flags;else if(t.IsValueType)args[i]=Activator.CreateInstance(t);else args[i]=null;}
   try{conn=c.Invoke(args);break;}catch(TargetInvocationException tx){last=tx.InnerException??tx;}catch(Exception ex){last=ex;}
  }
  if(conn is null){try{wsm.Dispose();}catch{}throw new InvalidOperationException("Vault connection object could not be created: "+(last is null?"no usable Connection constructor":Root(last).Message),last);}
  var v=new VaultSession{Connection=conn,Source="Windows authentication",Owned=true,Ws=ws,Fw=fw,Vdf=vdf};log.Add("Vault connection: Windows authentication to "+server+" / "+s.VaultName+" ("+v.Describe()+")");return v;
 }
 static object ServerIdentities(Assembly ws,Type wsmT,string server){
  Type? t=ws.GetType(WsName+".ServerIdentities");
  if(t is not null)try{object o=Activator.CreateInstance(t)!;Set(o,"DataServer",server);Set(o,"FileServer",server);return o;}catch{}
  var m=wsmT.GetMethods(BindingFlags.Public|BindingFlags.Static).FirstOrDefault(x=>x.Name=="GetServerIdentities"&&x.GetParameters().Length==1)??throw new MissingMemberException("ServerIdentities not available in "+ws.Location);
  object? z=m.Invoke(null,new object[]{server});if(z is Array a&&a.Length>0)z=a.GetValue(0);return z??throw new InvalidOperationException("Could not obtain the Vault server identity for "+server);
 }
 static Assembly Need(string name,string explorer,List<string> log)=>TryLoad(name,explorer,log)??throw new FileNotFoundException("Vault SDK assembly "+name+".dll was not found. Install the Vault Client 2026 (Vault Professional) on this workstation or point the Vault Explorer folder setting at its Explorer folder"+(explorer.Length==0?".":" (looked in "+explorer+").")+" Leave the Catalog Vault path blank to work without Vault.",name+".dll");
 static Assembly? TryLoad(string name,string explorer,List<string> log){
  var a=VaultProbe.Loaded(name);if(a is not null){log.Add("Vault SDK "+name+": already loaded by the host ("+Ver(a)+")");return a;}
  if(explorer.Length==0)return null;string path=Path.Combine(explorer,name+".dll");if(!File.Exists(path))return null;
  string? bad=VaultProbe.RuntimeMismatch(explorer);if(bad is not null)throw new InvalidOperationException(bad);
  try{a=Assembly.LoadFrom(path);}catch(BadImageFormatException ex){throw new InvalidOperationException("Vault SDK assembly "+path+" cannot be loaded into this process: "+ex.Message,ex);}
  log.Add("Vault SDK "+name+": loaded from "+path+" ("+Ver(a)+")");return a;
 }
 static string Ver(Assembly a){try{string l=a.Location;if(l.Length>0)return "file version "+VaultProbe.FileVersion(l);}catch{}return "version "+a.GetName().Version;}

 // ------------------------------------------------------------------------------------------------- file helpers ----
 static dynamic FindFile(VaultSession v,string vaultPath){
  if(vaultPath.Length==0)throw new InvalidOperationException("Catalog Vault path is blank.");
  dynamic files=v.Wsm.DocumentService.FindLatestFilesByPaths(new string[]{vaultPath});dynamic? f=null;foreach(var x in files){f=x;break;}
  if(f is null||Convert.ToInt64(f.Id)<=0)throw new FileNotFoundException("The catalog was not found in Vault at "+vaultPath+". The Catalog Vault path setting must be the file's path inside Vault, for example $/CAD Standards/AutoCAD/ECAD Standards/E-CAD .mbd ACCESS FILES/AeData/en-US/Catalogs/default_cat.mdb (Vault Explorer shows it in the file's Properties).");
  return f!;
 }
 static object NewFileIteration(VaultSession v,object file){Type itT=v.Vdf.GetType(VdfName+".Currency.Entities.FileIteration")!;return Activator.CreateInstance(itT,(object)v.Connection,file)!;}
 static long MyId(VaultSession v){try{return Convert.ToInt64(v.Wsm.SecurityService.Session.User.Id);}catch{}try{return Convert.ToInt64(v.Wsm.WebServiceCredentials.SecurityHeader.UserId);}catch{}return 0;}
 static string UserName(VaultSession v,long userId){try{return (string)v.Wsm.AdminService.GetUserByUserId(userId).Name;}catch{return "user id "+userId;}}
 // The working copy is current when its size and timestamp match the Vault version (Vault's Get keeps the file's modification date).
 static bool NeedsDownload(dynamic f,string localPath,List<string> log){
  if(!File.Exists(localPath)){log.Add("Working copy missing; the Vault version will be downloaded");return true;}
  try{var fi=new FileInfo(localPath);long size=Convert.ToInt64(f.FileSize);DateTime mod=Convert.ToDateTime(f.ModDate);
   bool same=fi.Length==size&&(Math.Abs((fi.LastWriteTime-mod).TotalSeconds)<3||Math.Abs((fi.LastWriteTimeUtc-mod).TotalSeconds)<3);
   if(same)return false;
   log.Add($"Working copy differs from the Vault version ({fi.Length} bytes, {fi.LastWriteTime:s} vs {size} bytes, {mod:s})"+(VaultProbe.IsReadOnly(localPath)?"":" and is writable - if it holds edits that were never checked in they are in this transaction's backup")+"; the Vault version will be downloaded");
   return true;}
  catch{return true;}
 }
 static void Acquire(VaultSession v,dynamic file,string localPath,bool checkout,bool download,List<string> log){
  Type stT=v.Vdf.GetType(VdfName+".Settings.AcquireFilesSettings")??throw new MissingMemberException("AcquireFilesSettings not found");
  dynamic settings=Activator.CreateInstance(stT,(object)v.Connection)!;dynamic it=NewFileIteration(v,file);
  Type optT=stT.GetNestedType("AcquisitionOption")??v.Vdf.GetType(VdfName+".Settings.AcquireFilesSettings+AcquisitionOption")!;
  long bits=0;if(checkout)bits|=Convert.ToInt64(Enum.Parse(optT,"Checkout"));if(download)bits|=Convert.ToInt64(Enum.Parse(optT,"Download"));dynamic opt=Enum.ToObject(optT,bits);
  try{settings.AddEntityToAcquire(it);}catch(RuntimeBinderException){settings.AddFileToAcquire(it,opt);}
  settings.DefaultAcquisitionOption=opt;
  if(download){Type folderT=v.Fw.GetType(FwName+".Currency.FolderPathAbsolute")!;dynamic folder=Activator.CreateInstance(folderT,Path.GetDirectoryName(localPath)!)!;settings.LocalPath=folder;}
  try{settings.OptionsRelationshipGathering.FileRelationshipSettings.IncludeChildren=false;settings.OptionsRelationshipGathering.FileRelationshipSettings.IncludeParents=false;settings.OptionsRelationshipGathering.FileRelationshipSettings.IncludeAttachments=false;}catch{}
  dynamic results=v.Connection.FileManager.AcquireFiles(settings);
  string downloaded="";
  try{foreach(var r in results.FileResults){Exception? ex=null;try{ex=(Exception?)r.Exception;}catch{}if(ex is not null)throw new InvalidOperationException("Vault reported: "+ex.Message,ex);try{string p=(string)r.LocalPath.FullPath;if(p.Length>0)downloaded=p;}catch{}}}catch(RuntimeBinderException){}
  string what=checkout&&download?"Vault check-out + download of the latest version":checkout?"Vault check-out":"Vault download of the latest version";
  if(download&&downloaded.Length>0&&File.Exists(downloaded)&&!Path.GetFullPath(downloaded).Equals(Path.GetFullPath(localPath),StringComparison.OrdinalIgnoreCase)){if(File.Exists(localPath))File.SetAttributes(localPath,FileAttributes.Normal);File.Copy(downloaded,localPath,true);log.Add(what+": PASS (Vault placed the file at "+downloaded+"; copied over the working copy)");}
  else log.Add(what+": PASS"+(checkout&&!download?" (working copy already current)":""));
 }
 static void RawCheckout(VaultSession v,dynamic file,string localPath,List<string> log){
  object doc=(object)v.Wsm.DocumentService;long id=Convert.ToInt64(file.Id);Exception? last=null;
  foreach(MethodInfo m in doc.GetType().GetMethods().Where(x=>x.Name=="CheckoutFile").OrderBy(x=>x.GetParameters().Length)){
   var ps=m.GetParameters();var args=new object?[ps.Length];
   for(int i=0;i<ps.Length;i++){var p=ps[i];Type t=p.ParameterType;string n=p.Name??"";
    if(p.IsOut)args[i]=null;else if(t==typeof(long))args[i]=id;else if(t.IsEnum)args[i]=Enum.IsDefined(t,"Master")?Enum.Parse(t,"Master"):Enum.GetValues(t).GetValue(0);
    else if(t==typeof(string))args[i]=n.Contains("mach",StringComparison.OrdinalIgnoreCase)?Environment.MachineName:n.Contains("path",StringComparison.OrdinalIgnoreCase)?localPath:AppName;
    else if(t.IsValueType)args[i]=Activator.CreateInstance(t);else args[i]=null;}
   try{m.Invoke(doc,args);log.Add("Vault check-out (DocumentService, no download): PASS");return;}catch(TargetInvocationException tx){last=tx.InnerException??tx;}catch(Exception ex){last=ex;}
  }
  throw new InvalidOperationException("Vault check-out failed: "+(last is null?"no CheckoutFile method was found":Root(last).Message),last);
 }
 // A parameter "fits" a value when its type is the value's own type or a base/interface of it, never plain object.
 static bool Fits(Type t,object o)=>t!=typeof(object)&&t.IsInstanceOfType(o);
 static void Set(object o,string prop,string value){try{var p=o.GetType().GetProperty(prop);if(p is not null&&p.CanWrite&&p.PropertyType==typeof(string)&&!string.IsNullOrWhiteSpace(value))p.SetValue(o,value);}catch{}}
 static string Str(object o,string member){try{var t=o.GetType();var p=t.GetProperty(member);if(p is not null)return Convert.ToString(p.GetValue(o))??"";var f=t.GetField(member);return f is null?"":Convert.ToString(f.GetValue(o))??"";}catch{return "";}}
 static Exception Root(Exception ex){while(true){if(ex is TargetInvocationException{InnerException:not null} t)ex=t.InnerException!;else if(ex is AggregateException{InnerException:not null} a)ex=a.InnerException!;else return ex;}}
}
