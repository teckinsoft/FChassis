using FChassis.Data.Model.Settings.Laser.LaserCutting.Piercing;
using FChassis.Data.ViewModel;

using Avalonia.Markup.Xaml;

namespace FChassis.UI.Settings.Laser.LaserCutting.Piercing;
public partial class PeckSettings : Panel {

   public PeckSettings () {
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof(Peck), Configuration.peckVM);
   }
}
