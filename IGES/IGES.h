#pragma once

class IGESPriv;
namespace IGES {
public ref class IGES {
  public:
   IGES();
   ~IGES();

   void Initialize();
   void Uninitialize();

   void ZoomIn();
   void ZoomOut();

   int LoadIGES(System::String^ filePath, int shapeType);
   int SaveIGES(System::String^ filePath, int shapeType);

   int AlignToXYPlane(int shapeType);

   int GetShape(int shapeType, int width, int height, array<unsigned char>^% rData);
   int RotatePartBy180AboutZAxis(int order);
   void Redraw();
   int SaveAsIGS(System::String^ filePath);
   int UnionShapes();

   void GetErrorMessage([System::Runtime::InteropServices::Out] System::String^% message);

private:
   IGESPriv* pPriv = nullptr;
};
} // namespace IGES
