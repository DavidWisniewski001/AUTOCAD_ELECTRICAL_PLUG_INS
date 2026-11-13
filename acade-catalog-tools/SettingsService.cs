using System.Text.Json;
namespace LSE.AutoCADElectrical.CatalogTools;
public static class SettingsService {
 static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"LSE","AutoCADElectricalCatalogTools");
 static readonly string FilePath=Path.Combine(Folder,"settings.json"); public static string SettingsPath=>FilePath;
 public static AppSettings Load(){Directory.CreateDirectory(Folder); AppSettings s; try{s=File.Exists(FilePath)?JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath))??new():new();}catch{s=new();}
  if(string.IsNullOrWhiteSpace(s.CatalogPath))s.CatalogPath=DiscoverCatalog(); if(string.IsNullOrWhiteSpace(s.CompanyStandardsPath))s.CompanyStandardsPath=Path.Combine(Folder,"CompanyStandards.json"); return s;}
 public static void Save(AppSettings s){Directory.CreateDirectory(Folder); string t=FilePath+".tmp"; File.WriteAllText(t,JsonSerializer.Serialize(s,new JsonSerializerOptions{WriteIndented=true})); File.Move(t,FilePath,true);}
 static string DiscoverCatalog(){string p=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Acade 2026","AeData","en-US","Catalogs","default_cat.mdb");return File.Exists(p)?p:"";}
}
public static class StandardsService {
 public static List<StandardEntry> Load(string path){try{return File.Exists(path)?JsonSerializer.Deserialize<List<StandardEntry>>(File.ReadAllText(path))??new():new();}catch{return new();}}
 public static void Save(string path,List<StandardEntry> x){Directory.CreateDirectory(Path.GetDirectoryName(path)!);string t=path+".tmp";File.WriteAllText(t,JsonSerializer.Serialize(x,new JsonSerializerOptions{WriteIndented=true}));File.Move(t,path,true);}
}
public static class FamilyNames {
 static readonly Dictionary<string,string> Names=new(StringComparer.OrdinalIgnoreCase){{"TRMS","Terminal Blocks"},{"PB","Pushbuttons"},{"CR","Control Relays"},{"CB","Circuit Breakers"},{"DISC","Disconnects"},{"FU","Fuses"},{"LT","Pilot Lights"},{"MOT","Motors"},{"PLCIO","PLC I/O"},{"PS","Power Supplies"},{"SS","Selector Switches"},{"VFD","Variable Frequency Drives"},{"MISC_CAT","Miscellaneous"}};
 public static string Friendly(string table)=>Names.TryGetValue(table,out var value)?value:table; public static IReadOnlyDictionary<string,string> All=>Names;
}
