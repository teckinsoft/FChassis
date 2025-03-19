#include <assert.h>
#include <memory>
#include <vector>
#include <algorithm>
#include <map>
#include <omp.h>

#include "./../OcctHeaders.h"

#include "IGESNative.h"
extern "C" void CleanupOCCT() {
   try {
      // Properly unload Tcl/Tk resources before exiting
      Tcl_Finalize();
   }
   catch (const std::exception) {}
   catch (...) {}
}

struct SurfaceInfo {
   TopoDS_Face face;
   double length; // Longest dimension (Xmax - Xmin)
   double width;  // Shortest dimension
};

class OCCTUtils {
   public:

   static TopoDS_Shape CopyShape(const TopoDS_Shape& originalShape) {
      BRepBuilderAPI_Copy copier(originalShape);
      return copier.Shape();
   }

   static TopoDS_Shape FixShape(const TopoDS_Shape& shape) {
      Handle(ShapeFix_Shape) fixer = new ShapeFix_Shape(shape);
      fixer->Perform();
      return fixer->Shape();
   }

   static bool IsShapeValid(const TopoDS_Shape& shape) {
      BRepCheck_Analyzer analyzer(shape);
      return analyzer.IsValid();
   }

   static void PrintErrors(const Handle(Message_Report)& report) {
      for (const Handle(Message_Alert)& alert : report->GetAlerts(Message_Gravity::Message_Fail))
      {
         if (!alert.IsNull())
         {
            Message_Msg msg = alert->GetMessageKey();
            std::cout << "Error: " << msg.Get() << std::endl;
         }
      }
   }

   // Function to translate a shape along the X-axis
   static TopoDS_Shape TranslateAlongX(const TopoDS_Shape& shape, double translation) {
      gp_Trsf transform;
      transform.SetTranslation(gp_Vec(translation, 0, 0));
      BRepBuilderAPI_Transform transformer(shape, transform, true);
      return transformer.Shape();
   }

   // Function to perform Boolean union using BOPAlgo_Builder
   static TopoDS_Shape BooleanUnion(const TopoDS_Shape& shape1, const TopoDS_Shape& shape2) {
      // Validate and repair the input shapes
      TopoDS_Shape fixedShape1 = IsShapeValid(shape1) ? shape1 : FixShape(shape1);
      TopoDS_Shape fixedShape2 = IsShapeValid(shape2) ? shape2 : FixShape(shape2);

      // Perform the Boolean union
      BOPAlgo_Builder bopBuilder;
      bopBuilder.AddArgument(fixedShape1);
      bopBuilder.AddArgument(fixedShape2);
      bopBuilder.SetRunParallel(Standard_True);
      bopBuilder.SetFuzzyValue(1e-5);
      bopBuilder.Perform();

      if (bopBuilder.HasErrors()) {
         // Print error messages
         std::cerr << "Boolean operation errors:" << std::endl;
         bopBuilder.DumpErrors(std::cerr);

         // Attempt to repair the shapes and retry the operation
         TopoDS_Shape repairedShape1 = FixShape(fixedShape1);
         TopoDS_Shape repairedShape2 = FixShape(fixedShape2);

         BRep_Builder builder;
         TopoDS_Compound compound;
         builder.MakeCompound(compound);
         builder.Add(compound, repairedShape1);
         builder.Add(compound, repairedShape2);

         BOPAlgo_Builder retryBuilder;
         retryBuilder.AddArgument(compound);
         retryBuilder.SetRunParallel(Standard_True);
         retryBuilder.SetFuzzyValue(1e-5);
         retryBuilder.Perform();

         if (retryBuilder.HasErrors()) {
            std::cerr << "Boolean union failed even after repair:" << std::endl;
            retryBuilder.DumpErrors(std::cerr);
            throw std::runtime_error("Boolean union failed even after repair.");
         }

         return retryBuilder.Shape();
      }
      return bopBuilder.Shape();
   }

   static double ShortestDistanceX(const TopoDS_Shape& shape1, const TopoDS_Shape& shape2) {
      BRepExtrema_DistShapeShape distAlgo(shape1, shape2);
      distAlgo.Perform();

      if (!distAlgo.IsDone() || distAlgo.NbSolution() == 0)
         throw std::runtime_error("Distance computation failed.");

      double minXDist = std::numeric_limits<double>::max();

      // Loop through all possible solutions and find the minimum distance along the X-axis
      for (int i = 1; i <= distAlgo.NbSolution(); ++i) {
         gp_Pnt p1 = distAlgo.PointOnShape1(i);
         gp_Pnt p2 = distAlgo.PointOnShape2(i);

         double dx = std::abs(p1.X() - p2.X()); // Compute X-axis distance

         if (dx < minXDist) {
            minXDist = dx;
         }
      }
      return minXDist; // Returns the shortest distance along X-axis
   }

