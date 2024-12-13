using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Machine.ProcessingDefaults {
   public partial class WorkSupport : ObservableObject {
      [ObservableProperty, Prop ("Work Support Configuration",
                                 Prop.Type.Text, "Distance between Slats")]
      double? distanceBetweenSlats = 12.56;

      [ObservableProperty, Prop (Prop.Type.Text, "Offset of First Slat from Sheet Edge")]
      double? offsetOfFirstSlatFromSheetEdge = 12.56;

      [ObservableProperty, Prop (Prop.Type.Text, "Distance between Support Pins in a Slat")]
      double? distanceBetweenSupportPinsInSlat = 12.56;
   }
}
