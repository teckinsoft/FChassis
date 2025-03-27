namespace FChassis.Installer;
public partial class ComponentPage : UserControl {
   // #region Constructions
   public ComponentPage () {
      InitializeComponent ();
      this.initComponents ();
   }

   #region Methods
   public void StartInstall () {
      List<Component> components = this.componentTreeNodes
          .Where (c => c.Tag is Component comp && comp.selected)
          .Select (c => (Component)c.Tag)
          .ToList ();

      foreach (var component in components)
         component.method ();
   }

   public Component GetNextComponent (Button nextBtn, Button backBtn, out bool startInstall) {
      startInstall = this.pageIndex == this.componentList.Count ();
      if (startInstall)
         return null!;

      Component component = null!;
      do {
         this.pageIndex++;
         if (this.pageIndex == this.componentList.Count ())
            nextBtn.Text = "Install";         

         if (this.pageIndex < this.componentList.Count ())
            component = this.componentList[this.pageIndex];
         else
            return null!;

         if (this.pageIndex >= 0)
            backBtn.Visible = true;
      } while (component != null && component.selected == false);
      return component!;
   }

   public Component GetBackComponent(Button nextBtn, Button backBtn) {
      Component component = null!;

      do {
         this.pageIndex--;
         if (pageIndex >= 0)
            component = this.componentList[pageIndex];

         backBtn.Visible = pageIndex >= 0;
         nextBtn.Text = "Next";            
      } while (component != null && component.selected == false && this.pageIndex >= 0);
      return component!;
   }
   #endregion Methods

   #region Private Implementations
   void initComponents () {
      this.ComponentTV.CheckBoxes = true;

      TreeNode rootNode = new TreeNode ("Installation") { Checked = true };
      this.componentTreeNodes = new TreeNode[] {
         new TreeNode {
            Text = "Dot Net 8.0",
            Tag = new Components.InstallDotNet()
         },
         new TreeNode {
            Text = "Flux SDK.4 Setup",
            Tag = new Components.InstallFluxSDK(),
         },
         new TreeNode {
            Text = "OpenCascade 7.5.0",
            Tag = new Components.InstallOpenCascade ()
         },
         new TreeNode {
            Text = "FChassis Installation",
            Tag = new Components.InstallFChassis ()
         },
         new TreeNode {
            Text = "Data Folder Mapping",
            Tag = new Components.DataFolderMapping () {
               page = new DataFolderMappingPage () }
         },
      };

      foreach (TreeNode component in this.componentTreeNodes) {
         component.Checked = true;
         rootNode.Nodes.Add (component);

         if (component.Tag is Component comp && comp.page != null)
            this.componentList.Add (comp);
      }

      this.ComponentTV.Nodes.Add (rootNode);
      this.ComponentTV.ExpandAll ();
      this.ComponentTV.AfterCheck += this.componentTV_AfterCheck!;
   }
  
   void componentTV_AfterCheck (object sender, TreeViewEventArgs e) {
      if (e.Action != TreeViewAction.ByMouse) return; // Prevent recursion issues

      Component? comp = e.Node!.Tag as Component;
      if (comp != null)
         comp.selected = e.Node!.Checked;
   }
   #endregion Private Implementations

   // #region fields
   public TreeNode[] componentTreeNodes = null!;
   public List<Component> componentList = new ();
   int pageIndex = -1;
}
