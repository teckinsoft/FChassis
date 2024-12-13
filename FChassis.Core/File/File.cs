using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace FChassis.Core.File;
//-----------------------------------------------------------------------------
/// <summary></summary>
public enum ObjectKind {
   obj,
   list,
   array,
}

public partial class ObjectNode {
   public object? obj;
   public Dictionary<string, object> children = new ();
}

//-----------------------------------------------------------------------------
/// <summary></summary>
public abstract class File {
   public ObjectNode rootNode = new ();   
   public string? error;

   public bool setError(string error) {  
      this.error = error; 
      Debug.Assert(this.error != null);
      return false;
   }
}

//-----------------------------------------------------------------------------
/// <summary></summary>
public abstract class FileRead : File {
   // Overridable   
   public abstract bool Read (string path);
}

//-----------------------------------------------------------------------------
/// <summary></summary>
public abstract class FileWrite : File {
   // Overridable
   public abstract bool Write (string path);
}

//-----------------------------------------------------------------------------
public partial class ObjectNode {
   public object Add (string name, object obj) {
      Debug.Assert(this.obj == null);    // Object should not be assigned

      Type elementType = null!;
      Type objType = obj.GetType ();
      if(objType.IsArray || IsListType (obj, objType, ref elementType)) 
         return this.AddNode (name, obj);
      
      this.children.Add (name, obj!);
      return obj;
   }

   public ObjectNode AddNode (string name, object obj) {
      Debug.Assert (this.obj == null);    // Object should not be assigned
      
      ObjectNode childNode = new () { obj = obj };
      this.children.Add (name, childNode);
      return childNode;
   }

   public static object GetObject (ObjectNode node, string name) {
      object childObj = null!;
      if (node.children.TryGetValue (name, out childObj!))  // Find from children
         return childObj;  // Node or Object

      return childObj;
   }

   public static object GetObject (object srcObj, System.Type objType, string name) {
      if (srcObj is ObjectNode) 
         return GetObject ((ObjectNode)srcObj, name);

      FieldInfo fi = objType.GetField (name, BindingFlags.NonPublic | BindingFlags.Instance)!;
      if (fi != null!)
         return fi.GetValue (srcObj)!;

      return null!;
   }

   static internal bool IsUserDefinedClass (System.Type type)
      =>  type is not null
          && !type.IsClass
          && !type.IsPrimitive
          && !type.IsEnum
          && !type.IsArray
          && type != typeof (string)
          && type != typeof (decimal)
          && type != typeof (DateTime);

   internal static bool IsListType (System.Type objType, ref System.Type elementType) {
      if (objType.IsGenericType && typeof (List<>).IsAssignableFrom (objType.GetGenericTypeDefinition ())) {
         elementType = objType.GetGenericArguments ()[0];
         return true;
      }

      return false;
   }

   internal static bool IsListType (object obj, System.Type objType, ref System.Type elementType) {
      bool isList = obj is IList && objType.IsGenericType && objType.GetGenericTypeDefinition () == typeof (List<>);
      if (isList)
         elementType = objType.GetGenericArguments ()[0];

      return isList;
   }

   internal static object CreateElement (object obj, System.Type objType, ObjectKind objKind, 
                                         System.Type elementType) {
      object childObj = Activator.CreateInstance (elementType)!;
      if (childObj == null)
         return null!;

      if(objKind == ObjectKind.list) {
         MethodInfo addMethod = objType!.GetMethod ("Add")!;
         addMethod.Invoke (obj, new object[] { childObj });
      }      

      return childObj;
   }
}