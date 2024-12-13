using Avalonia.Markup.Xaml;

namespace FChassis.Avalonia.Settings.Laser.LaserCutting.Cutting;
public partial class TabPanel : Settings.TabPanel {
   public TabPanel () {
      AvaloniaXamlLoader.Load (this);

      this.PopulateTabItemContent ([
         new LargeSettings(),
         new MediumSettings(),
         new SmallSettings(),
         new SpecialSettings(),
         new PreholeSettings(),
      ]);
   }
}