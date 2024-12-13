using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Machine.TechParams {
   public partial class LaserSys : ObservableObject {
      [ObservableProperty, Prop ("Custom Parameters 1", 
                                 Prop.Type.Text, "X-Axis Park Position", null!, "mm")]
      int? xAxisParkPosition = 0;

      [ObservableProperty, Prop (Prop.Type.Text, "Y-Axis Park Position", null!, "mm")]
      int? yAxisParkPosition = 0;

      [ObservableProperty, Prop (Prop.Type.Text, "Test Run Feedrate", null!, "mm/min")]
      int? testRunFeedrate = 10000;

      [ObservableProperty, Prop (Prop.Type.Text, "Frog Jump Adjust Distance", null!, "mm")]
      int? frogJumpAdjustDistance = 2;

      [ObservableProperty, Prop (Prop.Type.Text, "Frog Jump Adjust Time", null!, "mm")]
      int? frogJumpAdjustTime = 10;

      [ObservableProperty, Prop (Prop.Type.Text, "Contour End Control Time OFF", null!, "ms")]
      int? contourEndControlTimeOFF = 10;

      [ObservableProperty, Prop (Prop.Type.Text, "Delay before Start of Cut", null!, "ms")]
      double? delaybeforeStartofCut = 0.02;

      [ObservableProperty, Prop (Prop.Type.Text, "Delay after End of Cut", null!, "ms")]
      double? delayafterEndofCut = 0.02;

      [ObservableProperty, Prop (Prop.Type.Text, "Laser HV ON Delay", null!, "s")]
      double? laserHVONDelay = 0.1;


      [ObservableProperty, Prop ("Custom Parameters 2", 
                                 Prop.Type.Text, "High Pressure Valve", null!, "bar")]
      int? highPressureValve = 25;

      [ObservableProperty, Prop (Prop.Type.Text, "Low Pressure Value", null!, "bar")]
      int? lowPressureValve = 5;

      [ObservableProperty, Prop (Prop.Type.Text, "Gas Pressure Difference to Generate Error", null!, "%")]
      int? gasPressureDifferencetoGenerateError = 10;

      [ObservableProperty, Prop (Prop.Type.Text, "High Pressure to Clean Kerf", null!, "bar")]
      int? highPressuretoCleanKerf = 5;

      [ObservableProperty, Prop (Prop.Type.Text, "Focus Ref Voltage", null!, "V")]
      int? focusRefVoltage = 0;

      [ObservableProperty, Prop (Prop.Type.Text, "Focus Offset Voltage", null!, "V")]
      int? focusOffsetVoltage = 0;

      [ObservableProperty, Prop (Prop.Type.Text, "Contour Length/No. of Pierce for Nozzle Cleaning", null!, "mm")]
      int? contourLengthorNoofPierceforNozzleCleaning = 1000;

      [ObservableProperty, Prop (Prop.Type.Text, "Height Control Sensor Gain", null!)]
      int? heightControlSensorGain = 2; 
      
      [ObservableProperty, Prop (Prop.Type.Text, "Height Control Retract Speed", null!, "mm/min")]
      int? heightControlRetractSpeed = 25000;

      [ObservableProperty, Prop (Prop.Type.Text, "Collision Delay Time", null!, "ms")]
      int? collisionDelayTime = 20;


      [ObservableProperty, Prop ("Custom Parameters 3", 
                                 Prop.Type.Text, "Gas Idle Purge Pressure", null!, "bar")]
      double? gasIdlePurgePressure = 0.2;

      [ObservableProperty, Prop (Prop.Type.Text, "Z-Axis Negative Limit - Shuttle Table", null!, "mm")]
      int? zAxisNegativeLimitShuttleTable = 100;

      [ObservableProperty, Prop (Prop.Type.Text, "Exhaust Lag Time to Stop", null!, "s")]
      int? exhaustLagTimetoStop = 20;

      [ObservableProperty, Prop (Prop.Type.Text, "Min Distance between 2 Gantries", null!, "mm")]
      int? minDistancebetween2Gantries = 0;

      [ObservableProperty, Prop (Prop.Type.Text, "Tandem Maximum Machine Stroke", null!, "mm")]
      int? tandemMaximumMachineStroke = 0;

      [ObservableProperty, Prop (Prop.Type.Text, "Gas Idle Purge Time", null!, "s")]
      int? gasIdlePurgetime = 15;

      [ObservableProperty, Prop (Prop.Type.Text, "Gas Purge Pressure before Start of Program", null!, "bar")]
      double? gasPurgePressurebeforeStartofProgram = 2.5;

      [ObservableProperty, Prop (Prop.Type.Text, "Gas Purge Time before Start of Program", null!, "s")]
      int? gasPurgeTimebeforeStartofProgram = 5;

      [ObservableProperty, Prop (Prop.Type.Text, "Z-Axis Park Position", null!, "mm")]
      int? zAxisParkPosition = 290;

      [ObservableProperty, Prop (Prop.Type.Text, "X-Axis Limit Value for Nozzle Cleaning", null!, "mm")]
      int? xAxisLimitValueforNozzleCleaning = 0;


      [ObservableProperty, Prop ("Custom Parameters 4",
                                 Prop.Type.Text, "Piercing Sensor Delay Time", null!, "ms")]
      int? piercingSensorDelayTime = 0;

      [ObservableProperty, Prop (Prop.Type.Text, "Param 2", null!)]
      int? param2 = 0;

      [ObservableProperty, Prop (Prop.Type.Text, "Param 3", null!)]
      int? param3 = 0;

      [ObservableProperty, Prop (Prop.Type.Text, "Param 4", null!)]
      int? param4 = 0;

      [ObservableProperty, Prop (Prop.Type.Text, "Param 5", null!)]
      int? param5 = 0;

      [ObservableProperty, Prop (Prop.Type.Text, "Param 6", null!)]
      int? param6 = 0;

      [ObservableProperty, Prop (Prop.Type.Text, "Param 7", null!)]
      int? param7 = 0;

      [ObservableProperty, Prop (Prop.Type.Text, "Param 8", null!)]
      int? param8 = 0;

      [ObservableProperty, Prop (Prop.Type.Text, "Param 9", null!)]
      int? param9 = 0;

      [ObservableProperty, Prop (Prop.Type.Text, "Param 10", null!)]
      int? param10 = 0;
   }
}
