namespace FChassis.Installer {
   partial class InstallationPage {
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
         InstallLB = new Label ();
         OutputLBox = new ListBox ();
         SuspendLayout ();
         // 
         // InstallLB
         // 
         InstallLB.AutoSize = true;
         InstallLB.Location = new Point (3, 0);
         InstallLB.Name = "InstallLB";
         InstallLB.Size = new Size (103, 15);
         InstallLB.TabIndex = 0;
         InstallLB.Text = "Installing FChassis";
         // 
         // OutputLBox
         // 
         OutputLBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
         OutputLBox.FormattingEnabled = true;
         OutputLBox.HorizontalScrollbar = true;
         OutputLBox.ItemHeight = 15;
         OutputLBox.Location = new Point (3, 18);
         OutputLBox.Name = "OutputLBox";
         OutputLBox.Size = new Size (547, 259);
         OutputLBox.TabIndex = 1;
         // 
         // InstallationPage
         // 
         AutoScaleDimensions = new SizeF (7F, 15F);
         AutoScaleMode = AutoScaleMode.Font;
         Controls.Add (OutputLBox);
         Controls.Add (InstallLB);
         Name = "InstallationPage";
         Size = new Size (550, 280);
         ResumeLayout (false);
         PerformLayout ();
      }

      #endregion

      private Label InstallLB;
      private ListBox OutputLBox;
   }
}
