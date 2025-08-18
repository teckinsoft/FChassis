namespace FChassis.Installer;
public partial class MainForm : Form {
   public MainForm ()
      => InitializeComponent ();

   #region Private Implementations
   void cancelBtn_Click (object sender, EventArgs e)
      => this.Close ();

   void nextBtn_Click (object sender, EventArgs e) {
      Component? component = this.getNextComponent (this.NextBtn, this.BackBtn, out var startInstall);
      if (startInstall) {
         this.setPage (this.installationPage);
         this.ComponentSelectionPage.StartInstall ();
         return;
      }

      if (component != null ) 
         this.setPage (component?.page);

      CancelBtn.Text = "Close";
   }

   void backBtn_Click (object sender, EventArgs e) {
      Component component = this.getBackComponent (this.NextBtn, this.BackBtn);
      this.setPage (component == null ? this.ComponentSelectionPage : component.page);
   }

   public Component? getNextComponent (Button nextBtn, Button backBtn, out bool startInstall) {
      startInstall = this.activeComponentIndex == this.ComponentSelectionPage.componentList.Count ();
      if (startInstall) {
         nextBtn.Enabled = false;
         return null!;
      }

      Component component = null!;
      do {
         // Confirm with active page whether data validated and allow to set next page
         if (this.activeComponent != null
            && this.activeComponent.page != null 
            && false == this.activeComponent.page.onChange ())
            return null;

         this.activeComponentIndex++;
         if (this.activeComponentIndex == this.ComponentSelectionPage.componentList.Count ())
            nextBtn.Text = "Install";

         if (this.activeComponentIndex < this.ComponentSelectionPage.componentList.Count ())
            component = this.ComponentSelectionPage.componentList[this.activeComponentIndex];
         else
            return null;

         if (this.activeComponentIndex >= 0)
            backBtn.Visible = true;
      } while (component != null && component.selected == false);
      this.activeComponent = component;
      return component;
   }

   public Component getBackComponent (Button nextBtn, Button backBtn) {
      Component component = null!;

      do {
         this.activeComponentIndex--;
         if (this.activeComponentIndex >= 0)
            component = this.ComponentSelectionPage.componentList[this.activeComponentIndex];

         backBtn.Visible = this.activeComponentIndex >= 0;
         nextBtn.Text = "Next";
      } while (component != null && component.selected == false && this.activeComponentIndex >= 0);
      this.activeComponent = component;
      return component!;
   }

   void setPage (ComponentPage? page) {
      this.Content.Controls.Clear ();

      if (page != null)
         this.Content.Controls.Add (page);
   }
   #endregion Private Implementations   

   InstallationPage installationPage = new ();
   public Component? activeComponent = null;
   public int activeComponentIndex = -1;
}
 