using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.IO;

namespace FChassis;

/// <summary>All fields that can be set through the Options/Settings dialog</summary>
public class MCSettings : INotifyPropertyChanged {
   #region Constructors
   // Singleton instance
   public static MCSettings It => sIt ??= new ();
   static MCSettings sIt;
   // Notify event, to bind the changes with SettingsDlg
   public event PropertyChangedEventHandler PropertyChanged;

   [JsonConstructor]
   public MCSettings () {
      mToolingPriority = [EKind.Hole, EKind.Notch, EKind.Cutout, EKind.Mark];
      mStandoff = 0.0;
      mMarkText = "Deluxe";
      mPartitionRatio = 0.5;
      mHeads = EHeads.Both;
      mApproachLength = 2;
      PartConfig = PartConfigType.LHComponent;
      MarkTextPosX = 700.4;
      MarkTextPosY = 10.0;
      NotchWireJointDistance = 2.0;
      NotchApproachLength = 5.0;
      mEnableMultipassCut = true;
      mMaxFrameLength = 3500;
      MaximizeFrameLengthInMultipass = true;
      mCutHoles = true;
      mCutNotches = true;
      mCutCutouts = true;
      mCutMarks = true;
      mRotateX180 = false;
      if (System.IO.Directory.Exists ("W:\\FChassis\\Sample"))
         NCFilePath = "W:\\FChassis\\Sample";
      else NCFilePath = "";
      MinThresholdForPartition = 585.0;
      MinNotchLengthThreshold = 210;
      DINFilenameSuffix = "";
      WorkpieceOptionsFilename = @"W:\FChassis\LCM2HWorkpieceOptions.json";
      DeadbandWidth = 600.0;
   }
   #endregion

   #region Delegates and Events
   public delegate void SettingValuesChangedEventHandler ();
   // Any changes to the properties here will also change 
   // elsewhere where the OnSettingValuesChangedEvent is subscribed with
   public event SettingValuesChangedEventHandler OnSettingValuesChangedEvent;
   #endregion

   #region Enums
   public enum ERotate {
      Rotate0, Rotate90, Rotate180, Rotate270
   }
   public enum EHeads {
      Left,
      Right,
      Both,
      None
   }
   public enum PartConfigType {
      LHComponent,
      RHComponent
   }
   #endregion

   #region Helpers 
   // Method to copy values from the deserialized object to the current singleton instance
   private void UpdateFields (MCSettings other) {
      Heads = other.Heads;
      Standoff = other.Standoff;
      ToolingPriority = other.ToolingPriority;
      MarkTextPosX = other.MarkTextPosX;
      MarkTextPosY = other.MarkTextPosY;
      MarkText = other.MarkText;
      MarkAngle = other.MarkAngle;
      OptimizeSequence = other.OptimizeSequence;
      ProgNo = other.ProgNo;
      NCFilePath = other.NCFilePath;
      SafetyZone = other.SafetyZone;
      SerialNumber = other.SerialNumber;
      SyncHead = other.SyncHead;
      UsePingPong = other.UsePingPong;
      PartConfig = other.PartConfig;
      OptimizePartition = other.OptimizePartition;
      RotateX180 = other.RotateX180;
      IncludeFlange = other.IncludeFlange;
      IncludeCutout = other.IncludeCutout;
      IncludeWeb = other.IncludeWeb;
      PartitionRatio = other.PartitionRatio;
      ProbeMinDistance = other.ProbeMinDistance;
      NotchApproachLength = other.NotchApproachLength;
      ApproachLength = other.ApproachLength;
      NotchWireJointDistance = other.NotchWireJointDistance;
      FlexOffset = other.FlexOffset;
      StepLength = other.StepLength;
      EnableMultipassCut = other.EnableMultipassCut;
      MaxFrameLength = other.MaxFrameLength;
      MaximizeFrameLengthInMultipass = other.MaximizeFrameLengthInMultipass;
      CutHoles = other.CutHoles;
      CutNotches = other.CutNotches;
      CutCutouts = other.CutCutouts;
      CutMarks = other.CutMarks;
      MinThresholdForPartition = other.MinThresholdForPartition;
      MinNotchLengthThreshold = other.MinNotchLengthThreshold;
      DINFilenameSuffix = other.DINFilenameSuffix;
      Machine = other.Machine;
      WorkpieceOptionsFilename = other.WorkpieceOptionsFilename;
      ShowToolingNames = other.ShowToolingNames;
      DeadbandWidth = other.DeadbandWidth;
   }
   // Helper method to set a property and raise the event
   private void SetProperty<T> (ref T field, T value) {
      if (!Equals (field, value)) {
         field = value;
         OnSettingValuesChangedEvent?.Invoke ();
      }
   }

