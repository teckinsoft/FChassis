using Avalonia.Controls;

namespace FChassis.Avalonia;
public partial class MainWindow : Window {
   Panels.MainPanel mainPanel = new ();
   public MainWindow () {
      InitializeComponent ();

      FChassis.Control.Avalonia.Panels.Child.mainWindow = this;
      this.mainPanel.switchPanel ();
   }

   internal void Switch2MainPanel () {
      this.mainPanel.switchPanel ();
   }
}