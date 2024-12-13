using FChassis.Data.Model.Settings.Laser.LaserCutting.Marking;
using FChassis.Data.ViewModel;

using Avalonia.Markup.Xaml;

namespace FChassis.UI.Settings.Laser.LaserCutting.Marking;
public partial class MediumSettings : Panel {
   public MediumSettings () { 
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls(typeof (Medium), Configuration.makingMediumVM);
   }
}