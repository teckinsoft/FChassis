using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Laser.LaserCutting.Marking {
   public partial class Special : ObservableObject {
      [ObservableProperty, Prop (Prop.Type.DBGrid, null!),
                                  DBGridColProp (Prop.Type.Text, "Name", "Name"),
                                  DBGridColProp (Prop.Type.Text, "Special", "Special")]
      private object[]? dataGrid1;
   }
}
