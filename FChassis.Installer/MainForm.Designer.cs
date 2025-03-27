namespace FChassis.Installer {
   partial class MainForm {
      /// <summary>
      ///  Required designer variable.
      /// </summary>
      private System.ComponentModel.IContainer components = null;

      /// <summary>
      ///  Clean up any resources being used.
      /// </summary>
      /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
      protected override void Dispose (bool disposing) {
         if (disposing && (components != null)) {
            components.Dispose ();
         }
         base.Dispose (disposing);
      }

      #region Windows Form Designer generated code

      /// <summary>
      ///  Required method for Designer support - do not modify
      ///  the contents of this method with the code editor.
      /// </summary>
      private void InitializeComponent () {
         ComponentPage = new ComponentPage ();
         Content = new Panel ();
         NextBtn = new Button ();
         CancelBtn = new Button ();
         BackBtn = new Button ();
         Content.SuspendLayout ();
         SuspendLayout ();
         // 
         // ComponentPage
         // 
         ComponentPage.Location = new Point (0, 0);
         ComponentPage.Name = "ComponentPage";
         ComponentPage.Size = new Size (550, 280);
         ComponentPage.TabIndex = 0;
         // 
         // Content
         // 
         Content.Controls.Add (ComponentPage);
         Content.Location = new Point (9, 8);
         Content.Name = "Content";
         Content.Size = new Size (550, 280);
         Content.TabIndex = 1;
         // 
         // NextBtn
         // 
         NextBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
         NextBtn.Location = new Point (373, 303);
         NextBtn.Name = "NextBtn";
         NextBtn.Size = new Size (75, 23);
         NextBtn.TabIndex = 13;
         NextBtn.Text = "Next";
         NextBtn.UseVisualStyleBackColor = true;
         NextBtn.Click += nextBtn_Click;
         // 
         // CancelBtn
         // 
         CancelBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
         CancelBtn.DialogResult = DialogResult.Cancel;
         CancelBtn.Location = new Point (482, 303);
         CancelBtn.Name = "CancelBtn";
         CancelBtn.Size = new Size (75, 23);
         CancelBtn.TabIndex = 14;
         CancelBtn.Text = "Cancel";
         CancelBtn.UseVisualStyleBackColor = true;
         CancelBtn.Click += cancelBtn_Click;
         // 
         // BackBtn
         // 
         BackBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
         BackBtn.Location = new Point (292, 303);
         BackBtn.Name = "BackBtn";
         BackBtn.Size = new Size (75, 23);
         BackBtn.TabIndex = 15;
         BackBtn.Text = "Back";
         BackBtn.UseVisualStyleBackColor = true;
         BackBtn.Visible = false;
         BackBtn.Click += backBtn_Click;
         // 
         // MainForm
         // 
         AutoScaleDimensions = new SizeF (7F, 15F);
         AutoScaleMode = AutoScaleMode.Font;
         ClientSize = new Size (569, 338);
         Controls.Add (BackBtn);
         Controls.Add (NextBtn);
         Controls.Add (CancelBtn);
         Controls.Add (Content);
         FormBorderStyle = FormBorderStyle.FixedDialog;
         Name = "MainForm";
         Text = "FChassis Installer";
         Content.ResumeLayout (false);
         ResumeLayout (false);
      }

      #endregion

      private ComponentPage ComponentPage;
      private Panel Content;
      private Button NextBtn;
      private Button CancelBtn;
      private Button BackBtn;
   }
}
