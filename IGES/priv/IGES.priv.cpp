#include <assert.h>
#include <iostream>
#include <memory>
#include <vector>
#include <algorithm>
#include <map>

#include <gp_Ax1.hxx>
#include <BRepAlgoAPI_Fuse.hxx>
#include <BRepBuilderAPI_Transform.hxx>
#include <BRepBndLib.hxx>
#include <BRepTools.hxx>
#include <gp_Trsf.hxx>
#include <gp_Vec.hxx>
#include <Graphic3d_Mat4.hxx>
#include <IGESControl_Reader.hxx>
#include <IGESControl_Writer.hxx>
#include <TopoDS_Shape.hxx>
#include <Bnd_Box.hxx>
#include <V3d_Viewer.hxx>
#include <V3d_View.hxx>
#include <AIS_Shape.hxx>
#include <Aspect_DisplayConnection.hxx>
#include <OpenGl_GraphicDriver.hxx>
#include <Image_AlienPixMap.hxx>
#include <Aspect_NeutralWindow.hxx>
#include <AIS_InteractiveContext.hxx>
#include <AIS_Shape.hxx>
#include <WNT_Window.hxx>
#include <V3d_Viewer.hxx>
#include <TopoDS_Compound.hxx>
#include <BRep_Builder.hxx>
#include <TopExp_Explorer.hxx>
#include <TopAbs_ShapeEnum.hxx>
#include <TopoDS_Face.hxx>
#include <BRep_Tool.hxx>
#include <Geom_Surface.hxx>
#include <GeomAPI_ProjectPointOnSurf.hxx>
#include <TopoDS.hxx>
#include <gp_Pnt.hxx>
#include <GeomAPI_IntCS.hxx>
#include <Geom_Line.hxx>
#include <gp_Dir.hxx>
#include <gp_Lin.hxx>
#include <BRepCheck_Analyzer.hxx>
#include <BRepExtrema_DistShapeShape.hxx>
#include <Geom_TrimmedCurve.hxx>
#include <GeomConvert.hxx>
#include <TopTools_IndexedMapOfShape.hxx>
#include <TopExp.hxx>
#include <BRepBuilderAPI_MakeEdge.hxx>
#include <GeomConvert_CompCurveToBSplineCurve.hxx>
#include <Standard_Handle.hxx>
#include <ShapeUpgrade_UnifySameDomain.hxx>
#include <BRepBuilderAPI_Sewing.hxx>
#include <ShapeFix_Shape.hxx>
#include <ShapeFix_Shell.hxx>
#include <GeomAdaptor_Surface.hxx>
#include <GeomAbs_SurfaceType.hxx>
#include <Geom_SurfaceOfRevolution.hxx>
#include <GeomLProp_SurfaceTool.hxx>

#include "IGES.priv.h"

// --------------------------------------------------------------------------------------------
class IGESShape {
   public:
#define ShapeCount  (3)
   enum class ShapeType {
      Left = 0,
      Right = 1,
      Fused = 2,

      Count = ShapeCount
   };

   public:
   IGESShape() = default;
   explicit IGESShape(const Handle(V3d_Viewer)& aViewer) : viewer(aViewer) {}

   void SetViewer(const Handle(V3d_Viewer)& viewer) {
      this->viewer = viewer; }

   void SetContext(const Handle(AIS_InteractiveContext)& context) {
      this->context = context; }

   Handle(V3d_Viewer) GetViewer() const {
      return this->viewer; }

   Handle(AIS_InteractiveContext) GetContext() const {
      return context; }

   V3d_ListOfView GetActiveViews() {
      return this->viewer->ActiveViews(); }

   void SetFuser(const BRepAlgoAPI_Fuse& fuser) {
      this->fuser = fuser; }

   BRepAlgoAPI_Fuse GetFuser() {
      return this->fuser; }

   void SetShape(ShapeType index, const TopoDS_Shape& shape) {
      assert(index >= ShapeType::Left && index <= ShapeType::Count);
      this->shapes[(int)index] = shape;
   }

   TopoDS_Shape GetShape(ShapeType index) {
      assert(index >= ShapeType::Left && index <= ShapeType::Count);
      return this->shapes[(int)index];
   }

   Bnd_Box GetBBox(const TopoDS_Shape& shape) {
      Bnd_Box bbox;
      BRepBndLib::Add(shape, bbox);
      double xmin, ymin, zmin, xmax, ymax, zmax;
      bbox.Get(xmin, ymin, zmin, xmax, ymax, zmax);
      return bbox;
   }

