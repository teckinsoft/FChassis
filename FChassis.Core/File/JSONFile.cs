using CommunityToolkit.Mvvm.ComponentModel;

using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System;
using System.Diagnostics;

namespace FChassis.Core.File;
/// <summary></summary>
public class JSONFileWrite : FileWrite {
   #region Method
   public bool Write (string path, string objName, object obj) {
      this.rootNode.Add (objName, obj);
      return Write (path);
   }

   public override bool Write (string path) {
      using var fileStream = System.IO.File.Create (path);

      JsonWriterOptions options = new () { Indented = true };
      using Utf8JsonWriter writer = new (fileStream, options);

      if(!this.writeNodeChildren(writer, this.rootNode))
         return false;

      fileStream.Flush ();
      return true;
   }
   #endregion Method

   #region Implement
   bool writeNodeChildren (Utf8JsonWriter writer, ObjectNode node) {
      bool success = true;

      string name;
      object srcObj, obj;
      ObjectNode? childNode;

      #region Write Object -----------------------------------------------
      writer.WriteStartObject (); 
      foreach (var pair in node.children) {
         obj = srcObj = pair.Value;
         name = pair.Key;

         childNode = srcObj as ObjectNode;
         if (childNode != null)   // ObjectNode ----------------
            obj = childNode.obj!;

         if (obj != null)           // Object or Object Node ---
            success = this.writeObject (writer, obj, name);
         else if (node != null)     // Container Node ----------
            success = this.writeNodeChildren (writer, node);

         if(!success)
            return false;
      }

      writer.WriteEndObject ();
      #endregion            

      return success;
   }

   bool writeObject (Utf8JsonWriter writer, object obj, string name) {
      Type objType = obj.GetType ();

      #region Write Array/List Type -----------------------
      Type elementType = null!;
      if (!this.writeArray (writer, obj!, objType, ref elementType, name)) 
         return false;

      if (elementType != null)      // Array or List written
         return true;
      #endregion

      #region Otherwise Write Object Type -----------------
      writer.WriteStartObject (name);
      if (!this.writeObjectClassAttributes (writer, obj, objType))
         return false;

      writer.WriteEndObject ();
      #endregion

      return true;
   }

   // For List and Array
   bool writeArray (Utf8JsonWriter writer, object obj, Type objType, 
                    ref Type elementType, string name) {
      if (!objType.IsArray && !ObjectNode.IsListType (obj, objType!, ref elementType))
         return true;               //  Not List or Array

      if (objType.IsArray)
         elementType = objType.GetElementType ()!;

      #region Write Object or Prop Array -------------
      writer.WriteStartArray (name); // Start ---
      dynamic iteratable = obj;
      if (ObjectNode.IsUserDefinedClass (elementType)) 
         foreach (var element in iteratable)
            writeObjectElement__l (element, ref elementType);
      else
         foreach (var element in iteratable)
            this.writePropElement (writer, element, elementType);

      writer.WriteEndArray ();             // End
      #endregion
      return true;

      #region Local
      bool writeObjectElement__l (object element, ref Type elementType) {
         writer.WriteStartObject ();
         if (!this.writeObjectClassAttributes (writer, element, elementType))
            return false;

         writer.WriteEndObject ();
         return true;
      }
      #endregion Local     
   }

   bool writePropElement (Utf8JsonWriter writer, object element, 
                          Type elementType) {
      if (elementType.IsEnum) {
         writer.WriteStringValue (element.ToString ());
         return true;
      }

      TypeCode typeCode = Type.GetTypeCode (elementType);
      switch (typeCode) {
         case TypeCode.String:
            writer.WriteStringValue (element.ToString ());
            break;

         case TypeCode.Int16:
            writer.WriteNumberValue ((Int16)element);
            break;

         case TypeCode.UInt16:
            writer.WriteNumberValue ((UInt16)element);
            break;

         case TypeCode.Int32:
            writer.WriteNumberValue ((Int32)element);
            break;

         case TypeCode.UInt32:
            writer.WriteNumberValue ((UInt32)element);
            break;

         case TypeCode.Int64:
            writer.WriteNumberValue ((Int64)element);
            break;

         case TypeCode.UInt64:
            writer.WriteNumberValue ((UInt64)element);
            break;

         case TypeCode.Single:
            writer.WriteNumberValue ((Single)element);
            break;

         case TypeCode.Double:
            writer.WriteNumberValue ((double)element);
            break;

         case TypeCode.Decimal:
            writer.WriteNumberValue ((decimal)element);
            break;

         case TypeCode.Boolean:
            writer.WriteBooleanValue ((bool)element);
            break;

         default:
            Debug.Assert ($"'{elementType.ToString ()}' not handled" != "");
            return false;
      }

      return true;
   }

