using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Laser.LaserCutting.Evapourating {
   public partial class EvapouratingBase : ObservableObject {
      [ObservableProperty, Prop (Prop.Type.DBGrid, null!),
                                  DBGridColProp (Prop.Type.Text, "Name", "Name"),
                                  DBGridColProp (Prop.Type.Text, "Contour", "Contour"),
                                  DBGridColProp (Prop.Type.Text, "RampControl", "RampControl")]
      private object[]? dataGrid1;

      [ObservableProperty, Prop (Prop.Type.DBGrid, null!),
                                  DBGridColProp (Prop.Type.Text, "Name", "Name"),
                                  DBGridColProp (Prop.Type.Text, "Common", "Common")]
      private object[]? dataGrid2;
   }

   public  class Large : EvapouratingBase { }
   public  class Medium : EvapouratingBase { }
   public  class Small : EvapouratingBase { }
}
