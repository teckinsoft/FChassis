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
      public string Version { get; } = "Debug 69";
#elif TESTRELEASE
      public string Version { get; } = "Test Release 69";
#else 
      public string Version { get; } = "1.0.4";
#endif
      void OnAboutCloseClick (object sender, RoutedEventArgs e) => this.Close ();
   }
}
