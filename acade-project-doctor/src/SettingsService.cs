using System.Text.Json;
namespace LSE.AutoCADElectrical.ProjectDoctor;
public static class SettingsService {
 static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"LSE","LSE_Doctor");
 static readonly string FilePath=Path.Combine(Folder,"settings.json"); public static string SettingsPath=>FilePath; public static string LogFolder=>Path.Combine(Folder,"Logs");
 public static string AssemblyFolder=>Path.GetDirectoryName(typeof(SettingsService).Assembly.Location)??"";
 public static string BundledStandardFolder=>Path.Combine(AssemblyFolder,"standard");
 public static AppSettings Load(){Directory.CreateDirectory(Folder);AppSettings s;try{s=File.Exists(FilePath)?JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath))??new():new();}catch{s=new();}
  if(string.IsNullOrWhiteSpace(s.RulesConfigPath))s.RulesConfigPath=Path.Combine(AssemblyFolder,"LseDoctorRules.json");if(string.IsNullOrWhiteSpace(s.BackupFolderName))s.BackupFolderName="_LSE_DOCTOR_BACKUP";if(s.BackupsToKeep<1)s.BackupsToKeep=1;return s;}
 public static void Save(AppSettings s){Directory.CreateDirectory(Folder);string t=FilePath+".tmp";File.WriteAllText(t,JsonSerializer.Serialize(s,new JsonSerializerOptions{WriteIndented=true}));File.Move(t,FilePath,true);}
 public static void Remember(AppSettings s,string wdp){s.RecentProjects.RemoveAll(x=>x.Equals(wdp,StringComparison.OrdinalIgnoreCase));s.RecentProjects.Insert(0,wdp);if(s.RecentProjects.Count>25)s.RecentProjects.RemoveRange(25,s.RecentProjects.Count-25);try{Save(s);}catch{}}
 public static string? BundledStandardWdp(){try{return Directory.Exists(BundledStandardFolder)?Directory.EnumerateFiles(BundledStandardFolder,"*.wdp").OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).FirstOrDefault():null;}catch{return null;}}
}
public static class RulesService {
 static readonly JsonSerializerOptions Options=new(){PropertyNameCaseInsensitive=true,ReadCommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true};
 public static DoctorRules Load(string path){DoctorRules r;if(!File.Exists(path))r=new();else r=JsonSerializer.Deserialize<DoctorRules>(File.ReadAllText(path),Options)??throw new InvalidOperationException("The doctor rules JSON is empty: "+path);
  if(!r.PathListMode.Equals("replace",StringComparison.OrdinalIgnoreCase)&&!r.PathListMode.Equals("merge",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("pathListMode must be \"replace\" or \"merge\" in "+path);
  if(r.WdmBlockName.Trim().Length==0)r.WdmBlockName="WD_M";if(r.DescriptionLineCount<0)r.DescriptionLineCount=0;r.Labels=new(r.Labels,StringComparer.OrdinalIgnoreCase);r.Source=File.Exists(path)?path:"(built-in defaults; "+path+" not found)";return r;}
}
