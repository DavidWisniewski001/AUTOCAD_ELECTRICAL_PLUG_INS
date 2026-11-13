using System.Runtime.InteropServices;
namespace LSE.AutoCADElectrical.WireToFrom;
// Every scratch-database query lives here and nowhere else. The database is opened read-only through ADODB (COM, dynamic),
// so no NuGet package and no write is possible. Table and column names come from tofrom.config.json; LSETOFROMDISCOVER
// lists what the live database holds so the config can be confirmed on the workstation.
public static class ScratchDb {
 public const string DiscoverHint="Run LSETOFROMDISCOVER on the workstation: it lists the tables and columns of the project database, marks the table that carries the from/to columns, and can write the SELECT into tofrom.config.json.";
 static dynamic Open(string path,IEnumerable<string> providers){if(!File.Exists(path))throw new FileNotFoundException("Project database was not found. Run a native report (for example Wire From/To) once so AutoCAD Electrical creates and refreshes it, or correct database.pathPattern in the config.",path);
  Type t=Type.GetTypeFromProgID("ADODB.Connection")??throw new InvalidOperationException("ADODB is not available on this workstation.");dynamic c=Activator.CreateInstance(t)!;var errors=new List<string>();
  foreach(string p in providers){try{c.Mode=1;c.Open($"Provider={p};Data Source={path};Persist Security Info=False;");return c;}catch(Exception ex){errors.Add(p+": "+ex.Message);}}
  Release(c);throw new InvalidOperationException("The project database could not be opened read-only with any configured provider (the 64-bit Access Database Engine must be installed):"+Environment.NewLine+string.Join(Environment.NewLine,errors));}
 public static List<SchemaTable> Schema(string path,DatabaseConfig d){dynamic c=Open(path,d.Providers);dynamic? r=null;try{r=c.OpenSchema(4);var map=new Dictionary<string,SchemaTable>(StringComparer.OrdinalIgnoreCase);
   while(!r.EOF){string table=Convert.ToString(r.Fields["TABLE_NAME"].Value)??"",col=Convert.ToString(r.Fields["COLUMN_NAME"].Value)??"";if(table.Length>0&&!table.StartsWith("MSys",StringComparison.OrdinalIgnoreCase)){if(!map.TryGetValue(table,out var st)){st=new SchemaTable{Name=table};map[table]=st;}st.Columns.Add(col);}r.MoveNext();}
   var list=map.Values.OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToList();foreach(var st in list){st.Columns.Sort(StringComparer.OrdinalIgnoreCase);st.Score=d.CandidateColumns.Count(k=>st.Columns.Contains(k,StringComparer.OrdinalIgnoreCase));}return list;}
  finally{Release(r);try{c.Close();}catch{}Release(c);}}
 public static List<Dictionary<string,string>> Query(string path,string sql,IEnumerable<string> providers,int limit=0){if(!sql.Trim().StartsWith("SELECT",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Only SELECT statements are run against the project database.");
  dynamic c=Open(path,providers);dynamic? r=null;try{r=c.Execute(sql);var rows=new List<Dictionary<string,string>>();var names=new List<string>();for(int i=0;i<r.Fields.Count;i++)names.Add(Convert.ToString(r.Fields[i].Name)??"");
   while(!r.EOF){var row=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);for(int i=0;i<names.Count;i++){object v=r.Fields[i].Value;row[names[i]]=v is null or DBNull?"":Convert.ToString(v)?.Trim()??"";}rows.Add(row);if(limit>0&&rows.Count>=limit)break;r.MoveNext();}
   if(rows.Count==0)rows.Add(names.ToDictionary(n=>n,_=>"\u0000",StringComparer.OrdinalIgnoreCase));return rows;}
  finally{Release(r);try{c.Close();}catch{}Release(c);}}
 public static List<WireRow> ReadRows(string path,DatabaseConfig d,List<string> notes){string sql=d.WireSql.Trim();if(sql.Length==0)throw new InvalidOperationException("database.wireSql is empty in the config, so the from/to rows cannot be read yet. "+DiscoverHint);
  var raw=Query(path,sql,d.Providers);bool empty=raw.Count==1&&raw[0].Values.All(v=>v=="\u0000");var columns=raw[0].Keys.ToList();if(empty)raw.Clear();
  var missing=d.Columns.All().Where(x=>x.Required&&!columns.Contains(x.Column,StringComparer.OrdinalIgnoreCase)).Select(x=>x.Role+" ("+x.Column+")").ToList();
  if(missing.Count>0)throw new InvalidOperationException("The query result has no column for "+string.Join(", ",missing)+". Columns returned: "+string.Join(", ",columns)+". Fix database.columns or database.wireSql in the config. "+DiscoverHint);
  var optional=d.Columns.All().Where(x=>!x.Required&&x.Column.Trim().Length>0&&!columns.Contains(x.Column,StringComparer.OrdinalIgnoreCase)).Select(x=>x.Column).ToList();if(optional.Count>0)notes.Add("Columns not in the query result (left blank): "+string.Join(", ",optional));
  notes.Add($"Project database: {path}  ({raw.Count} row(s) from database.wireSql, confidence {d.Confidence})");var list=new List<WireRow>();int n=0;foreach(var rec in raw)list.Add(IdentifierService.MapRow(k=>rec.TryGetValue(k,out var v)?v:"",d.Columns,++n));return list;}
 public static Dictionary<int,string> ReadDrawings(string path,DatabaseConfig d,List<string> notes){var map=new Dictionary<int,string>();string sql=d.DrawingSql.Trim();if(sql.Length==0)return map;
  var raw=Query(path,sql,d.Providers);if(raw.Count==1&&raw[0].Values.All(v=>v=="\u0000"))return map;foreach(var rec in raw){var vals=rec.Values.ToList();if(vals.Count<2)continue;if(int.TryParse(vals[0],out int ix)&&vals[1].Length>0)map[ix]=vals[1];}
  notes.Add($"Drawing index map: {map.Count} row(s) from database.drawingSql (first column index, second column file name)");return map;}
 public static string SuggestSql(SchemaTable t)=>"SELECT * FROM ["+t.Name+"]";
 public static string Describe(List<SchemaTable> schema,DatabaseConfig d){var x=new List<string>{$"{schema.Count} table(s). Candidate columns: {string.Join(", ",d.CandidateColumns)}",""};
  foreach(var t in schema.OrderByDescending(t=>t.Score).ThenBy(t=>t.Name,StringComparer.OrdinalIgnoreCase)){x.Add((t.Score>=3?"CANDIDATE ":"          ")+t.Name+(t.Score>0?$"  ({t.Score} of {d.CandidateColumns.Count} candidate columns)":""));x.Add("            "+string.Join(", ",t.Columns));}
  return string.Join(Environment.NewLine,x);}
 static void Release(object? o){if(o!=null&&Marshal.IsComObject(o))try{Marshal.FinalReleaseComObject(o);}catch{}}
}
