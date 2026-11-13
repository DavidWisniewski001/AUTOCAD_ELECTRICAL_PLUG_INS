using System.Globalization;using System.IO.Compression;using System.Text;using System.Text.Json;using System.Text.RegularExpressions;using System.Xml;
namespace LSE.AutoCADElectrical.Librarian;
// Reads the LSE part-number naming standard straight out of "LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx".
// Every "LSE ..." tab starts with a "PROPOSED ... NAMING & DESCRIPTION STANDARD" block: the part-number rule
// ([Category 2][Mfr code 3][Type 2][Sequence 7]), a "Part # Segment" table (category codes, type codes, sequence rule)
// with a Manufacturer | Code table beside it (its column differs per tab), a "Description Field" table with the
// numbered rules, an "Examples" block, and then the part tables ("Internal ID | LSE Part Number | ... |
// LSE Recommended Part Number | ..."). The workbook is an ordinary xlsx (a zip of XML), so it is read with
// System.IO.Compression and XmlReader: no Excel, no NuGet, and the file is opened shared so it may stay open in Excel.
public static class NamingWorkbookService {
 static readonly object Gate=new(); static NamingStandard? cached; static string cacheKey="";
 static readonly Regex CodeRe=new(@"^[A-Z]{3}(\s*/\s*[A-Z]{3})*$"),FormatRe=new(@"\[Category\s*(\d+)\]\s*\[Mfr code\s*(\d+)\]\s*\[Type\s*(\d+)\]\s*\[Sequence\s*(\d+)\]",RegexOptions.IgnoreCase),RuleRe=new(@"^\d+\.\s");
 static readonly JsonSerializerOptions JsonOptions=new(){PropertyNameCaseInsensitive=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase,ReadCommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true,WriteIndented=true};
 public const string WorkbookFileName="LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx";

 // The workbook wins; the JSON snapshot (config\LseNamingStandard.json, same shape as the Inventor add-in's) is the fallback for workstations that cannot reach it.
 public static NamingStandard Load(AppSettings s){string wb=s.NamingWorkbookPath.Trim();if(wb.Length>0&&File.Exists(wb))return LoadWorkbook(wb);if(s.NamingStandardPath.Trim().Length>0&&File.Exists(s.NamingStandardPath))return LoadJson(s.NamingStandardPath);throw new FileNotFoundException("Neither the LSE parts catalog workbook nor the naming standard JSON snapshot was found. Set 'LSE parts catalog workbook (.xlsx)' in LSELIBRARYSETTINGS.",wb.Length>0?wb:s.NamingStandardPath);}
 public static string Describe(NamingStandard n)=>$"{n.Tabs.Count} naming tabs, {n.Manufacturers.Count} manufacturer codes, {n.Tabs.Sum(t=>t.Types.Count)} type codes, {n.AllKnownPartNumbers().Count()} part numbers listed, format {n.Format.CategoryChars}+{n.Format.ManufacturerChars}+{n.Format.TypeChars}+{n.Format.SequenceChars} = {n.Format.Length} chars"+(n.SkippedTabs.Count>0?$"; no naming block on: {string.Join(", ",n.SkippedTabs)}":"");

