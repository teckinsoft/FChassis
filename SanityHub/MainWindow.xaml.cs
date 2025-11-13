using System.Windows;

namespace SanityHub {
   /// <summary>
   /// Interaction logic for MainWindow.xaml
   /// </summary>
   public partial class MainWindow : Window {
      public MainWindow () {
         InitializeComponent ();
         DataContext = new MainViewModel ();
      }

      void Close_Clicked (object sender, RoutedEventArgs e) => Close ();
   }
}