#include <assert.h>
#include <memory>

#include <msclr/marshal_cppstd.h>

#include "priv/IGES.priv.h"
#include "IGES.h"

using namespace System;

namespace IGES {
IGES::IGES()
   : pPriv(nullptr) // Initialize the shape pointer
{}

IGES::~IGES() {
   // Clean up the unmanaged TopoDS_Shape instance
   if (this->pPriv != nullptr) {
      delete this->pPriv;
      this->pPriv = nullptr;
   }
}

void IGES::GetErrorMessage([System::Runtime::InteropServices::Out] System::String^% message) {
   message = gcnew String(g_Status.error.data()); }

void IGES::ZoomIn() {
   assert(this->pPriv);
   this->pPriv->PerformZoomAndRender(true); }

void IGES::ZoomOut() {
   assert(this->pPriv);
   this->pPriv->PerformZoomAndRender(false); }

void IGES::Initialize() {
   assert(this->pPriv == nullptr);
   if (this->pPriv == nullptr)
      this->pPriv = new IGESPriv();
}

void IGES::Uninitialize() {
   assert(this->pPriv != nullptr);
   if (this->pPriv != nullptr) {
      delete this->pPriv;
      this->pPriv = nullptr;
   }
}

int IGES::LoadIGES(System::String^ filePath, int order) {
   assert(this->pPriv);

   std::string stdFilePath = msclr::interop::marshal_as<std::string>(filePath);
   return this->pPriv->LoadIGES(stdFilePath, order);
}

int IGES::SaveIGES(System::String^ filePath, int order) {
   assert(this->pPriv);

   std::string stdFilePath = msclr::interop::marshal_as<std::string>(filePath);
   return this->pPriv->SaveIGES(stdFilePath, order);
}

int IGES::SaveAsIGS(System::String^ filePath) {
   assert(this->pPriv);

   std::string stdFilePath = msclr::interop::marshal_as<std::string>(filePath);
   return pPriv->SaveAsIGS(stdFilePath);
}

int IGES::UnionShapes() {
   assert(this->pPriv);
   return pPriv->UnionShapes();
}

int IGES::AlignToXYPlane(int order) {
   assert(this->pPriv);
   return this->pPriv->AlignToXYPlane(order); }

void IGES::Redraw() {
   assert(this->pPriv);
   this->pPriv->Redraw();  }

int IGES::RotatePartBy180AboutZAxis(int order) {
   assert(this->pPriv);
   int errorNo = this->pPriv->RotatePartBy180AboutZAxis(order);
   if (0 == errorNo)
      this->Redraw();

   return errorNo;
}

int IGES::GetShape(int shapeType, int width, int height, array<unsigned char>^% rData) {
   assert(this->pPriv);

   // Call the native RenderToPNG method
   std::vector<unsigned char> pngData;
   int errorNo = this->pPriv->GetShape(pngData, shapeType, width, height);
   if (0 != errorNo)
      return errorNo;

   // Create a managed byte array and populate it
   rData = gcnew array<unsigned char>(static_cast<int>(pngData.size()));
   for (int i = 0; i < (int)pngData.size(); ++i)
      rData[i] = pngData[i];

   return errorNo;
}
} // namespace IGES
