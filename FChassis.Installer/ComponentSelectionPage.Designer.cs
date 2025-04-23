namespace FChassis.Installer {
   partial class ComponentSelectionPage {
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
         System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager (typeof (ComponentSelectionPage));
         ComponentTV = new TreeView ();
         SelectLB = new Label ();
         AppIcoSEP = new GroupBox ();
         ComponentDescTB = new TextBox ();
         AppICO = new PictureBox ();
         ((System.ComponentModel.ISupportInitialize)AppICO).BeginInit ();
         SuspendLayout ();
         // 
         // ComponentTV
         // 
         ComponentTV.Anchor = AnchorStyles.Top | AnchorStyles.Right;
         ComponentTV.Location = new Point (294, 91);
         ComponentTV.Name = "ComponentTV";
         ComponentTV.Size = new Size (258, 190);
         ComponentTV.TabIndex = 10;
         // 
         // SelectLB
         // 
         SelectLB.AutoSize = true;
         SelectLB.Location = new Point (1, 89);
         SelectLB.Name = "SelectLB";
         SelectLB.Size = new Size (174, 15);
         SelectLB.TabIndex = 8;
         SelectLB.Text = "Select Componenents to Install:";
         // 
         // AppIcoSEP
         // 
         AppIcoSEP.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
         AppIcoSEP.Location = new Point (0, 34);
         AppIcoSEP.Name = "AppIcoSEP";
         AppIcoSEP.Size = new Size (552, 2);
         AppIcoSEP.TabIndex = 6;
         AppIcoSEP.TabStop = false;
         // 
         // ComponentDescTB
         // 
         ComponentDescTB.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
         ComponentDescTB.BorderStyle = BorderStyle.None;
         ComponentDescTB.Enabled = false;
         ComponentDescTB.Location = new Point (0, 53);
         ComponentDescTB.Multiline = true;
         ComponentDescTB.Name = "ComponentDescTB";
         ComponentDescTB.ReadOnly = true;
         ComponentDescTB.Size = new Size (550, 33);
         ComponentDescTB.TabIndex = 7;
         ComponentDescTB.Text = "Check the components you want to install and uncheck the components you don’t want to install. Click Next to continue.";
         // 
         // AppICO
         // 
         AppICO.Image = (Image)resources.GetObject ("AppICO.Image");
         AppICO.Location = new Point (519, -1);
         AppICO.Name = "AppICO";
         AppICO.Size = new Size (32, 32);
         AppICO.SizeMode = PictureBoxSizeMode.StretchImage;
         AppICO.TabIndex = 11;
         AppICO.TabStop = false;
         // 
         // ComponentSelectionPage
         // 
         AutoScaleDimensions = new SizeF (7F, 15F);
         AutoScaleMode = AutoScaleMode.Font;
         Controls.Add (AppICO);
         Controls.Add (ComponentTV);
         Controls.Add (SelectLB);
         Controls.Add (AppIcoSEP);
         Controls.Add (ComponentDescTB);
         Name = "ComponentSelectionPage";
         Size = new Size (550, 280);
         ((System.ComponentModel.ISupportInitialize)AppICO).EndInit ();
         ResumeLayout (false);
         PerformLayout ();
      }

      #endregion
      private TreeView ComponentTV;
      private Label SelectLB;
      private GroupBox AppIcoSEP;
      private TextBox ComponentDescTB;
      private PictureBox AppICO;
   }
}