   // Assuming bbox is a class with a Get method as described
   auto GetBBoxComp(const TopoDS_Shape& shape)
      -> std::tuple<double, double, double, double, double, double> {
      Bnd_Box bbox;
      BRepBndLib::Add(shape, bbox);
      double xmin, ymin, zmin, xmax, ymax, zmax;
      bbox.Get(xmin, ymin, zmin, xmax, ymax, zmax);
      return std::make_tuple(xmin, ymin, zmin, xmax, ymax, zmax);
   }

   TopoDS_Shape TranslateAlongX(const TopoDS_Shape& shape, double requiredTranslation) {
      //double requiredTranslation = leftXmax - rightXmin - 0.4;
      //double requiredTranslation = delta;

      gp_Trsf moveRightTrsf;
      moveRightTrsf.SetTranslation(gp_Vec(requiredTranslation, 0, 0));

      // Apply the translation to mShapeRight
      BRepBuilderAPI_Transform moveRightTransform(shape, moveRightTrsf, true);
      return moveRightTransform.Shape();
   }

   /*int ShortestDistanceBetweenShapes(gp_Pnt& pointOnShape1, gp_Pnt& pointOnShape2, double& rShortDistance) {
      g_IgesStatus.errorNo = 0;

      // Create an instance of BRepExtrema_DistShapeShape
      BRepExtrema_DistShapeShape distanceCalculator(mShapeLeft, mShapeRight);

      // Check if the computation was successful
      if (!distanceCalculator.IsDone())
         return g_IgesStatus.SetError(IGESStatus::CalculationError, "Distance computation between shapes failed");

      // Get the minimum distance
      double minDistance = distanceCalculator.Value();

      // Retrieve the closest points on each shape
      if (distanceCalculator.NbSolution() > 0) {
         pointOnShape1 = distanceCalculator.PointOnShape1(1);
         pointOnShape2 = distanceCalculator.PointOnShape2(1);
      }

      rShortDistance = minDistance;
      return g_IgesStatus.errorNo;
   }

   void SewFlexes(TopoDS_Shape& shape) {
      try {
         // Process and stitch the surfaces of revolution
         TopoDS_Shape resultShape =
         processRevolutions(shape);

         // Save the result
         //BRepTools::Write(resultShape, "stitchedRevolutions.brep");

         std::cout << "Successfully processed and stitched surfaces of revolution." << std::endl;
      }
      catch (const std::exception& e) {
         std::cerr << "Error: " << e.what() << std::endl;
      }
   }*/

private:
   TopoDS_Shape shapes[ShapeCount];

   Handle(V3d_Viewer) viewer; // Open CASCADE viewer
   Handle(AIS_InteractiveContext) context; // AIS Context14
   BRepAlgoAPI_Fuse fuser;
};

// --------------------------------------------------------------------------------------------
IGESPriv::IGESPriv() {
   this->pShape = new IGESShape(); }

IGESPriv::~IGESPriv() {
   if (this->pShape) {
      delete this->pShape;
      this->pShape = nullptr;
   }
}

// File handling
int IGESPriv::LoadIGES(const std::string& filePath, int shapeType /*= 0*/) {
   g_Status.errorNo = IGESStatus::NoError;

   IGESControl_Reader reader;
   if (!reader.ReadFile(filePath.c_str()))
      return g_Status.SetError(IGESStatus::FileReadFailed, "IGES File Read failed");

   reader.TransferRoots();
   TopoDS_Shape shape = TopoDS_Shape(reader.OneShape());
   this->pShape->SetShape((IGESShape::ShapeType)shapeType, shape);

   return g_Status.errorNo;
}

int IGESPriv::SaveIGES(const std::string& filePath, int shapeType /*= 0*/)
{
   g_Status.errorNo = IGESStatus::NoError;

   TopoDS_Shape shape = this->pShape->GetShape((IGESShape::ShapeType)shapeType);
   assert(!shape.IsNull());

   IGESControl_Writer writer;
   writer.AddShape(shape);
   if (!writer.Write(filePath.c_str()))
      g_Status.SetError(IGESStatus::FileWriteFailed, "IGES File Write failed");

   return g_Status.errorNo;   
}

