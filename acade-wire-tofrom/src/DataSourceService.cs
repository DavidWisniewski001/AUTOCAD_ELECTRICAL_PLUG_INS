namespace LSE.AutoCADElectrical.WireToFrom;
// Picks the data source (project scratch database or a saved Wire From/To report), loads the rows, resolves each row's
// drawings and checks that the database is not older than the drawings. Never writes anything.
public static class DataSourceService {
 public static List<WireRow> Load(AppSettings s,ToFromConfig c,ProjectInfo p,string scratchFromLisp,string? reportFile,List<Issue> issues,List<string> notes,out string sourceText){var rows=new List<WireRow>();var dbDrawings=new Dictionary<int,string>();
  if(s.DataSource.Equals("report",StringComparison.OrdinalIgnoreCase)){string file=(reportFile??s.LastReportFile).Trim();if(file.Length==0)throw new InvalidOperationException("Data source is 'report file' but no file is chosen. Save a Wire From/To report to a file in AutoCAD Electrical and pick it.");
   rows=ReportImport.Read(file,c.ReportFile,notes);sourceText="report file "+file;}
  else{string path=ProjectService.DatabasePath(p,c.Database,scratchFromLisp);p.DatabasePath=path;p.DatabaseWritten=File.Exists(path)?File.GetLastWriteTime(path):null;if(scratchFromLisp.Length>0)notes.Add("Scratch database path from AutoCAD Electrical: "+scratchFromLisp);
   rows=ScratchDb.ReadRows(path,c.Database,notes);dbDrawings=ScratchDb.ReadDrawings(path,c.Database,notes);sourceText="project database "+path;Stale(s,p,issues,notes);}
  IdentifierService.Resolve(rows,p,c.Database,dbDrawings,issues);return rows;}
 public static void Stale(AppSettings s,ProjectInfo p,List<Issue> issues,List<string> notes){if(p.DatabaseWritten is null||s.StalePolicy.Equals("ignore",StringComparison.OrdinalIgnoreCase))return;var newer=new List<string>();
  foreach(var d in p.Drawings.Where(d=>!d.ReferenceOnly)){try{if(File.Exists(d.Path)&&File.GetLastWriteTime(d.Path)>p.DatabaseWritten.Value.AddSeconds(2))newer.Add(d.Path);}catch{}}
  foreach(string f in newer)issues.Add(new Issue{Kind=IssueKind.StaleDatabase,Drawing=f,Detail=$"saved {File.GetLastWriteTime(f):yyyy-MM-dd HH:mm}, database written {p.DatabaseWritten:yyyy-MM-dd HH:mm}"});
  if(newer.Count==0)return;string msg=$"{newer.Count} drawing(s) were saved after the project database was last written ({p.DatabaseWritten:yyyy-MM-dd HH:mm}). Refresh it (run a native report such as Wire From/To, or configure the refresh LISP) so the from/to data is current.";
  if(s.StalePolicy.Equals("stop",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException(msg+" (stalePolicy is 'stop')");notes.Add("WARNING: "+msg);}
}
