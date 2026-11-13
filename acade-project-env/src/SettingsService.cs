using System.Text.Json;
namespace LSE.AutoCADElectrical.ProjectEnvironment;
public static class SettingsService {
 static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"LSE","LSE_Env");
 static readonly string FilePath=Path.Combine(Folder,"settings.json"); public static string SettingsPath=>FilePath; public static string LogFolder=>Path.Combine(Folder,"Logs"); public static string SnapshotFolder=>Path.Combine(Folder,"Snapshots");
 public static string AssemblyFolder=>Path.GetDirectoryName(typeof(SettingsService).Assembly.Location)??"";
 public static string BundledProfilesFolder=>Path.Combine(AssemblyFolder,"profiles");
 public static readonly JsonSerializerOptions Json=new(){WriteIndented=true,PropertyNameCaseInsensitive=true,ReadCommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true};
 public static AppSettings Load(){Directory.CreateDirectory(Folder);AppSettings s;try{s=File.Exists(FilePath)?JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath),Json)??new():new();}catch{s=new();}
  if(string.IsNullOrWhiteSpace(s.RulesConfigPath))s.RulesConfigPath=Path.Combine(AssemblyFolder,"LseEnvRules.json");if(string.IsNullOrWhiteSpace(s.ProfilesFolder))s.ProfilesFolder=Path.Combine(Folder,"Profiles");if(string.IsNullOrWhiteSpace(s.BackupFolderName))s.BackupFolderName="_LSE_ENV_BACKUP";if(s.BackupsToKeep<1)s.BackupsToKeep=1;return s;}
 public static void Save(AppSettings s){Directory.CreateDirectory(Folder);string t=FilePath+".tmp";File.WriteAllText(t,JsonSerializer.Serialize(s,Json));File.Move(t,FilePath,true);}
 public static void Remember(AppSettings s,string wdp){s.RecentProjects.RemoveAll(x=>x.Equals(wdp,StringComparison.OrdinalIgnoreCase));s.RecentProjects.Insert(0,wdp);if(s.RecentProjects.Count>25)s.RecentProjects.RemoveRange(25,s.RecentProjects.Count-25);try{Save(s);}catch{}}
}
public static class RulesService {
 public static EnvRules Load(string path){EnvRules r;if(!File.Exists(path))r=Defaults();else r=JsonSerializer.Deserialize<EnvRules>(File.ReadAllText(path),SettingsService.Json)??throw new InvalidOperationException("The LSE_Env rules JSON is empty: "+path);
  if(r.WdmBlockName.Trim().Length==0)r.WdmBlockName="WD_M";r.Labels=new(r.Labels,StringComparer.OrdinalIgnoreCase);
  foreach(var m in r.Settings){if(m.Key.Trim().Length==0)throw new InvalidOperationException("A settings row in the rules JSON has no key: "+path);m.Key=m.Key.Trim();m.Wdm=m.Wdm.Trim();if(m.Label.Length==0)m.Label=m.Key;}
  var dup=r.Settings.GroupBy(m=>m.Key,StringComparer.OrdinalIgnoreCase).FirstOrDefault(g=>g.Count()>1);if(dup is not null)throw new InvalidOperationException("Setting key '"+dup.Key+"' appears twice in "+path);
  foreach(var s in r.WireSchemes){if(s.Name.Trim().Length==0)throw new InvalidOperationException("A wire scheme in the rules JSON has no name: "+path);s.Values=new(s.Values,StringComparer.OrdinalIgnoreCase);foreach(string k in s.Values.Keys.Concat(s.TemplateKeys))if(r.Map(k) is null)throw new InvalidOperationException("Wire scheme '"+s.Name+"' uses setting key '"+k+"' which the setting map does not define: "+path);}
  if(r.FromTo.WireNumberAttribute.Trim().Length==0)r.FromTo.WireNumberAttribute="WIRENO";try{_=new System.Text.RegularExpressions.Regex(r.FromTo.ConnectionAttributePattern);}catch(Exception ex){throw new InvalidOperationException("fromTo.connectionAttributePattern is not a valid regular expression: "+ex.Message);}
  r.Source=File.Exists(path)?path:"(built-in defaults; "+path+" not found)";return r;}
 static EnvRules Defaults(){var r=new EnvRules();
  r.Settings.AddRange(new[]{new SettingMap{Key="wireMode",Label="Wire number mode",Group="Wire numbers",Property=6,Wdm="WIREMODE",Kind="choice",Choices="S=Sequential;R=Reference-based"},new SettingMap{Key="wireFormat",Label="Wire number format",Group="Wire numbers",Property=9,Wdm="WIREFMT"},new SettingMap{Key="wireStart",Label="Wire number start",Group="Wire numbers",Property=7,Wdm="WIRE_START",Kind="number"},new SettingMap{Key="wireIncrement",Label="Wire number increment",Group="Wire numbers",Property=10,Wdm="WIREINC",Kind="number"},new SettingMap{Key="wireSuffixes",Label="Wire number suffix list",Group="Wire numbers",Property=8,Wdm="WIRE_RSUF"},
   new SettingMap{Key="tagMode",Label="Component tag mode",Group="Component tags",Property=2,Wdm="TAGMODE",Kind="choice",Choices="S=Sequential;R=Reference-based"},new SettingMap{Key="tagFormat",Label="Component tag format",Group="Component tags",Property=5,Wdm="TAGFMT"},new SettingMap{Key="tagStart",Label="Component tag start",Group="Component tags",Property=3,Wdm="TAG_START",Kind="number"},new SettingMap{Key="tagSuffixes",Label="Component tag suffix list",Group="Component tags",Property=4,Wdm="TAG_RSUF"}});
  r.WireSchemes.Add(new WireScheme{Name="LSE standard (template)",Description="As the LSE template project.",FromTemplate=true,TemplateKeys=new(){"wireMode","wireFormat","wireStart","wireIncrement","wireSuffixes"}});
  r.WireSchemes.Add(new WireScheme{Name="Line reference (grid)",Description="Reference-based wire numbers.",Values=new(StringComparer.OrdinalIgnoreCase){["wireMode"]="R",["wireFormat"]="%N"}});
  r.WireSchemes.Add(new WireScheme{Name="From/To device",Description="FROM-TO from the connected device tags, written as fixed wire numbers.",Values=new(StringComparer.OrdinalIgnoreCase){["wireMode"]="S",["wireFormat"]="%N"},FromTo=new FromToSettings()});return r;}
}
public static class ProfileService {
 public static List<EnvironmentProfile> Load(AppSettings s){Seed(s);var list=new List<EnvironmentProfile>();if(!Directory.Exists(s.ProfilesFolder))return list;
  foreach(string f in Directory.EnumerateFiles(s.ProfilesFolder,"*.json").OrderBy(x=>x,StringComparer.OrdinalIgnoreCase)){try{var p=Read(f);if(p is not null)list.Add(p);}catch{}}return list;}
 public static EnvironmentProfile? Read(string path){var p=JsonSerializer.Deserialize<EnvironmentProfile>(File.ReadAllText(path),SettingsService.Json);if(p is null)return null;p.FilePath=path;if(p.Name.Trim().Length==0)p.Name=Path.GetFileNameWithoutExtension(path);
  p.Values=new(p.Values,StringComparer.OrdinalIgnoreCase);p.Drawing=new(p.Drawing,StringComparer.OrdinalIgnoreCase);return p;}
 public static string Save(AppSettings s,EnvironmentProfile p){if(p.Name.Trim().Length==0)throw new InvalidOperationException("The profile needs a name.");Directory.CreateDirectory(s.ProfilesFolder);
  string path=p.FilePath.Length>0&&Path.GetDirectoryName(p.FilePath)!.Equals(Path.GetFullPath(s.ProfilesFolder).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)?p.FilePath:Path.Combine(s.ProfilesFolder,FileName(p.Name)+".json");
  if(!path.Equals(p.FilePath,StringComparison.OrdinalIgnoreCase)&&File.Exists(path))throw new InvalidOperationException("A profile file with that name already exists: "+path);
  string t=path+".tmp";File.WriteAllText(t,JsonSerializer.Serialize(p,SettingsService.Json));File.Move(t,path,true);if(p.FilePath.Length>0&&!p.FilePath.Equals(path,StringComparison.OrdinalIgnoreCase)&&File.Exists(p.FilePath))File.Delete(p.FilePath);p.FilePath=path;return path;}
 public static void Export(EnvironmentProfile p,string path){File.WriteAllText(path,JsonSerializer.Serialize(p,SettingsService.Json));}
 public static string Retire(AppSettings s,EnvironmentProfile p){if(p.FilePath.Length==0||!File.Exists(p.FilePath))return "";string folder=Path.Combine(s.ProfilesFolder,"_retired");Directory.CreateDirectory(folder);string target=Path.Combine(folder,Path.GetFileNameWithoutExtension(p.FilePath)+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".json");File.Move(p.FilePath,target);p.FilePath="";return target;}
 public static string FileName(string name){var sb=new System.Text.StringBuilder();foreach(char c in name.Trim())sb.Append(Path.GetInvalidFileNameChars().Contains(c)?'_':c);return sb.Length==0?"profile":sb.ToString();}
 static void Seed(AppSettings s){try{if(Directory.Exists(s.ProfilesFolder)&&Directory.EnumerateFiles(s.ProfilesFolder,"*.json").Any())return;if(!Directory.Exists(SettingsService.BundledProfilesFolder))return;Directory.CreateDirectory(s.ProfilesFolder);foreach(string f in Directory.EnumerateFiles(SettingsService.BundledProfilesFolder,"*.json")){string t=Path.Combine(s.ProfilesFolder,Path.GetFileName(f));if(!File.Exists(t))File.Copy(f,t);}}catch{}}
}
public static class MarkerService {
 public static string PathFor(string wdpPath)=>System.IO.Path.ChangeExtension(wdpPath,".lseenv.json");
 public static EnvMarker? Read(string wdpPath){string p=PathFor(wdpPath);if(!File.Exists(p))return null;try{return JsonSerializer.Deserialize<EnvMarker>(File.ReadAllText(p),SettingsService.Json);}catch{return null;}}
 public static void Write(string wdpPath,EnvMarker m){string p=PathFor(wdpPath);string t=p+".tmp";File.WriteAllText(t,JsonSerializer.Serialize(m,SettingsService.Json));File.Move(t,p,true);}
}
