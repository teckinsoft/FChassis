using Flux.API;
using System.Windows;

namespace FChassis;
/// <summary>Interaction logic for MainWindow.xaml</summary>
public partial class MainWindow : Window {
   public ViewModel.MainWindow VM { get; } = new ();

   public MainWindow () {
      InitializeComponent ();
      this.DataContext = this.VM;
   }

   void OnWindowLoaded (object sender, RoutedEventArgs e) {
      this.VM.Initialize (this.Dispatcher, this.PartFiles, this);
      Area.Child = (UIElement)Lux.CreatePanel ();
#if DEBUG
      //SanityCheckMenuItem.Visibility = Visibility.Visible;
#endif
   }
}

