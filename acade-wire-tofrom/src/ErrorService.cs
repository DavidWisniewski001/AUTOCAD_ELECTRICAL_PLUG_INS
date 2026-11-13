using System.Windows.Forms;
namespace LSE.AutoCADElectrical.WireToFrom;
public static class ErrorService{
 public static string Log(string step,Exception ex){try{Directory.CreateDirectory(SettingsService.LogFolder);string p=Path.Combine(SettingsService.LogFolder,$"Error_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
  File.WriteAllLines(p,new[]{"Step: "+step,"Build: "+BuildInfo.Label,"Time: "+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),"User: "+Environment.UserName,"Machine: "+Environment.MachineName,"",ex.ToString()});return p;}catch{return "";}}
 public static void Show(IWin32Window? owner,string step,Exception ex,string? advice=null){string path=Log(step,ex);string where=path.Length>0?path:"(the log file could not be written)";
  bool plain=ex is InvalidOperationException||ex is FileNotFoundException;
  string todo=advice??(plain?"Fix what is described above, then try again. Log: "+where:"Try again. If it happens again, send the log file "+where+" with the project name.");
  MessageBox.Show(owner,"What happened: "+ex.Message+Environment.NewLine+Environment.NewLine+"Where: "+step+"  ("+BuildInfo.Label+")"+Environment.NewLine+Environment.NewLine+"What to do: "+todo,"LSE_ToFrom: "+step,MessageBoxButtons.OK,MessageBoxIcon.Warning);}
}
