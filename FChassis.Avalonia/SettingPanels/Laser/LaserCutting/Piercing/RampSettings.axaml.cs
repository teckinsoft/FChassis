using FChassis.Data.Model.Settings.Laser.LaserCutting.Piercing;
using FChassis.Data.ViewModel;

using Avalonia.Markup.Xaml;

namespace FChassis.Avalonia.Settings.Laser.LaserCutting.Piercing;
public partial class RampSettings : Panel {
   public RampSettings () {

      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof(Ramp), Configuration.rampVM);
   }
}