using Flux.API;
namespace FChassis.GCodeGen;


public class CutOut {
   // Constructor: Note that the parentheses after 'CutOut' have been removed
   public CutOut (GCodeGenerator gcgen, Tooling toolingItem,
      Tooling prevToolingItem, List<ToolingSegment> prevSegs, ToolingSegment? lastToolingSegment,
      double xStart, double xPartition,
      double xEnd, double prevCutToolingsLength, double prevMarkToolingsLength,
            double totalMarkLength, double totalToolingCutLength, bool isFirstTooling) {
      GCGen = gcgen;
      ToolingItem = toolingItem; // Initialize the Tooling property here
      mPrevToolingItem = prevToolingItem;
      mPrevTlgSegs = prevSegs;
      mXStart = xStart; mXPartition = xPartition; mXEnd = xEnd;
      mIsFirstTooling = isFirstTooling;
      mLastToolingSegment = lastToolingSegment;
      mPrevCutToolingsLength = prevCutToolingsLength;
      mPrevMarkToolingsLength = prevMarkToolingsLength;
      mTotalMarkLength = totalMarkLength;
      mTotalToolingCutLength = totalToolingCutLength;
      ClassifySegments ();
   }

   // Property for GCodeGenerator
   public GCodeGenerator GCGen { get; set; }
   public List<ToolingSegment> ToolingSegments { get; set; }
   public ToolingSegment? LastToolingSegment { get => mLastToolingSegment; set => mLastToolingSegment = value; }

   Tooling mPrevToolingItem;
   List<ToolingSegment> mPrevTlgSegs;
   double mXStart, mXPartition, mXEnd;
   bool mIsFirstTooling;
   ToolingSegment? mLastToolingSegment;
   double mPrevCutToolingsLength, mPrevMarkToolingsLength,
            mTotalMarkLength, mTotalToolingCutLength;

   // Property for Tooling
   public Tooling ToolingItem {
      get => mT;
      set {
         // Null check to avoid NullReferenceException
         if (value == null) {
            throw new ArgumentNullException (nameof (value), "Tooling cannot be null.");
         }

         // Check if the new value is a valid Cutout
         if (!value.IsCutout ()) {
            throw new Exception ("Not a Cutout object");
         }

         // Assign value only if it's different from the current value
         if (mT != value) {
            mT = value;
         }
      }
   }
   List<(List<ToolingSegment> Segs, bool FlexSegs)> mSegValLists = [];
   // Private field for Tooling
   private Tooling mT;

   // Method WriteNotch (currently empty, but defined for future use)
   void ClassifySegments () {
      mSegValLists = new List<(List<ToolingSegment> Segs, bool FlexSegs)> ();
      ToolingSegments = GCGen.GetSegmentsAccountedForApproachLength (ToolingItem);
      if (ToolingSegments == null || ToolingSegments.Count == 0)
         throw new Exception ("Segments accounted for entry is null");

      var flexSegIndices = Notch.GetFlexSegmentIndices (ToolingSegments);
      List<ToolingSegment> flgSegs = [];
      bool isFlexSeg = false;

      for (int ii = 0; ii < ToolingSegments.Count; ii++) {
         if (flexSegIndices.Any (e => (ii >= e.Item1 && ii <= e.Item2) || (ii >= e.Item2 && ii <= e.Item1))) {
            if (!isFlexSeg) {
               if (flgSegs.Count > 0) {
                  mSegValLists.Add ((new List<ToolingSegment> (flgSegs), isFlexSeg));
                  flgSegs.Clear ();
               }
            }
            flgSegs.Add (ToolingSegments[ii]);
            isFlexSeg = true;
         } else {
            if (isFlexSeg) {
               if (flgSegs.Count > 0) {
                  mSegValLists.Add ((new List<ToolingSegment> (flgSegs), isFlexSeg));
                  flgSegs.Clear ();
               }
            }
            flgSegs.Add (ToolingSegments[ii]);
            isFlexSeg = false;
         }
      }
      if (flgSegs.Count > 0) {
         mSegValLists.Add ((new List<ToolingSegment> (flgSegs), isFlexSeg));
         flgSegs.Clear ();
      }
   }

   public void WriteTooling () {
      bool firstTime = true;
      for (int ii=0; ii<mSegValLists.Count; ii++ ) {
         var (Segs, FlexSegs) = mSegValLists[ii];
         double currSegsLen = 0;
         GCGen.InitializeToolingBlock (ToolingItem, mPrevToolingItem, /*frameFeed,*/
              mXStart, mXPartition, mXEnd, ToolingSegments, /*isValidNotch:*/false, ii==mSegValLists.Count-1, FlexSegs);
         if (firstTime) {
            GCGen.PrepareforToolApproach (ToolingItem, ToolingSegments, mLastToolingSegment, mPrevToolingItem, mPrevTlgSegs, mIsFirstTooling, /*isValidNotch:*/false);
            GCGen.WriteToolCorrectionData (ToolingItem);
            firstTime = false;
         }
         WriteGCode (Segs);
         currSegsLen += Segs.Sum (s => s.Curve.Length);
         GCGen.FinalizeToolingBlock (ToolingItem, mPrevCutToolingsLength, mPrevMarkToolingsLength, mTotalMarkLength, mTotalToolingCutLength);
         mPrevCutToolingsLength += currSegsLen;
      }
   }

   public void WriteGCode (List<ToolingSegment> toolingSegs) {

      (var curve, var CurveStartNormal, _) = toolingSegs[0];
      Utils.EPlane previousPlaneType = Utils.EPlane.None;
      Utils.EPlane currPlaneType;
      if (ToolingItem.IsFlexFeature ()) currPlaneType = Utils.EPlane.Flex;
      else currPlaneType = Utils.GetFeatureNormalPlaneType (CurveStartNormal, new ());
      // Write any feature other than notch
      GCGen.MoveToMachiningStartPosition (curve.Start, CurveStartNormal, ToolingItem.Name);
      {
         // Write all other features such as Holes, Cutouts and edge notches
         GCGen.EnableMachiningDirective ();
         for (int i = 0; i < toolingSegs.Count; i++) {
            var (Curve, startNormal, endNormal) = toolingSegs[i];
            startNormal = startNormal.Normalized ();
            endNormal = endNormal.Normalized ();
            var startPoint = Curve.Start;
            var endPoint = Curve.End;
            if (i > 0) currPlaneType = Utils.GetFeatureNormalPlaneType (endNormal, new ());

            if (Curve is Arc3) { // This is a 2d arc. 
               var arcPlaneType = Utils.GetArcPlaneType (startNormal, new ());
               var arcFlangeType = Utils.GetArcPlaneFlangeType (startNormal, new ());
               (var center, _) = Geom.EvaluateCenterAndRadius (Curve as Arc3);
               GCGen.WriteArc (Curve as Arc3, arcPlaneType, arcFlangeType, center, startPoint, endPoint, startNormal,
                  ToolingItem.Name);
            } else GCGen.WriteLine (endPoint, startNormal, endNormal, currPlaneType, previousPlaneType,
               Utils.GetFlangeType (ToolingItem, new ()), ToolingItem.Name);
            previousPlaneType = currPlaneType;
         }
         GCGen.DisableMachiningDirective ();
         mLastToolingSegment = toolingSegs[^1];
      }
   }
}


