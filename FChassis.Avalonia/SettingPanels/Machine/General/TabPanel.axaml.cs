using Avalonia.Markup.Xaml;

namespace FChassis.Avalonia.Settings.Machine.General;
public partial class TabPanel : Settings.TabPanel {
   public TabPanel () {
      AvaloniaXamlLoader.Load (this);
      this.PopulateTabItemContent ([
         new HMISettings(),
         new MachineSettings(),
      ]);
   }
}