 public static NamingStandard LoadWorkbook(string path,bool force=false){
  if(!File.Exists(path))throw new FileNotFoundException("The LSE parts catalog workbook was not found.",path);var fi=new FileInfo(path);string key=fi.FullName.ToUpperInvariant()+"|"+fi.LastWriteTimeUtc.Ticks+"|"+fi.Length;
  lock(Gate){if(!force&&cached is not null&&cacheKey==key)return cached;var n=Parse(ReadSheets(path,name=>name.StartsWith("LSE ",StringComparison.OrdinalIgnoreCase)),Path.GetFileName(path));n.Source=path;n.FromWorkbook=true;cached=n;cacheKey=key;return n;}}
 public static NamingStandard LoadJson(string path){var n=JsonSerializer.Deserialize<NamingStandard>(File.ReadAllText(path),JsonOptions)??throw new InvalidOperationException("The naming standard JSON is empty: "+path);
  n.Manufacturers=new(n.Manufacturers,StringComparer.OrdinalIgnoreCase);n.FamilyHints=new(n.FamilyHints,StringComparer.OrdinalIgnoreCase);foreach(var t in n.Tabs){t.Types=new(t.Types,StringComparer.OrdinalIgnoreCase);t.Manufacturers=new(t.Manufacturers,StringComparer.OrdinalIgnoreCase);t.ManufacturerSpellings=new(t.ManufacturerSpellings,StringComparer.OrdinalIgnoreCase);}
  if(n.Tabs.Count==0)throw new InvalidOperationException("The naming standard JSON has no tabs: "+path);if(n.Format.Length<8)n.Format=new();if(n.Source.Length==0)n.Source=path;n.FromWorkbook=false;n.Loaded=DateTime.Now;return n;}
 public static void SaveJson(NamingStandard n,string path){Directory.CreateDirectory(Path.GetDirectoryName(path)??".");string t=path+".tmp";File.WriteAllText(t,JsonSerializer.Serialize(n,JsonOptions),new UTF8Encoding(false));File.Move(t,path,true);}

