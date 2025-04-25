using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FChassis.Windows;
public class Win32Host : HwndHost {
   protected override HandleRef BuildWindowCore (HandleRef hwndParent) {
      childHwnd = CreateWindowEx (
          0, "static", "",
          WS_CHILD | WS_VISIBLE,
          0, 0,
          (int)ActualWidth, (int)ActualHeight,
          hwndParent.Handle,
          IntPtr.Zero,
          IntPtr.Zero,
          IntPtr.Zero);

      // Subclass the window procedure
      newWndProcDelegate = CustomWndProc;
      nint childWndProc = Marshal.GetFunctionPointerForDelegate (newWndProcDelegate);
      oldWndProc = SetWindowLongPtr (childHwnd, GWLP_WNDPROC, childWndProc);

      return new HandleRef (this, childHwnd);
   }

   IntPtr CustomWndProc (IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam) {
      if (msg == WM_PAINT && !DesignerProperties.GetIsInDesignMode (this))
         OnPaint ();

      Debug.Assert (oldWndProc != IntPtr.Zero);
      return CallWindowProc (oldWndProc, hWnd, msg, wParam, lParam); // Call original window procedure for default handling
   }

   void OnPaint () {
      ValidateRect (childHwnd, IntPtr.Zero);
      Redraw?.Invoke ();
   }

   protected override void DestroyWindowCore (HandleRef hwnd) {
      if (hwnd.Handle != IntPtr.Zero) {
         DestroyWindow (hwnd.Handle);
         childHwnd = IntPtr.Zero;
         oldWndProc = IntPtr.Zero;
      }
   }

   protected override Size MeasureOverride (Size constraint)
      => constraint;  

   protected override Size ArrangeOverride (Size finalSize) {
      MoveWindow (childHwnd, 0, 0, (int)finalSize.Width, (int)finalSize.Height, true);
      return finalSize;
   }

   protected override void OnRenderSizeChanged (SizeChangedInfo sizeInfo) {
      base.OnRenderSizeChanged (sizeInfo);

      if (childHwnd != IntPtr.Zero) {
         int width = (int)(sizeInfo.NewSize.Width - 200);
         int height = (int)sizeInfo.NewSize.Height;
         MoveWindow (childHwnd, 0, 0, width, height, true);
      }

      Redraw?.Invoke ();
   }

   public void InvalidateChildWindow ()
      => InvalidateRect(childHwnd, IntPtr.Zero, false);

   public Action Redraw;
   public IntPtr childHwnd;
   IntPtr oldWndProc = IntPtr.Zero;
   WndProc1 newWndProcDelegate;

   #region Delegate Declarations
   public delegate void DGOnResize ();
   public delegate IntPtr WndProc1 (IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
   #endregion Delegate Declarations

   #region PInvoke declarations
   [DllImport ("user32.dll", SetLastError = true)]
   static extern IntPtr CallWindowProc (IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

   [DllImport ("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
   static extern IntPtr CreateWindowEx (
       int dwExStyle, string lpszClassName, string lpszWindowName,
       int style, int x, int y, int width, int height,
       IntPtr hwndParent, IntPtr hMenu, IntPtr hInst, IntPtr pvParam);

   [DllImport ("user32.dll", SetLastError = true)]
   static extern bool DestroyWindow (IntPtr hwnd);

   [DllImport ("user32.dll", SetLastError = true)]
   public static extern bool MoveWindow (IntPtr hwnd, int x, int y, int width, int height, bool repaint);
   
   [DllImport ("user32.dll", SetLastError = true)]
   public static extern bool SetWindowPos (IntPtr hWnd, IntPtr hWndInsertAfter,
                                           int X, int Y, int cx, int cy, uint uFlags);

   [DllImport ("user32.dll", SetLastError = true)]
   static extern bool ValidateRect (IntPtr hWnd, IntPtr lpRect); // lpRect = IntPtr.Zero validates entire client area

   [DllImport ("user32.dll", SetLastError = true)]
   static extern bool InvalidateRect (IntPtr hWnd, IntPtr lpRect, bool bErase); // lpRect = IntPtr.Zero validates entire client area

   [DllImport ("user32.dll", SetLastError = true)]
   static extern IntPtr SetWindowLongPtr (IntPtr hWnd, int nIndex, IntPtr dwNewLong);

   [DllImport ("user32.dll", SetLastError = true)]
   static extern IntPtr GetWindowLongPtr (IntPtr hWnd, int nIndex);
   #endregion PInvoke declarations

   #region PInvoke constant declarations
   const int WS_CHILD = 0x40000000;
   const int WS_VISIBLE = 0x10000000;   

   const int GWLP_WNDPROC = -4;
   const int WM_PAINT = 0x000F;
   #endregion PInvoke constant declarations
}