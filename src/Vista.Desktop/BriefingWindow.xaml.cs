using System.Windows;
namespace Vista.Desktop;
public partial class BriefingWindow:Window
{
    private readonly MainViewModel viewModel;
    public BriefingWindow(MainViewModel model)
    {
        InitializeComponent();viewModel=model;DataContext=model;model.BriefingCloseRequested+=CloseSigned;
        Closed+=(_,_)=>{model.BriefingCloseRequested-=CloseSigned;model.AcknowledgementOpen=false;};
    }
    private void CloseSigned()=>Close();
    private void CloseClicked(object sender,RoutedEventArgs e)=>Close();

 private void ExportPdfClicked(object sender,System.Windows.RoutedEventArgs e)
 {
  if(DataContext is not MainViewModel vm)return;
  var dialog=new Microsoft.Win32.SaveFileDialog{Title="Export VISTA briefing",Filter="PDF briefing (*.pdf)|*.pdf",DefaultExt=".pdf",FileName="VISTA-briefing.pdf"};
  if(dialog.ShowDialog(this)!=true)return;
  try{vm.ExportBriefingPdf(dialog.FileName);}catch(Exception error)when(error is System.IO.IOException or UnauthorizedAccessException or InvalidOperationException){System.Windows.MessageBox.Show(error.Message,"Briefing PDF");}
 }
}
