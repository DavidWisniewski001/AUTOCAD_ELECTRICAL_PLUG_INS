using System.Text.RegularExpressions;
namespace LSE.AutoCADElectrical.ProjectDoctor;
public static class WireLayerService {
 static readonly StringComparer Ci=StringComparer.OrdinalIgnoreCase;
 public static List<string> Split(string? v)=>(v??"").Split(new[]{',',';'},StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim()).Where(x=>x.Length>0).Distinct(Ci).ToList();
 public static bool HasWildcard(string p)=>p.IndexOfAny(new[]{'*','?'})>=0;
 public static bool Wc(string name,string pattern){pattern=pattern.Trim();if(pattern.Length==0)return false;return Regex.IsMatch(name,"^"+Regex.Escape(pattern).Replace("\\*",".*").Replace("\\?",".")+"$",RegexOptions.IgnoreCase);}
 public static bool MatchesAny(string name,IEnumerable<string> patterns)=>patterns.Any(p=>Wc(name,p));
 public static bool IsList(DoctorRules r,string tag)=>r.WireLayerListAttributes.Any(t=>t.Equals(tag,StringComparison.OrdinalIgnoreCase));
 public static bool SameList(string? a,string? b){var x=Split(a);var y=Split(b);return x.Count==y.Count&&x.All(i=>y.Contains(i,Ci));}
 public static bool SameValue(DoctorRules r,string tag,string? a,string? b)=>IsList(r,tag)?SameList(a,b):(a??"").Trim().Equals((b??"").Trim(),StringComparison.OrdinalIgnoreCase);
 public static bool HasWireXdata(LayerInfo l,DoctorRules r)=>l.XdataApps.Any(a=>MatchesAny(a,r.WireLayerXdataApps));
 public static List<string> ListPatterns(DrawingInspection d,DoctorRules r)=>r.WireLayerAttributes.Where(t=>IsList(r,t)).SelectMany(t=>Split(d.Wdm(t))).ToList();
 public static HashSet<string> SingleNames(DrawingInspection d,DoctorRules r)=>new(r.WireLayerAttributes.Where(t=>!IsList(r,t)).Select(t=>d.Wdm(t).Trim()).Where(v=>v.Length>0&&!HasWildcard(v)),Ci);
 public static List<string> WireLayers(DrawingInspection d,DoctorRules r){var patterns=ListPatterns(d,r);var singles=SingleNames(d,r);return d.Layers.Where(l=>MatchesAny(l.Name,patterns)||singles.Contains(l.Name)||HasWireXdata(l,r)).Select(l=>l.Name).ToList();}
 public static bool IsStandard(string name,DrawingInspection tpl,DoctorRules r,HashSet<string> templateWireLayers)=>templateWireLayers.Contains(name)||MatchesAny(name,ListPatterns(tpl,r))||SingleNames(tpl,r).Contains(name);
 public static List<string> Differences(LayerInfo drawing,LayerInfo template,DoctorRules r){var x=new List<string>();
  if(!drawing.Color.Equals(template.Color,StringComparison.OrdinalIgnoreCase))x.Add($"color {drawing.Color} (template {template.Color})");if(!drawing.Linetype.Equals(template.Linetype,StringComparison.OrdinalIgnoreCase))x.Add($"linetype {drawing.Linetype} (template {template.Linetype})");
  if(drawing.LineWeight!=template.LineWeight)x.Add($"lineweight {drawing.LineWeight} (template {template.LineWeight})");if(drawing.IsPlottable!=template.IsPlottable)x.Add(drawing.IsPlottable?"plottable (template: no plot)":"no plot (template: plottable)");
  if(HasWireXdata(template,r)&&!HasWireXdata(drawing,r))x.Add("no AutoCAD Electrical wire-type data on the layer");return x;}
 public static bool SameLayer(LayerInfo drawing,LayerInfo template,DoctorRules r)=>Differences(drawing,template,r).Count==0;
 public static bool Resolved(Finding f,DrawingInspection after,DrawingInspection? tpl,DoctorRules r){if(after.Error.Length>0||after.OpenInEditor)return false;
  switch(f.Action){case RepairAction.InsertWdm:return after.HasWdm;case RepairAction.SetAttribute:return SameValue(r,f.Name,after.Wdm(f.Name),f.Value);
   case RepairAction.CreateLayer:case RepairAction.RepairLayer:{var d=after.Layer(f.Name);var t=tpl?.Layer(f.Name);return d is not null&&(t is null||SameLayer(d,t,r));}
   case RepairAction.PurgeLayer:return after.Layer(f.Name) is null;default:return true;}}
}
