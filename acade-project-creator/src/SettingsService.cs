using System.Text.Json;
using System.Text.RegularExpressions;
namespace LSE.AutoCADElectrical.ProjectTools;
public static class SettingsService {
 static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"LSE","AutoCADElectricalProjectTools");
 static readonly string FilePath=Path.Combine(Folder,"settings.json"); public static string SettingsPath=>FilePath; public static string LogFolder=>Path.Combine(Folder,"Logs");
 public const string VaultFolderName="_Lake_Shore_E_Vault"; public const string DefaultVaultRoot="C:\\"+VaultFolderName;
 public static AppSettings Load(){Directory.CreateDirectory(Folder);AppSettings s;try{s=File.Exists(FilePath)?JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath))??new():new();}catch{s=new();}
  if(string.IsNullOrWhiteSpace(s.TemplatesConfigPath))s.TemplatesConfigPath=Path.Combine(Path.GetDirectoryName(typeof(SettingsService).Assembly.Location)??"","LseProjectTemplates.json");
  VaultDefaults v=TemplateService.TryLoadVault(s.TemplatesConfigPath);string vault=DiscoverVaultRoot();
  if(string.IsNullOrWhiteSpace(s.BaseProjectPath)){string p=OnVault(v.TemplateProject,vault);if(File.Exists(p))s.BaseProjectPath=p;}
  if(string.IsNullOrWhiteSpace(s.ProjectsRootFolder)){string p=OnVault(v.JobsRoot,vault);s.ProjectsRootFolder=Directory.Exists(p)?p:DiscoverProjectsRoot();}
  if(string.IsNullOrWhiteSpace(s.DrawingTemplatePath)){string p=TemplateService.ResolveTemplate(v.DrawingTemplate,TemplateFolder(s.BaseProjectPath,OnVault(v.TemplateProject,vault)));s.DrawingTemplatePath=File.Exists(p)?p:DiscoverTemplate();}
  if(string.IsNullOrWhiteSpace(s.SymbolLibraryFolder)){string p=OnVault(v.SymbolLibrary,vault);s.SymbolLibraryFolder=Directory.Exists(p)?p:DiscoverLibrary();}
  return s;}
 public static void Save(AppSettings s){Directory.CreateDirectory(Folder);string t=FilePath+".tmp";File.WriteAllText(t,JsonSerializer.Serialize(s,new JsonSerializerOptions{WriteIndented=true}));File.Move(t,FilePath,true);}
 public static string TemplateFolder(string baseProject,string fallback){string p=baseProject.Trim().Length>0?baseProject:fallback;return p.Trim().Length>0?Path.GetDirectoryName(p)??"":"";}
 public static string DiscoverVaultRoot(){if(Directory.Exists(DefaultVaultRoot))return DefaultVaultRoot;try{foreach(var d in DriveInfo.GetDrives()){try{if(d.DriveType is not(DriveType.Fixed or DriveType.Network))continue;string p=Path.Combine(d.RootDirectory.FullName,VaultFolderName);if(Directory.Exists(p))return p;}catch{}}}catch{}return "";}
 public static string OnVault(string path,string vaultRoot){if(path.Trim().Length==0)return "";if(vaultRoot.Length==0||File.Exists(path)||Directory.Exists(path))return path;int i=path.IndexOf(VaultFolderName,StringComparison.OrdinalIgnoreCase);return i<0?path:Path.Combine(vaultRoot,path[(i+VaultFolderName.Length)..].TrimStart('\\','/'));}
 public static string AeData(){string d=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);foreach(string v in new[]{"Acade 2026","Acade 2025"}){string p=Path.Combine(d,v,"AeData");if(Directory.Exists(p))return p;}return Path.Combine(d,"Acade 2026","AeData");}
 static string DiscoverProjectsRoot(){string p=Path.Combine(AeData(),"Proj");return Directory.Exists(p)?p:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"AutoCAD Electrical Projects");}
 static string DiscoverTemplate(){string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Autodesk");if(!Directory.Exists(root))return "";try{foreach(string product in Directory.GetDirectories(root,"AutoCAD Electrical 20*").OrderByDescending(x=>x,StringComparer.OrdinalIgnoreCase))foreach(string f in Directory.GetFiles(product,"ACAD_ELECTRICAL.dwt",SearchOption.AllDirectories))return f;}catch{}return "";}
 static string DiscoverLibrary(){string libs=Path.Combine(AeData(),"en-US","Libs");if(!Directory.Exists(libs))return "";foreach(string n in new[]{"jic125","JIC125","iec2","IEC2"}){string p=Path.Combine(libs,n);if(Directory.Exists(p))return p;}try{return Directory.GetDirectories(libs).FirstOrDefault()??"";}catch{return "";}}
}
public static class TemplateService {
 static readonly JsonSerializerOptions Options=new(){PropertyNameCaseInsensitive=true,ReadCommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true};
 public static ProjectTemplates Load(string path){if(!File.Exists(path))throw new FileNotFoundException("The LSE project templates JSON was not found. Point Settings at a shared copy or restore config\\LseProjectTemplates.json next to the plugin.",path);var t=JsonSerializer.Deserialize<ProjectTemplates>(File.ReadAllText(path),Options)??throw new InvalidOperationException("The project templates JSON is empty.");
  if(t.DescriptionLines.Count==0)throw new InvalidOperationException("The project templates JSON defines no description lines.");if(t.ProjectTypes.Count==0)throw new InvalidOperationException("The project templates JSON defines no project types.");
  var dup=t.DescriptionLines.GroupBy(x=>x.Index).FirstOrDefault(g=>g.Count()>1);if(dup is not null)throw new InvalidOperationException("Description line index "+dup.Key+" is defined more than once in the project templates JSON.");
  if(t.DescriptionLines.Any(x=>x.Index<1))throw new InvalidOperationException("Description line indexes must start at 1.");if(t.DescriptionLines.Count(x=>x.HasRole("title"))!=1)throw new InvalidOperationException("The project templates JSON needs exactly one description line with role \"title\" (the LSE PROJECT NAME line).");
  if(t.DescriptionLines.Count(x=>x.HasRole("customer"))>1)throw new InvalidOperationException("Only one description line may carry role \"customer\".");foreach(var l in t.DescriptionLines)if(l.HasRole("title"))l.Required=true;
  if(t.Job.NumberPattern.Trim().Length>0)try{_=new Regex(t.Job.NumberPattern);}catch(Exception ex){throw new InvalidOperationException("job.numberPattern is not a valid regular expression: "+ex.Message);}
  foreach(var p in t.ProjectTypes)if(p.FileNamePattern.Trim().Length==0)p.FileNamePattern=t.Job.FileNamePattern.Trim().Length>0?t.Job.FileNamePattern:"{JOB}-{SHEET:3}";
  var types=t.ProjectTypes.Select(p=>p.Name).ToList();foreach(var pl in t.ProductLines)if(pl.ProjectType.Trim().Length>0&&!types.Contains(pl.ProjectType,StringComparer.OrdinalIgnoreCase))throw new InvalidOperationException("Product line \""+pl.Name+"\" names a project type that is not defined: "+pl.ProjectType);
  t.DescriptionLines=t.DescriptionLines.OrderBy(x=>x.Index).ToList();t.TitleBlock.Fixed=new(t.TitleBlock.Fixed,StringComparer.OrdinalIgnoreCase);t.Source=path;return t;}
 public static VaultDefaults TryLoadVault(string path){try{if(!File.Exists(path))return new();return JsonSerializer.Deserialize<ProjectTemplates>(File.ReadAllText(path),Options)?.Vault??new();}catch{return new();}}
 public static string ResolveTemplate(string template,string templateFolder){string t=template.Trim();if(t.Length==0)return "";if(Path.IsPathRooted(t)||templateFolder.Trim().Length==0)return t;return Path.Combine(templateFolder,t);}
 public static string Expand(string pattern,string job,int sheet,string projectName,DateTime now,string user,string dateFormat,IDictionary<string,string>? extra=null){string date;try{date=now.ToString(dateFormat);}catch{date=now.ToString("yyyy-MM-dd");}string r=pattern.Replace("{JOB}",job,StringComparison.OrdinalIgnoreCase).Replace("{PROJECT}",projectName,StringComparison.OrdinalIgnoreCase).Replace("{USER}",user,StringComparison.OrdinalIgnoreCase).Replace("{YEAR}",now.ToString("yyyy"),StringComparison.OrdinalIgnoreCase).Replace("{DATE}",date,StringComparison.OrdinalIgnoreCase);
  if(extra is not null)foreach(var e in extra)r=r.Replace("{"+e.Key+"}",e.Value??"",StringComparison.OrdinalIgnoreCase);
  return Regex.Replace(r,@"\{SHEET(?::(\d+))?\}",m=>m.Groups[1].Success?sheet.ToString().PadLeft(int.Parse(m.Groups[1].Value),'0'):sheet.ToString(),RegexOptions.IgnoreCase);}
 public static string SafeName(string value){char[] invalid=Path.GetInvalidFileNameChars();return new string(value.Trim().Select(c=>invalid.Contains(c)?'-':c).ToArray()).Trim();}
 public static bool JobNumberOk(JobRules j,string job){string p=j.NumberPattern.Trim().Length>0?j.NumberPattern:@"^[A-Za-z0-9][A-Za-z0-9._\- ]*$";try{return Regex.IsMatch(job.Trim(),p);}catch{return job.Trim().Length>0;}}
 public static string ComposeUnitSpecs(UnitSpecsDef u,string type,string amps,string volts,string kaic,string nema){string kv=Regex.Matches(volts,@"\d+").Select(m=>int.Parse(m.Value)).DefaultIfEmpty(0).Max().ToString();if(kv=="0")kv="###";
  var x=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){["TYPE"]=Blank(type,"####"),["AMPS"]=Blank(Regex.Replace(amps.Trim(),@"[Aa]$",""),"####"),["VOLTS"]=Blank(volts,"###/###VAC"),["KAIC"]=Blank(kaic,"##"),["KV"]=kv,["NEMA"]=Blank(nema,"N#?")};
  return Expand(u.Pattern.Trim().Length>0?u.Pattern:"{TYPE}, {AMPS}A, {VOLTS}, {KAIC}KA@{KV}V, {NEMA}","",0,"",DateTime.Now,"","",x);}
 static string Blank(string v,string placeholder)=>v.Trim().Length>0?v.Trim():placeholder;
}