 // ---- parsing the naming blocks ----------------------------------------------------------------------------------
 public static NamingStandard Parse(Dictionary<string,List<string[]>> sheets,string sourceName){
  var n=new NamingStandard{Source=sourceName};bool formatSet=false;var spellings=new Dictionary<string,Dictionary<string,int>>(StringComparer.OrdinalIgnoreCase);
  foreach(var (sheet,rows) in sheets){
   var tab=new NamingTab{Name=sheet};int segHeader=-1,descHeader=-1,examplesRow=-1,appendixRow=-1,nameCol=5,codeCol=6;var dataHeaders=new List<int>();
   for(int i=0;i<rows.Count;i++){var r=rows[i];string a=r[0];
    if(i==0&&a.Contains("NAMING",StringComparison.OrdinalIgnoreCase))tab.Title=a;
    if(a.StartsWith("Part number",StringComparison.OrdinalIgnoreCase)&&tab.PartNumberRule.Length==0){tab.PartNumberRule=r[1];var m=FormatRe.Match(r[1]);if(m.Success&&!formatSet){n.Format=new NamingFormat{CategoryChars=int.Parse(m.Groups[1].Value),ManufacturerChars=int.Parse(m.Groups[2].Value),TypeChars=int.Parse(m.Groups[3].Value),SequenceChars=int.Parse(m.Groups[4].Value)};formatSet=true;}}
    else if(a.StartsWith("Description:",StringComparison.OrdinalIgnoreCase)&&r[1].Length>0&&tab.DescriptionPattern.Length==0)tab.DescriptionPattern=r[1];
    else if(a.StartsWith("Purchase Desc",StringComparison.OrdinalIgnoreCase)&&tab.PurchaseDescriptionPattern.Length==0)tab.PurchaseDescriptionPattern=r[1];
    else if(a=="Part # Segment"&&segHeader<0){segHeader=i;int nc=IndexOf(r,"Manufacturer",1),cc=IndexOf(r,"Code",1);if(nc>0&&cc>0){nameCol=nc;codeCol=cc;}}
    else if(a=="Description Field"&&descHeader<0)descHeader=i;
    else if(a=="Examples"&&examplesRow<0)examplesRow=i;
    else if(a=="APPENDIX"&&appendixRow<0)appendixRow=i;
    if(IndexOf(r,"Internal ID",0)>=0)dataHeaders.Add(i);}
   if(segHeader<0){n.SkippedTabs.Add(sheet);continue;}
   var standardForm=PartNumberService.StandardForm(n.Format);
   int segEnd=descHeader>segHeader?descHeader:Math.Min(rows.Count,segHeader+12);
   for(int i=segHeader+1;i<segEnd;i++){var r=rows[i];if(r[0]=="Category"){tab.CategoryRule=r[2];foreach(Match m in Regex.Matches(r[2],@"\b(\d{2})\b"))if(!tab.CategoryCodes.Contains(m.Groups[1].Value))tab.CategoryCodes.Add(m.Groups[1].Value);}
    else if(r[0]=="Type")foreach(Match m in Regex.Matches(r[2],@"\b([A-Z]{2})\s*=\s*([^;]+)"))tab.Types[m.Groups[1].Value]=m.Groups[2].Value.Trim();}
   int mfrEnd=dataHeaders.Count>0?dataHeaders[0]:rows.Count;if(appendixRow>segHeader&&appendixRow<mfrEnd)mfrEnd=appendixRow;
   for(int i=segHeader+1;i<mfrEnd;i++){var r=rows[i];string name=r[nameCol],code=r[codeCol];if(code.Length==0||name.Length==0||name=="Manufacturer"||!CodeRe.IsMatch(code))continue;var names=Regex.Split(name,@"\s*/\s*");var codes=Regex.Split(code,@"\s*/\s*");var pairs=names.Length==codes.Length?names.Zip(codes).ToList():new List<(string,string)>{(name,codes[0])};foreach(var (nm,cd) in pairs){string c=cd.ToUpperInvariant();if(!tab.Manufacturers.ContainsKey(c))tab.Manufacturers[c]=nm;if(!n.Manufacturers.ContainsKey(c))n.Manufacturers[c]=new ManufacturerCode{Name=nm};}}
   if(descHeader>=0){int rulesCol=IndexOf(rows[descHeader],"Rules",1);if(rulesCol<0)rulesCol=nameCol;for(int i=descHeader+1;i<rows.Count;i++){var r=rows[i];if(r[0].Length==0||r[0]=="Examples"||r[0]=="APPENDIX"||IndexOf(r,"Internal ID",0)>=0)break;tab.DescriptionFields.Add(new DescriptionField{Name=r[0],Format=r[1],AllowedValues=r[2],Example=r[3]});}
    for(int i=descHeader+1;i<Math.Min(rows.Count,descHeader+16);i++){string v=rows[i][rulesCol];if(RuleRe.IsMatch(v)&&!tab.Rules.Contains(v))tab.Rules.Add(v);}}
   if(examplesRow>=0)for(int i=examplesRow;i<rows.Count;i++){var r=rows[i];string pn=PartNumberService.Normalize(r[1]);if(!standardForm.IsMatch(pn))break;tab.Examples.Add(new NamingExample{PartNumber=pn,Description=r[2],PurchaseDescription=r[3]});}
   var known=new HashSet<string>(StringComparer.Ordinal);var recommendedCategories=new List<string>();
   foreach(int h in dataHeaders){var hr=rows[h];int pnCol=IndexOf(hr,"LSE Part Number",0),recCol=IndexOf(hr,"LSE Recommended Part Number",0),mfrCol=IndexOf(hr,"Manufacturer",0);
    for(int i=h+1;i<rows.Count&&IndexOf(rows[i],"Internal ID",0)<0;i++){var r=rows[i];foreach(int c in new[]{pnCol,recCol}){if(c<0)continue;string v=PartNumberService.Normalize(r[c]);if(!standardForm.IsMatch(v))continue;known.Add(v);if(c==recCol){string cat=v[..n.Format.CategoryChars];if(!recommendedCategories.Contains(cat))recommendedCategories.Add(cat);if(mfrCol>=0&&r[mfrCol].Length>0){string code=v.Substring(n.Format.CategoryChars,n.Format.ManufacturerChars);if(!spellings.TryGetValue(code,out var counter))spellings[code]=counter=new(StringComparer.OrdinalIgnoreCase);string sp=r[mfrCol].ToUpperInvariant();counter[sp]=counter.TryGetValue(sp,out int k)?k+1:1;if(!tab.ManufacturerSpellings.ContainsKey(code))tab.ManufacturerSpellings[code]=sp;}}}}}
   tab.KnownPartNumbers=known.OrderBy(v=>v,StringComparer.Ordinal).ToList();
   if(tab.CategoryCodes.Count==0){foreach(string c in recommendedCategories.Count>0?recommendedCategories:known.Select(v=>v[..n.Format.CategoryChars]).Distinct())tab.CategoryCodes.Add(c);tab.CategoryCodes.Sort(StringComparer.Ordinal);if(tab.CategoryCodes.Count>0)tab.CategoryRule=(tab.CategoryRule.Length>0?tab.CategoryRule+" ":"")+"(codes taken from the part numbers listed on this tab)";}
   if(tab.Types.Count==0){n.SkippedTabs.Add(sheet);continue;}
   n.Tabs.Add(tab);}
  foreach(var (code,counter) in spellings){string top=counter.OrderByDescending(p=>p.Value).ThenBy(p=>p.Key,StringComparer.Ordinal).First().Key;if(!n.Manufacturers.TryGetValue(code,out var mc))n.Manufacturers[code]=mc=new ManufacturerCode{Name=top};if(mc.AcadeManufacturer.Length==0)mc.AcadeManufacturer=top;}
  foreach(var t in n.Tabs)foreach(var (code,sp) in t.ManufacturerSpellings)if(n.Manufacturers.TryGetValue(code,out var mc)&&mc.AcadeManufacturer.Length==0)mc.AcadeManufacturer=sp;
  n.Manufacturers=new(n.Manufacturers.OrderBy(p=>p.Key,StringComparer.Ordinal).ToDictionary(p=>p.Key,p=>p.Value),StringComparer.OrdinalIgnoreCase);
  if(n.Tabs.Count==0)throw new InvalidOperationException("No \"LSE ...\" tab with a Part # Segment table and type codes was found in "+sourceName+".");return n;}
 static int IndexOf(string[] r,string text,int from){for(int i=from;i<r.Length;i++)if(r[i].Equals(text,StringComparison.OrdinalIgnoreCase))return i;return -1;}

