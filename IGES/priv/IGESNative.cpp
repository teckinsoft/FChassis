#include <assert.h>
#include <memory>
#include <vector>
#include <algorithm>
#include <map>

#include "./../OcctHeaders.h"

#include "IGESNative.h"
extern "C" void CleanupTCL() {
   try {
      // Properly unload Tcl/Tk resources before exiting
      Tcl_Finalize();
   }
   catch (const std::exception) {}
   catch (...) {}
}

// Private implementation class ( forward declared in header )
class IGESShapePimpl {
   public:
   // Use constexpr instead of #define
   static constexpr std::size_t ShapeCount = 3;

   // Strongly typed enum class for ShapeType
   enum class ShapeType : std::size_t {
      Left = 0,
      Right = 1,
      Fused = 2,
      Count = ShapeCount
   };

   private:
   TopoDS_Shape shapes[ShapeCount];

   Handle(V3d_Viewer) viewer; // Open CASCADE viewer
   Handle(AIS_InteractiveContext) context; // AIS Context14
   BRepAlgoAPI_Fuse fuser;


   public:
   IGESShapePimpl() = default;
   ~IGESShapePimpl() {
      try {
         // Ensure OCCT handles are released before exiting
         context.Nullify();
         viewer.Nullify();

         // Clear fuser object
         fuser = BRepAlgoAPI_Fuse(); // Reset to default

         // Ensure all shapes are cleared
         for (int i = 0; i < ShapeCount; i++)
            shapes[i].Nullify();
      }
      catch (const std::exception& ex) {
         std::cerr << "Exception in IGESShapePimpl destructor: " << ex.what() << std::endl;
      }
      catch (...) {
         std::cerr << "Unknown exception in IGESShapePimpl destructor!" << std::endl;
      }
   }
   explicit IGESShapePimpl(const Handle(V3d_Viewer)& aViewer) : viewer(aViewer) {}

   void SetViewer(const Handle(V3d_Viewer)& viewer) {
      this->viewer = viewer;
   }

   void SetContext(const Handle(AIS_InteractiveContext)& context) {
      this->context = context;
   }

   Handle(V3d_Viewer) GetViewer() const {
      return this->viewer;
   }

   Handle(AIS_InteractiveContext) GetContext() const {
      return context;
   }

   V3d_ListOfView GetActiveViews() {
      return this->viewer->ActiveViews();
   }

   void SetFuser(const BRepAlgoAPI_Fuse& fuser) {
      this->fuser = fuser;
   }

   BRepAlgoAPI_Fuse GetFuser() {
      return this->fuser;
   }

   void SetShape(ShapeType index, const TopoDS_Shape& shape) {
      assert(index >= ShapeType::Left && index <= ShapeType::Count);
      this->shapes[(int)index] = shape;
   }

   TopoDS_Shape GetShape(ShapeType index) {
      assert(index >= ShapeType::Left && index <= ShapeType::Count);
      return this->shapes[(int)index];
   }

   void ClearJoinedShape() {
      if (!this->shapes[(int)2].IsNull())
         this->shapes[(int)2].Nullify();
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
      gp_Trsf moveRightTrsf;
      moveRightTrsf.SetTranslation(gp_Vec(requiredTranslation, 0, 0));

      // Apply the translation to mShapeRight
      BRepBuilderAPI_Transform moveRightTransform(shape, moveRightTrsf, true);
      return moveRightTransform.Shape();
   }
};

// --------------------------------------------------------------------------------------------
IGESNative::IGESNative() {
   this->pShape = new IGESShapePimpl();
}

IGESNative::~IGESNative() {
   try {
      if (pShape) {
         pShape->SetViewer(Handle(V3d_Viewer)()); // Nullify viewer
         pShape->SetContext(Handle(AIS_InteractiveContext)()); // Nullify context
         delete pShape;
         pShape = nullptr;
      }
   }
   catch (const std::exception& ex) {
      std::cerr << "Exception in IGESNative destructor: " << ex.what() << std::endl;
   }
   catch (...) {
      std::cerr << "Unknown exception in IGESNative destructor!" << std::endl;
   }

   //Ensure Tcl/Tk cleanup before exiting
   CleanupTCL();
}

