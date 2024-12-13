using Avalonia.Markup.Xaml;
using FChassis.Avalonia.Settings.Machine.General;

namespace FChassis.Avalonia.Settings.Machine.PLCOptions;
public partial class TabPanel : Settings.TabPanel {
   public TabPanel () {
      AvaloniaXamlLoader.Load (this);
      this.PopulateTabItemContent ([
         new FuncParamSettings(),
         new ControlParamSettings(),
         new PLCKeySettings(),
      ]);
   }
}