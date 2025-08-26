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

#if DEBUG
      public string Version { get; } = "Debug 67";
#elif TESTRELEASE
      public string Version { get; } = "Test Release 67";
#else 
      public string Version { get; } = "1.0.67";
#endif
      void OnAboutCloseClick (object sender, RoutedEventArgs e) => this.Close ();
   }
}
