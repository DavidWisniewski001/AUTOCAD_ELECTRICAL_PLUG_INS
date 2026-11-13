namespace LSE.AutoCADElectrical.WireToFrom;
// The one write path. Order: validate, refuse read-only, back up every closed drawing that will change, write labels drawing
// by drawing, verify by reading each drawing again, export the CSVs and the validation report, append the run log, prune
// backups. Any exception restores the backed-up drawings, removes files this run created, writes the log and rethrows.
public static class RunService {
 public static RunResult Execute(RunPlan plan,AppSettings s,HostBridge bridge,bool exportCsv=true,Action<string>? progress=null){
  var p=plan.Project;if(!File.Exists(p.WdpPath))throw new FileNotFoundException("Project file was not found.",p.WdpPath);var work=plan.Drawings.Where(d=>d.HasWork).ToList();
  if(work.Count>0&&!bridge.CanWrite)throw new InvalidOperationException("Label changes are ticked but no drawing writer is available (outside AutoCAD).");
  var res=new RunResult{Plan=plan};void Log(string m){res.Log.Add(m);progress?.Invoke(m);}
  Log($"Started: {DateTime.Now:s} by {Environment.UserName}@{Environment.MachineName}");Log("Project: "+p.WdpPath);Log("Data source: "+plan.DataSource);Log("Config: "+plan.Config.Source);Log($"Rows: {plan.Rows.Count}  |  drawings to write: {work.Count}  |  labels ticked: {plan.Selected}");
  var closed=work.Where(d=>!d.State.OpenInEditor).Select(d=>d.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
  foreach(string f in closed.Where(File.Exists))if((File.GetAttributes(f)&FileAttributes.ReadOnly)!=0)throw new InvalidOperationException("Read-only drawing: "+f+". If the project is under Vault control check it out first; otherwise clear the read-only attribute. Nothing was changed.");
  var backed=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);var created=new List<string>();
  try{
   if(closed.Count>0){res.BackupFolder=Path.Combine(p.Folder,s.BackupFolderName,DateTime.Now.ToString("yyyyMMdd_HHmmss"));Directory.CreateDirectory(res.BackupFolder);
    foreach(string f in closed.Where(File.Exists)){string b=Path.Combine(res.BackupFolder,Path.GetFileName(f));for(int n=2;File.Exists(b);n++)b=Path.Combine(res.BackupFolder,Path.GetFileNameWithoutExtension(f)+"("+n+")"+Path.GetExtension(f));File.Copy(f,b,true);File.SetAttributes(b,FileAttributes.Normal);backed[f]=b;Log("Backed up "+Path.GetFileName(f)+" to "+res.BackupFolder);}}
   foreach(var d in work.Where(d=>d.State.OpenInEditor))Log("Open in the editor: "+d.Name+". Labels are written into the open session and not saved by LSE_ToFrom; save the drawing to keep them. Rollback cannot undo session changes: close without saving to discard them.");
   foreach(var d in work){var req=new DrawingWriteRequest{DrawingPath=d.Path,Changes=d.Changes.Where(c=>c.Selected&&c.Writes).ToList(),Schematic=plan.Config.Schematic,LibraryPaths=LibraryPaths(plan.Config)};
    Log($"Writing {d.Name}: {req.Changes.Count(c=>c.Action==LabelAction.Insert)} insert, {req.Changes.Count(c=>c.Action==LabelAction.Update)} update, {req.Changes.Count(c=>c.Action==LabelAction.Delete)} delete");foreach(string m in bridge.WriteLabels!(req))Log("  "+m);res.Applied+=req.Changes.Count;}
   if(work.Count>0&&bridge.CanRead){Log("Verifying");foreach(var d in work){DrawingLabels after;try{after=bridge.ReadLabels!(d.Path);}catch(Exception ex){Log("  "+d.Name+": could not be re-read: "+ex.Message);foreach(var c in d.Changes.Where(c=>c.Selected&&c.Writes))res.Unresolved.Add(c);continue;}
     var byKey=after.Labels.GroupBy(l=>l.Key.Trim(),StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.ToList(),StringComparer.OrdinalIgnoreCase);
     foreach(var c in d.Changes.Where(c=>c.Selected&&c.Writes)){bool still;byKey.TryGetValue(c.Key,out var found);
      if(c.Action==LabelAction.Delete)still=found is not null&&found.Any(l=>l.Handle.Equals(c.ExistingHandle,StringComparison.OrdinalIgnoreCase));else still=found is null||found.Count!=1||!found[0].Text.Trim().Equals(c.Text,StringComparison.Ordinal);
      if(still){res.Unresolved.Add(c);Log("  UNRESOLVED: "+d.Name+" "+c.ActionText+" "+c.Key+" \""+c.Text+"\""+(found is null?" (not found)":found.Count>1?" ("+found.Count+" labels share the key)":" (reads \""+found[0].Text+"\")"));}}}}
   if(exportCsv){var csv=plan.Config.Csv;res.LabelCsv=Track(Export.WriteLabelCsv(plan,ProjectService.OutputFile(p,csv,csv.LabelFile)),created);Log("Label CSV: "+res.LabelCsv+$" ({plan.CsvEnds.Count} rows)");res.ReportCsv=Track(Export.WriteReportCsv(plan,ProjectService.OutputFile(p,csv,csv.ReportFile)),created);Log("Report CSV: "+res.ReportCsv+$" ({plan.Rows.Count} rows)");}
   res.ValidationPath=Track(Export.WriteValidation(plan,res,ProjectService.OutputFile(p,plan.Config.Csv,plan.Config.Csv.ValidationFile)),created);Log("Validation report: "+res.ValidationPath+$" ({plan.Issues.Count} issue(s))");
   Log(res.Unresolved.Count==0?$"Status: SUCCESS ({res.Applied} label change(s) written and verified, {plan.Issues.Count} validation issue(s))":$"Status: PARTIAL ({res.Applied} label change(s) written, {res.Unresolved.Count} not verified)");
   res.ReportPath=Path.Combine(p.Folder,"LSE_ToFromReport.txt");try{File.AppendAllLines(res.ReportPath,res.Log.Append(""));}catch(Exception ex){Log("Report could not be written to the project folder: "+ex.Message);}
   Prune(Path.Combine(p.Folder,s.BackupFolderName),s.BackupsToKeep,Log);SettingsService.Remember(s,p.WdpPath);return res;}
  catch(Exception ex){Log("ERROR: "+ex.Message);
   foreach(var (original,backup) in backed)try{File.Copy(backup,original,true);Log("Rollback restored "+Path.GetFileName(original));}catch(Exception rx){Log("Rollback could not restore "+original+": "+rx.Message);}
   foreach(string f in created)try{if(File.Exists(f)){File.Delete(f);Log("Rollback removed "+f);}}catch(Exception rx){Log("Rollback could not remove "+f+": "+rx.Message);}
   try{Directory.CreateDirectory(SettingsService.LogFolder);File.WriteAllLines(Path.Combine(SettingsService.LogFolder,$"ToFromRun_{DateTime.Now:yyyyMMdd_HHmmss}.txt"),res.Log.Append("Detail: "+ex));}catch{}
   throw new InvalidOperationException(string.Join(Environment.NewLine,res.Log),ex);}
 }
 static string Track(string path,List<string> created){created.Add(path);return path;}
 public static List<string> LibraryPaths(ToFromConfig c)=>c.Schematic.BlockFolder.Trim().Length>0?c.Schematic.BlockFolder.Split(';',StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim()).Where(x=>x.Length>0).ToList():new List<string>{SettingsService.AssemblyFolder};
 static void Prune(string root,int keep,Action<string> log){try{if(!Directory.Exists(root))return;var dirs=Directory.GetDirectories(root).OrderByDescending(x=>x,StringComparer.OrdinalIgnoreCase).Skip(keep).ToList();foreach(string d in dirs){foreach(string f in Directory.EnumerateFiles(d,"*",SearchOption.AllDirectories))File.SetAttributes(f,FileAttributes.Normal);Directory.Delete(d,true);log("Old backup removed: "+d);}}catch(Exception ex){log("Backup pruning skipped: "+ex.Message);}}
}
