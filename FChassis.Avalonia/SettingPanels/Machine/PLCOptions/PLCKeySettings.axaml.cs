using FChassis.Data.Model.Settings.PLCOptions;
using FChassis.Data.ViewModel;

using Avalonia.Markup.Xaml;

namespace FChassis.Avalonia.Settings.Machine.PLCOptions;
public partial class PLCKeySettings : Panel {
   public PLCKeySettings () {
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof (PLCKey), Configuration.plcKeyVM);
   }
}