using System.Text;
namespace LSE.AutoCADElectrical.WireToFrom;
public static class ProjectService {
 public static ProjectInfo Read(string wdpPath){if(!File.Exists(wdpPath))throw new FileNotFoundException("Project file was not found.",wdpPath);var p=new ProjectInfo{WdpPath=Path.GetFullPath(wdpPath)};
  var lines=ReadLines(wdpPath);var pending=new ProjectDrawing();int index=0;
  foreach(string raw in lines){string line=raw.TrimEnd('\r');if(line.Trim().Length==0)continue;if(line.StartsWith("*",StringComparison.Ordinal)||line.StartsWith("?",StringComparison.Ordinal)||line.StartsWith("+",StringComparison.Ordinal))continue;
   if(line.StartsWith("=====SUB=",StringComparison.Ordinal))continue;if(line.StartsWith("====REF",StringComparison.Ordinal)){pending.ReferenceOnly=true;continue;}
   if(line.StartsWith("===",StringComparison.Ordinal)){if(pending.Description.Length==0)pending.Description=line[3..];continue;}if(line.StartsWith("==",StringComparison.Ordinal))continue;if(line.StartsWith("=",StringComparison.Ordinal)){pending.Section=line[1..];continue;}
   pending.Path=FullPath(p.WdpPath,line.Trim());pending.Index=++index;p.Drawings.Add(pending);pending=new ProjectDrawing();}
  return p;}
 static List<string> ReadLines(string path){byte[] b=File.ReadAllBytes(path);string text;if(b.Length>=3&&b[0]==0xEF&&b[1]==0xBB&&b[2]==0xBF)text=new UTF8Encoding(true).GetString(b,3,b.Length-3);else if(b.Length>=2&&b[0]==0xFF&&b[1]==0xFE)text=new UnicodeEncoding(false,true).GetString(b,2,b.Length-2);else{try{text=new UTF8Encoding(false,true).GetString(b);}catch(DecoderFallbackException){text=Encoding.Latin1.GetString(b);}}return text.Split('\n').ToList();}
 public static string FullPath(string wdpPath,string entry){string folder=Path.GetDirectoryName(wdpPath)??"";try{return Path.IsPathRooted(entry)?Path.GetFullPath(entry):Path.GetFullPath(Path.Combine(folder,entry));}catch{return entry;}}
 // The scratch database: the path AutoCAD Electrical reported wins when the file exists; then database.pathPattern; then
 // <project>.mdb in each of database.searchFolders, newest file first. Nothing found: the pattern path (reported as missing).
 public static string DatabasePath(ProjectInfo p,DatabaseConfig d,string fromLisp)=>DatabasePath(p,d,fromLisp,null);
 public static string DatabasePath(ProjectInfo p,DatabaseConfig d,string fromLisp,List<string>? searched){if(fromLisp.Trim().Length>0&&File.Exists(fromLisp.Trim()))return Path.GetFullPath(fromLisp.Trim());
  string pattern=Expand(d.PathPattern,p);try{pattern=Path.GetFullPath(pattern);}catch{}searched?.Add(pattern);if(File.Exists(pattern))return pattern;
  var found=new List<string>();foreach(string raw in d.SearchFolders){string folder;try{folder=Path.GetFullPath(Expand(raw,p));}catch{continue;}if(folder.Length==0)continue;string candidate=Path.Combine(folder,p.Name+".mdb");if(searched is not null&&!searched.Contains(candidate,StringComparer.OrdinalIgnoreCase))searched.Add(candidate);if(File.Exists(candidate))found.Add(candidate);}
  return found.Count>0?found.OrderByDescending(File.GetLastWriteTime).First():pattern;}
 public static string Expand(string text,ProjectInfo p)=>text.Replace("{FOLDER}",p.Folder,StringComparison.OrdinalIgnoreCase).Replace("{PROJECT}",p.Name,StringComparison.OrdinalIgnoreCase).Replace("{TEMP}",Path.GetTempPath().TrimEnd('\\'),StringComparison.OrdinalIgnoreCase).Replace("{LOCALAPPDATA}",Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),StringComparison.OrdinalIgnoreCase).Replace("{APPDATA}",Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),StringComparison.OrdinalIgnoreCase);
 public static string OutputFolder(ProjectInfo p,CsvConfig c)=>Path.IsPathRooted(c.OutputFolder)?c.OutputFolder:Path.Combine(p.Folder,c.OutputFolder.Trim().Length==0?"LSE_ToFrom":c.OutputFolder);
 public static string OutputFile(ProjectInfo p,CsvConfig c,string pattern)=>Path.Combine(OutputFolder(p,c),pattern.Replace("{PROJECT}",p.Name,StringComparison.OrdinalIgnoreCase).Replace("{DATE}",DateTime.Now.ToString("yyyyMMdd"),StringComparison.OrdinalIgnoreCase));
}
