using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using FChassis.GCodeGen;
using FChassis.Processes;
using Flux.API;
using Microsoft.Win32;
using static FChassis.Processes.Processor;
using SPath = System.IO.Path;

namespace FChassis;
/// <summary>Interaction logic for MainWindow.xaml</summary>
public partial class MainWindow : Window, INotifyPropertyChanged {
   #region Data Members
   Part mPart = null;
   SimpleVM mOverlay;
   Scene mScene;
   Workpiece mWork;
   List<Part> mSubParts = [];
   SettingsDlg mSetDlg;
   Processor mProcess;
   Processor.ESimulationStatus mSimulationStatus = ESimulationStatus.NotRunning;
   string mSrcDir = "W:/FChassis/Sample";

   public event PropertyChangedEventHandler PropertyChanged;
   #endregion

   #region Constructor
   UIElement luxPanel = null;
   UserControl joinPanel = null;
   public MainWindow () {
      InitializeComponent ();

      this.DataContext = this;
      Library.Init ("W:/FChassis/Data", "C:/FluxSDK/Bin", this);
      Flux.API.Settings.IGESviaHOOPS = false;

      this.luxPanel = (UIElement)Lux.CreatePanel ();
      Area.Child = this.luxPanel;
      PopulateFilesFromDir (PathUtils.ConvertToWindowsPath (mSrcDir));

      Sys.SelectionChanged += OnSelectionChanged;
#if DEBUG
      SanityCheckMenuItem.Visibility = Visibility.Visible;
#endif
   }
   void PopulateFilesFromDir (string dir) {
      string inputFileType = Environment.GetEnvironmentVariable ("FC_INPUT_FILE_TYPE");
      var fxFiles = new List<string> ();
      if (!string.IsNullOrEmpty (inputFileType) && inputFileType.ToUpper ().Equals ("FX")) {
         // Get FX files if the environment variable is set to "FX"
         fxFiles = System.IO.Directory.GetFiles (dir, "*.fx")
                                       .Select (System.IO.Path.GetFileName)
                                       .ToList ();
      }

      // Get IGES and IGS files
      var allowedExtensions = new[] { ".iges", ".igs", ".step", ".stp", ".dxf", ".step" };
      var igesFiles = System.IO.Directory.GetFiles (dir)
                                          .Where (file => allowedExtensions.Contains (System.IO.Path.GetExtension (file).ToLower ()))
                                          .Select (System.IO.Path.GetFileName)
                                          .ToList ();

      // Combine the two collections
      var allFiles = igesFiles.Concat (fxFiles).ToList ();

      // Assign the combined collection to ItemsSource
      Files.ItemsSource = allFiles;
   }

   #endregion

   #region Event handlers
   void TriggerRedraw ()
      => Dispatcher.Invoke (() => mOverlay?.Redraw ());
   void ZoomWithExtents (Bound3 bound) => Dispatcher.Invoke (() => mScene.Bound3 = bound);
   void OnSimulationFinished () 
      => Process.SimulationStatus = Processor.ESimulationStatus.NotRunning;

