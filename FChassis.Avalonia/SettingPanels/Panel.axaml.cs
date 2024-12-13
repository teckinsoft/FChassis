using FChassis.Core.Model;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FChassis.Avalonia.Settings;
public partial class Panel : FChassis.Control.Avalonia.Panels.Child {
   internal void AddPropControls (Type type, object dataContext = null!) {
      if (dataContext != null) // Set DataContext
         this.DataContext = dataContext;

      Grid? grid = null!;
      if (this.LogicalChildren.Count > 0 && this.LogicalChildren[0] != null)
         grid = this.LogicalChildren[0].LogicalChildren[0] as Grid;
      else {
         // Create ScrollView and Grid if found
         var scrollViewer = new ScrollViewer ();
         this.Content = scrollViewer;

         grid = new Grid ();
         for (int i = 0; i < 5; i++)
            grid.ColumnDefinitions.Add (new ColumnDefinition { Width = new GridLength (20, GridUnitType.Star) });

         scrollViewer.Content = grid;
      }

      this.AddPropControls (grid!, type);
   }

   internal void AddPropControls (Grid grid, Type type) {
      Type baseType = typeof (ObservableObject);
      List<Type> types = Core.Reflection.Object.GetTypeList (type, baseType);
      foreach (var iType in types)
         if (baseType != iType)
            this.addPropControls (grid!, iType);
   }

   internal void addPropControls (Grid grid, Type type) {
      int row = grid.RowDefinitions.Count;

      var fields = type.GetFields (BindingFlags.NonPublic | BindingFlags.Instance);
      foreach (FieldInfo f in fields) {
         var p = f.GetCustomAttribute<Prop> ()!;
         if (p == null) continue;

         Border border = null!;
         TextBlock textBlock = null!;
         if (p.groupName != null) {
            border = new Border ();
            grid.RowDefinitions.Add (new RowDefinition { Height = new (32) });
            _setGridRowColumn (border, row++, 0, 5);
            border.Classes.Add ("header");
            grid.Children.Add (border);

            textBlock = new TextBlock ();
            textBlock.Text = p.groupName;
            textBlock.Classes.Add ("title");
            border.Child = textBlock;
         }

         grid.RowDefinitions.Add (new RowDefinition { Height = new GridLength (1, GridUnitType.Auto) });

         Label label = null!;
         DataGrid dGrid = null!;
         global::Avalonia.Controls.Control control = null!;
         switch (p.type) {
            case Prop.Type.Button:
               p.control = control = new Button () { Content = p.label };
               _setGridRowColumn (control, row, 2);
               break;

            case Prop.Type.Text:
            case Prop.Type.Combo:
            case Prop.Type.Check:
               if (p.type != Prop.Type.Check) {
                  label = new Label ();
                  label.Content = p.label;
                  label.Classes.Add ("info");
                  _setGridRowColumn (label, row, 0, 2);
                  grid.Children.Add (label);
               }

               p.control = control = p.type switch {
                  Prop.Type.Text  => new TextBox (),
                  Prop.Type.Combo => new ComboBox (),
                  Prop.Type.Check => new CheckBox () { Content = p.label },
                                _ => null!
               };

               _setGridRowColumn (control, row, 2);

               if (p.unit != null) {
                  label = new Label ();
                  label.Content = p.unit;
                  label.Classes.Add ("blue");
                  _setGridRowColumn (label, row, 3);
                  grid.Children.Add (label);
               }
               break;

            case Prop.Type.DBGrid:
               p.control = control = dGrid = _createDBGridColumns (p, f);
               dGrid.SetCurrentValue (Grid.RowProperty, row);
               break;
         }

         if (control != null) {
            _bindProp (control, f);

            (AvaloniaProperty property, Type type)? sp = p.type switch {
               Prop.Type.Text   => (TextBox.TextProperty, typeof (DataGrid)),
               Prop.Type.Check  => (CheckBox.IsCheckedProperty, typeof (TextBox)),
               Prop.Type.Button => (Button.CommandProperty, typeof (Button)),
               Prop.Type.Combo  => (ComboBox.SelectedItemProperty, typeof (ComboBox)),
                              _ => null!
            };

            if (sp != null) {
               control.Bind (sp.Value.property, new Binding (_capitalizeFirstLetter (f.Name)));
               if (p.type == Prop.Type.Combo)
                  _bindItemSource (control, sp.Value.type, p, ComboBox.ItemsSourceProperty);
            }

            grid.Children.Add (control);
         }

         row++;
      }

      #region Local function
      void _setGridRowColumn (global::Avalonia.Controls.Control control, int row, int col, int colSpan = 1) {
         control.SetCurrentValue (Grid.RowProperty, row);
         control.SetCurrentValue (Grid.ColumnProperty, col);
         control.SetCurrentValue (Grid.ColumnSpanProperty, colSpan);
      }

      string _capitalizeFirstLetter (string str)
         => char.ToUpper (str[0]) + str.Substring (1);

      void _bindProp (global::Avalonia.Controls.Control control, FieldInfo f) {
         var pbis = f.GetCustomAttributes<PropBind> ()!;
         foreach (var bi in pbis) {
            if (bi == null) continue;
            control.Bind ((AvaloniaProperty)bi.property, new Binding (bi.name));
         }
      }

      void _bindItemSource (AvaloniaObject obj, Type objType, Prop p, AvaloniaProperty itemSourceProperty) {
         if (p.items != null) {
            Type type = obj.GetType ();
            PropertyInfo piInstance = objType.GetProperty ("ItemsSource")!;
            piInstance.SetValue (obj, p.items);

         } else if (p.bindName != null)
            obj.Bind (itemSourceProperty, new Binding (_capitalizeFirstLetter (p.bindName)));
      }

      DataGrid _createDBGridColumns (Prop p, FieldInfo f) {
         var dbGrid = new DataGrid ();
         _bindItemSource (dbGrid, typeof (DataGrid), p, DataGrid.ItemsSourceProperty);

         DataGridColumn column = null!;
         var dbgcis = f.GetCustomAttributes<DBGridColProp> ()!;
         foreach (var dgci in dbgcis) {
            switch (dgci.type) {
               case Prop.Type.Text:
                  column = new DataGridTextColumn ();
                  ((DataGridTextColumn)column).Binding = new Binding (dgci.bindName);
                  break;

               case Prop.Type.Check:
                  column = new DataGridCheckBoxColumn ();
                  break;
            }

            if (column == null)
               continue;

            column.Header = dgci.header;
            dbGrid.Columns.Add (column);
            column = null!;
         }

         return dbGrid;
      }
      #endregion Local function
   }

