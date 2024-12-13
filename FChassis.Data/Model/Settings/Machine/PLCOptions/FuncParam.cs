using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Machine.PLCOptions; 
public partial class FuncParam : ObservableObject{

   [ObservableProperty, Prop ("PLC Custom Parameters", 
                              Prop.Type.Check, "Advanced")]
   bool? advanced = true;


   [ObservableProperty, Prop ("Jog Parameters",
                              Prop.Type.Text, "Acc/Dcc", "m/sec²")]
   double? accDcc = 12.23;

   [ObservableProperty, Prop (Prop.Type.Text, "Ramp Time", "ms")]
   double? softwareLimitNegative = 98.56;


   [ObservableProperty, Prop ("Miscellaneous",
                              Prop.Type.Text, "Oil Lubrication Cycle Time", "h")]
   double? oilLubricationCycleTime = 12.23;

   [ObservableProperty, Prop (Prop.Type.Text, "Grease Lubrication Cycle Time", "h")]
   double? greaseLubricationCycleTime = 12.23;

   [ObservableProperty, Prop (Prop.Type.Text, "Chip Conveyor Time", "min")]
   double? chipConveyorTime = 12.23;

   [ObservableProperty, Prop (Prop.Type.Combo, "Laser Cut Head Type", null!, null!, 
                              ["No head","Precitec"])]
   string? laserCutHeadType = "No head";

   [ObservableProperty, Prop (Prop.Type.Combo, "Laser Type", null!, null!,
                              ["No laser source","IPG_YLS"])]
   string? laserType = "IPG_YLS";

   [ObservableProperty, Prop (Prop.Type.Text, "Delay for Stop-pause", "ms")]
   double? delayforStopPause = 12.23;

   [ObservableProperty, Prop (Prop.Type.Text, "Delay for Stop-abort", "ms")]
   double? delayforStopAbort = 12.23;

   [ObservableProperty, Prop (Prop.Type.Combo, "Machine Origin", null!, null!,
                              ["X-ve X+ve & Y+ve Y-ve", "X-ve X+ve & Y-ve Y+ve"])]
   string? machineOrgin = "X-ve X+ve & Y+ve Y-ve";

   [ObservableProperty, Prop (Prop.Type.Combo, "Jog Keys", null!, null!,
                              ["Use HMI jog keys", "External axis sel & jog keys"])]
   string? jogKeys = "Use HMI jog keys";

   [ObservableProperty, Prop (Prop.Type.Combo, "Override Keys", null!, null!,
                              ["Use HMI override", "Gray code rotary switch"])]
   string? overrideKeys = "Use HMI override";


   [ObservableProperty, Prop ("Tandem Operation Options",
                              Prop.Type.Check, "Active Tandem Operation")]
   bool? activeTandemOperation = true;

   [ObservableProperty, Prop (Prop.Type.Check, "Deactivate Red Zone Stop")]
   bool? deactivateRedZoneStop = true;


   [ObservableProperty, Prop ("Nozzle Cleaning Options",
                              Prop.Type.Check, "Nozzle Cleaning Active")]
   bool? nozzleCleaningActive = true;

   [ObservableProperty, Prop (Prop.Type.Check, "HC Calibration after Nozzle Clean")]
   bool? hCCalibrationAfterNozzleClean = true;

   [ObservableProperty, Prop (Prop.Type.Check, "X-axis Limit Change")]
   bool? xAxisLimitChange = true;

   [ObservableProperty, Prop (Prop.Type.Combo, "Brush", null!, null!,
                              ["In machine","In Pallet"])]
   string? brush = "In machine";


   [ObservableProperty, Prop ("Auto Nozzle Clean Option",
                              Prop.Type.Combo, "Based on", null!, null!, ["Cutting length","Number of pierce"])]
   string? basedOn = "Cutting length";

   [ObservableProperty, Prop (Prop.Type.Combo, "State of Program", null!, null!,
                              ["Start of program", "Running of program"])]
   string? stateofProgram = "Start of program";


   [ObservableProperty, Prop ("Pallet Changer Configuration",
                              Prop.Type.Combo, "Pallet", null!, null!, ["No pallet", "Single pallet", "Double pallet"])]
   string? pallet = "No pallet";

   [ObservableProperty, Prop (Prop.Type.Check, "Pallet Lock")]
   bool? palletLock = true;

   [ObservableProperty, Prop (Prop.Type.Combo, "Shuttle", null!, null!,
                              ["No shuttle", "One level shuttle","Two level shuttle single drive",
                               "Two level shuttle double drive"])]
   string? shuttle = "No shuttle";

