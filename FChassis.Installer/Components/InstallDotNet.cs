namespace FChassis.Installer.Components;
public class InstallDotNet : Component {
   override public void Install () {
      string installPath = @$"{installExePath}\files";
      string dotNetSetupFilePath = @$"{installPath}\{this.dotNetSetupFile}.exe";
      this.installDependSetup (dotNetSetupFilePath, installPath, this.dotNetSetupFile);
   }

   readonly string dotNetSetupFile = "dotnet-sdk-8.0.405-win-x64";
}