using System.Drawing;using System.Windows.Forms;
namespace LSE.AutoCADElectrical.CatalogTools;
public sealed class TextReportForm:Form{public TextReportForm(string title,string text){Text=title;StartPosition=FormStartPosition.CenterParent;Size=new Size(900,600);var t=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill,Font=new Font("Consolas",9.5f),Text=text};var copy=new Button{Text="Copy",Dock=DockStyle.Bottom,Height=32};copy.Click+=(_,_)=>Clipboard.SetText(t.Text);Controls.Add(t);Controls.Add(copy);}}
