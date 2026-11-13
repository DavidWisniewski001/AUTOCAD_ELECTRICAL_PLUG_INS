using System.Text;
namespace LSE.AutoCADElectrical.WireToFrom;
// Reads a Wire From/To report that AutoCAD Electrical saved to a text/CSV file (Reports > Schematic > Wire From/To > Save to File).
// This is the fallback data source when the scratch-database query is not confirmed yet, and the ground truth the CSV export is compared with.
public static class ReportImport {
 public static List<WireRow> Read(string path,ReportFileConfig c,List<string> notes){if(!File.Exists(path))throw new FileNotFoundException("Report file was not found.",path);
  var lines=File.ReadAllLines(path,Detect(path)).ToList();if(lines.All(l=>l.Trim().Length==0))throw new InvalidOperationException("The report file is empty: "+path);
  char delim=c.Delimiter.Length>0?(c.Delimiter.Equals("\\t",StringComparison.Ordinal)||c.Delimiter.Equals("tab",StringComparison.OrdinalIgnoreCase)?'\t':c.Delimiter[0]):Guess(lines.First(l=>l.Trim().Length>0));
  var m=c.Columns;int headerAt=-1;List<string>? header=null;
  for(int i=0;i<Math.Min(lines.Count,30);i++){if(lines[i].Trim().Length==0)continue;var f=Split(lines[i],delim).Select(x=>x.Trim()).ToList();if(f.Any(x=>x.Equals(m.Cmp1,StringComparison.OrdinalIgnoreCase))&&f.Any(x=>x.Equals(m.WireHdl1,StringComparison.OrdinalIgnoreCase)||x.Equals(m.Cmp2,StringComparison.OrdinalIgnoreCase))){headerAt=i;header=f;break;}}
  if(header is null){if(!c.HasHeader)throw new InvalidOperationException("reportFile.hasHeader is false, but a header row is required to map the columns. Save the report with a header line, or map the columns in the config.");
   throw new InvalidOperationException("No header row with the columns "+m.Cmp1+" and "+m.WireHdl1+" was found in the first 30 lines of "+path+". First line: "+lines.First(l=>l.Trim().Length>0));}
  var missing=m.All().Where(x=>x.Required&&!header.Contains(x.Column,StringComparer.OrdinalIgnoreCase)).Select(x=>x.Role+" ("+x.Column+")").ToList();
  if(missing.Count>0)throw new InvalidOperationException("The report header has no column for "+string.Join(", ",missing)+". Header: "+string.Join(delim.ToString(),header)+". Add the field to the report format or change reportFile.columns in the config.");
  var list=new List<WireRow>();for(int i=headerAt+1;i<lines.Count;i++){if(lines[i].Trim().Length==0)continue;var f=Split(lines[i],delim);if(f.Count<2)continue;var rec=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);for(int k=0;k<header.Count&&k<f.Count;k++)rec[header[k]]=f[k].Trim();
   var row=IdentifierService.MapRow(k=>rec.TryGetValue(k,out var v)?v:"",m,i+1);if(row.Cmp1.Length==0&&row.Cmp2.Length==0&&row.WireHdl1.Length==0)continue;list.Add(row);}
  notes.Add($"Report file: {path}  ({list.Count} row(s), delimiter '{(delim=='\t'?"tab":delim.ToString())}', header on line {headerAt+1})");return list;}
 static Encoding Detect(string path){byte[] b=new byte[3];using(var f=File.OpenRead(path)){int n=f.Read(b,0,3);if(n>=3&&b[0]==0xEF&&b[1]==0xBB&&b[2]==0xBF)return new UTF8Encoding(true);if(n>=2&&b[0]==0xFF&&b[1]==0xFE)return Encoding.Unicode;}
  try{new UTF8Encoding(false,true).GetString(File.ReadAllBytes(path));return new UTF8Encoding(false);}catch(DecoderFallbackException){return Encoding.Latin1;}}
 static char Guess(string line){int tabs=line.Count(ch=>ch=='\t'),semis=line.Count(ch=>ch==';'),commas=line.Count(ch=>ch==',');if(tabs>=semis&&tabs>=commas&&tabs>0)return '\t';return semis>commas?';':',';}
 public static List<string> Split(string line,char delim){var f=new List<string>();var sb=new StringBuilder();bool q=false;for(int i=0;i<line.Length;i++){char ch=line[i];
   if(q){if(ch=='"'){if(i+1<line.Length&&line[i+1]=='"'){sb.Append('"');i++;}else q=false;}else sb.Append(ch);}
   else if(ch=='"')q=true;else if(ch==delim){f.Add(sb.ToString());sb.Clear();}else sb.Append(ch);}f.Add(sb.ToString());return f;}
}
