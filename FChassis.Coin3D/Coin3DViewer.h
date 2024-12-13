#pragma once
#include <Inventor/Win/SoWin.h>
#include <Inventor/Win/SoWinRenderArea.h>
#include <Inventor/Win/viewers/SoWinExaminerViewer.h>
#include <Inventor/nodes/SoSeparator.h>
#include <Inventor/nodes/SoPerspectiveCamera.h>
#include <Inventor/nodes/SoOrthographicCamera.h>
#include <Inventor/nodes/SoDirectionalLight.h>
#include <Inventor/nodes/SoMaterial.h>
#include <Inventor/nodes/SoCoordinate3.h>
#include <Inventor/nodes/SoLineSet.h>
#include <Inventor/nodes/SoQuadMesh.h>

#include <Inventor/nodes/SoSphere.h>
#include <Inventor/nodes/SoCone.h>
#include <Inventor/nodes/SoTranslation.h>

using namespace System;
using namespace FChassis::Core::Drawing;
using namespace Flux::API;

class GCodeDrawing { 
public:
	typedef void (*FNCreateShape)(SoSeparator* pSep, int count);
	void createShape (SoSeparator* root, SoSeparator*& container,
					  SoMaterial*& rpMaterial, SoCoordinate3*& rpCoord3,
					  FNCreateShape pCreateShapeFN, int count = 0)	{ 
		//assert(pCreateShape);
		SoSeparator* pSep = container;
		if (nullptr == pSep) {
			pSep = new SoSeparator();

			rpMaterial = new SoMaterial;
			pSep->addChild(rpMaterial);

			rpCoord3 = new SoCoordinate3();
			pSep->addChild(rpCoord3);

			pCreateShapeFN(pSep, count);
			 
			root->addChild(pSep);
			container = pSep;
		}
		else {
			rpMaterial = (SoMaterial*)pSep->getChild(0);
			rpCoord3 = (SoCoordinate3*)pSep->getChild(1);
		}
	}

	void UpdateSepDraw(SoSeparator* root, SoSeparator** ppArray, int index, Color32 color,
				       Point3List^ pts, Point3ListList^ ptsList = nullptr, 
					   Point3List^ ptQuadList = nullptr) {
		SoMaterial* pMaterial; SoCoordinate3* pCoord3;
		if (nullptr != pts || nullptr != ptsList) {
			this->createShape(root, ppArray[index], pMaterial, pCoord3, _createLineSet);
			this->_setColor(pMaterial, color);

			const float hc[][3] = {
				{1.1f, 2.2f, 3.3f},
				{4.4f, 5.5f, 6.6f},
				{117.7f, 8.8f, 119.9f} };
			pCoord3->point.setValues(0, 3, hc);
		}

		/*if (nullptr != pts)
			this->_updateCoord3Values(pCoord3, pts);

		if (nullptr != ptsList)
			for each (auto pts in ptsList)
				this->_updateCoord3Values(pCoord3, pts); */

		if (nullptr != ptQuadList && ptQuadList->Count > 0) {
			this->createShape(root, ppArray[index], pMaterial, pCoord3, _createQuadMesh);
			this->_setColor(pMaterial, color);
			this->_updateCoord3Values(pCoord3, ptQuadList, false);
		}
	}

	void UpdateGCodeLines(SoSeparator* root, int index, Color32 color,
					      Point3List^ pts, Point3ListList^ ptsList) {
		UpdateSepDraw(root, this->lineSegs, index, color, pts);
		UpdateSepDraw(root, this->lineSegs, index, color, nullptr, ptsList);
	}

	void UpdateToolWayPoints(SoSeparator* root, 
							 Color32 lineColor, Point3List^ linePtList, 
							 Color32 quadColor, Point3List^ quadPtList) {
		UpdateSepDraw(root, this->wayPoints, 0, lineColor, linePtList);
		UpdateSepDraw(root, this->wayPoints, 1, quadColor, nullptr, nullptr, quadPtList);
	}

	void UpdateSegs(SoSeparator* root, 
				    Color32 lineColor, Point3List^ linePtList,
					Color32 quadColor, Point3List^ quadPtList) {
		UpdateSepDraw(root, this->segs, 0, lineColor, linePtList);
		UpdateSepDraw(root, this->segs, 1, quadColor, nullptr, nullptr, quadPtList); }

	static void _createLineSet(SoSeparator* pSep, int count) {
		SoLineSet* pLineSet = new SoLineSet();
		pSep->addChild(pLineSet); }

