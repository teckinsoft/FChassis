using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Laser.LaserCutting.Piercing {
   public partial class Peck : ObservableObject {
      [ObservableProperty, Prop ( Prop.Type.DBGrid, null!),
                                  DBGridColProp (Prop.Type.Text, "Name", "Name"),
                                  DBGridColProp (Prop.Type.Text, "Start", "Start"),
                                  DBGridColProp (Prop.Type.Text, "Increment", "Increment"),
                                  DBGridColProp (Prop.Type.Text, "RampStart", "RampStart"),
                                  DBGridColProp (Prop.Type.Text, "RampEnd", "RampEnd")]
      private object[]? dataGrid1;

      [ObservableProperty, Prop ( Prop.Type.DBGrid, null!),
                                  DBGridColProp (Prop.Type.Text, "Name", "Name"),
                                  DBGridColProp (Prop.Type.Text, "Common", "Common")]
      private object[]? dataGrid2;
   }
}