   bool writeObjectClassAttributes (Utf8JsonWriter writer, object obj, Type objType) {
      List<Type> types = Reflection.Object.GetTypeList (objType, typeof (ObservableObject));
      foreach (Type classType in types) 
         if(!this.writeObjectAttributes (writer, obj, classType))
            return false;

      return true;
   }

   bool writeObjectAttributes (Utf8JsonWriter writer, object obj, Type classType) {
      ObservablePropertyAttribute a;
      Type attrType, attrElementType = null!;

      var fields = classType.GetFields (BindingFlags.NonPublic | BindingFlags.Instance);
      foreach (FieldInfo fi in fields) {
         if ((a = fi.GetCustomAttribute<ObservablePropertyAttribute> ()!) == null) 
            continue;

         object attrObj = fi?.GetValue (obj)!;
         if (attrObj == null) {
            //continue;
            return setError ($"Attribute '{fi!.Name}' not found");
         }

         #region Write Object Attribute 
         attrType = attrObj.GetType ();
         if (ObjectNode.IsListType (attrObj, attrType!, ref attrElementType!)
               || ObjectNode.IsUserDefinedClass (attrType)
               || attrType.IsArray) {
            if (!this.writeObject (writer, attrObj!, fi!.Name))
               return false;
            continue;
         }
         #endregion

         // Other Write Attribute
         this.writeObjectAttribute (writer, attrObj, attrType, fi?.Name!);
      }

      return true;
   }

   void writeObjectAttribute (Utf8JsonWriter writer, object value, Type valueType, string name) {
      if (valueType.IsEnum) {
         writer.WriteString (name, value.ToString ());
         return;
      }

      TypeCode attrTypeCode = Type.GetTypeCode (valueType);
      switch (attrTypeCode) {
         case TypeCode.String:
            writer.WriteString (name, value.ToString ());
            break;

         case TypeCode.Int16:
            writer.WriteNumber (name, (Int16)value);
            break;

         case TypeCode.UInt16:
            writer.WriteNumber (name, (UInt16)value);
            break;

         case TypeCode.Int32:
            writer.WriteNumber(name, (Int32)value);
            break;

         case TypeCode.UInt32:
            writer.WriteNumber (name, (UInt32)value);
            break;

         case TypeCode.Int64:
            writer.WriteNumber (name, (Int64)value);
            break;

         case TypeCode.UInt64:
            writer.WriteNumber (name, (UInt64)value);
            break;

         case TypeCode.Single:
            writer.WriteNumber (name, (Single)value);
            break;

         case TypeCode.Double:
            writer.WriteNumber (name, (double)value);
            break;

         case TypeCode.Decimal:
            writer.WriteNumber (name, (decimal)value);
            break;

         case TypeCode.Boolean:
            writer.WriteBoolean (name, (bool)value);
            break;         

         default:
            Debug.Assert ($"'{valueType.ToString ()}' not handled" != "");
            break;
      }
   }
   #endregion Implement
}

//-----------------------------------------------------------------------------
/// <summary></summary>
public class JSONFileRead : FileRead {
   #region Method

   public bool Read (string path, object obj, string objName) {
      this.rootNode.Add (objName, obj);
      return Read (path);
   }

#pragma warning disable CS0162
   public override bool Read (string path) {
      return true;
      if (!System.IO.File.Exists (path))
         return false;

      ReadOnlySpan<byte> jsonReadOnlySpan = System.IO.File.ReadAllBytes (path);
      var reader = new Utf8JsonReader (jsonReadOnlySpan);

      try {
         if (!reader.Read ())
            return true;
      } catch (Exception e) {
         this.error = e.Message;
         return false;
      }

      return this.readObject (this.rootNode, null!, ref reader);
   }
#pragma warning restore CS0162
   #endregion Method

