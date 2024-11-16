using System.IO;
namespace FChassis;
public class SettingServices {
   private readonly string settingsFilePath;
   //public static string Multipass2HMc = "LCMMultipass2H";
   //public static string LegacyMc = "LCMLegacy";
   
   public static SettingServices It => sIt ??= new ();
   static SettingServices sIt;
   
   public bool LeftToRightMachining { get; set; }
   SettingServices () {
      string fChassisDrive = "W:\\";
      string fChassisFolderPath;
      if (!Directory.Exists (fChassisDrive)) {
         string userHomePath = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
         fChassisFolderPath = Path.Combine (userHomePath, "FChassis");
      } else {
         fChassisFolderPath = Path.Combine (fChassisDrive, "FChassis");
      }

      // Define the full path to the settings file
      settingsFilePath = Path.Combine (fChassisFolderPath, "FChassis.User.Settings.JSON");

      // Ensure the directory exists
      if (!Directory.Exists (fChassisFolderPath)) {
         Directory.CreateDirectory (fChassisFolderPath);
      }
      LeftToRightMachining = true;
   }

   public void SaveSettings (MCSettings settings, bool backupNew = false) {
      if (backupNew) settings.SaveToJson (settingsFilePath+".bckup");
      else settings.SaveToJson (settingsFilePath);
   }

   public void LoadSettings (MCSettings settings) {
      if (File.Exists (settingsFilePath)) {
         try {
            settings.LoadFromJson (settingsFilePath);
         } catch (Exception) {}
      }
   }
}
