using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Machine.ProcessingDefaults {
   public partial class CutCam : ObservableObject {
      [ObservableProperty, Prop (Prop.Type.Check, "Advanced")]
      bool? advanced = true;

      [ObservableProperty, Prop ("Finishing Rules", 
                                 Prop.Type.DBGrid, null!, null!, "BindName"),
                                       DBGridColProp (Prop.Type.Text, "Action", "Action"),
                                       DBGridColProp (Prop.Type.Text, "Contour Flags", "ContourFlag"),
                                       DBGridColProp (Prop.Type.Text, "Disabled", "Disabled"),
                                       DBGridColProp (Prop.Type.Text, "Size", "Size")]
      private object[]? finishingRules;

      [ObservableProperty, Prop ("Microjoint settings", 
                                 Prop.Type.Text, "Microjoint length")]
      double? microjointSettings = 12.23;
   }
}
