namespace FChassis.Core.Model;
[System.AttributeUsage (System.AttributeTargets.Field, AllowMultiple = false)]
public class Prop (Prop.Type type, string label, string unit = null!, string bindName = null!, object[] items = null!) 
             : System.Attribute {

   public Prop (string groupName, Type type, string label, string unit = null!, string bindName = null!, object[] items = null!)
   : this (type, label, unit, bindName, items) {
      this.groupName = groupName;
   }

   public string groupName = null!;
   public Type type = type;
   public string label = label;

   public string unit = unit;

   public object[] items = items;
   public string bindName = bindName;

   public object control = null!;

   #region Inner Class --------------------------------------------------------
   public enum Type {
      Text,
      Combo,
      Check,
      Button,
      DBGrid,
   };
   #endregion Inner Class
}

// -----------------------------------------------------------------
[System.AttributeUsage (System.AttributeTargets.Field, AllowMultiple = true)]
public class PropBind (string name, object property)
             : System.Attribute {
   public object property = property;
   public string name = name;
}

// -----------------------------------------------------------------
[System.AttributeUsage (System.AttributeTargets.Field, AllowMultiple = true)]
public class DBGridColProp (Prop.Type type, string header, string bindName)
             : System.Attribute {
   public Prop.Type type = type;
   public string header = header;
   public string bindName = bindName;
}