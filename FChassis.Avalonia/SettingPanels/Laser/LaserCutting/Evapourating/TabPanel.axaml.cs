using Avalonia.Markup.Xaml;
using Avalonia.Controls;

namespace FChassis.Avalonia.Settings.Laser.LaserCutting.Evapourating;
public partial class TabPanel : Settings.TabPanel {
   public TabPanel () {
      AvaloniaXamlLoader.Load (this);
      this.PopulateTabItemContent ([
         new LargeSettings(),
         new MediumSettings(),
         new SmallSettings(),
      ]);
   }
}