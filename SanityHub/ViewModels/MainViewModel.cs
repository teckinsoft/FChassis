using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SanityHub.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SanityHub {
   public partial class MainViewModel : ObservableObject {
      public ObservableCollection<FileItem> Files { get; } = [];

      public MainViewModel () {}

      [RelayCommand]
      private void BrowseFiles () {
         var dlg = new OpenFileDialog () {
            Multiselect = true,
            Title = "Select files to run"
         };

         if (dlg.ShowDialog () == true) {
            foreach (var f in dlg.FileNames) {
               if (!Files.Any (x => x.FullPath.Equals (f, StringComparison.OrdinalIgnoreCase)))
                  Files.Add (new FileItem { FullPath = f, Status = RunStatus.None });
            }
         }
      }

      [RelayCommand]
      private void DeleteFile (FileItem item) {
         if (item == null) return;
         Files.Remove (item);
      }

      [RelayCommand]
      private void ShowDetails (FileItem item) {
         if (item == null) return;
         var wnd = new DetailsWindow (item);
         wnd.Owner = Application.Current.Windows.OfType<Window> ().FirstOrDefault (w => w.IsActive);
         wnd.ShowDialog ();
      }

      [RelayCommand]
      public void RunAll() {
         foreach (var file in Files.ToList ()) {
            // Run sequentially. If parallel runs wanted, adapt here.
            RunFile (file);
         }
      }

      private void RunFile(FileItem file) {
         file.Status = RunStatus.Running;
         file.Details = $"Started at {DateTime.Now:HH:mm:ss}\n";

         try {
            // TODO : Run logic here. We'll simulate work.

            // Simulate pass/fail
            var passed = new Random ().NextDouble () > 0.3; // 70% pass rate

            if (passed) {
               file.Status = RunStatus.Passed;
               file.Details += $"Result: Passed at {DateTime.Now:HH:mm:ss}\n";
            } else {
               file.Status = RunStatus.Failed;
               file.Details += $"Result: Failed at {DateTime.Now:HH:mm:ss}\nError: Simulated failure.\n";
            }
         } catch (Exception ex) {
            file.Status = RunStatus.Failed;
            file.Details += $"Exception: {ex.Message}\n";
         }
      }
   }
}
