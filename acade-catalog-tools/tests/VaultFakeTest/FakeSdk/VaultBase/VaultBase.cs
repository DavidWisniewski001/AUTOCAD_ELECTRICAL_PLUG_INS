namespace Connectivity.Application.VaultBase{
 public sealed class ConnectionManager{ public static ConnectionManager Instance{get;}=new(); public Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections.Connection? Connection{get;set;} }
}
