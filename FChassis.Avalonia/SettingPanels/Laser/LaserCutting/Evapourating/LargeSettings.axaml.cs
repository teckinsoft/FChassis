using FChassis.Data.Model.Settings.Laser.LaserCutting.Evapourating;
using FChassis.Data.ViewModel;

using Avalonia.Markup.Xaml;

namespace FChassis.Avalonia.Settings.Laser.LaserCutting.Evapourating;
public partial class LargeSettings : Panel {
   public LargeSettings () { 
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof (Large), Configuration.evapouratingLargeVM);
   }
}