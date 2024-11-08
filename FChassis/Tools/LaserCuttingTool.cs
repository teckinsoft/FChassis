using Flux.API;
using System.Windows.Threading;

namespace FChassis.Tools;

/// <summary>
/// The class Nozzle contains the logic of the laser cutting tool nozzle.
/// </summary>
/// <param name="diameter">The diameter of the laser cutting tool nozzle</param>
/// <param name="height">The height of the laser cutting tool nozzle.</param>
/// <param name="segments">No. of segments prescribed to simulate</param>
public class Nozzle (double diameter, double height, int segments) {
   #region Data Members
   readonly Cylinder mCylinder = new (diameter, height, segments);
   #endregion

   #region Draw methods
   public void Draw (XForm4 LHCompTransform, Color32 LHToolColor, XForm4 RHCompTransform, Color32 RHToolColor, Dispatcher dispatcher) {
      List<Point3> lhCompTrfPts = [],
         rhCompTrfPts = [];
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
   }
   #endregion
}