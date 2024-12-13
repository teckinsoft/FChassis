using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Laser.LaserCutting.Piercing {
   public partial class DotPunch : ObservableObject{
      [ObservableProperty, Prop (Prop.Type.DBGrid, null!),
                                  DBGridColProp (Prop.Type.Text, "Name", "Name"),
                                  DBGridColProp (Prop.Type.Text, "DotPunch", "DotPunch")]
      private object[]? dotPunchs;
   }
}
