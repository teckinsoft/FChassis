using System.Windows;
using FChassis.Core;

namespace FChassis {
   /// <summary>
   /// Interaction logic for About.xaml
   /// </summary>
   public partial class AboutWindow : Window {
      
      public AboutWindow () {
         InitializeComponent ();
         DataContext = this;
         
      }

      public string Version { get; } = MCSettings.It.Version; 

      void OnAboutCloseClick (object sender, RoutedEventArgs e) => this.Close ();
   }
}
