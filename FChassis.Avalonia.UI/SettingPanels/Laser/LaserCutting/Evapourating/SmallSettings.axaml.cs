using FChassis.Data.Model.Settings.Laser.LaserCutting.Evapourating;
using FChassis.Data.ViewModel;

using Avalonia.Markup.Xaml;

namespace FChassis.UI.Settings.Laser.LaserCutting.Evapourating;
public partial class SmallSettings : Panel {
   public SmallSettings () { 
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls(typeof(Small), Configuration.evapouratingSmallVM);
   }
}