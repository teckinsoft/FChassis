namespace FChassis.Installer.Components;
public class InstallOpenCascade : Component {
   override public async void method ()
      => await this.installDependSetupFromURL (this.downloadURL, "Open Cascade", this.installPath, this.downloadFileName);

   // #region Fields
   readonly string downloadURL = "https://downloads.sourceforge.net/winmerge/WinMerge-2.16.46-x64-Setup.exe";
   //readonly string downloadURL2 = "https://dev.opencascade.org/system/files/occt/OCC_7.5.0_release/opencascade-7.5.0-vc14-64.exe";
   //readonly string downloadURL = "https://old.opencascade.com/sites/default/files/private/occt/OCC_7.5.0_release/opencascade-7.5.0-vc14-64.exe";
   readonly string downloadFileName = "opencascade-7.5.0-vc14-64.exe";
   readonly string installPath = @"C:\OpenCASCADE";
}