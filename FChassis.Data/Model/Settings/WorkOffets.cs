using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings;
public partial class WorkOffsets : ObservableObject {
   [ObservableProperty, Prop (Prop.Type.DBGrid, null!),
                                   DBGridColProp (Prop.Type.Text, "X", "X"),
                                   DBGridColProp (Prop.Type.Text, "Y", "Y"),
                                   DBGridColProp (Prop.Type.Text, "Z", "Z")]
   private object[]? offsets;
}