int IGESPriv::SaveAsIGS(const std::string& filePath) {
   g_Status.errorNo = IGESStatus::NoError;

   // Check if mFusedShape is initialized
   TopoDS_Shape fusedShape = this->pShape->GetShape(IGESShape::ShapeType::Fused);
   if (fusedShape.IsNull()) 
      return g_Status.SetError(IGESStatus::ShapeError, "Fused shape is not initialized or empty");
      
   // Verify if mFusedShape has only one connected component
   TopTools_IndexedMapOfShape shapesmap;

   //auto fusedShape = this->pIGESPrivimpl->GetFusedShape();
   TopExp::MapShapes(fusedShape, TopAbs_SOLID, shapesmap);

   if (shapesmap.Extent() != 1) 
      return g_Status.SetError(IGESStatus::FuseError, "Fused shape does not have exactly one connected component");

   // Write mFusedShape to an IGES file
   IGESControl_Writer writer;
   writer.AddShape(fusedShape);

   if (!writer.Write(filePath.c_str())) 
      g_Status.SetError(IGESStatus::FileWriteFailed, "IGES File Write failed");

   // Successfully saved IGES file
   return g_Status.errorNo;
}

int IGESPriv::getShape(TopoDS_Shape& shape, int shapeType) {
   g_Status.errorNo = IGESStatus::NoError;

   assert(shapeType >= (int)IGESShape::ShapeType::Left && shapeType <= (int)IGESShape::ShapeType::Fused);
   shape = this->pShape->GetShape((IGESShape::ShapeType)shapeType);;
   
   if (shape.IsNull()) {
      const char* msg = "No Left Shape is loaded";
      switch (shapeType)
      {
      case 1:
         msg = "No Right Shape is loaded";
         break;

      case 2:
         msg = "No Fused Shape is loaded";      
      }

      return g_Status.SetError(IGESStatus::ShapeError, msg);
   }

   return g_Status.errorNo;
}

// Geometry Process
int IGESPriv::AlignToXYPlane(int shapeType /*= 0*/) {
   TopoDS_Shape shape;
   if (0 != this->getShape(shape, shapeType))
      return g_Status.errorNo;
   
   double xmin, ymin, zmin, xmax, ymax, zmax;
   std::tie(xmin, ymin, zmin, xmax, ymax, zmax) = this->pShape->GetBBoxComp(shape);

   // Calculate dimensions
   double length = xmax - xmin;
   double width = ymax - ymin;
   double height = zmax - zmin;

   // Determine primary axes based on dimensions
   gp_Dir xAxis(1, 0, 0);
   gp_Dir yAxis(0, 1, 0);
   gp_Dir zAxis(0, 0, 1);

   if (length < width)
      std::swap(length, width), std::swap(xAxis, yAxis);

   if (length < height)
      std::swap(length, height), std::swap(xAxis, zAxis);

   if (width < height)
      std::swap(width, height), std::swap(yAxis, zAxis);

   // Align to the required orientation
   gp_Ax3 targetSystem(gp_Pnt(0, 0, 0), zAxis, xAxis); // Z-axis up, X-axis along longest dimension
   gp_Trsf alignmentTrsf;
   alignmentTrsf.SetTransformation(targetSystem, gp_Ax3(gp::Origin(), gp_Dir(0, 0, 1), gp_Dir(1, 0, 0)));
   BRepBuilderAPI_Transform aligner(shape, alignmentTrsf, true);

   // Recalculate the bounding box after alignment
   TopoDS_Shape alignedShape = aligner.Shape();
   std::tie(xmin, ymin, zmin, xmax, ymax, zmax) = this->pShape->GetBBoxComp(alignedShape);

   // Calculate the translation required
   double yMid = (ymax + ymin) / 2.0;
   gp_Vec translation(xmin, yMid, zmin); // Translation vector to position the shape
   translation *= -1; // Move to required global positions

   gp_Trsf translationTrsf;
   translationTrsf.SetTranslation(translation);

   // Apply the translation
   BRepBuilderAPI_Transform finalTransform(alignedShape, translationTrsf, true);

   // Testing the bounds
   // Recalculate the bounding box after alignment
   shape = finalTransform.Shape();

   std::tie(xmin, ymin, zmin, xmax, ymax, zmax) = this->pShape->GetBBoxComp(shape);
   auto xmid = (xmin + xmax) / 2.0;
   auto ymid = (ymin + ymax) / 2.0;
   auto zmid = (zmin + zmax) / 2.0;

   gp_Pnt fromPt(xmid, ymid, zmid);
   gp_Dir fromPtDirNegZ(0, 0, -1);
   gp_Pnt ixnPt;
   if (doesVectorIntersectShape(shape, fromPt, fromPtDirNegZ, ixnPt)) {
      auto xAxis = gp_Dir(1, 0, 0);
      screwRotationAboutMidPart(shape, fromPt, xAxis, 180);
   }

   this->pShape->SetShape((IGESShape::ShapeType)shapeType, shape);
   return g_Status.errorNo;
}

