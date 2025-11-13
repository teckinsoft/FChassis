using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FChassis.Core;
using FChassis.Core.Processes;
using Flux.API;
using Microsoft.Win32;
using SanityHub.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace SanityHub {
   public partial class MainViewModel : ObservableObject {
      [ObservableProperty] string selectedSetting = string.Empty;
      public ObservableCollection<FileItem> Files { get; } = [];
      public Part Part { get; private set; } = new ();
      public GenesysHub GenesysHub { get; private set; } = new();
      public MainViewModel () {
         CollectCombinations ();
      }

      void CollectCombinations () {

      }

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
      public void RunAll () {
         string folderPath = "C:\\D drive\\Projects\\FChassis\\FChassis\\TestData\\SettingJSONs";
         foreach (string filePath in Directory.GetFiles (folderPath, "*.json")) {
            if (!File.Exists (filePath)) continue;
            SelectedSetting = Path.GetFileNameWithoutExtension (filePath);
            MCSettings.It.LoadSettingsFromJson (filePath);
            foreach (var file in Files.ToList ()) {
               RunFile (file);
            }
         }
      }

      public void LoadPart (string partName) {
         Part = Part.Load (partName);
         if (Part.Info.MatlName == "NONE")
            Part.Info.MatlName = "1.0038";

         if (Part.Model == null) {
            if (Part.Dwg != null) Part.FoldTo3D ();
            else if (Part.SurfaceModel != null)
               Part.SheetMetalize ();
            else
               throw new Exception ("Invalid part");
         }

         GenesysHub.Workpiece = new Workpiece (Part.Model, Part);
      }

      private void RunFile (FileItem file) {
         file.Status = RunStatus.Running;
         file.Details = $"Started at {DateTime.Now:HH:mm:ss}\n";

         try {
            // TODO : Run logic here. We'll simulate work.
            // Part loading, aligning, and cutting
            //LoadPart (file.FullPath);
            //GenesysHub.Workpiece.Align ();
            //if (MCSettings.It.CutHoles) GenesysHub.Workpiece.DoAddHoles ();
            //if (MCSettings.It.CutMarks) GenesysHub.Workpiece.DoTextMarking (MCSettings.It);
            //if (MCSettings.It.CutNotches || MCSettings.It.CutCutouts) GenesysHub.Workpiece.DoCutNotchesAndCutouts ();

            //GenesysHub.Workpiece.DoSorting ();

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