using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Machine.General;
public partial class HMI : ObservableObject {
   [ObservableProperty, Prop ("General", 
                              Prop.Type.Combo, "Orientation", null!, null!, ["Portrait", "Landscape"])]
   string? orientation = "Portrait";

   [ObservableProperty, Prop (Prop.Type.Text, "Step size to increment")]
   double? stepSizetoIncrement = 10;

   [ObservableProperty, Prop (Prop.Type.Text, "Maximum days keep back up files")]
   int? maximumDaysKeepBackupFiles = 10;

   [ObservableProperty, Prop (Prop.Type.Text, "Minimum storage to keep back up files")]
   int? minimumStoragetoKeepBackupFiles = 10;

   [ObservableProperty, Prop (Prop.Type.Combo, "PLC messages to display", null!, null!, 
                              ["Only error", "Warn & error", "info, warn & error"])]
   string? plcMessagesToDisplay = "Only error";

   [ObservableProperty, Prop (Prop.Type.Check, "Caption for command-bar icons")]
   bool? captionForcommandBarIcons = true;

   [ObservableProperty, Prop (Prop.Type.DBGrid, "Mini player", null!, "gridItems"), 
                                 DBGridColProp (Prop.Type.Text, "ColumnName1", "Col1"),
                                 DBGridColProp (Prop.Type.Text, "ColumnName2", "Col2")]
   bool? miniPlayer = true;

   [ObservableProperty, Prop (Prop.Type.Combo, "Language", null!, null!, ["EN", "FR"])]
   string? language = "EN";

   [ObservableProperty, Prop (Prop.Type.Combo, "Theme", null!, null!, ["Grey", "Blue"])]
   string? theme = "Grey";

   [ObservableProperty, Prop ("Screen size", 
                              Prop.Type.Text, "Width")]
   double? width = 10;
   
   [ObservableProperty, Prop (Prop.Type.Text, "Height")]
   double? height = 10;

   partial void OnWidthChanged (double? oldValue, double? newValue) {
      this.Width = newValue;
   }
}