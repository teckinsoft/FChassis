using Microsoft.Win32;

using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Runtime.InteropServices;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FChassis.IGES;

namespace FChassis.VM;

public partial class JoinWindowVM : ObservableObject, IDisposable {
   #region Events
   public event Action<string> EvMirrorAndJoinedFileSaved; // Event to notify MainWindow when a file is saved
   public event Action<string> EvLoadPart;
   public event Action EvRequestCloseWindow;
   #endregion

   #region Property
   [ObservableProperty]  string modelFileName = "";
   [ObservableProperty] string joinedFileName = "";
   [ObservableProperty]  BitmapImage thumbnailBitmap;
   #endregion

   #region Fields
   IGES.IGES iges;
    bool disposed = false;
   JoinResultVM.JoinResultOption mJoinResOpt = JoinResultVM.JoinResultOption.None;
   #endregion

   #region Commands
   [RelayCommand]
    void Load () {
      var fileName = getFilename (ModelFileName, "Select a Part File",
                                "CAD Files (*.iges;*.igs;*.stp;*.step)|*.iges;*.igs;*.stp;*.step|All Files (*.*)|*.*");
      if (fileName == null) return;

      ModelFileName = fileName;
      action (loadPart).GetAwaiter ();
   }

   [RelayCommand]
    void Flip180 () => action (flip180).GetAwaiter ();

   [RelayCommand]
   async Task MirrorAndJoin (object parameter) {
      await action (join); // Ensure join() completes before checking the result

      if (parameter is Window currentWindow && (mJoinResOpt == JoinResultVM.JoinResultOption.SaveAndOpen ||
         mJoinResOpt == JoinResultVM.JoinResultOption.Cancel))
         currentWindow.Close ();
   }
   #endregion

   #region Initialization & Cleanup
   public bool Initialize () {
      Debug.Assert (iges == null);
      iges = new IGES.IGES ();
      iges.Initialize ();
      return true;
   }

   public bool Uninitialize () {
      //Debug.Assert (iges != null);
      if (iges != null) {
         iges.Uninitialize ();
         iges.Dispose ();
         iges = null;
      }
      return true;
   }

   public void Dispose () {
      if (!disposed) {

         Uninitialize ();
         disposed = true;
         GC.SuppressFinalize (this);
      }
   }

   ~JoinWindowVM () {
      Dispose ();
   }
   #endregion

   #region Methods
    int loadPart () {
      int errorNo;

      if (iges == null) return -1; // Ensure iges is initialized

      do {
         int shapeType = 0;

         if ((errorNo = iges.LoadIGES (ModelFileName, shapeType)) != 0)
            break;

         if ((errorNo = iges.AlignToXYPlane (shapeType)) != 0)
            break;

         convertCad2Image (false);
      } while (false);

      if (errorNo != 0)
         HandleIGESError (errorNo);

      return errorNo;
   }

    int flip180 () {
      if (iges == null) return -1;
      int errorNo = iges.RotatePartBy180AboutZAxis (0);
      if (errorNo == 0)
         convertCad2Image (false);
      return errorNo;
   }

   int undoJoin () {
      if (iges == null) return -1;
      int errorNo = iges.UndoJoin ();
      if (errorNo == 0)
         convertCad2Image (false);
      return errorNo;
   }

   int join () {
      if (iges == null) return -1;
      int errorNo = iges.UnionShapes ();
      if (errorNo == 0)
         convertCad2Image (true);

      // Ensure the dialog is opened on the UI thread
      int resVal = Application.Current.Dispatcher.Invoke (() => {
         JoinResult joinResultDialog = new ();
         bool? dialogResult = joinResultDialog.ShowDialog ();
         
         int res = 1;
         if (dialogResult == true) {
            mJoinResOpt = joinResultDialog.joinResVM.Result;

            switch (mJoinResOpt) {
               case JoinResultVM.JoinResultOption.SaveAndOpen:
                  res = joinSave ();
                  if (res == 0)
                     OpenSavedFile ();
                  else return res;
                  break;
               case JoinResultVM.JoinResultOption.Save:
                  res = joinSave ();
                  break;
               case JoinResultVM.JoinResultOption.Cancel:
               case JoinResultVM.JoinResultOption.None:
                  undoJoin ();
                  EvRequestCloseWindow?.Invoke ();
                  return 0;
            }
         }
         return res;
      });
      return resVal;
   }


