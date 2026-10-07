using System.Windows;
using System.Windows.Controls;
namespace Vista.Desktop;
public partial class MissionPlannerView : UserControl
{
 public MissionPlannerView()=>InitializeComponent();
 private void RegenerateClicked(object sender,RoutedEventArgs e){if(DataContext is MainViewModel vm&&vm.GenerateMissionCommand.CanExecute(null)&&MessageBox.Show("Replace all current task locations and loads? Your saved mission is unchanged until you save.","Regenerate mission",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes)vm.GenerateMissionCommand.Execute(null);}
 private void FocusDetailsClicked(object sender,RoutedEventArgs e){MissionName.BringIntoView();MissionName.Focus();}
}
