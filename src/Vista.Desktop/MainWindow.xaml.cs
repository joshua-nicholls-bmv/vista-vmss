using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace Vista.Desktop;
public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }
    private SimulatorConnection? simulator;
    private BriefingWindow? briefingWindow;
    private Window? profileWindow;
    private SharedMissionsWindow? sharedWindow;
    private bool plannerPromptOpen,allowClosing;
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent(); ViewModel = viewModel; DataContext = viewModel;
        viewModel.BriefingRequested+=OpenBriefing;
        viewModel.PlannerLeaveRequested+=ConfirmPlannerLeave;
        viewModel.SharedMissionsRequested+=()=>{if(sharedWindow is not null){sharedWindow.Activate();return;}sharedWindow=new SharedMissionsWindow{Owner=this,DataContext=viewModel};sharedWindow.Closed+=(_,_)=>sharedWindow=null;sharedWindow.Show();};
        Closing+=(_,e)=>{if(!allowClosing&&viewModel.IsSignedIn&&viewModel.PlannerDirty&&!viewModel.SuppressPlannerPrompts){e.Cancel=true;ConfirmPlannerLeave(()=>{allowClosing=true;Close();});}};
        viewModel.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(MainViewModel.IsSignedIn)&&!viewModel.IsSignedIn){briefingWindow?.Close();profileWindow?.Close();sharedWindow?.Close();}};
        SourceInitialized += (_, _) =>
        {
            simulator = new SimulatorConnection();
            simulator.Attach(new WindowInteropHelper(this).Handle); ViewModel.AttachSimulator(simulator);
        };
        Closed += (_, _) => { viewModel.BriefingRequested-=OpenBriefing;ViewModel.SaveTrackingOnClose(); simulator?.Dispose(); };
    }
    private async void ConfirmPlannerLeave(Action continuation)
    {
        if(plannerPromptOpen)return;plannerPromptOpen=true;
        try
        {
            var choice="keep";var dialog=new Window{Title="Unsaved mission changes",Owner=this,Width=510,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Background,Foreground=Foreground};
            var panel=new StackPanel{Margin=new Thickness(24)};
            panel.Children.Add(new TextBlock{Text="Save your mission before leaving?",FontSize=21,FontWeight=FontWeights.SemiBold});
            panel.Children.Add(new TextBlock{Text="Save draft validates and saves the plan. If required details are missing, you will stay in the planner.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,16)});
            var buttons=new WrapPanel();foreach(var label in new[]{"Save draft","Discard","Keep editing"}){var b=new Button{Content=label};b.Click+=(_,_)=>{choice=label;dialog.DialogResult=true;};buttons.Children.Add(b);}panel.Children.Add(buttons);dialog.Content=panel;dialog.ShowDialog();
            if(choice=="Save draft"){if(!await ViewModel.SaveBeforeLeavingAsync())return;}
            else if(choice=="Discard")ViewModel.DiscardPlannerChanges();else return;
            continuation();
        }
        finally{plannerPromptOpen=false;}
    }
    private void PublishMissionClicked(object sender,RoutedEventArgs e){if(ViewModel.PublishMissionCommand.CanExecute(null)&&MessageBox.Show("Share this saved plan with all active VISTA pilots? They can import an independent copy, labelled with its squadron.","Share mission",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes)ViewModel.PublishMissionCommand.Execute(null);}
    private async void SignInClicked(object sender, RoutedEventArgs e)
    {
        var password = LoginPassword.Password; LoginPassword.Clear();
        await ViewModel.SignInAsync(password);
    }
    private async void PasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || ViewModel.IsBusy) return;
        e.Handled = true;
        var password = LoginPassword.Password; LoginPassword.Clear();
        await ViewModel.SignInAsync(password);
    }
    private void SaveClicked(object sender, RoutedEventArgs e)
    {
        if (!RouteGrid.CommitEdit(DataGridEditingUnit.Cell, true) || !RouteGrid.CommitEdit(DataGridEditingUnit.Row, true))
        { MessageBox.Show("Finish editing the route and correct any invalid values before saving.", "VISTA"); return; }
        if (ViewModel.SaveCommand.CanExecute(null)) ViewModel.SaveCommand.Execute(null);
        else MessageBox.Show("Add at least one route point before saving.", "VISTA");
    }
    private void UseMissionClicked(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.UseMissionCommand.CanExecute(null)) return;
        if (ViewModel.Waypoints.Count > 0 && MessageBox.Show("Replace the current route points with this mission route?", "VISTA", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        ViewModel.UseMissionCommand.Execute(null);
    }
    private void DeleteDraftClicked(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.DeleteDraftCommand.CanExecute(null)) return;
        if (MessageBox.Show($"Delete the saved mission \"{ViewModel.SelectedSortie?.Title}\" permanently? This removes the saved plan and its route points.", "VISTA", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            ViewModel.DeleteDraftCommand.Execute(null);
    }
    private void CancelActiveClicked(object sender,RoutedEventArgs e)
    {
        if(!ViewModel.CancelActiveMissionCommand.CanExecute(null)) return;
        if(MessageBox.Show("Cancel the active mission and stop tracking? Its recorded history is retained.","VISTA",MessageBoxButton.YesNo)==MessageBoxResult.Yes) ViewModel.CancelActiveMissionCommand.Execute(null);
    }
    private void OpenBriefing()
    {
        if(briefingWindow is not null){briefingWindow.Activate();return;}
        briefingWindow=new BriefingWindow(ViewModel){Owner=this};
        briefingWindow.Closed+=async (_,_)=>{briefingWindow=null;await ViewModel.BriefingClosedAsync();};
        briefingWindow.Show();
    }
    private void ProfileClicked(object sender,RoutedEventArgs e)
    {
        if(profileWindow is not null){profileWindow.Activate();return;}
        profileWindow=new Window{Title="VISTA · Pilot profile",Width=800,Height=740,MinWidth=600,MinHeight=500,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,DataContext=ViewModel,Content=new PilotSettingsView()};
        profileWindow.Closed+=(_,_)=>profileWindow=null;profileWindow.Show();
    }

}
