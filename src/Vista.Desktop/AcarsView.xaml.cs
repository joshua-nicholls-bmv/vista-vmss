using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
namespace Vista.Desktop;
public partial class AcarsView:UserControl
{
 public AcarsView()=>InitializeComponent();
 private void FitRouteClicked(object sender,RoutedEventArgs e)=>FlightMap.FitRoute();
 private void EnlargeMapClicked(object sender,RoutedEventArgs e)
 {
  var window=new Window{Title="VISTA · Mission map",Width=1150,Height=780,MinWidth=700,MinHeight=500,Owner=Window.GetWindow(this),DataContext=DataContext,Background=(Brush)FindResource("Background"),Foreground=(Brush)FindResource("Text"),WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var panel=new DockPanel{Margin=new Thickness(18)};var tools=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(tools,Dock.Top);panel.Children.Add(tools);
  var map=new TrackMap();map.SetBinding(TrackMap.RouteProperty,new Binding("ActiveRoute"));map.SetBinding(TrackMap.TrackProperty,new Binding("LocalTrackPoints"));map.SetBinding(TrackMap.DepartureProperty,new Binding("ActiveDeparture"));map.SetBinding(TrackMap.ArrivalProperty,new Binding("ActiveArrival"));map.SetBinding(TrackMap.NextPointIndexProperty,new Binding("CurrentMissionPointIndex"));
  var fit=new Button{Content="Fit full mission"};fit.Click+=(_,_)=>map.FitRoute();tools.Children.Add(fit);var follow=new CheckBox{Content="Follow aircraft",Foreground=window.Foreground,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(10,0,0,0)};follow.SetBinding(CheckBox.IsCheckedProperty,new Binding(nameof(TrackMap.FollowAircraft)){Source=map,Mode=BindingMode.TwoWay});tools.Children.Add(follow);
  var legend=new TextBlock{Text="Route · blue / Track · red / Passed · green / Next · gold / Airfields · labelled dots",Margin=new Thickness(0,12,0,12),Foreground=window.Foreground};DockPanel.SetDock(legend,Dock.Bottom);panel.Children.Add(legend);panel.Children.Add(map);window.Content=panel;window.Show();
 }
 private void CancelActiveClicked(object sender,RoutedEventArgs e)
 {
  if(DataContext is MainViewModel vm&&vm.CancelActiveMissionCommand.CanExecute(null)&&MessageBox.Show(vm.IsTracking?"Abort this flight? Recorded tracking is retained.":"Deactivate this mission? The saved plan remains ready to use.","VISTA",MessageBoxButton.YesNo)==MessageBoxResult.Yes)vm.CancelActiveMissionCommand.Execute(null);
 }
}
