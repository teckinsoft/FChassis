using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace FChassis.VisibilityConverters;
public class DbgToVisibilityConverter : IValueConverter {
   public object Convert (object value, Type targetType, object parameter, CultureInfo culture) {
      return (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
   }

   public object ConvertBack (object value, Type targetType, object parameter, CultureInfo culture) {
      return value is Visibility visibility && visibility == Visibility.Visible;
   }
}

