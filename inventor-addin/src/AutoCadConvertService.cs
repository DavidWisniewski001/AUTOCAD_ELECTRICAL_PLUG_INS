using System.Runtime.InteropServices;
namespace LSE.InventorFootprintTools;
public static class AutoCadConvertService{
 [DllImport("ole32.dll",PreserveSig=false)]static extern void CLSIDFromProgID([MarshalAs(UnmanagedType.LPWStr)]string progId,out Guid clsid);
 [DllImport("oleaut32.dll",PreserveSig=false)]static extern void GetActiveObject(ref Guid clsid,IntPtr reserved,[MarshalAs(UnmanagedType.IUnknown)]out object o);
 public static bool IsInstalled()=>Type.GetTypeFromProgID("AutoCAD.Application") is not null;
 public static void DxfToDwg(string dxf,string dwg){dynamic? app=null,doc=null;bool started=false;try{try{CLSIDFromProgID("AutoCAD.Application",out Guid id);GetActiveObject(ref id,IntPtr.Zero,out object o);app=o;}catch{Type t=Type.GetTypeFromProgID("AutoCAD.Application")??throw new InvalidOperationException("AutoCAD Electrical is not installed on this workstation, so the DXF could not be saved as DWG. Turn off 'Convert DXF to DWG with AutoCAD' in Settings, or run the wizard on a workstation that has AutoCAD Electrical.");app=Activator.CreateInstance(t)!;started=true;}
  object? ready=null;for(int i=0;i<60&&ready is null;i++){try{ready=app!.Documents.Count;}catch{Thread.Sleep(1000);}}if(ready is null)throw new InvalidOperationException("AutoCAD did not become ready within 60 seconds.");
  doc=app!.Documents.Open(dxf,false);try{doc.SaveAs(dwg,64);}catch(COMException){doc.SaveAs(dwg);}doc.Close(false);Ace.Release(doc);doc=null;if(!File.Exists(dwg))throw new InvalidOperationException("AutoCAD reported success but the DWG was not found: "+dwg);}
  finally{Ace.Release(doc);if(started)try{app?.Quit();}catch{}Ace.Release(app);}}
}
