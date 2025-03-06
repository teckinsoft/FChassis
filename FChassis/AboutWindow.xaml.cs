using System.Windows;

namespace FChassis {
   /// <summary>
   /// Interaction logic for About.xaml
   /// </summary>
   public partial class AboutWindow : Window {
      public AboutWindow () {
         InitializeComponent ();
         DataContext = this;
      }
      public string Version { get; } = "55.4";
      void OnAboutCloseClick (object sender, RoutedEventArgs e) => this.Close (); 
   }
}
