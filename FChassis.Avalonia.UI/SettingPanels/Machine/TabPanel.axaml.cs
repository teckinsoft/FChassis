using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FChassis.Core.File;

namespace FChassis.UI.Settings.Machine;
public partial class TabPanel : Settings.TabPanel {
   public TabPanel () : base() {
      AvaloniaXamlLoader.Load (this);
      this.PopulateTabItemContent ([
         new General.TabPanel(),
         new AxisParams.TabPanel(),
         new TechParams.TabPanel(),
         new PLCOptions.TabPanel(),
         new ProcessingDefaults.TabPanel(),
      ]);

      JSONFileRead reader = new ();
      this.UpdateConfiguraionNodes (reader.rootNode);
      bool result = reader.Read ("C:/work/config.json");
   }

   override protected void TabItemSelected (TabItem? tabItem, string? tabName) {
      this.TabItemSelected_Default (tabItem, tabName); }
}