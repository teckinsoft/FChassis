using FChassis.Data.ViewModel;
using FChassis.Data.Model.Settings.Machine.TechParams;

using Avalonia.Markup.Xaml;

namespace FChassis.UI.Settings.Machine.TechParams;
public partial class MFuncSettings : Panel {
   public MFuncSettings () {
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof (MFunctions), Configuration.functionsVM);
   }
}