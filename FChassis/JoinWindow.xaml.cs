using System.Windows;

namespace FChassis {
   public partial class JoinWindow : Window {
      VM.JoinWindow vm = new ();

      public JoinWindow () {
      InitializeComponent ();

         vm.Initialize ();
      this.DataContext = vm;

         this.Closing += JoinWindow_Closing;
      }

      private void JoinWindow_Closing (object sender, System.ComponentModel.CancelEventArgs e)
         => vm.Uninitialize ();
   }
}
