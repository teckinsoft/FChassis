using FChassis.UI.SettingPanels.Laser.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FChassis.UI.SettingPanels.Laser.ViewModel {
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
