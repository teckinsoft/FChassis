using System.Windows;

namespace FChassis {
   public partial class JoinResult : Window {
      public VM.JoinResultVM joinResVM = new ();

      public JoinResult () {
         InitializeComponent ();
         joinResVM.Initialize ();
         this.Closing += OnJoinResultWndClosing;
         this.DataContext = joinResVM;
      }

      void OnJoinResultWndClosing (object sender, System.ComponentModel.CancelEventArgs e) => this.DialogResult = true;
   }
}