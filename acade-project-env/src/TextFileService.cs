using System.Text;
namespace LSE.AutoCADElectrical.ProjectEnvironment;
public static class TextFileService {
 public static TextDocument Read(string path){var d=new TextDocument{Path=path};if(!File.Exists(path))return d;d.Exists=true;byte[] b=File.ReadAllBytes(path);int skip;string text;
  if(b.Length>=3&&b[0]==0xEF&&b[1]==0xBB&&b[2]==0xBF){d.Encoding=new UTF8Encoding(true);skip=3;text=d.Encoding.GetString(b,skip,b.Length-skip);}
  else if(b.Length>=2&&b[0]==0xFF&&b[1]==0xFE){d.Encoding=new UnicodeEncoding(false,true);skip=2;text=d.Encoding.GetString(b,skip,b.Length-skip);}
  else if(b.Length>=2&&b[0]==0xFE&&b[1]==0xFF){d.Encoding=new UnicodeEncoding(true,true);skip=2;text=d.Encoding.GetString(b,skip,b.Length-skip);}
  else{try{text=new UTF8Encoding(false,true).GetString(b);d.Encoding=new UTF8Encoding(false);}catch(DecoderFallbackException){d.Encoding=Encoding.Latin1;text=d.Encoding.GetString(b);}}
  d.NewLine=text.Contains("\r\n",StringComparison.Ordinal)?"\r\n":"\n";d.Lines=text.Split('\n').Select(x=>x.TrimEnd('\r')).ToList();if(d.Lines.Count>0&&d.Lines[^1].Length==0)d.Lines.RemoveAt(d.Lines.Count-1);return d;}
 public static void Write(TextDocument d){string text=d.Lines.Count==0?"":string.Join(d.NewLine,d.Lines)+d.NewLine;Directory.CreateDirectory(Path.GetDirectoryName(d.Path)??"");string t=d.Path+".tmp";using(var f=new FileStream(t,FileMode.Create,FileAccess.Write)){byte[] p=d.Encoding.GetPreamble();f.Write(p,0,p.Length);byte[] body=d.Encoding.GetBytes(text);f.Write(body,0,body.Length);}File.Move(t,d.Path,true);d.Exists=true;d.Changed=false;}
 public static string EncodingName(Encoding e)=>e.CodePage==1200?"UTF-16 LE":e.CodePage==1201?"UTF-16 BE":e.CodePage==65001?(e.GetPreamble().Length>0?"UTF-8 with BOM":"UTF-8"):e.CodePage==28591?"ANSI (Latin-1)":e.WebName;
 public static bool TryIndexed(string line,char prefix,out int index,out string value){index=0;value="";if(line.Length<4||line[0]!=prefix||line[1]!='[')return false;int close=line.IndexOf(']',2);if(close<0||!int.TryParse(line.AsSpan(2,close-2),out index))return false;value=line[(close+1)..];return true;}
 public static int Find(TextDocument d,char prefix,int index){for(int i=0;i<d.Lines.Count;i++)if(TryIndexed(d.Lines[i],prefix,out int n,out _)&&n==index)return i;return -1;}
 public static string? Get(TextDocument d,char prefix,int index){int i=Find(d,prefix,index);return i<0?null:(TryIndexed(d.Lines[i],prefix,out _,out string v)?v:null);}
 public static SortedDictionary<int,string> Group(TextDocument d,char prefix){var g=new SortedDictionary<int,string>();foreach(string l in d.Lines)if(TryIndexed(l,prefix,out int n,out string v)&&!g.ContainsKey(n))g[n]=v;return g;}
 public static void Set(TextDocument d,char prefix,int index,string value){string line=prefix+"["+index+"]"+value;int at=Find(d,prefix,index);if(at>=0){if(d.Lines[at]==line)return;d.Lines[at]=line;d.Changed=true;return;}
  int insert=-1;for(int i=0;i<d.Lines.Count;i++)if(TryIndexed(d.Lines[i],prefix,out int n,out _)){if(n<index)insert=i+1;else{if(insert<0)insert=i;break;}}
  if(insert<0){int last=-1;foreach(char p in GroupOrderBefore(prefix))for(int i=0;i<d.Lines.Count;i++)if(d.Lines[i].Length>1&&d.Lines[i][0]==p&&d.Lines[i][1]=='[')last=i;insert=last+1;}
  d.Lines.Insert(insert,line);d.Changed=true;}
 static IEnumerable<char> GroupOrderBefore(char prefix)=>prefix switch{'*'=>Array.Empty<char>(),'+'=>new[]{'*'},_=>new[]{'*','+'}};
 public static List<WdpDrawingEntry> Drawings(TextDocument d){var list=new List<WdpDrawingEntry>();var pending=new WdpDrawingEntry{FirstLine=-1};for(int i=0;i<d.Lines.Count;i++){string line=d.Lines[i];if(line.Trim().Length==0)continue;
   if(line.StartsWith("*",StringComparison.Ordinal)||line.StartsWith("?",StringComparison.Ordinal)||line.StartsWith("+",StringComparison.Ordinal))continue;
   if(pending.FirstLine<0)pending.FirstLine=i;
   if(line.StartsWith("=====SUB=",StringComparison.Ordinal)){pending.Folder=line[9..];continue;}if(line.StartsWith("====REF",StringComparison.Ordinal)){pending.ReferenceOnly=true;continue;}
   if(line.StartsWith("===",StringComparison.Ordinal)){pending.Descriptions.Add(line[3..]);continue;}if(line.StartsWith("==",StringComparison.Ordinal)){pending.Subsection=line[2..];continue;}if(line.StartsWith("=",StringComparison.Ordinal)){pending.Section=line[1..];continue;}
   pending.Path=line.Trim();pending.PathLine=i;list.Add(pending);pending=new WdpDrawingEntry{FirstLine=-1};}return list;}
 public static string FullPath(string wdpPath,WdpDrawingEntry e){string folder=Path.GetDirectoryName(wdpPath)??"";try{return Path.IsPathRooted(e.Path)?Path.GetFullPath(e.Path):Path.GetFullPath(Path.Combine(folder,e.Path));}catch{return e.Path;}}
 public static string LabelsPath(string wdpPath)=>Path.Combine(Path.GetDirectoryName(wdpPath)??"",Path.GetFileNameWithoutExtension(wdpPath)+"_wdtitle.wdl");
 public static string MappingPath(string wdpPath)=>Path.ChangeExtension(wdpPath,".wdt");
 public static string? ExistingCaseInsensitive(string path){if(File.Exists(path))return path;string folder=Path.GetDirectoryName(path)??"";string name=Path.GetFileName(path);try{return Directory.Exists(folder)?Directory.EnumerateFiles(folder).FirstOrDefault(f=>Path.GetFileName(f).Equals(name,StringComparison.OrdinalIgnoreCase)):null;}catch{return null;}}
 public static List<string> PathList(string value)=>value.Split(';').Select(x=>x.Trim()).Where(x=>x.Length>0).ToList();
}