   #region Implement
   /// <summary></summary>
   /// <param name="targetObj">This can be either ObjectNode or object</param>
   /// <param name="name"></param>
   /// <param name="reader"></param>
   /// <returns></returns>
   bool readObject (object targetObj, string name, ref Utf8JsonReader reader) {
      bool success = true;

      ObjectNode? node = targetObj as ObjectNode;
      object? obj = node == null ?targetObj :node!.obj;

      object childObj = null!;
      Type elementType = null!,
           objType = obj?.GetType()!;

      do {
          while (success && reader.Read ()) {
            switch (reader.TokenType) {
               case JsonTokenType.StartObject:
                  success = (childObj = ObjectNode.GetObject (targetObj, objType, name)) == null
                              ? setError ($"Object '{name}' not found")
                              : this.readObject (childObj, name, ref reader);
                  break;

               case JsonTokenType.EndObject:
               case JsonTokenType.EndArray:
                  return true;

               case JsonTokenType.StartArray:
                  success = obj == null   // Container Node ?
                              ? this.readNodeArray (node!, name, ref reader)                  
                              : this.readPropArray (obj, objType, elementType, name, ref reader);
                  break;

               case JsonTokenType.Comment:
                  break;

               case JsonTokenType.PropertyName:
                  name = reader.GetString ()!;
                  break;

               default:
                  success = this.readAttribute(obj!, objType!, name, ref reader);
                  break;
            } // switch - reader.TokenType
         } // while = reader.Read ();
      } while (false);

      return success;
   }

   bool readNodeArray (ObjectNode node, string name, ref Utf8JsonReader reader) {
      object childObj = ObjectNode.GetObject (node, name);
      return childObj == null
                  ? setError ($"Object '{name}' not found")
                  : readArray__l (ref reader);

      #region Local
      bool readArray__l (ref Utf8JsonReader reader) {
         ObjectNode? childNode = childObj as ObjectNode;
         if (childNode == null)
            return setError ($"Array/List '{name}' object should be added as Object Node");

         childObj = childNode.obj!;
         if (childObj == null)
            return setError ($"'{name}' Should not be Container Node for Array/List object");

         Type elementType = null!,
              childObjType = childObj.GetType ();

         object list = null!;
         ObjectKind childKind = ObjectKind.obj;
         if (childObjType.IsArray) {
            childKind = ObjectKind.array;
            elementType = childObjType.GetElementType ()!;
         } else if (ObjectNode.IsListType (childObjType, ref elementType)) {
            list = childNode.obj!;     // To fill Elements to direct to Node -> object
            childKind = ObjectKind.list;
         }

         if (ObjectKind.obj == childKind)
            return setError ($"Object '{name}' should be Array/List");

         // Set Array 
         childNode.obj = this.readArray (list, childObj, childKind, elementType, name, ref reader);
         return true;
      }
      #endregion Local
   }

