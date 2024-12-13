using Flux.API;
using System.Windows;

namespace FChassis;
/// <summary>Interaction logic for MainWindow.xaml</summary>
public partial class MainWindow : Window {
   public ViewModel.MainWindow vm { get; } = new ();

   public MainWindow () {
      InitializeComponent ();
      this.DataContext = this.vm;
   }

   void OnWindowLoaded (object sender, RoutedEventArgs e) {
      this.vm.Initialize (this.Dispatcher, this.PartFiles, this);
      Area.Child = (UIElement)Lux.CreatePanel ();
#if DEBUG
      //SanityCheckMenuItem.Visibility = Visibility.Visible;
#endif
   }
}

