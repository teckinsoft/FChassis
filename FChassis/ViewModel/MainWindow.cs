using Flux.API;
using FChassis.Processes;

using System.Windows;
using System.IO;
using Microsoft.Win32;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using static FChassis.Processes.Processor;
using FChassis.GCodeGen;
using System.ComponentModel;

namespace FChassis.ViewModel;
public partial class MainWindow : ObservableObject {
   #region Data Members
   Part mPart = null;
   SimpleVM overlay;
   Scene mScene;
   List<Part> mSubParts = [];
   SettingsDlg mSetDlg;
   Processor mProcess;
   readonly string mSrcDir = "W:/FChassis/Sample";
   dynamic dispatcher = null;
   dynamic partListBox = null;
   #endregion

   #region Constructor
   public void Initialize (dynamic dispatcher, dynamic partListBox, object mainWindow) {
      this.dispatcher = dispatcher;
      Library.Init ("W:/FChassis/Data", "C:/FluxSDK/Bin", mainWindow);

      this.partListBox = partListBox;
      this.partListBox.ItemsSource = System.IO.Directory.GetFiles (mSrcDir, "*.fx")
                                             .Select (System.IO.Path.GetFileName);
      Sys.SelectionChanged += OnSelectionChanged;
      this.WindowLoaded ();
   }

   static public UIElement CreateViewerPanel () {
      return (UIElement)Lux.CreatePanel (); }
   #endregion

   #region Event handlers
   void TriggerRedraw ()
      => this.dispatcher.Invoke ((Action)(() => this.overlay?.Redraw ()));

   void ZoomWithExtents (Bound3 bound) 
      => this.dispatcher.Invoke ((Action)(() => mScene.Bound3 = bound));

   void OnSimulationFinished ()
      => Process.SimulationStatus = Processor.ESimulationStatus.NotRunning;

   void OnSelectionChanged (object obj) {
      //Title = obj?.ToString () ?? "NONE";
      this.overlay?.Redraw ();
   }

   void OnProcessPropertyChanged (object sender, PropertyChangedEventArgs e) {
      if (e.PropertyName == nameof (Processor.SimulationStatus)) 
         OnPropertyChanged (nameof (SimulationStatus)); }

   [RelayCommand]
   protected void FileOpen () {
      OpenFileDialog openFileDialog = new () {
         Filter = "STEP Files (*.stp;*.step)|*.stp;*.step|FX Files (*.fx)|*.fx|IGS Files (*.igs;*.iges)|*.igs;*.iges|All files (*.*)|*.*",
         InitialDirectory = @"W:\FChassis\Sample" };

      if (openFileDialog.ShowDialog () == false)
         return;

      // Handle file opening, e.g., load the file into your application
      if (!string.IsNullOrEmpty (openFileDialog.FileName))
         LoadPart (openFileDialog.FileName);
   }

   [RelayCommand]
   protected void ImportFile () {
      OpenFileDialog openFileDialog = new () {
         Filter = "GCode Files (*.din)|*.din|All files (*.*)|*.*",
         InitialDirectory = @"W:\FChassis\Sample" };

      if (openFileDialog.ShowDialog () == false)
         return;

      // Handle file opening, e.g., load the file into your application
      if (!string.IsNullOrEmpty (openFileDialog.FileName)) {
         var extension = System.IO.Path.GetExtension (openFileDialog.FileName).ToLower ();
         if (string.Equals(extension, ".din"))
            LoadGCode (openFileDialog.FileName);
      }
   }

   [RelayCommand]
   protected void UnbendExportDXF () {
      SaveFileDialog saveFileDialog = new () {
         Filter = "DXF files (*.dxf)|*.dxf|All files (*.*)|*.*",
         DefaultExt = "dxf",
      };

      // Show save file dialog box
      if(saveFileDialog.ShowDialog () == false)
         return;

      // Process save file dialog box results
      string filePath = saveFileDialog.FileName;
      try {
         mPart.UnfoldTo2D ();
         var dwg = mPart.Dwg;
         dwg.SaveDXF (filePath);
      } catch (Exception ex) {
         MessageBox.Show ("Error: Could not unfold the part. Original error: " + ex.Message); }
   }

