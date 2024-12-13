using System.Collections.Generic;
using System;

namespace FChassis.Core.Reflection;
public class Object {
   static public List<Type> GetTypeList (Type type, Type baseType) {
      List<Type> types = new List<Type> ();
      Type _type = type;
      while (true) {
         if (baseType == null! || _type == baseType)
            break;

         types.Add (_type);   
         _type = _type.BaseType!;
      }

      // Base to Derived classes
      types.Reverse ();
      return types;
   }
}