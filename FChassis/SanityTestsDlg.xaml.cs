using Microsoft.Win32;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Shapes;

using FChassis.Core.Processes;
using FChassis.Core;

namespace FChassis; 
public partial class SanityTestsDlg : Window, INotifyPropertyChanged {
   #region Constructor
   public SanityTestsDlg (GenesysHub genHub) {
      InitializeComponent ();
      GenesysHub = genHub;
      this.DataContext = this;
   }
   #endregion

   #region Data Members
   // Cached list to store the UI controls created for each row
   List<(CheckBox checkBox, TextBox textBox, Button browseButton, Button settingsButton, Button gCodeButton, Button diffButton, Ellipse statEllipse)> cachedControls = [];
   bool isDirty = false; // Dirty flag to track changes
   const string mDefaultTestSuiteDir = @"W:\FChassis\SanityTests";
   const string mDefaultFxFileDir = @"W:\FChassis\Sample";
   const string mDefaultBaselineDir = @"W:\FChassis\TData";
   JsonSerializerOptions mJSONWriteOptions, mJSONReadOptions;
   string mTestSuiteDir;
   string mTestFileName;
   int mNFixedTopRows = 2;
   List<int> mSelectedTestIndices = [];
   public event PropertyChangedEventHandler PropertyChanged;
   #endregion

   #region Properties
   public String TestFileName {
      get => mTestFileName;
      set {
         if (mTestFileName != value) {
            mTestFileName = value;
            this.Title = "Sanity Check " + value;
         }
      }
   }

   public string FxFilePath { get; set; } = mDefaultFxFileDir;
   public string BaselineDir { get; set; } = mDefaultBaselineDir;
   public GenesysHub GenesysHub { get; set; }
   public SanityCheck SanityCheck { get; set; }
   
   public List<SanityTestData> SanityTests { get; set; } = [];
   public List<bool> SanityTestResult { get; set; } = [];
   public string TestSuiteDir {
      get => mTestSuiteDir;
      set {
         if (mTestSuiteDir != value) {
            mTestSuiteDir = value;
            OnPropertyChanged ();
         }
      }
   }
   #endregion

   #region Event Handlers
   public void OnSanityTestsDlgLoaded (object sender, RoutedEventArgs e) {
      TestSuiteDir = mDefaultTestSuiteDir;
      SanityCheck = new (GenesysHub);
   }

