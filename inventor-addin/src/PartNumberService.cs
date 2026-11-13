using System.Text.RegularExpressions;
namespace LSE.InventorFootprintTools;
public static class PartNumberService {
 public static void Validate(NamingFormat f,PartNumberRequest r){if(!Regex.IsMatch(r.Category,$"^[0-9]{{{f.CategoryChars}}}$"))throw new InvalidOperationException($"Category code must be {f.CategoryChars} digits (for example 45).");if(!Regex.IsMatch(r.ManufacturerCode,$"^[A-Za-z]{{{f.ManufacturerChars}}}$"))throw new InvalidOperationException($"Manufacturer code must be {f.ManufacturerChars} letters (for example BUS).");if(!Regex.IsMatch(r.Type,$"^[A-Za-z]{{{f.TypeChars}}}$"))throw new InvalidOperationException($"Type code must be {f.TypeChars} letters (for example FH).");}
 public static Regex Pattern(NamingFormat f,string prefix)=>new("^"+Regex.Escape(prefix.ToUpperInvariant())+$"([0-9]{{{f.SequenceChars}}})$");
 public static Regex StandardForm(NamingFormat f)=>new($"^[0-9]{{{f.CategoryChars}}}[A-Z]{{{f.ManufacturerChars}}}[A-Z]{{{f.TypeChars}}}[0-9]{{{f.SequenceChars}}}$");
 public static string Normalize(string v)=>v.Trim().ToUpperInvariant();
 public static long Highest(NamingFormat f,string prefix,IEnumerable<string> existing){var p=Pattern(f,prefix);long h=0;foreach(string v in existing){var m=p.Match(Normalize(v));if(m.Success&&long.TryParse(m.Groups[1].Value,out long n)&&n>h)h=n;}return h;}
 public static string Next(NamingFormat f,string prefix,IReadOnlyCollection<string> existing){long n=Highest(f,prefix,existing)+1;long cap=(long)Math.Pow(10,f.SequenceChars)-1;if(n>cap)throw new InvalidOperationException($"Sequence exhausted for {prefix}: every number up to {cap} is used.");string pn=prefix.ToUpperInvariant()+n.ToString().PadLeft(f.SequenceChars,'0');if(existing.Any(x=>Normalize(x).Equals(pn,StringComparison.Ordinal)))throw new InvalidOperationException("Computed part number already exists: "+pn);return pn;}
}