   static double FindMinDistance(const std::vector<gp_Pnt>& midpoints1, const std::vector<gp_Pnt>& midpoints2) {
      double minDistance = std::numeric_limits<double>::max();

      // Flatten the nested loops into a single loop
      size_t totalIterations = midpoints1.size() * midpoints2.size();

#pragma omp parallel
      {
         // Each thread has its own local minimum
         double localMinDistance = std::numeric_limits<double>::max();

#pragma omp for
         for (int k = 0; k < totalIterations; ++k) {
            // Compute the indices for the original nested loops
            int i = k / (int)midpoints2.size();
            int j = k % midpoints2.size();

            // Compute the distance
            double distance = midpoints1[i].Distance(midpoints2[j]);
            if (distance < localMinDistance) {
               localMinDistance = distance;
            }
         }

         // Combine local results into the global minimum
#pragma omp critical
         {
            if (localMinDistance < minDistance) {
               minDistance = localMinDistance;
            }
         }
      }
      return minDistance;
   }

   // Function to compute the midpoint of an edge
   static gp_Pnt ComputeEdgeMidpoint(const TopoDS_Edge& edge) {
      Standard_Real first, last;
      Handle(Geom_Curve) curve = BRep_Tool::Curve(edge, first, last);

      if (!curve.IsNull()) {
         return curve->Value((first + last) / 2.0);  // Compute midpoint
      }
      else {
         std::cerr << "Warning: Null curve encountered!" << std::endl;
         return gp_Pnt(0, 0, 0); // Return a default point
      }
   }

   // Compute the minimum distance using edge midpoints
   static double EdgeMidpointDistance(const TopoDS_Shape& shape1, const TopoDS_Shape& shape2) {
      std::vector<gp_Pnt> midpoints1, midpoints2;

      // Extract edges and compute midpoints for shape1
      for (TopExp_Explorer edgeExp(shape1, TopAbs_EDGE); edgeExp.More(); edgeExp.Next()) {
         TopoDS_Edge edge = TopoDS::Edge(edgeExp.Current());
         midpoints1.push_back(ComputeEdgeMidpoint(edge));
      }

      // Extract edges and compute midpoints for shape2
      for (TopExp_Explorer edgeExp(shape2, TopAbs_EDGE); edgeExp.More(); edgeExp.Next()) {
         TopoDS_Edge edge = TopoDS::Edge(edgeExp.Current());
         midpoints2.push_back(ComputeEdgeMidpoint(edge));
      }


      auto d = FindMinDistance(midpoints1, midpoints2);
      return d;

   }

   // Function to merge two shapes along the X-axis
   static TopoDS_Shape MergeShapesAlongX(const TopoDS_Shape& shape1, const TopoDS_Shape& shape2) {
      // Check if the shapes are valid
      if (shape1.IsNull() || shape2.IsNull()) {
         throw std::runtime_error("One or both input shapes are null.");
      }

      // Try the Boolean union operation
      TopoDS_Shape fusedShape = BooleanUnion(shape1, shape2);

      if (fusedShape.IsNull())
         throw std::runtime_error("Fusing input parts failed even after translation.");

      // Fix the resulting shape
      fusedShape = FixShape(fusedShape);

      return fusedShape;
   }
};

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
   CleanupOCCT();
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
   shape = OCCTUtils::FixShape(shape);
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
      double translationX = (xmax - xmin) - 1.0;

      // Create a translation transformation
      gp_Trsf translation;
      translation.SetTranslation(gp_Vec(translationX, 0, 0)); // Translate along the X-axis

      // Apply the transformation to the shape
      BRepBuilderAPI_Transform shapeTransformer(part2Shape, translation, Standard_True); // Standard_True for copying the shape
      part2Shape = shapeTransformer.Shape(); // Update the shape with the transformed shape
      this->pShape->SetShape((IGESShapePimpl::ShapeType::Right), part2Shape);
   }
   return 0;
}

