using FChassis.Data.Model.Settings.Machine.TechParams;
using FChassis.Data.ViewModel;

using Avalonia.Markup.Xaml;

namespace FChassis.Avalonia.Settings.Machine.TechParams;
public partial class MachineDbSettings : Panel {
   public MachineDbSettings () {
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof (MachineDb), Configuration.MachineDbVM);
   }
}