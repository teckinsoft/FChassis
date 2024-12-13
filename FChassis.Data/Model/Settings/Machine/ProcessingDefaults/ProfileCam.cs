using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Machine.ProcessingDefaults {
   public partial class ProfileCam : ObservableObject {
      [ObservableProperty, Prop (Prop.Type.Check,"Advanced")]
      bool? unitForDXFFiles = true;

      [ObservableProperty, Prop ("Cutting", 
                                 Prop.Type.Combo, "Choose Cutting Condition by", null!, null!, 
                                    ["Max.Segment Length", "Avg Segment Length", "Area",])]
      string? chooseCuttingConditionBy = "Avg.segment length";

      [ObservableProperty, Prop (Prop.Type.Combo, "Process for Open Polylines", null!, null!, 
                                 ["None", "Mark", "Cut", "EopenPlineProcess.ByLayer"])]
      string? processForOpenPolylines = "EopenPlineProcess.ByLayer";


      [ObservableProperty, Prop ("Pierce settings", 
                                 Prop.Type.Check, "Allow Approach that is more than 0.5 Distance to Opposite Side")]
      bool? allowApproachMoreThanDistanceToOppositeSide = true;


      [ObservableProperty, Prop ("Scrap Cutting", 
                                 Prop.Type.Text, "Scrap Grid Width")]
      double? scrapGridWidth = 2.32;

      [ObservableProperty, Prop (Prop.Type.Text, "Approach Length for Separating Cuts")]
      double? approachLengthForSeparatingCuts = 2.32;
   }
}
