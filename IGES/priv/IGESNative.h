#pragma once
#include <string>
#include <vector>
#include <exception>

// Declare CleanupOCCT() as an external function
extern "C" void CleanupOCCT();

// Forward declarations
class TopoDS_Shape;
class TCollection_AsciiString;
class IGESShapePimpl;
class gp_Pnt;
class gp_Dir;

// Specialized Exceptions
class NoPartLoadedException : public std::exception {
   private:
   int _pNo;
   std::string _message;

   public:
   NoPartLoadedException(int pno) noexcept : _pNo(pno+1) {
      if (_pNo ==1 || _pNo == 2 )
         _message = "No Part " + std::to_string(_pNo) + " is loaded";
      else if ( _pNo == 3 )
         _message = "No Parts loaded";
   }

   const char* what() const noexcept override {
      return _message.c_str();
   }
};

class InputIGESFileCorruptException : public std::exception {
   private:
   std::string _filename;
   std::string _message;

   public:
   InputIGESFileCorruptException(std::string filename) noexcept : _filename(filename) {
      _message = "File " + _filename  + " is corrupt";
   }

   const char* what() const noexcept override {
      return _message.c_str();
   }
};

class FuseFailureException : public std::exception {
   private:
   std::string _message;

   public:
   FuseFailureException() noexcept {
      _message = "Joining of the parts failed";
   }

   FuseFailureException(std::string str) noexcept :_message(str) {
      _message = _message + " Joining of the parts failed";
   }

   const char* what() const noexcept override {
      return _message.c_str();
   }
};

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
   void ClearError() {
      errorNo = NoError;
      error = "";
   }
};

static IGESStatus g_Status;

class IGESNative {
   public:
   public:
   enum EAxis
   {
      X, Y, Z, None
   };
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
   int YawBy180(int shapeType);
   int RollBy180(int shapeType);
   void RotatePartByAxis(TopoDS_Shape& shape, double deg, EAxis axis);
   int UndoJoin();
   void PerformZoomAndRender(bool zoomIn);

   void ZoomIn();
   void ZoomOut();
   void Redraw();

   int GetShape(std::vector<unsigned char>& data, int shapeType,
      int width, int height, bool save = false);

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