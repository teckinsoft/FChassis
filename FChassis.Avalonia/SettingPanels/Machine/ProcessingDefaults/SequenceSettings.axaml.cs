using Avalonia.Markup.Xaml;
using FChassis.Data.Model.Settings.Machine.ProcessingDefaults;
using FChassis.Data.ViewModel;

namespace FChassis.Avalonia.Settings.Machine.ProcessingDefaults;
public partial class SequenceSettings : Panel{

   public SequenceSettings () {
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof (Sequence), Configuration.sequenceVM);
   }
}