using FChassis.Data.Model.Settings.PLCOptions;
using FChassis.Data.ViewModel;

using Avalonia.Markup.Xaml;

namespace FChassis.UI.Settings.Machine.PLCOptions;
public partial class ControlParamSettings : Panel {
   public ControlParamSettings () {
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof (ControlParam), Configuration.controlParamVM);
   }
}