   [RelayCommand]
   protected void UnbendExport2D () {
      if (mPart == null)
         return;

      // Get the original file name (assuming mPart.FileName gives you the file name with extension)
      string originalFileName = System.IO.Path.GetFileNameWithoutExtension (mPart.Info.FileName);
      string originalFileDir = System.IO.Path.GetDirectoryName (mPart.Info.FileName);
      string originalExtension = System.IO.Path.GetExtension (mPart.Info.FileName); // Get the original extension (like .dxf, .geo, etc.)

      // Prepare SaveFileDialog
      SaveFileDialog saveFileDialog = new () {
         Filter = "DXF files (*.dxf)|*.dxf|GEO files (*.geo)|*.geo|All files (*.*)|*.*",
         DefaultExt = "dxf", // Set default file type as DXF
         FileName = originalFileName + ".dxf" // Set default file name as the original file name + .dxf initially
      };

      // Subscribe to the FileOk event to update the file name based on the selected file type
      saveFileDialog.FileOk += (s, args) => {
         // Determine which file type is selected based on the filter index
         if (saveFileDialog.FilterIndex == 1) // DXF selected
            saveFileDialog.FileName = originalFileName + ".dxf";
         else if (saveFileDialog.FilterIndex == 2) // GEO selected
            saveFileDialog.FileName = originalFileName + ".geo";
      };

      // Show save file dialog box
      if(saveFileDialog.ShowDialog () == false)
         return;

      // Process save file dialog box results
      string filePath = System.IO.Path.Combine (originalFileDir, saveFileDialog.FileName);
      string extension = System.IO.Path.GetExtension (filePath)?.ToLower ();

      try {
         // Perform unfolding operation
         mPart.UnfoldTo2D ();
         var dwg = mPart.Dwg;

         // Determine the file type by checking the extension
         if (extension == ".dxf") {
            dwg.SaveDXF (filePath);
         } else if (extension == ".geo") {
            dwg.SaveGEO (filePath); // Assuming you have a method to save GEO files
         } else {
            MessageBox.Show ("Unsupported file type. Please choose either DXF or GEO.");
         }
      } catch (Exception ex) {
         MessageBox.Show ("Error: Could not unfold the part. Original error: " + ex.Message); }
   }

   [RelayCommand]
   protected void FileSave () {
      SaveFileDialog saveFileDialog = new () {
         Filter = "FX files (*.fx)|*.fx|All files (*.*)|*.*",
         DefaultExt = "fx",
         FileName = Path.GetFileName (mPart.Info.FileName),
      };

      if(saveFileDialog.ShowDialog () == false)
         return;

      string filePath = saveFileDialog.FileName;
      try {
         mPart.SaveFX (filePath);
      } catch (Exception ex) {
         MessageBox.Show ("Error: Could not write file to disk. Original error: " + ex.Message); }
   }

   [RelayCommand]
   protected void FileClose () {
      if (Work != null) {
         if (Process != null) {
            if (Process.SimulationStatus == ESimulationStatus.Running ||
            Process.SimulationStatus == ESimulationStatus.Paused) Process.Stop ();
         }

         Work = null;
         Lux.UIScene = null;
         this.overlay = null;
      }

      this.partListBox.SelectedItem = null;
   }

   [RelayCommand]
   protected void OpenSettings () {
      mSetDlg = new SettingsDlg (MCSettings.It);
      mSetDlg.OnOkAction += SaveSettings;
      mSetDlg.ShowDialog ();
   }

   void WindowLoaded () {
      mProcess = new Processor (this.dispatcher);
      mProcess.TriggerRedraw += TriggerRedraw;
      mProcess.SetSimulationStatus += status => SimulationStatus = status;
      mProcess.zoomExtentsWithBound3Delegate += bound => this.dispatcher.Invoke ((Action)(() => ZoomWithExtents (bound)));

      SettingServices.It.LoadSettings (MCSettings.It);
      if (String.IsNullOrEmpty (MCSettings.It.NCFilePath))
         MCSettings.It.NCFilePath = Process?.Workpiece?.NCFilePath ?? "";
   }

   [RelayCommand]
   protected void OpenSanityCheck () {
      mProcess.ResetGCodeGenForTesting ();
      SanityTestsDlg sanityTestsDlg = new (mProcess);
      sanityTestsDlg.ShowDialog ();
   }

   [RelayCommand]
   protected void OnExit () {
      // Get the user's home directory path
      string userHomePath = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);

      // Define the path to the FChassis folder
      string fChassisFolderPath = Path.Combine (userHomePath, "FChassis");

