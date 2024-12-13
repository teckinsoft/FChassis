using Avalonia.Markup.Xaml;
using FChassis.UI.Settings.Machine.General;

namespace FChassis.UI.Settings.Machine.PLCOptions;
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