void IGESNative::Cleanup() {
   if (pShape) {
      delete pShape;
      pShape = nullptr;
   }
}

// File handling
int IGESNative::LoadIGES(const std::string& filePath, int pNo /*= 0*/) {
   g_Status.errorNo = IGESStatus::NoError;

   IGESControl_Reader reader;
   if (!reader.ReadFile(filePath.c_str()))
      throw InputIGESFileCorruptException(filePath);

   reader.TransferRoots();
   TopoDS_Shape shape = TopoDS_Shape(reader.OneShape());
   this->pShape->SetShape((IGESShapePimpl::ShapeType)pNo, shape);

   // Any lew loading of Part 1 or 2, fused part should be set to null
   shape.Nullify();
   this->pShape->SetShape((IGESShapePimpl::ShapeType::Fused), shape);

   return g_Status.errorNo;
}

int IGESNative::SaveIGES(const std::string& filePath, int shapeType /*= 0*/)
{
   g_Status.errorNo = IGESStatus::NoError;

   TopoDS_Shape shape = this->pShape->GetShape((IGESShapePimpl::ShapeType)shapeType);
   assert(!shape.IsNull());

   IGESControl_Writer writer;
   writer.AddShape(shape);
   if (!writer.Write(filePath.c_str()))
      g_Status.SetError(IGESStatus::FileWriteFailed, "IGES File Write failed");

   return g_Status.errorNo;
}

