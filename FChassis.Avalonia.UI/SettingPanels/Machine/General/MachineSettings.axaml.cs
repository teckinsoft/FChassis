using FChassis.Data.ViewModel;
using Avalonia.Markup.Xaml;

namespace FChassis.UI.Settings.Machine.General;
public partial class MachineSettings : Panel {
   public MachineSettings () {
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof (Data.Model.Settings.Machine.General.Machine), Configuration.machineVM);
   }
}