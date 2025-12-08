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
      public string Version { get; } = "Debug 80";
#elif TESTRELEASE
      public string Version { get; } = "Test Release 80";
#else 
      public string Version { get; } = "1.0.15";
#endif
      void OnAboutCloseClick (object sender, RoutedEventArgs e) => this.Close ();
   }
}
