using Avalonia.Markup.Xaml;
using FChassis.Data.Model.Settings.Machine.ProcessingDefaults;
using FChassis.Data.ViewModel;

namespace FChassis.UI.Settings.Machine.ProcessingDefaults;
public partial class WorkSupportSettings : Panel{
   public WorkSupportSettings () {
      AvaloniaXamlLoader.Load (this);

      this.AddPropControls (typeof (WorkSupport), Configuration.workSupportVM);
   }
}