int IGESNative::RotatePartBy180AboutZAxis(int shapeType) {
   YawBy180(shapeType);
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

   auto d = OCCTUtils::EdgeMidpointDistance(leftShape, rightShape);
   double trans = 0;
   if (d < 0.5) trans = 2.5 * d;
   else if (d < 0.9) trans = 1 + d;
   TopoDS_Shape translatedRightShape = OCCTUtils::TranslateAlongX(rightShape, -2.0 * d); // Translate by -1.0 mm along X-axis

   // Perform the initial union operation
   BRepAlgoAPI_Fuse fuser(leftShape, translatedRightShape);
   fuser.Build();

   TopoDS_Shape fusedShape;

   // Retrieve the initial fused shape
   fusedShape = fuser.Shape();

   // Validate the fuse operation
   if (!fuser.IsDone() || fusedShape.IsNull())
   {
      // Try fusion once again
      fusedShape = OCCTUtils::MergeShapesAlongX(leftShape, translatedRightShape);
      if (fusedShape.IsNull())
         throw FuseFailureException("Fusing input parts failed");
      fusedShape = OCCTUtils::FixShape(fusedShape);
      if (fusedShape.IsNull())
         throw FuseFailureException("Fusing input parts failed");
   }
   else
      fusedShape = OCCTUtils::FixShape(fusedShape);

   pShape->SetShape(IGESShapePimpl::ShapeType::Fused, fusedShape);

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

void IGESNative::handleIntersectingBoundingCurves(TopoDS_Shape& shape, double tolerance) {
   auto cpShape = OCCTUtils::CopyShape(shape);

   // Step 1: Sew gaps between surfaces
   BRepBuilderAPI_Sewing sewing(tolerance);
   sewing.Add(cpShape);
   sewing.Perform();
   TopoDS_Shape sewedShape = sewing.SewedShape();

   // Step 2: Fuse surfaces to create a single solid
   BRepAlgoAPI_Fuse fuse(sewedShape, sewedShape); // Self-fuse
   fuse.Build();
   cpShape = fuse.Shape();

   // Step 3: Refine the shape to remove small edges
   ShapeUpgrade_UnifySameDomain unify(cpShape, Standard_True, Standard_True, Standard_False);
   unify.Build();
   cpShape = unify.Shape();
   if (!OCCTUtils::IsShapeValid(cpShape))
      return;

   // Step 1: Heal the shape to fix gaps and ensure continuity
   Handle(ShapeFix_Shape) shapeFix = new ShapeFix_Shape(cpShape);
   shapeFix->SetPrecision(1e-3); // Set tolerance for fixing gaps
   shapeFix->Perform(); // Perform the healing operation
   TopoDS_Shape healedShape = shapeFix->Shape();

   // Step 2: Refine the healed shape
   unify = ShapeUpgrade_UnifySameDomain(healedShape, Standard_True, Standard_True, Standard_False);
   unify.Build();

   shape = unify.Shape();
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
   int width, int height,
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

// Rotate part about Z axis passing through center - Yaw 180
int IGESNative::YawBy180(int shapeType) {
   TopoDS_Shape shape;
   this->getShape(shape, shapeType);
   if (shape.IsNull()) {
      throw NoPartLoadedException(shapeType);
   }
   RotatePartByAxis(shape, 180, EAxis::Z);
   this->pShape->SetShape((IGESShapePimpl::ShapeType)shapeType, shape);
   return 0;
}

int IGESNative::RollBy180(int shapeType) {
   TopoDS_Shape shape;
   this->getShape(shape, shapeType);
   if (shape.IsNull()) {
      throw NoPartLoadedException(shapeType);
   }
   RotatePartByAxis(shape, 180, EAxis::X);
   this->pShape->SetShape((IGESShapePimpl::ShapeType)shapeType, shape);
   return 0;
}

void IGESNative::RotatePartByAxis(TopoDS_Shape& shape, double deg, EAxis axis) {
   // Compute the bounding box of the shape
   auto [xmin, ymin, zmin, xmax, ymax, zmax] = this->pShape->GetBBoxComp(shape);

   // Calculate the midpoint of the bounding box in X and Y
   double xMid = (xmax + xmin) / 2.0;
   double yMid = (ymax + ymin) / 2.0;
   double zMid = (zmax + zmin) / 2.0;

   gp_Pnt pt;
   gp_Dir gpAxis;
   if (axis == EAxis::Z) {
      pt = gp_Pnt(xMid, yMid, 0);
      gpAxis = gp_Dir(0, 0, 1);
      screwRotationAboutMidPart(shape, pt, gpAxis, 180);
   }
   else if (axis == EAxis::X) {
      pt = gp_Pnt(xMid, yMid, zMid);
      gpAxis = gp_Dir(1, 0, 0);
      screwRotationAboutMidPart(shape, pt, gpAxis, 180);
   }
}