   void OnPropertyChanged ([CallerMemberName] string propertyName = null) 
      => PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (propertyName));

   void OnAddTestButtonClick (object sender, RoutedEventArgs e) {
      // Open file dialog to select a JSON file
      OpenFileDialog openFileDialog = new () {
         Filter = "FX files (*.fx)|*.fx|All files (*.*)|*.*",
         InitialDirectory = FxFilePath
      };

      if (openFileDialog.ShowDialog () == true) {
         // Create Sanity test data object 
         var sanityTestData = new SanityTestData ();
         FxFilePath = System.IO.Path.GetDirectoryName (openFileDialog.FileName);

         // Create a new row in the grid
         int newRowIdx = MainGrid.RowDefinitions.Count;
         MainGrid.RowDefinitions.Add (new RowDefinition { Height = GridLength.Auto });

         // Add a CheckBox to the new row
         CheckBox newCheckBox = new () {
            Margin = new Thickness (5), IsChecked = true,
            HorizontalAlignment = HorizontalAlignment.Center
         };

         // If "Select All" is checked, check this new checkbox too
         if (SelectAllCheckBox.IsChecked == true) {
            newCheckBox.IsChecked = true;
         }

         Grid.SetRow (newCheckBox, newRowIdx);
         newCheckBox.Checked += (s, args) => OnRunCheckBoxChangeStatus (newRowIdx - mNFixedTopRows);
         newCheckBox.Unchecked += (s, args) => OnRunCheckBoxChangeStatus (newRowIdx - mNFixedTopRows);
         Grid.SetColumn (newCheckBox, 0);
         MainGrid.Children.Add (newCheckBox);
         sanityTestData.ToRun = newCheckBox.IsChecked.Value;

         // Add a TextBox to the new row to display the JSON file path
         TextBox filePathTextBox = new () {
            Margin = new Thickness (5),
            Text = openFileDialog.FileName,
            Width = 220
         };

         Grid.SetRow (filePathTextBox, newRowIdx);
         Grid.SetColumn (filePathTextBox, 1);
         MainGrid.Children.Add (filePathTextBox);
         sanityTestData.FxFileName = openFileDialog.FileName;

         // Add a "Browse" Button to the new row
         Button browseButton = new () { Content = "Browse", Margin = new Thickness (5) };
         browseButton.Click += (s, args) => OnSelectFxFileButtonClick (newRowIdx);
         Grid.SetRow (browseButton, newRowIdx);
         Grid.SetColumn (browseButton, 2);
         MainGrid.Children.Add (browseButton);

         // Add a "Settings" Button to the new row
         Button settingsButton = new () { Content = "Settings", Margin = new Thickness (5) };
         settingsButton.Click += (s, args) => OnClickSettingsButton (newRowIdx, sanityTestData);
         Grid.SetRow (settingsButton, newRowIdx);
         Grid.SetColumn (settingsButton, 3);
         MainGrid.Children.Add (settingsButton);

         // Add a "GCode" Button to the new row
         Button gCodeButton = new () { Content = "Run", Margin = new Thickness (5) };
         gCodeButton.Click += (s, args) => OnRunSingleTestClick (newRowIdx);
         Grid.SetRow (gCodeButton, newRowIdx);
         Grid.SetColumn (gCodeButton, 4);
         MainGrid.Children.Add (gCodeButton);

         // Add a "Diff" Button to the new row
         Button diffButton = new () { Content = "Diff", Margin = new Thickness (5) };
         diffButton.Click += (s, args) => OnDiffButtonClick (newRowIdx);
         Grid.SetRow (diffButton, newRowIdx);
         Grid.SetColumn (diffButton, 5);
         MainGrid.Children.Add (diffButton);

         var statusEllipse = CreateEllipseWidget (newRowIdx, 6);
         MainGrid.Children.Add (statusEllipse);

         // Cache the created controls
         cachedControls.Add ((newCheckBox, filePathTextBox, browseButton, 
                              settingsButton, gCodeButton, diffButton, 
                              statusEllipse));
         SanityTests.Add (sanityTestData);

         // Mark as dirty since changes were made
         isDirty = true;
      }
   }

   void OnOkButtonClick (object sender, RoutedEventArgs e) {
      SaveIfDirty ();

      // Close without any prompts if not dirty
      this.Close ();
   }

   void OnDeleteButtonClick (object sender, RoutedEventArgs e)
      => RemoveSanityTestRows ();

   void OnSelectFxFileButtonClick (int rowIndex) {
      // Handle Browse button click for the given row index
      OpenFileDialog openFileDialog = new () {
         Filter = "FX files (*.fx)|*.fx|All files (*.*)|*.*",
         InitialDirectory = FxFilePath
      };

      if (openFileDialog.ShowDialog () == true) {
         FxFilePath = openFileDialog.FileName;
         cachedControls[rowIndex - mNFixedTopRows].textBox.Text = FxFilePath;
         var sanityTst = SanityTests[rowIndex - mNFixedTopRows];
         sanityTst.FxFileName = FxFilePath;
         SanityTests[rowIndex - mNFixedTopRows] = sanityTst;
      }
   }

   void OnRunCheckBoxChangeStatus (int controlIndex) {
      var testData = SanityTests[controlIndex];
      testData.ToRun = cachedControls[controlIndex].checkBox.IsChecked.Value;
      if (cachedControls[controlIndex].checkBox.IsChecked.Value == false && SelectAllCheckBox.IsChecked == true)
         SelectAllCheckBox.IsChecked = false;

      SanityTests[controlIndex] = testData;
      bool anyNoRuns = cachedControls.Any (cc => cc.checkBox.IsChecked == false);
      if (!anyNoRuns) 
         SelectAllCheckBox.IsChecked = true;

      isDirty = true;
   }

   void OnBrowseSuiteButtonClick (object sender, RoutedEventArgs e)
      => MessageBox.Show ($"Select Sanity Tests Directory");

   void OnClickSettingsButton (int rowIndex, SanityTestData sData) {
      // Create a new SettingsDlg instance with the settings
      var settingsDialog = new SettingsDlg (sData.MCSettings);

      // Subscribe to the OnOkAction event to handle the callback
      settingsDialog.OnOkAction += () => {
         // Handle the OK action and update the SanityTestData with the updated settings
         sData.MCSettings = settingsDialog.Settings;

         // Update the cached control if needed
         cachedControls[rowIndex - mNFixedTopRows] = (cachedControls[rowIndex - mNFixedTopRows].checkBox,
                                                      cachedControls[rowIndex - mNFixedTopRows].textBox,
                                                      cachedControls[rowIndex - mNFixedTopRows].browseButton,
                                                      cachedControls[rowIndex - mNFixedTopRows].settingsButton,
                                                      cachedControls[rowIndex - mNFixedTopRows].gCodeButton,
                                                      cachedControls[rowIndex - mNFixedTopRows].diffButton,
                                                      cachedControls[rowIndex - mNFixedTopRows].statEllipse);
         if (settingsDialog.IsModified && !string.IsNullOrEmpty (TestFileName))
            //SaveToJson (TestFileName);
            SanityTests[rowIndex - mNFixedTopRows] = sData;
      };

      // Show the settings dialog
      settingsDialog.ShowDialog ();
   }

   void OnCloseTSuiteButtonClick (object sender, RoutedEventArgs e) {
      //SaveIfDirty ();
      RemoveSanityTestRows (onlySelected: false);
      this.Close ();
   }

   void OnSaveTSuiteAsButtonClick (object sender, RoutedEventArgs e) {
      if (SanityTests.Count == 0) 
         return;

      SaveFileDialog saveFileDialog = new () {
         Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
         InitialDirectory = TestSuiteDir
      };

      if (saveFileDialog.ShowDialog () == true) {
         SaveToJson (saveFileDialog.FileName);
         TestFileName = saveFileDialog.FileName;
         this.Title = "Sanity Check " + TestFileName;
         isDirty = false; // Reset the dirty flag after saving
      }
   }

   void OnRunSelectedTestsButtonClick (object sender, RoutedEventArgs e) {
      mSelectedTestIndices.Clear ();
      List<SanityTestData> testDataList = [];
      for (int ii = 0; ii < SanityTests.Count; ii++) {
         if (cachedControls[ii].checkBox.IsChecked == false) 
            continue;

         testDataList.Add (SanityTests[ii]);
         mSelectedTestIndices.Add (ii);
      }

      var stats = RunTests (testDataList);
      UpdateRunStatus (mSelectedTestIndices, stats);
   }

   void OnRunSingleTestClick (int rowIndex) {
      var testIndex = rowIndex - mNFixedTopRows;
      List<SanityTestData> testData = [SanityTests[testIndex]];
      mSelectedTestIndices = [testIndex];
      var stats = RunTests (testData, true);
      UpdateRunStatus (mSelectedTestIndices, stats);
   }

   void OnDiffButtonClick (int rowIndex) {
      var dataIndex = mSelectedTestIndices.FindIndex (t => t == rowIndex - mNFixedTopRows);
      if (dataIndex != -1) {
         bool anyDiff = SanityCheck.Diff (BaselineDir, dataIndex, launchWinmerge: true);
         if (!anyDiff)
            MessageBox.Show ("There are no differences!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
      }
   }

   void OnNewTSuiteButtonClick (object sender, RoutedEventArgs e) {
      if (isDirty) {
         MessageBoxResult result = MessageBox.Show ("Do you want to save before creating a new item?", "Save Confirmation",
                                                    MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

         if (result == MessageBoxResult.Yes) {
            // Show Save File Dialog
            SaveFileDialog saveFileDialog = new () {
               Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
               InitialDirectory = TestSuiteDir
            };

            if (saveFileDialog.ShowDialog () == true) {
               // Save logic here (not implemented)
               // Assume successful save, proceed with removing rows
               RemoveSanityTestRows (onlySelected: false);
               SaveToJson (saveFileDialog.FileName);
            }
         } else if (result == MessageBoxResult.No) 
            RemoveSanityTestRows (); // Proceed without saving

         isDirty = false;
      } else 
         RemoveSanityTestRows ();

      ClearZombies ();
   }

   void OnLoadTSuiteButtonClick (object sender, RoutedEventArgs e) {
      RemoveSanityTestRows ();
      OpenFileDialog openFileDialog = new () {
         Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
         InitialDirectory = TestSuiteDir
      };

      if (openFileDialog.ShowDialog () == true)
         LoadFromJson (openFileDialog.FileName);

      TestFileName = openFileDialog.FileName;
      this.Title = "Sanity Check " + TestFileName;
   }

   void OnSelectAllCheckBoxClick (object sender, RoutedEventArgs e) {
      if (SelectAllCheckBox.IsChecked == true) {
         // Check all checkboxes
         foreach (var (checkbox, _, _, _, _, _, _) in cachedControls)
            checkbox.IsChecked = true;
      } else {
         // Uncheck all checkboxes
         foreach (var (checkbox, _, _, _, _, _, _) in cachedControls)
            checkbox.IsChecked = false;
      }
   }

   void OnSaveTSuiteButtonClick (object sender, RoutedEventArgs e) {
      if (!string.IsNullOrEmpty (TestFileName)) {
         SaveToJson (TestFileName);
         isDirty = false; // Reset the dirty flag after saving
      } else if (SanityTests.Count > 0) {
         SaveFileDialog saveFileDialog = new () {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = TestSuiteDir
         };

         if (saveFileDialog.ShowDialog () == true) {
            SaveToJson (saveFileDialog.FileName);
            isDirty = false; // Reset the dirty flag after saving
         }
      }
   }
   #endregion

   #region Utilities
   TextBox CreateTextBox (string initialText, int width) {
      TextBox filePathTextBox = new () {
         Margin = new Thickness (5),
         Text = initialText,
         Width = width
      };

      return filePathTextBox;
   }

   Ellipse CreateEllipseWidget (int newRowIdx, int colIndex) {
      // Add an Ellipse to the new row to indicate status
      Ellipse statusEllipse = new () {
         Width = 40,
         Height = 20,
         Fill = new SolidColorBrush (Colors.LightBlue),
         Margin = new Thickness (5),
         Stroke = new SolidColorBrush (Colors.Black)
      };

      Grid.SetRow (statusEllipse, newRowIdx);
      Grid.SetColumn (statusEllipse, colIndex);
      return statusEllipse;
   }

   void RemoveSanityTestRows (bool onlySelected = true) {
      // Collect all selected rows
      List<int> rowsToDelete = [];

      for (int i = 0; i < cachedControls.Count; i++) {
         if ((cachedControls[i].checkBox.IsChecked == true && onlySelected) || onlySelected == false)
            rowsToDelete.Add (i);
      }

      // Delete rows in reverse order to avoid indexing issues
      rowsToDelete.Sort ((a, b) => b.CompareTo (a));
      foreach (int rowIndex in rowsToDelete)
         RemoveRow (rowIndex + mNFixedTopRows);

      // Iterate through the indices and remove from cachedControls
      foreach (int index in rowsToDelete) {
         if (index >= 0 && index < SanityTests.Count)
            SanityTests.RemoveAt (index);
      }
   }

   void SaveIfDirty () {
      // If the test pres is modified and if the test was loaded (not cewly created)
      if (isDirty && !string.IsNullOrEmpty (TestFileName)) {
         MessageBoxResult result = MessageBox.Show ("Do you want to save before closing?", "Save Confirmation", 
                                                    MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
         if (result == MessageBoxResult.Yes) {
            // Show Save File Dialog
            SaveFileDialog saveFileDialog = new () {
               Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
               InitialDirectory = TestSuiteDir
            };

            if (saveFileDialog.ShowDialog () == true) {
               // Save logic here (not implemented)
               // Assume successful save, proceed with closing
               SaveToJson (saveFileDialog.FileName);
               TestFileName = saveFileDialog.FileName;
               this.Close ();
            }
         } else if (result == MessageBoxResult.No) 
            this.Close ();
      }
   }

   List<bool> RunTests (List<SanityTestData> testDataLst, bool forceRun = false) {
      var stats = SanityCheck?.Run (testDataLst, BaselineDir, SanityCheck?.GetArgumentNullException (), forceRun);
      return stats;
   }

   void UpdateRunStatus (List<int> testIndices, List<bool> stats) {
      for (int ii = 0; ii < stats.Count; ii++) {
         if (stats[ii]) cachedControls[testIndices[ii]].statEllipse.Fill = new SolidColorBrush (Colors.Green);
         else cachedControls[testIndices[ii]].statEllipse.Fill = new SolidColorBrush (Colors.Red);
      }
   }

   void RemoveRow (int rowIndex) {
      // Create a list of elements to remove to avoid modifying the collection while iterating
      var elementsToRemove = new List<UIElement> ();
      foreach (UIElement element in MainGrid.Children)
         if (Grid.GetRow (element) == rowIndex)
            elementsToRemove.Add (element);

      foreach (UIElement element in elementsToRemove)
         MainGrid.Children.Remove (element);

      // Remove the row definition
      MainGrid.RowDefinitions.RemoveAt (rowIndex);

      // Remove from cached controls
      cachedControls.RemoveAt (rowIndex - mNFixedTopRows);

      // Update the row index for the remaining rows
      foreach (UIElement element in MainGrid.Children) {
         int currentRow = Grid.GetRow (element);
         if (currentRow > rowIndex) {
            Grid.SetRow (element, currentRow - 1);
         }
      }

      // Mark as dirty since changes were made
      isDirty = true;
   }

   void ClearZombies () {
      TestSuiteDir = mDefaultTestSuiteDir;
      isDirty = false;
      cachedControls.Clear ();
      SanityTests.Clear ();
      TestFileName = string.Empty;
      mSelectedTestIndices.Clear ();
   }

   void SaveToJson (string filePath) {
      mJSONWriteOptions ??= new JsonSerializerOptions {
         WriteIndented = true, // For pretty-printing the JSON
         Converters = { new JsonStringEnumConverter () } // Converts Enums to their string representation
      };

      var jsonObject = new {
         Tests = SanityTests
      };

      var json = JsonSerializer.Serialize (jsonObject, mJSONWriteOptions);
      File.WriteAllText (filePath, json);
   }

   void LoadFromJson (string filePath) {
      mJSONReadOptions ??= new JsonSerializerOptions {
         Converters = { new JsonStringEnumConverter () } // Converts Enums from their string representation
      };

      if (File.Exists (filePath)) {
         var json = File.ReadAllText (filePath);
         var jsonObject = JsonSerializer.Deserialize<JsonElement> (json, mJSONReadOptions);

         if (jsonObject.TryGetProperty ("Tests", out JsonElement testsElement) 
             && testsElement.ValueKind == JsonValueKind.Array) {
            SanityTests.Clear ();
            cachedControls.Clear ();
            foreach (var element in testsElement.EnumerateArray ()) {
               var sanityTestData = new SanityTestData ().LoadFromJsonElement (element);
               SanityTests.Add (sanityTestData);

               // Create UI elements to match the loaded data, starting from row index 2 onwards
               AddSanityTestRow (sanityTestData);
            }

            isDirty = false; // Reset the dirty flag after loading
         }
      }
   }

   void AddSanityTestRow (SanityTestData sanityTestData) {
      int newRowIdx = MainGrid.RowDefinitions.Count;
      MainGrid.RowDefinitions.Add (new RowDefinition { Height = GridLength.Auto });

      // Add a CheckBox to the new row
      CheckBox newCheckBox = new () {
         Margin = new Thickness (5), IsChecked = sanityTestData.ToRun,
         HorizontalAlignment = HorizontalAlignment.Center
      };

      if (!sanityTestData.ToRun && SelectAllCheckBox.IsChecked == true)
         SelectAllCheckBox.IsChecked = false;

      newCheckBox.Checked += (s, args) => OnRunCheckBoxChangeStatus (newRowIdx - mNFixedTopRows);
      newCheckBox.Unchecked += (s, args) => OnRunCheckBoxChangeStatus (newRowIdx - mNFixedTopRows);
      Grid.SetRow (newCheckBox, newRowIdx);
      Grid.SetColumn (newCheckBox, 0);
      MainGrid.Children.Add (newCheckBox);

      // Add a TextBox to the new row to display the JSON file path
      var filePathTextBox = CreateTextBox (sanityTestData.FxFileName, 220);
      Grid.SetRow (filePathTextBox, newRowIdx);
      Grid.SetColumn (filePathTextBox, 1);
      MainGrid.Children.Add (filePathTextBox);

      // Add a "Browse" Button to the new row
      Button browseButton = new () { Content = "Browse", Margin = new Thickness (5, 5, 5, 5), Width = 60 };
      browseButton.Click += (s, args) => OnSelectFxFileButtonClick (newRowIdx);
      Grid.SetRow (browseButton, newRowIdx);
      Grid.SetColumn (browseButton, 2);
      MainGrid.Children.Add (browseButton);

      // Add a "Settings" Button to the new row
      Button settingsButton = new () { Content = "Settings", Margin = new Thickness (5, 5, 5, 5), Width = 60 };
      settingsButton.Click += (s, args) => OnClickSettingsButton (newRowIdx, sanityTestData);
      Grid.SetRow (settingsButton, newRowIdx);
      Grid.SetColumn (settingsButton, 3);
      MainGrid.Children.Add (settingsButton);

      // Add a "GCode" Button to the new row
      Button gCodeButton = new () { Content = "Run", Margin = new Thickness (5, 5, 5, 5), Width = 60 };
      gCodeButton.Click += (s, args) => OnRunSingleTestClick (newRowIdx);
      Grid.SetRow (gCodeButton, newRowIdx);
      Grid.SetColumn (gCodeButton, 4);
      MainGrid.Children.Add (gCodeButton);

      // Add a "Diff" Button to the new row
      Button diffButton = new () { Content = "Diff", Margin = new Thickness (5, 5, 5, 5), Width = 60 };
      diffButton.Click += (s, args) => OnDiffButtonClick (newRowIdx);
      Grid.SetRow (diffButton, newRowIdx);
      Grid.SetColumn (diffButton, 5);
      MainGrid.Children.Add (diffButton);

      var statusEllipse = CreateEllipseWidget (newRowIdx, 6);
      MainGrid.Children.Add (statusEllipse);

      // Cache the created controls
      cachedControls.Add ((newCheckBox, filePathTextBox, browseButton, 
                           settingsButton, gCodeButton, diffButton, statusEllipse));
   }
   #endregion
}
