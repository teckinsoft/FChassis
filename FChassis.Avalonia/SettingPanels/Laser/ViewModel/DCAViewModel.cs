using FChassis.Avalonia.SettingPanels.Laser.Model;
using System.Collections.ObjectModel;

namespace FChassis.Avalonia.SettingPanels.Laser.ViewModel {
   public class DCAViewModel {
      public ObservableCollection<DCAModel> Parameters { get; set; }
      public DCAViewModel () {
         Parameters =
         [
            new DCAModel(){Name = "Acc (m/sec2)"},
            new DCAModel(){Name = "Ramp time (ms)"},
            new DCAModel(){Name = "Tolerance (mm)"},
            new DCAModel(){Name = "Angle (degree)"},
            new DCAModel(){Name = "Limit Factor (%)"},
         ];

      }

   }
}
