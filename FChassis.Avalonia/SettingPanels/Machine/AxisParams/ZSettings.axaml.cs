using FChassis.Data.ViewModel;
using FChassis.Data.Model.Settings.Machine.AxisParams;

using Avalonia.Markup.Xaml;

namespace FChassis.Avalonia.Settings.Machine.AxisParams;
public partial class ZSettings : Panel {
   public ZSettings () {
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof (Axis), Configuration.zAxisVM);
   }
}