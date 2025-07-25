namespace FChassis.Installer;
public partial class MainForm : Form {
   public MainForm ()
      => InitializeComponent ();

   #region Private Implementations
   void cancelBtn_Click (object sender, EventArgs e)
      => Close ();

   void nextBtn_Click (object sender, EventArgs e) {
      Component? component = getNextComponent (NextBtn, BackBtn, out var startInstall);
      if (startInstall) {
         setPage (installationPage);
         ComponentSelectionPage.StartInstall ();
         return;
      }

      if (component != null ) 
         setPage (component?.page);

      CancelBtn.Text = "Close";
   }

   void backBtn_Click (object sender, EventArgs e) {
      Component component = getBackComponent (NextBtn, BackBtn);
      setPage (component == null ? ComponentSelectionPage : component.page);
   }

   public Component? getNextComponent (Button nextBtn, Button backBtn, out bool startInstall) {
      startInstall = activeComponentIndex == ComponentSelectionPage.componentList.Count ();
      if (startInstall) {
         nextBtn.Enabled = false;
         return null!;
      }

      Component component = null!;
      do {
         // Confirm with active page whether data validated and allow to set next page
         if (activeComponent != null
            && activeComponent.page != null 
            && false == activeComponent.page.onChange ())
            return null;

         activeComponentIndex++;
         if (activeComponentIndex == ComponentSelectionPage.componentList.Count ())
            nextBtn.Text = "Install";

         if (activeComponentIndex < ComponentSelectionPage.componentList.Count ())
            component = ComponentSelectionPage.componentList[activeComponentIndex];
         else
            return null;

         if (activeComponentIndex >= 0)
            backBtn.Visible = true;
      } while (component != null && component.selected == false);
      activeComponent = component;
      return component;
   }

   public Component getBackComponent (Button nextBtn, Button backBtn) {
      Component component = null!;

      do {
         activeComponentIndex--;
         if (activeComponentIndex >= 0)
            component = ComponentSelectionPage.componentList[activeComponentIndex];

         backBtn.Visible = activeComponentIndex >= 0;
         nextBtn.Text = "Next";
      } while (component != null && component.selected == false && activeComponentIndex >= 0);
      activeComponent = component;
      return component!;
   }

   void setPage (ComponentPage? page) {
      Content.Controls.Clear ();

      if (page != null) {
         page.Anchor = AnchorStyles.Left | AnchorStyles.Right
                     | AnchorStyles.Top | AnchorStyles.Bottom;
         Content.Controls.Add (page);
      }
   }
   #endregion Private Implementations   

   InstallationPage installationPage = new ();
   public Component? activeComponent = null;
   public int activeComponentIndex = -1;
}
 