int IGESNative::SaveAsIGS(const std::string& filePath) {
   g_Status.errorNo = IGESStatus::NoError;

   // Check if mFusedShape is initialized
   TopoDS_Shape fusedShape = this->pShape->GetShape(IGESShapePimpl::ShapeType::Fused);
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

int IGESNative::getShape(TopoDS_Shape& shape, int shapeType) {
   g_Status.errorNo = IGESStatus::NoError;

   assert(shapeType >= (int)IGESShapePimpl::ShapeType::Left && shapeType <= (int)IGESShapePimpl::ShapeType::Fused);
   shape = this->pShape->GetShape((IGESShapePimpl::ShapeType)shapeType);;

   return g_Status.errorNo;
}

int IGESNative::UndoJoin() {
   pShape->ClearJoinedShape();
   return IGESStatus::NoError;
}

// Geometry Process
int IGESNative::AlignToXYPlane(int pNo /*= 0*/) {
   TopoDS_Shape shape;
   this->getShape(shape, pNo);
   if (shape.IsNull()) {
      g_Status.SetError(IGESStatus::ShapeError, "No shape to align");
      return g_Status.errorNo;
   }

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

   this->pShape->SetShape((IGESShapePimpl::ShapeType)pNo, shape);

   // Translate the second part
   TopoDS_Shape part1Shape, part2Shape;
   this->getShape(part1Shape, (int)IGESShapePimpl::ShapeType::Left);
   this->getShape(part2Shape, (int)IGESShapePimpl::ShapeType::Right);
   if (!part1Shape.IsNull() && !part2Shape.IsNull()) {
      // Compute the bounding box of the shape
      std::tie(xmin, ymin, zmin, xmax, ymax, zmax) = this->pShape->GetBBoxComp(part1Shape);

      // Calculate the translation value
      double translationX = (xmax - xmin) - 0.5;

      // Create a translation transformation
      gp_Trsf translation;
      translation.SetTranslation(gp_Vec(translationX, 0, 0)); // Translate along the X-axis

      // Apply the transformation to the shape
      BRepBuilderAPI_Transform shapeTransformer(part2Shape, translation, Standard_True); // Standard_True for copying the shape
      part2Shape = shapeTransformer.Shape(); // Update the shape with the transformed shape
      this->pShape->SetShape((IGESShapePimpl::ShapeType::Right), part2Shape);
   }
   return g_Status.errorNo;
}

int IGESNative::RotatePartBy180AboutZAxis(int shapeType) {
   TopoDS_Shape shape;
   this->getShape(shape, shapeType);
   if (shape.IsNull()) {
      throw NoPartLoadedException(shapeType);
   }

   // Compute the bounding box of the shape
   auto [xmin, ymin, zmin, xmax, ymax, zmax] = this->pShape->GetBBoxComp(shape);

   // Calculate the midpoint of the bounding box in X and Y
   double xMid = (xmax + xmin) / 2.0;
   double yMid = (ymax + ymin) / 2.0;

   auto pt = gp_Pnt(xMid, yMid, 0);
   auto parallelaxis = gp_Dir(0, 0, 1);

   screwRotationAboutMidPart(shape, pt, parallelaxis, 180);
   this->pShape->SetShape((IGESShapePimpl::ShapeType)shapeType, shape);
   return 0;
}

int IGESNative::UnionShapes() {
   g_Status.errorNo = IGESStatus::NoError;

   TopoDS_Shape leftShape;
   this->getShape(leftShape, (int)IGESShapePimpl::ShapeType::Left);

   TopoDS_Shape rightShape;
   this->getShape(rightShape, (int)IGESShapePimpl::ShapeType::Right);
   if (leftShape.IsNull() && rightShape.IsNull())
      throw NoPartLoadedException(2);
   if (leftShape.IsNull())
      throw NoPartLoadedException(0);
   if (rightShape.IsNull())
      throw NoPartLoadedException(1);

   // Perform the initial union operation
   BRepAlgoAPI_Fuse fuser(leftShape, rightShape);
   fuser.Build();

   // Validate the fuse operation
   if (!fuser.IsDone())
      return g_Status.SetError(IGESStatus::FuseError, "Initial Boolean union operation failed");

   // Retrieve the initial fused shape
   TopoDS_Shape fusedShape = fuser.Shape();
   if (fusedShape.IsNull())
      return g_Status.SetError(IGESStatus::FuseError, "Fused shape is null after the initial union operation");

   // Call the function to handle intersecting bounding curves
   double tolerance = 1e-1; // Adjust the tolerance as needed
   this->handleIntersectingBoundingCurves(fusedShape, tolerance);

   // Check for multiple connected components
   TopTools_IndexedMapOfShape solids;
   TopExp::MapShapes(fusedShape, TopAbs_SOLID, solids);

   // If there's more than one solid, merge them
   if (solids.Extent() > 1) {

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
   this->pShape->SetShape(IGESShapePimpl::ShapeType::Fused, fusedShape);

   // Optional: Validate the final fused shape
   BRepCheck_Analyzer analyzer(fusedShape);
   if (!analyzer.IsValid())
      return g_Status.SetError(IGESStatus::FuseError, "Final fused shape is invalid");

   if (hasMultipleConnectedComponents(fusedShape))
      return g_Status.SetError(IGESStatus::FuseError, "Fused shape contains multiple connected components");

   return g_Status.errorNo;
}

int IGESNative::screwRotationAboutMidPart(TopoDS_Shape& shape, const gp_Pnt& pt,
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
void IGESNative::PerformZoomAndRender(bool zoomIn) {
   if (zoomIn) ZoomIn();
   else ZoomOut();
}

int IGESNative::mirror(TopoDS_Shape leftShape) {
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
   this->pShape->SetShape(IGESShapePimpl::ShapeType::Fused, mirroredShape);

   return g_Status.errorNo;
}

bool IGESNative::hasMultipleConnectedComponents(const TopoDS_Shape& shape) {
   int solidCount = 0;

   // Traverse the shape to count solids
   for (TopExp_Explorer explorer(shape, TopAbs_SOLID); explorer.More(); explorer.Next()) {
      solidCount++;
      if (solidCount > 1)
         return true;  // More than one solid found
   }

   return false;  // Only one or no solid found
}

bool IGESNative::isPointOnAnySurface(const TopoDS_Shape& shape, const gp_Pnt& point, double tolerance) {
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

bool IGESNative::doesVectorIntersectShape(const TopoDS_Shape& shape, const gp_Pnt& point,
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

void IGESNative::handleIntersectingBoundingCurves(TopoDS_Shape& fusedShape, double tolerance) {
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
static bool isSurfaceOfRevolution(const TopoDS_Face& face) {
   Handle(Geom_Surface) surface = BRep_Tool::Surface(face);
   if (surface.IsNull())
      return false;

   // Check if the surface is a surface of revolution
   Handle(Geom_SurfaceOfRevolution) revSurface = Handle(Geom_SurfaceOfRevolution)::DownCast(surface);
   return !revSurface.IsNull();
}

// Function to compute the bounding box dimensions of a face
static SurfaceInfo computeBoundingBox(const TopoDS_Face& face) {
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
static int computeShortestDistance(const TopoDS_Face& face1, const TopoDS_Face& face2, double& rShortestDistance) {
   BRepExtrema_DistShapeShape distCalc(face1, face2);
   distCalc.Perform();

   if (!distCalc.IsDone())
      return g_Status.SetError(IGESStatus::CalculationError, "Distance calculation failed");

   rShortestDistance = distCalc.Value(); // Returns the shortest distance
   return g_Status.errorNo;
}

// Function to find two surface of revolutions with the longest lengths
static int findClosestRevolutions(const TopoDS_Shape& shape, std::pair<SurfaceInfo, SurfaceInfo> rClosedRev, double tolerance = 1e-6) {
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
static TopoDS_Shape sewFaces(const TopoDS_Face& face1, const TopoDS_Face& face2, double tolerance) {
   BRepBuilderAPI_Sewing sewing(tolerance);

   // Add the faces to the sewing operation
   sewing.Add(face1);
   sewing.Add(face2);

   // Perform sewing
   sewing.Perform();

   return sewing.SewedShape();
}

// Main function to process and stitch two revolutions
static int processRevolutions(TopoDS_Shape& unionShape, const TopoDS_Shape& shape, double tolerance = 1e-6) {
   // Step 1: Find the two longest surface of revolutions
   std::pair<SurfaceInfo, SurfaceInfo> closedRev;
   if (0 != findClosestRevolutions(shape, closedRev))
      return g_Status.errorNo;

   auto& [surf1, surf2] = closedRev;

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

void IGESNative::Redraw() {
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

void IGESNative::ZoomIn()
{
   assert(pShape);
   if (this->pShape->GetViewer().IsNull()) {
      throw std::runtime_error("Viewer is not set in the viewer implementation.");
   }

   Handle(V3d_View) view = this->pShape->GetActiveViews().First();
   if (view.IsNull())
      throw std::runtime_error("Active view is not initialized.");

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

void IGESNative::ZoomOut()
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

static void _addShape(Handle(AIS_InteractiveContext) context,
   Bnd_Box& combinedBoundingBox, TopoDS_Shape shape) {
   Handle(AIS_Shape) aisShape = new AIS_Shape(shape);
   context->Display(aisShape, Standard_False);
   context->SetDisplayMode(aisShape, AIS_Shaded, Standard_False);
   BRepBndLib::Add(shape, combinedBoundingBox);
}

int IGESNative::GetShape(std::vector<unsigned char>& rData, int pNo,
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
   if (pNo == (int)IGESShapePimpl::ShapeType::Fused) {
      TopoDS_Shape fusedShape;
      this->getShape(fusedShape, (int)IGESShapePimpl::ShapeType::Fused);
      if (fusedShape.IsNull()) {
         g_Status.SetError(IGESStatus::FuseError, "No fused shapes. Fuse left and right shapes first");
         return g_Status.errorNo;
      }
      _addShape(context, combinedBoundingBox, fusedShape);
   }
   else {
      std::vector<unsigned char> res;
      TopoDS_Shape leftShape, rightShape;
      if (pNo == 2) {
         this->getShape(fusedShape, (int)IGESShapePimpl::ShapeType::Fused);
         if (fusedShape.IsNull())
            _addShape(context, combinedBoundingBox, fusedShape);
         else {
            g_Status.SetError(IGESStatus::FuseError, "fused shapes.Fuse left and right shapes first");
            return g_Status.errorNo;
         }
      }
      else {
         this->getShape(leftShape, (int)IGESShapePimpl::ShapeType::Left);
         if (!leftShape.IsNull())
            _addShape(context, combinedBoundingBox, leftShape);
         this->getShape(rightShape, (int)IGESShapePimpl::ShapeType::Right);
         if (!rightShape.IsNull())
            _addShape(context, combinedBoundingBox, rightShape);
      }
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