#pragma once
#include <gp_Ax1.hxx>
#include <gp_Trsf.hxx>
#include <gp_Vec.hxx>
#include <gp_Dir.hxx>
#include <gp_Lin.hxx>
#include <gp_Pnt.hxx>

#include <BRepAlgoAPI_Fuse.hxx>
#include <BRepBuilderAPI_Transform.hxx>
#include <BRepBndLib.hxx>
#include <BRepTools.hxx>
#include <BRep_Builder.hxx>
#include <BRep_Tool.hxx>
#include <BRepCheck_Analyzer.hxx>
#include <BRepClass3d_SolidClassifier.hxx>
#include <BRepExtrema_DistShapeShape.hxx>
#include <BRepBuilderAPI_Sewing.hxx>
#include <BRepBuilderAPI_Copy.hxx>
#include <BRepLib.hxx>
#include <BRepBuilderAPI_MakeShell.hxx>
#include <BRepBuilderAPI_MakeSolid.hxx>

#include <BOPAlgo_BOP.hxx>
#include <BOPAlgo_Alerts.hxx>
#include <Message_Report.hxx>
#include <Message_Alert.hxx>
#include <Message_Msg.hxx>

#include <IGESControl_Reader.hxx>
#include <IGESControl_Writer.hxx>

#include <Graphic3d_Mat4.hxx>

#include <TopoDS_Shape.hxx>
#include <TopoDS_Compound.hxx>
#include <TopExp_Explorer.hxx>
#include <TopAbs_ShapeEnum.hxx>
#include <TopoDS_Face.hxx>
#include <TopTools_IndexedMapOfShape.hxx>
#include <TopExp.hxx>
#include <TopoDS.hxx>
#include <TopoDS_Iterator.hxx>   // For iterating through compounds

#include <Bnd_Box.hxx>
#include <V3d_Viewer.hxx>
#include <V3d_View.hxx>

#include <AIS_Shape.hxx>
#include <AIS_InteractiveContext.hxx>

#include <Aspect_DisplayConnection.hxx>
#include <Aspect_NeutralWindow.hxx>

#include <OpenGl_GraphicDriver.hxx>
#include <Image_AlienPixMap.hxx>

#include <WNT_Window.hxx>

#include <GeomAdaptor_Surface.hxx>
#include <GeomAbs_SurfaceType.hxx>
#include <Geom_SurfaceOfRevolution.hxx>
#include <GeomLProp_SurfaceTool.hxx>
#include <Geom_Surface.hxx>
#include <GeomAPI_ProjectPointOnSurf.hxx>
#include <GeomAPI_IntCS.hxx>
#include <Geom_Line.hxx>
#include <Geom_TrimmedCurve.hxx>
#include <GeomConvert.hxx>
#include <GeomConvert_CompCurveToBSplineCurve.hxx>

#include <Standard_Handle.hxx>
#include <ShapeUpgrade_UnifySameDomain.hxx>

#include <ShapeFix_Shape.hxx>
#include <ShapeFix_Shell.hxx>

#include <tcl.h>
