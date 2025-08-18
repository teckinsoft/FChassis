namespace FChassis.Installer;
public partial class InstallationPage : ComponentPage {
   public InstallationPage () {
      InitializeComponent ();

      sOutputLBox = this.OutputLBox;
   }

   public static void WriteLine(string line) {
      if (sOutputLBox.InvokeRequired)  // We are on a background thread, so use Invoke
         sOutputLBox.Invoke (new Action (() => sOutputLBox.Items.Add (line)));
      else                             // Already on UI thread
         sOutputLBox.Items.Add (line); 
   }

   public static ListBox sOutputLBox = null!;
}