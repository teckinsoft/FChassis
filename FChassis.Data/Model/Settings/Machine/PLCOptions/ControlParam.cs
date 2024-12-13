using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.PLCOptions; 
public partial class ControlParam : ObservableObject {
   [ObservableProperty, Prop ("Maximum override Adjust Level",
                              Prop.Type.Text, "Maximum Override Adjust Value", "%")]
   int? maximumOverrideAdjustValue = 10;

   [ObservableProperty, Prop ("Height Control 2 Steps Only for Piercing",
                              Prop.Type.Text, "HC Minimum Height for 2 Steps", "mm")]
   int? hcMinimumHeightFor2Steps = 9;


   [ObservableProperty, Prop ("Auto Lubrication Cycle",
                              Prop.Type.Text, "Number of Lubrication Values", null!)]
   int? numberOfLubricationValues = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "X-Axis Lubrication Feedrate", "mm/min")]
   int? xAxisLubricationFeedrate = 10000;

   [ObservableProperty, Prop (Prop.Type.Text, "Y-Axis Lubrication Feedrate", "mm/min")]
   int? yAxisLubricationFeedrate = 10000;

   [ObservableProperty, Prop (Prop.Type.Text, "Lubrication ON Delay", "s")]
   int? lubricationONDelay = 10;


   [ObservableProperty, Prop ("Adjust Gas Pressure Online",
                              Prop.Type.Text, "Maximum Gas Pressure Adjust", "bar")]
   double? maximumGasPressureAdjust = 0.4;

   [ObservableProperty, Prop (Prop.Type.Text, "Gas Pressure Adjust Steps", null!)]
   int? gasPressureAdjustSteps = 4;


   [ObservableProperty, Prop ("Height Sensor Calibration Program",
                              Prop.Type.Text, "Offset Distance from Negative Limit for Slow Speed", "mm")]
   int? offsetDistanceFromNegativeLimitForSlowSpeed = 30;

   [ObservableProperty, Prop (Prop.Type.Text, "Tip Touch Offset Value", "mm")]
   int? tipTouchOffsetValue = 0;


   [ObservableProperty, Prop ("Edge Detection Program",
                              Prop.Type.Text, "Slat Start Point Along X-Axis", "mm")]
   int? slatStartPointAlongXAxis = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "Slat Start Point Along Y-Axis", "mm")]
   int? slatStartPointAlongYAxis = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "Slats Equal Distance Along X-Axis", "mm")]
   int? slatsEqualDistanceAlongXAxis = 10;

   [ObservableProperty, Prop (Prop.Type.Text, "Slats Peak to Peak Distance Along Y-Axis", "mm")]
   int? slatsPeakToPeakDistanceAlongYAxis = 10;

   [ObservableProperty, Prop (Prop.Type.Text, "Edge Correction Offset Along X-Axis", "mm")]
   int? edgeCorrectionOffsetAlongXAxis = 5;

   [ObservableProperty, Prop (Prop.Type.Text, "Edge Correction Offset Along Y-Axis", "mm")]
   int? edgeCorrectionOffsetAlongYAxis = 5;

   [ObservableProperty, Prop (Prop.Type.Text, "Speed to Detect the Edge", "mm/min")]
   int? speedToDetectEdge = 20000;

   [ObservableProperty, Prop (Prop.Type.Check, "Start Height Sensor Calibration before Edge Detect", null!)]
   bool? startHeightSensorCalibrationBeforeEdgeDetect = true;

   [ObservableProperty, Prop (Prop.Type.Check, "Acknowledge the Sheet Origin Point", null!)]
   bool? acknowledgeSheetOriginPoint = true;


   [ObservableProperty, Prop ("Sheet Stopper Specification",
                              Prop.Type.Text, "Number of Stoppers along X-Axis", null!)]
   int? numberOfStoppersAlongXAxis = 3;

   [ObservableProperty, Prop (Prop.Type.Text, "Number of Stoppers along Y-Axis", null!)]
   int? numberOfStoppersAlongYAxis = 2;

   [ObservableProperty, Prop (Prop.Type.Text, "Stopper Width Along Y-Axis", "mm")]
   int? stopperWidthAlongYAxis = 55;

   [ObservableProperty, Prop (Prop.Type.Text, "Distance of First Stopper from Origin along X-Axis", "mm")]
   int? distanceOfFirstStopperFromOriginAlongXAxis = 280;

   [ObservableProperty, Prop (Prop.Type.Text, "Distance between First and Second Stopper along X-Axis", "mm")]
   int? distanceBetweenFirstAndSecondStopperAlongXAxis = 715;

   [ObservableProperty, Prop (Prop.Type.Text, "Distance between Second and Third stopper along X-Axis", "mm")]
   int? distanceBetweenSecondAndThirdstopperAlongXAxis = 990;

   [ObservableProperty, Prop (Prop.Type.Text, "Distance of first Stopper from Origin along Y-Axis", "mm")]
   int ? distanceOfFirstStopperFromOriginAlongYAxis = 290;

   [ObservableProperty, Prop (Prop.Type.Text, "Distance between First and Second Stopper along Y-Axis", "mm")]
   int? distanceBetweenFirstAndSecondStopperAlongYAxis = 595;

   [ObservableProperty, Prop (Prop.Type.Text, "Distance between Second and Third Stopper along Y-Axis", "mm")]
   int? distanceBetweenSecondAndThirdStopperAlongYAxis = 0;


   [ObservableProperty, Prop ("Suction Anti Blow Valves Configuration",
                              Prop.Type.Text, "Number of Anti Blow Valves", null!)]
   int? numberofAntiBlowValves = 4;

   [ObservableProperty, Prop (Prop.Type.Text, "Anti Blow Valve ON Time", "s")]
   double? antiBlowValveONTime = 0.2;

   [ObservableProperty, Prop (Prop.Type.Text, "Anti Blow Valve Wait Time", "s")]
   int? antiBlowValveWaitTime = 20;

   [ObservableProperty, Prop (Prop.Type.Check, "Filter Sensor", null!)]
   bool? filterSensor = false;


   [ObservableProperty, Prop ("Gas Pressure Adjust Levels for Analogure Control",
                              Prop.Type.Text, "HP Gas Regulator Command Correction", "%")]
   int? hpGasRegulatorCommandCorrection = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "HP Gas Regulator Feedback Correction", "%")]
   int? hpGasRegulatorFeedbackCorrection = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "LP Gas Regulator Command Correction", "%")]
   int? lpGasRegulatorCommandCorrection = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "LP Gas Regulator Feedback Correction", "%")]
   int? lpGasRegulatorFeedbackCorrection = 0;


   [ObservableProperty, Prop ("Pallet Changer - Up and Down Motion Hydraulic Control",
                              Prop.Type.Text, "Up Solenoid OFF Delay Time", "s")]
   int? upSolenoidOFFDelayTime = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "Hydraulic Motor OFF Delay Time", "s")]
   int? hydraulicMotorOFFDelayTime = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "Hydraulic Motor ON - Max Time Out", "s")]
   int? hydraulicMotorONMaxTimeOut = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "Fwd/Rev Fast Move Time Out", "s")]
   int? fwdRevFastMoveTimeOut = 30;

   [ObservableProperty, Prop (Prop.Type.Text, "Fwd/Rev Slow Move Time Out", "s")]
   int? fwdRevSlowMoveTimeOut = 20;

   [ObservableProperty, Prop (Prop.Type.Combo, "Fwd/Rev Button Function Type", null!, null!, ["Jog", "Auto"])]
   string? fwdRevButtonFunctionType = "Jog";


   [ObservableProperty, Prop ("Nozzle Cleaning &amp; Height Sensor Calibration Offsets",
                              Prop.Type.Text, "Nozzle Clean X-Offset", "mm")]
   int? nozzleCleanXOffset = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "Nozzle Clean Y-Offset", "mm")]
   int? nozzleCleanYOffset = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "Nozzle Clean Z-Offset", "mm")]
   int? nozzleCleanZOffset = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "HS Calibration X-Offset", "mm")]
   int? hsCalibrationXOffset = 0;

   [ObservableProperty, Prop (Prop.Type.Text, "HS Calibration Y-Offset", "mm")]
   int? hsCalibrationYOffset = 0;


   [ObservableProperty, Prop ("Cutting Head Warning Levels",
                              Prop.Type.Text, "Sensor Insert Temperature", "°C")]
   int? sensorInsertTemperature = 50;

   [ObservableProperty, Prop (Prop.Type.Text, "Plasma Value Percentage", "°C")]
   int? plasmaValuePercentage = 80;

   [ObservableProperty, Prop (Prop.Type.Text, "Protective Window Temperature", "°C")]
   int? protectiveWindowTemperature = 49;

   [ObservableProperty, Prop (Prop.Type.Text, "Collimating Lens Temperature", "°C")]
   int? collimatingLensTemperature = 56;

   [ObservableProperty, Prop (Prop.Type.Text, "Focal Lens Temperature", "°C")]
   int? focalLensTemperature = 53;

   [ObservableProperty, Prop (Prop.Type.Text, "Cutting Head Temperature", "°C")]
   int? cuttingHeadTemperature = 55;

   [ObservableProperty, Prop (Prop.Type.Text, "Diffusion Light Level", null!)]
   int? diffusionLightLevel = 10;


   [ObservableProperty, Prop ("Nozzle Changer Configuration",
                              Prop.Type.Text, "Set Torque Value for Opening", "%")]
   int? setTorqueValueforOpening = 50;

   [ObservableProperty, Prop (Prop.Type.Text, "Set Torque Value for Closing", "%")]
   int? setTorqueValueforClosing = 30;

   [ObservableProperty, Prop (Prop.Type.Text, "Opening Delay Time", "s")]
   int? openingDelayTime = 5;

   [ObservableProperty, Prop (Prop.Type.Text, "Closing Delay Time", "s")]
   int? closingDelayTime = 8;

   [ObservableProperty, Prop (Prop.Type.Text, "Unwinding Nozzle Position", "°")]
   int? unwindingNozzlePosition = 10;

   [ObservableProperty, Prop (Prop.Type.Combo, "Control Method", "ms", null!, 
                              ["Voltage", "Current"])]
   string? controlMethod = "Voltage";

   [ObservableProperty, Prop (Prop.Type.Combo, "Maximum Pressure", "ms")]
   int? maximumPressure = 30;


   [ObservableProperty, Prop ("Laser Pulsing Gate(LPG) delay Time", 
                              Prop.Type.Text, "LPG ON delay", "ms")]
   int? lpgONDelay = 1;

   [ObservableProperty, Prop (Prop.Type.Text, "LPG OFF delay", "ms")]
   int? lpgOFFDelay = 1;


   [ObservableProperty, Prop ("Sealing Gas Pressure Monitor",
                             Prop.Type.Text, "Minimum Warning Level", "mbar")]
   int? minimumWarningLevel = 30;

   [ObservableProperty, Prop (Prop.Type.Text, "Maximum Warning Level", "mbar")]
   int? maximumWarningLevel = 60;

   [ObservableProperty, Prop (Prop.Type.Text, "Minimum Error Level", "mbar")]
   int? minimumErrorLevel = 20;

   [ObservableProperty, Prop (Prop.Type.Text, "Maximum Error Level", "mbar")]
   int? maximumErrorLevel = 60;


   [ObservableProperty, Prop ("Protective Glass Monitor",
                              Prop.Type.Text, "Offline Broken Factor")]
   int? offlineBrokenFactor = 15;

   [ObservableProperty, Prop (Prop.Type.Text, "Online Broken Factor")]
   int? onlineBrokenFactor = 30;

   [ObservableProperty, Prop (Prop.Type.Text, "Warning Limit", "%")]
   int? warningLimit = 60;

   [ObservableProperty, Prop (Prop.Type.Text, "Error Limit", "%")]
   int? errorLimit = 20;

   [ObservableProperty, Prop (Prop.Type.Text, "Delay Time to Report Warning", "s")]
   int? delayTimetoReportWarning = 1;
}
