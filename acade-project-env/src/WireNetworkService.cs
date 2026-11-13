using System.Text.RegularExpressions;
namespace LSE.AutoCADElectrical.ProjectEnvironment;
public static class WireNetworkService {
 static readonly StringComparer Ci=StringComparer.OrdinalIgnoreCase;const StringComparison Cs=StringComparison.OrdinalIgnoreCase;
 public static bool Wc(string name,string pattern){pattern=pattern.Trim();if(pattern.Length==0)return false;return Regex.IsMatch(name,"^"+Regex.Escape(pattern).Replace("\\*",".*").Replace("\\?",".")+"$",RegexOptions.IgnoreCase);}
 public static bool MatchesAny(string name,IEnumerable<string> patterns)=>patterns.Any(p=>Wc(name,p));
 public static double PointToSegment(Pt p,WireSegment s){double dx=s.B.X-s.A.X,dy=s.B.Y-s.A.Y;double len2=dx*dx+dy*dy;if(len2<1e-18)return p.Dist(s.A);double t=((p.X-s.A.X)*dx+(p.Y-s.A.Y)*dy)/len2;t=Math.Clamp(t,0,1);return p.Dist(new Pt(s.A.X+t*dx,s.A.Y+t*dy));}
 public static double Distance(Pt p,IEnumerable<WireSegment> segs){double best=double.MaxValue;foreach(var s in segs){double d=PointToSegment(p,s);if(d<best)best=d;}return best;}
 static bool Collinear(WireSegment a,WireSegment b,double tol){double ax=a.B.X-a.A.X,ay=a.B.Y-a.A.Y,bx=b.B.X-b.A.X,by=b.B.Y-b.A.Y;double la=Math.Sqrt(ax*ax+ay*ay),lb=Math.Sqrt(bx*bx+by*by);if(la<1e-12||lb<1e-12)return false;
  double cross=Math.Abs(ax*by-ay*bx)/(la*lb);if(cross>0.01)return false;double off=Math.Abs((b.A.X-a.A.X)*ay-(b.A.Y-a.A.Y)*ax)/la;return off<=tol;}
 static bool Touch(WireSegment a,WireSegment b,FromToRules r){double t=r.JoinTolerance;
  if(a.A.Dist(b.A)<=t||a.A.Dist(b.B)<=t||a.B.Dist(b.A)<=t||a.B.Dist(b.B)<=t)return true;
  if(PointToSegment(a.A,b)<=t||PointToSegment(a.B,b)<=t||PointToSegment(b.A,a)<=t||PointToSegment(b.B,a)<=t)return true;
  if(r.GapTolerance>t&&Collinear(a,b,Math.Max(t,0.005))){double g=Math.Min(Math.Min(a.A.Dist(b.A),a.A.Dist(b.B)),Math.Min(a.B.Dist(b.A),a.B.Dist(b.B)));if(g<=r.GapTolerance)return true;}return false;}
 static int Find(int[] parent,int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
 static void Union(int[] parent,int a,int b){a=Find(parent,a);b=Find(parent,b);if(a!=b)parent[Math.Max(a,b)]=Math.Min(a,b);}
 public static List<WireNetwork> Build(DrawingWires d,FromToRules r){int n=d.Segments.Count;var parent=Enumerable.Range(0,n).ToArray();
  for(int i=0;i<n;i++)for(int j=i+1;j<n;j++)if(Find(parent,i)!=Find(parent,j)&&Touch(d.Segments[i],d.Segments[j],r))Union(parent,i,j);
  var groups=new Dictionary<int,WireNetwork>();var list=new List<WireNetwork>();for(int i=0;i<n;i++){int root=Find(parent,i);if(!groups.TryGetValue(root,out var net)){net=new WireNetwork{Id=list.Count+1,Drawing=d.Path};groups[root]=net;list.Add(net);}net.Segments.Add(d.Segments[i]);}
  foreach(var t in d.Terminals){var net=Nearest(list,t.Point,r.ConnectTolerance);if(net is null)continue;if(!net.Devices.Any(x=>x.Tag.Equals(t.Tag,Cs)&&x.Pin.Equals(t.Pin,Cs)))net.Devices.Add(t);}
  foreach(var s in d.Signals){var net=Nearest(list,s.Point,r.ConnectTolerance);if(net is null||s.Code.Trim().Length==0)continue;if(!net.SignalCodes.Contains(s.Code.Trim(),Ci))net.SignalCodes.Add(s.Code.Trim());}
  foreach(var m in d.Marks){var net=Nearest(list,m.Point,r.MarkTolerance);if(net is not null)net.Marks.Add(m);}
  foreach(var net in list){var longest=net.Segments.OrderByDescending(s=>s.Horizontal?1:0).ThenByDescending(s=>s.Length).First();net.LabelPoint=Pt.Mid(longest.A,longest.B);net.LabelRotation=longest.Horizontal?0:90;}
  return list;}
 static WireNetwork? Nearest(List<WireNetwork> nets,Pt p,double tol){WireNetwork? best=null;double bd=double.MaxValue;foreach(var n in nets){double d=Distance(p,n.Segments);if(d<bd){bd=d;best=n;}}return bd<=tol?best:null;}
 public static int NaturalCompare(string a,string b){int i=0,j=0;while(i<a.Length&&j<b.Length){if(char.IsDigit(a[i])&&char.IsDigit(b[j])){int si=i,sj=j;while(i<a.Length&&char.IsDigit(a[i]))i++;while(j<b.Length&&char.IsDigit(b[j]))j++;string na=a[si..i].TrimStart('0'),nb=b[sj..j].TrimStart('0');int c=na.Length!=nb.Length?na.Length.CompareTo(nb.Length):string.CompareOrdinal(na,nb);if(c!=0)return c;}
   else{int c=char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));if(c!=0)return c;i++;j++;}}return (a.Length-i).CompareTo(b.Length-j);}
 public static int Rank(DeviceTerminal t,FromToSettings f){for(int i=0;i<f.DevicePriority.Count;i++){try{if(Regex.IsMatch(t.Tag,f.DevicePriority[i],RegexOptions.IgnoreCase))return i;}catch{}}return f.DevicePriority.Count+(f.TerminalsLast&&t.IsTerminalStrip?1:0);}
 public static List<DeviceTerminal> Ordered(IEnumerable<DeviceTerminal> devices,FromToSettings f){var list=devices.ToList();list.Sort((x,y)=>{int c=Rank(x,f).CompareTo(Rank(y,f));if(c!=0)return c;c=Math.Round(x.Point.X,3).CompareTo(Math.Round(y.Point.X,3));if(c!=0)return c;c=Math.Round(y.Point.Y,3).CompareTo(Math.Round(x.Point.Y,3));if(c!=0)return c;c=NaturalCompare(x.Tag,y.Tag);return c!=0?c:NaturalCompare(x.Pin,y.Pin);});return list;}
 public static string Compose(List<DeviceTerminal> ordered,FromToSettings f,string sheet,string drawing){if(ordered.Count==0)return "";var from=ordered[0];var to=ordered[^1];string text;
  if(ordered.Count==1)text=from.Display(f);
  else if(f.MultiDevice.Equals("all",Cs)&&ordered.Count>2)text=string.Join(f.AllSeparator,ordered.Select(d=>d.Display(f)));
  else text=f.Format.Replace("{FROM}",from.Display(f),StringComparison.OrdinalIgnoreCase).Replace("{TO}",ordered.Count>1?to.Display(f):"",StringComparison.OrdinalIgnoreCase).Replace("{FROMPIN}",from.Pin,StringComparison.OrdinalIgnoreCase).Replace("{TOPIN}",ordered.Count>1?to.Pin:"",StringComparison.OrdinalIgnoreCase);
  text=text.Replace("{SHEET}",sheet,StringComparison.OrdinalIgnoreCase).Replace("{DRAWING}",drawing,StringComparison.OrdinalIgnoreCase).Trim();if(f.Uppercase)text=text.ToUpperInvariant();return text;}
 static string Fit(string text,FromToSettings f)=>f.MaxLength>0&&text.Length>f.MaxLength?text[..f.MaxLength]:text;
 public static void Assign(List<WirePlan> plans,FromToSettings f,FromToRules r){var all=plans.SelectMany(p=>p.Networks).ToList();int n=all.Count;var parent=Enumerable.Range(0,n).ToArray();var byCode=new Dictionary<string,int>(Ci);
  for(int i=0;i<n;i++)foreach(string code in all[i].SignalCodes){if(byCode.TryGetValue(code,out int first))Union(parent,first,i);else byCode[code]=i;}
  var groups=new Dictionary<int,List<WireNetwork>>();var order=new List<int>();for(int i=0;i<n;i++){int root=Find(parent,i);if(!groups.TryGetValue(root,out var g)){g=new();groups[root]=g;order.Add(root);}g.Add(all[i]);all[i].Logical=root+1;}
  var used=new Dictionary<string,int>(Ci);var sheets=plans.ToDictionary(p=>p.Drawing,p=>p.Sheet,Ci);var info=new List<(List<WireNetwork> members,List<DeviceTerminal> devices,bool single,string number)>();
  foreach(int root in order){var members=groups[root];var first=members[0];var devices=Ordered(members.SelectMany(m=>m.Devices).GroupBy(d=>d.Tag+"\u0001"+d.Pin,Ci).Select(g=>g.First()),f);
   bool single=devices.Count==1&&f.SingleDevice.Equals("device",Cs);string number=devices.Count>=2||single?Fit(Compose(devices,f,sheets.TryGetValue(first.Drawing,out var s)?s:"",Path.GetFileNameWithoutExtension(first.Drawing)),f):"";info.Add((members,devices,single,number));}
  bool Claims((List<WireNetwork> members,List<DeviceTerminal> devices,bool single,string number) g)=>g.number.Length>0&&g.members.Any(m=>m.Marks.Any(k=>k.Number.Trim().Equals(g.number,Cs)));
  foreach(var g in info.Where(Claims).Concat(info.Where(x=>!Claims(x)))){var members=g.members;var devices=g.devices;bool single=g.single;string number=g.number;
   if(number.Length>0){used.TryGetValue(number,out int count);used[number]=count+1;if(count>0)number=Fit(number+f.DuplicateSuffix.Replace("{N}",(count+1).ToString(),StringComparison.OrdinalIgnoreCase),f);}
   foreach(var net in members){
    if(devices.Count==0){Policy(net,f.NoDevice,"no tagged device on this network");}
    else if(devices.Count==1&&!single){Policy(net,f.SingleDevice,"only one tagged device ("+devices[0].Tag+")");}
    else{Decide(net,number,f,r);if(single)Add(net,"only one tagged device ("+devices[0].Tag+"); numbered with it alone");if(devices.Count>2&&!f.MultiDevice.Equals("all",Cs))Add(net,devices.Count+" devices, first and last used ("+string.Join(", ",devices.Select(d=>d.Tag))+")");}
    if(members.Count>1)Add(net,"continues on "+(members.Count-1)+" other network(s) via "+string.Join(",",net.SignalCodes));}}
  foreach(var p in plans)p.Fixed=f.Fixed;}
 static void Add(WireNetwork net,string note)=>net.Note=net.Note.Length>0?net.Note+"; "+note:note;
 static void Policy(WireNetwork net,string policy,string why){if(policy.Equals("skip",Cs)){net.Action=WireAction.Skip;Add(net,why+"; skipped");}else{net.Action=WireAction.Keep;Add(net,why+"; existing number kept");}}
 static void Decide(WireNetwork net,string number,FromToSettings f,FromToRules r){net.Proposed=number;if(number.Length==0){net.Action=WireAction.Keep;Add(net,"empty number; existing kept");return;}
  if(net.Marks.Count==0){if(r.InsertMissing)net.Action=WireAction.Insert;else{net.Action=WireAction.Skip;Add(net,"no wire number block on this network and insertMissing is off");}return;}
  if(net.Marks.All(m=>m.Number.Trim().Equals(number,Cs))){net.Action=WireAction.Keep;return;}
  if(!f.OverwriteFixed&&net.Marks.Any(m=>m.IsFixed)){net.Action=WireAction.Skip;Add(net,"already has a fixed wire number ("+net.Existing+") and overwriteFixed is off");return;}
  net.Action=WireAction.Set;}
 public static List<string> Verify(WirePlan plan,DrawingWires after,FromToRules r){var issues=new List<string>();var nets=Build(after,r);
  foreach(var net in plan.Networks.Where(x=>x.Action is WireAction.Set or WireAction.Insert)){var found=Nearest(nets,net.LabelPoint,Math.Max(r.MarkTolerance,0.5));if(found is null){issues.Add("network "+net.Id+" ("+net.DeviceText+") not found after the write");continue;}
   if(!found.Marks.Any(m=>m.Number.Trim().Equals(net.Proposed,StringComparison.OrdinalIgnoreCase)))issues.Add("network "+net.Id+" ("+net.DeviceText+") reads \""+found.Existing+"\" instead of \""+net.Proposed+"\"");}return issues;}
 public static string Render(WirePlan p){var x=new List<string>{"  "+Path.GetFileName(p.Drawing)+": "+p.Summary};x.AddRange(p.Notes.Select(n=>"    note: "+n));
  foreach(var net in p.Networks.OrderBy(n=>n.Action==WireAction.Keep?1:0).ThenBy(n=>n.Id))x.Add($"    #{net.Id} {net.Action,-6} {(net.Existing.Length>0?"\""+net.Existing+"\"":"(none)")} -> \"{net.Proposed}\"  devices: {net.DeviceText}{(net.Note.Length>0?"  | "+net.Note:"")}");return string.Join(Environment.NewLine,x);}
}
