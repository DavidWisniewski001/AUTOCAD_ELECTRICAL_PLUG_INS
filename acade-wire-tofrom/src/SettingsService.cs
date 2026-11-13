using System.Text.Json;
namespace LSE.AutoCADElectrical.WireToFrom;
public static class SettingsService {
 static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"LSE","LSE_ToFrom");
 static readonly string FilePath=Path.Combine(Folder,"settings.json"); public static string SettingsPath=>FilePath; public static string LogFolder=>Path.Combine(Folder,"Logs");
 public static string AssemblyFolder=>Path.GetDirectoryName(typeof(SettingsService).Assembly.Location)??"";
 public static readonly JsonSerializerOptions Json=new(){WriteIndented=true,PropertyNameCaseInsensitive=true,ReadCommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true};
 public static AppSettings Load(){Directory.CreateDirectory(Folder);AppSettings s;try{s=File.Exists(FilePath)?JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath),Json)??new():new();}catch{s=new();}
  if(string.IsNullOrWhiteSpace(s.ConfigPath))s.ConfigPath=Path.Combine(AssemblyFolder,"tofrom.config.json");if(string.IsNullOrWhiteSpace(s.BackupFolderName))s.BackupFolderName="_LSE_TOFROM_BACKUP";if(s.BackupsToKeep<1)s.BackupsToKeep=1;
  if(!s.DataSource.Equals("report",StringComparison.OrdinalIgnoreCase))s.DataSource="scratch";if(!s.StalePolicy.Equals("stop",StringComparison.OrdinalIgnoreCase)&&!s.StalePolicy.Equals("ignore",StringComparison.OrdinalIgnoreCase))s.StalePolicy="warn";return Upgrade(s);}
 // A settings file written by the first cut carries the single-function bridge expressions; move it to the multi-candidate defaults.
 public static AppSettings Upgrade(AppSettings s){if(string.IsNullOrWhiteSpace(s.ActiveProjectLisp)||s.ActiveProjectLisp.Trim()==AppSettings.LegacyActiveProjectLisp)s.ActiveProjectLisp=AppSettings.DefaultActiveProjectLisp;if(string.IsNullOrWhiteSpace(s.ScratchDbLisp)||s.ScratchDbLisp.Trim()==AppSettings.LegacyScratchDbLisp)s.ScratchDbLisp=AppSettings.DefaultScratchDbLisp;return s;}
 public static void Save(AppSettings s){Directory.CreateDirectory(Folder);string t=FilePath+".tmp";File.WriteAllText(t,JsonSerializer.Serialize(s,Json));File.Move(t,FilePath,true);}
 public static void Remember(AppSettings s,string wdp){s.RecentProjects.RemoveAll(x=>x.Equals(wdp,StringComparison.OrdinalIgnoreCase));s.RecentProjects.Insert(0,wdp);if(s.RecentProjects.Count>25)s.RecentProjects.RemoveRange(25,s.RecentProjects.Count-25);try{Save(s);}catch{}}
}
public static class ConfigService {
 public static ToFromConfig Load(string path){ToFromConfig c;if(!File.Exists(path))c=new();else c=JsonSerializer.Deserialize<ToFromConfig>(File.ReadAllText(path),SettingsService.Json)??throw new InvalidOperationException("The LSE_ToFrom config JSON is empty: "+path);
  Validate(c,path);c.Source=File.Exists(path)?path:"(built-in defaults; "+path+" not found)";return c;}
 public static void Save(ToFromConfig c,string path){Validate(c,path);string src=c.Source;c.Source="";try{Directory.CreateDirectory(Path.GetDirectoryName(path)??"");string t=path+".tmp";File.WriteAllText(t,JsonSerializer.Serialize(c,SettingsService.Json));File.Move(t,path,true);}finally{c.Source=src.Length>0?src:path;}}
 public static void Validate(ToFromConfig c,string path){
  if(c.Format.Trim().Length==0)throw new InvalidOperationException("format is empty in "+path);if(c.FarEndFormat.Trim().Length==0)c.FarEndFormat=c.Format;if(c.TagPinFormat.Trim().Length==0)c.TagPinFormat="{TAG}:{PIN}";
  var sc=c.Schematic;if(sc.LabelLayer.Trim().Length==0)throw new InvalidOperationException("schematic.labelLayer is empty in "+path);if(sc.LabelLayer.IndexOfAny(new[]{'<','>','/','\\','"',':',';','?','*','|',',','=','`'})>=0)throw new InvalidOperationException("schematic.labelLayer '"+sc.LabelLayer+"' is not a valid AutoCAD layer name.");
  foreach(var (n,v) in new[]{("blockH",sc.BlockH),("blockV",sc.BlockV),("textAttribute",sc.TextAttribute),("nodeAttribute",sc.NodeAttribute),("keyAttribute",sc.KeyAttribute)})if(v.Trim().Length==0)throw new InvalidOperationException("schematic."+n+" is empty in "+path);
  if(!new[]{"from","to","both"}.Contains(sc.LabelAt.Trim(),StringComparer.OrdinalIgnoreCase))throw new InvalidOperationException("schematic.labelAt must be from, to or both (is '"+sc.LabelAt+"').");sc.LabelAt=sc.LabelAt.Trim().ToLowerInvariant();
  if(sc.TextHeight<=0)throw new InvalidOperationException("schematic.textHeight must be greater than 0.");if(sc.CollisionTries<0)sc.CollisionTries=0;
  if(c.Csv.Delimiter.Length==0)c.Csv.Delimiter=",";if(c.Csv.Delimiter.Equals("\\t",StringComparison.Ordinal)||c.Csv.Delimiter.Equals("tab",StringComparison.OrdinalIgnoreCase))c.Csv.Delimiter="\t";
  if(!c.Csv.Encoding.Equals("ansi",StringComparison.OrdinalIgnoreCase)&&!c.Csv.Encoding.Equals("utf8",StringComparison.OrdinalIgnoreCase))c.Csv.Encoding="utf8bom";
  if(c.Database.Providers.Count==0)c.Database.Providers=new(){"Microsoft.ACE.OLEDB.16.0","Microsoft.ACE.OLEDB.12.0"};if(c.Database.PathPattern.Trim().Length==0)c.Database.PathPattern=@"{FOLDER}\{PROJECT}.mdb";
  string sql=c.Database.WireSql.Trim();if(sql.Length>0&&!sql.StartsWith("SELECT",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("database.wireSql must be a SELECT statement; the scratch database is never written.");
  string dsql=c.Database.DrawingSql.Trim();if(dsql.Length>0&&!dsql.StartsWith("SELECT",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("database.drawingSql must be a SELECT statement.");
  foreach(var (role,col,req) in c.Database.Columns.All())if(req&&col.Trim().Length==0)throw new InvalidOperationException("database.columns."+role+" is empty in "+path);}
}
