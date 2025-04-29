using FChassis.Installer.Components;

namespace FChassis.Installer; 
public partial class DataFolderMappingPage : ComponentPage {
   public DataFolderMappingPage ()
      => InitializeComponent ();

   void BrowseBtn_Click (object sender, EventArgs e) {
      FolderBrowserDialog fbrowserDialog = new FolderBrowserDialog ();
      if (fbrowserDialog.ShowDialog () != DialogResult.OK)
         return;

      MapFolderTB.Text = fbrowserDialog.SelectedPath;
   }

   public override bool onChange () {
      if (string.IsNullOrEmpty (MapFolderTB.Text)) {
         MessageBox.Show ("Select a Folder for mapping");
         return false;
      }

      DataFolderMapping dataFolderMappinng = (this.component as DataFolderMapping)!;
      dataFolderMappinng.mapPath = MapFolderTB.Text;
      return true;
   }
}
