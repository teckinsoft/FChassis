using FChassis.Data.ViewModel;
using FChassis.Data.Model.Settings.Machine.AxisParams;

using Avalonia.Markup.Xaml;

namespace FChassis.Avalonia.Settings.Machine.AxisParams;
public partial class YSettings : Panel {
   public YSettings () {
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof (Axis), Configuration.yAxisVM);
   }
}