using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FChassis.Control.Avalonia.Panels;

namespace FChassis.Avalonia.Panels;
public partial class MainPanel : Child {
 
   public MainPanel () : base() {
      this.mainPanel = this;
      AvaloniaXamlLoader.Load (this); }

   internal void switchPanel () {
      this.switchPanel (this); }

   void switchPanel (string name) {
      Child panel = name switch {
         "Main Panel"         => this.mainPanel,
         "Model Viewer"       => this.modelViewer,
         "Laser DataBases"    => this.laserSettingPanel,
         "Work Offsets"       => this.workOffsetSettingPanel,
         "Machine Settings"   => this.machineSettingPanel,
                            _ => null!
      };

      this.switchPanel (panel);
   }

   private void Button_Click (object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) {
      Button button = sender as Button ?? null!;
      var name = button?.Content;
      this.switchPanel (name as string ?? "");
   }

   #region "Field" 
   MainPanel mainPanel;
   ModelViewer modelViewer = new ModelViewer ();

   Settings.Laser.TabPanel laserSettingPanel = new ();
   Settings.WorkOffsets.Panel workOffsetSettingPanel = new ();
   Settings.Machine.TabPanel machineSettingPanel = new ();
   #endregion "Field" 
}