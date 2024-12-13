using CommunityToolkit.Mvvm.Input;

namespace FChassis.Data.ViewModel.Settings.Machine.General;
public partial class MachineViewModel : Model.Settings.Machine.General.Machine {
   [RelayCommand]
   public void IncrementLimit1 () {
      this.OverrideLimit += 1;
   }

   //[ObservableProperty] string[] codes = ["12", "13", "14", "15", "16"];
}
