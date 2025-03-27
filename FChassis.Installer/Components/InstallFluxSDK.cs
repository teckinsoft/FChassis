namespace FChassis.Installer.Components;
public class InstallFluxSDK : Component {
   override public void method () {
      string installPath = $"{installExePath}/files";
      string dotNetSetupFilePath = $"{installPath}/Setup.FluxSDK.4.exe";
      this.installDependSetup (dotNetSetupFilePath, installPath, "Flux SDK");
   }
}