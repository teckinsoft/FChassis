using FChassis.Data.ViewModel;
using FChassis.Data.Model.Settings.Machine.AxisParams;

using Avalonia.Markup.Xaml;

namespace FChassis.Avalonia.Settings.Machine.AxisParams;
public partial class Pallet1Settings : Panel {
   public Pallet1Settings () {
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof (Pallet1), Configuration.pallet1ViewModelVM);
   }
}