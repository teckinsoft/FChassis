using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Machine.ProcessingDefaults {
   public partial class Import : ObservableObject {
       [ObservableProperty, Prop ("Import Settings", 
                                  Prop.Type.Combo, "", null!, null!, 
                                      ["mm","inch"])]
       string? unitForDXFFiles = "mm";

       [ObservableProperty, Prop (Prop.Type.Text, "Stitch Together Lines/Arcs Closer than this")]
       double? stitchTogetherLinesOrArcsCloserThanThis = 0.23;

       [ObservableProperty, Prop (Prop.Type.Text, "Maximum Thickness for Sheet-Metal Part")]
       double? maximumThicknessForSheetMetalPart = 21.52;

       [ObservableProperty, Prop (Prop.Type.Check, "Ignore Layer in DXF/DWG Files")]
       bool? ignoreLayerInDXFOrDWGFiles = false;

       [ObservableProperty, Prop (Prop.Type.Check, "Explode Blocks in 2D Drawing")]
       bool? explodeBlockIn2DDrawing = false;

       [ObservableProperty, Prop (Prop.Type.Check, "Convert White Entities to Black")]
       bool? convertWhiteEntitiesToBlock = true;

       [ObservableProperty, Prop (Prop.Type.Check, "Darken Colors during DXF Import")]
       bool? darkenColorsDuringDXFImport = true;


      [ObservableProperty, Prop ("DXF Settings", 
                                 Prop.Type.Check, "Angles in DXF are Interior Angles")]
      bool? anglesInDXFAreInteriorAngles = true;

      [ObservableProperty, Prop ("Spline Coversion", 
                                 Prop.Type.Combo, "Convert Splines on Import", null!, null!, 
                                    ["Off", "Lines", "Arcs"])]
      string? covertSplinesOnImport = "Lines";


      [ObservableProperty, Prop ("Layer mapping", 
                                  Prop.Type.Text, "Auxilary Layers Names")]
      string? auxilaryLayersNames = "_AUX, AUX, BENDLIMIT";

      [ObservableProperty, Prop (Prop.Type.Text, "Mark Layers Names")]
      string? markLayersNames = "_AUX, AUX, BENDLIMIT";
   }
}
