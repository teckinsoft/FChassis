using FChassis.Data.ViewModel;

using Avalonia.Markup.Xaml;

namespace FChassis.UI.Settings.Laser.DCAPage;
public partial class Panel : Settings.Panel {
   public Panel () {
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof (Data.Model.Settings.Laser.DCAPage), Configuration.dcaPageVM);
   }
}