 // ---- minimal xlsx reader -----------------------------------------------------------------------------------------
 // Returns, per matching sheet, one string[] per row (row 1 = index 0, missing rows empty, every row at least 16 cells, cells trimmed with line breaks collapsed).
 public static Dictionary<string,List<string[]>> ReadSheets(string path,Func<string,bool> sheetFilter){
  var result=new Dictionary<string,List<string[]>>();using var fs=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);using var zip=new ZipArchive(fs,ZipArchiveMode.Read);
  var rels=new Dictionary<string,string>();var relEntry=Entry(zip,"xl/_rels/workbook.xml.rels");if(relEntry is not null)using(var x=Reader(relEntry))while(x.Read())if(x.NodeType==XmlNodeType.Element&&x.LocalName=="Relationship"){string id=x.GetAttribute("Id")??"",target=x.GetAttribute("Target")??"";if(id.Length>0)rels[id]=target.StartsWith("/")?target.TrimStart('/'):"xl/"+target;}
  var shared=new List<string>();var ssEntry=Entry(zip,"xl/sharedStrings.xml");if(ssEntry is not null)using(var x=Reader(ssEntry)){var sb=new StringBuilder();bool inSi=false;while(x.Read()){if(x.NodeType==XmlNodeType.Element){if(x.LocalName=="si"){inSi=true;sb.Clear();}else if(x.LocalName=="rPh")x.Skip();else if(x.LocalName=="t"&&inSi)sb.Append(x.ReadElementContentAsString());}if(x.NodeType==XmlNodeType.EndElement&&x.LocalName=="si"){shared.Add(sb.ToString());inSi=false;}}}
  var wbEntry=Entry(zip,"xl/workbook.xml")??throw new InvalidOperationException("Not an Excel workbook (xl/workbook.xml missing): "+path);var sheets=new List<(string Name,string Target)>();
  using(var x=Reader(wbEntry))while(x.Read())if(x.NodeType==XmlNodeType.Element&&x.LocalName=="sheet"){string name=x.GetAttribute("name")??"",rid=x.GetAttribute("id","http://schemas.openxmlformats.org/officeDocument/2006/relationships")??"";if(rels.TryGetValue(rid,out var t))sheets.Add((name,t));}
  foreach(var (name,target) in sheets){if(!sheetFilter(name))continue;var entry=Entry(zip,target);if(entry is null)continue;result[name]=ReadSheet(entry,shared);}
  return result;}
 static ZipArchiveEntry? Entry(ZipArchive zip,string path)=>zip.GetEntry(path)??zip.Entries.FirstOrDefault(e=>e.FullName.Equals(path,StringComparison.OrdinalIgnoreCase));
 static XmlReader Reader(ZipArchiveEntry e)=>XmlReader.Create(e.Open(),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,IgnoreWhitespace=true});
 static List<string[]> ReadSheet(ZipArchiveEntry entry,List<string> shared){
  var rows=new SortedDictionary<int,Dictionary<int,string>>();using var x=Reader(entry);int row=0;
  while(x.Read()){if(x.NodeType!=XmlNodeType.Element)continue;
   if(x.LocalName=="row"){row=int.TryParse(x.GetAttribute("r"),out int r)?r:row+1;continue;}
   if(x.LocalName!="c")continue;string cellRef=x.GetAttribute("r")??"",type=x.GetAttribute("t")??"";int col=ColumnIndex(cellRef);if(x.IsEmptyElement)continue;string value="";
   using(var cell=x.ReadSubtree()){while(cell.Read()){if(cell.NodeType!=XmlNodeType.Element)continue;if(cell.LocalName=="v")value=cell.ReadElementContentAsString();else if(cell.LocalName=="is"){var sb=new StringBuilder();using var inner=cell.ReadSubtree();while(inner.Read())if(inner.NodeType==XmlNodeType.Element&&inner.LocalName=="t")sb.Append(inner.ReadElementContentAsString());value=sb.ToString();type="inlineStr";}}}
   if(type=="s"){value=int.TryParse(value,out int i)&&i>=0&&i<shared.Count?shared[i]:"";}else if(type=="b")value=value=="1"?"TRUE":"FALSE";else if(type==""||type=="n"){if(double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out double d)&&Math.Abs(d)<1e15&&Math.Abs(d-Math.Round(d))<1e-9)value=((long)Math.Round(d)).ToString(CultureInfo.InvariantCulture);}
   value=Clean(value);if(value.Length==0)continue;if(!rows.TryGetValue(row,out var cells))rows[row]=cells=new();cells[col]=value;}
  int last=rows.Count==0?0:rows.Keys.Max();var list=new List<string[]>(last);
  for(int r=1;r<=last;r++){int width=16;if(rows.TryGetValue(r,out var cells)&&cells.Count>0)width=Math.Max(width,cells.Keys.Max()+1);var arr=new string[width];Array.Fill(arr,"");if(cells is not null)foreach(var (c,v) in cells)arr[c]=v;list.Add(arr);}
  return list;}
 static int ColumnIndex(string cellRef){int n=0;foreach(char ch in cellRef){if(ch>='A'&&ch<='Z')n=n*26+(ch-'A'+1);else if(ch>='a'&&ch<='z')n=n*26+(ch-'a'+1);else break;}return Math.Max(0,n-1);}
 static string Clean(string v){v=v.Replace("_x000D_","").Replace(' ',' ');v=Regex.Replace(v,@"\s+"," ");return v.Trim();}
}
