using FChassis.Data.Model.Settings.Machine.TechParams;
using FChassis.Data.ViewModel;

using Avalonia.Markup.Xaml;

namespace FChassis.UI.Settings.Machine.TechParams;
public partial class LaserTechSettings : Panel {
   public LaserTechSettings () {
      AvaloniaXamlLoader.Load (this);  
      this.AddPropControls (typeof (LaserSys), Configuration.laserTechVM);
   }
}