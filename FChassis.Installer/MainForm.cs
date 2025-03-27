namespace FChassis.Installer;
public partial class MainForm : Form {
   public MainForm () 
      => InitializeComponent ();

   #region Private Implementations
   void cancelBtn_Click (object sender, EventArgs e)
      => this.Close ();

   void nextBtn_Click (object sender, EventArgs e) {
      bool startInstall;
      Component component = this.ComponentPage.GetNextComponent (this.NextBtn, this.BackBtn, out startInstall);
      this.Content.Controls.Clear ();
      if (component != null)
         this.Content.Controls.Add (component!.page);

      if (startInstall) {
         this.NextBtn.Enabled = false;
         this.ComponentPage.StartInstall ();
      }      
   }

   void backBtn_Click (object sender, EventArgs e) {
      Component component = this.ComponentPage.GetBackComponent (this.NextBtn, this.BackBtn);
      this.Content.Controls.Clear ();
      this.Content.Controls.Add (component == null ?this.ComponentPage :component.page);
   }   
   #endregion Private Implementations   
}
