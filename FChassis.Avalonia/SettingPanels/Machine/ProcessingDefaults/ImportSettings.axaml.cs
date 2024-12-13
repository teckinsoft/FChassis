using Avalonia.Markup.Xaml;
using FChassis.Data.Model.Settings.Machine.ProcessingDefaults;
using FChassis.Data.ViewModel;

namespace FChassis.Avalonia.Settings.Machine.ProcessingDefaults;
public partial class ImportSettings : Panel {

   public ImportSettings () {
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof (Import), Configuration.importVM);
    }
}