   [ObservableProperty, Prop (Prop.Type.Combo, "Up/Down Motion", null!, null!,
                              ["No up/down motion", "Hydraulic","Induction motor","Servo motor","Free"])]
   string? upDownMotion = "No up/down motion";

   [ObservableProperty, Prop (Prop.Type.Combo, "Forward/Reverse Motion", null!, null!,
                              ["No fed/rev motion", "Hydraulic", "Induction motor", "Servo motor", "Free"])]
   string? forwardReverseMotion = "No fed/rev motion";

   [ObservableProperty, Prop (Prop.Type.Check, "Pallet Door")]
   bool? palletDoor = true;

   [ObservableProperty, Prop (Prop.Type.Check, "Light Barrier")]
   bool? lightBarrier = true;


   [ObservableProperty, Prop ("Laser Technology Miscellaneous Control", 
                              Prop.Type.Check, "Auto Focus active")]
   bool? autoFocusActive = true;

   [ObservableProperty, Prop (Prop.Type.Check, "HC Calibration on Ext. Plate")]
   bool? hCCalibrationonExtPlate = true;

   [ObservableProperty, Prop (Prop.Type.Check, "High Peak Power")]
   bool? hightPeakPower = true;

   [ObservableProperty, Prop (Prop.Type.Check, "HC in 2 Steps for Position > 9mm &amp; Measure Range = 20mm")]
   bool? hCinStepsforPositionMeasureRange = true;

   [ObservableProperty, Prop (Prop.Type.Combo, "High Pressure Valve", null!, null!,
                              ["Voltage control","Current control"])]
   string? hightPressureValve = "No up/down motion";

   [ObservableProperty, Prop (Prop.Type.Combo, "Low Pressure Valve", null!, null!,
                              ["Voltage control", "Current control"])]
   string? lowPressureValve = "No up/down motion";

   [ObservableProperty, Prop (Prop.Type.Check, "Adaptive Optics")]
   bool? adaptiveOptics = true;


   [ObservableProperty, Prop ("Machine Miscellaneous Control",
                              Prop.Type.Check, "External Start Stop")]
   bool? externalStartStop = true;

   [ObservableProperty, Prop (Prop.Type.Check, "Mode Selection via External Keys")]
   bool? modeSelectionViaExternalKeys = true;

   [ObservableProperty, Prop (Prop.Type.Check, "Z-axis Dynamic Enable Bit")]
   bool? zAxisDynamicEbaleBit = true;

   [ObservableProperty, Prop (Prop.Type.Check, "Activate Exhaust System")]
   bool? activateExhaustSystem = true;

   [ObservableProperty, Prop (Prop.Type.Check, "Activate Sheet Edge Function")]
   bool? activeSheetEdgeFunction = true;

   [ObservableProperty, Prop (Prop.Type.Check, "Auto Lubrication Activate")]
   bool? autoLubricationActivate = true;

   [ObservableProperty, Prop (Prop.Type.Check, "Auto Chip Conveyor Activate")]
   bool? autoChipConveyorActivate = true;

   [ObservableProperty, Prop (Prop.Type.Check, "Auto Exhaust Activate")]
   bool? autoExhaustActivate = true;

   [ObservableProperty, Prop (Prop.Type.Check, "Enable Park Position at Program End")]
   bool? enableParkPositionatProgramEnd = true;

   [ObservableProperty, Prop (Prop.Type.Check, "Activate Auto Nozzle Changer")]
   bool? activeAutoNozzleChanger = true;


   [ObservableProperty, Prop ("Emulate Hardware",
                              Prop.Type.Check, "EStop")]
   bool? eStop = true;

   [ObservableProperty, Prop (Prop.Type.Check, "Field Bus Module Device")]
   bool? fieldBusModuleDevice = true;

   [ObservableProperty, Prop (Prop.Type.Check, "Axis Device")]
   bool? axisDevice = true;

   [ObservableProperty, Prop (Prop.Type.Check, "Exhaust Device")]
   bool? exhauseDevice = true;

   [ObservableProperty, Prop (Prop.Type.Check, "Height Control")]
   bool? heightControl = true;

   [ObservableProperty, Prop (Prop.Type.Check, "Laser Device")]
   bool? laserDevice = true;

   [ObservableProperty, Prop (Prop.Type.Check, "No Collision Input")]
   bool? noCollisionInput = true;

   [ObservableProperty, Prop (Prop.Type.Check, "Beam Mode Index")]
   bool? beamModeIndex = true;
}
