using System.Text;
namespace LSE.AutoCADElectrical.WireToFrom;
// CSV export (one row per wire end for the label printer; one row per wire mirroring the native report) and the validation report.
public static class Export {
 public static readonly string[] LabelColumns={"Drawing","Sheet","WireNo","End","Label","Tag","Pin","OtherTag","OtherPin","WireHandle","ComponentHandle","Key"};
 public static readonly string[] ReportColumns={"WIRENO","CMP1","PIN1","CMP2","PIN2","WIREHDL1","WIREHDL2","CMPHDL1","CMPHDL2","DWGIX1","DWGIX2"};
 public static Encoding EncodingFor(CsvConfig c)=>c.Encoding.Equals("ansi",StringComparison.OrdinalIgnoreCase)?Encoding.Latin1:c.Encoding.Equals("utf8",StringComparison.OrdinalIgnoreCase)?new UTF8Encoding(false):new UTF8Encoding(true);
 public static string Field(string v,string delim){if(v.Length==0)return "";bool q=v.Contains(delim,StringComparison.Ordinal)||v.Contains('"')||v.Contains('\n')||v.Contains('\r')||v.StartsWith(' ')||v.EndsWith(' ');return q?"\""+v.Replace("\"","\"\"")+"\"":v;}
 public static string Line(IEnumerable<string> fields,string delim)=>string.Join(delim,fields.Select(f=>Field(f,delim)));
 public static List<string> LabelLines(RunPlan plan){string d=plan.Config.Csv.Delimiter;var x=new List<string>{Line(LabelColumns,d)};
  foreach(var e in plan.CsvEnds.OrderBy(e=>Path.GetFileName(e.Drawing),StringComparer.OrdinalIgnoreCase).ThenBy(e=>e.Row.Line).ThenBy(e=>e.End))x.Add(Line(new[]{Path.GetFileName(e.Drawing),e.Sheet,e.Node,e.End.ToString(),e.Text,e.Tag,e.Pin,e.OtherTag,e.OtherPin,e.WireHandle,e.ComponentHandle,e.Key},d));return x;}
 public static List<string> ReportLines(RunPlan plan){string d=plan.Config.Csv.Delimiter;var x=new List<string>{Line(ReportColumns,d)};foreach(var r in plan.Rows)x.Add(Line(new[]{r.WireNo,r.Cmp1,r.Pin1,r.Cmp2,r.Pin2,r.WireHdl1,r.WireHdl2,r.CmpHdl1,r.CmpHdl2,r.DwgIx1,r.DwgIx2},d));return x;}
 public static string WriteLabelCsv(RunPlan plan,string path){Write(path,LabelLines(plan),plan.Config.Csv);return path;}
 public static string WriteReportCsv(RunPlan plan,string path){Write(path,ReportLines(plan),plan.Config.Csv);return path;}
 static void Write(string path,List<string> lines,CsvConfig c){Directory.CreateDirectory(Path.GetDirectoryName(path)??"");string t=path+".tmp";File.WriteAllLines(t,lines,EncodingFor(c));File.Move(t,path,true);}
 public static string Validation(RunPlan plan,RunResult? result){var x=new List<string>{"LSE_ToFrom validation report  "+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),"Project: "+plan.Project.WdpPath,"Data source: "+plan.DataSource,"Config: "+plan.Config.Source,""};
  x.Add($"Rows: {plan.Rows.Count}   wire ends: {plan.CsvEnds.Count}   schematic labels wanted: {plan.SchematicEnds.Count}   drawings planned: {plan.Drawings.Count} ({plan.Drawings.Count(d=>d.SkipReason.Length>0)} skipped)");
  if(plan.AnnotationRequested&&plan.Drawings.Count>0)x.Add($"Labels: {plan.AllChanges.Count(c=>c.Action==LabelAction.Insert)} insert, {plan.AllChanges.Count(c=>c.Action==LabelAction.Update)} update, {plan.AllChanges.Count(c=>c.Action==LabelAction.Keep)} keep, {plan.AllChanges.Count(c=>c.Action==LabelAction.Delete)} delete, {plan.AllChanges.Count(c=>c.Action==LabelAction.Skip)} skip");
  if(result is not null){x.Add($"Run: {result.Applied} label(s) written, {result.Unresolved.Count} not verified"+(result.BackupFolder.Length>0?", backup "+result.BackupFolder:""));}
  x.Add("");foreach(IssueKind k in Enum.GetValues<IssueKind>()){var list=plan.Issues.Where(i=>i.Kind==k).ToList();if(list.Count==0)continue;x.Add($"{k} ({list.Count}): {Explain(k)}");foreach(var i in list)x.Add("  "+(i.Drawing.Length>0?Path.GetFileName(i.Drawing)+"  ":"")+(i.WireNo.Length>0?"wire "+i.WireNo+"  ":"")+i.Detail);x.Add("");}
  if(plan.Issues.Count==0)x.Add("No issues.");
  if(result is not null&&result.Unresolved.Count>0){x.Add("Not verified after the write:");foreach(var c in result.Unresolved)x.Add("  "+Path.GetFileName(c.Drawing)+"  "+c.ActionText+"  "+c.Key+"  \""+c.Text+"\"");}
  return string.Join(Environment.NewLine,x);}
 public static string WriteValidation(RunPlan plan,RunResult? result,string path){Directory.CreateDirectory(Path.GetDirectoryName(path)??"");File.WriteAllText(path,Validation(plan,result),new UTF8Encoding(true));return path;}
 static string Explain(IssueKind k)=>k switch{IssueKind.MissingPin=>"a wire connection without a terminal number; fix the symbol's pin attribute or the catalog pin list",IssueKind.MissingTag=>"a component without a tag; tag it and refresh the project database",
  IssueKind.WireNotFound=>"the data names no wire handle or drawing for this end, or the handle is not in the drawing; no label was placed",IssueKind.DrawingSkipped=>"drawing not written (missing, locked, or unreadable); run again when it is available",IssueKind.DrawingUnresolved=>"a drawing index or name in the data is not in the project; check drawingIndexBase/drawingSql",
  IssueKind.StaleLabel=>"a label in the drawing whose wire is not in the data (wire removed or renumbered)",IssueKind.DuplicateKey=>"two rows name the same wire end; both are labelled, check the wire sequence",IssueKind.StaleDatabase=>"a drawing was saved after the project database was last refreshed; the data may be out of date",
  IssueKind.LayerConflict=>"the label layer would be treated as a wire or wire-number layer by AutoCAD Electrical; the drawing was skipped",_=>"the data source reported a problem"};
}
