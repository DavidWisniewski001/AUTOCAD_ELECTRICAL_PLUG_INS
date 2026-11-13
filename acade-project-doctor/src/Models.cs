using System.Text;
namespace LSE.AutoCADElectrical.ProjectDoctor;
public sealed class AppSettings {
 public string TemplateProjectPath{get;set;}=@"C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\Project Templates\CUSTOM TEMPLATES\CUSTOM PROJECT TEMPLATE 2.0\E-CAD CUSTOM PROJECT TEMPLATE 2.wdp";
 public string RulesConfigPath{get;set;}=""; public string ProjectsRootFolder{get;set;}=@"C:\_Lake_Shore_E_Vault\Engineering\Custom Sales Order";
 public bool CheckDrawings{get;set;}=true; public bool InsertMissingWdm{get;set;}=true; public bool ScanChecksDrawings{get;set;} public bool ReactivateAfterRepair{get;set;}=true; public bool ShowReportAfterRepair{get;set;}=true;
 public string BackupFolderName{get;set;}="_LSE_DOCTOR_BACKUP"; public int BackupsToKeep{get;set;}=10;
 public string ActiveProjectLisp{get;set;}="(setvar \"USERS1\" (if (and (boundp 'ace_getactiveproject) (ace_getactiveproject)) (ace_getactiveproject) \"\"))";
 public string ActivateProjectLisp{get;set;}="(if (boundp 'c:wd_makeproj_current) (progn (c:wd_makeproj_current \"{WDP}\") (princ \"\\nLSE_Doctor: reloaded project {WDP}\")) (princ \"\\nLSE_Doctor: c:wd_makeproj_current is not loaded. Re-open the project from the Project Manager to pick up the repaired settings: {WDP}\"))";
 public List<string> RecentProjects{get;set;}=new();
}
public sealed class DoctorRules {
 public string Source{get;set;}=""; public int DescriptionLineCount{get;set;} public bool FillBlankDescriptionsWithPlaceholder{get;set;}=true;
 public bool EnforceOptions{get;set;}=true; public bool EnforceProperties{get;set;}=true; public string PathListMode{get;set;}="replace";
 public List<int> PathListOptions{get;set;}=new(){1,3}; public List<int> PathOptions{get;set;}=new(){2,4}; public List<int> ProtectedOptions{get;set;}=new(); public List<int> ProtectedProperties{get;set;}=new();
 public bool RequireLabelFile{get;set;}=true; public bool EnforceLabels{get;set;}=true; public bool RequireMappingFile{get;set;}=true; public bool EnforceMappings{get;set;}=true; public bool RequireProjectConfigurationFile{get;set;}
 public string WdmBlockName{get;set;}="WD_M"; public List<string> TitleBlockNames{get;set;}=new(); public Dictionary<string,string> Labels{get;set;}=new(StringComparer.OrdinalIgnoreCase);
 public bool CheckWireLayers{get;set;}=true; public bool EnforceWireLayers{get;set;}=true; public bool PurgeUnusedWireLayers{get;set;}=true;
 public List<string> WireLayerAttributes{get;set;}=new(){"WIRELAYS","WIRENO_LAY","WIRECOPY_LAY","WIREFIXED_LAY","WIREREF_LAY","FAN_INOUT_LAYS"}; public List<string> WireLayerListAttributes{get;set;}=new(){"WIRELAYS","FAN_INOUT_LAYS"};
 public List<string> WireLayerXdataApps{get;set;}=new(){"VIA_WD*"}; public List<string> NeverPurgeLayers{get;set;}=new(){"0","DEFPOINTS"};
 public bool Merge=>PathListMode.Equals("merge",StringComparison.OrdinalIgnoreCase);
}
public sealed class TextDocument { public string Path{get;set;}=""; public bool Exists{get;set;} public Encoding Encoding{get;set;}=new UTF8Encoding(true); public string NewLine{get;set;}="\r\n"; public List<string> Lines{get;set;}=new(); public bool Changed{get;set;} }
public sealed class WdpDrawingEntry { public string Path{get;set;}=""; public int FirstLine{get;set;} public int PathLine{get;set;} public string Section{get;set;}=""; public string Subsection{get;set;}=""; public string Folder{get;set;}=""; public List<string> Descriptions{get;set;}=new(); public bool ReferenceOnly{get;set;} }
public sealed class WdtMapping { public string Attribute{get;set;}=""; public string Code{get;set;}=""; public int Line{get;set;}=-1; public bool IsBlock=>Attribute.Equals("BLOCK",StringComparison.OrdinalIgnoreCase); }
public sealed class ProjectStandard {
 public string WdpPath{get;set;}=""; public string Folder=>System.IO.Path.GetDirectoryName(WdpPath)??""; public bool Bundled{get;set;} public TextDocument Wdp{get;set;}=new();
 public SortedDictionary<int,string> Descriptions{get;set;}=new(); public SortedDictionary<int,string> Options{get;set;}=new(); public SortedDictionary<int,string> Properties{get;set;}=new();
 public TextDocument? Wdt{get;set;} public List<WdtMapping> Mappings{get;set;}=new(); public List<string> TitleBlockNames{get;set;}=new(); public TextDocument? Wdl{get;set;} public SortedDictionary<int,string> Labels{get;set;}=new();
 public string TemplateDrawingPath{get;set;}=""; public List<string> DrawingPaths{get;set;}=new();
}
public enum Severity{Error,Warning,Info}
public enum RepairAction{None,Add,Replace,CreateFile,InsertWdm,RemoveDrawing,SetAttribute,CreateLayer,RepairLayer,PurgeLayer}
public sealed class Finding {
 public string Category{get;set;}=""; public string Item{get;set;}=""; public string Current{get;set;}=""; public string Standard{get;set;}=""; public string Note{get;set;}="";
 public Severity Severity{get;set;} public RepairAction Action{get;set;} public bool Recommended{get;set;} public bool Selected{get;set;}
 public string Target{get;set;}=""; public char Prefix{get;set;} public int Index{get;set;} public string Value{get;set;}=""; public string Name{get;set;}="";
 public string Key=>Target+"|"+Category+"|"+Item;
 public string ActionText=>Action switch{RepairAction.Add=>"Add line",RepairAction.Replace=>"Replace value",RepairAction.CreateFile=>"Create file",RepairAction.InsertWdm=>"Insert WD_M",RepairAction.RemoveDrawing=>"Remove from project",RepairAction.SetAttribute=>"Set WD_M value",RepairAction.CreateLayer=>"Create layer from template",RepairAction.RepairLayer=>"Repair layer",RepairAction.PurgeLayer=>"Purge layer",_=>"(report only)"};
 public bool IsDrawingAction=>Action is RepairAction.InsertWdm or RepairAction.SetAttribute or RepairAction.CreateLayer or RepairAction.RepairLayer or RepairAction.PurgeLayer;
}
public sealed class Diagnosis {
 public string WdpPath{get;set;}=""; public string Folder=>System.IO.Path.GetDirectoryName(WdpPath)??""; public string ProjectName=>System.IO.Path.GetFileNameWithoutExtension(WdpPath); public ProjectStandard Standard{get;set;}=new(); public DoctorRules Rules{get;set;}=new();
 public List<Finding> Findings{get;set;}=new(); public List<WdpDrawingEntry> Drawings{get;set;}=new(); public List<string> Log{get;set;}=new(); public bool DrawingsChecked{get;set;} public int OpenDrawings{get;set;} public DateTime Examined{get;set;}=DateTime.Now; public DrawingInspection? TemplateDrawing{get;set;}
 public int Errors=>Findings.Count(f=>f.Severity==Severity.Error); public int Warnings=>Findings.Count(f=>f.Severity==Severity.Warning); public int Infos=>Findings.Count(f=>f.Severity==Severity.Info);
 public bool Healthy=>Errors==0&&Warnings==0; public int Recommended=>Findings.Count(f=>f.Recommended); public int Selected=>Findings.Count(f=>f.Selected&&f.Action!=RepairAction.None);
 public string Status=>Healthy?"Healthy":$"{Errors} error(s), {Warnings} warning(s)";
}
public sealed class RepairResult { public Diagnosis Before{get;set;}=new(); public Diagnosis? After{get;set;} public string BackupFolder{get;set;}=""; public List<string> Log{get;set;}=new(); public int Applied{get;set;} public List<Finding> Unresolved{get;set;}=new(); public string ReportPath{get;set;}=""; public string Report=>string.Join(Environment.NewLine,Log); }
public sealed class DrawingInspection {
 public string Path{get;set;}=""; public bool OpenInEditor{get;set;} public bool HasWdm{get;set;} public List<string> TitleBlocks{get;set;}=new(); public List<string> Layouts{get;set;}=new(); public string Error{get;set;}="";
 public Dictionary<string,string> WdmValues{get;set;}=new(StringComparer.OrdinalIgnoreCase); public List<LayerInfo> Layers{get;set;}=new();
 public LayerInfo? Layer(string name)=>Layers.FirstOrDefault(l=>l.Name.Equals(name,StringComparison.OrdinalIgnoreCase));
 public string Wdm(string tag)=>WdmValues.TryGetValue(tag,out var v)?v:"";
}
public sealed class LayerInfo {
 public string Name{get;set;}=""; public string Color{get;set;}=""; public string Linetype{get;set;}=""; public int LineWeight{get;set;}=-3; public bool IsPlottable{get;set;}=true; public bool IsOff{get;set;} public bool IsFrozen{get;set;} public bool IsLocked{get;set;}
 public string Description{get;set;}=""; public List<string> XdataApps{get;set;}=new(); public int EntityCount{get;set;}
 public string Summary=>$"color {Color}, linetype {Linetype}, lineweight {(LineWeight==-3?"default":LineWeight==-2?"ByBlock":LineWeight==-1?"ByLayer":(LineWeight/100.0).ToString("0.00")+" mm")}{(IsPlottable?"":", no plot")}";
}
public sealed class DrawingRepairRequest {
 public string DrawingPath{get;set;}=""; public string TemplateDrawingPath{get;set;}=""; public string WdmBlockName{get;set;}="WD_M"; public bool InsertWdm{get;set;}
 public Dictionary<string,string> Attributes{get;set;}=new(StringComparer.OrdinalIgnoreCase); public List<string> CreateLayers{get;set;}=new(); public List<string> RepairLayers{get;set;}=new(); public List<string> PurgeLayers{get;set;}=new();
 public bool NeedsTemplate=>InsertWdm||CreateLayers.Count>0||RepairLayers.Count>0;
}
