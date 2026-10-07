using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
namespace Vista.Desktop;
public sealed class StatusBrushConverter:IValueConverter
{
 public object Convert(object value,Type targetType,object parameter,CultureInfo culture)
 {
  var key=(value as string) switch { "good"=>"StatusGood", "danger"=>"Action", "warning"=>"StatusWarning", "accent"=>"Accent", _=>"Muted" };
  return Application.Current.FindResource(key);
 }
 public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
}

