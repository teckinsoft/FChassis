using FChassis.Data.Model.Settings.Laser.LaserCutting.Piercing;
using FChassis.Data.ViewModel;

using Avalonia.Markup.Xaml;

namespace FChassis.Avalonia.Settings.Laser.LaserCutting.Piercing;
public partial class DotPunchSettings : Panel {
   public DotPunchSettings () {
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof(DotPunch), Configuration.dotpunchVM);
   }
}