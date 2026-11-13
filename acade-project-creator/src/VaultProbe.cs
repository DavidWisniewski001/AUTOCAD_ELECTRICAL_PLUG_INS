using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
namespace LSE.AutoCADElectrical.ProjectTools;

// Vault awareness shared by every LSE plugin (identical copy in each plugin folder; keep them in step).
// Nothing from the Vault SDK is referenced at compile time. The probe only looks at assemblies the host has already
// loaded (the Autodesk Vault add-in for AutoCAD / Inventor) and at the Vault Client install folder on disk.
public static class VaultProbe{
 public const string VaultBaseAssembly="Connectivity.Application.VaultBase";
 public const string WebServicesAssembly="Autodesk.Connectivity.WebServices";
 public const string DefaultWorkingFolder=@"C:\_Lake_Shore_E_Vault";
 // ---- Host runtime ----
 public static int RuntimeMajor=>Environment.Version.Major;
 public static string RuntimeLine()=>$"Host: {Process.GetCurrentProcess().ProcessName} on {RuntimeInformation.FrameworkDescription} (.NET {RuntimeMajor})";
 // ---- Vault add-in session (AutoCAD and Inventor both load Connectivity.Application.VaultBase when their Vault add-in is on) ----
 public static Assembly? Loaded(string name)=>AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>{try{return string.Equals(a.GetName().Name,name,StringComparison.OrdinalIgnoreCase);}catch{return false;}});
 // The connection object the Vault add-in holds (Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections.Connection) or null.
 public static object? AddinConnection(){
  try{var asm=Loaded(VaultBaseAssembly);if(asm is null)return null;var cm=asm.GetType(VaultBaseAssembly+".ConnectionManager");if(cm is null)return null;
   object? inst=cm.GetProperty("Instance",BindingFlags.Public|BindingFlags.Static)?.GetValue(null)??cm.GetField("Instance",BindingFlags.Public|BindingFlags.Static)?.GetValue(null);if(inst is null)return null;
   var p=inst.GetType().GetProperty("Connection",BindingFlags.Public|BindingFlags.Instance);if(p is not null)return p.GetValue(inst);var m=inst.GetType().GetMethod("Connection",Type.EmptyTypes);return m?.Invoke(inst,null);}
  catch{return null;}
 }
 public static string Describe(object conn){string user=Str(conn,"UserName"),vault=Str(conn,"Vault"),server=Str(conn,"Server");return $"{(user.Length==0?"(user unknown)":user)} in vault {(vault.Length==0?"?":vault)} on {(server.Length==0?"?":server)}";}
 public static string Str(object o,string member){try{var t=o.GetType();var p=t.GetProperty(member);if(p is not null)return Convert.ToString(p.GetValue(o))??"";var f=t.GetField(member);return f is null?"":Convert.ToString(f.GetValue(o))??"";}catch{return "";}}
 public static string AddinLine(){
  var c=AddinConnection();if(c is not null)return "Vault add-in: logged in as "+Describe(c);
  return Loaded(VaultBaseAssembly) is not null?"Vault add-in: loaded but not logged in (log in on the Vault tab)":"Vault add-in: not loaded in this session (Vault check-out/check-in must be done in Vault Explorer, or the LSE tools will show the Vault login when they need it)";
 }
 // ---- Installed Vault client ----
 public static string DiscoverExplorerFolder(string configured){
  if(!string.IsNullOrWhiteSpace(configured)&&Directory.Exists(configured))return configured;
  var hits=new List<(int Year,string Path)>();
  foreach(string root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}.Where(r=>r.Length>0).Distinct()){
   string autodesk=Path.Combine(root,"Autodesk");if(!Directory.Exists(autodesk))continue;
   foreach(string d in Directory.EnumerateDirectories(autodesk,"Vault*")){string exp=Path.Combine(d,"Explorer");if(!File.Exists(Path.Combine(exp,WebServicesAssembly+".dll")))continue;var m=System.Text.RegularExpressions.Regex.Match(Path.GetFileName(d),@"(20\d\d)");hits.Add((m.Success?int.Parse(m.Groups[1].Value):0,exp));}}
  return hits.OrderByDescending(h=>h.Year).Select(h=>h.Path).FirstOrDefault()??"";
 }
 // ".NETCoreApp,Version=v10.0" / ".NETFramework,Version=v4.8" or null when the file cannot be read.
 public static string? TargetFrameworkOf(string path){
  try{using var fs=File.OpenRead(path);using var pe=new PEReader(fs);if(!pe.HasMetadata)return null;var md=pe.GetMetadataReader();
   foreach(var h in md.GetAssemblyDefinition().GetCustomAttributes()){var ca=md.GetCustomAttribute(h);if(AttributeTypeName(md,ca)!="System.Runtime.Versioning.TargetFrameworkAttribute")continue;var br=md.GetBlobReader(ca.Value);if(br.ReadUInt16()!=1)return null;return br.ReadSerializedString();}}
  catch{}return null;
 }
 static string AttributeTypeName(MetadataReader md,CustomAttribute ca){
  EntityHandle type=default;if(ca.Constructor.Kind==HandleKind.MemberReference)type=md.GetMemberReference((MemberReferenceHandle)ca.Constructor).Parent;else if(ca.Constructor.Kind==HandleKind.MethodDefinition)type=md.GetMethodDefinition((MethodDefinitionHandle)ca.Constructor).GetDeclaringType();
  if(type.Kind==HandleKind.TypeReference){var tr=md.GetTypeReference((TypeReferenceHandle)type);return md.GetString(tr.Namespace)+"."+md.GetString(tr.Name);}
  if(type.Kind==HandleKind.TypeDefinition){var td=md.GetTypeDefinition((TypeDefinitionHandle)type);return md.GetString(td.Namespace)+"."+md.GetString(td.Name);}
  return "";
 }
 // Major .NET version an assembly was built for (0 = unknown, -1 = .NET Framework).
 public static int TargetMajor(string? tfm){if(string.IsNullOrEmpty(tfm))return 0;if(tfm.StartsWith(".NETFramework",StringComparison.OrdinalIgnoreCase))return -1;var m=System.Text.RegularExpressions.Regex.Match(tfm,@"Version=v(\d+)");return m.Success?int.Parse(m.Groups[1].Value):0;}
 public static string FileVersion(string path){try{return FileVersionInfo.GetVersionInfo(path).FileVersion??"";}catch{return "";}}
 // Null when the Vault client in that folder can run inside this process; otherwise the reason it cannot.
 public static string? RuntimeMismatch(string explorerFolder){
  string dll=Path.Combine(explorerFolder,WebServicesAssembly+".dll");if(!File.Exists(dll))return null;int t=TargetMajor(TargetFrameworkOf(dll));
  if(t==-1)return $"The Vault client in {explorerFolder} is the .NET Framework build; {Process.GetCurrentProcess().ProcessName} runs on .NET {RuntimeMajor}. Point the Vault Explorer folder setting at the Vault Client 2026 Explorer folder (the .NET build).";
  if(t>RuntimeMajor)return $"The Vault client in {explorerFolder} (file version {FileVersion(dll)}) is built for .NET {t} but {Process.GetCurrentProcess().ProcessName} is running .NET {RuntimeMajor} ({RuntimeInformation.FrameworkDescription}). Autodesk moved AutoCAD 2026.1.2, Inventor 2026.4 and Vault 2026.4 to .NET 10 together (August 2026): install the matching updates for every Autodesk 2026 product on this workstation so the host and the Vault client use the same .NET, then try again.";
  return null;
 }
 public static string ClientLine(string configuredExplorer){
  string exp=DiscoverExplorerFolder(configuredExplorer);if(exp.Length==0)return "Vault client: not found (no Vault Client 20xx\\Explorer folder with "+WebServicesAssembly+".dll)"+(string.IsNullOrWhiteSpace(configuredExplorer)?"":"; setting was "+configuredExplorer);
  string dll=Path.Combine(exp,WebServicesAssembly+".dll");string? tfm=TargetFrameworkOf(dll);string? bad=RuntimeMismatch(exp);
  return $"Vault client: {exp} (file version {FileVersion(dll)}, built for {(tfm??"unknown framework")}) "+(bad is null?"- compatible with this host":"- NOT LOADABLE HERE: "+bad);
 }
 public static string Report(string configuredExplorer)=>string.Join(Environment.NewLine,new[]{RuntimeLine(),ClientLine(configuredExplorer),AddinLine()});
 // ---- Working-folder safety ----
 public static bool IsReadOnly(string path){try{return File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReadOnly)!=0;}catch{return false;}}
 public static bool InVaultWorkingFolder(string path){try{string full=Path.GetFullPath(path);return full.IndexOf(@"\_Lake_Shore_E_Vault\",StringComparison.OrdinalIgnoreCase)>=0||full.StartsWith(DefaultWorkingFolder+"\\",StringComparison.OrdinalIgnoreCase);}catch{return false;}}
 // Throws before anything is written when one or more of the files are read-only (Vault marks files that are not checked out read-only).
 public static void RequireWritable(IEnumerable<string> files,string what="Nothing was changed."){
  var ro=files.Where(f=>!string.IsNullOrWhiteSpace(f)).Distinct(StringComparer.OrdinalIgnoreCase).Where(IsReadOnly).ToList();if(ro.Count==0)return;
  var sb=new System.Text.StringBuilder();sb.Append(ro.Count==1?"This file is read-only: ":"These files are read-only: ").Append(string.Join(", ",ro)).Append(". ");
  sb.Append(ro.Any(InVaultWorkingFolder)?"They are in the Vault working folder: check them out in Vault first (the Vault tab in AutoCAD / Inventor, or Vault Explorer > Check Out), then run this again. ":"If they are under Vault control check them out first; otherwise clear the read-only attribute. ");
  sb.Append(what);throw new InvalidOperationException(sb.ToString());
 }
 // The reminder a transaction appends after success: which of the files it touched live in the Vault working folder.
 public static IEnumerable<string> CheckinReminder(IEnumerable<string> touched){
  var v=touched.Where(f=>!string.IsNullOrWhiteSpace(f)).Distinct(StringComparer.OrdinalIgnoreCase).Where(f=>InVaultWorkingFolder(f)&&(File.Exists(f)||Directory.Exists(f))).ToList();if(v.Count==0)yield break;
  yield return "Vault: these "+v.Count+" item(s) are in the Vault working folder and are not checked in yet. Check them in (new files: Add to Vault) from the Vault tab or Vault Explorer so other workstations get them:";
  foreach(string f in v)yield return "  "+f;
 }
}
