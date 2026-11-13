namespace LSE.AutoCADElectrical.ProjectEnvironment;
public static class ApplyService {
 public static ApplyResult Execute(EnvPlan plan,AppSettings s,HostBridge bridge,Action<string>? progress=null){
  var sel=plan.Changes.Where(c=>c.Selected&&c.Applies).ToList();if(sel.Count==0)throw new InvalidOperationException("Nothing is ticked. Tick the changes to apply, then press Apply.");
  var st=plan.Project;if(!File.Exists(st.WdpPath))throw new FileNotFoundException("Project file was not found.",st.WdpPath);
  if(sel.Any(c=>c.IsDrawing)&&bridge.ApplyDrawing is null)throw new InvalidOperationException("Drawing changes are ticked but no drawing writer is available (outside AutoCAD).");
  var res=new ApplyResult{Plan=plan};void Log(string m){res.Log.Add(m);progress?.Invoke(m);}
  Log($"Started: {DateTime.Now:s} by {Environment.UserName}@{Environment.MachineName}");Log("Project: "+st.WdpPath);Log("Profile: "+plan.ProfileName+"  |  Wire scheme: "+plan.SchemeName);Log("Rules: "+plan.Rules.Source);Log($"Changes ticked: {sel.Count}");
  string markerPath=MarkerService.PathFor(st.WdpPath);var touched=new List<string>();if(sel.Any(c=>c.Target=="wdp"))touched.Add(st.WdpPath);touched.AddRange(sel.Where(c=>c.Kind==ChangeKind.CopyFile).Select(c=>c.Target));touched.AddRange(sel.Where(c=>c.IsDrawing).Select(c=>c.Target));if(sel.Any(c=>c.Kind==ChangeKind.Marker))touched.Add(markerPath);
  VaultProbe.RequireWritable(touched.Distinct(StringComparer.OrdinalIgnoreCase).Where(File.Exists));
  res.BackupFolder=Path.Combine(st.Folder,s.BackupFolderName,DateTime.Now.ToString("yyyyMMdd_HHmmss"));var backed=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);var created=new List<string>();
  try{
   Directory.CreateDirectory(res.BackupFolder);foreach(string f in touched.Distinct(StringComparer.OrdinalIgnoreCase).Where(File.Exists)){string b=Path.Combine(res.BackupFolder,Path.GetFileName(f));for(int n=2;File.Exists(b);n++)b=Path.Combine(res.BackupFolder,Path.GetFileNameWithoutExtension(f)+"("+n+")"+Path.GetExtension(f));File.Copy(f,b,true);File.SetAttributes(b,FileAttributes.Normal);backed[f]=b;Log("Backed up "+Path.GetFileName(f)+" to "+res.BackupFolder);}
   foreach(string f in sel.Where(c=>c.IsDrawing).Select(c=>c.Target).Distinct(StringComparer.OrdinalIgnoreCase))if(st.Drawings.Any(d=>d.OpenInEditor&&d.Path.Equals(f,StringComparison.OrdinalIgnoreCase)))Log("Open in the editor: "+Path.GetFileName(f)+". The backup is the file on disk (its last saved state); changes are written into the open session and not saved. Rollback restores the file on disk only: to discard changes in the session, close the drawing without saving.");
   var wdpSel=sel.Where(c=>c.Target=="wdp").ToList();
   if(wdpSel.Count>0){var doc=TextFileService.Read(st.WdpPath);foreach(var c in wdpSel){TextFileService.Set(doc,c.Prefix,c.Index,c.Value);Log($"{c.KindText}: {c.Prefix}[{c.Index}] = {EnvironmentService.Short(c.Value)}  ({c.Item})");res.Applied++;}
    if(doc.Changed){TextFileService.Write(doc);Log($"Project file written ({TextFileService.EncodingName(doc.Encoding)}): {st.WdpPath}");}}
   foreach(var c in sel.Where(c=>c.Kind==ChangeKind.CopyFile)){bool existed=File.Exists(c.Target);File.Copy(c.Value,c.Target,true);if(!existed)created.Add(c.Target);Log("Copied "+c.Value+" -> "+c.Target);res.Applied++;}
   var libs=TextFileService.PathList(st.Option(1)??"");
   foreach(var g in sel.Where(c=>c.IsDrawing).GroupBy(c=>c.Target,StringComparer.OrdinalIgnoreCase)){var req=new DrawingApplyRequest{DrawingPath=g.Key,WdmBlockName=plan.Rules.WdmBlockName,LibraryPaths=libs};
    foreach(var c in g){if(c.Kind==ChangeKind.SetWdm)req.Attributes[c.Name]=c.Value;else if(c.Kind==ChangeKind.WireNumbers)req.Wires=c.Wires;else if(c.Kind==ChangeKind.UnfixWireNumbers){req.UnfixHandles.AddRange(c.Handles);req.WireNoLayer=c.Value;}}
    Log("Writing drawing "+Path.GetFileName(g.Key)+$" ({req.Attributes.Count} WD_M value(s){(req.Wires is not null?", "+(req.Wires.Count(WireAction.Set)+req.Wires.Count(WireAction.Insert))+" wire number(s)":"")}{(req.UnfixHandles.Count>0?", "+req.UnfixHandles.Count+" to un-fix":"")})");foreach(string m in bridge.ApplyDrawing!(req))Log("  "+m);res.Applied+=g.Count();}
   if(sel.Any(c=>c.Kind==ChangeKind.Marker)){bool existed=File.Exists(markerPath);var m=new EnvMarker{Profile=plan.ProfileName,Customer=plan.Profile?.Customer??"",Scheme=plan.SchemeName,Applied=DateTime.Now,By=Environment.UserName,FromTo=plan.FromTo is not null,Values=new(plan.Resolved,StringComparer.OrdinalIgnoreCase)};
    m.Properties=sel.Where(c=>c.Kind==ChangeKind.SetProperty).Select(c=>c.Index).Distinct().OrderBy(i=>i).ToList();m.Options=sel.Where(c=>c.Kind==ChangeKind.SetOption).Select(c=>c.Index).Distinct().OrderBy(i=>i).ToList();m.DrawingAttributes=sel.Where(c=>c.Kind==ChangeKind.SetWdm).Select(c=>c.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).ToList();
    if(st.Marker is not null){foreach(int i in st.Marker.Properties)if(!m.Properties.Contains(i))m.Properties.Add(i);foreach(int i in st.Marker.Options)if(!m.Options.Contains(i))m.Options.Add(i);foreach(string a in st.Marker.DrawingAttributes)if(!m.DrawingAttributes.Contains(a,StringComparer.OrdinalIgnoreCase))m.DrawingAttributes.Add(a);m.Properties.Sort();m.Options.Sort();}
    MarkerService.Write(st.WdpPath,m);if(!existed)created.Add(markerPath);Log("Recorded profile in "+Path.GetFileName(markerPath));res.Applied++;}
   Log("Verifying");var after=TextFileService.Read(st.WdpPath);var inspected=new Dictionary<string,DrawingState>(StringComparer.OrdinalIgnoreCase);var wires=new Dictionary<string,DrawingWires>(StringComparer.OrdinalIgnoreCase);
   foreach(var c in sel){bool still=false;switch(c.Kind){
     case ChangeKind.SetProperty:case ChangeKind.SetOption:still=!(TextFileService.Get(after,c.Prefix,c.Index)??"").Trim().Equals(c.Value.Trim(),StringComparison.Ordinal);break;
     case ChangeKind.CopyFile:still=!File.Exists(c.Target)||new FileInfo(c.Target).Length!=new FileInfo(c.Value).Length;break;
     case ChangeKind.SetWdm:if(bridge.Inspect is null)break;if(!inspected.TryGetValue(c.Target,out var d)){d=bridge.Inspect(c.Target);inspected[c.Target]=d;}still=d.Error.Length>0||!d.Wdm(c.Name).Trim().Equals(c.Value.Trim(),StringComparison.Ordinal);break;
     case ChangeKind.WireNumbers:if(bridge.ReadWires is null||c.Wires is null)break;if(!wires.TryGetValue(c.Target,out var w)){w=bridge.ReadWires(c.Target);wires[c.Target]=w;}var issues=WireNetworkService.Verify(c.Wires,w,plan.Rules.FromTo);still=issues.Count>0;foreach(string i in issues)Log("  "+Path.GetFileName(c.Target)+": "+i);break;
     case ChangeKind.UnfixWireNumbers:if(bridge.ReadWires is null)break;if(!wires.TryGetValue(c.Target,out var u)){u=bridge.ReadWires(c.Target);wires[c.Target]=u;}still=u.Marks.Any(m=>m.IsFixed&&c.Handles.Contains(m.Handle,StringComparer.OrdinalIgnoreCase));break;
     case ChangeKind.Marker:still=!File.Exists(markerPath);break;}
    if(still){res.Unresolved.Add(c);Log("UNRESOLVED: "+c.Scope+" / "+c.Item);}}
   Log(res.Unresolved.Count==0?$"Status: SUCCESS ({res.Applied} change(s) applied, all verified)":$"Status: PARTIAL ({res.Applied} change(s) applied, {res.Unresolved.Count} not verified)");
   foreach(string m in VaultProbe.CheckinReminder(touched.Concat(created).Where(f=>!st.Drawings.Any(d=>d.OpenInEditor&&d.Path.Equals(f,StringComparison.OrdinalIgnoreCase))).Distinct(StringComparer.OrdinalIgnoreCase)))Log(m);
   res.ReportPath=Path.Combine(st.Folder,"LSE_EnvReport.txt");try{File.AppendAllLines(res.ReportPath,res.Log.Append(""));}catch(Exception ex){Log("Report could not be written to the project folder: "+ex.Message);}
   Prune(Path.Combine(st.Folder,s.BackupFolderName),s.BackupsToKeep,Log);SettingsService.Remember(s,st.WdpPath);return res;}
  catch(Exception ex){Log("ERROR: "+ex.Message);
   foreach(var (original,backup) in backed)try{File.Copy(backup,original,true);Log("Rollback restored "+Path.GetFileName(original));}catch(Exception rx){Log("Rollback could not restore "+original+": "+rx.Message);}
   foreach(string f in created)try{if(File.Exists(f)){File.Delete(f);Log("Rollback removed "+f);}}catch(Exception rx){Log("Rollback could not remove "+f+": "+rx.Message);}
   try{Directory.CreateDirectory(SettingsService.LogFolder);File.WriteAllLines(Path.Combine(SettingsService.LogFolder,$"EnvApply_{DateTime.Now:yyyyMMdd_HHmmss}.txt"),res.Log.Append("Detail: "+ex));}catch{}
   throw new InvalidOperationException(string.Join(Environment.NewLine,res.Log),ex);}
 }
 static void Prune(string root,int keep,Action<string> log){try{if(!Directory.Exists(root))return;var dirs=Directory.GetDirectories(root).OrderByDescending(x=>x,StringComparer.OrdinalIgnoreCase).Skip(keep).ToList();foreach(string d in dirs){foreach(string f in Directory.EnumerateFiles(d,"*",SearchOption.AllDirectories))File.SetAttributes(f,FileAttributes.Normal);Directory.Delete(d,true);log("Old backup removed: "+d);}}catch(Exception ex){log("Backup pruning skipped: "+ex.Message);}}
}