int IGESPriv::RotatePartBy180AboutZAxis(int shapeType) {
   TopoDS_Shape shape;
   if (0 != this->getShape(shape, shapeType))
      return g_Status.errorNo;

   // Compute the bounding box of the shape
   /*Bnd_Box bbox;
   BRepBndLib::Add(shape, bbox);
   bbox.Get(xmin, ymin, zmin, xmax, ymax, zmax);*/
   auto [xmin, ymin, zmin, xmax, ymax, zmax] = this->pShape->GetBBoxComp(shape);

   //double xmin, ymin, zmin, xmax, ymax, zmax;
   //bbox.Get(xmin, ymin, zmin, xmax, ymax, zmax);

   // Calculate the midpoint of the bounding box in X and Y
   double xMid = (xmax + xmin) / 2.0;
   double yMid = (ymax + ymin) / 2.0;

   auto pt = gp_Pnt(xMid, yMid, 0);
   auto parallelaxis = gp_Dir(0, 0, 1);

   screwRotationAboutMidPart(shape, pt, parallelaxis, 180);
   this->pShape->SetShape((IGESShape::ShapeType)shapeType, shape);
   return this->AlignToXYPlane(shapeType);
}

int IGESPriv::UnionShapes() {
   g_Status.errorNo = IGESStatus::NoError;

   TopoDS_Shape leftShape;
   if (0 != this->getShape(leftShape, (int)IGESShape::ShapeType::Left))
      return g_Status.errorNo;

   if (0 != this->mirror(leftShape))
      return g_Status.errorNo;

   TopoDS_Shape mirroredShape;
   if (0 != this->getShape(mirroredShape, (int)IGESShape::ShapeType::Fused))
      return g_Status.errorNo;
   
   // Check if both shapes are solids
   if (leftShape.ShapeType() != TopAbs_SOLID || mirroredShape.ShapeType() != TopAbs_SOLID)
      return g_Status.SetError(IGESStatus::FuseError, "Union operation requires both shapes to be solids");
         
   // Perform the initial union operation
   BRepAlgoAPI_Fuse fuser(leftShape, mirroredShape);
   fuser.Build();

   // Validate the fuse operation
   if (!fuser.IsDone()) 
      return g_Status.SetError(IGESStatus::FuseError, "Initial Boolean union operation failed");

   // Retrieve the initial fused shape
   TopoDS_Shape fusedShape = fuser.Shape();
   if (fusedShape.IsNull()) 
      return g_Status.SetError(IGESStatus::FuseError, "Fused shape is null after the initial union operation");

   // Call the function to handle intersecting bounding curves
   double tolerance = 1e-2; // Adjust the tolerance as needed
   this->handleIntersectingBoundingCurves(fusedShape, tolerance);

   //fusedShape = this->pIGESPrivimpl->processCurvedFaces(fusedShape, tolerance = 1e-3);
   //this->pIGESPrivimpl->SewFlexes(fusedShape);

   // Check for multiple connected components
   TopTools_IndexedMapOfShape solids;
   TopExp::MapShapes(fusedShape, TopAbs_SOLID, solids);

   // If there's more than one solid, merge them
   if (solids.Extent() > 1) {
      std::cout << "Multiple connected components detected. Performing iterative union." << std::endl;

      // Start with the first solid
      TopoDS_Shape unifiedSolid = solids(1);

      // Iteratively fuse the remaining solids
      for (int i = 2; i <= solids.Extent(); ++i) {
         BRepAlgoAPI_Fuse iterativeFuser(unifiedSolid, solids(i));
         iterativeFuser.Build();

         if (!iterativeFuser.IsDone()) 
            return g_Status.SetError(IGESStatus::FuseError, "Iterative union operation failed");

         unifiedSolid = iterativeFuser.Shape();
      }

      // Update the fused shape to the unified result
      fusedShape = unifiedSolid;
   }

   // Store the final fused shape and fuser in the handler
   this->pShape->SetFuser(fuser); // Optionally store the last fuser
   this->pShape->SetShape(IGESShape::ShapeType::Fused, fusedShape);

   // Optional: Validate the final fused shape
   BRepCheck_Analyzer analyzer(fusedShape);
   if (!analyzer.IsValid()) 
      return g_Status.SetError(IGESStatus::FuseError, "Final fused shape is invalid");

   if (hasMultipleConnectedComponents(fusedShape)) 
      return g_Status.SetError(IGESStatus::FuseError, "Fused shape contains multiple connected components");

   std::cout << "Boolean union operation completed successfully." << std::endl;
   return g_Status.errorNo;
}

