using Avalonia.Markup.Xaml;

namespace FChassis.Avalonia.Settings.Laser.LaserCutting.Piercing;
public partial class TabPanel : Settings.TabPanel {
   public TabPanel () {
      AvaloniaXamlLoader.Load (this);
      this.PopulateTabItemContent ([
         new PeckSettings(),
         new MultipleSettings(),
         new RampSettings(),
         new SingleSettings(),
         new NormalSettings(),
         new GentleSettings(),
         new DotPunchSettings()
      ]);
   }
}