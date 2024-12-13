using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Machine.TechParams; 
public partial class ExhaustSys : ObservableObject {
   [ObservableProperty, Prop ("Sections", 
                              Prop.Type.DBGrid, "Sections", null!, "BindName"),
                                 DBGridColProp (Prop.Type.Text, "Section Number", "BindName1"),
                                 DBGridColProp (Prop.Type.Text, "X ON", "BindName2"),
                                 DBGridColProp (Prop.Type.Text, "X OFF", "BindName2"),
                                 DBGridColProp (Prop.Type.Text, "Y ON", "BindName2"),
                                 DBGridColProp (Prop.Type.Text, "Y OFF", "BindName2")]
   private string? sections;


   [ObservableProperty, Prop ("Splitters", 
                              Prop.Type.DBGrid, "Splitters", null!, "BindName"),
                                 DBGridColProp (Prop.Type.Text, "X ON", "BindName1"),
                                 DBGridColProp (Prop.Type.Text, "X OFF", "BindName2"),
                                 DBGridColProp (Prop.Type.Text, "Y ON", "BindName2"),
                                 DBGridColProp (Prop.Type.Text, "Y OFF", "BindName2")]
   private string? splitters;
}
