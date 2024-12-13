using Avalonia.Markup.Xaml;
using FChassis.Data.Model.Settings.Machine.ProcessingDefaults;
using FChassis.Data.ViewModel;

namespace FChassis.Avalonia.Settings.Machine.ProcessingDefaults;
public partial class SkeletonCutsSettings : Panel{
   public SkeletonCutsSettings () {
      AvaloniaXamlLoader.Load (this);
      this.AddPropControls (typeof (SkeletonCuts), Configuration.skeletonCutsVM);
   }
}