int IGESPriv::screwRotationAboutMidPart(TopoDS_Shape& shape, const gp_Pnt& pt,
   const gp_Dir& parallelaxis, double angleDegrees) {
   g_Status.errorNo = IGESStatus::NoError;
   if (shape.IsNull())
      return g_Status.SetError(IGESStatus::ShapeError, "No mShapeLeft is loaded to apply screw rotation");

   // Define the axis of rotation (parallel to Z-axis)
   gp_Ax1 rotationAxis(pt, parallelaxis);

   // Create the rotation transformation
   gp_Trsf rotationTrsf;
   rotationTrsf.SetRotation(rotationAxis, angleDegrees * M_PI / 180.0); // Convert degrees to radians

   // Apply the rotation to the shape
   BRepBuilderAPI_Transform rotator(shape, rotationTrsf, true);
   TopoDS_Shape rotatedShape = rotator.Shape();

   // Replace the original shape with the rotated shape
   shape = TopoDS_Shape(rotatedShape);
   return g_Status.errorNo;
}

// Redraw and capture the updated image
void IGESPriv::PerformZoomAndRender(bool zoomIn) {
   if (zoomIn)
      ZoomIn();
   else
      ZoomOut();
}

int IGESPriv::mirror(TopoDS_Shape leftShape) {
   // Compute the bounding box of the left shape
   auto [xmin, ymin, zmin, xmax, ymax, zmax] = this->pShape->GetBBoxComp(leftShape);

   // Define the mirror plane passing through (xmax, ymax, zmax) with normal (-1, 0, 0)
   gp_Pnt planePoint(xmax, ymax, zmax);
   gp_Dir planeNormal(-1, 0, 0);
   gp_Ax2 mirrorPlane(planePoint, planeNormal);

   // Create a transformation for mirroring
   gp_Trsf mirrorTransformation;
   mirrorTransformation.SetMirror(mirrorPlane);

   // Apply the mirroring transformation to the left shape
   BRepBuilderAPI_Transform mirroringTransform(leftShape, mirrorTransformation, true);
   TopoDS_Shape mirroredShape = mirroringTransform.Shape();

   if (mirroredShape.IsNull()) 
      return g_Status.SetError(IGESStatus::FuseError, "Failed to create mirrored shape");
   
   mirroredShape = this->pShape->TranslateAlongX(mirroredShape, -0.9);

   // Store the mirrored shape
   this->pShape->SetShape(IGESShape::ShapeType::Fused, mirroredShape);

   std::cout << "Mirroring operation completed successfully." << std::endl;
   return g_Status.errorNo;
}

bool IGESPriv::hasMultipleConnectedComponents(const TopoDS_Shape& shape) {
   int solidCount = 0;

   // Traverse the shape to count solids
   for (TopExp_Explorer explorer(shape, TopAbs_SOLID); explorer.More(); explorer.Next()) {
      solidCount++;
      if (solidCount > 1) 
         return true;  // More than one solid found
   }

   return false;  // Only one or no solid found
}

bool IGESPriv::isPointOnAnySurface(const TopoDS_Shape& shape, const gp_Pnt& point, double tolerance) {
   for (TopExp_Explorer explorer(shape, TopAbs_FACE); explorer.More(); explorer.Next()) {
      const TopoDS_Face& face = TopoDS::Face(explorer.Current());

      Handle(Geom_Surface) surface = BRep_Tool::Surface(face);

      if (!surface.IsNull()) {
         GeomAPI_ProjectPointOnSurf projector;
         projector.Init(point, surface);

         if (projector.NbPoints() > 0 && projector.LowerDistance() <= tolerance)
            return true;
      }
   }

   return false;
}

bool IGESPriv::doesVectorIntersectShape(const TopoDS_Shape& shape, const gp_Pnt& point, 
                                           const gp_Dir& direction, gp_Pnt& intersectionPoint) {
   // Create a line from the point and direction
   Handle(Geom_Line) geomLine = new Geom_Line(point, direction);

   // Variable to store the closest intersection point and minimum distance
   gp_Pnt closestIntersection;
   double minDistanceSquared = std::numeric_limits<double>::max();
   bool intersectionFound = false;

   // Iterate over all faces in the shape
   for (TopExp_Explorer faceExplorer(shape, TopAbs_FACE); faceExplorer.More(); faceExplorer.Next()) {
      const TopoDS_Face& face = TopoDS::Face(faceExplorer.Current());

      // Get the geometric surface of the face
      Handle(Geom_Surface) surface = BRep_Tool::Surface(face);
      if (surface.IsNull()) 
         continue; // Skip invalid surfaces

      // Check intersection of the line with the surface
      GeomAPI_IntCS intersectionChecker(geomLine, surface);

      // If there is an intersection
      if (intersectionChecker.IsDone() && intersectionChecker.NbPoints() > 0) {
         for (int i = 1; i <= intersectionChecker.NbPoints(); ++i) {
            gp_Pnt candidatePoint = intersectionChecker.Point(i);

            // Calculate the vector from the line origin to the intersection point
            gp_Vec toIntersection(point, candidatePoint);

            // Check if the vector aligns with the specified direction
            if (toIntersection * direction > 0) { // Dot product must be positive
               // Calculate the squared distance
               double distanceSquared = toIntersection.SquareMagnitude();

               // Update the closest point if this one is nearer
               if (distanceSquared < minDistanceSquared) {
                  minDistanceSquared = distanceSquared;
                  closestIntersection = candidatePoint;
                  intersectionFound = true;
               }
            }
         }
      }
   }

   // If an intersection was found, update the output parameter
   if (intersectionFound) {
      intersectionPoint = closestIntersection;
      return true;
   }

   // No valid intersection found
   return false;
}

