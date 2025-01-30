using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

using FChassis.GCodeGen;
using FChassis.Tools;
using Flux.API;
using FChassis.Core;

namespace FChassis.Processes;

/// <summary>Processor is used to generate G-Code, and the Traces for simulation</summary>
public class Processor : INotifyPropertyChanged {

   #region Enums
   public enum RefCSys {
      WCS,
      MCS
   }
   public enum ESimulationStatus {
      Running,
      Paused,
      NotRunning
   }
   #endregion

   #region G Code Drawables and Utilities
   List<List<GCodeSeg>> mTraces = [[], []];
   public List<List<GCodeSeg>> Traces { get => mTraces; }
   public List<List<GCodeSeg>[]> CutScopeTraces { get => mGCodeGenerator.CutScopeTraces; }
   //MultiPassCuts mMultipassCuts;

   public void ClearTraces () {
      mTraces[0]?.Clear ();
      mTraces[1]?.Clear ();
      CutScopeTraces?.Clear ();
   }
   readonly List<XForm4>[] mXForms = [[], []];
   public List<XForm4>[] XForms => mXForms;
   public void ClearXForms () { mXForms[0].Clear (); mXForms[1].Clear (); }

   RefCSys mReferenceCS = RefCSys.WCS;
   public RefCSys ReferenceCS { get => mReferenceCS; set => mReferenceCS = value; }
   #endregion

   #region Digital Twins - Resources and Workpiece
   Workpiece mWorkpiece;
   public Workpiece Workpiece {
      get => mWorkpiece;
      set {
         if (mWorkpiece != value) {
            mWorkpiece = value;
            mGCodeGenerator.OnNewWorkpiece ();
         }
      }
   }
   Nozzle mMachiningTool;
   public Nozzle MachiningTool { get => mMachiningTool; set => mMachiningTool = value; }
   #endregion

