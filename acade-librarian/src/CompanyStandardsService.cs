using System.Text.Json;
namespace LSE.AutoCADElectrical.Librarian;
// CompanyStandards.json: the curated list of catalog records (by CatalogRecord.Key) that the catalog browser shows in its
// "Company Standards" scope. Merged from the former LSE Catalog Tools plugin on 2026-11-12. Read and written only here.
public static class CompanyStandardsService {
 static readonly JsonSerializerOptions Options=new(){WriteIndented=true,PropertyNameCaseInsensitive=true,ReadCommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true};
 public static List<StandardEntry> Load(string path){try{return path.Trim().Length>0&&File.Exists(path)?JsonSerializer.Deserialize<List<StandardEntry>>(File.ReadAllText(path),Options)??new():new();}catch{return new();}}
 public static void Save(string path,List<StandardEntry> x){if(path.Trim().Length==0)throw new InvalidOperationException("The company standards JSON path is not set (LSELIBRARYSETTINGS).");Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);string t=path+".tmp";File.WriteAllText(t,JsonSerializer.Serialize(x,Options));File.Move(t,path,true);}
 public static StandardEntry? Find(List<StandardEntry> x,string key)=>x.FirstOrDefault(e=>e.RecordKey.Equals(key,StringComparison.OrdinalIgnoreCase));
 // Lists the record as a company standard, or removes it when it is already listed (legacy or not). True = listed afterwards.
 public static bool Toggle(List<StandardEntry> x,string key,string category){var e=Find(x,key);if(e is not null){x.Remove(e);return false;}x.Add(new StandardEntry{RecordKey=key,Category=category.Length==0?"Uncategorized":category});return true;}
}
// Friendly names of the default_cat.mdb family tables: the catalog's own _FAMILY_DESCRIPTION text when present, else a
// built-in list for the common AutoCAD Electrical codes, else the code itself.
public static class FamilyNames {
 static readonly Dictionary<string,string> Names=new(StringComparer.OrdinalIgnoreCase){{"TRMS","Terminal Blocks"},{"PB","Pushbuttons"},{"CR","Control Relays"},{"CB","Circuit Breakers"},{"DISC","Disconnects"},{"FU","Fuses"},{"LT","Pilot Lights"},{"MOT","Motors"},{"PLCIO","PLC I/O"},{"PS","Power Supplies"},{"SS","Selector Switches"},{"VFD","Variable Frequency Drives"},{"MISC_CAT","Miscellaneous"}};
 public static string Friendly(string table,IReadOnlyDictionary<string,string>? descriptions=null){if(descriptions is not null&&descriptions.TryGetValue(table,out var d)&&d.Trim().Length>0)return d.Trim();return Names.TryGetValue(table,out var n)?n:table;}
 public static IReadOnlyDictionary<string,string> BuiltIn=>Names;
}
