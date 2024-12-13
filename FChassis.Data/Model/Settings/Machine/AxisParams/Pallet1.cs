using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Machine.AxisParams; 
public partial class Pallet1 : LPC1 {
   [ObservableProperty, Prop (Prop.Type.Text, "Interpolation Filter Time", "s")]
   double? interpolationFilterTime = -1;

   [ObservableProperty,Prop("Encoder", 
                            Prop.Type.Text, "Encoder Fault Detect US")]
   int? encoderFaultDetectUS = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "Encoder Monitoring UA0")]
   int? encoderMonitoringUA0 = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "Encoder Monitoring UA1 UA2")]
   int? encoderMonitoringUA1UA2 = 0;

   [ObservableProperty, Prop ("Control", 
                              Prop.Type.Text, "Analog Offset Compensation", "mV")]
   int? analogOffsetCompensation = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "Analog Quick Stop Time", "ms")]
   int? analogQuickStopTime = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "Position Controller KP")]
   int? positionCControllerKP = 1;

   [ObservableProperty, Prop (Prop.Type.Text, "Position Controller KF")]
   int? positionCControllerKF = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "Position Controller KB")]
   int? positionCControllerKB = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "Position Controller TV")]
   int? positionCControllerTV = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "Position Controller TN", "s")]
   int? positionCControllerTN = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "Position Controller Mode")]
   int? positionCControllerMode = 2;

   [ObservableProperty, Prop ("Resolution", 
                              Prop.Type.Text, "Increment Per Increments")]
   int? incrementPerIncrements = 1048576;

   [ObservableProperty, Prop (Prop.Type.Text, "Distance", "mm")]
   double? averagePower = -1;
   
   [ObservableProperty, Prop ("Monitoring", 
                              Prop.Type.Check, "Software Limit Active")]
   bool? softwareLimitActive = false;

   [ObservableProperty, Prop (Prop.Type.Text, "Software Limit Negative", "mm")]
   int? softwareLimitNegative = 1048576;

   [ObservableProperty, Prop (Prop.Type.Text, "Software Limit Positive", "mm")]
   double? softwareLimitPositive = 6.5;

   [ObservableProperty, Prop (Prop.Type.Text, "Exact Stop Lag Window", "mm")]
   double? exactStopLagWindow = 0.001;

   [ObservableProperty, Prop (Prop.Type.Text, "Exact Stop Time Window", "s")]
   int? exactStopTimeWindow = 0;

   [ObservableProperty, Prop ("Referencing", 
                              Prop.Type.Text, "Homing Velocity2", "m/min")]
   double? homingVelocity2 = 1;

   [ObservableProperty, Prop (Prop.Type.Text, "Homing Acceleration2", "m/sec²")]
   double? homingAcceleration2 = 0.5;

   [ObservableProperty, Prop (Prop.Type.Text, "Homing Mode")]
   double? homingMode = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "Homing Velocity1", "m/min")]
   double? homingVelocity1 = 2.5;

   [ObservableProperty, Prop (Prop.Type.Text, "Homing Acceleration1", "m/sec²")]
   double? homingAcceleration1 = 1;

   [ObservableProperty, Prop (Prop.Type.Text, "Homing Offset", "mm")]
   double? homingOffset = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "Homing Direction and Sequence")]
   double? homingDirectionAndSequence = 0;

   [ObservableProperty, Prop ("Speed &amp; Acceleration", 
                              Prop.Type.Text, "Modal Velocity", "m/min")]
   int? modalVelocity = 10;

   [ObservableProperty, Prop (Prop.Type.Text, "Velocity", "m/min")]
   int? velocity = 30;

   [ObservableProperty, Prop (Prop.Type.Text, "Acceleration", "m/sec²")]
   double? aceleration = 0.2;

   [ObservableProperty, Prop (Prop.Type.Text, "Deceleration", "m/sec²")]
   double? deceleration = 0.2;

   [ObservableProperty, Prop (Prop.Type.Text, "Ramp Time", "ms²")]
   double? rampTime = 0.2;

   [ObservableProperty, Prop ("Corrections",
                              Prop.Type.Text, "Backlash Compensation", "mm")]
   int? backlashCompensation = 0;

   [ObservableProperty, Prop ("Synchronous",
                              Prop.Type.Text, "Synchronous Offset", "mm")]
   int? synchronousOffset = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "Synchronous Position Deviation", "mm")]
   int? synchronousPositionDeviation = 10;


   [ObservableProperty, Prop ("HandWheel",
                              Prop.Type.Text, "Handwheel Assignment")]
   int? handwheelAssignment = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "Handwheel Factor")]
   int? handwheelFactor = 1;
}