   #region Simulation and Redraw Data members
   public delegate void TriggerRedrawDelegate ();
   public delegate void SetSimulationStatusDelegate (Processor.ESimulationStatus status);
   public delegate void ZoomExtentsWithBound3Delegate (Bound3 bound);
   public event TriggerRedrawDelegate TriggerRedraw;
   public event Action SimulationFinished;
   public event SetSimulationStatusDelegate SetSimulationStatus;
   public event ZoomExtentsWithBound3Delegate zoomExtentsWithBound3Delegate;
   readonly Dispatcher mDispatcher;
   ESimulationStatus mSimulationStatus = ESimulationStatus.NotRunning;
   public ESimulationStatus SimulationStatus {
      get => mSimulationStatus;
      set {
         if (mSimulationStatus != value) {
            mSimulationStatus = value;
            SetSimulationStatus?.Invoke (value);
         }
      }
   }
   public event PropertyChangedEventHandler PropertyChanged;
   protected void OnPropertyChanged (string propertyName) {
      PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (propertyName));
   }
   double mPrevStepLen;
   private int mCutScopeIndex = 0;
   private readonly object mCutScopeLockObject = new ();
   bool mIsZoomedToCutScope = false;
   XForm4 mTransform0, mTransform1;
   Point3 mWayPt0, mWayPt1;
   EMove mMoveType0, mMoveType1;
   #endregion

   #region Constructor
   public Processor (Dispatcher dispatcher) {
      mDispatcher = dispatcher;
      MachiningTool = new Nozzle (9.0, 100.0, 100);
      mGCodeGenerator = new GCodeGenerator (this, true/* Left to right machining*/);
      mGCodeParser = new GCodeParser ();
      mReferenceCS = RefCSys.WCS;
      CutHoles = true;
      CutMark = true;
      CutNotches = true;
      Cutouts = true;
   }
   #endregion

   #region GCode generator properties
   public MCSettings.PartConfigType PartConfigType {
      get => mGCodeGenerator.PartConfigType;
      set => mGCodeGenerator.PartConfigType = value;
   }
   public bool Cutouts { get => mGCodeGenerator.Cutouts; set => mGCodeGenerator.Cutouts = value; }
   public bool CutHoles { get => mGCodeGenerator.CutHoles; set => mGCodeGenerator.CutHoles = value; }
   public bool CutMark { get => mGCodeGenerator.CutMarks; set => mGCodeGenerator.CutMarks = value; }
   public bool CutNotches { get => mGCodeGenerator.CutNotches; set => mGCodeGenerator.CutNotches = value; }
   public MCSettings.EHeads Heads { get => mGCodeGenerator.Heads; set => mGCodeGenerator.Heads = value; }
   public double PartitionRatio { get => mGCodeGenerator.PartitionRatio; set => mGCodeGenerator.PartitionRatio = value; }
   public double NotchWireJointDistance { get => mGCodeGenerator.NotchWireJointDistance; set => mGCodeGenerator.NotchWireJointDistance = value; }
   #endregion

   #region GCode Generator and Utilities
   GCodeParser mGCodeParser;
   readonly GCodeGenerator mGCodeGenerator;
   public GCodeGenerator GCodeGen { get => mGCodeGenerator; }
   public void ClearZombies () {
      ClearTraces ();
      ClearXForms ();
      RewindEnumerator (0);
      RewindEnumerator (1);
      TriggerRedraw?.Invoke ();
      //mMultipassCuts?.ClearZombies ();
      mGCodeGenerator.ClearZombies ();
   }
   public void LoadGCode (string filename) {
      try {
         mGCodeParser.Parse (filename);
      } catch (Exception e) {
         string formattedString = String.Format ("Parsing GCode file {0} failed. Error: {1}", filename, e.Message);
         MessageBox.Show (formattedString, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
         return;
      }
      mTraces[0] = mGCodeParser.Traces[0];
      mTraces[1] = mGCodeParser.Traces[1];
      TriggerRedraw?.Invoke ();
   }

   public void ResetGCodeGenForTesting () => mGCodeGenerator?.ResetForTesting (MCSettings.It);

   /// <summary>Uses Processor to generate code, and to generate the simulation traces</summary>
   /// If 'testing' is set to true, we reset the settings to a known stable value
   /// used for testing, and create always a partition at 0.5. Otherwise, we use a 
   /// dynamically computed optimal partitioning
   //public void ComputeGCode (bool testing = false, double ratio = 0.5) {
   public void ComputeGCode (bool testing = false) {
      ClearZombies ();
      mTraces = Utils.ComputeGCode (mGCodeGenerator, testing);
   }
   #endregion

   #region Simulation Implementation
   List<Tuple<Point3, Vector3>>[] mWayPoints = new List<Tuple<Point3, Vector3>>[2];

   struct GCodeSegmentIndices {
      public GCodeSegmentIndices () {
         gCodeSegIndex = 0;
         wayPointIndex = 0;
      }
      public int gCodeSegIndex, wayPointIndex;
   }
   GCodeSegmentIndices[] mNextXFormIndex = [new (), new ()];
   (XForm4 XForm, Point3 WayPt, EMove MoveType)? GetNextToolXForm (int head) {
      XForm4 xFormRes;
      if (mTraces[head] == null || MachiningTool == null) return null;

      if (mNextXFormIndex[head].gCodeSegIndex >= mTraces[head].Count) return null;
      int steps = (int)(mTraces[head][mNextXFormIndex[head].gCodeSegIndex].Length / MCSettings.It.StepLength);

      if (mNextXFormIndex[head].wayPointIndex == 0) {
         if (mTraces[head][mNextXFormIndex[head].gCodeSegIndex].GCode is EGCode.G0 or EGCode.G1) mWayPoints[head] =
               Utils.DiscretizeLine (mTraces[head][mNextXFormIndex[head].gCodeSegIndex], steps);
         else if (mTraces[head][mNextXFormIndex[head].gCodeSegIndex].GCode is EGCode.G2 or EGCode.G3) mWayPoints[head] =
               Utils.DiscretizeArc (mTraces[head][mNextXFormIndex[head].gCodeSegIndex], steps);
      }
      if (mWayPoints[head].Count == 0) throw new Exception ("Unable to compute treadingPoints");
      var waypointVec = mWayPoints[head][mNextXFormIndex[head].wayPointIndex];
      mNextXFormIndex[head].wayPointIndex++;
      if (mNextXFormIndex[head].wayPointIndex >= mWayPoints[head].Count) {
         mNextXFormIndex[head].gCodeSegIndex += 1;
         mNextXFormIndex[head].wayPointIndex = 0;
      }
      var (wayPt, wayVecAtPt) = waypointVec;
      var yComp = Geom.Cross (wayVecAtPt, XForm4.mXAxis).Normalized ();
      xFormRes = new XForm4 (XForm4.mXAxis, yComp, wayVecAtPt.Normalized (), Geom.P2V (wayPt));
      if (ReferenceCS == RefCSys.MCS) xFormRes = GCodeGenerator.XfmToMachine (mGCodeGenerator, xFormRes);

      return (xFormRes, wayPt, mTraces[head][mNextXFormIndex[head].gCodeSegIndex].MoveType);
   }

   void RewindEnumerator (int head) {
      mNextXFormIndex[head].wayPointIndex = 0;
      mNextXFormIndex[head].gCodeSegIndex = 0;
      mWayPoints = new List<Tuple<Point3, Vector3>>[2];
      SetCutScopeIndex (0);
      if (CutScopeTraces.Count > 0) {
         mTraces[0] = CutScopeTraces[0][0];
         mTraces[1] = CutScopeTraces[0][1];
      }
   }
   
   void DrawToolSim (int head) {
      var mcCss = GCodeGen.MachinableCutScopes;
      Bound3 bound = new ();

      if (mcCss.Count > 0)
         bound = mcCss[0].Bound;

      if (!mIsZoomedToCutScope) {
         zoomExtentsWithBound3Delegate?.Invoke (bound);
         mIsZoomedToCutScope = true;
      }

      while (true) {
         if (head == 3) {
            (mTransform0, mWayPt0, mMoveType0) = GetNextToolXForm (0) ?? (new XForm4 (), new Point3 (), EMove.None);
            (mTransform1, mWayPt1, mMoveType1) = GetNextToolXForm (1) ?? (new XForm4 (), new Point3 (), EMove.None);
         } else if (head == 0) {
            (mTransform0, mWayPt0, mMoveType0) = GetNextToolXForm (0) ?? (new XForm4 (), new Point3 (), EMove.None);
         } else if (head == 1) {
            (mTransform1, mWayPt1, mMoveType1) = GetNextToolXForm (1) ?? (new XForm4 (), new Point3 (), EMove.None);
         }

         if (mTransform0 == null && mTransform1 == null && SimulationStatus != ESimulationStatus.NotRunning) {
            // If Multipass
            if (CutScopeTraces.Count > 1 && GetCutScopeIndex () + 1 < CutScopeTraces.Count) {
               // Safe incrementor
               IncrementCutScopeIndex ();
               int csIdx = GetCutScopeIndex ();

               if (csIdx >= 0 && csIdx < mcCss.Count)
                  zoomExtentsWithBound3Delegate?.Invoke (mcCss[csIdx].Bound);

               // Reset enumerator
               RewindEnumerator (0);
               RewindEnumerator (1);

               // Rewind will reset everything, so the cutscope index needs to be restored
               SetCutScopeIndex (csIdx);

               if (head == 3) {
                  mTraces[0] = CutScopeTraces[csIdx][0];
                  mTraces[1] = CutScopeTraces[csIdx][1];
               } else {
                  mTraces[head] = CutScopeTraces[GetCutScopeIndex ()][head];
               }

               mMachiningTool.Draw (
                   mTransform0, Utils.LHToolColor,
                   mTransform1, Utils.RHToolColor,
                   Utils.ToolTipColor2, mDispatcher,
                   mWayPt0, mTransform0?.ZCompRot,
                   mWayPt1, mTransform1?.ZCompRot,
                   mMoveType0, mMoveType1
               );

               return; // Exit the loop after drawing
            } else {
               // Draw the tool again at the beginning of the process
               RewindEnumerator (0);
               RewindEnumerator (1);

               if (CutScopeTraces.Count > 0) {
                  if (head == 3) {
                     mTraces[0] = CutScopeTraces[0][0];
                     mTraces[1] = CutScopeTraces[0][1];
                  } else {
                     mTraces[head] = CutScopeTraces[mCutScopeIndex][head];
                  }
               }

               mMachiningTool.Draw (
                   mTransform0, Utils.LHToolColor,
                   mTransform1, Utils.RHToolColor,
                   Utils.ToolTipColor2, mDispatcher,
                   mWayPt0, mTransform0?.ZCompRot,
                   mWayPt1, mTransform1?.ZCompRot,
                   mMoveType0, mMoveType1
               );

               // Finish the simulation trigger
               SimulationFinished?.Invoke ();
               SimulationStatus = ESimulationStatus.NotRunning;

               if (MCSettings.It.EnableMultipassCut)
                  MCSettings.It.StepLength = mPrevStepLen;

               Lux.StopContinuousRender (GFXCallback);
               TriggerRedraw ();

               // Restore the zoom to cover the entire part
               zoomExtentsWithBound3Delegate?.Invoke (Workpiece.Bound);
               return;
            }
         } else {
            mMachiningTool.Draw (
                mTransform0, Utils.LHToolColor,
                mTransform1, Utils.RHToolColor,
                Utils.ToolTipColor2, mDispatcher,
                mWayPt0, mTransform0?.ZCompRot,
                mWayPt1, mTransform1?.ZCompRot,
                mMoveType0, mMoveType1
            );

            return;
         }
      }
   }


   public void DrawToolInstance () {
      if (SimulationStatus == ESimulationStatus.Running) {
         int head = 0;
         if (MCSettings.It.Heads == MCSettings.EHeads.Right) head = 1;
         if (MCSettings.It.Heads == MCSettings.EHeads.Both) DrawToolSim (3);
         else DrawToolSim (head);
      }
   }

   /// <summary>Called when the SIMULATE button is clicked</summary>
   public void Run () {
      if (SimulationStatus == ESimulationStatus.Running) return;

      var prevSimulationStatus = SimulationStatus;
      if (mTraces[0] == null && mTraces[1] == null) return;
      if (mTraces[0] != null && mTraces[0].Count > 0) {
         SimulationStatus = ESimulationStatus.Running;
         mXForms[0].Clear ();
      }
      if (mTraces[1] != null && mTraces[1].Count > 0) {
         SimulationStatus = ESimulationStatus.Running;
         mXForms[1].Clear ();
      }
      if (SimulationStatus == ESimulationStatus.Running) {
         if (MCSettings.It.Heads is MCSettings.EHeads.Left or MCSettings.EHeads.Both &&
            prevSimulationStatus == ESimulationStatus.NotRunning) RewindEnumerator (0);
         if (MCSettings.It.Heads is MCSettings.EHeads.Right or MCSettings.EHeads.Both &&
            prevSimulationStatus == ESimulationStatus.NotRunning) RewindEnumerator (1);

         mPrevStepLen = MCSettings.It.StepLength;
         SetCutScopeIndex (0);
         Lux.StartContinuousRender (GFXCallback);
      }
   }
   void GFXCallback (double elapsed) {
      // TODO : Based on the elapsed time, the speed of the tool(s)
      // should be calculated.
      TriggerRedraw?.Invoke ();
   }

   public void Stop () {
      Lux.StopContinuousRender (GFXCallback);
      if (MCSettings.It.EnableMultipassCut) MCSettings.It.StepLength = mPrevStepLen;
      SimulationStatus = ESimulationStatus.NotRunning;
      if (MCSettings.It.Heads is MCSettings.EHeads.Left or MCSettings.EHeads.Both) RewindEnumerator (0);
      if (MCSettings.It.Heads is MCSettings.EHeads.Right or MCSettings.EHeads.Both) RewindEnumerator (1);
      SimulationFinished?.Invoke ();
      GFXCallback (0.01);
   }
   public void Pause () {
      SimulationStatus = ESimulationStatus.Paused;
      if (MCSettings.It.EnableMultipassCut) MCSettings.It.StepLength = mPrevStepLen;
      Lux.StopContinuousRender (GFXCallback);
   }
   #endregion

   #region GCode Draw Implementation
   public void DrawGCode () {
      foreach (var cutScopeTooling in CutScopeTraces)
         DrawGCode (cutScopeTooling);
   }
   public void DrawGCodeForCutScope () {
      // If simulation runs and when a new part is loaded, this 
      // check is necessary
      if (CutScopeTraces.Count > 0)
         DrawGCode (CutScopeTraces[GetCutScopeIndex ()]);
   }

   // Method to set the index
   public void SetCutScopeIndex (int value) {
      lock (mCutScopeLockObject) {
         mCutScopeIndex = value;
      }
   }

   // Method to get the index
   public int GetCutScopeIndex () {
      lock (mCutScopeLockObject) {
         return mCutScopeIndex;
      }
   }

   //// Method to increment the index safely
   public void IncrementCutScopeIndex () {
      lock (mCutScopeLockObject) {
         mCutScopeIndex++;
      }
   }

   public void DrawGCode (List<GCodeSeg>[] cutScopeTooling) {
      List<List<GCodeSeg>> listOfListOfDrawables = [];
      if (cutScopeTooling[0].Count > 0) listOfListOfDrawables.Add (cutScopeTooling[0]);
      if (cutScopeTooling[1].Count > 0) listOfListOfDrawables.Add (cutScopeTooling[1]);
      List<Action> drawActions = [];
      List<Point3> G0DrawPoints = [], G1DrawPoints = [];
      List<List<Point3>> G2DrawPoints = [], G3DrawPoints = [];
      foreach (var drawables in listOfListOfDrawables) {
         foreach (var gcseg in drawables) {
            var seg = gcseg;
            if (ReferenceCS == RefCSys.MCS)
               seg = seg.XfmToMachineNew (mGCodeGenerator);
            Color32 segColor = Color32.Nil;

            if (seg.IsLine ()) {
               if (seg.GCode == EGCode.G0 || seg.MoveType == EMove.Retract2Machining) {
                  segColor = new Color32 (255, 255, 255);
                  G0DrawPoints.Add (seg.StartPoint);
                  G0DrawPoints.Add (seg.EndPoint);
               } else {
                  segColor = Color32.Blue;
                  G1DrawPoints.Add (seg.StartPoint);
                  G1DrawPoints.Add (seg.EndPoint);
               }
            } else if (seg.IsArc ()) {
               var arcPointVecs = Utils.DiscretizeArc (seg, 50);
               List<Point3> arcPts = [];
               if (seg.GCode == EGCode.G3) {
                  segColor = Color32.Cyan;
                  foreach (var ptVec in arcPointVecs) arcPts.Add (ptVec.Item1);
                  G3DrawPoints.Add (arcPts);
               } else {
                  segColor = Color32.Magenta;
                  foreach (var ptVec in arcPointVecs) arcPts.Add (ptVec.Item1);
                  G2DrawPoints.Add (arcPts);
               }
            }
         }
      }
      mDispatcher.Invoke (() => {
         Lux.HLR = true;
         Lux.Color = Utils.G3SegColor;
         foreach (var arcPoints in G3DrawPoints) {
            Lux.Draw (EDraw.Lines, arcPoints);

            // The following draw call is to terminate drawing of the 
            // above arc points. Else, the arcs are connected continuously
            // There has to be a better/elegant solution: TODO
            Lux.Draw (EDraw.LineStrip, [arcPoints[^1], arcPoints[^1]]);
         }
      });
      mDispatcher.Invoke (() => {
         Lux.HLR = true;
         Lux.Color = Utils.G2SegColor;
         foreach (var arcPoints in G2DrawPoints) {
            Lux.Draw (EDraw.Lines, arcPoints);

            // The following draw call is to terminate drawing of the 
            // above arc points. Else, the arcs are connected continuously
            // There has to be a better/elegant solution: TODO
            Lux.Draw (EDraw.LineStrip, [arcPoints[^1], arcPoints[^1]]);
         }
      });
      mDispatcher.Invoke (() => {
         Lux.HLR = true;
         Lux.Color = Utils.G0SegColor;
         Lux.Draw (EDraw.Lines, G0DrawPoints);
      });
      mDispatcher.Invoke (() => {
         Lux.HLR = true;
         Lux.Color = Utils.G1SegColor;
         Lux.Draw (EDraw.Lines, G1DrawPoints);
      });
   }
   #endregion
}