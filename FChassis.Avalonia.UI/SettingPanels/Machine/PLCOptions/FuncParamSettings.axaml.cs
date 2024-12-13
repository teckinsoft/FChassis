using FChassis.Data.Model.Settings.Machine.PLCOptions;
using FChassis.Data.ViewModel;

using Avalonia.Markup.Xaml;

namespace FChassis.UI.Settings.Machine.PLCOptions;
public partial class FuncParamSettings : Panel {
   public FuncParamSettings () {
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof (FuncParam), Configuration.funcParamVM);
   }
}