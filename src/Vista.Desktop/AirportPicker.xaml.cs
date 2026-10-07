using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Vista.Core;
namespace Vista.Desktop;
public partial class AirportPicker:UserControl
{
    public static readonly DependencyProperty TextProperty=DependencyProperty.Register(nameof(Text),typeof(string),typeof(AirportPicker),new FrameworkPropertyMetadata("",FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,TextUpdated));
    public static readonly DependencyProperty AirportsProperty=DependencyProperty.Register(nameof(Airports),typeof(IEnumerable),typeof(AirportPicker),new PropertyMetadata(null));
    public string Text {get=>(string)GetValue(TextProperty);set=>SetValue(TextProperty,value);}
    public IEnumerable? Airports {get=>(IEnumerable?)GetValue(AirportsProperty);set=>SetValue(AirportsProperty,value);}
    private bool updating;
    public AirportPicker(){InitializeComponent();}
    private static void TextUpdated(DependencyObject d,DependencyPropertyChangedEventArgs e){var picker=(AirportPicker)d;if(picker.Input is null||picker.Input.Text==(string)e.NewValue)return;picker.updating=true;picker.Input.Text=(string)e.NewValue;picker.Input.CaretIndex=picker.Input.Text.Length;picker.updating=false;}
    private void InputChanged(object sender,TextChangedEventArgs e){if(updating||Results is null)return;SetCurrentValue(TextProperty,Input.Text);ShowMatches();}
    private void ShowMatches(){var matches=AirportSearch.Find(Airports?.Cast<Airfield>()??[],Input.Text);Results.ItemsSource=matches;Results.SelectedIndex=matches.Count>0?0:-1;Suggestions.IsOpen=Input.IsKeyboardFocused&&matches.Count>0&&!matches.Any(a=>a.Icao.Equals(Input.Text.Trim(),StringComparison.OrdinalIgnoreCase));}
    private void Commit(){if(Results.SelectedItem is not Airfield airfield)return;updating=true;SetCurrentValue(TextProperty,airfield.Icao);Input.Text=airfield.Icao;Input.CaretIndex=4;updating=false;Suggestions.IsOpen=false;Input.Focus();}
    private void InputFocused(object sender,KeyboardFocusChangedEventArgs e)=>ShowMatches();
    private void InputBlurred(object sender,KeyboardFocusChangedEventArgs e){Dispatcher.BeginInvoke(()=>{if(!IsKeyboardFocusWithin&&!Results.IsKeyboardFocusWithin)Suggestions.IsOpen=false;});}
    private void InputKey(object sender,KeyEventArgs e){if(e.Key==Key.Escape){Suggestions.IsOpen=false;e.Handled=true;}else if(Suggestions.IsOpen&&e.Key==Key.Enter){Commit();e.Handled=true;}else if(Suggestions.IsOpen&&e.Key is Key.Down or Key.Up){Results.SelectedIndex=Math.Clamp(Results.SelectedIndex+(e.Key==Key.Down?1:-1),0,Results.Items.Count-1);Results.ScrollIntoView(Results.SelectedItem);e.Handled=true;}}
    private void ResultClicked(object sender,MouseButtonEventArgs e)=>Commit();
    private void ResultKey(object sender,KeyEventArgs e){if(e.Key==Key.Enter){Commit();e.Handled=true;}else if(e.Key==Key.Escape){Suggestions.IsOpen=false;Input.Focus();}}
}