   internal void AddParameterControls (Grid grid, ControlInfo[] controlInfos) {
      int row = grid.RowDefinitions.Count;
      foreach (var ci in controlInfos) {
         grid.RowDefinitions.Add (new RowDefinition { Height = new (32) });

         Border border = null!;
         Label label = null!;
         TextBlock textBlock = null!;
         DataGrid dGrid = null!;

         switch (ci.type) {
            case ControlInfo.Type.Group:
               border = new Border ();
               setGridRowColumn (border, row, 0, 5);
               border.Classes.Add ("header");
               grid.Children.Add (border);

               textBlock = new TextBlock ();
               textBlock.Text = ci.label;
               textBlock.Classes.Add ("title");
               border.Child = textBlock;
               break;

            case ControlInfo.Type.Button:
               ci.control = new Button () { Content = ci.label };
               setGridRowColumn (ci.control, row, 2);
               break;

            case ControlInfo.Type.Text_:
            case ControlInfo.Type.Combo:
            case ControlInfo.Type.Check:
               if (ci.type != ControlInfo.Type.Check) {
                  label = new Label ();
                  label.Content = ci.label;
                  setGridRowColumn (label, row, 0, 2);
                  label.Classes.Add ("info");
                  grid.Children.Add (label);
               }

               ci.control = ci.type switch {
                  ControlInfo.Type.Text_ => new TextBox (),
                  ControlInfo.Type.Combo => new ComboBox (),
                  ControlInfo.Type.Check => new CheckBox () { Content = ci.label},
                                       _ => null!
               };

               setGridRowColumn (ci.control, row, 2);

               if (ci.unit != null) {
                  label = new Label ();
                  label.Content = ci.unit;
                  label.Classes.Add ("blue");
                  setGridRowColumn (label, row, 3);
                  grid.Children.Add (label);
               }
               break;

            case ControlInfo.Type.DGrid:
               DGridControlInfo dgi = (DGridControlInfo)ci;
               ci.control = dGrid = createDGridColumns (dgi.columns, dgi.collections);
               grid.RowDefinitions[row].Height = new GridLength (1, GridUnitType.Auto);
               dGrid.SetCurrentValue (Grid.RowProperty, row);
               break;
         }

         if (ci.control != null) {
            List<ControlInfo.BindInfo> bis = ci.bindInfos!;
            if (bis != null)
               bind (ci.control, bis!);

            grid.Children.Add (ci.control);
         }

         row++;
      }

      void setGridRowColumn (global::Avalonia.Controls.Control control, int row, int col, int colSpan = 1) {
         control.SetCurrentValue (Grid.RowProperty, row);
         control.SetCurrentValue (Grid.ColumnProperty, col);
         control.SetCurrentValue (Grid.ColumnSpanProperty, colSpan);
      }

      #region Local function
      void bind (global::Avalonia.Controls.Control control, List<ControlInfo.BindInfo> bindInfos) {
         foreach (ControlInfo.BindInfo bi in bindInfos) {
            if (bi == null) continue;
            control.Bind (bi.property, bi.binding); }
         }

         DataGrid createDGridColumns (DGridControlInfo.ColInfo[] dgcis, IEnumerable collections) {
         DataGrid dGrid = new DataGrid ();
         dGrid.ItemsSource = collections;
         DataGridColumn column = null!;
         foreach (var dgci in dgcis) {
            switch (dgci.type) {
               case ControlInfo.Type.Text_:
                  column = new DataGridTextColumn ();
                  ((DataGridTextColumn)column).Binding = new Binding (dgci.path);
                  break;
               case ControlInfo.Type.Check:
                  column = new DataGridCheckBoxColumn ();
                  break;
            }

            if (column == null)
               continue;

            column.Header = dgci.header;
            dGrid.Columns.Add (column);
            column = null!;
         }

         return dGrid;
      }
      #endregion Local function
   }
}

