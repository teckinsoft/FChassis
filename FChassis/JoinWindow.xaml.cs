using System.Windows;

namespace FChassis;
public partial class JoinWindow : Window, IDisposable {
   public VM.JoinWindowVM joinWndVM = new ();

   public JoinWindow () {
      InitializeComponent (); // No need to call this manually in OnMirrorAndJoin()

      joinWndVM.Initialize ();
      this.DataContext = joinWndVM;

      this.Closing += JoinWindow_Closing;
      joinWndVM.EvRequestCloseWindow += () => this.Close ();
   }

   void JoinWindow_Closing (object sender, System.ComponentModel.CancelEventArgs e) {
      this.DialogResult = true; // Only useful if the window was shown as a dialog
      Dispose (); // Ensure cleanup on window close
   }

   // Implement IDisposable pattern
   public void Dispose () {
      if (joinWndVM != null) {
         joinWndVM.Uninitialize ();
         joinWndVM = null;
      }

      // Unsubscribe from events
      this.Closing -= JoinWindow_Closing;
      if (joinWndVM != null) {
         joinWndVM.EvRequestCloseWindow -= () => this.Close ();
      }

      GC.SuppressFinalize (this);
   }

   ~JoinWindow () {
      Dispose ();
   }
}
