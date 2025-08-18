using FChassis.Installer.Components;

namespace FChassis.Installer;
public partial class ComponentSelectionPage : ComponentPage {
   // Constructions
   public ComponentSelectionPage () {
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
         component.Install ();
   }
   #endregion Methods

   #region Private Implementations
   void initComponents () {
      // ------------------------------------------------------------
      this.ComponentTV.CheckBoxes = true;

      TreeNode rootNode = new TreeNode ("Installation") { Checked = true };
      this.componentTreeNodes = new TreeNode[] {
         new TreeNode {
            Text = "Install Open Cascade Zip",
            Tag = new InstallOpenCascadeZip()
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

   #region fields
   public TreeNode[] componentTreeNodes = null!;
   public List<Component> componentList = new ();
   #endregion fields
}
