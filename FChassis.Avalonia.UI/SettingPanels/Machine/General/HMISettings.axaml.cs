using FChassis.Data.ViewModel;
using FChassis.Data.Model.Settings.Machine.General;

using Avalonia.Markup.Xaml;

namespace FChassis.UI.Settings.Machine.General;
public partial class HMISettings : Panel {
   public HMISettings () {
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof (HMI), Configuration.hmiVM);
   }
}