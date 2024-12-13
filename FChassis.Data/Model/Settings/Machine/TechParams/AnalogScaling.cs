using CommunityToolkit.Mvvm.ComponentModel;
using FChassis.Core.Model;

namespace FChassis.Data.Model.Settings.Machine.TechParams {
   public partial class AnalogScaling : ObservableObject {
      [ObservableProperty, Prop ("Channels", Prop.Type.Text, "Channel0")]
      double? channel0 = 0;

      [ObservableProperty, Prop (Prop.Type.Text, "Channel1")]
      double? channel1 = 1;

      [ObservableProperty, Prop (Prop.Type.Text, "Channel2")]
      double? channel2 = 2;

      [ObservableProperty, Prop (Prop.Type.Text, "Channel3")]
      double? channel3 = 3;

      [ObservableProperty, Prop (Prop.Type.Text, "Channel4")]
      double? channel4 = 4;

      [ObservableProperty, Prop (Prop.Type.Text, "Channel5")]
      double? channel5 = 5;

      [ObservableProperty, Prop (Prop.Type.Text, "Channel6")]
      double? channel6 = 6;

      [ObservableProperty, Prop (Prop.Type.Text, "Channel7")]
      double? channel7 = 7;

      [ObservableProperty, Prop (Prop.Type.Text, "Channel8")]
      double? channel8 = 8;

      [ObservableProperty, Prop (Prop.Type.Text, "Channel9")]
      double? channel9 = 9;

      [ObservableProperty, Prop (Prop.Type.Text, "Channel10")]
      double? channel10 = 10;

      [ObservableProperty, Prop (Prop.Type.Text, "Channel11")]
      double? channel11 = 11;

      [ObservableProperty, Prop (Prop.Type.Text, "Channel12")]
      double? channel12 = 12;

      [ObservableProperty, Prop (Prop.Type.Text, "Channel13")]
      double? channel13 =13;

      [ObservableProperty, Prop (Prop.Type.Text, "Channel14")]
      double? channel14 = 14;

      [ObservableProperty, Prop (Prop.Type.Text, "Channel15")]
      double? channel15 = 15;
   }
}
