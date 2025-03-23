using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using FChassis.Core;
using System.ComponentModel;
using System.Runtime.CompilerServices;


namespace FChassis;
using static FChassis.Core.DoubleExtensions;
using static FChassis.Core.IntExtensions;
using static FChassis.Core.MCSettings.EHeads;


/// <summary>Interaction logic for SettingsDlg.xaml</summary>
public partial class SettingsDlg : Window, INotifyPropertyChanged {
   public delegate void OnOkActionDelegate ();
   public event OnOkActionDelegate OnOkAction;
   bool _isRestrictedSettingsVisible;

   public bool IsRestrictedSettingsVisible {
      get => _isRestrictedSettingsVisible;
      set {
         if (_isRestrictedSettingsVisible != value) {
            _isRestrictedSettingsVisible = value;
            OnPropertyChanged ();
         }
      }
   }

   public event PropertyChangedEventHandler PropertyChanged;
   protected void OnPropertyChanged ([CallerMemberName] string propertyName = null) {
      PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (propertyName));
   }
   public MCSettings Settings { get; private set; }
   public bool IsModified { get; private set; }

   public SettingsDlg (MCSettings set) {
      InitializeComponent ();
      DataContext = this;
      
#if DEBUG || TESTRELEASE
       IsRestrictedSettingsVisible = true;
#else
      IsRestrictedSettingsVisible = false;
#endif
      Bind (set);
      tbNotchWireJointDistance.TextChanged += TbNotchWireJointDistanceValueChanged;
   }

   /// <summary>
   /// This method binds the dialog controls with setters and getters
   /// </summary>
   /// <param name="set">MCSettings</param>
   void Bind (MCSettings set) {
      // Set the internal property to the passed settings object
      Settings = set;
      tbStandoff.Bind (() => Settings.Standoff, f => { Settings.Standoff = f.Clamp (0, 100); IsModified = true; });
      tbFlexCuttingGap.Bind (() => Settings.FlexCuttingGap, f => { Settings.FlexCuttingGap = f.Clamp (0, 100); IsModified = true; });
      tbPartition.Bind (() => Settings.PartitionRatio, f => { Settings.PartitionRatio = f.Clamp (0, 1); IsModified = true; });
      tbStepLength.Bind (() => Settings.StepLength, f => { Settings.StepLength = f.Clamp (0.001, 50); IsModified = true; });
      cbPingPong.Bind (() => Settings.UsePingPong, b => { Settings.UsePingPong = b; IsModified = true; });
      cbOptimize.Bind (() => Settings.OptimizePartition, b => { Settings.OptimizePartition = b; IsModified = true; });
      tbMarkText.Bind (() => Settings.MarkText, s => { Settings.MarkText = s; IsModified = true; });
      tbMarkTextHeight.Bind (() => Settings.MarkTextHeight, h => { Settings.MarkTextHeight = h.Clamp (8, 80); IsModified = true; });
      cbMarkTextAngle.ItemsSource = Enum.GetValues (typeof (ERotate)).Cast<ERotate> ().ToList ();
      cbMarkTextAngle.Bind (() => Settings.MarkAngle, s => { Settings.MarkAngle = s; IsModified = true; });
      tbMarkTextPositionX.Bind (() => Settings.MarkTextPosX, f => { Settings.MarkTextPosX = f.Clamp (0.05, 100000); IsModified = true; });
      tbMarkTextPositionY.Bind (() => Settings.MarkTextPosY, f => { Settings.MarkTextPosY = f.Clamp (0.05, 100000); IsModified = true; });

      //lbPriority.Bind (btnPrioUp, btnPrioDown, () => Settings.ToolingPriority, a => Settings.ToolingPriority = [.. a.OfType<EKind> ()]);
      rbBoth.Bind (() => Settings.Heads == Both, () => { Settings.Heads = Both; IsModified = true; });
      rbLeft.Bind (() => Settings.Heads == MCSettings.EHeads.Left,
         () => { Settings.Heads = MCSettings.EHeads.Left; IsModified = true; });
      rbRight.Bind (() => Settings.Heads == Right, () => { Settings.Heads = Right; IsModified = true; });
      rbLHComponent.Bind (() => Settings.PartConfig == MCSettings.PartConfigType.LHComponent,
         () => { Settings.PartConfig = MCSettings.PartConfigType.LHComponent; IsModified = true; });
      rbRHComponent.Bind (() => Settings.PartConfig == MCSettings.PartConfigType.RHComponent,
         () => { Settings.PartConfig = MCSettings.PartConfigType.RHComponent; IsModified = true; });
      tbApproachLength.Bind (() => Settings.ApproachLength, al => { Settings.ApproachLength = al.Clamp (0, 6); IsModified = true; });
      tbNotchApproachLength.Bind (() => Settings.NotchApproachLength,
         al => { Settings.NotchApproachLength = al.Clamp (0, 6); IsModified = true; });
      tbNotchWireJointDistance.Bind (() => Settings.NotchWireJointDistance,
         al => { Settings.NotchWireJointDistance = al.Clamp (0, 5); IsModified = true; });
      tbMinNotchLengthThreshold.Bind (() => Settings.MinNotchLengthThreshold,
         al => { Settings.MinNotchLengthThreshold = al.Clamp (10, 300.0); IsModified = true; });
      tbMinCutOutLengthThreshold.Bind (() => Settings.MinCutOutLengthThreshold,
         al => { Settings.MinCutOutLengthThreshold = al.Clamp (10, 300.0); IsModified = true; });
      cbCutHoles.Bind (() => Settings.CutHoles, b => { Settings.CutHoles = b; IsModified = true; });
      cbCutNotches.Bind (() => Settings.CutNotches, b => { Settings.CutNotches = b; IsModified = true; });
      cbCutCutouts.Bind (() => Settings.CutCutouts, b => { Settings.CutCutouts = b; IsModified = true; });
      cbCutMarks.Bind (() => Settings.CutMarks, b => { Settings.CutMarks = b; IsModified = true; });
      cbShowTlgNames.Bind (() => Settings.ShowToolingNames, b => { Settings.ShowToolingNames = b; IsModified = true; });
      cbShowTlgExtents.Bind (() => Settings.ShowToolingExtents, b => { Settings.ShowToolingExtents = b; IsModified = true; });
      tbMinThresholdPart.Bind (() => Settings.MinThresholdForPartition, b => { Settings.MinThresholdForPartition = b; IsModified = true; });
      tbDinFilenameSuffix.Bind (() => Settings.DINFilenameSuffix, b => { Settings.DINFilenameSuffix = b; IsModified = true; });

      chbMPC.Bind (() => {
         return Settings.EnableMultipassCut;
      },
       b => {
          Settings.EnableMultipassCut = b;
          IsModified = true;
       });

      tbMaxFrameLength.Bind (() => Settings.MaxFrameLength, b => { Settings.MaxFrameLength = b; IsModified = true; });
      tbDeadBandWidth.Bind (() => Settings.DeadbandWidth, b => { Settings.DeadbandWidth = b; IsModified = true; });
      tbDirectoryPath.Bind (() => {
         return Settings.NCFilePath;
      }, b => {
         Settings.NCFilePath = b;
         IsModified = true;
      });
      cbLCMMachine.ItemsSource = Enum.GetValues (typeof (MachineType)).Cast<MachineType> ();
      cbLCMMachine.Bind (() => {
         return Settings.Machine;
      },
         (MachineType selectedType) => {
            Settings.Machine = selectedType;
            IsModified = true;
         });
      tbWPOptions.Bind (() => {
         return Settings.WorkpieceOptionsFilename;
      }, b => {
         if (Settings.WorkpieceOptionsFilename != b) {
            Settings.WorkpieceOptionsFilename = b;
            IsModified = true;
         }
      });

      btnOK.Bind (OnOk);
   }
   private void OnKeyDownHandler (object sender, KeyEventArgs e) {
      if (e.Key == Key.Enter) OnOk ();
      else if (e.Key == Key.Escape) Close ();
   }

   void OnOk () {
      OnOkAction?.Invoke ();
      Close ();
   }

   // Event handler for the Browse button click
   private void OnOutputFolderSelect (object sender, RoutedEventArgs e) {
      // Create an OpenFileDialog to select a folder (we'll trick it for folder selection)
      var dialog = new OpenFileDialog {
         Title = "Select a Folder",
         Filter = "All files (*.*)|*.*",
         CheckFileExists = false,
         ValidateNames = false,
         FileName = "Select folder" // Trick to make it look like folder selection
      };

      // Show the dialog and get the result
      if (dialog.ShowDialog () == true) {
         // Extract the directory path from the selected file
         string selectedDirectory = System.IO.Path.GetDirectoryName (dialog.FileName);
         tbDirectoryPath.Text = selectedDirectory;
      }
   }

   void OnWorkpieceOptionsFileSelect (object sender, RoutedEventArgs e) {
      // Create an OpenFileDialog to select a JSON file
      var dialog = new OpenFileDialog {
         Title = "Select a JSON File",
         Filter = "JSON files (*.json)|*.json",
         CheckFileExists = true,  // This ensures the user selects an existing file
         ValidateNames = true     // Validate that a valid file name is selected
      };

      // Show the dialog and get the result
      if (dialog.ShowDialog () == true) {
         // Get the selected file path
         string selectedFile = dialog.FileName;
         tbWPOptions.Text = selectedFile;
      }
   }
   void TbNotchWireJointDistanceValueChanged (object sender, TextChangedEventArgs e) {
      if (double.TryParse (tbNotchWireJointDistance.Text, out double value) && ( value.SGT(5) || value.SLT(0) )) {
         // Show an error message
         MessageBox.Show ("Notch Wire Joint Distance should lie betweem 0 and 5 mm", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);

         // Optionally, reset the value back to 5
         if (value.GTEQ (5)) {
            tbNotchWireJointDistance.Text = "5";
            Settings.NotchWireJointDistance = value.Clamp (0, 5);
         } else if (value.LTEQ (0)) {
            tbNotchWireJointDistance.Text = "0";
            Settings.NotchWireJointDistance = value.Clamp (0, 5);
         }
      }
   }
}
