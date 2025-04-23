namespace FChassis.Installer;
public partial class ComponentPage : UserControl {
   // Constructions
   public ComponentPage () {}

   public virtual bool onChange()
      => true;

   public Component? component;
}