      // Check if the directory exists, if not, create it
      if (!Directory.Exists (fChassisFolderPath))
         Directory.CreateDirectory (fChassisFolderPath);

      // Define the full path to the settings file
      string settingsFilePath = Path.Combine (fChassisFolderPath, "FChassis.User.Settings.JSON");

      // Call the SaveToJson method from the MCSettings singleton to save the JSON file
      MCSettings.It.SaveToJson (settingsFilePath);

      Console.WriteLine ($"Settings file created at: {settingsFilePath}");
   }
   #endregion

   #region Properties
   Processor.ESimulationStatus mSimulationStatus = ESimulationStatus.NotRunning;
   public Processor.ESimulationStatus SimulationStatus {
      get => mSimulationStatus;
      set {
         if (mSimulationStatus != value)
            this.SetProperty (ref mSimulationStatus, value);
      }
   }

   Workpiece mWork;
   public Workpiece Work {
      get => mWork;
      set {
         this.SetProperty (ref mWork, value);
         mProcess.Workpiece = mWork;
      }
   }

   public string Parts_SelectedFileItem {
      get => this.partListBox?.SelectedItem;
      set {
         if (value != null)
            LoadPart (System.IO.Path.Combine (mSrcDir, (string)value));
      }
   }

   public Processor Process {
      get => mProcess;
      set {
         if (mProcess != value) {
            if (mProcess != null) {
               mProcess.PropertyChanged -= OnProcessPropertyChanged;
               this.SetProperty(ref mProcess, value);
               if (mProcess != null) 
                  mProcess.PropertyChanged += OnProcessPropertyChanged;

               OnPropertyChanged (nameof (SimulationStatus));
            }
         }
      }
   }
   #endregion

   #region Draw Related Methods
   void DrawOverlay () {
      DrawTooling ();
      if (Process.SimulationStatus == ESimulationStatus.Running
         || Process.SimulationStatus == ESimulationStatus.Paused)
         Process.DrawGCodeForCutScope ();
      else
         Process.DrawGCode ();

      Process.DrawToolInstance ();
   }

   void DrawTooling () {
      if (Process.SimulationStatus == Processor.ESimulationStatus.NotRunning
         || Process.SimulationStatus == Processor.ESimulationStatus.Paused) {
         Lux.HLR = false;
         Lux.Color = new Color32 (255, 255, 0);
         switch (Sys.Selection) {
            case E3Plane ep:
               Lux.Draw (EMarker2D.CSMarker, ep.Xfm.ToCS (), 25);
               break;

            case E3Flex ef:
               Lux.Draw (EMarker2D.CSMarker, ef.Socket, 25);
               break;

            default:
               if (Work.Cuts.Count == 0)
                  Lux.Draw (EMarker2D.CSMarker, CoordSystem.World, 25);
               break;
         }

         // Draw LH and RH coordinate systems
         if (Work != null) {
            foreach (var cut in Work.Cuts) {
               if (cut.Head == 0)
                  cut.DrawSegs (Utils.LHToolColor, 10);
               else if (cut.Head == 1)
                  cut.DrawSegs (Utils.RHToolColor, 10);
               else
                  cut.DrawSegs (Color32.Yellow, 10);

               if (MCSettings.It.ShowToolingNames) {
                  // Draw the tool names
                  var tName = cut.Name;
                  var pt = cut.Segs[0].Curve.Start;
                  Lux.Color = new Color32 (128, 0, 128);
                  Lux.DrawBillboardText (tName, pt, (float)12);
               }
               if (MCSettings.It.ShowToolingExtents) {
                  // Draw the tool extents
                  var tXMin = $"{cut.XMin:F2}"; var tXMax = $"{cut.XMax:F2}";
                  var ptXMin = new Point3 (cut.XMin, cut.Segs[0].Curve.Start.Y, cut.Segs[0].Curve.Start.Z + 5);
                  var ptXMax = new Point3 (cut.XMax, cut.Segs[0].Curve.Start.Y, cut.Segs[0].Curve.Start.Z + 5);
                  Lux.Color = new Color32 (128, 0, 128);
                  Lux.DrawBillboardText (tXMin, ptXMin, (float)12);
                  Lux.DrawBillboardText (tXMax, ptXMax, (float)12);
               }
            }
         }
      }
   }
   #endregion

   #region Part Preparation Methods
   void LoadPart (string file) {
      file = file.Replace ('\\', '/');
      mPart = Part.Load (file);
      mPart.Info.FileName = file;
      if (mPart.Info.MatlName == "NONE")
         mPart.Info.MatlName = "1.0038";

      try {
         if (mPart.CanExplode) {
            var parts = mPart.ExplodePart ();
            foreach (var part in parts) {
               part.Info.FileName = file;
               mSubParts.Add (part);
            }
         }
      } catch (Exception) { }

      //if (mSubParts.Count > 0) {
      //   mPart = mSubParts[1];
      //   Mechanism lmc = Mechanism.LoadFrom ("C:\\Users\\Parthasarathy.LAP-TK01\\Downloads\\Unnamed-A4003110814_FP002_FINAL_PART.step");
      //} else 
      {
         if (mPart.Model == null) {
            if (mPart.Dwg != null)
               mPart.FoldTo3D ();
            else if (mPart.SurfaceModel != null)
               mPart.SheetMetalize ();
            else
               throw new Exception ("Invalid part");
         }
      }

      this.overlay = new SimpleVM (DrawOverlay);
      Lux.UIScene = mScene = new Scene (new GroupVModel (VModel.For (mPart.Model),
                                        this.overlay), mPart.Model.Bound);

      Work = new Workpiece (mPart.Model, mPart);
      GCodeGenerator.EvaluateToolConfigXForms (null, Work.Bound);

      // Clear the zombies if any
      mProcess?.ClearZombies ();
   }

   [RelayCommand]
   protected void Align () {
      if (!HandleNoWorkpiece ()) {
         Work.Align ();
         mScene.Bound3 = Work.Model.Bound;
         mProcess?.ClearZombies ();
         this.overlay.Redraw ();
      }
   }

   [RelayCommand]
   protected void AddHoles () {
      if (!HandleNoWorkpiece ()) {
         if (Work.DoAddHoles ())
            mProcess?.ClearZombies ();
         this.overlay.Redraw ();
      }
   }

   [RelayCommand]
   protected void TextMarking () {
      if (!HandleNoWorkpiece ()) {
         if (Work.DoTextMarking (MCSettings.It))
            mProcess?.ClearZombies ();

         this.overlay.Redraw ();
      }
   }

   [RelayCommand]
   protected void CutNotches () {
      if (!HandleNoWorkpiece ()) {
         if (Work.DoCutNotchesAndCutouts ())
            mProcess?.ClearZombies ();

         this.overlay.Redraw ();
      }
   }

   [RelayCommand]
   protected void Sorting () {
      if (!HandleNoWorkpiece ()) {
         Work.DoSorting ();
         this.overlay.Redraw ();
      }
   }

   [RelayCommand]
   protected void GenerateGCode () {
      if (!HandleNoWorkpiece ()) {
#if DEBUG
         mProcess.ComputeGCode ();
#else
         try {
            mProcess.ComputeGCode ();
         } catch (Exception ex) {
            if (ex is NegZException) 
               MessageBox.Show ("Part might not be aligned", "Error", 
                                 MessageBoxButton.OK, MessageBoxImage.Error);
            else if (ex is NotchCreationFailedException ex1) 
                  MessageBox.Show (ex1.Message, "Error", 
                                   MessageBoxButton.OK, MessageBoxImage.Error);
            else 
               MessageBox.Show ("G Code generation failed", "Error", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
         }
#endif
         this.overlay.Redraw ();
      }
   }
   #endregion

   #region Simulation Related Methods
   [RelayCommand]
   protected void Simulate () {
      if (!HandleNoWorkpiece ()) {
         Process.SimulationFinished += OnSimulationFinished;
         Task.Run (Process.Run);
      }
   }

   [RelayCommand]
   protected void PauseSimulation () {
      if (!HandleNoWorkpiece ())
         Process.Pause ();
   }

   [RelayCommand]
   protected void StopSimulation () {
      if (!HandleNoWorkpiece ())
         Process.Stop ();
   }
   #endregion

   #region Actionable Methods
   bool HandleNoWorkpiece () {
      if (Work == null) {
         MessageBox.Show ("No Part is Loaded.", "Error",
                           MessageBoxButton.OK, MessageBoxImage.Error);
         return true;
      }

      return false;
   }

   void SaveSettings ()
     => SettingServices.It.SaveSettings (MCSettings.It);

   void LoadGCode (string filename)
      => mProcess.LoadGCode (filename);
   #endregion
}

