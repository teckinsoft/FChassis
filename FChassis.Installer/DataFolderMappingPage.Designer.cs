namespace FChassis.Installer {
   partial class DataFolderMappingPage {
      /// <summary> 
      /// Required designer variable.
      /// </summary>
      private System.ComponentModel.IContainer components = null;

      /// <summary> 
      /// Clean up any resources being used.
      /// </summary>
      /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
      protected override void Dispose (bool disposing) {
         if (disposing && (components != null)) {
            components.Dispose ();
         }
         base.Dispose (disposing);
      }

      #region Component Designer generated code

      /// <summary> 
      /// Required method for Designer support - do not modify 
      /// the contents of this method with the code editor.
      /// </summary>
      private void InitializeComponent () {
         MapFolderLB = new Label ();
         MapFolderTB = new TextBox ();
         BrowseBtn = new Button ();
         SuspendLayout ();
         // 
         // MapFolderLB
         // 
         MapFolderLB.AutoSize = true;
         MapFolderLB.Location = new Point (3, 0);
         MapFolderLB.Name = "MapFolderLB";
         MapFolderLB.Size = new Size (172, 15);
         MapFolderLB.TabIndex = 0;
         MapFolderLB.Text = "Select the Path for 'Map Folder'";
         // 
         // MapFolderTB
         // 
         MapFolderTB.Location = new Point (3, 18);
         MapFolderTB.Name = "MapFolderTB";
         MapFolderTB.Size = new Size (458, 23);
         MapFolderTB.TabIndex = 1;
         // 
         // BrowseBtn
         // 
         BrowseBtn.Location = new Point (467, 18);
         BrowseBtn.Name = "BrowseBtn";
         BrowseBtn.Size = new Size (75, 23);
         BrowseBtn.TabIndex = 2;
         BrowseBtn.Text = "Browse";
         BrowseBtn.UseVisualStyleBackColor = true;
         BrowseBtn.Click += BrowseBtn_Click;
         // 
         // DataFolderMappingPage
         // 
         AutoScaleDimensions = new SizeF (7F, 15F);
         AutoScaleMode = AutoScaleMode.Font;
         Controls.Add (BrowseBtn);
         Controls.Add (MapFolderTB);
         Controls.Add (MapFolderLB);
         Name = "DataFolderMappingPage";
         Size = new Size (545, 275);
         ResumeLayout (false);
         PerformLayout ();
      }

      #endregion

      private Label MapFolderLB;
      private TextBox MapFolderTB;
      private Button BrowseBtn;
   }
}
