using System.Windows.Controls;

namespace FChassis;
public partial class JoinControl : UserControl {
   FChassis.VM.JoinControl vm = new ();

   public JoinControl () {
      InitializeComponent ();
      this.DataContext = vm;
   }
}
