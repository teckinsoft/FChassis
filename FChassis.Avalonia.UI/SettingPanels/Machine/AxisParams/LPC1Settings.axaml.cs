using FChassis.Data.ViewModel;
using FChassis.Data.Model.Settings.Machine.AxisParams;

using Avalonia.Markup.Xaml;

namespace FChassis.UI.Settings.Machine.AxisParams;
public partial class LPC1Settings : Panel {
   public LPC1Settings () {
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof (LPC1), Configuration.lpc1VM);
   }
}