void IGESPriv::handleIntersectingBoundingCurves(TopoDS_Shape& fusedShape, double tolerance) {
   //// Create the ShapeUpgrade_UnifySameDomain object
   //ShapeUpgrade_UnifySameDomain unify(fusedShape, Standard_True, Standard_True, Standard_False);

   //// Perform the unification
   //unify.Build();

   //// Get the refined shape
   //fusedShape = unify.Shape();

 // Step 1: Sew gaps between surfaces
   BRepBuilderAPI_Sewing sewing(tolerance);
   sewing.Add(fusedShape);
   sewing.Perform();
   TopoDS_Shape sewedShape = sewing.SewedShape();

   // Step 2: Fuse surfaces to create a single solid
   BRepAlgoAPI_Fuse fuse(sewedShape, sewedShape); // Self-fuse
   fuse.Build();
   fusedShape = fuse.Shape();

   // Step 3: Refine the shape to remove small edges
   ShapeUpgrade_UnifySameDomain unify(fusedShape, Standard_True, Standard_True, Standard_False);
   unify.Build();
   fusedShape = unify.Shape();

   // Step 1: Heal the shape to fix gaps and ensure continuity
   Handle(ShapeFix_Shape) shapeFix = new ShapeFix_Shape(fusedShape);
   shapeFix->SetPrecision(1e-3); // Set tolerance for fixing gaps
   shapeFix->Perform(); // Perform the healing operation
   TopoDS_Shape healedShape = shapeFix->Shape();

   // Step 2: Refine the healed shape
   unify = ShapeUpgrade_UnifySameDomain(healedShape, Standard_True, Standard_True, Standard_False);
   unify.Build();

   fusedShape = unify.Shape();
}

// ----------------------------------------------------------------------------------------
struct SurfaceInfo {
   TopoDS_Face face;
   double length; // Longest dimension (Xmax - Xmin)
   double width;  // Shortest dimension
};

// ----------------------------------------------------------------------------------------
// Function to check if a face is a surface of revolution
bool isSurfaceOfRevolution(const TopoDS_Face& face) {
   Handle(Geom_Surface) surface = BRep_Tool::Surface(face);
   if (surface.IsNull()) 
      return false;

   // Check if the surface is a surface of revolution
   Handle(Geom_SurfaceOfRevolution) revSurface = Handle(Geom_SurfaceOfRevolution)::DownCast(surface);
   return !revSurface.IsNull();
}

// Function to compute the bounding box dimensions of a face
SurfaceInfo computeBoundingBox(const TopoDS_Face& face) {
   Bnd_Box bbox;
   BRepBndLib::Add(face, bbox);

   double xmin, ymin, zmin, xmax, ymax, zmax;
   bbox.Get(xmin, ymin, zmin, xmax, ymax, zmax);

   SurfaceInfo info;
   info.face = face;
   info.length = xmax - xmin; // Longest dimension (X-axis)
   info.width = ymax - ymin;  // Shortest dimension (Y-axis)

   return info;
}

// Function to compute the shortest distance between two faces
int computeShortestDistance(const TopoDS_Face& face1, const TopoDS_Face& face2, double& rShortestDistance) {
   BRepExtrema_DistShapeShape distCalc(face1, face2);
   distCalc.Perform();

   if (!distCalc.IsDone())
      return g_Status.SetError(IGESStatus::CalculationError, "Distance calculation failed");

   rShortestDistance = distCalc.Value(); // Returns the shortest distance
   return g_Status.errorNo;
}

