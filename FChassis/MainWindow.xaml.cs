using System.ComponentModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;

using Flux.API;
using FChassis.Draw;
using FChassis.Core;
using FChassis.Core.GCodeGen;
using FChassis.Core.Processes;
using FChassis.Input;


using SPath = System.IO.Path;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FChassis.Core.AssemblyUtils;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text;
using MessagePack;
using System.Runtime.Serialization;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace FChassis;
/// <summary>Interaction logic for MainWindow.xaml</summary>
public partial class MainWindow : Window, INotifyPropertyChanged {
   #region Fields
   Part mPart = null;
   SimpleVM mOverlay;
   Scene mScene;
   Workpiece mWork;
   List<Part> mSubParts = [];
   SettingsDlg mSetDlg;
   GenesysHub mGHub;
   ProcessSimulator mProcessSimulator;
   ProcessSimulator.ESimulationStatus mSimulationStatus = ProcessSimulator.ESimulationStatus.NotRunning;
   string mSrcDir = "W:/FChassis/Sample";
   
   [IgnoreDataMember]
   Dictionary<string, string> mRecentFilesMap = [];
   public bool IsIgesAvailable { get; }
   public ProcessSimulator.ESimulationStatus SimulationStatus {
      get => mSimulationStatus;
      set {
         if (mSimulationStatus != value) {
            mSimulationStatus = value;
            OnPropertyChanged (nameof (SimulationStatus));
         }
      }
   }

   public event PropertyChangedEventHandler PropertyChanged;
   #endregion

   #region Constructor
   public MainWindow () {
      InitializeComponent ();

      this.DataContext = this;
      Library.Init ("W:/FChassis/Data", "C:/FluxSDK/Bin", this);
      Flux.API.Settings.IGESviaHOOPS = false;

      Area.Child = (UIElement)Lux.CreatePanel ();
      PopulateFilesFromDir (PathUtils.ConvertToWindowsPath (mSrcDir));

      Sys.SelectionChanged += OnSelectionChanged;
#if DEBUG
      IsIgesAvailable = AssemblyLoader.IsAssemblyLoadable ("igesd");
#else
      IsIgesAvailable = AssemblyLoader.IsAssemblyLoadable ("iges");
#endif

#if DEBUG
      IsSanityCheckVisible = true;
#else
      IsSanityCheckVisible = false;
#endif

#if DEBUG || TESTRELEASE
      IsTextMarkingOptionVisible = true;
#else
      IsTextMarkingOptionVisible = false;
#endif
   }

   bool _isSanityCheckVisible;
   public bool IsSanityCheckVisible {
      get => _isSanityCheckVisible;
      set {
         if (_isSanityCheckVisible != value) {
            _isSanityCheckVisible = value;
            OnPropertyChanged ();
         }
      }
   }

   bool _isTextMarkingOptionVisible;
   public bool IsTextMarkingOptionVisible {
      get => _isTextMarkingOptionVisible;
      set {
         if (_isTextMarkingOptionVisible != value) {
            _isTextMarkingOptionVisible = value;
            OnPropertyChanged ();
         }
      }
   }

