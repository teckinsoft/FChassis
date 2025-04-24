using System.Windows;

namespace FChassis;
public partial class JoinWindow : Window, IDisposable {
   public VM.JoinWindowVM joinWndVM = new ();
   public JoinWindow () {
      InitializeComponent ();

      joinWndVM.Initialize ();
      this.DataContext = joinWndVM;

      this.Loaded += (sender, e) => this.joinWndVM.Iges.InitView (this.OCCHostWnd.childHwnd);

      this.OCCHostWnd.Redraw = () => this.joinWndVM.Iges.ResizeView ();
      this.joinWndVM.Redraw += () => 
         // this.OCCHostWnd.InvalidateChildWindow ();
          this.joinWndVM.Iges.ResizeView ();

      this.Closing += JoinWindow_Closing;
      joinWndVM.EvRequestCloseWindow += () => this.Close ();
   }

   void JoinWindow_Closing (object sender, System.ComponentModel.CancelEventArgs e) {
      this.DialogResult = true;
      Dispose (); // Ensure cleanup on window close
   }

   // Implement IDisposable pattern
   public void Dispose () {
      if (joinWndVM != null) {
         joinWndVM.Uninitialize ();
         joinWndVM = null;
      }
      GC.SuppressFinalize (this);
   }

   ~JoinWindow () {
      Dispose ();
   }
}