// Function to find two surface of revolutions with the longest lengths
int findClosestRevolutions(const TopoDS_Shape& shape, std::pair<SurfaceInfo, SurfaceInfo> rClosedRev, double tolerance = 1e-6) {
   std::vector<SurfaceInfo> revolutions;

   // Extract all faces from the shape
   for (TopExp_Explorer explorer(shape, TopAbs_FACE); explorer.More(); explorer.Next()) {
      TopoDS_Face face = TopoDS::Face(explorer.Current());

      // Check if the face is a surface of revolution
      if (isSurfaceOfRevolution(face)) {
         // Compute bounding box dimensions
         SurfaceInfo info = computeBoundingBox(face);
         revolutions.push_back(info);
      }
   }

   // Sort surfaces by their longest dimension (length)
   std::sort(revolutions.begin(), revolutions.end(), [](const SurfaceInfo& a, const SurfaceInfo& b) {
      return a.length > b.length;
      });

   // Ensure there are enough revolutions to process
   if (revolutions.size() < 2) 
      return g_Status.SetError(IGESStatus::CalculationError, 
                                   "Not enough surfaces of revolution found");

   // Find the first pair with a shortest distance of zero (within tolerance)
   for (size_t i = 0; i < revolutions.size() - 1; ++i) {
      for (size_t j = i + 1; j < revolutions.size(); ++j) {
         const auto& rev1 = revolutions[i];
         const auto& rev2 = revolutions[j];

         // Compute the shortest distance between the two surfaces
         double distance;
         if (0 != computeShortestDistance(rev1.face, rev2.face, distance))
            return g_Status.errorNo;

         // Check if the distance is within the tolerance
         if (distance <= tolerance) {
            rClosedRev = { rev1, rev2 }; // First matching pair
            return g_Status.errorNo;            
         }
      }
   }

   return g_Status.SetError(IGESStatus::CalculationError, 
                                "No matching surfaces of revolution found within the given tolerance");
}

// Function to sew two faces
TopoDS_Shape sewFaces(const TopoDS_Face& face1, const TopoDS_Face& face2, double tolerance) {
   BRepBuilderAPI_Sewing sewing(tolerance);

   // Add the faces to the sewing operation
   sewing.Add(face1);
   sewing.Add(face2);

   // Perform sewing
   sewing.Perform();

   return sewing.SewedShape();
}

// Main function to process and stitch two revolutions
int processRevolutions(TopoDS_Shape& unionShape, const TopoDS_Shape& shape, double tolerance = 1e-6) {
   // Step 1: Find the two longest surface of revolutions
   std::pair<SurfaceInfo, SurfaceInfo> closedRev;
   if (0 != findClosestRevolutions(shape, closedRev))
      return g_Status.errorNo;

   auto [surf1, surf2] = closedRev;

   // Step 2: Check if they are aligned along their width
   if (std::abs(surf1.width - surf2.width) > tolerance) 
      return g_Status.SetError(IGESStatus::FileWriteFailed, "The two surfaces are not aligned along their width");

   // Step 3: Sew the two surfaces together
   TopoDS_Shape sewnShape = sewFaces(surf1.face, surf2.face, tolerance);

   // Step 4: Unify the result (optional, to remove redundant edges)
   ShapeUpgrade_UnifySameDomain unify(sewnShape, Standard_True, Standard_True, Standard_False);
   unify.Build();

   unionShape = unify.Shape();
   return g_Status.errorNo;
}

void IGESPriv::Redraw() {
   if (this->pShape->GetViewer().IsNull()) {
      throw std::runtime_error("Viewer is not set in the viewer implementation.");
   }

   Handle(V3d_View) view = this->pShape->GetActiveViews().First();
   if (view.IsNull()) {
      throw std::runtime_error("Active view is not initialized.");
   }

   // Refresh the viewer to update changes
   view->Redraw();
}

void IGESPriv::ZoomIn()
{
   assert(pShape);
   if (this->pShape->GetViewer().IsNull()) {
      throw std::runtime_error("Viewer is not set in the viewer implementation.");
   }

   Handle(V3d_View) view = this->pShape->GetActiveViews().First();
   if (view.IsNull()) {
      throw std::runtime_error("Active view is not initialized.");
   }

   // Get the view dimensions
   Standard_Integer width, height;
   view->Window()->Size(width, height);

   // Define zoom rectangle near the center
   Standard_Integer centerX = width / 2;
   Standard_Integer centerY = height / 2;
   Standard_Integer delta = 20; // Adjust for zoom intensity

   // Perform zoom in
   view->Zoom(centerX - delta, centerY - delta, centerX + delta, centerY + delta);

   // Refresh the viewer to update changes
   view->Redraw();
}

