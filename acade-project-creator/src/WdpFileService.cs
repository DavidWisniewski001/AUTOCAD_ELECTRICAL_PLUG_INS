using System.Text;
namespace LSE.AutoCADElectrical.ProjectTools;
public static class WdpFileService {
 public static WdpDocument Parse(string path){if(!File.Exists(path))throw new FileNotFoundException("Project file was not found.",path);var d=new WdpDocument();string[] lines;using(var r=new StreamReader(path,new UTF8Encoding(false),true)){string text=r.ReadToEnd();d.Encoding=r.CurrentEncoding;lines=text.Split('\n');}
  var pending=new WdpDrawing();foreach(string raw in lines){string line=raw.TrimEnd('\r');if(line.Trim().Length==0)continue;
   if(TryIndexed(line,'*',out int i,out string v)){d.Descriptions[i]=v;continue;}if(TryIndexed(line,'?',out i,out v)){d.Properties[i]=v;continue;}if(TryIndexed(line,'+',out i,out v)){d.Options[i]=v;continue;}
   if(line.StartsWith("=====SUB=",StringComparison.Ordinal)){pending.Folder=line[9..];continue;}if(line.StartsWith("====REF",StringComparison.Ordinal)){pending.ReferenceOnly=true;continue;}
   if(line.StartsWith("===",StringComparison.Ordinal)){pending.Descriptions.Add(line[3..]);continue;}if(line.StartsWith("==",StringComparison.Ordinal)){pending.Subsection=line[2..];continue;}if(line.StartsWith("=",StringComparison.Ordinal)){pending.Section=line[1..];continue;}
   if(line.StartsWith("*",StringComparison.Ordinal)||line.StartsWith("?",StringComparison.Ordinal)||line.StartsWith("+",StringComparison.Ordinal)){d.Unknown.Add(line);continue;}
   pending.Path=line;d.Drawings.Add(pending);pending=new();}
  return d;}
 static bool TryIndexed(string line,char prefix,out int index,out string value){index=0;value="";if(line.Length<4||line[0]!=prefix||line[1]!='[')return false;int close=line.IndexOf(']',2);if(close<0||!int.TryParse(line.AsSpan(2,close-2),out index))return false;value=line[(close+1)..];return true;}
 // Group order *[n], +[n], ?[n] is what the LSE template (AutoCAD Electrical 2026) writes. minDescriptionLines keeps *[1..n] positional even when blank.
 public static string Render(string wdpPath,WdpDocument d,int minDescriptionLines=8){var sb=new StringBuilder();int max=Math.Max(d.Descriptions.Count==0?0:d.Descriptions.Keys.Max(),minDescriptionLines);for(int i=1;i<=max;i++)sb.Append("*[").Append(i).Append(']').Append(d.Descriptions.TryGetValue(i,out var v)?v:"").Append("\r\n");
  foreach(var p in d.Options)sb.Append("+[").Append(p.Key).Append(']').Append(p.Value).Append("\r\n");foreach(var p in d.Properties)sb.Append("?[").Append(p.Key).Append(']').Append(p.Value).Append("\r\n");string folder=Path.GetDirectoryName(wdpPath)??"";
  foreach(var w in d.Drawings){if(w.Folder.Length>0)sb.Append("=====SUB=").Append(w.Folder).Append("\r\n");if(w.ReferenceOnly)sb.Append("====REF\r\n");int last=w.Descriptions.FindLastIndex(x=>x.Length>0);for(int i=0;i<=last;i++)sb.Append("===").Append(w.Descriptions[i]).Append("\r\n");
   if(w.Section.Length>0)sb.Append('=').Append(w.Section).Append("\r\n");if(w.Subsection.Length>0)sb.Append("==").Append(w.Subsection).Append("\r\n");sb.Append(Relative(folder,w.Path)).Append("\r\n");}
  return sb.ToString();}
 public static void Write(string wdpPath,WdpDocument d,int minDescriptionLines=8)=>WriteText(wdpPath,Render(wdpPath,d,minDescriptionLines),d.Encoding);
 public static string Relative(string folder,string file){if(folder.Length==0||!Path.IsPathRooted(file))return file;string rel=Path.GetRelativePath(folder,file);return rel.StartsWith("..",StringComparison.Ordinal)||Path.IsPathRooted(rel)?file:rel;}
 public static void WriteText(string path,string text,Encoding enc){string t=path+".tmp";File.WriteAllText(t,text,enc);File.Move(t,path,true);}
 public static string LabelsPath(string wdpPath)=>Path.Combine(Path.GetDirectoryName(wdpPath)??"",Path.GetFileNameWithoutExtension(wdpPath)+"_wdtitle.wdl");
 public static string MappingPath(string wdpPath)=>Path.ChangeExtension(wdpPath,".wdt");
 public static string ConfigurationPath(string wdpPath)=>Path.ChangeExtension(wdpPath,".aepx");
 public static string? ExistingCaseInsensitive(string path){if(File.Exists(path))return path;string dir=Path.GetDirectoryName(path)??"";if(!Directory.Exists(dir))return null;try{return Directory.EnumerateFiles(dir).FirstOrDefault(f=>Path.GetFileName(f).Equals(Path.GetFileName(path),StringComparison.OrdinalIgnoreCase));}catch{return null;}}
 // The template's own sidecar files are the standard: copying them (renamed) beats regenerating them. Returns null when the template has none.
 public static string? CopySidecar(string wdpPath,string baseProjectPath,Func<string,string> pathOf){if(baseProjectPath.Trim().Length==0)return null;string? src=ExistingCaseInsensitive(pathOf(baseProjectPath));if(src is null)return null;string dst=pathOf(wdpPath);File.Copy(src,dst+".tmp",true);File.Move(dst+".tmp",dst,true);return dst;}
 // LINEn=label without spaces, ASCII: the form the LSE template _wdtitle.wdl uses.
 public static string WriteLabels(string wdpPath,IEnumerable<DescriptionLineDef> lines,LabelFileDef extra){var map=new SortedDictionary<int,string>();foreach(var l in lines.Where(x=>x.Label.Trim().Length>0))map[l.Index]=l.Label.Trim();foreach(var e in extra.ExtraLabels)if(int.TryParse(e.Key,out int i)&&!map.ContainsKey(i)&&e.Value.Trim().Length>0)map[i]=e.Value.Trim();
  var sb=new StringBuilder();foreach(var m in map)sb.Append("LINE").Append(m.Key).Append('=').Append(m.Value).Append("\r\n");string p=LabelsPath(wdpPath);WriteText(p,sb.ToString(),new UTF8Encoding(false));return p;}
 // Same order as the LSE template .wdt: BLOCK, file name, drawing descriptions, then the LINEn mappings.
 public static string? WriteMapping(string wdpPath,ProjectTemplates t,Encoding enc){var tb=t.TitleBlock;var names=tb.BlockNames.Where(x=>x.Trim().Length>0).Select(x=>x.Trim()).ToList();if(names.Count==0)return null;var sb=new StringBuilder();sb.Append(";; Title block mapping generated by LSE Project Tools ").Append(DateTime.Now.ToString("yyyy-MM-dd")).Append(" from ").Append(Path.GetFileName(t.Source)).Append("\r\n");sb.Append("BLOCK = ").Append(string.Join(",",names)).Append("\r\n");
  Map(sb,tb.FileNameAttribute,"FILENAME");for(int i=0;i<tb.DescriptionAttributes.Count&&i<3;i++)Map(sb,tb.DescriptionAttributes[i],"DD"+(i+1));foreach(var l in t.DescriptionLines.Where(x=>x.Attribute.Trim().Length>0).OrderBy(x=>x.Index))Map(sb,l.Attribute,"LINE"+l.Index);
  Map(sb,tb.SheetAttribute,"SHEET");Map(sb,tb.SheetMaxAttribute,"SHEETMAX");Map(sb,tb.SectionAttribute,"DWGSEC");Map(sb,tb.SubsectionAttribute,"DWGSUB");string p=MappingPath(wdpPath);WriteText(p,sb.ToString(),enc);return p;}
 static void Map(StringBuilder sb,string attribute,string code){if(attribute.Trim().Length>0)sb.Append(attribute.Trim()).Append(" = ").Append(code).Append("\r\n");}
 // Same shape as the template .aepx (and as the E-CAD Doctor writes it): one <Drawing> per listed drawing, file names upper-cased, relative to the project folder.
 public static string WriteConfiguration(string wdpPath,IEnumerable<string> drawingPaths){string folder=Path.GetDirectoryName(wdpPath)??"";var paths=drawingPaths.ToList();var sb=new StringBuilder();sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<ProjectConfiguration Version=\"1.0\"><Project ProjectPath=\"").Append(Esc(folder)).Append("\" LocalGUID=\"").Append(Guid.NewGuid().ToString()).Append("\" EmxFilePath=\"\" EmxGUID=\"\"/><Drawings NextID=\"").Append(paths.Count+1).Append("\">");
  for(int i=0;i<paths.Count;i++)sb.Append("<Drawing FilePath=\"").Append(Esc(Relative(folder,paths[i]).ToUpperInvariant())).Append("\" FileID=\"").Append(i+1).Append("\"/>");sb.Append("</Drawings></ProjectConfiguration>\r\n");string p=ConfigurationPath(wdpPath);WriteText(p,sb.ToString(),new UTF8Encoding(false));return p;}
 static string Esc(string v)=>System.Security.SecurityElement.Escape(v)??"";
 public static string EncodingName(Encoding e)=>e.CodePage==1200?"UTF-16 LE":e.CodePage==1201?"UTF-16 BE":e.CodePage==65001?(e.GetPreamble().Length>0?"UTF-8 with BOM":"UTF-8"):e.WebName;
}
