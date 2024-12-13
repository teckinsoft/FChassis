using Avalonia.Markup.Xaml;

namespace FChassis.UI.Settings.Machine.ProcessingDefaults;
public partial class TabPanel : Settings.TabPanel {
   public TabPanel () {
      AvaloniaXamlLoader.Load (this);
      this.PopulateTabItemContent ([
         new ImportSettings(),
         new CutCamSettings(),
         new ProfileCamSettings(),
         new SequenceSettings(),
         new WorkSupportSettings(),
         new SkeletonCutsSettings(),
      ]);
   }
}