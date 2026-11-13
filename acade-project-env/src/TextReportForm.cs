using System.Drawing;using System.Windows.Forms;
namespace LSE.AutoCADElectrical.ProjectEnvironment;
public sealed class TextReportForm:Form{
 public TextReportForm(string title,string text){Text=title;StartPosition=FormStartPosition.CenterParent;Size=new Size(1100,720);MinimumSize=new Size(640,400);var p=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=2,Padding=new Padding(10)};p.ColumnStyles.Add(new(SizeType.Percent,100));p.ColumnStyles.Add(new(SizeType.Absolute,110));p.ColumnStyles.Add(new(SizeType.Absolute,100));p.RowStyles.Add(new(SizeType.Percent,100));p.RowStyles.Add(new(SizeType.Absolute,44));
  var box=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill,Font=new Font("Consolas",9f),Text=text};p.Controls.Add(box,0,0);p.SetColumnSpan(box,3);
  var save=new Button{Text="Save as...",Dock=DockStyle.Fill};save.Click+=(_,_)=>{using var d=new SaveFileDialog{Filter="Text (*.txt)|*.txt",FileName=title.Replace(' ','_')+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".txt"};if(d.ShowDialog(this)==DialogResult.OK){try{File.WriteAllText(d.FileName,text);}catch(Exception ex){MessageBox.Show(ex.Message,title);}}};
  var close=new Button{Text="Close",Dock=DockStyle.Fill};close.Click+=(_,_)=>Close();p.Controls.Add(save,1,1);p.Controls.Add(close,2,1);Controls.Add(p);CancelButton=close;Shown+=(_,_)=>{box.SelectionStart=0;box.SelectionLength=0;close.Focus();};}
}
