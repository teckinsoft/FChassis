using Avalonia.Markup.Xaml;

namespace FChassis.Avalonia.Settings.Machine.AxisParams;
public partial class TabPanel : Settings.TabPanel {
   public TabPanel () {
      AvaloniaXamlLoader.Load (this);
      this.PopulateTabItemContent ([
         new XSettings(),
         new YSettings(),
         new ZSettings(),
         new LPC1Settings(),
         new Pallet1Settings(),
      ]);
   }
}