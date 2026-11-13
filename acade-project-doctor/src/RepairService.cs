namespace LSE.AutoCADElectrical.ProjectDoctor;
public static class RepairService {
 public static RepairResult Execute(Diagnosis dx,AppSettings s,Action<string>? progress=null){
  var sel=dx.Findings.Where(f=>f.Selected&&f.Action!=RepairAction.None).ToList();if(sel.Count==0)throw new InvalidOperationException("Nothing is ticked. Tick the repairs to apply, then press Repair.");
  if(!File.Exists(dx.WdpPath))throw new FileNotFoundException("Project file was not found.",dx.WdpPath);
  var res=new RepairResult{Before=dx};void Log(string m){res.Log.Add(m);progress?.Invoke(m);}
  Log($"Started: {DateTime.Now:s} by {Environment.UserName}@{Environment.MachineName}");Log("Project: "+dx.WdpPath);Log("Standard: "+dx.Standard.WdpPath+(dx.Standard.Bundled?" (bundled copy)":""));Log("Rules: "+dx.Rules.Source);Log($"Repairs ticked: {sel.Count}");
  string wdtPath=TextFileService.ExistingCaseInsensitive(TextFileService.MappingPath(dx.WdpPath))??TextFileService.MappingPath(dx.WdpPath);string wdlPath=TextFileService.ExistingCaseInsensitive(TextFileService.LabelsPath(dx.WdpPath))??TextFileService.LabelsPath(dx.WdpPath);string aepxPath=TextFileService.ExistingCaseInsensitive(TextFileService.ConfigurationPath(dx.WdpPath))??TextFileService.ConfigurationPath(dx.WdpPath);
  var touched=new List<string>();if(sel.Any(f=>f.Target=="wdp"))touched.Add(dx.WdpPath);if(sel.Any(f=>f.Target=="wdt"&&f.Action!=RepairAction.CreateFile))touched.Add(wdtPath);if(sel.Any(f=>f.Target=="wdl"&&f.Action!=RepairAction.CreateFile))touched.Add(wdlPath);touched.AddRange(sel.Where(f=>f.IsDrawingAction).Select(f=>f.Target));
  foreach(string f in touched.Distinct(StringComparer.OrdinalIgnoreCase).Where(File.Exists))if((File.GetAttributes(f)&FileAttributes.ReadOnly)!=0)throw new InvalidOperationException("Read-only file: "+f+". If the project is under Vault control check it out first; otherwise clear the read-only attribute. Nothing was changed.");
  res.BackupFolder=Path.Combine(dx.Folder,s.BackupFolderName,DateTime.Now.ToString("yyyyMMdd_HHmmss"));var backed=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);var created=new List<string>();
  try{
   Directory.CreateDirectory(res.BackupFolder);foreach(string f in touched.Distinct(StringComparer.OrdinalIgnoreCase).Where(File.Exists)){string b=Path.Combine(res.BackupFolder,Path.GetFileName(f));for(int n=2;File.Exists(b);n++)b=Path.Combine(res.BackupFolder,Path.GetFileNameWithoutExtension(f)+"("+n+")"+Path.GetExtension(f));File.Copy(f,b,true);File.SetAttributes(b,FileAttributes.Normal);backed[f]=b;Log("Backed up "+Path.GetFileName(f)+" to "+res.BackupFolder);}
   var wdpSel=sel.Where(f=>f.Target=="wdp").ToList();
   if(wdpSel.Count>0){var doc=TextFileService.Read(dx.WdpPath);
    foreach(var f in wdpSel){if(f.Action==RepairAction.Add||f.Action==RepairAction.Replace){TextFileService.Set(doc,f.Prefix,f.Index,f.Value);Log($"{f.ActionText}: {f.Prefix}[{f.Index}] = {DiagnosisService.Short(f.Value)}");res.Applied++;}
     else if(f.Action==RepairAction.RemoveDrawing){var e=TextFileService.Drawings(doc).FirstOrDefault(x=>x.Path.Equals(f.Item,StringComparison.OrdinalIgnoreCase));if(e is null)Log("Skipped: drawing entry is no longer in the project list: "+f.Item);else{TextFileService.RemoveDrawing(doc,e);Log("Removed from the project list: "+f.Item);res.Applied++;}}}
    if(doc.Changed){TextFileService.Write(doc);Log($"Project file written ({TextFileService.EncodingName(doc.Encoding)}): {dx.WdpPath}");}}
   ApplyFile(sel,"wdt",wdtPath,dx.Standard.Wdt,(d,f)=>TextFileService.SetMapping(d,f.Item,f.Value),created,res,Log);
   ApplyFile(sel,"wdl",wdlPath,dx.Standard.Wdl,(d,f)=>TextFileService.SetLabel(d,f.Index,f.Value),created,res,Log);
   if(sel.Any(f=>f.Target=="aepx"&&f.Action==RepairAction.CreateFile)){if(File.Exists(aepxPath))Log("Skipped: "+Path.GetFileName(aepxPath)+" already exists");else{ProjectConfigurationService.Write(aepxPath,dx.WdpPath,dx.Drawings);created.Add(aepxPath);Log("Created "+aepxPath);res.Applied++;}}
   foreach(var g in sel.Where(f=>f.IsDrawingAction).GroupBy(f=>f.Target,StringComparer.OrdinalIgnoreCase)){var req=new DrawingRepairRequest{DrawingPath=g.Key,TemplateDrawingPath=dx.Standard.TemplateDrawingPath,WdmBlockName=dx.Rules.WdmBlockName};
    foreach(var f in g)switch(f.Action){case RepairAction.InsertWdm:req.InsertWdm=true;break;case RepairAction.SetAttribute:req.Attributes[f.Name]=f.Value;break;case RepairAction.CreateLayer:req.CreateLayers.Add(f.Name);break;case RepairAction.RepairLayer:req.RepairLayers.Add(f.Name);break;case RepairAction.PurgeLayer:req.PurgeLayers.Add(f.Name);break;}
    Log("Repairing drawing "+Path.GetFileName(g.Key)+$" ({g.Count()} repair(s))");foreach(string m in DrawingInspectionService.Repair(req))Log("  "+m);res.Applied+=g.Count();}
   Log("Verifying");res.After=DiagnosisService.Examine(dx.WdpPath,dx.Standard,dx.Rules,false,false);var inspected=new Dictionary<string,DrawingInspection>(StringComparer.OrdinalIgnoreCase);
   foreach(var f in sel){bool still;if(f.IsDrawingAction){if(!inspected.TryGetValue(f.Target,out var after)){after=DrawingInspectionService.Inspect(f.Target,dx.Rules.WdmBlockName,dx.Standard.TitleBlockNames);inspected[f.Target]=after;}still=!WireLayerService.Resolved(f,after,dx.TemplateDrawing,dx.Rules);}
    else still=res.After.Findings.Any(a=>a.Key==f.Key&&a.Action==f.Action);if(still){res.Unresolved.Add(f);Log("UNRESOLVED: "+f.Category+" / "+f.Item);}}
   Log(res.Unresolved.Count==0?$"Status: SUCCESS ({res.Applied} repair(s) applied, all verified)":$"Status: PARTIAL ({res.Applied} repair(s) applied, {res.Unresolved.Count} still reported)");
   res.ReportPath=Path.Combine(dx.Folder,"LSE_DoctorReport.txt");try{File.AppendAllLines(res.ReportPath,res.Log.Append(""));}catch(Exception ex){Log("Report could not be written to the project folder: "+ex.Message);}
   Prune(Path.Combine(dx.Folder,s.BackupFolderName),s.BackupsToKeep,Log);SettingsService.Remember(s,dx.WdpPath);return res;}
  catch(Exception ex){Log("ERROR: "+ex.Message);
   foreach(var (original,backup) in backed)try{File.Copy(backup,original,true);Log("Rollback restored "+Path.GetFileName(original));}catch(Exception rx){Log("Rollback could not restore "+original+": "+rx.Message);}
   foreach(string f in created)try{if(File.Exists(f)){File.Delete(f);Log("Rollback removed "+f);}}catch(Exception rx){Log("Rollback could not remove "+f+": "+rx.Message);}
   try{Directory.CreateDirectory(SettingsService.LogFolder);File.WriteAllLines(Path.Combine(SettingsService.LogFolder,$"DoctorRepair_{DateTime.Now:yyyyMMdd_HHmmss}.txt"),res.Log.Append("Detail: "+ex));}catch{}
   throw new InvalidOperationException(string.Join(Environment.NewLine,res.Log),ex);}
 }
 static void ApplyFile(List<Finding> sel,string target,string path,TextDocument? std,Action<TextDocument,Finding> apply,List<string> created,RepairResult res,Action<string> log){var list=sel.Where(f=>f.Target==target).ToList();if(list.Count==0)return;
  if(list.Any(f=>f.Action==RepairAction.CreateFile)){if(std is null||!std.Exists)throw new InvalidOperationException("The standard has no "+Path.GetFileName(path)+" to copy from.");if(File.Exists(path))log("Skipped: "+Path.GetFileName(path)+" already exists");else{File.Copy(std.Path,path);created.Add(path);log("Created "+path+" from "+std.Path);res.Applied++;}}
  var edits=list.Where(f=>f.Action==RepairAction.Add||f.Action==RepairAction.Replace).ToList();if(edits.Count==0)return;var doc=TextFileService.Read(path);if(!doc.Exists){log("Skipped: "+path+" does not exist and 'Create file' was not ticked");return;}
  foreach(var f in edits){apply(doc,f);log($"{f.ActionText}: {Path.GetFileName(path)} {f.Item} = {f.Value}");res.Applied++;}if(doc.Changed){TextFileService.Write(doc);log($"Written ({TextFileService.EncodingName(doc.Encoding)}): {path}");}}
 static void Prune(string root,int keep,Action<string> log){try{if(!Directory.Exists(root))return;var dirs=Directory.GetDirectories(root).OrderByDescending(x=>x,StringComparer.OrdinalIgnoreCase).Skip(keep).ToList();foreach(string d in dirs){foreach(string f in Directory.EnumerateFiles(d,"*",SearchOption.AllDirectories))File.SetAttributes(f,FileAttributes.Normal);Directory.Delete(d,true);log("Old backup removed: "+d);}}catch(Exception ex){log("Backup pruning skipped: "+ex.Message);}}
}
public static class ProjectConfigurationService {
 public static void Write(string aepxPath,string wdpPath,List<WdpDrawingEntry> drawings){string folder=Path.GetDirectoryName(wdpPath)??"";var sb=new System.Text.StringBuilder();sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<ProjectConfiguration Version=\"1.0\"><Project ProjectPath=\"").Append(Esc(folder)).Append("\" LocalGUID=\"").Append(Guid.NewGuid().ToString()).Append("\" EmxFilePath=\"\" EmxGUID=\"\"/><Drawings NextID=\"").Append(drawings.Count+1).Append("\">");
  for(int i=0;i<drawings.Count;i++)sb.Append("<Drawing FilePath=\"").Append(Esc(drawings[i].Path.ToUpperInvariant())).Append("\" FileID=\"").Append(i+1).Append("\"/>");sb.Append("</Drawings></ProjectConfiguration>");
  string t=aepxPath+".tmp";File.WriteAllText(t,sb.ToString(),new System.Text.UTF8Encoding(false));File.Move(t,aepxPath,true);}
 static string Esc(string v)=>System.Security.SecurityElement.Escape(v)??"";
}
