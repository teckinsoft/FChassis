using Avalonia.Markup.Xaml;

namespace FChassis.Avalonia.Settings.Laser.LaserCutting;
public partial class TabPanel : Settings.TabPanel {
   public TabPanel () {
      AvaloniaXamlLoader.Load (this);
      this.PopulateTabItemContent ([
         new Piercing.TabPanel(),
         new Cutting.TabPanel(),
         new Marking.TabPanel(),
         new Evapourating.TabPanel()
      ]);
   }
}