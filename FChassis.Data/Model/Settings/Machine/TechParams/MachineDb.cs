using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Machine.TechParams {
   public partial class MachineDb : ObservableObject {
      [ObservableProperty, Prop ("General", 
                                 Prop.Type.Text, "Paramater1")]
      double? paramater1 = 0;


      [ObservableProperty, Prop ("Database Type", 
                                 Prop.Type.Combo, "Database", null!, null!,
                                 ["Default", "Trumpf LTT"])]
      string? database = "Default";
   }
}
