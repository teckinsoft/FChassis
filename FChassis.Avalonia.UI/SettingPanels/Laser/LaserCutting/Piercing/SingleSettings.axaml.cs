using FChassis.Data.Model.Settings.Laser.LaserCutting.Piercing;
using FChassis.Data.ViewModel;

using Avalonia.Markup.Xaml;

namespace FChassis.UI.Settings.Laser.LaserCutting.Piercing;
public partial class SingleSettings : Panel {
   public SingleSettings () {
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof(Single), Configuration.singleVM);
   }
}