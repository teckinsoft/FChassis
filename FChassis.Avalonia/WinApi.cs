using System.Runtime.InteropServices;
using System;

namespace FChassis.Avalonia;
public static unsafe class WinApi {
   [DllImport ("user32.dll", SetLastError = true)]
   public static unsafe extern bool DestroyWindow (IntPtr hwnd);
}
