using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Machine.AxisParams; 
public partial class Axis : LPC1Base {
   [ObservableProperty, Prop(Prop.Type.Text, "Sync Connection")]
   int? syncConnection = -1;

   [ObservableProperty, Prop(Prop.Type.Text, "Interpolation Filter Time","s")]
   double? interpolationFilterTime = 1.5;

   [ObservableProperty, Prop("Resolution", 
                             Prop.Type.Text, "Increaments per Distance")]
   double? incrementsPerDistance = 1762.23;

   [ObservableProperty, Prop(Prop.Type.Text,"Distance")]
   double? distance = 22.67;


   [ObservableProperty, Prop("Monitoring", 
                             Prop.Type.Text, "Software Limit Active", "mm")]
   bool? softwareLimitActive;

   [ObservableProperty,Prop(Prop.Type.Text, "Software Limit Negative", "mm")]
   double? softwareLimitNegative = 98.56;

   [ObservableProperty,Prop(Prop.Type.Text, "Software Limit Positive", "mm")]
   double? softwareLimitPositive = 54.23;

   [ObservableProperty,Prop(Prop.Type.Text, "Exact Stop Lag Window","mm")]
   double? exactStopLagWindow = 12.23;

   [ObservableProperty,Prop(Prop.Type.Text, "Exact Stop Time Window", "s")]
   double? exactStopTimeWindow = 12.34;


   [ObservableProperty,Prop("Referencing", 
                            Prop.Type.Text, "Homing Velocity2", "m/min")]
   double? homeVelocity2 = 12.23;

   [ObservableProperty,Prop(Prop.Type.Text, "Homing Acceleration2", "m/sec²")]
   double? homingAcceration2 = 89.98;

   [ObservableProperty, Prop (Prop.Type.Text, "Homing Mode", "")]
   int? homingMode = 12;

   [ObservableProperty, Prop (Prop.Type.Text, "Homing Velocity1", "m/min")]
   double? homingVelocity1 = 34.5;

   [ObservableProperty, Prop (Prop.Type.Text, "Homing Acceleration1", "m/sec²")]
   double? homingAcceration1 = 67.89;

   [ObservableProperty, Prop (Prop.Type.Text, "Homing Offset", "mm")]
   double? homingOffset = 12.23;

   [ObservableProperty, Prop (Prop.Type.Text, "Homing Direction and Sequence", "")]
   int? homingDirectionAndSequence = 3;


   [ObservableProperty, Prop ("Speed & Acceleration", 
                              Prop.Type.Text, "Moal Velocity", "m/min")]
   double? modalVelocity = 12.3;

   [ObservableProperty, Prop (Prop.Type.Text, "Velocity", "m/min")]
   double? velocity = 12.3;

   [ObservableProperty, Prop (Prop.Type.Text, "Acceleration", "m/min")]
   double? acceleration = 12.3;

   [ObservableProperty, Prop (Prop.Type.Text, "Decceleration", "m/sec²")]
   double? deceleration = 12.3;

   [ObservableProperty, Prop (Prop.Type.Text, "Ramp Time", "ms")]
   double? rampTime = 12.3;

   [ObservableProperty, Prop ("Corrections", 
                              Prop.Type.Text, "Blacklash Compensation", "mm")]
   double? backlashCompensation = 12.52;

   [ObservableProperty, Prop ("Synchronous", 
                               Prop.Type.Text, "Synchronous Offset", "mm")]
   double? synchronousOffset = 12.36;

   [ObservableProperty, Prop (Prop.Type.Text, "Synchronous position deviation", "mm")]
   double? synchronousPositionDeviation = 12.3;


   [ObservableProperty, Prop ("Handwheel", 
                              Prop.Type.Text, "Handwheel assignment")]
   int? handwheelAssignment = 1;

   [ObservableProperty, Prop ("Handwheel", Prop.Type.Text, "Handwheel factor")]
   double? handwheelFactor = 12.3;
}
