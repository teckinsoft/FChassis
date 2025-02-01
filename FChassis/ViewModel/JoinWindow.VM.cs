using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Win32;

using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Runtime.InteropServices;

namespace FChassis.VM;
internal partial class JoinWindow : ObservableObject {
   #region Property
   [ObservableProperty] string modelFileName = "";
   [ObservableProperty] BitmapImage thumbnailBitmap = null;
   #endregion Property

   #region Command
   [RelayCommand]
   void Load () {
      //var d = this.browseFolder ("[[[Test Code]]]Select Folder");
   
      var fileName = this.getFilename (this.ModelFileName, "Select a Part File",
                                       "CAD Files (*.iges;*.igs;*.stp;*.step)|*.iges;*.igs;*.stp;*.step|All Files (*.*)|*.*");
      if (fileName == null) 
         return;

      this.ModelFileName = fileName;
      this.action (this.loadPart).GetAwaiter ();
   }

   [RelayCommand]
   void Flip180 () 
      => this.action (this.flip180).GetAwaiter ();

   [RelayCommand]
   void Join ()
      => this.action (this.join).GetAwaiter ();   
   #endregion Command

   // #region Field
   IGES.IGES iges = null!;

   #region Implement
   public bool Initialize () {
      Debug.Assert(this.iges == null);

      this.iges = new ();
      this.iges.Initialize ();

      return true; 
   }

   public bool Uninitialize () {
      Debug.Assert (this.iges != null);

      this.iges.Uninitialize ();
      return true;
   }
   #endregion 

   #region Method
   int loadPart () {
      int errorNo = 0;

      do {
         int shapeType = 0;

         if (0 != (errorNo = this.iges.LoadIGES (this.ModelFileName, shapeType)))
            break;

         if (0 != (errorNo = this.iges.AlignToXYPlane (shapeType)))
            break;

         this.convertCad2Image (false);
      } while (false);

      if (errorNo != 0)
         this.HandleIGESError (errorNo);

      return errorNo;
   }

   int flip180 () {
      int errorNo = this.iges.RotatePartBy180AboutZAxis (0);
      if(errorNo == 0)
         this.convertCad2Image (false);

      return errorNo;
   }

   int join () {
      int errorNo = this.iges.UnionShapes ();
      if (errorNo == 0)
         this.convertCad2Image (true);

      return this.joinSave ();
   }

   int joinSave () {
      int errorNo = 0;
      var fileName = this.saveFilename (this.ModelFileName, "Select a Part File",
                                        "CAD Files (*.iges;*.igs;*.stp;*.step)|*.iges;*.igs;*.stp;*.step|All Files (*.*)|*.*",
                                        @"W:\FChassis\Sample");
      if (fileName != null)
         errorNo = this.iges.SaveIGES (fileName, 2);
      return errorNo;
   }
   #endregion 

   #region Helper
   async Task action (Func<int> func) {
      Mouse.OverrideCursor = Cursors.Wait;
      int errorNo = 0;

      await Task.Run (() => {
         errorNo = func ();
      });

      this.HandleIGESError(errorNo);
      Mouse.OverrideCursor = null;
   }

   public BitmapImage ConvertWriteableBitmapToBitmapImage (WriteableBitmap wbm) {
      BitmapImage bmImage = new BitmapImage ();
      using (MemoryStream stream = new MemoryStream ()) {
         PngBitmapEncoder encoder = new PngBitmapEncoder ();
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

   public void UpdateImage (int width, int height, byte[] imageStream) {
      /*if (false) {
         var bitmapImage = new BitmapImage ();
         {
            using var ms = new MemoryStream (imageStream);
            bitmapImage.BeginInit ();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = ms;
            bitmapImage.EndInit ();
         }

         this.ThumbnailBitmap = bitmapImage;

      } else*/ {
         byte[] pixelBuffer = imageStream;
         PixelFormat pixelFormat = PixelFormats.Rgb24;

         WriteableBitmap bitmap = new WriteableBitmap (width, height, 96, 96, pixelFormat, null);
         bitmap.Lock ();

         try {
            // Get pixel buffer
            IntPtr backBuffer = bitmap.BackBuffer;
            int stride = bitmap.BackBufferStride;
            int bufferSize = stride * height;

            // Copy the pixel buffer to the bitmap
            Marshal.Copy (pixelBuffer, 0, backBuffer, pixelBuffer.Length);

            // Mark the bitmap as updated
            bitmap.AddDirtyRect (new Int32Rect (0, 0, width, height));
         } finally {
            bitmap.Unlock ();
         }

         this.ThumbnailBitmap = this.ConvertWriteableBitmapToBitmapImage (bitmap);
      }
   }

   bool HandleIGESError(int errorNo) {
      if (errorNo == 0)
         return false;

      string message = null!;
      this.iges.GetErrorMessage (out message);

      MessageBox.Show (message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      return true; 
   }

   void convertCad2Image (bool fused) {
      int width = 800;
      int height = 600;
      byte[] imageData = null!;
      int errorNo = 0;
      errorNo = fused ?this.iges.GetShape (2, width, height, ref imageData)  // 2 - Fused
                      :this.iges.GetShape (0, width, height, ref imageData); // 0 - Left 
      if (this.HandleIGESError (errorNo))
         return;

      this.UpdateImage (width, height, imageData);
   }

   string getFilename (string fileName, string title,
                       string filter = "All files (*.*)|*.*",
                       bool multiselect = false,
                       string initialFolder = null) {
      OpenFileDialog openDlg = new () {
         Title = title, Filter = filter, Multiselect = multiselect,
         InitialDirectory = @"W:\FChassis\Sample",
         FileName = fileName
      };

      if (initialFolder != null)
         openDlg.InitialDirectory = initialFolder;

      return openDlg.ShowDialog () == true ? openDlg.FileName : null;
   }

   string saveFilename (string fileName, string title,
                       string filter = "All files (*.*)|*.*",
                       string initialFolder = null) {
      SaveFileDialog saveDlg = new () {
         Title = title, Filter = filter,
         InitialDirectory = @"W:\FChassis\Sample",
         FileName = fileName
      };

      if (initialFolder != null)
         saveDlg.InitialDirectory = initialFolder;

      return saveDlg.ShowDialog () == true ? saveDlg.FileName : null;
   }

   string browseFolder (string title, string initialFolder = null) {
      var openFolderDlg = new OpenFolderDialog () {
         Title = title,       
      };
      
      if (initialFolder != null)
         openFolderDlg.InitialDirectory = initialFolder;

      return openFolderDlg.ShowDialog () == true ? openFolderDlg.FolderName : null;
   }
   #endregion
}

