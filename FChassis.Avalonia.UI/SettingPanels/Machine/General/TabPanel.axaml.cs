using Avalonia.Markup.Xaml;

namespace FChassis.UI.Settings.Machine.General;
public partial class TabPanel : Settings.TabPanel {
   public TabPanel () {
      AvaloniaXamlLoader.Load (this);
      this.PopulateTabItemContent ([
         new HMISettings(),
         new MachineSettings(),
      ]);
   }
}