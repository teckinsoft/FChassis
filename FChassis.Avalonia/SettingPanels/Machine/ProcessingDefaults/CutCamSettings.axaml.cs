using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FChassis.Data.Model.Settings.Machine.ProcessingDefaults;
using FChassis.Data.ViewModel;

namespace FChassis.Avalonia.Settings.Machine.ProcessingDefaults;
public partial class CutCamSettings : Panel {
   public CutCamSettings () {
      AvaloniaXamlLoader.Load (this); 
      this.AddPropControls (typeof(CutCam), Configuration.curcamVM);
   }
}