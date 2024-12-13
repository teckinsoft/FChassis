namespace FChassis.Data.ViewModel;
public class Configuration {
   // Machine -------------------------------------------------------------------------------------
   // General Tab
   public static Settings.Machine.General.HMIViewModel hmiVM = new ();
   public static Settings.Machine.General.MachineViewModel machineVM = new ();

   // AxisParams Tab
   public static Settings.Machine.AxisParams.XAxisViewModel xAxisVM = new ();
   public static Settings.Machine.AxisParams.YAxisViewModel yAxisVM = new ();
   public static Settings.Machine.AxisParams.ZAxisViewModel zAxisVM = new ();

   public static Settings.Machine.AxisParams.LPC1ViewModel lpc1VM = new ();
   public static Settings.Machine.AxisParams.Pallet1ViewModel pallet1ViewModelVM = new ();

   // TechParams Tab
   public static Settings.Machine.TechParams.AnalogScalingViewModel analogScalingVM = new ();
   public static Settings.Machine.TechParams.MFunctionsViewModel functionsVM = new ();
   public static Settings.Machine.TechParams.ExhaustSysViewModel exhaustSysVM = new ();
   public static Settings.Machine.TechParams.LaserSysViewModel laserTechVM = new ();
   public static Settings.Machine.TechParams.MachineDbViewModel MachineDbVM = new ();

   // PLCOptions Tab
   public static Settings.Machine.PLCOptions.FuncParamViewModel funcParamVM = new ();
   public static Settings.Machine.PLCOptions.ControlParamViewModel controlParamVM = new ();
   public static Settings.Machine.PLCOptions.PLCKeyViewModel plcKeyVM = new ();
   
   // Process Defaults
   public static Settings.Machine.ProcessingDefaults.ImportViewModel importVM = new ();
   public static Settings.Machine.ProcessingDefaults.CutCamViewModel curcamVM = new ();
   public static Settings.Machine.ProcessingDefaults.ProfileCamViewModel profileCamVM = new ();
   public static Settings.Machine.ProcessingDefaults.SequenceViewModel sequenceVM = new ();
   public static Settings.Machine.ProcessingDefaults.WorkSupportViewModel workSupportVM = new ();
   public static Settings.Machine.ProcessingDefaults.SkeletonCutsViewModel skeletonCutsVM = new ();

   
   // WorkOffsets ---------------------------------------------------------------------------------
   public static Settings.WorkOffsetsViewModel workOFfsetsVM = new ();

   // Laser ---------------------------------------------------------------------------------------
   public static Settings.Laser.DCAPageViewModel dcaPageVM = new ();

   // ---- LaserCutting
   // --------- Piercing
   public static Settings.Laser.LaserCutting.Piercing.PeckViewModel peckVM = new ();
   public static Settings.Laser.LaserCutting.Piercing.MultipleViewModel multipleVM = new ();
   public static Settings.Laser.LaserCutting.Piercing.RampViewModel rampVM = new ();
   public static Settings.Laser.LaserCutting.Piercing.SingleViewModel singleVM = new ();
   public static Settings.Laser.LaserCutting.Piercing.NormalViewModel normalVM = new ();
   public static Settings.Laser.LaserCutting.Piercing.GentleViewModel gentleVM = new ();
   public static Settings.Laser.LaserCutting.Piercing.DotPunchViewModel dotpunchVM = new ();

   // --------- Cutting
   public static Settings.Laser.LaserCutting.Cutting.LargeViewModel largeVM = new ();
   public static Settings.Laser.LaserCutting.Cutting.MediumViewModel mediumVM = new ();
   public static Settings.Laser.LaserCutting.Cutting.SmallViewModel smallVM = new ();
   public static Settings.Laser.LaserCutting.Cutting.SpecialViewModel specialVM = new ();
   public static Settings.Laser.LaserCutting.Cutting.PreHoleViewModel preholeVM = new ();

   // --------- Making
   public static Settings.Laser.LaserCutting.Marking.LargeViewModel makingLargeVM = new ();
   public static Settings.Laser.LaserCutting.Marking.MediumViewModel makingMediumVM = new ();
   public static Settings.Laser.LaserCutting.Marking.SmallViewModel makingSmallVM = new ();
   public static Settings.Laser.LaserCutting.Marking.SpecialViewModel makingSpecialVM = new ();

   // --------- Evapourating
   public static Settings.Laser.LaserCutting.Evapourating.LargeViewModel evapouratingLargeVM = new ();
   public static Settings.Laser.LaserCutting.Evapourating.MediumViewModel evapouratingMediumVM = new ();
   public static Settings.Laser.LaserCutting.Evapourating.SmallViewModel evapouratingSmallVM = new ();
}