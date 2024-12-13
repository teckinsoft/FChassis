using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FChassis.Data.Model.Settings.Machine.ProcessingDefaults;
using FChassis.Data.ViewModel;

namespace FChassis.UI.Settings.Machine.ProcessingDefaults;
public partial class CutCamSettings : Panel {
   public CutCamSettings () {
      AvaloniaXamlLoader.Load (this); 
      this.AddPropControls (typeof(CutCam), Configuration.curcamVM);
   }
}