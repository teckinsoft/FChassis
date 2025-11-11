using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SanityHub.Models;

namespace SanityHub {
   /// <summary>
   /// Interaction logic for DetailsWindow.xaml
   /// </summary>
   public partial class DetailsWindow : Window {
      public DetailsWindow (FileItem item) {
         InitializeComponent ();
         DataContext = item;
      }

      private void Close_Click (object sender, RoutedEventArgs e) {
         Close ();
      }
   }
}
