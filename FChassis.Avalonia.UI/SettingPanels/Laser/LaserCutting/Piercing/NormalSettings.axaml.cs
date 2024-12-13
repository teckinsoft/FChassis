using FChassis.Data.Model.Settings.Laser.LaserCutting.Piercing;
using FChassis.Data.ViewModel;

using Avalonia.Markup.Xaml;

namespace FChassis.UI.Settings.Laser.LaserCutting.Piercing;
public partial class NormalSettings : Panel {
   public NormalSettings () {
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof(Normal), Configuration.normalVM);   
   }
}