void IGESPriv::ZoomOut()
{
   assert(pShape);
   if (this->pShape->GetViewer().IsNull()) {
      throw std::runtime_error("Viewer is not set in the viewer implementation.");
   }

   Handle(V3d_View) view = this->pShape->GetActiveViews().First();
   if (view.IsNull()) {
      throw std::runtime_error("Active view is not initialized.");
   }

   // Get the view dimensions
   Standard_Integer width, height;
   view->Window()->Size(width, height);

   // Define zoom rectangle near the center
   Standard_Integer centerX = width / 2;
   Standard_Integer centerY = height / 2;
   Standard_Integer delta = 20; // Adjust for zoom intensity

   // Perform zoom out (larger rectangle)
   view->Zoom(centerX - delta * 2, centerY - delta * 2, centerX + delta * 2, centerY + delta * 2);

   // Refresh the viewer to update changes
   view->Redraw();
}

void _addShape(Handle(AIS_InteractiveContext) context, 
               Bnd_Box& combinedBoundingBox, TopoDS_Shape shape) {
   Handle(AIS_Shape) aisShape = new AIS_Shape(shape);
   context->Display(aisShape, Standard_False);
   context->SetDisplayMode(aisShape, AIS_Shaded, Standard_False);
   BRepBndLib::Add(shape, combinedBoundingBox);
}

int IGESPriv::GetShape(std::vector<unsigned char>& rData, int shapeType,
                        int width, const int height,
                        bool save /*= false*/)
{
   g_Status.errorNo = 0;

   std::vector<unsigned char> res;

   // Initialize viewer
   Handle(Aspect_DisplayConnection) displayConnection = new Aspect_DisplayConnection();
   Handle(OpenGl_GraphicDriver) graphicDriver = new OpenGl_GraphicDriver(displayConnection);
   auto v3dViewer = (Handle(V3d_Viewer)(new V3d_Viewer(graphicDriver)));
   this->pShape->SetViewer(v3dViewer);

   this->pShape->GetViewer()->SetDefaultLights();
   this->pShape->GetViewer()->SetLightOn();
   Handle(AIS_InteractiveContext) context = new AIS_InteractiveContext(this->pShape->GetViewer());

   // Prepare off-screen view
   Handle(V3d_View) view = this->pShape->GetViewer()->CreateView();
   Handle(Aspect_NeutralWindow) wnd = new Aspect_NeutralWindow();
   wnd->SetSize(width, height);
   wnd->SetVirtual(true);
   view->SetWindow(wnd);
   view->SetBackgroundColor(Quantity_Color(Quantity_NOC_WHITE));
   view->MustBeResized();

   // Prepare bounding box for fitting
   Bnd_Box combinedBoundingBox;

   TopoDS_Shape fusedShape;
   if (shapeType == (int)IGESShape::ShapeType::Fused) {
      TopoDS_Shape fusedShape;
      if (0 != this->getShape(fusedShape, (int)IGESShape::ShapeType::Fused))
         return g_Status.errorNo;

      _addShape(context, combinedBoundingBox, fusedShape);
   }
   else {
      std::vector<unsigned char> res;
      TopoDS_Shape leftShape;
      if(0 != this->getShape(leftShape, (int)IGESShape::ShapeType::Left)
         && 0 != this->getShape(fusedShape, (int)IGESShape::ShapeType::Fused))
         return g_Status.errorNo;

      _addShape(context, combinedBoundingBox, leftShape);
      _addShape(context, combinedBoundingBox, fusedShape);
   }

   // Check if the bounding box is valid
   if (combinedBoundingBox.IsVoid())
      return g_Status.SetError(IGESStatus::CalculationError,
                               "Bounding box of the shapes is void. Shapes might be empty.");

   // Fit view and redraw
   view->FitAll(0.01, Standard_True);
   view->Redraw();

   // Prepare pixmap image
   Image_AlienPixMap img;
   if (!view->ToPixMap(img, width, height))
      return g_Status.SetError(IGESStatus::FileWriteFailed,
         "Failed to render the view to pixmap");

   TCollection_AsciiString filename = "C:\\temp\\~shape.png";
   bool pixelBuffer = true;
   if (pixelBuffer || save) 
      img.Save(filename);

   if (pixelBuffer) {
      const unsigned char* imgData = img.Data();
      size_t imgSize = img.SizeRowBytes() * img.Height();
      rData = std::vector<unsigned char>(imgData, imgData + imgSize);
   }
   else {
      // Read the file content into memory
      std::ifstream file(filename.ToCString(), std::ios::binary);
      if (!file)
         throw std::runtime_error("Failed to open saved PNG file.");

      rData = std::vector<unsigned char>((std::istreambuf_iterator<char>(file)),
         std::istreambuf_iterator<char>());

      // Delete the temporary If file is not to required
      if (false == save)
         std::remove(filename.ToCString());
   }

   return g_Status.errorNo;
}