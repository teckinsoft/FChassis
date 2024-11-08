using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
namespace FChassis;
using static MCSettings.EHeads;

/// <summary>Interaction logic for SettingsDlg.xaml</summary>
public partial class SettingsDlg : Window {
   public delegate void OnOkActionDelegate ();
   public event OnOkActionDelegate OnOkAction;
   public MCSettings Settings { get; private set; }
   public bool IsModified { get; private set; }
   public SettingsDlg (MCSettings set) {
      InitializeComponent ();
      // Set the internal property to the passed settings object
      Settings = set;
      tbStandoff.Bind (() => Settings.Standoff, f => { Settings.Standoff = f.Clamp (0, 100); IsModified = true; });
      tbPartition.Bind (() => Settings.PartitionRatio, f => {Settings.PartitionRatio = f.Clamp (0, 1); IsModified = true; });
      tbStepLength.Bind (() => Settings.StepLength, f => {Settings.StepLength = f.Clamp (0.001, 50); IsModified = true;});
      cbPingPong.Bind (() => Settings.UsePingPong, b => {Settings.UsePingPong = b; IsModified = true; });
      cbOptimize.Bind (() => Settings.OptimizePartition, b => { Settings.OptimizePartition = b; IsModified = true; });
      tbMarkText.Bind (() => Settings.MarkText, s => { Settings.MarkText = s; IsModified = true; });
      tbMarkTextPositionX.Bind (() => Settings.MarkTextPosX, f => { Settings.MarkTextPosX = f.Clamp(0.05, 100000); IsModified = true; });
      tbMarkTextPositionY.Bind (() => Settings.MarkTextPosY, f => { Settings.MarkTextPosY = f.Clamp(0.05, 100000); IsModified = true; });
      //lbPriority.Bind (btnPrioUp, btnPrioDown, () => Settings.ToolingPriority, a => Settings.ToolingPriority = [.. a.OfType<EKind> ()]);
      rbBoth.Bind (() => Settings.Heads == Both, () => {Settings.Heads = Both; IsModified = true; });
      rbLeft.Bind (() => Settings.Heads == MCSettings.EHeads.Left,
         () => { Settings.Heads = MCSettings.EHeads.Left; IsModified = true; });
      rbRight.Bind (() => Settings.Heads == Right, () => { Settings.Heads = Right; IsModified = true; });
      rbLHComponent.Bind (() => Settings.PartConfig == MCSettings.PartConfigType.LHComponent, 
         () => { Settings.PartConfig = MCSettings.PartConfigType.LHComponent; IsModified = true; });
      rbRHComponent.Bind (() => Settings.PartConfig == MCSettings.PartConfigType.RHComponent, 
         () => { Settings.PartConfig = MCSettings.PartConfigType.RHComponent; IsModified = true; });
      tbApproachLength.Bind (() => Settings.ApproachLength, al => { Settings.ApproachLength = al.Clamp(0, 6); IsModified = true; });
      tbNotchApproachLength.Bind (() => Settings.NotchApproachLength, 
         al => { Settings.NotchApproachLength = al.Clamp(0, 6); IsModified = true; });
      tbNotchWireJointDistance.Bind (() => Settings.NotchWireJointDistance, 
         al => { Settings.NotchWireJointDistance = al.Clamp(0, 6); IsModified = true; });
      tbMinNotchLengthThreshold.Bind (() => Settings.MinNotchLengthThreshold, 
         al => { Settings.MinNotchLengthThreshold = al.Clamp(10, 300.0); IsModified = true; });
      cbCutHoles.Bind (() => Settings.CutHoles, b => { Settings.CutHoles = b; IsModified = true; });
      cbCutNotches.Bind (() => Settings.CutNotches, b => { Settings.CutNotches = b; IsModified = true; });
      cbCutCutouts.Bind (() => Settings.CutCutouts, b => { Settings.CutCutouts = b; IsModified = true; });
      cbCutMarks.Bind (() => Settings.CutMarks, b => { Settings.CutMarks = b; IsModified = true; });
      cbRotate180AbZ.Bind (() => Settings.RotateX180, b => { Settings.RotateX180 = b; IsModified = true; });
      tbMinThresholdPart.Bind (() => Settings.MinThresholdForPartition, b => { Settings.MinThresholdForPartition = b; IsModified = true; });
      tbDinFilenameSuffix.Bind(()=>Settings.DINFilenameSuffix, b=>{ Settings.DINFilenameSuffix = b; IsModified = true; });
      chbMPC.Bind (() => {
         tbMaxFrameLength.IsEnabled = rbMaxFrameLength.IsEnabled = 
         rbMinNotchCuts.IsEnabled = Settings.EnableMultipassCut;
         return Settings.EnableMultipassCut;
      },
       b => {
          Settings.EnableMultipassCut = b; // Update the value
          tbMaxFrameLength.IsEnabled = rbMaxFrameLength.IsEnabled = rbMinNotchCuts.IsEnabled = b; // Enable/disable based on the value
          IsModified = true;
       });
      tbMaxFrameLength.Bind (() => Settings.MaxFrameLength, b => { Settings.MaxFrameLength = b; IsModified = true; });
      rbMaxFrameLength.Bind (() => Settings.MaximizeFrameLengthInMultipass, 
         () => { Settings.MaximizeFrameLengthInMultipass = true; IsModified = true; });
      rbMinNotchCuts.Bind (() => !Settings.MaximizeFrameLengthInMultipass, 
         () => { Settings.MaximizeFrameLengthInMultipass = false; IsModified = true; });
      tbDirectoryPath.Bind (() => {
         return Settings.NCFilePath;
      }, b => {
         Settings.NCFilePath = b;
         IsModified = true;
      });
      cbLCMMachine.ItemsSource = Enum.GetValues (typeof (MachineType)).Cast<MachineType> ();
      cbLCMMachine.Bind (() => {
         chbMPC.IsEnabled = tbMaxFrameLength.IsEnabled = rbMaxFrameLength.IsEnabled =
            rbMinNotchCuts.IsEnabled = (Settings.Machine == MachineType.LCMMultipass2H);
         return Settings.Machine;
      },
         (MachineType selectedType) => {
            chbMPC.IsEnabled = tbMaxFrameLength.IsEnabled = rbMaxFrameLength.IsEnabled =
            rbMinNotchCuts.IsEnabled = Settings.EnableMultipassCut  = (selectedType == MachineType.LCMMultipass2H);
            Settings.Machine = selectedType;
            IsModified = true;
         });
      btnOK.Bind (OnOk);
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
}
