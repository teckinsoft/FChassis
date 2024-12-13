using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Machine.ProcessingDefaults {
   public partial class Sequence : ObservableObject {
      [ObservableProperty, Prop ("Laser Sequence", Prop.Type.Combo, "Laser Processing Sequence", null!, null!,
                                    ["Standard", "MarkCut","PierceCut", "MarkPierceCut", "PierceMarkCut"])]
      string? laserProcessingSeq = "Standard";

      [ObservableProperty, Prop (Prop.Type.Combo, "Laser Seq", null!, null!, 
                                 ["Partwise", "PartwiseNative", "InnerFirst", "SortByCC"])]
      string? laserSeq = "PartwiseNative";

      [ObservableProperty, Prop (Prop.Type.Check, "Do Pre-piercing Part-by-Part")]
      bool? doPrePiercingPartByPart = true;


      [ObservableProperty, Prop ("Route Traverse", Prop.Type.Check, "Move Pierce Points to Reduce Traverse")]
      bool? movePiercePointsToReduceTraverse = true;

      [ObservableProperty, Prop (Prop.Type.Check, "Move Pierce Points to Prevent Tilting")]
      bool? movePiercePointsToPreventTilting = true;

      [ObservableProperty, Prop (Prop.Type.Check, "Microjoint Nested Holes if Tilting")]
      bool? microjointNestedHolesIfTilting = true;

      [ObservableProperty, Prop (Prop.Type.Text, "Ignore Holes Smaller than this")]
      double? ignoreHolesSmallerThanThis = 12.23;

      [ObservableProperty, Prop (Prop.Type.Text, "Minimum Cutting Head Height when Traversing")]
      double? minimumCuttingHeadHeightWhenTraversing = 12.23;

      [ObservableProperty, Prop (Prop.Type.Check, "Route Traverse Lines around Holes")]
      bool? routeTraverseLinesAroundHoles = true;

      [ObservableProperty, Prop (Prop.Type.Text, "Allowance when Routing around Holes")]
      double? allowanceWhenRoutingAroundHoles = 25.23;

      [ObservableProperty, Prop (Prop.Type.Text, "lift Nozzle if Routing Penalty more than")]
      double? liftNozzleIfRoutingPenaltyMoreThan = 25.23;

      [ObservableProperty, Prop (Prop.Type.Text, "Allowance when Routing around Tilting Holes")]
      double? allowanceWhenRoutingAroundTiltingHoles = 25.23;

      [ObservableProperty, Prop (Prop.Type.Text, "Max.head Down Traverse Distance")]
      double? maxHeadDownTraverseDistance = 25.23;

      [ObservableProperty, Prop ("Laser Heads", 
                                 Prop.Type.Check, "Cut with single head")]
      bool? cutWithSingleHead = true;
   }
}
