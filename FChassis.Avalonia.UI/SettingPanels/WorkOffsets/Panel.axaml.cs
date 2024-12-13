using FChassis.Data.ViewModel;
using FChassis.UI.Panels;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FChassis.UI.Settings.WorkOffsets;
public partial class Panel : Settings.Panel {
   public Panel () {
      AvaloniaXamlLoader.Load (this);

      this.DataContext = Configuration.workOFfsetsVM;

      Grid grid = this.FindNameScope ()?.Find<Grid> ("offsetGrid")!;
      this.AddPropControls (grid, typeof (Data.Model.Settings.WorkOffsets));
   }

   private void CloseBtn_Click (object? sender, Avalonia.Interactivity.RoutedEventArgs e) {
      Child.mainWindow?.Switch2MainPanel ();}
}