   // Method to raise the PropertyChanged event
   protected virtual void OnPropertyChanged ([CallerMemberName] string propertyName = null) {
      PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (propertyName));
   }
   #endregion

   #region Properties
   public EHeads Heads { get => mHeads; set => SetProperty (ref mHeads, value); }
   EHeads mHeads = EHeads.Both;

   /// <summary>Stand-off distance between laser nozzle tip and workpiece</summary>
   public double Standoff { get => mStandoff; set => SetProperty (ref mStandoff, value); }
   double mStandoff;

   public EKind[] ToolingPriority { get => mToolingPriority; set => SetProperty (ref mToolingPriority, value); }
   EKind[] mToolingPriority;
   public double MarkTextPosX { get => mMarkTextPosX; set => SetProperty (ref mMarkTextPosX, value); }
   double mMarkTextPosX;
   public double MarkTextPosY { get => mMarkTextPosY; set => SetProperty (ref mMarkTextPosY, value); }
   double mMarkTextPosY;
   public string MarkText { get => mMarkText; set => SetProperty (ref mMarkText, value); }
   string mMarkText;
   public ERotate MarkAngle { get => mMarkAngle;  set => SetProperty (ref mMarkAngle, value);  }
   ERotate mMarkAngle = ERotate.Rotate0;
   public bool OptimizeSequence { get => mOptimizeSequence; set => SetProperty (ref mOptimizeSequence, value); }
   bool mOptimizeSequence = false;
   public int ProgNo { get => mProgNo; set => SetProperty (ref mProgNo, value); }
   int mProgNo = 1;
   public string NCFilePath {
      get { return mNCFilePath; }
      set {
         SetProperty (ref mNCFilePath, value);
      }
   }
   string mNCFilePath;
   public double SafetyZone { get=> mSafetyZone; set => SetProperty (ref mSafetyZone, value); }
   double mSafetyZone;
   public uint SerialNumber { get=> mSerialNumber; set => SetProperty (ref mSerialNumber, value); }
   uint mSerialNumber;
   public bool SyncHead { get=>mSyncHead; set => SetProperty (ref mSyncHead, value); }
   bool mSyncHead;
   public bool UsePingPong { get=> mUsePingPong; set => SetProperty (ref mUsePingPong, value); }
   bool mUsePingPong = true;
   public PartConfigType PartConfig { get => mPartConfig; set => SetProperty (ref mPartConfig, value); }
   PartConfigType mPartConfig;
   public bool OptimizePartition { get=> mOptimizePartition; set => SetProperty (ref mOptimizePartition, value); }
   bool mOptimizePartition;
   public bool RotateX180 { get=> mRotateX180; set => SetProperty (ref mRotateX180, value); }
   bool mRotateX180;
   public bool ShowToolingNames { get => mShowToolingNames; set => SetProperty(ref mShowToolingNames, value); }
   bool mShowToolingNames;
   public bool IncludeFlange { get=>mIncludeFlange; set => SetProperty (ref mIncludeFlange, value); }
   bool mIncludeFlange;
   public bool IncludeCutout { get=> mIncludeCutout; set => SetProperty (ref mIncludeCutout, value); }
   bool mIncludeCutout;
   public bool IncludeWeb { get=> mIncludeWeb; set => SetProperty (ref mIncludeWeb, value); }
   bool mIncludeWeb;
   public double PartitionRatio { get => mPartitionRatio; set => SetProperty (ref mPartitionRatio, value); }
   double mPartitionRatio;
   public double ProbeMinDistance { get=>mProbeMinDistance; set => SetProperty (ref mProbeMinDistance, value); }
   double mProbeMinDistance;
   public double NotchApproachLength { get=> mNotchApproachLength; set => SetProperty (ref mNotchApproachLength, value); }
   double mNotchApproachLength;
   public double ApproachLength { get => mApproachLength; set => SetProperty (ref mApproachLength, value); }
   double mApproachLength;
   public double NotchWireJointDistance { get=> mNotchWireDistance; set => SetProperty (ref mNotchWireDistance, value); }
   double mNotchWireDistance;
   public double FlexOffset { get => mFlexOffset; set => SetProperty (ref mFlexOffset, value); }
   double mFlexOffset;
   public double StepLength { get => mLengthPerStep; set => SetProperty (ref mLengthPerStep, value); }
   double mLengthPerStep = 1.0;
   public bool EnableMultipassCut { get=> mEnableMultipassCut; set => SetProperty (ref mEnableMultipassCut, value); }
   bool mEnableMultipassCut;
   public double MaxFrameLength { get => mMaxFrameLength; set => SetProperty (ref mMaxFrameLength, value); }
   double mMaxFrameLength;
   public bool MaximizeFrameLengthInMultipass { get=> mMazimizeFrameLengthInMultipass; set => SetProperty (ref mMazimizeFrameLengthInMultipass, value); }
   bool mMazimizeFrameLengthInMultipass;
   public bool CutHoles { get => mCutHoles; set => SetProperty (ref mCutHoles, value); }
   bool mCutHoles;
   public bool CutNotches { get => mCutNotches; set => SetProperty (ref mCutNotches, value); }
   bool mCutNotches;
   public bool CutCutouts { get => mCutCutouts; set => SetProperty (ref mCutCutouts, value); }
   bool mCutCutouts;
   public bool CutMarks { get => mCutMarks; set => SetProperty (ref mCutMarks, value); }
   bool mCutMarks;
   public double MinThresholdForPartition { get => mMinThresholdForPartition; set => SetProperty (ref mMinThresholdForPartition, value); }
   double mMinThresholdForPartition;
   public double MinNotchLengthThreshold { get => mMinNotchLengthThreshold; set => SetProperty (ref mMinNotchLengthThreshold, value); }
   double mMinNotchLengthThreshold;
   public string DINFilenameSuffix { get => mDINFilenameSuffix; set => SetProperty (ref mDINFilenameSuffix, value); }
   string mDINFilenameSuffix;
   public string WorkpieceOptionsFilename { get => mWorkpieceOptionsFilename; set => SetProperty (ref mWorkpieceOptionsFilename, value); }
   string mWorkpieceOptionsFilename;
   public MachineType Machine { get => mMachine; set => SetProperty (ref mMachine, value); }
   MachineType mMachine;
   public double DeadbandWidth { get => mDeadbandWidth; set=>SetProperty(ref mDeadbandWidth, value); }
   double mDeadbandWidth;
   #endregion

   #region Data Members
   JsonSerializerOptions mJSONWriteOptions, mJSONReadOptions;
   #endregion

   #region JSON Read/Write Methods
   // Method to serialize the singleton instance to a JSON file
   public void SaveToJson (string filePath) {
      mJSONWriteOptions ??= new JsonSerializerOptions {
         // For pretty-printing the JSON
         WriteIndented = true, 
         
         // Converts Enums to their string representation
         Converters = { new JsonStringEnumConverter () } 
      };
      var json = JsonSerializer.Serialize (It, mJSONWriteOptions);
      File.WriteAllText (filePath, json);
   }

   // Method to deserialize from JSON and set the singleton instance
   public void LoadFromJson (string filePath) {
      if (File.Exists (filePath)) {
         mJSONReadOptions ??= new JsonSerializerOptions {
            // Converts Enums from their string representation
            Converters = { new JsonStringEnumConverter () } 
         };
         var json = File.ReadAllText (filePath);
         var settings = JsonSerializer.Deserialize<MCSettings> (json, mJSONReadOptions);
         if (settings != null) {
            // Update current instance fields with deserialized values
            UpdateFields (settings);
         }
      }
   }
   #endregion
}
