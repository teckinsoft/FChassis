using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SanityHub.Models;
public enum RunStatus { None, Running, Passed, Failed }

public partial class FileItem : ObservableObject {
   public string FullPath { get; set; } = string.Empty;
   public string FileName => System.IO.Path.GetFileName (FullPath);
   public string CombinationName { get; set; } = string.Empty;

   [ObservableProperty] RunStatus status = RunStatus.None;
   [ObservableProperty] string details = string.Empty;

   public string StatusText {
      get {
         return Status switch {
            RunStatus.None => "None",
            RunStatus.Running => "Running...",
            RunStatus.Passed => "Passed",
            RunStatus.Failed => "Failed",
            _ => "None"
         };
      }
   }

   public Brush StatusBackground {
      get {
         return Status switch {
            RunStatus.None => new SolidColorBrush ((Color)ColorConverter.ConvertFromString ("#9CA3AF")),
            RunStatus.Running => new SolidColorBrush ((Color)ColorConverter.ConvertFromString ("#F59E0B")),
            RunStatus.Passed => new SolidColorBrush ((Color)ColorConverter.ConvertFromString ("#10B981")),
            RunStatus.Failed => new SolidColorBrush ((Color)ColorConverter.ConvertFromString ("#EF4444")),
            _ => new SolidColorBrush ((Color)ColorConverter.ConvertFromString ("#9CA3AF")),
         };
      }
   }
}
