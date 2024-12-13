using Avalonia.Controls;
using Avalonia.Platform;

using System;
using System.Runtime.InteropServices;

namespace FChassis.Avalonia; 
public partial class Coin3DViewerHost : NativeControlHost {
   Coin3D.Inventor.Viewer viewer = null!;
   protected override IPlatformHandle CreateNativeControlCore (IPlatformHandle hostHandle) {
      if (RuntimeInformation.IsOSPlatform (OSPlatform.Windows)) {
         var nativeHandle = ((IPlatformHandle)hostHandle).Handle;
         this.viewer = new Coin3D.Inventor.Viewer ();
         IntPtr handle = this.viewer.Create (nativeHandle);

         return new PlatformHandle (handle, "Handle");
      }

      return base.CreateNativeControlCore (hostHandle);
   }

   protected override void DestroyNativeControlCore (IPlatformHandle control) {
      if (RuntimeInformation.IsOSPlatform (OSPlatform.Windows)) {
         WinApi.DestroyWindow (control.Handle); // destroy the win32 window
         return;
      }

      base.DestroyNativeControlCore (control);
   }
}