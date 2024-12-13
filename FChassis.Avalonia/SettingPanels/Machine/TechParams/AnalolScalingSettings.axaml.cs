using FChassis.Data.Model.Settings.Machine.TechParams;
using FChassis.Data.ViewModel;

using Avalonia.Markup.Xaml;

namespace FChassis.Avalonia.Settings.Machine.TechParams;
public partial class AnalogScalingSettings : Panel {
   public AnalogScalingSettings () {
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof (AnalogScaling), Configuration.analogScalingVM);
   }
}