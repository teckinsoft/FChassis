using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Laser.LaserCutting.Piercing {
   public partial class Multiple : ObservableObject{
      [ObservableProperty, Prop (Prop.Type.DBGrid, null!),
                                  DBGridColProp (Prop.Type.Text, "Name", "Name"),
                                  DBGridColProp (Prop.Type.Text, "Step1", "Step1"),
                                  DBGridColProp (Prop.Type.Text, "Step2", "Step2"),
                                  DBGridColProp (Prop.Type.Text, "Step3", "Step3"),
                                  DBGridColProp (Prop.Type.Text, "Step4", "Step4"),
                                  DBGridColProp (Prop.Type.Text, "Step5", "Step5"),
                                  DBGridColProp (Prop.Type.Text, "Step6", "Step6"),
                                  DBGridColProp (Prop.Type.Text, "Step7", "Step7"),
                                  DBGridColProp (Prop.Type.Text, "Step8", "Step8"),
                                  DBGridColProp (Prop.Type.Text, "Step9", "Step9"),
                                  DBGridColProp (Prop.Type.Text, "Step10", "Step10")]
      private object[]? dataGrid1;

      [ObservableProperty, Prop (Prop.Type.DBGrid, null!),
                                  DBGridColProp (Prop.Type.Text, "Name", "Name"),
                                  DBGridColProp (Prop.Type.Text, "Common", "Common")]
      private object[]? dataGrid2;
   }
}
