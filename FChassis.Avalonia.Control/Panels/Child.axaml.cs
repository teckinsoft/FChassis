using Avalonia.Controls;

namespace FChassis.UI.Panels;
public partial class Child : UserControl {
   static internal MainWindow? mainWindow = null;
   public Child () {
      InitializeComponent ();
   }

   public void switchPanel(Child panel = null!) {
      if (panel == null || panel == this.currentPanel)
         return;

      this.currentPanel = panel;
      if(Child.mainWindow != null) 
         Child.mainWindow.Content = panel;
   }

  Child currentPanel = null!;
}