   protected void OnPropertyChanged ([CallerMemberName] string propertyName = null)
       => PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (propertyName));
   void UpdateInputFilesList (List<string> files) => Dispatcher.Invoke (() => Files.ItemsSource = files);

   void PopulateFilesFromDir (string dir) {
      string inputFileType = Environment.GetEnvironmentVariable ("FC_INPUT_FILE_TYPE");
      var fxFiles = new List<string> ();
      if (!string.IsNullOrEmpty (inputFileType) && inputFileType.ToUpper ().Equals ("FX")) {
         // Get FX files if the environment variable is set to "FX"
         fxFiles = [.. System.IO.Directory.GetFiles (dir, "*.fx").Select (System.IO.Path.GetFileName)];
      }

      // Get IGES and IGS files
      var allowedExtensions = new[] { ".iges", ".igs", ".step", ".stp", ".dxf", ".step", ".csv" };
      var igesFiles = System.IO.Directory.GetFiles (dir)
                                          .Where (file => allowedExtensions.Contains (System.IO.Path.GetExtension (file).ToLower ()))
                                          .Select (System.IO.Path.GetFileName)
                                          .ToList ();

      // Combine the two collections
      var allFiles = igesFiles.Concat (fxFiles).ToList ();

      // Assign the combined collection to ItemsSource
      UpdateInputFilesList (allFiles);
   }
   #endregion

   #region Event handlers
   void TriggerRedraw ()
      => Dispatcher.Invoke (() => mOverlay?.Redraw ());
   void ZoomWithExtents (Bound3 bound) => Dispatcher.Invoke (() => mScene.Bound3 = bound);
   void OnSimulationFinished ()
      => mProcessSimulator.SimulationStatus = ProcessSimulator.ESimulationStatus.NotRunning;

   //protected virtual void OnPropertyChanged (string propertyName)
   //   => PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (propertyName));

   void OnFileSelected (object sender, RoutedEventArgs e) {
      if (Files.SelectedItem != null)
         LoadPart (SPath.Combine (mSrcDir, (string)Files.SelectedItem));
   }

   void OnSelectionChanged (object obj) {
      Title = obj?.ToString () ?? "NONE";
      mOverlay?.Redraw ();
   }

   void OnProcessPropertyChanged (object sender, PropertyChangedEventArgs e) {
      if (e.PropertyName == nameof (ProcessSimulator.SimulationStatus))
         OnPropertyChanged (nameof (SimulationStatus));
   }

   void OnMenuFileOpen (object sender, RoutedEventArgs e) {
      OpenFileDialog openFileDialog;
      string inputFileType = Environment.GetEnvironmentVariable ("FC_INPUT_FILE_TYPE");
      if (!string.IsNullOrEmpty (inputFileType) && inputFileType.ToUpper ().Equals ("FX")) {
         openFileDialog = new () {
            Filter = "IGS Files (*.igs;*.iges)|*.igs;*.iges|STEP Files (*.stp;*.step)|*.stp;*.step|FX Files (*.fx)|*.fx|CSV Files (*.csv)|*.csv|All files (*.*)|*.*",
            InitialDirectory = @"W:\FChassis\Sample"
         };
      } else {
         openFileDialog = new () {
            Filter = "IGS Files (*.igs;*.iges)|*.igs;*.iges|STEP Files (*.stp;*.step)|*.stp;*.step|CSV Files (*.csv)|*.csv|All files (*.*)|*.*",
            InitialDirectory = @"W:\FChassis\Sample"
         };
      }

      if (openFileDialog.ShowDialog () == true) {
         // Handle file opening, e.g., load the file into your application
         if (!string.IsNullOrEmpty (openFileDialog.FileName))
            LoadPart (openFileDialog.FileName);
      }
   }


   void OnFilesHeaderItemClicked (object sender, RoutedEventArgs e) {
      string userHomePath = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
      string fChassisFolderPath = System.IO.Path.Combine (userHomePath, "FChassis");
      string recentFilesJSONFilePath = System.IO.Path.Combine (fChassisFolderPath, "FChassis.User.RecentFiles.JSON");

      mRecentFilesMap = LoadRecentFilesFromJSON (recentFilesJSONFilePath);

      // Rebuild the observable collection
      RecentFiles.Clear ();

      if (mRecentFilesMap is { Count: > 0 }) {
         static DateTimeOffset ParseTs (string ts) =>
             DateTimeOffset.TryParse (ts, out var dto) ? dto : DateTimeOffset.MinValue;

         foreach (var kv in mRecentFilesMap.OrderByDescending (kv => ParseTs (kv.Value))) {
            RecentFiles.Add ($"{kv.Key}\t{kv.Value}");
         }
      }
   }


   void OnRecentFileItemClick (object sender, RoutedEventArgs e) {
      if (sender is not MenuItem mi || mi.Header is not string line || string.IsNullOrWhiteSpace (line))
         return;

      const int TimestampLen = 19; // "yyyy-MM-dd HH:mm:ss"
      string path = line;

      if (line.Length >= TimestampLen + 1) // +1 for the separating space
      {
         // drop the timestamp and the preceding space
         path = line[..^(TimestampLen + 1)];
      }

      path = path.Trim ();

      if (!File.Exists (path)) {
         MessageBox.Show ($"File not found:\n{path}", "Open Recent", MessageBoxButton.OK, MessageBoxImage.Warning);
         return;
      }

      LoadPart (path);
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

   void OnJoin (object sender, RoutedEventArgs e) {
      JoinWindow joinWindow = new ();

      // Subscribe to the FileSaved event
      joinWindow.joinWndVM.EvMirrorAndJoinedFileSaved += OnMirrorAndJoinedFileSaved;
      joinWindow.joinWndVM.EvLoadPart += LoadPart;

      joinWindow.ShowDialog ();
      joinWindow.Dispose ();
   }

   void OnMirrorAndJoinedFileSaved (string savedDirectory) {
      // Check if the saved file's directory matches MainWindow's mSrcDir
      if (string.Equals (System.IO.Path.GetFullPath (savedDirectory), System.IO.Path.GetFullPath (mSrcDir), StringComparison.OrdinalIgnoreCase)) {
         // Refresh file list
         PopulateFilesFromDir (mSrcDir);
      }
   }

   void OnMenuFileSave (object sender, RoutedEventArgs e) {
      SaveFileDialog saveFileDialog = new () {
         Filter = "FX files (*.fx)|*.fx|All files (*.*)|*.*",
         DefaultExt = "fx",
         FileName = System.IO.Path.GetFileName (mPart.Info.FileName),
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
      WriteRecentFiles ();
      if (Work != null) {
         if (mProcessSimulator != null) {
            if (mProcessSimulator.SimulationStatus == ProcessSimulator.ESimulationStatus.Running ||
            mProcessSimulator.SimulationStatus == ProcessSimulator.ESimulationStatus.Paused) mProcessSimulator.Stop ();
         }

         Work = null;
         Lux.UIScene = null;
         mOverlay = null;
      }

      Files.SelectedItem = null;
   }

   void OnSettings (object sender, RoutedEventArgs e) {
      mSetDlg = new (MCSettings.It);
      mSetDlg.OnOkAction += () => { if (mSetDlg.IsModified) SaveSettings (); };
      mSetDlg.ShowDialog ();
   }

   void OnAboutClick (object sender, RoutedEventArgs e) {
      AboutWindow aboutWindow = new () {
         Owner = this
      };
      aboutWindow.InitializeComponent ();
      aboutWindow.ShowDialog ();
   }

   void OnWindowLoaded (object sender, RoutedEventArgs e) {
      GenesysHub = new ();
      mProcessSimulator = new (mGHub, this.Dispatcher);
      mProcessSimulator.TriggerRedraw += TriggerRedraw;
      mProcessSimulator.SetSimulationStatus += status => SimulationStatus = status;
      //mProcessSimulator.zoomExtentsWithBound3Delegate += bound => Dispatcher.Invoke (() => ZoomWithExtents (bound));

      SettingServices.It.LoadSettings (MCSettings.It);
      if (String.IsNullOrEmpty (MCSettings.It.NCFilePath))
         MCSettings.It.NCFilePath = mGHub?.Workpiece?.NCFilePath ?? "";
   }

   void OnSanityCheck (object sender, RoutedEventArgs e) {
      mGHub.ResetGCodeGenForTesting ();
      SanityTestsDlg sanityTestsDlg = new (mGHub);
      sanityTestsDlg.ShowDialog ();
   }

   protected override void OnClosing (CancelEventArgs e) {
      try {
         // ~/FChassis
         string userHomePath = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
         string fChassisFolderPath = Path.Combine (userHomePath, "FChassis");
         Directory.CreateDirectory (fChassisFolderPath);

         // Settings JSON
         string settingsFilePath = Path.Combine (fChassisFolderPath, "FChassis.User.Settings.JSON");
#if DEBUG || TESTRELEASE
         MCSettings.It.SaveSettingsToJsonASCII (settingsFilePath);
#else
      MCSettings.It.SaveSettingsToJson(settingsFilePath);
#endif

         WriteRecentFiles ();

         System.Diagnostics.Debug.WriteLine ($"Settings file created at: {settingsFilePath}");
      } catch (Exception ex) {
         // Don’t block app shutdown on save errors
         System.Diagnostics.Debug.WriteLine ($"Error saving on close: {ex}");
      }

      base.OnClosing (e);
   }

   void WriteRecentFiles () {
      try {
         string userHomePath = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
         string fChassisFolderPath = Path.Combine (userHomePath, "FChassis");
         Directory.CreateDirectory (fChassisFolderPath);
         string recentFilesJSONPath = System.IO.Path.Combine (fChassisFolderPath, "FChassis.User.RecentFiles.JSON");
         string timeStamp = DateTime.Now.ToString ("yyyy-MM-dd HH:mm:ss");
         mRecentFilesMap[mPart.Info.FileName] = timeStamp;
         TrimRecentFilesMap (mRecentFilesMap);

         // Recent files JSON (MCSettings manages mRecentFilesMap internally)
         SaveRecentFilesToJSON (mRecentFilesMap, recentFilesJSONPath);
      } catch (Exception) {
      }
   }

   static void TrimRecentFilesMap (Dictionary<string,string> map) {
      const int MaxEntries = 30;

      if (map == null || map.Count <= MaxEntries)
         return;

      static DateTimeOffset ParseTs (string ts) =>
          DateTimeOffset.TryParse (ts, out var dto) ? dto : DateTimeOffset.MinValue;

      map = map
          .OrderByDescending (kv => ParseTs (kv.Value))   // newest first
          .Take (MaxEntries)                             // keep only top 30
          .ToDictionary (kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
   }
   #endregion

   #region Properties
   public ObservableCollection<string> RecentFiles { get; set; } = [];

   public Workpiece Work {
      get => mWork;
      set {
         mWork = value;
         mGHub.Workpiece = mWork;
         OnPropertyChanged (nameof (Work));
      }
   }

   public GenesysHub GenesysHub {
      get => mGHub;
      set {
         if (mGHub != value)  // Check if value is different
         {
            if (mGHub != null) {
               mGHub.PropertyChanged -= OnProcessPropertyChanged;
            }

            mGHub = value;  // Ensure the new value is assigned

            if (mGHub != null) {
               mGHub.PropertyChanged += OnProcessPropertyChanged;
            }

            OnPropertyChanged (nameof (GenesysHub));
            OnPropertyChanged (nameof (SimulationStatus));
         }
      }
   }

   public ProcessSimulator ProcessSimulator {
      get => mProcessSimulator;
      set {
         if (mProcessSimulator != value) {
            if (mProcessSimulator != null) {
               mProcessSimulator.PropertyChanged -= OnProcessPropertyChanged;
               mProcessSimulator = value;
               if (mProcessSimulator != null) {
                  mProcessSimulator.PropertyChanged += OnProcessPropertyChanged;
               }

               OnPropertyChanged (nameof (mProcessSimulator));
               OnPropertyChanged (nameof (SimulationStatus));
            }
         }
      }
   }
   #endregion

   #region Draw Related Methods
   void DrawOverlay () {
      DrawTooling ();
      if (mProcessSimulator.SimulationStatus == ProcessSimulator.ESimulationStatus.Running
         || mProcessSimulator.SimulationStatus == ProcessSimulator.ESimulationStatus.Paused)
         mProcessSimulator.DrawGCodeForCutScope ();
      else
         mProcessSimulator.DrawGCode ();
      mProcessSimulator.DrawToolInstance ();
   }

   void DrawTooling () {
      if (mProcessSimulator.SimulationStatus == ProcessSimulator.ESimulationStatus.NotRunning
         || mProcessSimulator.SimulationStatus == ProcessSimulator.ESimulationStatus.Paused) {
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
      //string userHomePath = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
      //string fChassisFolderPath = System.IO.Path.Combine (userHomePath, "FChassis");
      //string recentFilesJSONPath = System.IO.Path.Combine (fChassisFolderPath, "FChassis.User.RecentFiles.JSON");
      //string timeStamp = DateTime.Now.ToString ("yyyy-MM-dd HH:mm:ss");
      ////SaveRecentFilesToJSON (file, timeStamp, recentFilesJSONPath);
      //mRecentFilesMap[file] = timeStamp;
      //static DateTimeOffset ParseTs (string ts)
      //      => DateTimeOffset.TryParse (ts, out var dto) ? dto : DateTimeOffset.MinValue;
      //RecentFiles = [];
      //foreach (var kv in mRecentFilesMap.OrderByDescending (kv => ParseTs (kv.Value))) {
      //   RecentFiles.Add ($"{kv.Key} {kv.Value}");
      //}
      file = file.Replace ('\\', '/');

      // Create DXF file from .CSV file.
      bool isCsv = System.IO.Path.GetExtension (file).Equals (".csv", StringComparison.OrdinalIgnoreCase);
      if (isCsv) {
         var origCSVFile = file;
         try {
            var csvPartData = CsvReader.ReadPartData (file);
            file += ".dxf";
            FChassis.Input.DXFWriter dxfW = new (file, csvPartData);
            dxfW.WriteDXF ();
            PopulateFilesFromDir (mSrcDir);
         } catch (Exception ex) {
            MessageBox.Show ($"Part defined by {origCSVFile} can not be created. Error: {ex.Message}"
            , "Error", MessageBoxButton.OK, MessageBoxImage.Error);
         }
      }
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
         try {
            if (mPart.Dwg != null)
               mPart.FoldTo3D ();
            else if (mPart.SurfaceModel != null)
               mPart.SheetMetalize ();
            else
               throw new Exception ("Invalid part");
         } catch (NullReferenceException) {
            MessageBox.Show ($"Part {mPart.Info.FileName} is invalid"
            , "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
         } catch (Exception) {
            MessageBox.Show ($"Part {mPart.Info.FileName} is invalid"
            , "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
         }
      }
      //mPart.Dwg?.SaveDXF ("C:\\temp\\xyz.dxf");

      mOverlay = new SimpleVM (DrawOverlay);
      Lux.UIScene = mScene = new Scene (new GroupVModel (VModel.For (mPart.Model),
                                        mOverlay), mPart.Model.Bound);
      Work = new Workpiece (mPart.Model, mPart);

      // Clear the zombies if any
      GenesysHub?.ClearZombies ();
   }
   bool _cutHoles = false, _cutNotches = false, _cutMarks = false;
   void DoAlign (object sender, RoutedEventArgs e) {
      if (!HandleNoWorkpiece ()) {
         Work.Align ();
         mScene.Bound3 = Work.Model.Bound;
         GenesysHub?.ClearZombies ();
         if (Work.Dirty) {
            Work.DeleteCuts ();
            _cutHoles = false; _cutNotches = false;
            _cutMarks = false;
         }
         mOverlay.Redraw ();
         GCodeGenerator.EvaluateToolConfigXForms (Work);
      }
   }

   void DoAddHoles (object sender, RoutedEventArgs e) {
      if (!HandleNoWorkpiece () && !_cutHoles) {
         if (Work.DoAddHoles ())
            GenesysHub?.ClearZombies ();
         _cutHoles = true;
         mOverlay.Redraw ();
      }
   }

   void DoTextMarking (object sender, RoutedEventArgs e) {
      if (!HandleNoWorkpiece () && !_cutMarks) {
         if (Work.DoTextMarking (MCSettings.It))
            GenesysHub?.ClearZombies ();
         _cutMarks = true;
         mOverlay.Redraw ();
      }
   }

   void DoCutNotches (object sender, RoutedEventArgs e) {
      if (!HandleNoWorkpiece () && !_cutNotches) {
         if (Work.DoCutNotchesAndCutouts ())
            GenesysHub?.ClearZombies ();
         _cutNotches = true;
         mOverlay.Redraw ();
      }
   }
   void DoRefresh (object sender, RoutedEventArgs e) => mOverlay?.Redraw ();
   void DoSorting (object sender, RoutedEventArgs e) {
      if (!HandleNoWorkpiece ()) {
         Work.DoSorting ();
         mOverlay.Redraw ();
      }
   }

   void DoGenerateGCode (object sender, RoutedEventArgs e) {

      if (!HandleNoWorkpiece ()) {

#if DEBUG || TESTRELEASE
         GenesysHub.ComputeGCode ();
         string jsonPath1 = "W:\\Fchassis\\" + System.IO.Path.GetFileNameWithoutExtension (mPart.Info.FileName) + "_Spatial_TimeStats.json";
         var timeStats1 = Utils.ReadJsonFile<Dictionary<string, double>> (jsonPath1);
         string jsonPath2 = "W:\\Fchassis\\" + System.IO.Path.GetFileNameWithoutExtension (mPart.Info.FileName) + "_TimeOptimal_TimeStats.json";
         var timeStats2 = Utils.ReadJsonFile<Dictionary<string, double>> (jsonPath2);

         try {
            string message =
            $"{"Metric(Secs)",-35} {"Spacial",15} {"TimeOptimal",15}\n" +
            new string ('-', 70) + "\n" +
            $"{"Total Time",-30} {timeStats1["TotalTime"],15:F2} {timeStats2["TotalTime"],15:F2}     \n" +
            $"{"Total Idle Time",-25} {timeStats1["TotalIdleTime"],15:F2} {timeStats2["TotalIdleTime"],15:F2}     \n" +
            $"{"Total Machining Time",-15} {timeStats1["TotalMachiningTime"],15:F2} {timeStats2["TotalMachiningTime"],15:F2}     \n" +
            $"{"Total Movement Time",-15} {timeStats1["TotalMovementTime"],15:F2} {timeStats2["TotalMovementTime"],15:F2}     \n\n" +
            $"{"Machining Time (Head 1)",-15} {timeStats1["MachiningTimeHead1"],15:F2} {timeStats2["MachiningTimeHead1"],15:F2}     \n" +
            $"{"Movement Time (Head 1)",-15} {timeStats1["MovementTimeHead1"],15:F2} {timeStats2["MovementTimeHead1"],15:F2}     \n" +
            $"{"Idle Time (Head 1)",-25} {timeStats1["IdleTimeHead1"],15:F2} {timeStats2["IdleTimeHead1"],15:F2}     \n\n" +
            $"{"Machining Time (Head 2)",-15} {timeStats1["MachiningTimeHead2"],15:F2} {timeStats2["MachiningTimeHead2"],15:F2}     \n" +
            $"{"Movement Time (Head 2)",-15} {timeStats1["MovementTimeHead2"],15:F2} {timeStats2["MovementTimeHead2"],15:F2}     \n" +
            $"{"Idle Time (Head 2)",-25} {timeStats1["IdleTimeHead2"],15:F2} {timeStats2["IdleTimeHead2"],15:F2}     ";

            MessageBox.Show (message, "Time Stats Comparison", MessageBoxButton.OK, MessageBoxImage.Information);
            Work.Dirty = false;
         } catch (Exception) { }

#else
         try {
            GenesysHub.ComputeGCode ();
            Work.Dirty = false; // This line is optional here since it will be set in finally
         } catch (InfeasibleCutoutException ex) {
            MessageBox.Show (ex.Message, "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
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
         } finally {
            Work.Dirty = false; // This will always execute
         }
#endif
         mOverlay.Redraw ();
      }
   }
   #endregion

   #region Simulation Related Methods
   void Simulate (object sender, RoutedEventArgs e) {
      if (!HandleNoWorkpiece ()) {
         ProcessSimulator.SimulationFinished += OnSimulationFinished;
         Task.Run (ProcessSimulator.Run);
      }
   }

   void PauseSimulation (object sender, RoutedEventArgs e) {
      if (!HandleNoWorkpiece ())
         ProcessSimulator.Pause ();
   }

   void StopSimulation (object sender, RoutedEventArgs e) {
      if (!HandleNoWorkpiece ())
         ProcessSimulator.Stop ();
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

   void LoadGCode (string filename) {
      try {
         GenesysHub.LoadGCode (filename);
      } catch (Exception ex) {
         MessageBox.Show (ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }
   }

   void OpenDINsClick (object sender, RoutedEventArgs e) {
      if (Files.SelectedItem is string selectedFile) {
         string dinFileNameH1 = "", dinFileNameH2 = "";
         try {
            string[] paths = Environment.GetEnvironmentVariable ("PATH")?.Split (';');
            string notepadPlusPlus = paths?.Select (p => System.IO.Path.Combine (p, "notepad++.exe")).FirstOrDefault (File.Exists);
            string notepad = paths?.Select (p => System.IO.Path.Combine (p, "notepad.exe")).FirstOrDefault (File.Exists);
            string editor = notepadPlusPlus ?? notepad; // Prioritize Notepad++, fallback to Notepad

            if (editor == null) {
               MessageBox.Show ("Neither Notepad++ nor Notepad was found in the system PATH.", "Error",
                  MessageBoxButton.OK, MessageBoxImage.Error);
               return;
            }

            // Construct the DIN file paths using the selected file name
            string dinFileSuffix = string.IsNullOrEmpty (MCSettings.It.DINFilenameSuffix) ? "" : $"-{MCSettings.It.DINFilenameSuffix}-";
            dinFileNameH1 = $@"{Utils.RemoveLastExtension (selectedFile)}-{1}{dinFileSuffix}({(MCSettings.It.PartConfig == MCSettings.PartConfigType.LHComponent ? "LH" : "RH")}).din";
            dinFileNameH1 = System.IO.Path.Combine (MCSettings.It.NCFilePath, "Head1", dinFileNameH1);
            dinFileNameH2 = $@"{Utils.RemoveLastExtension (selectedFile)}-{2}{dinFileSuffix}({(MCSettings.It.PartConfig == MCSettings.PartConfigType.LHComponent ? "LH" : "RH")}).din";
            dinFileNameH2 = System.IO.Path.Combine (MCSettings.It.NCFilePath, "Head2", dinFileNameH2);

            if (!File.Exists (dinFileNameH1)) throw new Exception ($"\nFile: {dinFileNameH1} does not exist.\nGenerate G Code first");
            if (!File.Exists (dinFileNameH2)) throw new Exception ($"\nFile: {dinFileNameH2} does not exist.\nGenerate G Code first");

            // Open the files
            Process.Start (new ProcessStartInfo (editor, $"\"{dinFileNameH1}\"") { UseShellExecute = true });
            Process.Start (new ProcessStartInfo (editor, $"\"{dinFileNameH2}\"") { UseShellExecute = true });
         } catch (Exception ex) {
            MessageBox.Show ($"Error opening DIN files: {ex.Message}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
         }
      }
   }
   #endregion
   #region JSON readers/writers
   
   // --------------------------------------------------------------------
   // Saves mRecentFilesMap to JSON (keeps latest 30 by timestamp)
   // --------------------------------------------------------------------
   public static void SaveRecentFilesToJSON (Dictionary<string,string> map, string jsonFileName) {
      const int MaxEntries = 30;

      if (string.IsNullOrWhiteSpace (jsonFileName))
         throw new ArgumentException ("jsonFileName must be a non-empty path.", nameof (jsonFileName));

      // Ensure directory exists
      var dir = Path.GetDirectoryName (jsonFileName);
      if (!string.IsNullOrEmpty (dir) && !Directory.Exists (dir))
         Directory.CreateDirectory (dir);

      // Ensure map exists
      map ??= new Dictionary<string, string> (StringComparer.Ordinal);

      // Trim to newest 30 by timestamp
      static DateTimeOffset ParseTs (string ts) =>
         DateTimeOffset.TryParse (ts, out var dto) ? dto : DateTimeOffset.MinValue;

      if (map.Count > MaxEntries) {
         map = map
            .OrderByDescending (kv => ParseTs (kv.Value))
            .Take (MaxEntries)
            .ToDictionary (kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
      }

      var jsonOptions = new JsonSerializerOptions {
         Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
         WriteIndented = true
      };

      var jsonOut = JsonSerializer.Serialize (map, jsonOptions);

      // Persist as ASCII (non-ASCII -> '?') to match your existing convention
      var asciiBytes = Encoding.ASCII.GetBytes (jsonOut);
      File.WriteAllBytes (jsonFileName, asciiBytes);
   }

   static List<string> DescendingOrderMap(Dictionary<string, string> map) {
      List<string> recFiles = [];
      if (map != null) {
         // Keep newest first (by timestamp)
         static DateTimeOffset ParseTs (string ts)
             => DateTimeOffset.TryParse (ts, out var dto) ? dto : DateTimeOffset.MinValue;

         foreach (var kv in map.OrderByDescending (kv => ParseTs (kv.Value))) {
            recFiles.Add ($"{kv.Key} {kv.Value}");
         }
      }
      return recFiles;
   }

   public static Dictionary<string, string> LoadRecentFilesFromJSON (string jsonFileName) {
      
      Dictionary<string, string> map = [];
      if (string.IsNullOrWhiteSpace (jsonFileName))
         throw new ArgumentException ("jsonFileName must be a non-empty path.", nameof (jsonFileName));

      if (!File.Exists (jsonFileName))
         return map;

      try {
         var bytes = File.ReadAllBytes (jsonFileName);
         if (bytes.Length == 0)
            return map;
         
         var json = Encoding.UTF8.GetString (bytes);
         map = JsonSerializer.Deserialize<Dictionary<string, string>> (json);

         //if (map != null) {
         //   // Keep newest first (by timestamp)
         //   static DateTimeOffset ParseTs (string ts)
         //       => DateTimeOffset.TryParse (ts, out var dto) ? dto : DateTimeOffset.MinValue;

         //   foreach (var kv in map.OrderByDescending (kv => ParseTs (kv.Value))) {
         //      RecentFiles.Add ($"{kv.Key} {kv.Value}");
         //   }
         //}
      } catch {
         // swallow exceptions -> return whatever was parsed so far (empty on error)
      }

      return map;
   }


   #endregion
}