   bool readPropArray (object obj, Type objType, 
                       Type elementType, string name, ref Utf8JsonReader reader) {
      string cname = this._capitalizeFirstLetter (name);
      PropertyInfo? pi = objType.GetProperty (cname, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

      return pi == null
         ? setError ($"Attribute '{cname}' not found")
         : readArray__l (ref reader);

      #region Local
      bool readArray__l (ref Utf8JsonReader reader) {
         ObjectKind attrKind = ObjectKind.obj;
         Type elementType = null!;
         if (pi.PropertyType.IsArray) {
            attrKind = ObjectKind.array;
            elementType = pi.PropertyType.GetElementType ()!;
         } else if (ObjectNode.IsListType (pi.PropertyType, ref elementType))
            attrKind = ObjectKind.list;
         else
            return setError ($"Attribute '{name}' should not be Array/List");

         object iteratable = this.readArray (null!, obj, attrKind, elementType, name, ref reader);
         if (iteratable == null)
            return false;

         // Set Array 
         pi.SetValue (obj, iteratable);
         return true;
      };
      #endregion Local
   }

   object readArray (object list, object propObj, ObjectKind propKind,
                     Type elementType, string name, ref Utf8JsonReader reader) {
      Type listType = typeof (List<>).MakeGenericType (elementType); 
      if(list ==  null)
         list = Activator.CreateInstance (listType)!;

      var toArrayMethod = listType.GetMethod ("ToArray")!;
      var addMethod = listType.GetMethod ("Add")!;

      bool result = ObjectNode.IsUserDefinedClass (elementType) // Is UserDefined class array
                     ? this.readObjectElements (propObj, elementType, list, addMethod, ref reader)
                     : this.readPropElements (propObj!, elementType, list, addMethod, ref reader);
      if (!result)
         return null!;

      object value = propKind switch {
         ObjectKind.array => (Array)toArrayMethod.Invoke (list, null)!,
         ObjectKind.list  => list,
                        _ => null!
      };

      return value;
   }

   bool readObjectElements (object obj,Type elementType,
                            object list, MethodInfo addMethod,
                            ref Utf8JsonReader reader) {
      bool success = true;
      object childObj;
      while (success && reader.Read ()) {
         switch (reader.TokenType) {
            case JsonTokenType.StartObject:
               if ((childObj = Activator.CreateInstance (elementType)!) == null)
                  return setError ($"Object Element '{elementType.Name}' create failed");

               addMethod.Invoke (list, new object[] { childObj });
               success = this.readObject (childObj, null!, ref reader);
               break;

            case JsonTokenType.EndArray:
               return true;

            default:
               Debug.Assert (false);
               return false;
         }
      }

      return success;
   }

   bool readPropElements (object obj, Type elementType, 
                          object list, MethodInfo addMethod, 
                          ref Utf8JsonReader reader) {
      object value;
      while (reader.Read ()) {
         switch (reader.TokenType) {
            case JsonTokenType.EndArray:
               return true;

            case JsonTokenType.String:
            case JsonTokenType.Number:
            case JsonTokenType.True:
            case JsonTokenType.False:
               value = this.readAttributeValue (elementType, ref reader);
               value = Convert.ChangeType (value, elementType)!;
               addMethod.Invoke (list, [value]);
               break;

            default:
               return setError ($"Unsupported Data Type {reader.TokenType.ToString ()}");
         }
      }

      return true;
   }

   bool readAttribute (object obj, Type objType, string name, ref Utf8JsonReader reader) {
      string cname = this._capitalizeFirstLetter (name);
      PropertyInfo attrPI = objType.GetProperty (cname, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!; 
      if (attrPI == null) 
         return setError ($"Attribute '{name}' not found");

      object value = this.readAttributeValue (attrPI.PropertyType, ref reader);
      if (value == null)
         return false;
       
      attrPI.SetValue (obj, value);
      return true;
   }

   object readAttributeValue (Type attrType, ref Utf8JsonReader reader) {
      object value = null!;
      if (attrType.IsEnum) {
         value = reader.GetString ()!;
         value = Enum.Parse (attrType, (string)value);
         return value;
      }

      switch (reader.TokenType) {
         case JsonTokenType.String:
         case JsonTokenType.True:
         case JsonTokenType.False:
            value = reader.TokenType switch {
               JsonTokenType.String => reader.GetString ()!,
               JsonTokenType.True   => reader.GetBoolean ()!,
               JsonTokenType.False  => reader.GetBoolean ()!,
                                  _ => null!
            };
            break;

         case JsonTokenType.Number:
            Type dataType = _getExactPropertyType (attrType);
            value = dataType switch {
               Type t when t == typeof (Int16)   => reader.GetInt16 ()!,
               Type t when t == typeof (UInt16)  => reader.GetUInt16 ()!,
               Type t when t == typeof (Int32)   => reader.GetInt32 ()!,
               Type t when t == typeof (UInt32)  => reader.GetUInt32 ()!,
               Type t when t == typeof (Int64)   => reader.GetInt64 ()!,
               Type t when t == typeof (UInt64)  => reader.GetUInt64 ()!,
               Type t when t == typeof (Single)  => reader.GetSingle ()!,
               Type t when t == typeof (double)  => reader.GetDouble ()!,
               Type t when t == typeof (decimal) => reader.GetDecimal ()!,
                                               _ => null!
            };
            break;

         default:
            break;           
      }

      if(value == null) 
         return setError($"Unsupported Data Type {reader.TokenType.ToString ()}");

      return value!;
   }

   Type _getExactPropertyType (Type propertyType)
     => Nullable.GetUnderlyingType (propertyType) ?? propertyType;

   string _capitalizeFirstLetter (string str)
     => char.ToUpper (str[0]) + str.Substring (1);
   #endregion Implement
}