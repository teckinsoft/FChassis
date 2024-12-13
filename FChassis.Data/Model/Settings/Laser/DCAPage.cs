using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Laser;
public partial class DCAPage : ObservableObject {
   [ObservableProperty, Prop (Prop.Type.DBGrid, null!),
                                    DBGridColProp (Prop.Type.Text, "Name", "Name"),
                                    DBGridColProp (Prop.Type.Text, "Acc", "Acc"),
                                    DBGridColProp (Prop.Type.Text, "RampTime", "RampTime"),
                                    DBGridColProp (Prop.Type.Text, "Tolerance", "Tolerance"),
                                    DBGridColProp (Prop.Type.Text, "Angle", "Angle"),
                                    DBGridColProp (Prop.Type.Text, "Limit Factor", "LimitFactor"),]
   private object[]? settings;
}