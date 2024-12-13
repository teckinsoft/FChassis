using Avalonia.Controls;

namespace FChassis.Control.Avalonia.Panels;
public partial class Child : UserControl {
   static public dynamic? mainWindow = null;

   public Child () {
      InitializeComponent (); }

   public void switchPanel(Child panel = null!) {
      if (panel == null || panel == this.currentPanel)
         return;

      this.currentPanel = panel;
      if(Child.mainWindow != null) 
         Child.mainWindow.Content = panel;
   }

   Child currentPanel = null!;
}