   void OpenSavedFile () {
      if (!string.IsNullOrEmpty (JoinedFileName)) {
         try {
            EvLoadPart?.Invoke (JoinedFileName);
         } catch (Exception ex) {
            MessageBox.Show ($"Error opening file: {ex.Message}", "Open Error", MessageBoxButton.OK, MessageBoxImage.Error);
         }
      }
   }

   int joinSave () {
      if (iges == null) return -1;
      int errorNo = 0;

      JoinedFileName = saveFilename (ModelFileName, "Select a Part File",
          "CAD Files (*.iges;*.igs;*.stp;*.step)|*.iges;*.igs;*.stp;*.step|All Files (*.*)|*.*",
          @"W:\FChassis\Sample");

      if (!string.IsNullOrEmpty (JoinedFileName)) {
         errorNo = iges.SaveIGES (JoinedFileName, 2);

         EvMirrorAndJoinedFileSaved?.Invoke (System.IO.Path.GetDirectoryName (JoinedFileName));
      }
      return errorNo;
   }
   #endregion

   #region Helper Methods
   async Task action (Func<int> func) {
      Mouse.OverrideCursor = Cursors.Wait;
      int errorNo = 0;

      await Task.Run (() => errorNo = func ());

      HandleIGESError (errorNo);
      Mouse.OverrideCursor = null;
   }

    bool HandleIGESError (int errorNo) {
      if (errorNo == 0 || iges == null) return false;

      string message = null!;
      iges.GetErrorMessage (out message);

      MessageBox.Show (message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      return true;
   }

    void convertCad2Image (bool fused) {
      if (iges == null) return;
      int width = 800, height = 600;
      byte[] imageData = null!;
      int errorNo = fused ? iges.GetShape (2, width, height, ref imageData)  // 2 - Fused
                          : iges.GetShape (0, width, height, ref imageData); // 0 - Left 
      if (HandleIGESError (errorNo))
         return;

      UpdateImage (width, height, imageData);
   }

    void UpdateImage (int width, int height, byte[] imageStream) {
      if (imageStream == null || imageStream.Length == 0) return;

      WriteableBitmap bitmap = new (width, height, 96, 96, PixelFormats.Rgb24, null);
      bitmap.Lock ();
      try {
         Marshal.Copy (imageStream, 0, bitmap.BackBuffer, imageStream.Length);
         bitmap.AddDirtyRect (new Int32Rect (0, 0, width, height));
      } finally {
         bitmap.Unlock ();
      }

      ThumbnailBitmap = ConvertWriteableBitmapToBitmapImage (bitmap);
   }

    BitmapImage ConvertWriteableBitmapToBitmapImage (WriteableBitmap wbm) {
      BitmapImage bmImage = new ();
      using (MemoryStream stream = new ()) {
         PngBitmapEncoder encoder = new ();
         encoder.Frames.Add (BitmapFrame.Create (wbm));
         encoder.Save (stream);
         bmImage.BeginInit ();
         bmImage.CacheOption = BitmapCacheOption.OnLoad;
         bmImage.StreamSource = stream;
         bmImage.EndInit ();
         bmImage.Freeze ();
      }
      return bmImage;
   }

#nullable enable
   string? getFilename (string fileName, string title, string filter = "All files (*.*)|*.*",
                              bool multiselect = false, string? initialFolder = null) {
      OpenFileDialog openDlg = new () {
         Title = title, Filter = filter, Multiselect = multiselect,
         InitialDirectory = initialFolder ?? @"W:\FChassis\Sample",
         FileName = fileName
      };
      return openDlg.ShowDialog () == true ? openDlg.FileName : null;
   }

   string? saveFilename (string fileName, string title, string filter = "All files (*.*)|*.*",
                              string? initialFolder = null) {
      SaveFileDialog saveDlg = new () {
         Title = title, Filter = filter,
         InitialDirectory = initialFolder ?? @"W:\FChassis\Sample",
         FileName = fileName
      };
      return saveDlg.ShowDialog () == true ? saveDlg.FileName : null;
   }
#nullable restore
   #endregion
}

