using FChassis.Data.Model.Settings.Machine.TechParams;
using FChassis.Data.ViewModel;

using Avalonia.Markup.Xaml;

namespace FChassis.UI.Settings.Machine.TechParams; 
public partial class ExhaustSysSettings : Panel {   
   public ExhaustSysSettings () {
      AvaloniaXamlLoader.Load (this);     
      this.AddPropControls (typeof(ExhaustSys), Configuration.exhaustSysVM);
   }
}