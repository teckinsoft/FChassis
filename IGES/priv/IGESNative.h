#pragma once
#include <string>
#include <vector>

// Declare CleanupTCL() as an external function
extern "C" void CleanupTCL(); 

// Forward declarations
class TopoDS_Shape;
class TCollection_AsciiString;
class IGESShapePimpl;
class gp_Pnt;
class gp_Dir;

class IGESStatus {
   public:
   enum Error {
      NoError = 0,
      FileReadFailed = 1,
      FileWriteFailed = 2,

      FuseError = 3,
      ShapeError = 4,
      CalculationError = 5,
   };

   /// -------------------------------------------
   int SetError(int errorNo, const char* error) {
      this->error = error;
      this->errorNo = errorNo;
      return errorNo;
   }

   /// -------------------------------------------
   int errorNo = 0;
   std::string error;
};

static IGESStatus g_Status;

class IGESNative {
   public:
   IGESNative();
   ~IGESNative();
   void Cleanup();

   // File handling
   int LoadIGES(const std::string& filePath, int shapeType = 0);
   int SaveIGES(const std::string& filePath, int shapeType = 0);
   int SaveAsIGS(const std::string& filePath);

   // Commands
   int UnionShapes();
   int AlignToXYPlane(int shapeType = 0);
   int RotatePartBy180AboutZAxis(int shapeType);
   int UndoJoin();
   void PerformZoomAndRender(bool zoomIn);

   void ZoomIn();
   void ZoomOut();
   void Redraw();

   int GetShape(std::vector<unsigned char>& data, int shapeType,
      const int width, const int height, bool save = false);

   private:
   int getShape(TopoDS_Shape& shape, int shapeType);

   // Geometry Process
   int screwRotationAboutMidPart(TopoDS_Shape& shape, const gp_Pnt& pt, const gp_Dir& axis, double angleDegrees);
   int mirror(TopoDS_Shape leftShape);
   void handleIntersectingBoundingCurves(TopoDS_Shape& fusedShape, double tolerance);

   // Predicates
   bool hasMultipleConnectedComponents(const TopoDS_Shape& shape);
   bool isPointOnAnySurface(const TopoDS_Shape& shape, const gp_Pnt& point, double tolerance);
   bool doesVectorIntersectShape(const TopoDS_Shape& shape, const gp_Pnt& point, const gp_Dir& direction, gp_Pnt& intersectionPoint);


   private:
   IGESShapePimpl* pShape = nullptr;
};