#pragma once

class IGESNative;
namespace IGES {
   public ref class IGES {
      public:
      IGES();
      ~IGES();
      !IGES();

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
      int UndoJoin();

      void GetErrorMessage([System::Runtime::InteropServices::Out] System::String^% message);

      private:
      IGESNative* pPriv = nullptr;
   };
} // namespace IGES
