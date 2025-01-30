using System.Windows.Threading;
using Flux.API;
using FChassis.Core;

namespace FChassis.Tools;

/// <summary>
/// The class Nozzle contains the logic of the laser cutting tool nozzle.
/// </summary>
/// <param name="diameter">The diameter of the laser cutting tool nozzle</param>
/// <param name="height">The height of the laser cutting tool nozzle.</param>
/// <param name="segments">No. of segments prescribed to simulate</param>
public class Nozzle (double diameter, double height, int segments, Point3? pt = null, Vector3? normal = null) {
   #region Data Members
   static readonly double mCylOffset = 15;
   readonly Cylinder mCylinder = new (diameter, height, segments, mCylOffset);
   readonly FrustumCone mFCone = new (1.0, diameter, mCylOffset);
   LinearSparks mLineSparks = (pt != null || normal != null)
    ? new LinearSparks (pt.Value, normal.Value, 5.0, 100, 50, 2)
    : null;

   #endregion

   #region Draw methods
   public void Draw (XForm4 LHCompTransform, Color32 LHToolColor, XForm4 RHCompTransform, Color32 RHToolColor, 
      Color32 toolTipColor, Dispatcher dispatcher, Point3? tipTool0, Vector3? normalTool0,
      Point3? tipTool1, Vector3? normalTool1, EMove t0MoveType, EMove t1MoveType) {
      List<Point3> lhCompTrfPts = [],
         rhCompTrfPts = [], lhCompToolTipTrfPts = [], rhCompToolTipTrfPts = [],
      lhCompLSPts = [], rhCompLSPts = [];

      if (t0MoveType == EMove.Machining) {
         if (normalTool0 != null && tipTool0 != null) {
            mLineSparks = new (tipTool0.Value, normalTool0.Value * -1, rc: 5.0, lc: 200, nSparksPerSet: 50, nSets: 2);
            foreach (var sparks in mLineSparks.Sparks) {
               foreach (var line in sparks) {
                  var p1 = mLineSparks.Points[line.A]; var p2 = mLineSparks.Points[line.B];
                  lhCompLSPts.AddRange ([p1, p2]);
               }
            }
            if (lhCompLSPts.Count > 0 ) 
               lhCompLSPts.AddRange ([lhCompLSPts[^1], lhCompLSPts[^1]]);
         }
      }

      if (t1MoveType == EMove.Machining) {
         if (normalTool1 != null && tipTool1 != null) {
            mLineSparks = new (tipTool1.Value, normalTool1.Value * -1, rc: 5.0, lc: 100, nSparksPerSet: 50, nSets: 2);
            foreach (var sparks in mLineSparks.Sparks) {
               foreach (var line in sparks) {
                  var p1 = mLineSparks.Points[line.A]; var p2 = mLineSparks.Points[line.B];
                  lhCompLSPts.AddRange ([p1, p2]);
               }
            }
            if (rhCompLSPts.Count > 0 )
               rhCompLSPts.AddRange ([rhCompLSPts[^1], rhCompLSPts[^1]]);
         }
      }


      foreach (var trg in mCylinder.Triangles) {
         var p1 = mCylinder.Points[trg.A]; var p2 = mCylinder.Points[trg.B];
         var p3 = mCylinder.Points[trg.C];
         Point3 xFormP1, xFormP2, xFormP3;
         if (LHCompTransform != null) {
            xFormP1 = Geom.V2P (LHCompTransform * p1); xFormP2 = Geom.V2P (LHCompTransform * p2);
            xFormP3 = Geom.V2P (LHCompTransform * p3);
            lhCompTrfPts.AddRange ([xFormP1, xFormP2, xFormP3]);
         }
         if (RHCompTransform != null) {
            xFormP1 = Geom.V2P (RHCompTransform * p1); xFormP2 = Geom.V2P (RHCompTransform * p2);
            xFormP3 = Geom.V2P (RHCompTransform * p3);
            rhCompTrfPts.AddRange ([xFormP1, xFormP2, xFormP3]);
         }
      }
      foreach (var trg in mFCone.Triangles) {
         var p1 = mFCone.Points[trg.A]; var p2 = mFCone.Points[trg.B];
         var p3 = mFCone.Points[trg.C];
         Point3 xFormP1, xFormP2, xFormP3;
         if (LHCompTransform != null) {
            xFormP1 = Geom.V2P (LHCompTransform * p1); xFormP2 = Geom.V2P (LHCompTransform * p2);
            xFormP3 = Geom.V2P (LHCompTransform * p3);
            lhCompToolTipTrfPts.AddRange ([xFormP1, xFormP2, xFormP3]);
         }
         if (RHCompTransform != null) {
            xFormP1 = Geom.V2P (RHCompTransform * p1); xFormP2 = Geom.V2P (RHCompTransform * p2);
            xFormP3 = Geom.V2P (RHCompTransform * p3);
            rhCompToolTipTrfPts.AddRange ([xFormP1, xFormP2, xFormP3]);
         }
      }

      if (lhCompLSPts.Count > 0) {
         dispatcher.Invoke (() => {
            Lux.HLR = true;
            Lux.Color = Utils.SteelCutingSparkColor2;
            Lux.Draw (EDraw.LineStrip, lhCompLSPts);
         });
      }

      if (rhCompLSPts.Count > 0) {
         dispatcher.Invoke (() => {
            Lux.HLR = true;
            Lux.Color = Utils.SteelCutingSparkColor2;
            Lux.Draw (EDraw.Lines, rhCompLSPts);
         });
      }


      if (lhCompTrfPts.Count > 0) {
         dispatcher.Invoke (() => {
            Lux.HLR = true;
            Lux.Color = LHToolColor;
            Lux.Draw (EDraw.Triangle, lhCompTrfPts);
         });
      }
      if (rhCompTrfPts.Count > 0) {
         dispatcher.Invoke (() => {
            Lux.HLR = true;
            Lux.Color = RHToolColor;
            Lux.Draw (EDraw.Triangle, rhCompTrfPts);
         });
      }
      if (lhCompToolTipTrfPts.Count > 0) {
         dispatcher.Invoke (() => {
            Lux.HLR = true;
            Lux.Color = toolTipColor;
            Lux.Draw (EDraw.Triangle, lhCompToolTipTrfPts);
         });
      }
      if (rhCompToolTipTrfPts.Count > 0) {
         dispatcher.Invoke (() => {
            Lux.HLR = true;
            Lux.Color = toolTipColor;
            Lux.Draw (EDraw.Triangle, rhCompToolTipTrfPts);
         });
      }
   }
   #endregion
}