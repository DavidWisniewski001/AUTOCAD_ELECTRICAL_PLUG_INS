namespace LSE.AutoCADElectrical.CatalogTools;
public enum CatalogScope { CompanyStandards, PersonalFavorites, Recent, AllCatalog }
public sealed class CatalogRecord {
 public string TableName {get;set;}=""; public Dictionary<string,string> Values {get;set;}=new(StringComparer.OrdinalIgnoreCase);
 public string Catalog=>Get("CATALOG"); public string Manufacturer=>Get("MANUFACTURER"); public string Description=>Get("DESCRIPTION");
 public string Type=>Get("TYPE"); public string Rating=>Get("RATING"); public string User1=>Get("USER1"); public string TextValue=>Get("TEXTVALUE"); public string WdBlkNam=>Get("WDBLKNAM");
 public string Get(string n)=>Values.TryGetValue(n,out var v)?v:""; public string Key=>$"{TableName}|{Manufacturer}|{Catalog}".ToUpperInvariant();
}
public sealed class StandardEntry { public string RecordKey{get;set;}=""; public string Category{get;set;}="Uncategorized"; public string Status{get;set;}="Company Standard"; public string Notes{get;set;}=""; }
public sealed class AppSettings {
 public string CatalogPath{get;set;}=""; public string CompanyStandardsPath{get;set;}=""; public string VaultExplorerPath{get;set;}="C:\\Program Files\\Autodesk\\Vault Client 2026\\Explorer";
 public string VaultServer{get;set;}="lse-cep-sql"; public string VaultName{get;set;}="Lake_Shore_E_Vault"; public string CatalogVaultPath{get;set;}="";
 public string LsePartAttribute{get;set;}="USER1"; public List<string> Favorites{get;set;}=new(); public List<string> Recent{get;set;}=new();
}
public sealed class SelectedBlockInfo { public Autodesk.AutoCAD.DatabaseServices.ObjectId Id{get;set;}=Autodesk.AutoCAD.DatabaseServices.ObjectId.Null; public string BlockName{get;set;}=""; public Dictionary<string,string> Attributes{get;set;}=new(StringComparer.OrdinalIgnoreCase); public string Family=>Attributes.TryGetValue("FAMILY",out var v)?v:""; }
public sealed class AttributeChange { public string Attribute{get;set;}=""; public string Current{get;set;}=""; public string Proposed{get;set;}=""; public bool Preserved{get;set;} }
public sealed class CatalogAddRequest { public string TableName{get;set;}=""; public string Catalog{get;set;}=""; public string Manufacturer{get;set;}=""; public string Description{get;set;}=""; public string Type{get;set;}=""; public string Rating{get;set;}=""; public string User1{get;set;}=""; public string User2{get;set;}=""; public string User3{get;set;}=""; public string TextValue{get;set;}=""; public string WdBlkNam{get;set;}=""; }
