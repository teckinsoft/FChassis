using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FChassis.VM;
internal partial class JoinControl : ObservableObject {
   #region Property
   [ObservableProperty] string leftFileName = "";
   [ObservableProperty] string rightFileName = "";
   #endregion Property

   #region Command
   [RelayCommand]
   void LeftLoad () { }
   
   [RelayCommand]
   void RightLoad () { }

   [RelayCommand]
   void LeftFlip () { }

   [RelayCommand]
   void RightFlip () { }

   [RelayCommand]
   void Join () { }
   #endregion Command
}