	static void _createQuadMesh(SoSeparator* pSep, int count) {
		SoQuadMesh* mesh = new SoQuadMesh();

		mesh->verticesPerRow = count;
		mesh->verticesPerColumn = 5;

		pSep->addChild(mesh); }

	void _updateCoord3Values(SoCoordinate3* pCoord3, Point3List^ ptList, bool forLine = true) {
		if (ptList->Count <= 0)
			return;

		int p = 0;
		int count = ptList->Count + forLine ? 1 : 0;
		float* xyzs = new float[count * 3];
		float* xyz = xyzs;
		for each(auto pt in ptList) {
			//_updateCoord3Value(xyz, pt);
			//xyz[p++] = (float)pt.X;
			//xyz[p++] = (float)pt.Y;
			//xyz[p++] = (float)pt.Z;
		}

		/*if (forLine)
			_updateCoord3Value (xyz, ptList[0]);*/

		pCoord3->point.setValues (0, count, reinterpret_cast<const float(*)[3]>(xyzs));
		delete[] xyzs;
	}

	void _updateCoord3Value (float*& xyz, Point3 pt) {
		*xyz++ = (float)pt.X;
		*xyz++ = (float)pt.Y;
		*xyz++ = (float)pt.Z;
	}

	void _setColor(SoMaterial* pMaterial, Color32 color) { 
		pMaterial->ambientColor.setValue(color.R / 255.0f,
			color.G / 255.0f,
			color.B / 255.0f); }

public: 
	SoSeparator* lineSegs[4] = {nullptr, nullptr, nullptr, nullptr};
	SoSeparator* wayPoints[2] = { nullptr, nullptr };
	SoSeparator* segs[2] = { nullptr, nullptr };
};

namespace Coin3D { namespace Inventor {
public ref class Viewer
{
public: 
	IntPtr Create(IntPtr _hwnd) {
		HWND hWnd = reinterpret_cast<HWND>(_hwnd.ToPointer());
		SoWin::init(hWnd);

		// Create a scene containing a sphere
		this->root = new SoSeparator;
		this->root->ref(); // increment the root's reference counter

		SoPerspectiveCamera* camera = new SoPerspectiveCamera;
		SoOrthographicCamera* ocamera = new SoOrthographicCamera;
		SoDirectionalLight* light = new SoDirectionalLight;

		this->root->addChild(ocamera);	// add camera node to the scene graph
		this->root->addChild(light);    // add directional light to the scene

		/*
		SoMaterial* material = new SoMaterial;
		this->root->addChild(material); // add material (with default settings)

		SoSphere* sphere = new SoSphere;
		this->root->addChild(sphere);	// add sphere node to the scene graph

		SoTranslation* translation = new SoTranslation;
		translation->translation.setValue(2.0f, 0.0f, 0.0f);
		this->root->addChild(translation);

		SoMaterial* material1 = new SoMaterial;
		material1->ambientColor.setValue(0, 1, 0);
		this->root->addChild(material1);

		SoCone* cone = new SoCone;
		this->root->addChild(cone); // add cone node to the scene graph
		*/

		// Create a renderingarea which will be used to display the
		// scene graph in the window.
		this->renderarea = new SoWinExaminerViewer(hWnd);

		// Make the camera able to see the whole scene
		camera->viewAll(root, this->renderarea->getViewportRegion());

		// Display the scene in our renderarea and change the title
		this->renderarea->setSceneGraph(root);
		this->renderarea->setTitle("Sphere");
		this->renderarea->show();

		HWND hViewer = this->renderarea->getWidget();
		IntPtr handle = IntPtr(reinterpret_cast<void*>(hViewer));
		return handle;
	}

	void UpdateGCodeLines(int index, Color32 color, Point3List^ pts, Point3ListList^ ptsList) {
		this->gcodeDrawing->UpdateGCodeLines(this->root, index, color, pts, ptsList); }

	void UpdateToolWayPoints(Color32 lineColor, Point3List^ linePtList, 
							 Color32 quadColor, Point3List^ quadPtList) {
		this->gcodeDrawing->UpdateToolWayPoints(this->root, 
												lineColor, linePtList, 
												quadColor, quadPtList); }

	void UpdateSegs(Color32 lineColor, Point3List^ linePtList, 
					Color32 quadColor, Point3List^ quadPtList) {
		this->gcodeDrawing->UpdateSegs(this->root, 
									   lineColor, linePtList,
									   quadColor, quadPtList); }

protected:
	SoWinExaminerViewer* renderarea = NULL;
	GCodeDrawing* gcodeDrawing = new GCodeDrawing;
	SoSeparator* root = nullptr;
};

} } // Namespace