   protected virtual void OnPropertyChanged (string propertyName)
      => PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (propertyName));

   void OnFileSelected (object sender, RoutedEventArgs e) {
      if (Files.SelectedItem != null)
         LoadPart (SPath.Combine (mSrcDir, (string)Files.SelectedItem));
   }

   void OnSelectionChanged (object obj) {
      Title = obj?.ToString () ?? "NONE";
      mOverlay?.Redraw ();
   }

   void OnProcessPropertyChanged (object sender, PropertyChangedEventArgs e) {
      if (e.PropertyName == nameof (Processor.SimulationStatus)) {
         OnPropertyChanged (nameof (SimulationStatus));
      }
   }

   void OnMenuFileOpen (object sender, RoutedEventArgs e) {
      OpenFileDialog openFileDialog = new () {
         Filter = "STEP Files (*.stp;*.step)|*.stp;*.step|FX Files (*.fx)|*.fx|IGS Files (*.igs;*.iges)|*.igs;*.iges|All files (*.*)|*.*",
         InitialDirectory = @"W:\FChassis\Sample"
      };
      if (openFileDialog.ShowDialog () == true) {

         // Handle file opening, e.g., load the file into your application
         if (!string.IsNullOrEmpty (openFileDialog.FileName))
            LoadPart (openFileDialog.FileName);
      }
   }

   void OnMenuDirOpen (object sender, RoutedEventArgs e) {
      var dlg = new FolderPicker {
         InputPath = PathUtils.ConvertToWindowsPath (mSrcDir),
      };
      if (dlg.ShowDialog () == true) {
         mSrcDir = dlg.ResultPath;
         PopulateFilesFromDir (mSrcDir);
      }
   }

   void OnMenuImportFile (object sender, RoutedEventArgs e) {
      OpenFileDialog openFileDialog = new () {
         Filter = "GCode Files (*.din)|*.din|All files (*.*)|*.*",
         InitialDirectory = @"W:\FChassis\Sample"
      };
      if (openFileDialog.ShowDialog () == true) {

         // Handle file opening, e.g., load the file into your application
         if (!string.IsNullOrEmpty (openFileDialog.FileName)) {
            var extension = SPath.GetExtension (openFileDialog.FileName).ToLower ();
            if (extension == ".din")
               LoadGCode (openFileDialog.FileName);
         }
      }
   }

   void OnMirrorAndJoin (object sender, RoutedEventArgs e) {
      if (this.Area.Child == this.joinPanel)
         return;

      this.joinPanel ??= new JoinControl ();

      this.Area.Child = this.joinPanel;
   }

   void OnMenuFileSave (object sender, RoutedEventArgs e) {
      SaveFileDialog saveFileDialog = new () {
         Filter = "FX files (*.fx)|*.fx|All files (*.*)|*.*",
         DefaultExt = "fx",
         FileName = Path.GetFileName (mPart.Info.FileName),
      };

      bool? result = saveFileDialog.ShowDialog ();
      if (result == true) {
         string filePath = saveFileDialog.FileName;
         try {
            mPart.SaveFX (filePath);
         } catch (Exception ex) {
            MessageBox.Show ("Error: Could not write file to disk. Original error: " + ex.Message);
         }
      }
   }

   void OnFileClose (object sender, RoutedEventArgs e) {
      if (Work != null) {
         if (Process != null) {
            if (Process.SimulationStatus == ESimulationStatus.Running ||
            Process.SimulationStatus == ESimulationStatus.Paused) Process.Stop ();
         }

         Work = null;
         Lux.UIScene = null;
         mOverlay = null;
      }

      Files.SelectedItem = null;
   }

   void OnSettings (object sender, RoutedEventArgs e) {
      mSetDlg = new SettingsDlg (MCSettings.It);
      mSetDlg.OnOkAction += SaveSettings;
      mSetDlg.ShowDialog ();
   }

   void OnWindowLoaded (object sender, RoutedEventArgs e) {
      mProcess = new Processor (this.Dispatcher);
      mProcess.TriggerRedraw += TriggerRedraw;
      mProcess.SetSimulationStatus += status => SimulationStatus = status;
      mProcess.zoomExtentsWithBound3Delegate += bound => Dispatcher.Invoke (() => ZoomWithExtents (bound));
      
      SettingServices.It.LoadSettings (MCSettings.It);
      if (String.IsNullOrEmpty (MCSettings.It.NCFilePath))
         MCSettings.It.NCFilePath = Process?.Workpiece?.NCFilePath ?? "";
   }

   void OnSanityCheck (object sender, RoutedEventArgs e) {
      mProcess.ResetGCodeGenForTesting ();
      SanityTestsDlg sanityTestsDlg = new (mProcess);
      sanityTestsDlg.ShowDialog ();
   }

   public void OnExit () {
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
   public Processor.ESimulationStatus SimulationStatus {
      get => mSimulationStatus;
      set {
         if (mSimulationStatus != value) {
            mSimulationStatus = value;
            OnPropertyChanged (nameof (SimulationStatus));
         }
      }
   }

   public Workpiece Work {
      get => mWork;
      set {
         mWork = value;
         mProcess.Workpiece = mWork;
         OnPropertyChanged (nameof (Work));
      }
   }

   public Processor Process {
      get => mProcess;
      set {
         if (mProcess != value) {
            if (mProcess != null) {
               mProcess.PropertyChanged -= OnProcessPropertyChanged;
               mProcess = value;
               if (mProcess != null) {
                  mProcess.PropertyChanged += OnProcessPropertyChanged;
               }

               OnPropertyChanged (nameof (Process));
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

         if (mPart.Model == null) {
            if (mPart.Dwg != null)
               mPart.FoldTo3D ();
            else if (mPart.SurfaceModel != null)
               mPart.SheetMetalize ();
            else
               throw new Exception ("Invalid part");
         }

      mOverlay = new SimpleVM (DrawOverlay);
      Lux.UIScene = mScene = new Scene (new GroupVModel (VModel.For (mPart.Model),
                                        mOverlay), mPart.Model.Bound);
      Work = new Workpiece (mPart.Model, mPart);
      
      // Clear the zombies if any
      mProcess?.ClearZombies ();
   }

   void DoAlign (object sender, RoutedEventArgs e) {
      if (!HandleNoWorkpiece ()) {
         Work.Align ();
         mScene.Bound3 = Work.Model.Bound;
         mProcess?.ClearZombies ();
         if (MCSettings.It.RotateX180)
            Work.DeleteCuts ();
         mOverlay.Redraw ();
         GCodeGenerator.EvaluateToolConfigXForms (Work);
      }
   }

   void DoAddHoles (object sender, RoutedEventArgs e) {
      if (!HandleNoWorkpiece ()) {
         if (Work.DoAddHoles ())
            mProcess?.ClearZombies ();
         mOverlay.Redraw ();
      }
   }

   void DoTextMarking (object sender, RoutedEventArgs e) {
      if (!HandleNoWorkpiece ()) {
         if (Work.DoTextMarking (MCSettings.It))
            mProcess?.ClearZombies ();
         mOverlay.Redraw ();
      }
   }

   void DoCutNotches (object sender, RoutedEventArgs e) {
      if (!HandleNoWorkpiece ()) {
         if (Work.DoCutNotchesAndCutouts ())
            mProcess?.ClearZombies ();
         mOverlay.Redraw ();
      }
   }

   void DoSorting (object sender, RoutedEventArgs e) {
      if (!HandleNoWorkpiece ()) {
         Work.DoSorting ();
         mOverlay.Redraw ();
      }
   }

   void DoGenerateGCode (object sender, RoutedEventArgs e) {
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
         mOverlay.Redraw ();
      }
   }
   #endregion

   #region Simulation Related Methods
   void Simulate (object sender, RoutedEventArgs e) {
      if (!HandleNoWorkpiece ()) {
         Process.SimulationFinished += OnSimulationFinished;
         Task.Run (Process.Run);
      }
   }

   void PauseSimulation (object sender, RoutedEventArgs e) {
      if (!HandleNoWorkpiece ())
         Process.Pause ();
   }

   void StopSimulation (object sender, RoutedEventArgs e) {
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

