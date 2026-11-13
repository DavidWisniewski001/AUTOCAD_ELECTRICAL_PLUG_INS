using System.Text;using System.Text.Json.Serialization;
namespace LSE.AutoCADElectrical.ProjectEnvironment;
public sealed class AppSettings {
 public string RulesConfigPath{get;set;}=""; public string ProfilesFolder{get;set;}="";
 public string TemplateProjectPath{get;set;}=@"C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\Project Templates\CUSTOM TEMPLATES\CUSTOM PROJECT TEMPLATE 2.0\E-CAD CUSTOM PROJECT TEMPLATE 2.wdp";
 public string BackupFolderName{get;set;}="_LSE_ENV_BACKUP"; public int BackupsToKeep{get;set;}=10;
 public bool ApplyToDrawings{get;set;}=true; public bool ComputeFromTo{get;set;}=true; public bool RetagAfterApply{get;set;} public bool ReactivateAfterApply{get;set;}=true; public bool ShowReportAfterApply{get;set;}=true;
 public string ActiveProjectLisp{get;set;}="(setvar \"USERS1\" (if (and (boundp 'ace_getactiveproject) (ace_getactiveproject)) (ace_getactiveproject) \"\"))";
 public string ActivateProjectLisp{get;set;}="(if (boundp 'c:wd_makeproj_current) (progn (c:wd_makeproj_current \"{WDP}\") (princ \"\\nLSE_Env: reloaded project {WDP}\")) (princ \"\\nLSE_Env: c:wd_makeproj_current is not loaded. Re-open the project from the Project Manager to pick up the new settings: {WDP}\"))";
 public string RetagLisp{get;set;}="(if (boundp 'c:wd_wnum) (c:wd_wnum) (princ \"\\nLSE_Env: c:wd_wnum is not loaded. Run Insert Wire Numbers (project-wide) from the Schematic tab by hand.\"))";
 public string LastProfile{get;set;}=""; public string LastScheme{get;set;}=""; public List<string> RecentProjects{get;set;}=new();
}
public sealed class EnvRules {
 public string Source{get;set;}=""; public string WdmBlockName{get;set;}="WD_M";
 public List<SettingMap> Settings{get;set;}=new(); public List<WireScheme> WireSchemes{get;set;}=new(); public FromToRules FromTo{get;set;}=new();
 public List<int> ProtectedOptions{get;set;}=new(){1,2,3,4}; public List<int> ProtectedProperties{get;set;}=new(); public Dictionary<string,string> Labels{get;set;}=new(StringComparer.OrdinalIgnoreCase);
 public SettingMap? Map(string key)=>Settings.FirstOrDefault(m=>m.Key.Equals(key,StringComparison.OrdinalIgnoreCase));
 public WireScheme? Scheme(string name)=>WireSchemes.FirstOrDefault(s=>s.Name.Equals(name,StringComparison.OrdinalIgnoreCase));
 public SettingMap? MapFor(char prefix,int index)=>Settings.FirstOrDefault(m=>prefix=='?'?m.Property==index:m.Option==index);
 public SettingMap? MapForWdm(string tag)=>Settings.FirstOrDefault(m=>m.Wdm.Equals(tag,StringComparison.OrdinalIgnoreCase));
 public string Label(char prefix,int index){string key=prefix+"["+index+"]";var m=MapFor(prefix,index);if(m is not null&&m.Label.Length>0)return key+" "+m.Label;return Labels.TryGetValue(prefix.ToString()+index,out var l)&&l.Trim().Length>0?key+" "+l.Trim():key;}
 public bool IsProtected(char prefix,int index)=>prefix=='+'?ProtectedOptions.Contains(index):ProtectedProperties.Contains(index);
}
public sealed class SettingMap {
 public string Key{get;set;}=""; public string Label{get;set;}=""; public string Group{get;set;}=""; public int Property{get;set;}=-1; public int Option{get;set;}=-1; public string Wdm{get;set;}="";
 public string Kind{get;set;}="text"; public string Choices{get;set;}=""; public string Confidence{get;set;}="inferred"; public string Note{get;set;}=""; public bool Protected{get;set;}
 public bool Mapped=>Property>=0||Option>=0||Wdm.Length>0;
 public string Where=>Mapped?string.Join(" + ",new[]{Property>=0?"?["+Property+"]":"",Option>=0?"+["+Option+"]":"",Wdm.Length>0?"WD_M "+Wdm:""}.Where(x=>x.Length>0)):"(not mapped yet)";
 public List<KeyValuePair<string,string>> ChoiceList()=>Choices.Split(';',StringSplitOptions.RemoveEmptyEntries).Select(c=>{int i=c.IndexOf('=');return i<0?new KeyValuePair<string,string>(c.Trim(),c.Trim()):new KeyValuePair<string,string>(c[..i].Trim(),c[(i+1)..].Trim());}).ToList();
 public string Describe(string value){foreach(var (k,v) in ChoiceList())if(k.Equals(value.Trim(),StringComparison.OrdinalIgnoreCase))return value+" ("+v+")";return value;}
}
public sealed class WireScheme {
 public string Name{get;set;}=""; public string Description{get;set;}=""; public bool FromTemplate{get;set;} public List<string> TemplateKeys{get;set;}=new();
 public Dictionary<string,string> Values{get;set;}=new(StringComparer.OrdinalIgnoreCase); public FromToSettings? FromTo{get;set;}
 public bool IsFromTo=>FromTo is not null;
}
public sealed class FromToSettings {
 public string Format{get;set;}="{FROM}-{TO}"; public string DuplicateSuffix{get;set;}="-{N}"; public bool IncludeTerminalPin{get;set;} public string PinFormat{get;set;}="{TAG}:{PIN}";
 public string MultiDevice{get;set;}="firstLast"; public string AllSeparator{get;set;}="/"; public List<string> DevicePriority{get;set;}=new(); public bool TerminalsLast{get;set;}=true;
 public bool Uppercase{get;set;}=true; public int MaxLength{get;set;} public bool Fixed{get;set;}=true; public string SingleDevice{get;set;}="keep"; public string NoDevice{get;set;}="keep"; public bool OverwriteFixed{get;set;}=true;
 public FromToSettings Clone()=>(FromToSettings)MemberwiseClone();
}
public sealed class FromToRules {
 public List<string> WireNumberBlocks{get;set;}=new(){"WD_WN*"}; public string WireNumberAttribute{get;set;}="WIRENO"; public string InsertBlockName{get;set;}="WD_WN"; public bool InsertMissing{get;set;}=true; public double InsertOffset{get;set;}=0.09375;
 public string ConnectionAttributePattern{get;set;}=@"^X[1248]TERM(\d{2})$"; public string PinAttributePrefix{get;set;}="TERM"; public List<string> TagAttributes{get;set;}=new(){"TAG1","TAG2","TAG","TAGSTRIP"}; public string TerminalStripAttribute{get;set;}="TAGSTRIP"; public string SignalCodeAttribute{get;set;}="SIGCODE";
 public List<string> IgnoreBlocks{get;set;}=new(){"WD_M","WD_WN*","WD_*"}; public double JoinTolerance{get;set;}=0.001; public double ConnectTolerance{get;set;}=0.02; public double GapTolerance{get;set;}=0.25; public double MarkTolerance{get;set;}=0.35;
}
public sealed class HostBridge { public Func<string,DrawingState>? Inspect{get;set;} public Func<string,DrawingWires>? ReadWires{get;set;} public Func<DrawingApplyRequest,List<string>>? ApplyDrawing{get;set;} public bool CanInspect=>Inspect is not null; public bool CanReadWires=>ReadWires is not null; }
public sealed class EnvironmentProfile {
 public string Name{get;set;}=""; public string Customer{get;set;}=""; public string Description{get;set;}=""; public string Scheme{get;set;}="";
 public Dictionary<string,string> Values{get;set;}=new(StringComparer.OrdinalIgnoreCase); public Dictionary<string,string> Properties{get;set;}=new(); public Dictionary<string,string> Options{get;set;}=new(); public Dictionary<string,string> Drawing{get;set;}=new(StringComparer.OrdinalIgnoreCase);
 public FromToSettings? FromTo{get;set;} public string WdtSource{get;set;}=""; public string WdlSource{get;set;}=""; public string Notes{get;set;}=""; public string CapturedFrom{get;set;}=""; public DateTime? Captured{get;set;}
 [JsonIgnore] public string FilePath{get;set;}="";
 [JsonIgnore] public string Display=>Customer.Trim().Length>0?Name+"  ("+Customer.Trim()+")":Name;
 public EnvironmentProfile Clone(){var c=(EnvironmentProfile)MemberwiseClone();c.Values=new(Values,StringComparer.OrdinalIgnoreCase);c.Properties=new(Properties);c.Options=new(Options);c.Drawing=new(Drawing,StringComparer.OrdinalIgnoreCase);c.FromTo=FromTo?.Clone();return c;}
}
public sealed class TextDocument { public string Path{get;set;}=""; public bool Exists{get;set;} public Encoding Encoding{get;set;}=new UTF8Encoding(true); public string NewLine{get;set;}="\r\n"; public List<string> Lines{get;set;}=new(); public bool Changed{get;set;} }
public sealed class WdpDrawingEntry { public string Path{get;set;}=""; public int FirstLine{get;set;} public int PathLine{get;set;} public string Section{get;set;}=""; public string Subsection{get;set;}=""; public string Folder{get;set;}=""; public List<string> Descriptions{get;set;}=new(); public bool ReferenceOnly{get;set;} }
public sealed class DrawingState {
 public string Path{get;set;}=""; public bool Exists{get;set;} public bool OpenInEditor{get;set;} public bool ReadFromEditor{get;set;} public bool HasWdm{get;set;} public string Error{get;set;}="";
 public Dictionary<string,string> WdmValues{get;set;}=new(StringComparer.OrdinalIgnoreCase); public List<string> WdmTags{get;set;}=new();
 public string Wdm(string tag)=>WdmValues.TryGetValue(tag,out var v)?v:""; public bool Has(string tag)=>WdmValues.ContainsKey(tag); public bool Readable=>Exists&&Error.Length==0;
 public string Name=>System.IO.Path.GetFileName(Path);
}
public sealed class ProjectState {
 public string WdpPath{get;set;}=""; public string Folder=>System.IO.Path.GetDirectoryName(WdpPath)??""; public string ProjectName=>System.IO.Path.GetFileNameWithoutExtension(WdpPath);
 public TextDocument Wdp{get;set;}=new(); public SortedDictionary<int,string> Properties{get;set;}=new(); public SortedDictionary<int,string> Options{get;set;}=new(); public List<WdpDrawingEntry> Entries{get;set;}=new(); public List<DrawingState> Drawings{get;set;}=new();
 public bool DrawingsInspected{get;set;} public EnvMarker? Marker{get;set;} public List<string> Log{get;set;}=new();
 public string? Property(int i)=>Properties.TryGetValue(i,out var v)?v:null; public string? Option(int i)=>Options.TryGetValue(i,out var v)?v:null;
}
public enum ChangeKind{Info,SetProperty,SetOption,SetWdm,CopyFile,WireNumbers,UnfixWireNumbers,Marker}
public sealed class Change {
 public ChangeKind Kind{get;set;} public string Scope{get;set;}=""; public string Item{get;set;}=""; public string Current{get;set;}=""; public string New{get;set;}=""; public string Note{get;set;}="";
 public bool Selected{get;set;}=true; public bool Recommended{get;set;}=true; public string Target{get;set;}=""; public char Prefix{get;set;} public int Index{get;set;} public string Name{get;set;}=""; public string Value{get;set;}=""; public string Source{get;set;}="";
 public WirePlan? Wires{get;set;} public List<string> Handles{get;set;}=new();
 public bool IsDrawing=>Kind is ChangeKind.SetWdm or ChangeKind.WireNumbers or ChangeKind.UnfixWireNumbers; public bool Applies=>Kind!=ChangeKind.Info;
 public string KindText=>Kind switch{ChangeKind.SetProperty=>"Set project property",ChangeKind.SetOption=>"Set project option",ChangeKind.SetWdm=>"Set WD_M value",ChangeKind.CopyFile=>"Copy file",ChangeKind.WireNumbers=>"From/To wire numbers",ChangeKind.UnfixWireNumbers=>"Un-fix wire numbers",ChangeKind.Marker=>"Record profile",_=>"(note)"};
 public string Key=>Kind+"|"+Target+"|"+Item;
}
public sealed class EnvPlan {
 public ProjectState Project{get;set;}=new(); public EnvironmentProfile? Profile{get;set;} public WireScheme? Scheme{get;set;} public EnvRules Rules{get;set;}=new(); public Dictionary<string,string> Resolved{get;set;}=new(StringComparer.OrdinalIgnoreCase);
 public FromToSettings? FromTo{get;set;} public List<Change> Changes{get;set;}=new(); public List<string> Log{get;set;}=new(); public DateTime Planned{get;set;}=DateTime.Now;
 public int Selected=>Changes.Count(c=>c.Selected&&c.Applies); public int Applicable=>Changes.Count(c=>c.Applies); public string ProfileName=>Profile?.Name??"(none)"; public string SchemeName=>Scheme?.Name??"(profile values only)";
}
public sealed class EnvMarker {
 public string Profile{get;set;}=""; public string Customer{get;set;}=""; public string Scheme{get;set;}=""; public DateTime Applied{get;set;} public string By{get;set;}=""; public bool FromTo{get;set;}
 public List<int> Properties{get;set;}=new(); public List<int> Options{get;set;}=new(); public List<string> DrawingAttributes{get;set;}=new(); public Dictionary<string,string> Values{get;set;}=new(StringComparer.OrdinalIgnoreCase);
}
public sealed class ApplyResult { public EnvPlan Plan{get;set;}=new(); public string BackupFolder{get;set;}=""; public List<string> Log{get;set;}=new(); public int Applied{get;set;} public List<Change> Unresolved{get;set;}=new(); public string ReportPath{get;set;}=""; public string Report=>string.Join(Environment.NewLine,Log); }
public sealed class Snapshot { public string WdpPath{get;set;}=""; public DateTime Taken{get;set;}=DateTime.Now; public Dictionary<string,string> Wdp{get;set;}=new(); public Dictionary<string,Dictionary<string,string>> Drawings{get;set;}=new(StringComparer.OrdinalIgnoreCase); public List<string> Notes{get;set;}=new(); }
public readonly record struct Pt(double X,double Y){ public double Dist(Pt o)=>Math.Sqrt((X-o.X)*(X-o.X)+(Y-o.Y)*(Y-o.Y)); public static Pt Mid(Pt a,Pt b)=>new((a.X+b.X)/2,(a.Y+b.Y)/2); }
public sealed class WireSegment { public Pt A{get;set;} public Pt B{get;set;} public string Layer{get;set;}=""; public string Handle{get;set;}=""; public double Length=>A.Dist(B); public bool Horizontal=>Math.Abs(A.Y-B.Y)<=Math.Abs(A.X-B.X); }
public sealed class DeviceTerminal { public string Tag{get;set;}=""; public string Pin{get;set;}=""; public Pt Point{get;set;} public string Block{get;set;}=""; public string Handle{get;set;}=""; public bool IsTerminalStrip{get;set;} public string Display(FromToSettings f)=>f.IncludeTerminalPin&&Pin.Length>0?f.PinFormat.Replace("{TAG}",Tag).Replace("{PIN}",Pin):Tag; }
public sealed class WireNumberMark { public string Handle{get;set;}=""; public Pt Point{get;set;} public string Number{get;set;}=""; public string Layer{get;set;}=""; public string Block{get;set;}=""; public bool IsFixed{get;set;} }
public sealed class SignalArrow { public string Code{get;set;}=""; public Pt Point{get;set;} public string Handle{get;set;}=""; public string Block{get;set;}=""; }
public sealed class DrawingWires {
 public string Path{get;set;}=""; public string Sheet{get;set;}=""; public string WireNoLayer{get;set;}="WIRENO"; public string FixedLayer{get;set;}="WIREFIXED"; public List<string> WireLayerPatterns{get;set;}=new();
 public List<WireSegment> Segments{get;set;}=new(); public List<DeviceTerminal> Terminals{get;set;}=new(); public List<WireNumberMark> Marks{get;set;}=new(); public List<SignalArrow> Signals{get;set;}=new(); public List<string> Notes{get;set;}=new(); public int UntaggedConnections{get;set;}
}
public enum WireAction{Keep,Set,Insert,Skip}
public sealed class WireNetwork {
 public int Id{get;set;} public string Drawing{get;set;}=""; public List<WireSegment> Segments{get;set;}=new(); public List<DeviceTerminal> Devices{get;set;}=new(); public List<WireNumberMark> Marks{get;set;}=new(); public List<string> SignalCodes{get;set;}=new();
 public int Logical{get;set;}=-1; public string Proposed{get;set;}=""; public string Existing=>Marks.Count==0?"":string.Join(" | ",Marks.Select(m=>m.Number).Distinct());
 public WireAction Action{get;set;} public string Note{get;set;}=""; public Pt LabelPoint{get;set;} public double LabelRotation{get;set;}
 public string DeviceText=>string.Join(", ",Devices.Select(d=>d.Pin.Length>0?d.Tag+":"+d.Pin:d.Tag));
}
public sealed class WirePlan {
 public string Drawing{get;set;}=""; public List<WireNetwork> Networks{get;set;}=new(); public List<string> Notes{get;set;}=new(); public string Error{get;set;}=""; public string FixedLayer{get;set;}="WIREFIXED"; public string WireNoLayer{get;set;}="WIRENO"; public bool Fixed{get;set;}=true; public string Sheet{get;set;}="";
 public int Count(WireAction a)=>Networks.Count(n=>n.Action==a); public bool HasWork=>Networks.Any(n=>n.Action is WireAction.Set or WireAction.Insert);
 public string Summary=>Error.Length>0?"not planned: "+Error:$"{Networks.Count} network(s): {Count(WireAction.Set)} renumber, {Count(WireAction.Insert)} insert, {Count(WireAction.Keep)} keep, {Count(WireAction.Skip)} skipped";
}
public sealed class DrawingApplyRequest { public string DrawingPath{get;set;}=""; public string WdmBlockName{get;set;}="WD_M"; public Dictionary<string,string> Attributes{get;set;}=new(StringComparer.OrdinalIgnoreCase); public WirePlan? Wires{get;set;} public List<string> LibraryPaths{get;set;}=new(); public List<string> UnfixHandles{get;set;}=new(); public string WireNoLayer{get;set;}="WIRENO"; }
