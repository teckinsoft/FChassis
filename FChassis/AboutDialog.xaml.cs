using System.Windows;

namespace FChassis {
   /// <summary>
   /// Interaction logic for Window1.xaml
   /// </summary>
   public partial class AboutDialog : Window {
      public AboutDialog () 
         => InitializeComponent ();
      
      private void CloseButton_Click (object sender, RoutedEventArgs e) 
         => this.Close ();      
   }
}
