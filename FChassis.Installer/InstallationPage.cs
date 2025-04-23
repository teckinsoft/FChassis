namespace FChassis.Installer;
public partial class InstallationPage : ComponentPage {
   public InstallationPage () {
      InitializeComponent ();

      sOutputLBox = this.OutputLBox;
   }

   public static void WriteLine(string line) 
      => sOutputLBox.Items.Add (line);

   public static ListBox sOutputLBox = null!;
}