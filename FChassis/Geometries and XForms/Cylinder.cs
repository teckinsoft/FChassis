using Flux.API;
using System.Windows.Threading;
using FChassis.Core;
namespace FChassis;

public class Cylinder {
   readonly double mDiameter = 10, mHeight = 40;
   readonly int mSegments = 100;
   public List<Point3> Points { get; set; } = [];
   public List<Geom.Triangle3D> Triangles { get; set; } = [];
   public Cylinder (double diameter, double height, int segments) {
      mDiameter = diameter; mHeight = height; mSegments = segments;
      double radius = mDiameter / 2.0;
      double angleStep = 2 * Math.PI / mSegments;
      double startZ = 0.0; // Starting X coordinate

      // Add points for the bottom base
      for (int i = 0; i <= mSegments; i++) {
         double angle = i * angleStep;
         Points.Add (new Point3 (radius * Math.Cos (angle), radius * Math.Sin (angle), startZ));
      }
      // Add points for the top base
      for (int i = 0; i < mSegments; i++) {
         double angle = i * angleStep;
         Points.Add (new Point3 (radius * Math.Cos (angle), radius * Math.Sin (angle), startZ + mHeight));
      }
      // Create triangles for the bottom base
      int bottomCenterIndex = Points.Count;
      Points.Add (new Point3 (0, 0, startZ)); // Center of the bottom base
      for (int i = 0; i < mSegments; i++) {
         Triangles.Add (new Geom.Triangle3D (bottomCenterIndex, i, (i + 1) % mSegments));
      }
      // Create triangles for the top base
      int topCenterIndex = Points.Count;
      Points.Add (new Point3 (0, 0, startZ + mHeight)); // Center of the top base
      for (int i = 0; i < mSegments; i++) {
         Triangles.Add (new Geom.Triangle3D (topCenterIndex, mSegments + ((i + 1) % mSegments), mSegments + i));
      }
      // Create triangles for the lateral surface
      for (int i = 0; i < mSegments; i++) {
         int nextIndex = (i + 1) % mSegments;
         Triangles.Add (new Geom.Triangle3D (i, mSegments + i, nextIndex));
         Triangles.Add (new Geom.Triangle3D (nextIndex, mSegments + i, mSegments + nextIndex));
      }
   }
}