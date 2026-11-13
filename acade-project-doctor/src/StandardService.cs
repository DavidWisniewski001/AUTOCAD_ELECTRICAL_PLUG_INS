namespace LSE.AutoCADElectrical.ProjectDoctor;
public static class StandardService {
 public static ProjectStandard Load(AppSettings s,DoctorRules r){string path=s.TemplateProjectPath.Trim();bool bundled=false;
  if(path.Length==0||!File.Exists(path)){string? b=SettingsService.BundledStandardWdp();if(b is null)throw new FileNotFoundException("The LSE template project (.wdp) was not found and no bundled copy exists in the 'standard' folder next to the plugin. Set 'Template project (.wdp)' in LSEDOCTORSETTINGS.",path);path=b;bundled=true;}
  var std=new ProjectStandard{WdpPath=Path.GetFullPath(path),Bundled=bundled};std.Wdp=TextFileService.Read(std.WdpPath);std.Descriptions=TextFileService.Group(std.Wdp,'*');std.Options=TextFileService.Group(std.Wdp,'+');std.Properties=TextFileService.Group(std.Wdp,'?');
  if(std.Descriptions.Count==0&&std.Options.Count==0&&std.Properties.Count==0)throw new InvalidOperationException("The template project has no *[n], +[n] or ?[n] lines, so it does not look like an AutoCAD Electrical .wdp: "+std.WdpPath);
  string? wdt=TextFileService.ExistingCaseInsensitive(TextFileService.MappingPath(std.WdpPath))??TextFileService.ExistingCaseInsensitive(Path.Combine(std.Folder,"default.wdt"));if(wdt is not null){std.Wdt=TextFileService.Read(wdt);std.Mappings=TextFileService.Mappings(std.Wdt);}
  string? wdl=TextFileService.ExistingCaseInsensitive(TextFileService.LabelsPath(std.WdpPath));if(wdl is not null){std.Wdl=TextFileService.Read(wdl);std.Labels=TextFileService.Labels(std.Wdl);}
  std.TitleBlockNames=r.TitleBlockNames.Where(x=>x.Trim().Length>0).Select(x=>x.Trim()).ToList();if(std.TitleBlockNames.Count==0){var block=std.Mappings.FirstOrDefault(m=>m.IsBlock);if(block is not null)std.TitleBlockNames=TextFileService.BlockNames(block.Code);}
  foreach(var e in TextFileService.Drawings(std.Wdp))std.DrawingPaths.Add(TextFileService.FullPath(std.WdpPath,e));
  std.TemplateDrawingPath=std.DrawingPaths.FirstOrDefault(p=>File.Exists(p)&&Path.GetFileName(p).StartsWith("DRAWING",StringComparison.OrdinalIgnoreCase))??std.DrawingPaths.FirstOrDefault(File.Exists)??"";
  return std;}
 public static string Describe(ProjectStandard std)=>$"{std.WdpPath}{(std.Bundled?"  [bundled copy - the vault template was not reachable]":"")}  |  {std.Descriptions.Count} descriptions, {std.Options.Count} options, {std.Properties.Count} properties, {std.Mappings.Count} title block mappings, {std.Labels.Count} labels{(std.TemplateDrawingPath.Length==0?"  |  no template drawing (WD_M insertion unavailable)":"")}";
 public static string Label(DoctorRules r,ProjectStandard std,char prefix,int index){string key=prefix+"["+index+"]";if(prefix=='*')return std.Labels.TryGetValue(index,out var l)&&l.Trim().Length>0?key+" "+l.Trim():key;return r.Labels.TryGetValue(prefix.ToString()+index,out var n)&&n.Trim().Length>0?key+" "+n.Trim():key;}
}
