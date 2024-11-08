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

   public void SaveSettings () {
      MCSettings.It.SaveToJson (settingsFilePath);
      Console.WriteLine ($"Settings file created at: {settingsFilePath}");
   }

   public void LoadSettings () {
      if (File.Exists (settingsFilePath)) {
         try {
            MCSettings.It.LoadFromJson (settingsFilePath);
         } catch (Exception ex) {
            Console.WriteLine ($"Error loading settings: {ex.Message}");
         }
      } else {
         Console.WriteLine ("Settings file not found. Using default settings.");
      }
   }
}
