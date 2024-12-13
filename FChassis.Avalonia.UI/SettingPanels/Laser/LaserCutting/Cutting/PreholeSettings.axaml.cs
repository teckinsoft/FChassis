using FChassis.Data.Model.Settings.Laser.LaserCutting.Cutting;
using FChassis.Data.ViewModel;

using Avalonia.Markup.Xaml;

namespace FChassis.UI.Settings.Laser.LaserCutting.Cutting;
public partial class PreholeSettings : Panel {
   public PreholeSettings () {
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof (PreHole), Configuration.preholeVM);
   }
}