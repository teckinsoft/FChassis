using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.PLCOptions; 
public partial class PLCKey : ObservableObject {
   [ObservableProperty, Prop ("PLCKey1",
                              Prop.Type.DBGrid, null!),
                                   DBGridColProp (Prop.Type.Text, "Name", "Name"),
                                   DBGridColProp (Prop.Type.Text, "Type", "Type"),
                                   DBGridColProp (Prop.Type.Text, "Function", "Function")]
   private object[]? plcKey1s;

   [ObservableProperty, Prop ("PLCKey2",
                              Prop.Type.DBGrid, null!),
                                   DBGridColProp (Prop.Type.Text, "Name", "Name"),
                                   DBGridColProp (Prop.Type.Text, "Type", "Type"),
                                   DBGridColProp (Prop.Type.Text, "Function", "Function")]
   private object[]? plcKey2s;

   [ObservableProperty, Prop ("PLCKey3",
                              Prop.Type.DBGrid, null!),
                                   DBGridColProp (Prop.Type.Text, "Name", "Name"),
                                   DBGridColProp (Prop.Type.Text, "Type", "Type"),
                                   DBGridColProp (Prop.Type.Text, "Function", "Function")]
   private object[]? plcKey3s;

   [ObservableProperty, Prop ("PLCKey4",
                              Prop.Type.DBGrid, null!),
                                   DBGridColProp (Prop.Type.Text, "Name", "Name"),
                                   DBGridColProp (Prop.Type.Text, "Type", "Type"),
                                   DBGridColProp (Prop.Type.Text, "Function", "Function")]
   private object[]? plcKey4s;

   [ObservableProperty, Prop ("PLCKey5",
                              Prop.Type.DBGrid, null!),
                                   DBGridColProp (Prop.Type.Text, "Name", "Name"),
                                   DBGridColProp (Prop.Type.Text, "Type", "Type"),
                                   DBGridColProp (Prop.Type.Text, "Function", "Function")]
   private object[]? plcKey5s;

   [ObservableProperty, Prop ("PLCKey6",
                              Prop.Type.DBGrid, null!),
                                   DBGridColProp (Prop.Type.Text, "Name", "Name"),
                                   DBGridColProp (Prop.Type.Text, "Type", "Type"),
                                   DBGridColProp (Prop.Type.Text, "Function", "Function")]
   private object[]? plcKey6s;

   [ObservableProperty, Prop ("PLCKey7",
                              Prop.Type.DBGrid, null!),
                                   DBGridColProp (Prop.Type.Text, "Name", "Name"),
                                   DBGridColProp (Prop.Type.Text, "Type", "Type"),
                                   DBGridColProp (Prop.Type.Text, "Function", "Function")]
   private object[]? plcKey7s;

   [ObservableProperty, Prop ("PLCKey8",
                              Prop.Type.DBGrid, null!),
                                   DBGridColProp (Prop.Type.Text, "Name", "Name"),
                                   DBGridColProp (Prop.Type.Text, "Type", "Type"),
                                   DBGridColProp (Prop.Type.Text, "Function", "Function")]
   private object[]? plcKey8s;

   [ObservableProperty, Prop ("PLCKey9",
                              Prop.Type.DBGrid, null!),
                                   DBGridColProp (Prop.Type.Text, "Name", "Name"),
                                   DBGridColProp (Prop.Type.Text, "Type", "Type"),
                                   DBGridColProp (Prop.Type.Text, "Function", "Function")]
   private object[]? plcKey9s;

   [ObservableProperty, Prop ("PLCKey10",
                              Prop.Type.DBGrid, null!),
                                   DBGridColProp (Prop.Type.Text, "Name", "Name"),
                                   DBGridColProp (Prop.Type.Text, "Type", "Type"),
                                   DBGridColProp (Prop.Type.Text, "Function", "Function")]
   private object[]? plcKey10s;

   [ObservableProperty, Prop ("PLCKey11",
                              Prop.Type.DBGrid, null!),
                                   DBGridColProp (Prop.Type.Text, "Name", "Name"),
                                   DBGridColProp (Prop.Type.Text, "Type", "Type"),
                                   DBGridColProp (Prop.Type.Text, "Function", "Function")]
   private object[]? plcKey11s;

   [ObservableProperty, Prop ("PLCKey12",
                              Prop.Type.DBGrid, null!),
                                   DBGridColProp (Prop.Type.Text, "Name", "Name"),
                                   DBGridColProp (Prop.Type.Text, "Type", "Type"),
                                   DBGridColProp (Prop.Type.Text, "Function", "Function")]
   private object[]? plcKey12s;
}
