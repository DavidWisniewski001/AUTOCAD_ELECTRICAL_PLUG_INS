using System.Text.Json;
namespace LSE.InventorFootprintTools;
public static class SettingsService {
 static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"LSE","InventorFootprintTools");
 static readonly string FilePath=Path.Combine(Folder,"settings.json"); public static string SettingsPath=>FilePath;
 public static AppSettings Load(){Directory.CreateDirectory(Folder);AppSettings s;try{s=File.Exists(FilePath)?JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath))??new():new();}catch{s=new();}
  if(string.IsNullOrWhiteSpace(s.CatalogPath))s.CatalogPath=DiscoverCatalog();string folder=Path.GetDirectoryName(s.CatalogPath)??"";
  if(string.IsNullOrWhiteSpace(s.FootprintLookupPath)&&folder.Length>0)s.FootprintLookupPath=Path.Combine(folder,"footprint_lookup.mdb");
  if(string.IsNullOrWhiteSpace(s.FootprintLibraryFolder)&&folder.Length>0)s.FootprintLibraryFolder=Path.Combine(folder,"LSE_Footprints");
  if(string.IsNullOrWhiteSpace(s.NamingStandardPath))s.NamingStandardPath=Path.Combine(Path.GetDirectoryName(typeof(SettingsService).Assembly.Location)??"","LseNamingStandard.json");return s;}
 public static void Save(AppSettings s){Directory.CreateDirectory(Folder);string t=FilePath+".tmp";File.WriteAllText(t,JsonSerializer.Serialize(s,new JsonSerializerOptions{WriteIndented=true}));File.Move(t,FilePath,true);}
 static string DiscoverCatalog(){string d=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);foreach(string p in new[]{Path.Combine(d,"Acade 2026","AeData","en-US","Catalogs","default_cat.mdb"),Path.Combine(d,"AutoCAD_Electrical_2026","Catalogs","default_cat.mdb")})if(File.Exists(p))return p;return "";}
}
public static class NamingStandardService {
 public static NamingStandard Load(string path){if(!File.Exists(path))throw new FileNotFoundException("The LSE naming standard JSON was not found. Generate it with tools/extract_naming_standard.py or point Settings at a shared copy.",path);var n=JsonSerializer.Deserialize<NamingStandard>(File.ReadAllText(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidOperationException("The naming standard JSON is empty.");
  n.Manufacturers=new(n.Manufacturers,StringComparer.OrdinalIgnoreCase);n.FamilyHints=new(n.FamilyHints,StringComparer.OrdinalIgnoreCase);foreach(var t in n.Tabs){t.Types=new(t.Types,StringComparer.OrdinalIgnoreCase);t.Manufacturers=new(t.Manufacturers,StringComparer.OrdinalIgnoreCase);}if(n.Tabs.Count==0)throw new InvalidOperationException("The naming standard JSON has no tabs.");return n;}
}
