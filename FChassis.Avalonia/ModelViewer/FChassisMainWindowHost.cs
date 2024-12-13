using Avalonia.Controls;
using Avalonia.Platform;

using System.Runtime.InteropServices;
using System.Windows.Forms.Integration;

namespace FChassis.Avalonia; 
public class FChassisMainWindowHost : NativeControlHost {
   protected override IPlatformHandle CreateNativeControlCore (IPlatformHandle parent) {
      if (RuntimeInformation.IsOSPlatform (OSPlatform.Windows)) {
         // Model Viewer
         ElementHost elementHost = new ElementHost ();
         //elementHost.Child = FChassis.ViewModel.MainWindow.CreateViewerPanel ();

         // use ElementHost to produce a win32 Handle for embedding
         //elementHost.Child = new FChassis.MainWindow ();

         return new PlatformHandle (elementHost.Handle, "Handle");
      }

      return base.CreateNativeControlCore (parent);
   }

   protected override void DestroyNativeControlCore (IPlatformHandle control) {
      if (RuntimeInformation.IsOSPlatform (OSPlatform.Windows)) {
         WinApi.DestroyWindow (control.Handle); // destroy the win32 window
         return;
      }

      base.DestroyNativeControlCore (control);
   }
}