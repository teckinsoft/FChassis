using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Machine.ProcessingDefaults {
   public partial class SkeletonCuts : ObservableObject {
      [ObservableProperty, Prop ("Sheet cutting rules", 
                                  Prop.Type.Check, "Create sheet cut")]
      bool? unitForDXFFiles = true;

      [ObservableProperty, Prop (Prop.Type.Text, "X Spacing between Vertical Sheet Cuts")]
      double? xSpacingBetweenVerticalSheetCuts = 500.23;

      [ObservableProperty, Prop (Prop.Type.Text, "Y spacing between Horizontal Sheet Cuts")]
      double? ySpacingBetweenHorizontalSheetCuts = 400.23;

      [ObservableProperty, Prop (Prop.Type.Check, "Create Remainder Sheet")]
      bool? createRemainderSheet = true;

      [ObservableProperty, Prop (Prop.Type.Text, "Minimum Remainder Sheet Width")]
      double? minimumRemainderSheetWidth = 300.23;

      [ObservableProperty, Prop (Prop.Type.Text, "Final Cut X Offset")]
      double? finalCustXOffset = 456.23;

      [ObservableProperty, Prop (Prop.Type.Check, "Process Sheet Cut after all Parts")]
      bool? processSheetCutAfterAllPart = true;


      [ObservableProperty, Prop ("Sheet cut parameters", 
                                 Prop.Type.Text, "Micro Joint Gap at Sheet ERdge")]
      double? microJointGapAtSheetEdge = 12.34;

      [ObservableProperty, Prop (Prop.Type.Text, "Micro Joint Gap at Part Edge")]
      double? microJointGapAtPartEdge = 12.34;

      [ObservableProperty, Prop (Prop.Type.Text, "Pierce Distance from Part Edge")]
      double? pierceDistanceFromPartEdge = 12.34;

      [ObservableProperty, Prop (Prop.Type.Text, "Measuring Distance from Sheet Edge")]
      double? measuringDistanceFromSheetEdge = 12.34;

      [ObservableProperty, Prop (Prop.Type.Text, "Overtravel after Sheet Edge")]
      double? overtravelAfterSheetEdge = 12.34;
   }
}
