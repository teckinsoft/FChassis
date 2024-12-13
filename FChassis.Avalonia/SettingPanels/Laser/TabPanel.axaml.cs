using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FChassis.Core.File;

namespace FChassis.Avalonia.Settings.Laser;
public partial class TabPanel : Settings.TabPanel {
   public TabPanel () {
      AvaloniaXamlLoader.Load (this);
      this.PopulateTabItemContent ([
         new LaserCutting.TabPanel(),
         new DCAPage.Panel(),
         new RampCycles.Panel(),
      ]);

      JSONFileRead reader = new ();
      this.UpdateConfiguraionNodes (reader.rootNode);
      reader.Read ("C:/work/config.json");
   }

   override protected void TabItemSelected (TabItem? tabItem, string? tabName) {
      this.TabItemSelected_Default (tabItem, tabName);
   }
}