#region Run Time ControlInfo 
internal class ControlInfo (ControlInfo.Type type, string label, string unit = null!) {
   static internal BindInfo Bind (string name, AvaloniaProperty property)
      => new BindInfo (name, property);

   // -------------------------------------------------------------------------
   #region Types enum
   internal enum Type {
      None,
      Group,
      Text_,
      Combo,
      Check,
      Button,
      DGrid,
   };
   #endregion Types enum

   internal Type type = type;
   internal string label = label;
   internal string unit = unit;
   internal object[] items = null!;

   internal global::Avalonia.Controls.Control control = null!;
   internal object binding = null!;
   internal List<BindInfo> bindInfos = new();

   #region Inner Class --------------------------------------------------------
   internal class BindInfo(string name, AvaloniaProperty property) {
      internal AvaloniaProperty property = property;
      internal Binding binding = new Binding (name);
   }
   #endregion Inner Class
}

#region Specialized ControlInfo classes ---------------------------------------
internal class GroupControlInfo : ControlInfo {
   internal GroupControlInfo (string label, string unit = null!)
      : base (Type.Group, label, unit) { }
}

internal class _TextControlInfo : ControlInfo {
   internal _TextControlInfo (string label, string bindName, string unitName = null!)
      : base (Type.Text_, label, unitName) {
      this.bindInfos = [Bind (bindName, TextBox.TextProperty)]; }
}

internal class ComboControlInfo : ControlInfo {
   internal ComboControlInfo (string label, string bindName, string itemsName, string unitName = null!)
      : base (Type.Combo, label, unitName) {
      this.bindInfos.Add(Bind (bindName, ComboBox.SelectedItemProperty));
      if (itemsName != null)
         this.bindInfos.Add (Bind (itemsName, ComboBox.ItemsSourceProperty));
   }
}

internal class CheckControlInfo : ControlInfo {
   internal CheckControlInfo (string label, string bindName)
      : base (Type.Check, label) {
         this.bindInfos = [Bind (bindName, CheckBox.IsCheckedProperty)]; }
}

internal class ButtonControlInfo : ControlInfo {
   internal ButtonControlInfo (string label, string bindName)
      : base (Type.Button, label) {
         this.bindInfos = [Bind (bindName, global::Avalonia.Controls.Button.CommandProperty)]; }
}

internal class DGridControlInfo : ControlInfo {
   internal DGridControlInfo (string label = null!)
      : base (Type.DGrid, label) { }

   internal IEnumerable collections { get; set; } = null!;
   internal ColInfo[] columns = null!;

   internal class ColInfo {
      internal Type type = Type.None;
      internal string header = null!;
      internal string path = null!;
   }
}
#endregion Specialized ControlInfo classes
#endregion  Run Time ControlInfo 