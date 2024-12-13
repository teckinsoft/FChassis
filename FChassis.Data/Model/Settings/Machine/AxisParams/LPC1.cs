using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Machine.AxisParams;
public partial class LPC1Base : ObservableObject {
   [ObservableProperty, Prop ("Configuration parameters",
                              Prop.Type.Check, "Advanced")]
   bool? advanced;

   [ObservableProperty, Prop (Prop.Type.Combo, "Axis Type", null!, null!,
                              ["Linear Axis", "Rotary Axis"])]
   string? axisType = "Linear Axis";

   [ObservableProperty, Prop (Prop.Type.Combo, "Axis Connection", null!, null!,
                              ["Analog Axis", "CAN Axis", "Virtual Axis", "EtherCAT Axis"])]
   string? axisConnection = "EtherCAT Axis";

   [ObservableProperty, Prop (Prop.Type.Text, "Axis Address")]
   int? axisAddress = 1;
}

public partial class LPC1 : LPC1Base {
   [ObservableProperty,Prop(Prop.Type.Text, "Sync Max Laser Power", "watts")]
   int? maxLaserPower = -1;

   [ObservableProperty, Prop (Prop.Type.Text, "Average Power", "watts")]
   int? averagePower = -1;
}

