namespace LSE.AutoCADElectrical.WireToFrom;
public sealed class AppSettings {
 public string ConfigPath{get;set;}=""; public string BackupFolderName{get;set;}="_LSE_TOFROM_BACKUP"; public int BackupsToKeep{get;set;}=10;
 public string DataSource{get;set;}="scratch"; public string LastReportFile{get;set;}=""; public string StalePolicy{get;set;}="warn"; public bool ShowReportAfterRun{get;set;}=true; public bool CurrentDrawingOnly{get;set;}
 public string ActiveProjectLisp{get;set;}=DefaultActiveProjectLisp;
 public string ScratchDbLisp{get;set;}=DefaultScratchDbLisp;
 public string RefreshDbLisp{get;set;}="";
 // Each candidate function is guarded by boundp and a string type check, so an unknown name in this release is skipped, not an error.
 public const string DefaultActiveProjectLisp="(setvar \"USERS1\" (cond ((and (boundp 'ace_getactiveproject) (= (type (setq lse_p (ace_getactiveproject))) 'STR)) lse_p) ((and (boundp 'c:ace_getactiveproject) (= (type (setq lse_p (c:ace_getactiveproject))) 'STR)) lse_p) ((and (boundp 'wd_proj_wdp_fnam) (= (type (setq lse_p (wd_proj_wdp_fnam))) 'STR)) lse_p) ((and (boundp 'c:wd_proj_wdp_fnam) (= (type (setq lse_p (c:wd_proj_wdp_fnam))) 'STR)) lse_p) ((and (boundp 'GBL_wd_prj) (= (type GBL_wd_prj) 'STR)) GBL_wd_prj) (t \"\")))";
 public const string DefaultScratchDbLisp="(setvar \"USERS1\" (cond ((and (boundp 'c:wd_mdb_get_proj_scratch_dbnam) (= (type (setq lse_x (c:wd_mdb_get_proj_scratch_dbnam))) 'STR)) lse_x) ((and (boundp 'wd_mdb_get_proj_scratch_dbnam) (= (type (setq lse_x (wd_mdb_get_proj_scratch_dbnam))) 'STR)) lse_x) (t \"\")))";
 public const string LegacyActiveProjectLisp="(setvar \"USERS1\" (if (and (boundp 'ace_getactiveproject) (ace_getactiveproject)) (ace_getactiveproject) \"\"))";
 public const string LegacyScratchDbLisp="(setvar \"USERS1\" (if (boundp 'c:wd_mdb_get_proj_scratch_dbnam) (progn (setq lse_x (c:wd_mdb_get_proj_scratch_dbnam)) (if (= (type lse_x) 'STR) lse_x \"\")) \"\"))";
 public List<string> RecentProjects{get;set;}=new();
}
public sealed class ToFromConfig {
 public string Source{get;set;}=""; public string Format{get;set;}="{FROM}/{TO}"; public string FarEndFormat{get;set;}="{TO}/{FROM}"; public string TagPinFormat{get;set;}="{TAG}:{PIN}"; public string LocationFormat{get;set;}="{LOC}+{TAG}";
 public string MissingPinText{get;set;}="?"; public string MissingTagText{get;set;}="?"; public bool IncludeLocation{get;set;} public bool Uppercase{get;set;}=true; public int MaxLength{get;set;} public bool SwapEnds{get;set;}
 public SchematicConfig Schematic{get;set;}=new(); public CsvConfig Csv{get;set;}=new(); public DatabaseConfig Database{get;set;}=new(); public ReportFileConfig ReportFile{get;set;}=new();
 public bool Both=>Schematic.LabelAt.Equals("both",StringComparison.OrdinalIgnoreCase);
}
public sealed class SchematicConfig {
 public string LabelAt{get;set;}="from"; public string LabelLayer{get;set;}="LSE_TOFROM"; public string BlockH{get;set;}="TOFROM_LBL_H"; public string BlockV{get;set;}="TOFROM_LBL_V"; public string BlockFolder{get;set;}="";
 public string TextAttribute{get;set;}="TOFROM"; public string NodeAttribute{get;set;}="WIRENO"; public string KeyAttribute{get;set;}="TFKEY"; public bool ShowNode{get;set;} public double TextHeight{get;set;}=0.08;
 public double EndOffset{get;set;}=0.5; public double SideOffset{get;set;}=0.0625; public double CollisionDistance{get;set;}=0.3; public double CollisionStep{get;set;}=0.25; public int CollisionTries{get;set;}=4;
 public bool RepositionOnRerun{get;set;} public bool DeleteStale{get;set;}=true; public string WdmBlockName{get;set;}="WD_M";
}
public sealed class CsvConfig {
 public string Delimiter{get;set;}=","; public string Encoding{get;set;}="utf8bom"; public bool ReverseAtFarEnd{get;set;}=true; public string OutputFolder{get;set;}="LSE_ToFrom";
 public string LabelFile{get;set;}="{PROJECT}_tofrom_labels.csv"; public string ReportFile{get;set;}="{PROJECT}_tofrom_report.csv"; public string ValidationFile{get;set;}="{PROJECT}_tofrom_validation.txt";
}
public sealed class DatabaseConfig {
 public string PathPattern{get;set;}=@"{FOLDER}\{PROJECT}.mdb"; public List<string> Providers{get;set;}=new(){"Microsoft.ACE.OLEDB.16.0","Microsoft.ACE.OLEDB.12.0","Microsoft.Jet.OLEDB.4.0"};
 public List<string> SearchFolders{get;set;}=new(){"{FOLDER}","{TEMP}","{LOCALAPPDATA}\\Temp"};
 public string WireSql{get;set;}=""; public string DrawingSql{get;set;}=""; public int DrawingIndexBase{get;set;}=1; public string Confidence{get;set;}="unverified"; public string Note{get;set;}="";
 public ColumnMap Columns{get;set;}=new(); public List<string> CandidateColumns{get;set;}=new(){"WIRENO","CMP1","PIN1","CMP2","PIN2","WIREHDL1","WIREHDL2","CMPHDL1","CMPHDL2","DWGIX1","DWGIX2"};
}
public sealed class ReportFileConfig { public bool HasHeader{get;set;}=true; public string Delimiter{get;set;}=""; public ColumnMap Columns{get;set;}=new(); }
public sealed class ColumnMap {
 public string WireNo{get;set;}="WIRENO"; public string Cmp1{get;set;}="CMP1"; public string Pin1{get;set;}="PIN1"; public string Cmp2{get;set;}="CMP2"; public string Pin2{get;set;}="PIN2";
 public string WireHdl1{get;set;}="WIREHDL1"; public string WireHdl2{get;set;}="WIREHDL2"; public string CmpHdl1{get;set;}="CMPHDL1"; public string CmpHdl2{get;set;}="CMPHDL2"; public string DwgIx1{get;set;}="DWGIX1"; public string DwgIx2{get;set;}="DWGIX2";
 public string Loc1{get;set;}="LOC1"; public string Inst1{get;set;}="INST1"; public string Loc2{get;set;}="LOC2"; public string Inst2{get;set;}="INST2"; public string DwgName1{get;set;}="DWGNAM1"; public string DwgName2{get;set;}="DWGNAM2";
 public IEnumerable<(string Role,string Column,bool Required)> All()=>new[]{("WireNo",WireNo,false),("Cmp1",Cmp1,true),("Pin1",Pin1,false),("Cmp2",Cmp2,true),("Pin2",Pin2,false),("WireHdl1",WireHdl1,true),("WireHdl2",WireHdl2,false),("CmpHdl1",CmpHdl1,false),("CmpHdl2",CmpHdl2,false),("DwgIx1",DwgIx1,false),("DwgIx2",DwgIx2,false),("Loc1",Loc1,false),("Inst1",Inst1,false),("Loc2",Loc2,false),("Inst2",Inst2,false),("DwgName1",DwgName1,false),("DwgName2",DwgName2,false)};
}
public sealed class WireRow {
 public int Line{get;set;} public string WireNo{get;set;}=""; public string Cmp1{get;set;}=""; public string Pin1{get;set;}=""; public string Cmp2{get;set;}=""; public string Pin2{get;set;}=""; public string Loc1{get;set;}=""; public string Inst1{get;set;}=""; public string Loc2{get;set;}=""; public string Inst2{get;set;}="";
 public string WireHdl1{get;set;}=""; public string WireHdl2{get;set;}=""; public string CmpHdl1{get;set;}=""; public string CmpHdl2{get;set;}=""; public string DwgIx1{get;set;}=""; public string DwgIx2{get;set;}=""; public string DwgName1{get;set;}=""; public string DwgName2{get;set;}="";
 public string Drawing1{get;set;}=""; public string Drawing2{get;set;}=""; public string Sheet1{get;set;}=""; public string Sheet2{get;set;}="";
 public string Describe=>$"{WireNo} {Cmp1}:{Pin1} -> {Cmp2}:{Pin2}";
 public WireRow Swapped()=>new(){Line=Line,WireNo=WireNo,Cmp1=Cmp2,Pin1=Pin2,Cmp2=Cmp1,Pin2=Pin1,Loc1=Loc2,Inst1=Inst2,Loc2=Loc1,Inst2=Inst1,WireHdl1=WireHdl2,WireHdl2=WireHdl1,CmpHdl1=CmpHdl2,CmpHdl2=CmpHdl1,DwgIx1=DwgIx2,DwgIx2=DwgIx1,DwgName1=DwgName2,DwgName2=DwgName1,Drawing1=Drawing2,Drawing2=Drawing1,Sheet1=Sheet2,Sheet2=Sheet1};
}
public sealed class LabelEnd {
 public WireRow Row{get;set;}=new(); public int End{get;set;}=1; public string Drawing{get;set;}=""; public string Sheet{get;set;}=""; public string WireHandle{get;set;}=""; public string ComponentHandle{get;set;}="";
 public string Text{get;set;}=""; public string Key{get;set;}=""; public string Node{get;set;}=""; public string Tag{get;set;}=""; public string Pin{get;set;}=""; public string OtherTag{get;set;}=""; public string OtherPin{get;set;}="";
}
public sealed class ExistingLabel { public string Handle{get;set;}=""; public string Key{get;set;}=""; public string Text{get;set;}=""; public string Node{get;set;}=""; public string Block{get;set;}=""; public string Layer{get;set;}=""; public Pt Point{get;set;} }
public sealed class DrawingLabels {
 public string Path{get;set;}=""; public bool Exists{get;set;} public bool OpenInEditor{get;set;} public bool Locked{get;set;} public string Error{get;set;}=""; public string Sheet{get;set;}="";
 public List<string> WireLayers{get;set;}=new(); public string WireNoLayer{get;set;}=""; public List<ExistingLabel> Labels{get;set;}=new(); public bool HasBlockH{get;set;} public bool HasBlockV{get;set;}
 public bool Readable=>Exists&&!Locked&&Error.Length==0;
}
public enum LabelAction{Insert,Update,Keep,Delete,Skip}
public sealed class LabelChange {
 public string Drawing{get;set;}=""; public string Key{get;set;}=""; public LabelAction Action{get;set;} public string Text{get;set;}=""; public string Node{get;set;}=""; public string Existing{get;set;}=""; public string ExistingHandle{get;set;}="";
 public string WireHandle{get;set;}=""; public string ComponentHandle{get;set;}=""; public string Note{get;set;}=""; public bool Selected{get;set;}=true; public LabelEnd? End{get;set;}
 public bool Writes=>Action is LabelAction.Insert or LabelAction.Update or LabelAction.Delete;
 public string ActionText=>Action switch{LabelAction.Insert=>"Insert label",LabelAction.Update=>"Update label",LabelAction.Delete=>"Delete stale label",LabelAction.Keep=>"Keep",_=>"Skip"};
}
public sealed class DrawingPlan {
 public string Path{get;set;}=""; public DrawingLabels State{get;set;}=new(); public List<LabelChange> Changes{get;set;}=new(); public string SkipReason{get;set;}="";
 public string Name=>System.IO.Path.GetFileName(Path); public int Count(LabelAction a)=>Changes.Count(c=>c.Action==a); public bool HasWork=>SkipReason.Length==0&&Changes.Any(c=>c.Writes&&c.Selected);
 public string Summary=>SkipReason.Length>0?"skipped: "+SkipReason:$"{Count(LabelAction.Insert)} insert, {Count(LabelAction.Update)} update, {Count(LabelAction.Keep)} keep, {Count(LabelAction.Delete)} delete, {Count(LabelAction.Skip)} skip";
}
public enum IssueKind{MissingPin,MissingTag,WireNotFound,DrawingSkipped,DrawingUnresolved,StaleLabel,DuplicateKey,StaleDatabase,LayerConflict,DataSource}
public sealed class Issue { public IssueKind Kind{get;set;} public string Drawing{get;set;}=""; public string WireNo{get;set;}=""; public string Detail{get;set;}=""; public string Text=>$"{Kind,-18} {(Drawing.Length>0?System.IO.Path.GetFileName(Drawing)+"  ":"")}{(WireNo.Length>0?"wire "+WireNo+"  ":"")}{Detail}"; }
public sealed class ProjectInfo {
 public string WdpPath{get;set;}=""; public string Folder=>System.IO.Path.GetDirectoryName(WdpPath)??""; public string Name=>System.IO.Path.GetFileNameWithoutExtension(WdpPath); public List<ProjectDrawing> Drawings{get;set;}=new(); public string DatabasePath{get;set;}=""; public DateTime? DatabaseWritten{get;set;}
 public ProjectDrawing? ByIndex(int oneBased)=>oneBased>=1&&oneBased<=Drawings.Count?Drawings[oneBased-1]:null;
 public ProjectDrawing? ByName(string name){if(name.Trim().Length==0)return null;string n=System.IO.Path.GetFileName(name.Trim());return Drawings.FirstOrDefault(d=>d.Path.Equals(name.Trim(),StringComparison.OrdinalIgnoreCase))??Drawings.FirstOrDefault(d=>System.IO.Path.GetFileName(d.Path).Equals(n,StringComparison.OrdinalIgnoreCase))??Drawings.FirstOrDefault(d=>System.IO.Path.GetFileNameWithoutExtension(d.Path).Equals(System.IO.Path.GetFileNameWithoutExtension(n),StringComparison.OrdinalIgnoreCase));}
}
public sealed class ProjectDrawing { public int Index{get;set;} public string Path{get;set;}=""; public string Section{get;set;}=""; public string Description{get;set;}=""; public bool ReferenceOnly{get;set;} }
public sealed class RunPlan {
 public ProjectInfo Project{get;set;}=new(); public ToFromConfig Config{get;set;}=new(); public string DataSource{get;set;}=""; public List<WireRow> Rows{get;set;}=new(); public List<LabelEnd> SchematicEnds{get;set;}=new(); public List<LabelEnd> CsvEnds{get;set;}=new();
 public List<DrawingPlan> Drawings{get;set;}=new(); public List<Issue> Issues{get;set;}=new(); public List<string> Log{get;set;}=new(); public DateTime Planned{get;set;}=DateTime.Now; public bool AnnotationRequested{get;set;}=true;
 public IEnumerable<LabelChange> AllChanges=>Drawings.SelectMany(d=>d.Changes); public int Selected=>Drawings.Where(d=>d.SkipReason.Length==0).Sum(d=>d.Changes.Count(c=>c.Selected&&c.Writes)); public int Writable=>Drawings.Where(d=>d.SkipReason.Length==0).Sum(d=>d.Changes.Count(c=>c.Writes));
 public int Count(IssueKind k)=>Issues.Count(i=>i.Kind==k);
}
public sealed class DrawingWriteRequest { public string DrawingPath{get;set;}=""; public List<LabelChange> Changes{get;set;}=new(); public SchematicConfig Schematic{get;set;}=new(); public List<string> LibraryPaths{get;set;}=new(); }
public sealed class HostBridge { public Func<string,DrawingLabels>? ReadLabels{get;set;} public Func<DrawingWriteRequest,List<string>>? WriteLabels{get;set;} public bool CanRead=>ReadLabels is not null; public bool CanWrite=>WriteLabels is not null; }
public sealed class RunResult { public RunPlan Plan{get;set;}=new(); public string BackupFolder{get;set;}=""; public List<string> Log{get;set;}=new(); public int Applied{get;set;} public List<LabelChange> Unresolved{get;set;}=new(); public string ReportPath{get;set;}=""; public string LabelCsv{get;set;}=""; public string ReportCsv{get;set;}=""; public string ValidationPath{get;set;}=""; public string Report=>string.Join(Environment.NewLine,Log); }
public sealed class SchemaTable { public string Name{get;set;}=""; public List<string> Columns{get;set;}=new(); public int Score{get;set;} }
public readonly record struct Pt(double X,double Y){ public double Dist(Pt o)=>Math.Sqrt((X-o.X)*(X-o.X)+(Y-o.Y)*(Y-o.Y)); }
