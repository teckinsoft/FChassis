using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Machine.General; 
public partial class Machine : ObservableObject {
   [ObservableProperty, Prop ("General", 
                              Prop.Type.Text, "Machine Id")]
   string machineId = "130166";

   [ObservableProperty, Prop (Prop.Type.Text, "Axis emulation")]
   int axisEmulation = 1;

   [ObservableProperty, Prop (Prop.Type.Text, "Cfg custom tech")]
   string cfgCustomTech = "ECUT";

   [ObservableProperty, Prop (Prop.Type.Text, "Presets")]
   string presets = "G122 X4;G17;S0;T0;G163;G175;M48;G31";

   [ObservableProperty, Prop (Prop.Type.Text, "Can Open Bit Rate", "kbps")]
   string canOpenBitRate = "1000,1000";

   [ObservableProperty, Prop (Prop.Type.Text, "Netdisk Server IP Address")]
   string netDiskServerIPAddr = "1000,1000";

   [ObservableProperty, Prop (Prop.Type.Combo, "Software limit code", null!, null!, 
                              ["12", "13", "14", "15", "16"])]
   string code = "14";

   [ObservableProperty, Prop (Prop.Type.Text, "Override limit")]
   int overrideLimit = 1;

   [ObservableProperty, Prop (Prop.Type.Button, "Limit Incrementer")]
   int incrementLimitCommand;


   [ObservableProperty, Prop ("Controller", 
                              Prop.Type.Text, "Interpolation Cycle Time", "ms")]
   string interpolationCycleTime = "1";

   [ObservableProperty, Prop (Prop.Type.Text, "Interpolation Divider")]
   string interpolationDivider = "2";

   [ObservableProperty, Prop (Prop.Type.Text, "Handwheel Filter Time", "ms")]
   string handWheelFilterTime = "0";

   [ObservableProperty, Prop (Prop.Type.Text, "Velocity", "m/min")]
   string velocity = "130";

   [ObservableProperty, Prop (Prop.Type.Text, "Acceleration", "m/sec²")]
   string acceleration = "40";

   [ObservableProperty, Prop (Prop.Type.Text, "Deceleration", "m/sec²")]
   string deceleration = "40";

   [ObservableProperty, Prop (Prop.Type.Text, "Ramp Time", "ms")]
   string rampTime = "10";

   [ObservableProperty, Prop (Prop.Type.Text, "Position Tolerance MM", "mm")]
   string positionToleranceMM = "0.05";

   [ObservableProperty, Prop (Prop.Type.Text, "Position Tolerance Degree", "°")]
   string positionToleranceDDegree = "0.01";

   [ObservableProperty, Prop (Prop.Type.Text, "Quick Stop Time", "ms")]
   int quickStopTime = 20;

   [ObservableProperty, Prop (Prop.Type.Text, "Creep Speed Velocity", "m/min")]
   string creepSpeedVelocity = "1";


   [ObservableProperty, Prop ("Memory Reservation", 
                              Prop.Type.Text, "Block Count")]
   string blockCount = "1024";

   [ObservableProperty, Prop (Prop.Type.Text, "Reverse Block Count")]
   int reverseBlockCount = 1000;

   [ObservableProperty, Prop (Prop.Type.Text, "Parameter Array Size")]
   int parameterArraySize = 10000;
}