using FChassis.Processes;
using Flux.API;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FChassis.GCodeGen;

using static FChassis.MCSettings;
using static FChassis.Utils;
using NotchAttribute = Tuple<
        Curve3, // Split curve, whose end point is the notch point
        Vector3, // Start Normal
        Vector3, // End Normal
        Vector3, // Outward Normal along flange
        Vector3, // Vector Outward to nearest boundary
        XForm4.EAxis, // Proximal boundary direction
        bool>; // Some boolean value 
using CutScopeToolingList = List<(List<Tooling> ToolingList, double XMin, double XMax)>;

/// <summary>
/// The following class parses any G Code and caches the G0 and G1 segments. Work is 
/// still in progress to read G2 and G3 segments. The processor has to be set with 
/// the Traces to simulate. Currently, only one G Code (file) can be used for simulation.
/// </summary>
public class GCodeParser {
   #region Data Members
   XForm4 mXformLH, mXformRH;
   Point3? mLHOrigin, mRHOrigin;
   int? mHead;
   #endregion

   #region Properties
   readonly List<GCodeSeg>[] mTraces = [[], []];
   public List<GCodeSeg>[] Traces { get => mTraces; }
   double? mJobLength;
   public double? JobLength { get => mJobLength; set => mJobLength = value; }
   double? mJobWidth;
   public double? JobWidth { get => mJobWidth; set => mJobWidth = value; }
   double? mJobThickness;
   public double? JobThickness { get => mJobThickness; set => mJobThickness = value; }
   bool? mLHComponent = true;
   public bool? LHComponent { get => mLHComponent; set { mLHComponent = value; } }
   #endregion

   #region Constructor(s)
   public GCodeParser () { }
   #endregion

   #region Data Processor(s)
   void EvaluateMachineXFm () {
      // For LH component
      mXformLH = new XForm4 ();
      mXformLH.Translate (new Vector3 (0.0, -JobWidth.Value / 2.0, 0.0));

      // For RH component
      mXformRH = new XForm4 ();
      mXformRH.Translate (new Vector3 (0.0, JobWidth.Value / 2.0, 0.0));
   }
   #endregion

   #region Lifecyclers
   public void ClearZombies () {
      mTraces[0].Clear ();
      mTraces[1].Clear ();
   }
   #endregion

   #region Parser(s)
   public void Parse (string filePath) {
      var fileLines = File.ReadAllLines (filePath);
      double lastX = 0, lastY = 0, lastZ = 0, lastAngle = 0;
      double iic = 0, jjc = 0, kkc = 0;
      Vector3 lastNormal = XForm4.mZAxis;
      bool firstEntry = true;
      string arcPlane = "XY";
      bool initTime = true;
      foreach (var line in fileLines) {
         if (line.StartsWith ("G18")) arcPlane = "XZ";
         if (line.StartsWith ("G17")) arcPlane = "XY";

         // Variables init
         double x = lastX, y = lastY, z = lastZ, angle = lastAngle;
         Vector3 normal = lastNormal;

         string cncIdPattern = @"CNC_ID\s*=\s*(\d+)";
         Match cncIdMatch = Regex.Match (line, cncIdPattern, RegexOptions.IgnoreCase);
         if (cncIdMatch.Success) {
            mHead = int.Parse (cncIdMatch.Groups[1].Value) - 1;
            if (mHead != 0 && mHead != 1)
               throw new Exception ("Undefined head (tool)");
            mTraces[mHead.Value].Clear ();
         }
         {
            string jobLengthPattern = @"Job_Length\s*=\s*(\d+)";
            Match jobLengthMatch = Regex.Match (line, jobLengthPattern, RegexOptions.IgnoreCase);
            if (jobLengthMatch.Success) {
               JobLength = int.Parse (jobLengthMatch.Groups[1].Value);
               if (!JobLength.HasValue)
                  throw new Exception ("Job length can not be inferred from Din file");
            }
         }
         {
            string jobWidthPattern = @"Job_Width\s*=\s*(\d+)";
            Match jobWidthMatch = Regex.Match (line, jobWidthPattern, RegexOptions.IgnoreCase);
            if (jobWidthMatch.Success) {
               JobWidth = int.Parse (jobWidthMatch.Groups[1].Value);
               if (!JobWidth.HasValue)
                  throw new Exception ("Job Width can not be inferred from Din file");
            }
         }
         {
            string jobThicknessPattern = @"Job_Thickness\s*=\s*(\d+)";
            Match jobThicknessMatch = Regex.Match (line, jobThicknessPattern, RegexOptions.IgnoreCase);
            if (jobThicknessMatch.Success) {
               JobThickness = int.Parse (jobThicknessMatch.Groups[1].Value);
               if (!JobThickness.HasValue)
                  throw new Exception ("Job Thickness can not be inferred from Din file");
            }
         }
         if (initTime && JobLength.HasValue && JobWidth.HasValue && JobThickness.HasValue) {
            mLHOrigin = new Point3 (0.0, -JobWidth.Value / 2, JobThickness.Value);
            mRHOrigin = new Point3 (JobLength.Value, JobWidth.Value / 2, JobThickness.Value);
            EvaluateMachineXFm ();
            initTime = false;
         }

         string jobTypePattern = @"Job_Type\s*=\s*(\d+)";
         Match jobTypeMatch = Regex.Match (line, jobTypePattern, RegexOptions.IgnoreCase);
         if (jobTypeMatch.Success) {
            var job_type = int.Parse (jobTypeMatch.Groups[1].Value);
            if (job_type == 1) LHComponent = true;
            else if (job_type == 2) LHComponent = false;
            else throw new Exception
                  ("Undefined Part Configuration [ Job_Type should either be 1 (LHComponent) or 2 (RHComponent)]");
         }

         // Regular expression to match G followed by 0, 1, 2, or 3 with optional whitespace (spaces, tabs)
         string gPattern = @"G\s*([0-9]+)";
         Match gMatch = Regex.Match (line, gPattern, RegexOptions.IgnoreCase);
         EGCode eGCodeVal;
         if (gMatch.Success) {
            //axisValues["G"] = double.Parse (gMatch.Groups[1].Value);
            var gval = int.Parse (gMatch.Groups[1].Value);
            eGCodeVal = gval switch {
               0 => EGCode.G0,
               1 => EGCode.G1,
               2 => EGCode.G2,
               3 => EGCode.G3,
               _ => EGCode.None
            };

            // Regular expression to match X, Y, Z, A, B, C, I, J, K, F followed by optional
            // whitespace (spaces, tabs) and then a number
            string axisPattern = @"([XYZABCIJKxyzabcijkfF])\s*([-+]?\d+(\.\d+)?)";
            MatchCollection axisMatches = Regex.Matches (line, axisPattern);

            // Loop through all matches and add them to the dictionary
            foreach (Match match in axisMatches) {
               string axis = match.Groups[1].Value.ToUpper ();
               double value = double.Parse (match.Groups[2].Value);
               //axisValues[axis] = value;
               switch (axis[0]) {
                  case 'X': x = value; continue;
                  case 'Y': y = value; continue;
                  case 'Z': z = value; continue;
                  case 'I': iic = value; continue;
                  case 'J': jjc = value; continue;
                  case 'K': kkc = value; continue;
                  case 'A':
                     normal = new Vector3 (0, -Math.Sin (value.D2R ()), Math.Cos (value.D2R ()));
                     normal = new Vector3 (0, -Math.Sin (value.D2R ()), Math.Cos (value.D2R ()));
                     continue;
                  default:
                     continue;
               }
            }
            string comment = string.Format ($"( Din file {0} )", filePath);
            if (!LHComponent.HasValue) throw new Exception ("Unable to find the part configuration. LHComponent or RHComponent");
            if (!mHead.HasValue) throw new Exception ("Unable to find the Tool '0' or '1'");
            if (eGCodeVal == EGCode.G0 || eGCodeVal == EGCode.G1) {
               var point = new Point3 (x, y, z);
               if (LHComponent.Value) point = Geom.V2P (mXformLH * point);
               else point = Geom.V2P (mXformRH * point);
               Point3 prevPoint;
               if (firstEntry) {
                  prevPoint = mLHComponent.Value ? mLHOrigin.Value : mRHOrigin.Value;
                  firstEntry = false;
               } else prevPoint = mTraces[mHead.Value][^1].EndPoint;

               mTraces[mHead.Value].Add (new GCodeSeg (prevPoint, point,
                           lastNormal, normal, eGCodeVal, EMove.Machining, comment));
            } else if (eGCodeVal == EGCode.G2 || eGCodeVal == EGCode.G3) {
               Point3 arcStartPoint = new (lastX, lastY, lastZ),
                  arcEndPoint = new (x, y, z), arcCenter;
               if (arcPlane == "XY") arcCenter = new Point3 (arcStartPoint.X + iic, arcStartPoint.Y + jjc, z);
               else arcCenter = new Point3 (arcStartPoint.X + iic, y, arcStartPoint.Z + kkc);

               if (mLHComponent.Value) {
                  arcStartPoint = Geom.V2P (mXformLH * arcStartPoint);
                  arcEndPoint = Geom.V2P (mXformLH * arcEndPoint);
                  arcCenter = Geom.V2P (mXformLH * arcCenter);
               } else {
                  arcStartPoint = Geom.V2P (mXformRH * arcStartPoint);
                  arcEndPoint = Geom.V2P (mXformRH * arcEndPoint);
                  arcCenter = Geom.V2P (mXformRH * arcCenter);
               }

               Arc3 arc = null;
               if (eGCodeVal == EGCode.G2) arc = Geom.CreateArc (arcStartPoint, arcEndPoint, arcCenter, normal, Utils.EArcSense.CW);
               else arc = Geom.CreateArc (arcStartPoint, arcEndPoint, arcCenter, normal, Utils.EArcSense.CCW);

               var radius = (arcStartPoint - arcCenter).Length;
               mTraces[mHead.Value].Add (new GCodeSeg (arc, arcStartPoint, arcEndPoint, arcCenter, radius, normal, eGCodeVal,
                     EMove.Machining, comment));
            }
         }
         lastX = x;
         lastY = y;
         lastZ = z;
         lastNormal = normal;
      }
   }
   #endregion
}

public class GCodeGenerator {
   #region Data members
   List<E3Flex> mFlexes;      // Flexes in the workpiece
   List<E3Plane> mPlanes;     // Planes in this workpiece
   double mThickness;         // Workpiece thickness
   readonly Point3[] mToolPos = new Point3[2];     // Tool position (for each head)
   readonly Vector3[] mToolVec = new Vector3[2];   // Tool orientaton (for each head)
   readonly Point3[] mSafePoint = new Point3[2];
   readonly bool mDebug = false;
   StreamWriter sw;
   bool mMachiningDirectiveSet = false;
   double mCurveLeastLength = 0.5;
   double[] mPercentLengths = [0.25, 0.5, 0.75];
   int mProgramNumber;
   int mCutScopeNo = 0;
   string NCName;
   bool mLastCutScope = false;
   #endregion

   #region Properties
   List<GCodeSeg>[] mTraces = [[], []];
   //public List<GCodeSeg>[] Traces => mTraces;
   Processor mProcess;
   List<List<GCodeSeg>[]> mCutScopeTraces = [];
   public List<List<GCodeSeg>[]> CutScopeTraces => mCutScopeTraces;
   public Processor Process { get => mProcess; set => mProcess = value; }
   List<NotchAttribute> mNotchAttributes = [];
   public List<NotchAttribute> NotchAttributes { get { return mNotchAttributes; } }
   public List<MachinableCutScope> MachinableCutScopes { get; private set; }
   #endregion

   #region Lifecyclers
   public void ClearZombies () {
      mTraces[0].Clear ();
      mTraces[1].Clear ();
      CutScopeTraces?.Clear ();
      ResetBookKeepers ();
   }

   void ResetNotchCutoutBlockTypes () {
      mNotchSegBlockTypeTopFlange = -4.0; mNotchSegBlockTypeBottomFlange = 4.0;
      mNotchSegBlockTypeWeb2TopFlange = -5.0; mNotchSegBlockTypeWeb2BottomFlange = 5.0;
   }
   void WriteBlockType (Tooling toolingItem, bool isValidNotch, bool isFlexCut, bool edgeNotch = false) {
      if (edgeNotch) return;
      string comment = "";
      double blockType;
      var notchCutKind = toolingItem.NotchKind;
      var cutoutKind = toolingItem.CutoutKind;
      var isCutout = toolingItem.IsCutout ();
      string gcodeSt = "";
      if (!isCutout && !isValidNotch) {
         if (Utils.GetFlangeType (toolingItem, GetXForm ()) == Utils.EFlange.Web) { // If any feature confined to only one flange
            if (IsOppositeReference (toolingItem.Name)) {
               blockType = -3;
               comment += " Web Flange Hole - Opposite reference ";
            } else {
               blockType = 3;
               comment += " Web Flange Hole";
            }
         } else if (Utils.GetFlangeType (toolingItem, GetXForm ()) == Utils.EFlange.Bottom) {
            blockType = 1;
            comment += " Bottom Flange Cut";
         } else if (Utils.GetFlangeType (toolingItem, GetXForm ()) == Utils.EFlange.Top) {
            blockType = 2;
            comment += " Top Flange Cut";
         } else if (toolingItem.IsFlexHole () || !isValidNotch) {
            blockType = 0;
         } else throw new Exception ("GCodeGenerator.WriteBlockType() Unrecognized feature type in gcode generation");
         gcodeSt = $"BlockType={blockType:F0} ({comment})";

      } else if (isValidNotch) {
         switch (notchCutKind) {
            case ECutKind.Top:
               comment = "Web Flange Notch";
               if (isFlexCut) throw new Exception ("There is no flex cut for web flange cuts");
               gcodeSt = $"BlockType={5} ({comment})";
               break;

            case ECutKind.YPos:
               comment = "Top Flange Notch";
               blockType = mNotchSegBlockTypeTopFlange;

               if (!isFlexCut) {
                  if ((mNotchSegBlockTypeTopFlange - 0.1).EQ (-4.5))
                     mNotchSegBlockTypeTopFlange += -0.2;
                  else mNotchSegBlockTypeTopFlange += -0.1;
               }
               gcodeSt = $"BlockType={blockType:F1} ({comment})";
               break;

            case ECutKind.YNeg:
               if (isValidNotch) comment = "Bottom Flange Notch";
               blockType = mNotchSegBlockTypeBottomFlange;
               if (isFlexCut) blockType = 4.5;
               gcodeSt = $"BlockType={blockType:F1} ({comment})";
               break;

            case ECutKind.Top2YPos: // Web -> Top Flange Notch/Cutout
               if (isValidNotch) comment = "Web -> Top Flange Notch";
               else if (isCutout) comment = "Web -> Top Flange Cutout";
               blockType = mNotchSegBlockTypeWeb2TopFlange;
               if (isFlexCut) {
                  blockType = -5.5;
               } else {
                  if ((mNotchSegBlockTypeWeb2TopFlange - 0.1).EQ (-5.5))
                     mNotchSegBlockTypeWeb2TopFlange += -0.2;
                  else mNotchSegBlockTypeWeb2TopFlange += -0.1;
               }
               gcodeSt = $"BlockType={blockType:F1} ({comment})";
               break;

            case ECutKind.Top2YNeg:
               comment = "Web -> Bottom Flange Notch";
               blockType = mNotchSegBlockTypeWeb2BottomFlange;
               if (isFlexCut) blockType = 5.5;
               else {
                  if ((mNotchSegBlockTypeWeb2BottomFlange + 0.1).EQ (5.5))
                     mNotchSegBlockTypeWeb2BottomFlange += 0.2;
                  else mNotchSegBlockTypeWeb2BottomFlange += 0.1;
               }
               gcodeSt = $"BlockType={blockType:F1} ({comment})";
               break;

            case ECutKind.YNegToYPos:
               throw new Exception ("Bottom to Top flange notches/cutouts not yet supported");
            default:
               break;
         }
      } else if (isCutout) {
         switch (cutoutKind) {
            case ECutKind.Top2YPos: // Web -> Top Flange Notch/Cutout
               comment = "Web -> Top Flange Cutout";
               blockType = mNotchSegBlockTypeWeb2TopFlange;
               if (isFlexCut) blockType = -5.5;
               else {
                  if ((mNotchSegBlockTypeWeb2TopFlange - 0.1).EQ (-5.5))
                     mNotchSegBlockTypeWeb2TopFlange += -0.2;
                  else mNotchSegBlockTypeWeb2TopFlange += -0.1;
               }
               gcodeSt = $"BlockType={blockType:F1} ({comment})";
               break;
            case ECutKind.Top2YNeg:
               comment = "Web -> Bottom Flange Cutout";
               blockType = mNotchSegBlockTypeWeb2BottomFlange;
               if (isFlexCut) blockType = 5.5;
               else {
                  if ((mNotchSegBlockTypeWeb2BottomFlange + 0.1).EQ (5.5))
                     mNotchSegBlockTypeWeb2BottomFlange += 0.2;
                  else mNotchSegBlockTypeWeb2BottomFlange += 0.1;
               }
               gcodeSt = $"BlockType={blockType:F1} ({comment})";
               break;
            case ECutKind.YNegToYPos:
               throw new Exception ("Bottom to Top flange notches/cutouts not yet supported");
            default:
               break;
         }
      }
      sw.WriteLine (gcodeSt);
   }
   /// <summary>Resets the GCodeGenerator state to a known default, for testing</summary>
   /// There is a lot of state in the GCodeGenerator, like program numbers that
   /// keep incrementing forward. We need to reset all this state to some known defaults
   /// so that tests can be run. Otherwise, the tests become sequence dependent and if we 
   /// add or remove additional tests in the sequence, the program numbers for all subsequent
   /// parts will be incorrect, leading to spurious test failures
   public void ResetForTesting (MCSettings mcs) {
      ResetBookKeepers ();
      Standoff = mcs.Standoff;
      ApproachLength = mcs.ApproachLength;
      UsePingPong = mcs.UsePingPong;
      NotchApproachLength = mcs.NotchApproachLength;
      NotchWireJointDistance = mcs.NotchWireJointDistance;
      MarkText = mcs.MarkText;
      MarkTextHeight = mcs.MarkTextHeight;
      MarkTextPosX = mcs.MarkTextPosX;
      MarkTextPosY = mcs.MarkTextPosY;
      PartConfigType = mcs.PartConfig;
      SerialNumber = mcs.SerialNumber;
      PartitionRatio = mcs.PartitionRatio;
      Heads = mcs.Heads;
      ProgNo = mcs.ProgNo;
      MarkAngle = mcs.MarkAngle;
      ToolingPriority = mcs.ToolingPriority;
      OptimizePartition = mcs.OptimizePartition;
      OptimizeSequence = mcs.OptimizeSequence;
      SafetyZone = mcs.SafetyZone;
      EnableMultipassCut = mcs.EnableMultipassCut;
      MaxFrameLength = mcs.MaxFrameLength;
      MaximizeFrameLengthInMultipass = mcs.MaximizeFrameLengthInMultipass;
      CutHoles = mcs.CutHoles;
      CutNotches = mcs.CutNotches;
      Cutouts = mcs.CutCutouts;
      CutMarks = mcs.CutMarks;
      Machine = mcs.Machine;
      MinThresholdForPartition = mcs.MinThresholdForPartition;
      MinNotchLengthThreshold = mcs.MinNotchLengthThreshold;
      DinFilenameSuffix = mcs.DinFilenameSuffix;
      FxFilePath = mcs.NCFilePath;
      DINFileNameHead1 = "";
      DINFileNameHead2 = "";
      WorkpieceOptionsFilename = mcs.WorkpieceOptionsFilename;
      DeadbandWidth = mcs.DeadbandWidth;

      // Following ovverriders
      if (Heads == EHeads.Left || Heads == EHeads.Right) PartitionRatio = 1.0;
      LeftToRightMachining = true;
      ResetNotchCutoutBlockTypes ();
   }
   public void ResetBookKeepers () {
      mPgmNo[Utils.EFlange.Web] = 3000;
      mPgmNo[Utils.EFlange.Top] = 2000;
      mPgmNo[Utils.EFlange.Bottom] = 1000;
      mContourProgNo = ContourProgNo;
      mNotchProgNo = NotchProgNo;
      mMarkProgNo = MarkProgNo;
      mHashProgNo.Clear ();
      ResetNotchCutoutBlockTypes ();
      mLastCutScope = false;
   }
   #endregion

   #region MCSettings Properties
   public double Standoff { get; set; }
   public double ApproachLength { get; set; }
   public bool UsePingPong { get; set; }
   public double MarkTextPosX { get; set; }
   public double MarkTextPosY { get; set; }
   public MCSettings.PartConfigType PartConfigType { get; set; }
   public string MarkText { get; set; }
   public int MarkTextHeight { get; set; }
   public uint SerialNumber { get; set; }
   public double PartitionRatio { get; set; }
   public MCSettings.EHeads Heads { get; set; }
   public int ProgNo { get; set; }
   public ERotate MarkAngle { get; set; }
   public bool OptimizeSequence { get; set; }
   public bool OptimizePartition { get; set; }
   public double SafetyZone { get; set; }
   public EKind[] ToolingPriority { get; set; }
   public double NotchWireJointDistance { get; set; } = 2.0;
   public double NotchApproachLength { get; set; } = 5.0;
   public bool Cutouts { get; set; } = true;
   public bool CutNotches { get; set; } = true;
   public bool CutMarks { get; set; } = true;
   public bool CutHoles { get; set; } = true;
   public bool EnableMultipassCut { get; set; }
   public double MaxFrameLength { get; set; }
   public bool MaximizeFrameLengthInMultipass { get; set; }
   public bool LeftToRightMachining { get; set; }
   public double MinThresholdForPartition { get; set; }
   public double MinNotchLengthThreshold { get; set; }
   public string DinFilenameSuffix { get; set; }
   public MachineType Machine { get; set; }
   public bool IsDryRun { get; set; }
   public string FxFilePath { get; set; }
   public string DINFileNameHead1 { get; set; }
   public string DINFileNameHead2 { get; set; }
   public string WorkpieceOptionsFilename { get; set; }
   public Dictionary<string, WorkpieceOptions> WPOptions { get; set; }
   public double DeadbandWidth { get; set; }
   #endregion

   #region GCode BookKeepers
   readonly HashSet<int> mHashProgNo = [];
   bool mWebFlangeOnly = false;
   const int WebCCNo = 3, FlangeCCNo = 2;
   int mContourProgNo = ContourProgNo;
   int mNotchProgNo = NotchProgNo;
   int mMarkProgNo = MarkProgNo;
   const int ContourProgNo = 5000;
   const int NotchProgNo = 4000;
   const int MarkProgNo = 8000;
   const int DigitProg = 6000, DigitConst = 1000, DigitPitch = 7;

   double mNotchSegBlockTypeTopFlange = -4.0, mNotchSegBlockTypeBottomFlange = 4.0,
      mNotchSegBlockTypeWeb2TopFlange = -5.0, mNotchSegBlockTypeWeb2BottomFlange = 5.0;

   // As we are outputting two head and we need to maintain program number
   // save program number in a dictionary so that it can be used while writing
   // cutting head 2 program number
   readonly Dictionary<Utils.EFlange, int> mPgmNo = new () {
      [Utils.EFlange.Web] = 3000,
      [Utils.EFlange.Top] = 2000,
      [Utils.EFlange.Bottom] = 1000
   };
   int mBaseBlockNo = 10000;
   int mNo = 0;
   int GetNotchProgNo () => mNotchProgNo;
   int GetStartMarkProgNo () => mMarkProgNo;
   int GetProgNo (Tooling item) {
      if (OptimizeSequence) return mNo++;
      if (item.IsNotch ()) return mNotchProgNo++;
      else if (item.IsCutout () || item.IsFlexHole ()) return mContourProgNo++;
      else if (item.IsMark ()) return mMarkProgNo++;
      else return mPgmNo[Utils.GetFlangeType (item, GetXForm ())];
   }
   void OutN (StreamWriter sw, int progNo, string comment = "") {
      if (mHashProgNo.Add (progNo)) {
         int nNo;
         if (EnableMultipassCut) nNo = mCutScopeNo * mBaseBlockNo + progNo;
         else nNo = progNo;
         sw.WriteLine ($"N{nNo}{(string.IsNullOrEmpty (comment) ? "" : $"\t( {comment} )")}");
         sw.WriteLine ($"BlockID={nNo}");
      } else throw new InvalidOperationException ($"Program number {progNo} is repeated");
   }
   #endregion

   #region GCode Options
   const double Rapid = 8000;

   bool IsSingleHead => !OptimizePartition && (PartitionRatio.EQ (0.0) ||
                          PartitionRatio.EQ (1.0));
   bool IsSingleHead1 => IsSingleHead && PartitionRatio.EQ (1.0);

   readonly double mSafeClearance = 28.0;
   readonly double mRetractClearance = 20.0;
   readonly double[] mControlDiameter = [14.7];
   #endregion

   #region Partition Data members
   /// <summary>The X-partition location</summary>
   double mXSplit;
   #endregion

   #region Tool Configuration data
   int mToolHead = 0;
   public int ToolHead { get => mToolHead; set => mToolHead = value; }
   static XForm4 mXformLHInv;
   static XForm4 mXformRHInv;
   public static XForm4 LHCSys {
      get {
         mXformLHInv ??= new ();
         var csys = mXformLHInv.InvertNew ();
         //Matrix3 coorsysM3 = new Matrix3 (csys[0, 0], csys[0, 1], csys[0, 2], csys[0, 3],
         //   csys[1, 0], csys[1, 1], csys[1, 2], csys[1, 3],
         //   csys[2, 0], csys[2, 1], csys[2, 2], csys[2, 3],
         //   csys[3, 0], csys[3, 1], csys[3, 2], csys[3, 3]);
         //return coorsysM3;
         return csys;
      }
   }
   public static XForm4 RHCSys {
      get {
         mXformLHInv ??= new ();
         var csys = mXformRHInv.InvertNew ();
         //Matrix3 coorsysM3 = new Matrix3 (csys[0, 0], csys[0, 1], csys[0, 2], csys[0, 3],
         //   csys[1, 0], csys[1, 1], csys[1, 2], csys[1, 3],
         //   csys[2, 0], csys[2, 1], csys[2, 2], csys[2, 3],
         //   csys[3, 0], csys[3, 1], csys[3, 2], csys[3, 3]);
         //return coorsysM3;
         return csys;
      }
   }
   #endregion

   #region Enums and Types
   enum EToolingShape {
      /// <summary>Circle hole</summary>
      Circle,
      HoleShape,
      NotchStart,
      NotchGiveWay,
      Notch,
      Cutout,
      /// <summary>Left segment of notch</summary>
      NotchL,
      NotchL2,
      /// <summary>Right segment of notch</summary>
      NotchR,
      NotchR2,
      Text,
      // If there is no hole in a flange, add this dummy
      HoleSubstituteLine,
      CutOutStart,
      CutOutStart2,
      CutOutYNeg,
      CutOutYPos,
      CutOutEnd,
      // Used to bring cutting head to top
      SerialStartNo,
      Arc,
      Others,
   }
   #endregion

   #region Constructors and constructing utilities
   public GCodeGenerator (Processor process, bool isLeftToRight) {
      mProcess = process;
      SetFromMCSettings ();
      MCSettings.It.OnSettingValuesChangedEvent += SetFromMCSettings;
      mTraces = [[], []];
      LeftToRightMachining = isLeftToRight;
      DinFilenameSuffix = "";

      //Point3 ps = new (39.33026, -23.696316, 6);
      //Point3 pe = new (41.124299, -26.224453, 6);
      //Point3 ip1 = new (39.97098746, -24.50688028, 6);
      //Point3 ip2 = new (40.56899935, -25.34945249, 6);
      //Arc3 arc = new (ps, ip1, ip2, pe);
      //var p1 = arc.Evaluate (0.3);
      //var p2 = arc.Evaluate (0.7);
      //var p11 = Geom.EvaluateArc (arc, 0.3, new Vector3 (0, 0, 1));
      //var p12 = Geom.EvaluateArc (arc, 0.7, new Vector3 (0, 0, 1));
   }

   /// <summary>
   /// The following method sets the properties that are local to the 
   /// GCodeGenerator. Note: Any changes to the global MCSettings properties
   /// will trigger the following method, as MCSettings.It.OnSettingValuesChangedEvent 
   /// is subscribed with SetFromMCSettings;
   /// </summary>
   public void SetFromMCSettings () {
      Standoff = MCSettings.It.Standoff;
      ApproachLength = MCSettings.It.ApproachLength;
      UsePingPong = MCSettings.It.UsePingPong;
      NotchApproachLength = MCSettings.It.NotchApproachLength;
      NotchWireJointDistance = MCSettings.It.NotchWireJointDistance;
      MarkText = MCSettings.It.MarkText;
      MarkTextHeight = MCSettings.It.MarkTextHeight;
      MarkTextPosX = MCSettings.It.MarkTextPosX;
      MarkTextPosY = MCSettings.It.MarkTextPosY;
      PartConfigType = MCSettings.It.PartConfig;
      SerialNumber = MCSettings.It.SerialNumber;
      PartitionRatio = MCSettings.It.PartitionRatio;
      Heads = MCSettings.It.Heads;
      ProgNo = MCSettings.It.ProgNo;
      MarkAngle = MCSettings.It.MarkAngle;
      ToolingPriority = MCSettings.It.ToolingPriority;
      OptimizePartition = MCSettings.It.OptimizePartition;
      OptimizeSequence = MCSettings.It.OptimizeSequence;
      SafetyZone = MCSettings.It.SafetyZone;
      EnableMultipassCut = MCSettings.It.EnableMultipassCut;
      MaxFrameLength = MCSettings.It.MaxFrameLength;
      MaximizeFrameLengthInMultipass = MCSettings.It.MaximizeFrameLengthInMultipass;
      CutHoles = MCSettings.It.CutHoles;
      CutNotches = MCSettings.It.CutNotches;
      Cutouts = MCSettings.It.CutCutouts;
      CutMarks = MCSettings.It.CutMarks;
      Machine = MCSettings.It.Machine;
      MinThresholdForPartition = MCSettings.It.MinThresholdForPartition;
      MinNotchLengthThreshold = MCSettings.It.MinNotchLengthThreshold;
      DinFilenameSuffix = MCSettings.It.DinFilenameSuffix;
      FxFilePath = MCSettings.It.NCFilePath;
      DINFileNameHead1 = "";
      DINFileNameHead2 = "";
      WorkpieceOptionsFilename = MCSettings.It.WorkpieceOptionsFilename;
      DeadbandWidth = MCSettings.It.DeadbandWidth;

      // Following ovverriders
      if (Heads == EHeads.Left || Heads == EHeads.Right) PartitionRatio = 1.0;
   }
   GCodeGenerator () { }
   public void OnNewWorkpiece () {
      if (Process != null && Process.Workpiece != null && Process.Workpiece.Model != null) {
         mFlexes = Process.Workpiece.Model.Flexes.ToList ();
         mPlanes = Process.Workpiece.Model.Entities.OfType<E3Plane> ().ToList ();
         mThickness = mPlanes[0].ThickVector.Length;
         mWebFlangeOnly = mFlexes.Count == 0;
         NCName = Process.Workpiece.NCFileName;
      }
   }
   #endregion

   #region Utilies for Tool Transformations
   public static void EvaluateToolConfigXForms (GCodeGenerator gCodeGen, Bound3 bound) {
      // Evaluate XForms wrt to the machine
      mXformLHInv = new XForm4 ();
      mXformRHInv = new XForm4 ();

      // The following lines of code are commented and it will continue to be upto the point
      // all the customer parts are tested. The machine calibration needs to test with various
      // transforms, hence the following lines are commented

      //if ((gCodeGen != null && gCodeGen.EnableMultipassCut) || MCSettings.It.EnableMultipassCut) {
      //   // For LH Component
      //   mXformLHInv.Translate (new Vector3 (bound.XMax, bound.YMax, 0.0));
      //   // For RH component
      //   mXformRHInv.Translate (new Vector3 (bound.XMax, bound.YMin, 0.0));
      //} else {
      //   // For LH Component
      //   mXformLHInv.Translate (new Vector3 (0.0, bound.YMin, 0.0));
      //   // For RH component
      //   mXformRHInv.Translate (new Vector3 (0.0, bound.YMax, 0.0));
      //}

      // For LH Component
      if ((gCodeGen != null && gCodeGen.LeftToRightMachining) || (gCodeGen == null && SettingServices.It.LeftToRightMachining)) {
         mXformLHInv.Translate (new Vector3 (0.0, bound.YMin, 0.0));
         //if (mcName == "LMMultipass2H")
         //mXformLHInv.SetRotationComponents (new Vector3 (-1, 0, 0), new Vector3 (0, -1, 0), new Vector3 (0, 0, 1));
         mXformLHInv.Invert ();
         // For RH component
         mXformRHInv.Translate (new Vector3 (0.0, bound.YMax, 0.0));
         //if (mcName == "LMMultipass2H")
         //mXformRHInv.SetRotationComponents (new Vector3 (-1, 0, 0), new Vector3 (0, -1, 0), new Vector3 (0, 0, 1));
         mXformRHInv.Invert ();
      } else {
         mXformLHInv.Translate (new Vector3 (bound.XMax, bound.YMax, 0.0));
         //if (mcName == "LMMultipass2H")
         // mXformLHInv.SetRotationComponents (new Vector3 (-1, 0, 0), new Vector3 (0, -1, 0), new Vector3 (0, 0, 1));
         mXformLHInv.Invert ();
         // For RH component
         mXformRHInv.Translate (new Vector3 (bound.XMax, bound.YMin, 0.0));
         //if (mcName == "LMMultipass2H")
         //mXformRHInv.SetRotationComponents (new Vector3 (-1, 0, 0), new Vector3 (0, -1, 0), new Vector3 (0, 0, 1));
         mXformRHInv.Invert ();
      }
   }

   public static Point3 XfmToMachine (GCodeGenerator codeGen, Point3 ptWRTWCS) {
      Vector3 resVec;
      if (codeGen.PartConfigType == MCSettings.PartConfigType.LHComponent) resVec = mXformLHInv * ptWRTWCS;
      else resVec = mXformRHInv * ptWRTWCS;
      return Geom.V2P (resVec);
   }

   public Point3 XfmToMachine (Point3 ptWRTWCS) {
      Vector3 resVec;
      if (PartConfigType == MCSettings.PartConfigType.LHComponent) resVec = mXformLHInv * ptWRTWCS;
      else resVec = mXformRHInv * ptWRTWCS;
      return Geom.V2P (resVec);
   }

   public static XForm4 XfmToMachine (GCodeGenerator codeGen, XForm4 xFormWCS) {
      XForm4 mcXForm;
      if (codeGen.PartConfigType == MCSettings.PartConfigType.LHComponent) mcXForm = mXformLHInv * xFormWCS;
      else mcXForm = mXformRHInv * xFormWCS;
      return mcXForm;
   }

   public XForm4 XfmToMachine (XForm4 xFormWCS) {
      XForm4 mcXForm;
      if (PartConfigType == MCSettings.PartConfigType.LHComponent) mcXForm = mXformLHInv * xFormWCS;
      else mcXForm = mXformRHInv * xFormWCS;
      return mcXForm;
   }

   public static Vector3 XfmToMachineVec (GCodeGenerator codeGen, Vector3 vecWRTWCS) {
      Vector3 resVec;
      if (codeGen.PartConfigType == MCSettings.PartConfigType.LHComponent) resVec = mXformLHInv * vecWRTWCS;
      else resVec = mXformRHInv * vecWRTWCS;
      return resVec;
   }

   public Vector3 XfmToMachineVec (Vector3 vecWRTWCS) {
      Vector3 resVec;
      if (PartConfigType == MCSettings.PartConfigType.LHComponent) resVec = mXformLHInv * vecWRTWCS;
      else resVec = mXformRHInv * vecWRTWCS;
      return resVec;
   }
   #endregion

   #region Tooling filters (FChassisMachineSettings.Settings)
   Utils.EFlange[] flangeCutPriority = [Utils.EFlange.Bottom, Utils.EFlange.Top, Utils.EFlange.Web, Utils.EFlange.Flex];

   public double GetXPartition (List<Tooling> cuts) {
      List<Tooling> resHead0 = [], resHead1 = [];
      if (!LeftToRightMachining) {
         resHead0 = [..cuts.Where (cut => cut.Head == 0)
      .OrderByDescending (cut => cut.Start.Pt.X)];
         resHead1 = [..cuts.Where (cut => cut.Head == 1)
      .OrderByDescending (cut => cut.Start.Pt.X)];
      } else {
         resHead0 = [..cuts.Where (cut => cut.Head == 0)
      .OrderBy (cut => cut.Start.Pt.X)];
         resHead1 = [..cuts.Where (cut => cut.Head == 1)
      .OrderBy (cut => cut.Start.Pt.X)];
      }
      double midX = -10;
      if (resHead0.Count > 0 && resHead1.Count > 0) {
         if (LeftToRightMachining) midX = (resHead0.Last ().XMax + resHead1.First ().XMin) / 2.0;
         else midX = (resHead0.Last ().XMin + resHead1.First ().XMax) / 2.0;
      } else if (resHead0.Count > 0) {
         if (LeftToRightMachining) midX = resHead0.Last ().XMax;
         else midX = resHead0.Last ().XMin;
      } else if (resHead1.Count > 0) {
         if (LeftToRightMachining) midX = resHead1.Last ().XMax;
         else midX = resHead1.Last ().XMin;
      }
      return midX;
   }

   public List<Tooling> GetToolings4Head (List<Tooling> cuts, int headNo) {
      List<Tooling> res;
      if (!LeftToRightMachining)
         res = [..cuts.Where (cut => cut.Head == headNo)
      .OrderBy (cut => Array.IndexOf (flangeCutPriority, Utils.GetFlangeType (cut,PartConfigType==PartConfigType.LHComponent?mXformLHInv:mXformRHInv)))
      .ThenBy (cut => MCSettings.It.ToolingPriority.IndexOf (cut.Kind))
      .ThenByDescending (cut => cut.Start.Pt.X)];
      else
         res = [..cuts.Where (cut => cut.Head == headNo)
      .OrderBy (cut => Array.IndexOf (flangeCutPriority, Utils.GetFlangeType (cut,PartConfigType==PartConfigType.LHComponent?mXformLHInv:mXformRHInv)))
      .ThenBy (cut => MCSettings.It.ToolingPriority.IndexOf (cut.Kind))
      .ThenBy (cut => cut.Start.Pt.X)];
      return res;
   }

   public List<Tooling> GetToolings4Head1 (List<Tooling> cuts) {
      if (EnableMultipassCut)
         return [..cuts.Where (cut => cut.Head == 1)
      .OrderBy (cut => MCSettings.It.ToolingPriority.IndexOf (cut.Kind))
      .ThenBy (cut => cut.IsHole () ? Array.IndexOf (flangeCutPriority, Utils.GetFlangeType (cut,
      PartConfigType==PartConfigType.LHComponent?mXformLHInv:mXformRHInv)) : 0)
      .ThenByDescending (cut => cut.Start.Pt.X)];
      else
         return [..cuts.Where (cut => cut.Head == 1)
      .OrderBy (cut => MCSettings.It.ToolingPriority.IndexOf (cut.Kind))
      .ThenBy (cut => cut.IsHole () ? Array.IndexOf (flangeCutPriority, Utils.GetFlangeType (cut,
      PartConfigType==PartConfigType.LHComponent?mXformLHInv:mXformRHInv)) : 0)
      .ThenBy (cut => cut.Start.Pt.X)];
   }
   #endregion

   #region Partition Implementation 

   public static void CreatePartition (GCodeGenerator gcGen, List<ToolingScope> tss, bool optimize, Bound3 bound) {
      var toolings = tss.Select (ts => ts.Tooling).ToList ();
      gcGen.CreatePartition (toolings, optimize, bound);
   }

   /// <summary>This creates the optimal partition of holes so both heads are equally busy</summary>
   public void CreatePartition (List<Tooling> cuts, bool optimize, Bound3 bound) {
      double min = 0.1, max = 0.9, mid = 0;
      int count = 15;
      if (!optimize) {
         count = 1;
         min = max = PartitionRatio;
      }
      for (int i = 0; i < count; i++) {
         mid = (min + max) / 2;
         Partition (cuts, mid, bound);
         GetTimes (cuts, out double t0, out double t1);
         if (t0 > t1) max = mid; else min = mid;
      }
      mXSplit = bound.XMin + ((bound.XMax - bound.XMin) * mid);
      //mXSplit = Math.Round (mid * bound.XMax, 0);
      mXSplit = Math.Round (mXSplit, 0);

      if (Machine == MachineType.LCMMultipass2H && bound.XMax - mXSplit < MinThresholdForPartition) {
         for (int ii = 0; ii < cuts.Count; ii++) {
            var cut = cuts[ii];
            cut.Head = 0;
            cuts[ii] = cut;
         }
      }
   }

   /// <summary>This partitions the cuts with a given ratio, and sorts them</summary>
   void Partition (List<Tooling> cuts, double ratio, Bound3 bound) {
      //double xf = Math.Round (bound.XMax * ratio, 1);
      double xPartVal = bound.XMin + ((bound.XMax - bound.XMin) * ratio);
      double xf = Math.Round (xPartVal, 1);
      foreach (Tooling tooling in cuts) {
         if (IsSingleHead) {
            if (IsSingleHead1) {
               if (Heads is MCSettings.EHeads.Left or MCSettings.EHeads.Both)
                  tooling.Head = 0;
               else if (Heads is MCSettings.EHeads.Right or MCSettings.EHeads.Both)
                  tooling.Head = 1;
               else throw new InvalidOperationException
                     ("Only single head detected while the tool configuration with Both tools prescribed!");
            }
         } else {
            if (tooling.End.Pt.X < xf)
               tooling.Head = 0;
            else
               tooling.Head = 1;
         }
      }
   }

   void GetTimes (List<Tooling> cuts, out double t0, out double t1) {
      t0 = t1 = 0;
      double tpierce = mThickness < 7 ? 0.1 : 0.3;

      for (int i = 0; i <= 1; i++) {
         double t = 0, fr = 1.8;
         if (mThickness < 7) fr = i == 0 ? 1 : 1.3;
         else fr = i == 0 ? 1.3 : 1;
         fr *= 1000 / 60.0;    // Convert to mm/sec

         cuts = cuts.Where (a => a.Head == i).ToList ();
         if (cuts.Count == 0) {
            if (i == 0) t0 = 0;
            else t1 = 0;
            continue;
         }
         var pt = cuts[0].Start.Pt;
         double rrx = 1000, rry = 500;
         foreach (var cut in cuts) {
            // First, find the traverse path to this location, and add the
            // traverse time (simplified)
            var vec = cut.Start.Pt - pt;
            double tx = Math.Abs (vec.X) / rrx, ty = Math.Abs (vec.Y) / rry;
            t += Math.Max (tx, ty);

            // Then, add the pierce time
            t += 0.5;

            // Then, add the cutting time (accurate)
            t += cut.Perimeter / fr;
            pt = cut.End.Pt;
         }
         if (i == 0) t0 = t;
         else t1 = t;
      }
   }
   #endregion

   #region GCode Generation Methods
   /// <summary>
   /// This method writes M14 directive in G Code if a previous
   /// immediate M14 is not found and/or M15 is found
   /// Note: This M14 directive intimates the machine controller
   /// to start the machining process
   /// </summary>
   public void EnableMachiningDirective () {
      if (!mMachiningDirectiveSet)
         sw.WriteLine ("M14\t( ** Start of Cut **)");
      mMachiningDirectiveSet = true;
   }

   /// <summary>
   /// This method writes M15 directive in G Code if a 
   /// previous M14 is found.
   /// Note: The M15 directive intimates the machine controller
   /// to stop the machining process.
   /// </summary>
   public void DisableMachiningDirective () {
      if (mMachiningDirectiveSet)
         sw.WriteLine ("M15\t( ** End Of Cut ** )");
      mMachiningDirectiveSet = false;
   }

   public int GenerateGCode (int head) {
      MachinableCutScope mccss = new (Process.Workpiece.Cuts, this);
      if (CutScopeTraces.Count == 0) AllocateCutScopeTraces (1);
      return GenerateGCode (head, [mccss]);
   }

   public void AllocateCutScopeTraces (int nCutScopes) {
      mCutScopeTraces = [];
      for (int i = 0; i < nCutScopes; i++) {
         // Create a new List<GCodeSeg>[] to hold the GCodeSeg lists
         List<GCodeSeg>[] newCutScope = [[], []]; // Adjust the size based on your needs

         // Add the new array to mCutScopeTraces
         mCutScopeTraces.Add (newCutScope);
      }
   }


   public void WriteNSequenceHeader (Utils.EFlange p, int np, int cnt, StreamWriter sw) {
      int startValue;
      int endValue;
      if (!OptimizeSequence)
         startValue = mPgmNo[p] + (Machine == MachineType.LCMMultipass2H ? mBaseBlockNo : 0);
      else
         startValue = np + (Machine == MachineType.LCMMultipass2H ? mBaseBlockNo : 0);

      if (!OptimizeSequence)
         endValue = mPgmNo[p] + (Machine == MachineType.LCMMultipass2H ? mBaseBlockNo : 0);
      else
         endValue = np + (Machine == MachineType.LCMMultipass2H ? mBaseBlockNo : 0);
      string line = $"(N{startValue + 1} to N{cnt + endValue} in {p} flange)";
      sw.WriteLine (line);
   }
   /// <summary>
   /// This method is the entry point for writing G Code both for LEGACY and 
   /// LCMMultipass2H machines. 
   /// <list type="bullet">
   ///   <item>
   ///     <description>0: Head - 1</description>
   ///   </item>
   ///   <item>
   ///     <description>1: Head - 2</description>
   ///   </item>
   /// </list>
   /// </summary>
   /// <param name="head">Cutting head identified by</param>
   /// <param name="cutScopeToolings">The cut scopes to be processed</param>
   /// <returns>The generated G Code</returns>
   /// <exception cref="Exception">Throws an exception if an error occurs during G Code generation</exception>
   public int GenerateGCode (int head, List<MachinableCutScope> mcCutScopes) {
      ToolHead = head;
      string headPos = $"{ToolHead + 1}";
      string dinFileSuffix = string.IsNullOrEmpty (DinFilenameSuffix) ? "" : $"-{DinFilenameSuffix}-";
      string ncName = Process.Workpiece.NCFileName + "-" + headPos + dinFileSuffix +
         $"({(PartConfigType == MCSettings.PartConfigType.LHComponent ? "LH" : "RH")}).din";
      string ncFolder;

      if (head == 0) {
         //ncFolder = Path.Combine (Process.Workpiece.NCFilePath, "Head1");
         ncFolder = Path.Combine (FxFilePath, "Head1");
         Directory.CreateDirectory (ncFolder);
         DINFileNameHead1 = Path.Combine (ncFolder, ncName);
      } else {
         ncFolder = Path.Combine (FxFilePath, "Head2");
         Directory.CreateDirectory (ncFolder);
         DINFileNameHead2 = Path.Combine (ncFolder, ncName);
      }
      using (sw = new StreamWriter (head == 0 ? DINFileNameHead1 : DINFileNameHead2)) {
         sw.WriteLine ("%{1}({0})", ncName, ProgNo);
         sw.WriteLine ("N1");
         sw.WriteLine ($"CNC_ID={ToolHead + 1}");
         sw.WriteLine ($"Job_Length = {Math.Round (Process.Workpiece.Model.Bound.XMax, 1)}");
         sw.WriteLine ($"Job_Width = {Math.Round (Process.Workpiece.Model.Bound.YMax - Process.Workpiece.Model.Bound.YMin, 1)}");
         sw.WriteLine ("Job_Height = {0}\r\nJob_Thickness = {1}", Math.Round (Process.Workpiece.Model.Bound.ZMax - Process.Workpiece.Model.Bound.ZMin, 1), Math.Round (mThickness, 1));
         sw.WriteLine ($"X_Partition = {mcCutScopes.Sum (cs => cs.ToolingScopesWidthH1)}");
         // Output the outer radius
         if (!mWebFlangeOnly) sw.WriteLine ($"Job_O_Radius = {Math.Round (Process.Workpiece.Model.Flexes.First ().Radius + Process.Workpiece.Model.Flexes.First ().Thickness, 1)}");
         // Output the lh or rh component
         sw.WriteLine ($"Job_Type  = {(PartConfigType == MCSettings.PartConfigType.LHComponent ? 1 : 2)}");
         if (!string.IsNullOrEmpty (MarkText)) {
            sw.WriteLine ($"Marking_X_Pos = {Math.Round (MarkTextPosX, 1)}");
            sw.WriteLine ($"Marking_Y_Pos = {Math.Round (MarkTextPosY, 1)}");
            int textRotAngle = MarkAngle switch {
               ERotate.Rotate0 => 0,
               ERotate.Rotate90 => 90,
               ERotate.Rotate180 => 180,
               ERotate.Rotate270 => 270,
               _ => 0
            };
            sw.WriteLine ($"Marking_Angle = {textRotAngle}");
            sw.WriteLine ($"Marking_Height = {MarkTextHeight}");
            sw.WriteLine ($"G253 F=\"ModelTag:{MarkText}\" E0");
         }
         sw.WriteLine ("(BF-Soffset:S1, TF-Soffset:S2, WEB_BF-Soffset:S3, WEB_TF-Soffset:S4, Marking:S3)\r\n(Block No - BF:N1001~N1999, TF:N2001~N2999, WEB:N3001~N3999, Notch:N4001~N4999, " +
            "CutOut:N5001~N5999)" +
            "\r\n(BlockType - 0:Flange Holes, 1:Web Block with BF reference, -1:Web Block with TF reference, 2:Notch, 3:Cutout, 4:Marking)" +
            "\r\n(PM:Pierce Method, CM:Cutting Method, EM:End Method, ZRH: Z/Y Retract Height)\r\n(M50 - Sync On command, only in Tandem job Programs)\r\n(Job_TYPE - 1:LH JOB, 2:RH_JOB)\r\n" +
            "(X_Correction & YZ_Correction Limit +/-5mm)");
         sw.WriteLine ($"(Multipass = {EnableMultipassCut && MultiPassCuts.IsMultipassCutTask (Process.Workpiece.Model)}) ");
         sw.WriteLine ("\r\n(---Don't alter above Parameters---)\r\n");

         if (Heads == EHeads.Both) sw.WriteLine ("M50");
         sw.WriteLine ("M15");
         sw.WriteLine ("H=LaserTableID");
         sw.WriteLine ("G61\t( Stop Block Preparation )\r\nG40 E0");
         sw.WriteLine ($"( Cutting with head {ToolHead + 1} )");
         sw.WriteLine ($"G20 X=BlockID\r\n");
         // ****************************************************************************
         // Logic to change scope goes here.
         // 0. Create partition for multipass tooling ( sets head 0 or 1 )
         // 1. Get all the toolings within a scope
         // 2. Pass them into the following methods
         //*****************************************************************************

         // Instead of getting one cuts for head0 and head1, in the case of MULTIPASS we should get
         // List<cuts> for head0 and another list<cuts> for head1

         List<Tooling> totalCuts = [];
         double xStart = 0;
         double xEnd = 0;
         mCutScopeNo = 0; int cnnt = 0;
         mLastCutScope = false;
         for (int mm = 0; mm < mcCutScopes.Count; mm++) {
            var mcCutScope = mcCutScopes[mm];
            cnnt++;

            if (head == 0 && mm == mcCutScopes.Count - 2 && mcCutScopes[mm + 1].ToolingScopesH1.Count == 0) mLastCutScope = true;
            else if (head == 1 && mm == mcCutScopes.Count - 2 && mcCutScopes[mm + 1].ToolingScopesH2.Count == 0) mLastCutScope = true;
            else if (cnnt == mcCutScopes.Count) mLastCutScope = true;


            Bound3 cutScopeBound = Utils.CalculateBound3 (mcCutScope.Toolings);
            xStart = mcCutScope.StartX; xEnd = mcCutScope.EndX;
            if ((xEnd - xStart).SGT (MaxFrameLength)) throw new Exception ($"The Cut scope length is greater than Max Frame Length:{MaxFrameLength}, " +
               $"for the Cut Scope index {cnnt} starting from Tooling {mcCutScope.Toolings.First ().Name}");
            mCutScopeNo++;
            mToolPos[0] = new Point3 (cutScopeBound.XMin, cutScopeBound.YMin, mSafeClearance);
            mToolPos[1] = new Point3 (cutScopeBound.XMax, cutScopeBound.YMax, mSafeClearance);
            mSafePoint[0] = new Point3 (cutScopeBound.XMin, cutScopeBound.YMin, 50);
            mSafePoint[1] = new Point3 (cutScopeBound.XMax, cutScopeBound.YMax, 50);
            EvaluateToolConfigXForms (this, Process.Workpiece.Model.Bound);
            List<Tooling> cuts = null;
            if (head == 0) cuts = GetToolings4Head (mcCutScope.ToolingsHead1, 0); else cuts = GetToolings4Head (mcCutScope.ToolingsHead2, 1);
            //bool iSingleHead = cuts.Count == mcCutScope.Toolings.Count;
            int np = 0;

            foreach (var name in Enum.GetNames (typeof (Utils.EFlange))) {
               Utils.EFlange p = (Utils.EFlange)Enum.Parse (typeof (Utils.EFlange), name);
               int cnt = cuts.Count (a => Utils.GetFlangeType (a, GetXForm ()) == p && !a.IsCutout () && !a.IsNotch ());
               if (cnt == 0) continue;
               if (p != Utils.EFlange.Flex)
                  WriteNSequenceHeader (p, np, cnt, sw);
               //sw.WriteLine ($"(N{(!OptimizeSequence ? mPgmNo[p] + (Machine == MachineType.LCMMultipass2H ? mBaseBlockNo : 0) : np + (Machine == MachineType.LCMMultipass2H ? mBaseBlockNo : 0)) + 1} to N{cnt + (!OptimizeSequence ? mPgmNo[p] + (Machine == MachineType.LCMMultipass2H ? mBaseBlockNo : 0) : np + (Machine == MachineType.LCMMultipass2H ? mBaseBlockNo : 0))} in {p} flange)");
               np += cnt;
            }

            // Now write notch information
            int cNotches = cuts.Count (a => a.Kind == EKind.Notch);
            string gcodeStr = "";
            if (cNotches != 0)
               gcodeStr = $"(N{GetNotchProgNo () + (Machine == MachineType.LCMMultipass2H ? mBaseBlockNo : 0) + 1} to N{GetNotchProgNo () + (Machine == MachineType.LCMMultipass2H ? mBaseBlockNo : 0) + cNotches} for notches)";
            if (!string.IsNullOrEmpty (gcodeStr)) sw.WriteLine (gcodeStr);
            // Now write Cutout part information
            int cMarks = cuts.Count (a => a.Kind == EKind.Mark);
            if (cMarks != 0) gcodeStr = $"(N{GetStartMarkProgNo () + 1} to N{GetStartMarkProgNo () + cMarks} for markings)";
            if (!string.IsNullOrEmpty (gcodeStr)) sw.WriteLine (gcodeStr);

            // GCode generation for all the eligible tooling starts here
            //----------------------------------------------------------
            xEnd = cutScopeBound.XMax;
            // Debug 
            foreach (var cut in cuts) {
               var cutBound = cut.Bound3;
               if (((double)cutBound.XMax).SGT (xEnd))
                  throw new Exception ("Tooling's XMax is more than frame freed");
            }
            // Compute the splitPartition
            var xPartition = GetXPartition (mcCutScope.Toolings);

            WriteCuts (cuts, cutScopeBound, xStart, xPartition, xEnd, false);
            //xStart = cutScopeBound.XMax;
            totalCuts.AddRange (cuts);
            //frameFeed += (cutScopeBound.XMax - cutScopeBound.XMin);

            // Update Traces for this cutscope

            if (CutScopeTraces[mCutScopeNo - 1][0].Count == 0)
               CutScopeTraces[mCutScopeNo - 1][0] = mTraces[0];
            if (CutScopeTraces[mCutScopeNo - 1][1].Count == 0)
               CutScopeTraces[mCutScopeNo - 1][1] = mTraces[1];
            mTraces = [[], []];
         }
         // Re init Traces with first entry of CutScopeTraces
         //mCutScopeNo = 0;
         sw.WriteLine ("\r\nN10000000");
         sw.WriteLine ("EndOfJob");
         sw.WriteLine ("G99");
         string headInfo = $"for Head{ToolHead + 1}";
         MachinableCutScopes = mcCutScopes;
         return totalCuts.Count;
      }
   }

   /// <summary>
   /// The following method writes the program number to intimate the 
   /// GCode controller to make appropriate actions before starting to
   /// machine the upcoming we, top or bottom flanges
   /// </summary>
   /// <param name="toolingItem">The input tooling item</param>
   /// <param name="number">Ther block number. </param>
   void SetProgNo (Tooling toolingItem, int number) {
      if (toolingItem.IsHole ()) mPgmNo[Utils.GetFlangeType (toolingItem, GetXForm ())] = number;
      else if (toolingItem.IsNotch ()) mNotchProgNo = number;
      else if (toolingItem.IsCutout () || toolingItem.IsFlexHole ()) mContourProgNo = number;
      else if (toolingItem.IsMark ()) mMarkProgNo = number;
   }

   public bool IgnoreSafety => SafetyZone.EQ (0);
   XForm4 GetXForm () => PartConfigType == PartConfigType.LHComponent ? mXformLHInv : mXformRHInv;
   public void WriteCurve (ToolingSegment segment, string toolingName) {
      var stNormal = segment.Vec0.Normalized ();
      var endNormal = segment.Vec1.Normalized ();
      WriteCurve (segment.Curve, stNormal, endNormal, toolingName);
   }

   /// <summary>
   /// This method generates G Code for machining a curve, which can either be a line or
   /// an arc.
   /// </summary>
   /// <param name="curve">Curve to machine</param>
   /// <param name="stNormal">Normal at the start of the curve</param>
   /// <param name="endNormal">Normal at the end of the curve</param>
   /// <param name="prevPlaneType">Utils.EPlane type of the previous segment</param>
   /// <param name="toolingName">Tooling name (for simulation data)</param>
   public void WriteCurve (Curve3 curve, Vector3 stNormal, Vector3 endNormal,
      string toolingName) {
      /* current normal plane type (at the end) */
      var currPlaneType = Utils.GetArcPlaneType (endNormal, XForm4.IdentityXfm);
      if (curve is Line3) {
         var endPoint = curve.End;/* end point*/
         WriteLine (endPoint, stNormal, endNormal, toolingName);
      } else if (curve is Arc3) {
         var (cen, _) = Geom.EvaluateCenterAndRadius (curve as Arc3);
         //currFlangeType = Utils.GetArcPlaneFlangeType (endNormal);
         WriteArc (curve as Arc3, currPlaneType,
            cen, curve.Start, curve.End, stNormal, toolingName);
      }
   }

   /// <summary>
   /// This method writes G Code machining for Arcs/Circles.
   /// </summary>
   /// <param name="arc"> The arc in 3d, to be machined thrrough</param>
   /// <param name="arcPlaneType">Arc plane type [Utils.EPlane] to derive the if the arc is G2 or 
   /// G3</param>
   /// <param name="arcFlangeType">Arc flange type [Utils.EFlange] to omit/include the appropriate 
   /// coordinates</param>
   /// <param name="arcCenter">Center of the arc</param>
   /// <param name="arcStartPoint">Arc start point</param>
   /// <param name="arcEndPoint">End point of the arc</param>
   /// <param name="startNormal">Start normal at the beginning of the arc. The arc is considered 
   /// planar and so the end normal
   /// is same as start normal</param>
   /// <param name="toolingName">Name of the tooling for simulation and debug</param>
   /// <exception cref="ArgumentException"></exception>
   public void WriteArc (Arc3 arc, Utils.EPlane arcPlaneType, Utils.EFlange arcFlangeType,
      Point3 arcCenter, Point3 arcStartPoint, Point3 arcEndPoint, Vector3 startNormal, string toolingName) {
      Utils.EArcSense arcType;
      var apn = arcPlaneType switch {
         Utils.EPlane.Top => XForm4.mZAxis,
         Utils.EPlane.YPos => XForm4.mYAxis,
         Utils.EPlane.YNeg => -XForm4.mYAxis,
         _ => throw new Exception ("Arc can not be written onflex plane")
      };
      var (_, arcSense) = Geom.GetArcAngleAndSense (arc, apn);
      // Both in YNeg and YPos plane, PLC is taking a different reference
      // Z axis is decreasing while moving from top according to Eckelmann controller
      // So need to reverse clockwise and counter clockwise option
      /*^ (arcPlaneType == FChassisUtils.EPlane.Top && !Options.ReverseY)*/
      if (arcSense == Utils.EArcSense.CCW) arcType = Utils.EArcSense.CCW;
      else arcType = Utils.EArcSense.CW;
      arcStartPoint = Utils.MovePoint (arcStartPoint, startNormal, Standoff);
      arcEndPoint = Utils.MovePoint (arcEndPoint, startNormal, Standoff);
      arcCenter = Utils.MovePoint (arcCenter, startNormal, Standoff);
      // Transform the arc end point to machine coordinate system
      var mcCoordArcCenter = XfmToMachine (arcCenter);
      var mcCoordArcStPoint = XfmToMachine (arcStartPoint);
      var mcCoordArcEndPoint = XfmToMachine (arcEndPoint);
      var mcCoordArcCenter2D = Utils.ToPlane (mcCoordArcCenter, arcPlaneType);
      var mcCoordArcStPoint2D = Utils.ToPlane (mcCoordArcStPoint, arcPlaneType);
      var mcCoordArcEndPoint2D = Utils.ToPlane (mcCoordArcEndPoint, arcPlaneType);
      var arcSt2CenVec = mcCoordArcCenter2D - mcCoordArcStPoint2D; // This gives I and J
      var radius = arcSt2CenVec.Length;
      EGCode gCmd;
      if (arcType == Utils.EArcSense.CW) gCmd = EGCode.G2; else gCmd = EGCode.G3;
      mTraces[ToolHead].Add (new GCodeSeg (arc, arcStartPoint, arcEndPoint, arcCenter, radius, startNormal,
         gCmd, EMove.Machining, toolingName));
      mToolPos[ToolHead] = arcEndPoint;
      mToolVec[ToolHead] = startNormal;
      switch (arcFlangeType) {
         case Utils.EFlange.Web:
            if (Utils.IsArc (arc))
               Utils.ArcMachining (sw, arcType, arcSt2CenVec.X, OrdinateAxis.Y, arcSt2CenVec.Y, mcCoordArcEndPoint2D.X,
                  mcCoordArcEndPoint2D.Y, arcFlangeType);
            else
               Utils.CircularMachining (sw, arcType, arcSt2CenVec.X, OrdinateAxis.Y, arcSt2CenVec.Y, arcFlangeType);
            break;
         case Utils.EFlange.Bottom:
            if (Utils.IsArc (arc))
               Utils.ArcMachining (sw, arcType, arcSt2CenVec.X, OrdinateAxis.Z, arcSt2CenVec.Y, mcCoordArcEndPoint2D.X,
                  mcCoordArcEndPoint2D.Y, arcFlangeType);
            else
               Utils.CircularMachining (sw, arcType, arcSt2CenVec.X, OrdinateAxis.Z, arcSt2CenVec.Y, arcFlangeType);
            break;
         case Utils.EFlange.Top:
            if (Utils.IsArc (arc))
               Utils.ArcMachining (sw, arcType, arcSt2CenVec.X, OrdinateAxis.Z, arcSt2CenVec.Y, mcCoordArcEndPoint2D.X,
                  mcCoordArcEndPoint2D.Y, arcFlangeType);
            else
               Utils.CircularMachining (sw, arcType, arcSt2CenVec.X, OrdinateAxis.Z, arcSt2CenVec.Y, arcFlangeType);
            break;
         default:
            throw new ArgumentException ("Arc is ill-defined perhaps on the flex");
      }
   }

   void WriteArc (Arc3 arc, Utils.EPlane arcPlaneType,
      Point3 arcCenter, Point3 arcStartPoint, Point3 arcEndPoint, Vector3 startNormal, string toolingName) {
      Utils.EArcSense arcType;
      var apn = arcPlaneType switch {
         Utils.EPlane.Top => XForm4.mZAxis,
         Utils.EPlane.YPos => XForm4.mYAxis,
         Utils.EPlane.YNeg => -XForm4.mYAxis,
         _ => throw new Exception ("Arc can not be written onflex plane")
      };
      var (_, arcSense) = Geom.GetArcAngleAndSense (arc, apn);
      // Both in YNeg and YPos plane, PLC is taking a different reference
      // Z axis is decreasing while moving from top according to Eckelmann controller
      // So need to reverse clockwise and counter clockwise option
      /*^ (arcPlaneType == FChassisUtils.EPlane.Top && !Options.ReverseY)*/
      if (arcSense == Utils.EArcSense.CCW) arcType = Utils.EArcSense.CCW;
      else arcType = Utils.EArcSense.CW;
      arcStartPoint = Utils.MovePoint (arcStartPoint, startNormal, Standoff);
      arcEndPoint = Utils.MovePoint (arcEndPoint, startNormal, Standoff);
      arcCenter = Utils.MovePoint (arcCenter, startNormal, Standoff);
      // Transform the arc end point to machine coordinate system
      var mcCoordArcCenter = XfmToMachine (arcCenter);
      var mcCoordArcStPoint = XfmToMachine (arcStartPoint);
      var mcCoordArcEndPoint = XfmToMachine (arcEndPoint);
      var mcCoordArcCenter2D = Utils.ToPlane (mcCoordArcCenter, arcPlaneType);
      var mcCoordArcStPoint2D = Utils.ToPlane (mcCoordArcStPoint, arcPlaneType);
      var mcCoordArcEndPoint2D = Utils.ToPlane (mcCoordArcEndPoint, arcPlaneType);
      var arcSt2CenVec = mcCoordArcCenter2D - mcCoordArcStPoint2D; // This gives I and J
      var radius = arcSt2CenVec.Length;
      EGCode gCmd;
      if (arcType == Utils.EArcSense.CW) gCmd = EGCode.G2; else gCmd = EGCode.G3;
      mTraces[ToolHead].Add (new GCodeSeg (arc, arcStartPoint, arcEndPoint, arcCenter, radius, startNormal,
         gCmd, EMove.Machining, toolingName));
      mToolPos[ToolHead] = arcEndPoint;
      mToolVec[ToolHead] = startNormal;

      Utils.EFlange arcFlangeType = Utils.GetArcPlaneFlangeType (startNormal, GetXForm ());
      switch (arcFlangeType) {
         case Utils.EFlange.Web:
            if (Utils.IsArc (arc))
               Utils.ArcMachining (sw, arcType, arcSt2CenVec.X, OrdinateAxis.Y, arcSt2CenVec.Y, mcCoordArcEndPoint2D.X,
                  mcCoordArcEndPoint2D.Y, arcFlangeType);
            else
               Utils.CircularMachining (sw, arcType, arcSt2CenVec.X, OrdinateAxis.Y, arcSt2CenVec.Y, arcFlangeType);
            break;
         case Utils.EFlange.Bottom:
            if (Utils.IsArc (arc))
               Utils.ArcMachining (sw, arcType, arcSt2CenVec.X, OrdinateAxis.Z, arcSt2CenVec.Y, mcCoordArcEndPoint2D.X,
                  mcCoordArcEndPoint2D.Y, arcFlangeType);
            else
               Utils.CircularMachining (sw, arcType, arcSt2CenVec.X, OrdinateAxis.Z, arcSt2CenVec.Y, arcFlangeType);
            break;
         case Utils.EFlange.Top:
            if (Utils.IsArc (arc))
               Utils.ArcMachining (sw, arcType, arcSt2CenVec.X, OrdinateAxis.Z, arcSt2CenVec.Y, mcCoordArcEndPoint2D.X,
                  mcCoordArcEndPoint2D.Y, arcFlangeType);
            else
               Utils.CircularMachining (sw, arcType, arcSt2CenVec.X, OrdinateAxis.Z, arcSt2CenVec.Y, arcFlangeType);
            break;
         default:
            throw new ArgumentException ("Arc is ill-defined perhaps on the flex");
      }
   }

   /// <summary>
   /// This method writes GCode for the segment of motion from Retracted tool position 
   /// to the machining start tool position.</summary>
   /// <param name="toolingItem">The input tooling</param>
   /// <param name="toolingStartPosition">The new tooling start position</param>
   /// <param name="toolingStartNormal">The normal at the tooling start normal</param>
   public void MoveToMachiningStartPosition (Point3 toolingStartPosition, Vector3 toolingStartNormal, string toolingName) {
      // Linear Move to start machining tooling
      Point3 toolingStartPointWithMachineClearance = toolingStartPosition + toolingStartNormal * Standoff;
      mTraces[ToolHead].Add (new (mToolPos[ToolHead], toolingStartPointWithMachineClearance,
         mToolVec[ToolHead], toolingStartNormal, EGCode.G1, EMove.Retract2Machining, toolingName));
      mToolPos[ToolHead] = toolingStartPointWithMachineClearance;
      mToolVec[ToolHead] = toolingStartNormal;
   }

   /// <summary>
   /// This method specifically writes machining G Code (G1) for linear machinable
   /// moves.
   /// </summary>
   /// <param name="endPoint">End point of the current segment.</param>
   /// <param name="startNormal">Start normal of the current linear segment, needed for simulation data</param>
   /// <param name="endNormal">End normal of the current linear segment, needed for simulation data</param>
   /// <param name="currPlaneType">Current plane type, needed if angle to be included in the G Code statement</param>
   /// <param name="previousPlaneType">Previous plane type, needed if angle to be included in the 
   /// G Code statement</param>
   /// <param name="currFlangeType">Current flange type, needed to include Y/Z coordinates in the G Code</param>
   /// <param name="toolingName">Name of the tooling for simulation purposes</param>
   public void WriteLine (Point3 endPoint, Vector3 startNormal, Vector3 endNormal,
      Utils.EPlane currPlaneType, Utils.EPlane previousPlaneType, Utils.EFlange currFlangeType, string toolingName) {
      var endPointWithMCClearance = endPoint + endNormal * Standoff;
      Point3 mcCoordEndPointWithMCClearance;
      string lineSegmentComment = "";
      double angleBetweenZAxisAndCurrToolingEndPoint;
      // This following check does not set angle every time for the same plane type.
      if (currPlaneType == Utils.EPlane.Flex || currPlaneType != previousPlaneType) {
         if (currPlaneType == Utils.EPlane.Flex)
            angleBetweenZAxisAndCurrToolingEndPoint = Utils.GetAngleAboutXAxis (XForm4.mZAxis,
               endNormal, GetXForm ()).R2D ();
         else
            angleBetweenZAxisAndCurrToolingEndPoint = Utils.GetAngle4PlaneTypeAboutXAxis (currPlaneType).R2D ();
         mcCoordEndPointWithMCClearance = XfmToMachine (endPointWithMCClearance);
         if (currFlangeType == Utils.EFlange.Bottom || currFlangeType == Utils.EFlange.Top)
            Utils.LinearMachining (sw, mcCoordEndPointWithMCClearance.X, OrdinateAxis.Z, mcCoordEndPointWithMCClearance.Z,
               angleBetweenZAxisAndCurrToolingEndPoint, lineSegmentComment);
         else if (currFlangeType == Utils.EFlange.Web)
            Utils.LinearMachining (sw, mcCoordEndPointWithMCClearance.X, OrdinateAxis.Y, mcCoordEndPointWithMCClearance.Y,
               angleBetweenZAxisAndCurrToolingEndPoint, lineSegmentComment);
         else
            Utils.LinearMachining (sw, mcCoordEndPointWithMCClearance.X, mcCoordEndPointWithMCClearance.Y, mcCoordEndPointWithMCClearance.Z,
               angleBetweenZAxisAndCurrToolingEndPoint, lineSegmentComment);

         mTraces[ToolHead].Add (new GCodeSeg (mToolPos[ToolHead], endPointWithMCClearance,
            startNormal, endNormal, EGCode.G1, EMove.Machining, toolingName));
         mToolPos[ToolHead] = endPointWithMCClearance;
         mToolVec[ToolHead] = endNormal;
      } else {
         mcCoordEndPointWithMCClearance = XfmToMachine (endPointWithMCClearance);
         if (currFlangeType == Utils.EFlange.Top || currFlangeType == Utils.EFlange.Bottom)
            Utils.LinearMachining (sw, mcCoordEndPointWithMCClearance.X, OrdinateAxis.Z, mcCoordEndPointWithMCClearance.Z,
               lineSegmentComment);
         else if (currFlangeType == Utils.EFlange.Web)
            Utils.LinearMachining (sw, mcCoordEndPointWithMCClearance.X, OrdinateAxis.Y, mcCoordEndPointWithMCClearance.Y,
               lineSegmentComment);
         else
            Utils.LinearMachining (sw, mcCoordEndPointWithMCClearance.X, mcCoordEndPointWithMCClearance.Y,
               mcCoordEndPointWithMCClearance.Z, lineSegmentComment);
         mTraces[ToolHead].Add (new GCodeSeg (mToolPos[ToolHead], endPointWithMCClearance,
            startNormal, endNormal, EGCode.G1, EMove.Machining, toolingName));
         mToolPos[ToolHead] = endPointWithMCClearance;
         mToolVec[ToolHead] = endNormal;
      }
   }

   /// <summary>
   /// This method specifically writes machining G Code (G1) for linear machinable moves
   /// </summary>
   /// <param name="endPoint">End point of the current segment.</param>
   /// <param name="startNormal">Start normal of the current linear segment, needed for simulation data</param>
   /// <param name="endNormal">End normal of the current linear segment, needed for simulation data</param>
   /// <param name="toolingName">Name of the tooling</param>
   public void WriteLine (Point3 endPoint, Vector3 startNormal, Vector3 endNormal,
      string toolingName) {
      var endPointWithMCClearance = endPoint + endNormal * Standoff;
      Point3 mcCoordEndPointWithMCClearance;
      string lineSegmentComment = "";
      double angleBetweenZAxisAndCurrToolingEndPoint;

      bool planeChange = false;
      var angleBetweenPrevAndCurrNormal = endNormal.AngleTo (mToolVec[ToolHead]).R2D ();
      if (!angleBetweenPrevAndCurrNormal.EQ (0)) planeChange = true;

      angleBetweenZAxisAndCurrToolingEndPoint = endNormal.AngleTo (XForm4.mZAxis).R2D ();
      mcCoordEndPointWithMCClearance = XfmToMachine (endPointWithMCClearance);

      Utils.EFlange currFlangeType = Utils.GetArcPlaneFlangeType (startNormal, GetXForm ());
      if (planeChange) {
         if (currFlangeType == Utils.EFlange.Bottom || currFlangeType == Utils.EFlange.Top)
            Utils.LinearMachining (sw, mcCoordEndPointWithMCClearance.X, OrdinateAxis.Z, mcCoordEndPointWithMCClearance.Z,
               angleBetweenZAxisAndCurrToolingEndPoint, lineSegmentComment);
         else if (currFlangeType == Utils.EFlange.Web)
            Utils.LinearMachining (sw, mcCoordEndPointWithMCClearance.X, OrdinateAxis.Y, mcCoordEndPointWithMCClearance.Y,
               angleBetweenZAxisAndCurrToolingEndPoint, lineSegmentComment);
         else
            Utils.LinearMachining (sw, mcCoordEndPointWithMCClearance.X, mcCoordEndPointWithMCClearance.Y, mcCoordEndPointWithMCClearance.Z,
               angleBetweenZAxisAndCurrToolingEndPoint, lineSegmentComment);
      } else {
         if (currFlangeType == Utils.EFlange.Top || currFlangeType == Utils.EFlange.Bottom)
            Utils.LinearMachining (sw, mcCoordEndPointWithMCClearance.X, OrdinateAxis.Z, mcCoordEndPointWithMCClearance.Z,
               lineSegmentComment);
         else if (currFlangeType == Utils.EFlange.Web)
            Utils.LinearMachining (sw, mcCoordEndPointWithMCClearance.X, OrdinateAxis.Y, mcCoordEndPointWithMCClearance.Y,
               lineSegmentComment);
         else
            Utils.LinearMachining (sw, mcCoordEndPointWithMCClearance.X, mcCoordEndPointWithMCClearance.Y, mcCoordEndPointWithMCClearance.Z,
               lineSegmentComment);
      }
      mTraces[ToolHead].Add (new GCodeSeg (mToolPos[ToolHead], endPointWithMCClearance,
            startNormal, endNormal, EGCode.G1, EMove.Machining, toolingName));
      mToolPos[ToolHead] = endPointWithMCClearance;
      mToolVec[ToolHead] = endNormal;
   }

   /// <summary>
   /// This method writes G Code for the plane of the arc to be machined
   /// </summary>
   /// <param name="arcFlangeType">Arc Flange Type is a type of Utils.EFlange</param>
   void WritePlaneForCircularMotionCommand (Utils.EFlange arcFlangeType) {
      switch (arcFlangeType) {
         case Utils.EFlange.Web: sw.Write ("G17"); break;
         case Utils.EFlange.Top: sw.Write ("G18"); break;
         case Utils.EFlange.Bottom: sw.Write ("G18"); break;
         default: break;
      }
      sw.Write ("\t( Plane Selection : G17-XY Plane, G18-XZ Plane )");
      sw.WriteLine ();
   }

   /// <summary>
   /// This method accounts for the notch approach length by adding a quartercircular arc
   /// lying on the material removal side or scrap side of the hole or cutout. 
   ///
   /// The start point of the arc will be on the material removal side, and the ending 
   /// point of the arc will be the mid point of the segment (arc or line). The ending point 
   /// of the arc is the start point of the arc if it is a circle. 
   /// 
   /// The tooling shall have to start from the approach arc start point and end at the 
   /// mid point of the original start segment if it is an arc or line segment, the end point 
   /// 
   /// If the segment is not a circle, in order to make this seamless, the start segment is 
   /// split into two. The modified first segment becomes the arc, second segment becomes 
   /// the second split segment of the original first curve, and finally,
   /// the last segment is the first split segment of the original first segment.
   /// 
   /// If the input segment is a circle, the modified segments list shall now have the approach arc
   /// as the first tooling segment, and the circle itself as the next tooling segment.
   /// 
   /// Algorithm: 
   /// The approach distance from the starting point in the case of circle, or the mid point
   /// in the case of line or arc, is set to 4 times the approach length set in the settings. 
   /// If this approach length is greater than the radius of the circle/arc itself, then the approach 
   /// distance is set to 2.0 mm. If it is still greater each halfed value is checked until the value is 
   /// more than 0.5mm. Otherwise, 0.5 mm is set.
   /// 
   /// The center of the approach arc is computed to be at radius distance ( approach distance of arc / 2)
   /// The first point on the arc is computed to be the above radius distance along the direction of 
   /// negative tooling
   /// 
   /// The last point of the arc is the new tooling entry point, which is the mid point of the tooling
   /// if the segment is not circle and is either arc or line OR the start point of the circle.
   /// Next, 2 points are found at the distances of 4 and then 2 mm from the new tooling entry point,
   /// (which is the end point of the arc), in the direction of negative tooling, say d1 and d2.
   ///
   /// The intermediate points are found by taking a point along the direction of  d1 and d2 from the 
   /// center of the arcs.
   /// </summary>
   /// <param name="toolingItem"></param>
   /// <returns>The modified list of the tooling segments</returns>
   public List<ToolingSegment> GetSegmentsAccountedForApproachLength (Tooling toolingItem) {
      // If the tooling item is Mark, no need of creating the G Code
      if (toolingItem.IsMark ()) return [.. toolingItem.Segs];

      List<ToolingSegment> modifiedSegmentsList = [];
      var toolingSegmentsList = toolingItem.Segs.ToList ();
      Vector3 materialRemovalDirection; Point3 firstToolingEntryPt;
      if (!toolingItem.IsNotch () && !toolingItem.IsMark ()) {
         // E3Plane normal 
         var apn = Utils.GetEPlaneNormal (toolingItem, XForm4.IdentityXfm);

         // Compute an appropriate approach length. From the engg team, 
         // it was asked to have a dia = approach length * 4, which is 
         // stored in approachDistOfArc. 
         // if the circle's dia is smaller than the above approachDistOfArc
         // then assign approach length from settings. 
         // Recursively find the approachDistOfArc by halving the previous value
         // until its not lesser than 0.5. 0.5 is the lower limit.
         var approachDistOfArc = ApproachLength * 4.0;
         double circleRad;
         if (toolingItem.Segs.ToList ()[0].Curve is Arc3 circle && Utils.IsCircle (circle)) {
            (_, circleRad) = Geom.EvaluateCenterAndRadius (circle);
            if (circleRad < approachDistOfArc) approachDistOfArc = ApproachLength;
            while (circleRad < approachDistOfArc) {
               if (approachDistOfArc < mCurveLeastLength) break;
               approachDistOfArc *= mCurveLeastLength;
            }
         }

         // Compute the scrap side direction
         (firstToolingEntryPt, materialRemovalDirection) = Utils.GetMaterialRemovalSideDirection (toolingItem);

         // Compute the tooling direction.
         Vector3 toolingDir;
         if (toolingSegmentsList[0].Curve is Line3)
            toolingDir = toolingSegmentsList[0].Curve.End - toolingSegmentsList[0].Curve.Start;
         else
            (toolingDir, _) = Geom.EvaluateTangentAndNormalAtPoint (toolingSegmentsList[0].Curve as Arc3,
               firstToolingEntryPt, apn);
         toolingDir = toolingDir.Normalized ();

         // Compute new start point of the tooling, which is the start point of the quarter arc point on the 
         // scrap side of the material
         //var newToolingStPt = firstToolingEntryPt + materialRemovalDirection * approachDistOfArc;
         var approachArcRad = approachDistOfArc * 0.5;
         var arcCenter = firstToolingEntryPt + materialRemovalDirection * approachArcRad;
         var newToolingStPt = arcCenter - toolingDir * approachArcRad;

         // Find 2 points on the ray from newToolingStPt in the direction of -toolingDir, from the center of the arc
         var p1 = firstToolingEntryPt - toolingDir * 4.0; var p2 = firstToolingEntryPt - toolingDir * 2.0;

         // Compute the vectors from center of the arc to the above points
         var cp1 = (p1 - arcCenter).Normalized (); var cp2 = (p2 - arcCenter).Normalized ();

         // Compute the intersection of the vector cenetr to p1/p2 on the circle of the arc. These are 
         // intermediate points along the actual direction of the arc
         var ip1 = arcCenter + cp1 * approachArcRad; var ip2 = arcCenter + cp2 * approachArcRad;

         // Create arc, the fourth point being the midpoint of the arc or starting point
         // if its a circle.
         Arc3 arc = new (newToolingStPt, ip1, ip2, firstToolingEntryPt);
         if (Utils.IsCircle (toolingSegmentsList[0].Curve)) {
            modifiedSegmentsList.Add (new (arc, toolingSegmentsList[0].Vec0, toolingSegmentsList[0].Vec0));
            modifiedSegmentsList.Add (toolingSegmentsList[0]);
            return modifiedSegmentsList;
         } else {
            List<Point3> internalPoints = [];
            internalPoints.Add (Geom.GetMidPoint (toolingSegmentsList[0].Curve, apn));
            var splitCurves = Geom.SplitCurve (toolingSegmentsList[0].Curve, internalPoints, apn, deltaBetween: 0.0);
            modifiedSegmentsList.Add (new (arc, toolingSegmentsList[0].Vec0, toolingSegmentsList[0].Vec0));
            modifiedSegmentsList.Add (new (splitCurves[1], toolingSegmentsList[0].Vec0, toolingSegmentsList[0].Vec1));
            for (int ii = 1; ii < toolingSegmentsList.Count; ii++) modifiedSegmentsList.Add (toolingSegmentsList[ii]);
            modifiedSegmentsList.Add (new (splitCurves[0], toolingSegmentsList[0].Vec0, toolingSegmentsList[0].Vec1));
            return modifiedSegmentsList;
         }
      } else return toolingSegmentsList;
   }

   /// <summary>
   /// This is the method to be called for actual machining. This takes care of 
   /// linear and circular machining.
   /// </summary>
   /// <param name="toolingSegmentsList">List of Tooling segments (of lines and arcs)</param>
   /// <param name="toolingItem">The actual tooling item. The tooling segments list might vary 
   /// from the tooling segments of the tooling item, when the segments are modified for approach 
   /// distance by adding a quarter circular arc</param>
   /// <param name="bound">The bounding box of the tooling item</param>
   ToolingSegment WriteTooling (List<ToolingSegment> toolingSegmentsList, Tooling toolingItem,
      Bound3 bound, double totalPrevCutToolingsLength, double totalToolingCutLength, /*double frameFeed*/
      double xStart, double xPartition, double xEnd) {
      ToolingSegment ts;
      (var curve, var CurveStartNormal, _) = toolingSegmentsList[0];
      Utils.EPlane previousPlaneType = Utils.EPlane.None;
      Utils.EPlane currPlaneType;
      if (toolingItem.IsFlexFeature ()) currPlaneType = Utils.EPlane.Flex;
      else currPlaneType = Utils.GetFeatureNormalPlaneType (CurveStartNormal, new ());

      // Write any feature other than notch
      MoveToMachiningStartPosition (curve.Start, CurveStartNormal, toolingItem.Name);
      {
         // Write all other features such as Holes, Cutouts and edge notches
         EnableMachiningDirective ();
         for (int i = 0; i < toolingSegmentsList.Count; i++) {
            var (Curve, startNormal, endNormal) = toolingSegmentsList[i];
            startNormal = startNormal.Normalized ();
            endNormal = endNormal.Normalized ();
            var startPoint = Curve.Start;
            var endPoint = Curve.End;
            if (i > 0) currPlaneType = Utils.GetFeatureNormalPlaneType (endNormal, new ());

            if (Curve is Arc3) { // This is a 2d arc. 
               var arcPlaneType = Utils.GetArcPlaneType (startNormal, new ());
               var arcFlangeType = Utils.GetArcPlaneFlangeType (startNormal, new ());
               (var center, _) = Geom.EvaluateCenterAndRadius (Curve as Arc3);
               WriteArc (Curve as Arc3, arcPlaneType, arcFlangeType, center, startPoint, endPoint, startNormal,
                  toolingItem.Name);
            } else WriteLine (endPoint, startNormal, endNormal, currPlaneType, previousPlaneType,
               Utils.GetFlangeType (toolingItem, new ()), toolingItem.Name);
            previousPlaneType = currPlaneType;
         }
         DisableMachiningDirective ();
         ts = toolingSegmentsList[^1];
      }
      return ts;
   }

   /// <summary>
   /// This method gets the tooling shape kind of the tooling
   /// </summary>
   /// <param name="toolingItem">The input tooling</param>
   /// <returns>Returns one of Notch, HoleShape, Text, or Cutout</returns>
   /// <exception cref="NotSupportedException">This exception is thrown if 
   /// any other kind is encountered</exception>
   static EToolingShape GetToolingShapeKind (Tooling toolingItem) {
      EToolingShape shape = EToolingShape.HoleShape;
      Curve3 firstCurve = toolingItem.Segs.First ().Curve;
      if (firstCurve as Arc3 != null) {
         if (Utils.IsCircle (firstCurve))
            shape = EToolingShape.Circle;
      } else if (toolingItem.Kind == EKind.Notch) shape = EToolingShape.Notch;
      else if (toolingItem.Kind == EKind.Hole) shape = EToolingShape.HoleShape;
      else if (toolingItem.Kind == EKind.Mark) shape = EToolingShape.Text;
      else if (toolingItem.Kind == EKind.Cutout) shape = EToolingShape.Cutout;
      else throw new NotSupportedException ("Invalid tooling item kind encountered");
      return shape;
   }

   /// <summary>
   /// THis method moves the tool in rapid position to the safety position. 
   /// This the X and Y coordinate of the tool origin with Z value as 28 mm.
   /// This method registers this data only for the simulation and has no 
   /// bearing on the G Code that is being written. 
   /// </summary>
   void MoveToSafety () {
      mTraces[ToolHead].Add (new (mSafePoint[ToolHead], mToolPos[ToolHead], XForm4.mZAxis, XForm4.mZAxis,
         EGCode.G0, EMove.Retract2SafeZ, "No tooling"));
      mToolVec[ToolHead] = XForm4.mZAxis;
   }

   /// <summary>
   /// This method moves the tool from current machining position, end of tooling to 
   /// the retracted position only for the simulation. This has no bearing on the G Code 
   /// that is being written.
   /// </summary>
   /// <param name="endPt">Current tooling end point</param>
   /// <param name="endNormal">End normal at the tooling end point</param>
   /// <param name="toolingName">Tooling name</param>
   public void MoveToRetract (Point3 endPt, Vector3 endNormal, string toolingName) {
      var toolingEPRetracted =
             Utils.MovePoint (endPt, endNormal, mRetractClearance);
      mTraces[ToolHead].Add (new (mToolPos[ToolHead], toolingEPRetracted, endNormal, endNormal,
         EGCode.G0, EMove.Retract, toolingName));
      mToolPos[ToolHead] = toolingEPRetracted;
      mToolVec[ToolHead] = endNormal.Normalized ();
   }

   /// <summary>
   /// This method makes the tool move from previous tooling retract position, which is 
   /// previous tooling end position away from the position by end normal of the previous tooling
   /// TO the position, whose coordinates are X of the next tooling, Y of the next tooling and Z as safety
   /// value (28 mm).
   /// </summary>
   /// <param name="prevToolingSegs">Segments of the previous tooling</param>
   /// <param name="prevToolingName">Name of the previous tooling</param>
   /// <param name="currToolingSegs">Segments of the current tooling.</param>
   /// <param name="currentToolingName">Name of the current tooling</param>
   void MoveFromRetractToSafety (List<ToolingSegment> prevToolingSegs, string prevToolingName,
      List<ToolingSegment> currToolingSegs,
      string currentToolingName, bool isMark) {
      if (prevToolingSegs != null && prevToolingSegs.Count > 0) {
         (var prevSegEndCurve, _, var prevSegEndCurveEndNormal) = prevToolingSegs[^1];
         var prevToolingEPRetracted =
                Utils.MovePoint (prevSegEndCurve.End, prevSegEndCurveEndNormal, mRetractClearance);
         Point3 prevToolingEPRetractedSafeZ = new (prevToolingEPRetracted.X, prevToolingEPRetracted.Y,
            mSafeClearance);
         var mcCoordsPrevToolingEPRetractedSafeZ = XfmToMachine (prevToolingEPRetractedSafeZ);
         Utils.LinearMachining (sw, mcCoordsPrevToolingEPRetractedSafeZ.X, mcCoordsPrevToolingEPRetractedSafeZ.Y,
            mcCoordsPrevToolingEPRetractedSafeZ.Z, 0, Rapid);
         mTraces[ToolHead].Add (new GCodeSeg (mToolPos[ToolHead], prevToolingEPRetractedSafeZ, mToolVec[ToolHead],
            XForm4.mZAxis, EGCode.G0, EMove.Retract2SafeZ, prevToolingName));
         mToolPos[ToolHead] = prevToolingEPRetractedSafeZ;
         mToolVec[ToolHead] = XForm4.mZAxis;
      }
      (var currSegStCurve, var currSegStCurveStNormal, _) = currToolingSegs[0];

      // Move to the current tooling item start posotion safeZ
      var currToolingSPRetracted =
             Utils.MovePoint (currSegStCurve.Start, currSegStCurveStNormal, mRetractClearance);
      Point3 currToolingSPRetractedSafeZ = new (currToolingSPRetracted.X, currToolingSPRetracted.Y,
         mSafeClearance);
      var mcCoordsCurrToolingSPRetractedSafeZ = XfmToMachine (currToolingSPRetractedSafeZ);
      if (!isMark) {
         Utils.RapidPosition (sw, mcCoordsCurrToolingSPRetractedSafeZ.X, mcCoordsCurrToolingSPRetractedSafeZ.Y,
            mcCoordsCurrToolingSPRetractedSafeZ.Z, 0);
         mTraces[ToolHead].Add (new (mToolPos[ToolHead], currToolingSPRetractedSafeZ, mToolVec[ToolHead],
            XForm4.mZAxis, EGCode.G0,
            EMove.SafeZ2SafeZ, currentToolingName));
         mToolPos[ToolHead] = currToolingSPRetractedSafeZ;
         mToolVec[ToolHead] = XForm4.mZAxis;
      }
   }

   /// <summary>
   /// This method moves the tool using G1, from safety Z position (28mm) to
   /// the retract position (for the next tooling). The retract position is the
   /// position from the next tooling start point, offset by retract clearance
   /// along the start normal vector
   /// </summary>
   /// <param name="toolingStartPt">Tooling start point of the next tooling</param>
   /// <param name="toolingStartNormalVec">Normal vector (outward) at the next tooling start point</param>
   /// <param name="toolingName">Name of the tooling : Can be used in simulation for debug purpose</param>
   void MoveFromSafetyToRetract (Point3 toolingStartPt, Vector3 toolingStartNormalVec, string toolingName,
      bool planeChangeNeeded, bool isMark) {
      var currToolingStPtRetracted =
            Utils.MovePoint (toolingStartPt, toolingStartNormalVec, mRetractClearance);
      var angleBetweenZAxisNcurrToolingStPt =
    Utils.GetAngleAboutXAxis (XForm4.mZAxis, toolingStartNormalVec, GetXForm ()).R2D ();
      var mcCoordsCurrToolingStPtRetracted = XfmToMachine (currToolingStPtRetracted);

      if (!isMark) {
         if (planeChangeNeeded) {
            Utils.LinearMachining (sw, mcCoordsCurrToolingStPtRetracted.X, mcCoordsCurrToolingStPtRetracted.Y,
               mcCoordsCurrToolingStPtRetracted.Z, angleBetweenZAxisNcurrToolingStPt, Rapid, "Move to Piercing Position");
         } else {
            var planeType = Utils.GetPlaneType (toolingStartNormalVec, GetXForm ());
            if (planeType == EPlane.YNeg || planeType == EPlane.YPos)
               Utils.RapidPosition (sw, mcCoordsCurrToolingStPtRetracted.X, OrdinateAxis.Z, mcCoordsCurrToolingStPtRetracted.Z,
                  "Move to Piercing Position", UsePingPong ? "M1014" : "");
            else if (planeType == EPlane.Top)
               Utils.RapidPosition (sw, mcCoordsCurrToolingStPtRetracted.X, OrdinateAxis.Y, mcCoordsCurrToolingStPtRetracted.Y,
                  "Move to Piercing Position", UsePingPong ? "M1014" : "");
         }
         mTraces[ToolHead].Add (new (mToolPos[ToolHead], currToolingStPtRetracted,
            mToolVec[ToolHead], toolingStartNormalVec, EGCode.G0, EMove.SafeZ2Retract, toolingName));
         mToolPos[ToolHead] = currToolingStPtRetracted;
         mToolVec[ToolHead] = toolingStartNormalVec.Normalized ();
      }
   }

   /// <summary>
   /// This method writes the X bounds for the feature described by the list of 
   /// tooling items, to write START_X and END_X. 
   /// </summary>
   /// <param name="toolingItem">The inout tooling item</param>
   /// <param name="segments">The list of tooling segments</param>
   /// <param name="startIndex">The start index in the list of tooling items. If it is -1, all the segments in the tooling
   /// segments will be considered</param>
   /// <param name="endIndex">The end index of the list of tooling items.</param>
   public void WriteBounds (Tooling toolingItem, List<ToolingSegment> segments, int startIndex = -1, int endIndex = -1) {
      var toolingSegsBounds = Utils.GetToolingSegmentsBounds (segments, Process.Workpiece.Model.Bound, startIndex, endIndex);
      var xMin = toolingSegsBounds.XMin; var xMax = toolingSegsBounds.XMax;
      var minPt = new Point3 (xMin, 0, 0); var maxPt = new Point3 (xMax, 0, 0);
      var mcMinPt = GCodeGenerator.XfmToMachine (this, minPt);
      var mcMaxPt = GCodeGenerator.XfmToMachine (this, maxPt);
      if (LeftToRightMachining)
         sw.WriteLine ($"START_X={mcMinPt.X:F3} END_X={mcMaxPt.X:F3} PathLength={toolingItem.Perimeter:F2}");
      else
         sw.WriteLine ($"START_X={mcMaxPt.X:F3} END_X={mcMinPt.X:F3} PathLength={toolingItem.Perimeter:F2}");
   }

   void CalibrateForCircle (Tooling toolingItem, Tooling prevToolingItem) {
      if (toolingItem.IsCircle ()) {
         var evalValue = Geom.EvaluateCenterAndRadius (toolingItem.Segs.ToList ()[0].Curve as Arc3);
         Point3 arcMcCoordsCenter;
         if (prevToolingItem != null) arcMcCoordsCenter = XfmToMachine (evalValue.Item1);
         else arcMcCoordsCenter = XfmToMachine (evalValue.Item1);
         var point2 = Utils.ToPlane (arcMcCoordsCenter, Utils.GetFeatureNormalPlaneType (toolingItem.Start.Vec, XForm4.IdentityXfm));
         sw.WriteLine ($"X_Coordinate={point2.X:F3} YZ_Coordinate={point2.Y:F3}");
      }
   }

   public void WriteProgramHeader (Tooling toolingItem, List<ToolingSegment> segs, /*double frameFeed, */
      double xStart, double xPartition, double xEnd, bool isLast,
      Tooling prevToolingItem = null, bool isValidNotch = false, bool isFlexCut = false,
      int startIndex = -1, int endIndex = -1) {
      string comment = $"** Tooling Name : {toolingItem.Name} - {toolingItem.FeatType} **";
      OutN (sw, mProgramNumber, comment);
      sw.WriteLine ("CutScopeNo={0}", mCutScopeNo);
      if (isValidNotch || toolingItem.IsCutout ()) mProgramNumber++;
      WriteBlockType (toolingItem, isValidNotch, isFlexCut);
      double SplitEndX = xEnd;
      if (mLastCutScope && isLast) SplitEndX = Process.Workpiece.Bound.XMax;

      sw.WriteLine ("SplitStartX={0} SplitPartitionX={1} SplitEndX={2} ( Cut Scope Length:{3} )",
         xStart.ToString ("F3"), xPartition.ToString ("F3"), SplitEndX.ToString ("F3"), (xEnd - xStart).ToString ("F3"));
      WriteBounds (toolingItem, segs, startIndex, endIndex);
      if (!isValidNotch) CalibrateForCircle (toolingItem, prevToolingItem);
      sw.WriteLine ("X_Correction=0 YZ_Correction=0");
   }

   public void WriteProgramHeader (Tooling toolingItem, List<Point3> pts, /*double frameFeed,*/
      double xStart, double xPartition, double xEnd, bool isFlexCut, bool isLast,
      Tooling prevToolingItem = null, bool isValidNotch = false) {
      string comment = $"** Tooling Name : {toolingItem.Name} - {toolingItem.FeatType} **";
      OutN (sw, mProgramNumber, comment);
      if (isValidNotch || toolingItem.IsCutout ()) mProgramNumber++;
      WriteBlockType (toolingItem, isValidNotch, isFlexCut);
      double SplitEndX = xEnd;
      if (mLastCutScope && isLast) SplitEndX = Process.Workpiece.Bound.XMax;

      sw.WriteLine ("SplitStartX={0} SplitPartitionX={1} SplitEndX={2} ( Cut Scope Length:{3} )",
         xStart.ToString ("F3"), xPartition.ToString ("F3"), SplitEndX.ToString ("F3"), (xEnd - xStart).ToString ("F3"));
      WriteBounds (toolingItem, pts);
      if (!isValidNotch) CalibrateForCircle (toolingItem, prevToolingItem);
      sw.WriteLine ("X_Correction=0 YZ_Correction=0");
   }

   /// <summary>
   /// This method writes START_X and END_X values, that signify the X bounds of the feature.
   /// </summary>
   /// <param name="toolingItem">The input tooling item</param>
   /// <param name="pts">The input set of points for which the bounds need to be written</param>
   public void WriteBounds (Tooling toolingItem, List<Point3> pts) {
      var toolingSegsBounds = Utils.GetPointsBounds (pts);
      var xMin = toolingSegsBounds.XMin; var xMax = toolingSegsBounds.XMax;
      if (LeftToRightMachining)
         sw.WriteLine ($"START_X={xMin:F3} END_X={xMax:F3} PathLength={toolingItem.Perimeter:F2}");
      else
         sw.WriteLine ($"START_X={xMax:F3} END_X={xMin:F3} PathLength={toolingItem.Perimeter:F2}");
   }

   /// <summary>
   /// This method is used to initialize the tooling block for non-edge notches, holes,
   /// cutouts and marks.
   /// </summary>
   /// <param name="toolingItem">The input tooling item</param>
   /// <param name="segs">The list of tooling segments</param>
   /// <param name="startIndex">The start index in the list of tooling items</param>
   /// <param name="endIndex">The end endex in the list of tooling items.</param>
   public void InitializeToolingBlock (Tooling toolingItem, Tooling prevToolingItem, /*double frameFeed,*/
      double xStart, double xPartition, double xEnd, List<ToolingSegment> segs, bool validNotch, bool isFlexCut, bool isLast,
      int startIndex = -1, int endIndex = -1) {
      // ** Tool block initialization **
      //sw.WriteLine ();
      // Now compute the offset based on X
      int offset = 0;
      switch (Utils.GetFlangeType (toolingItem, GetXForm ())) {
         case Utils.EFlange.Top:
            offset = 2;
            sw.WriteLine ("(-----CUTTING ON TOP FLANGE--------)");
            break;
         case Utils.EFlange.Bottom:
            offset = 1;
            sw.WriteLine ("(-----CUTTING ON BOTTOM FLANGE-----)");
            break;
         case Utils.EFlange.Web:
            offset = 3;// toolingItem.ShouldConsiderReverseRef ? 4 : 3; break;
            sw.WriteLine ("(-----CUTTING ON WEB FLANGE--------)");
            break;
      }
      sw.WriteLine ();
      sw.WriteLine ("( ** Tool Block Initialization ** )");
      WriteProgramHeader (toolingItem, segs, /*frameFeed,*/xStart, xPartition, xEnd, isLast, prevToolingItem,
         isValidNotch: validNotch, /*isFlexCut:*/isFlexCut, startIndex, endIndex);

      string sComment = offset switch {
         1 => string.Format ("( ** Machining on the Bottom Flange ** )"),
         2 => string.Format ("( ** Machining on the Top Flange ** )"),
         3 => string.Format ("( ** Machining on the Web Flange ** )"),
         _ => ""
      };
      sw.WriteLine ($"S{offset}\t{sComment}");

      // Output X tool compensation
      if (Utils.GetPlaneType (toolingItem, GetXForm ()) == Utils.EPlane.Top) sw.WriteLine ($"G93 Z0 T1");
      else sw.WriteLine ($"G93 Z=-Head_Height T1");
      WritePlaneForCircularMotionCommand (Utils.GetFlangeType (toolingItem, GetXForm ()));
      sw.WriteLine ("G61\t( Stop Block Preparation )");
      if (toolingItem.IsNotch () || toolingItem.IsCutout () && !toolingItem.IsFlexCutout ())
         sw.WriteLine ("PM=Notch_PM CM=Notch_CM EM=Notch_EM ZRH=Notch_YRH");
      // REVISIT for IsFlexCutout
      else if (toolingItem.IsFlexCutout ())
         sw.WriteLine ("PM=Profile_PM CM=Profile_CM EM=Profile_EM ZRH=Profile_YRH");
      else if (Utils.GetFlangeType (toolingItem, GetXForm ()) == Utils.EFlange.Web)
         sw.WriteLine ("PM=Web_PM CM=Web_CM EM=Web_EM ZRH=Web_ZRH");
      else sw.WriteLine ("PM=Flange_PM CM=Flange_CM EM=Flange_EM ZRH=Flange_YRH\t( Block Process Specific Parametes )");
      sw.WriteLine ("Update_Param\t( Update Cutting Parameters )");
      sw.WriteLine ($"Lead_In={(toolingItem.IsNotch () ? NotchApproachLength :
         ApproachLength):F3}\t( Approach Length )");
      sw.WriteLine ("( ** End - Tool Block Initialization ** )");
      sw.WriteLine ();
   }

   /// <summary>
   /// This method initializes the tooling block of an non-edge notch by specifying
   /// flange type, plane type for arcs, exact position mode, time fed rate
   /// and other parameters. This also writes X bounds for the specific section
   /// </summary>
   /// <param name="toolingItem">The tooling item input</param>
   /// <param name="segs">The segments participating in the specific notch section.
   /// Please refer to the parameters startIndex and endIndex</param>
   /// <param name="segmentNormal">The normal to the set tooling items.</param>
   /// /// <param name="startIndex">The start index of the tooling segments. If startIndex is -1,
   /// then the entire tooling segments will be considered</param>
   /// /// <param name="endIndex">The end index of the tooling segments</param>
   /// /// <param name="circularMotionCmd">If it is true, an appropriate G Code directive 
   /// between G17 or G18 will be written</param>
   /// <param name="comment">User's comment</param>
   public void InitializeNotchToolingBlock (Tooling toolingItem, Tooling prevToolingItem,
      List<ToolingSegment> segs, Vector3 segmentNormal, /*double frameFeed,*/
      double xStart, double xPartition, double xEnd, bool isFlexCut, bool isLast, int startIndex = -1, int endIndex = -1,
      bool circularMotionCmd = true, string comment = "") {
      int offset;
      switch (Utils.GetArcPlaneFlangeType (segmentNormal, GetXForm ())) {
         case Utils.EFlange.Top:
            offset = 2;
            sw.WriteLine ("(-----CUTTING ON TOP FLANGE--------)");
            break;
         case Utils.EFlange.Bottom:
            offset = 1;
            sw.WriteLine ("(-----CUTTING ON BOTTOM FLANGE-----)");
            break;
         case Utils.EFlange.Web:
            offset = 3;// toolingItem.ShouldConsiderReverseRef ? 4 : 3; break;
            sw.WriteLine ("(-----CUTTING ON WEB FLANGE--------)");
            break;
         default: offset = -10; break;
      }
      sw.WriteLine ();
      sw.WriteLine ("( ** Notch: Tool Block Initialization ** )");
      sw.WriteLine ($"({comment})");
      WriteProgramHeader (toolingItem, segs, xStart, xPartition, xEnd, isLast, prevToolingItem, isValidNotch: true, /*isFlexCut:*/ isFlexCut, startIndex, endIndex);

      if (offset > 0) {
         string sComment = offset switch {
            1 => string.Format ("( Machining on the Bottom Flange )"),
            2 => string.Format ("( Machining on the Top Flange )"),
            3 => string.Format ("( Machining on the Web Flange )"),
            _ => ""
         };
         sw.WriteLine ($"S{offset}\t{sComment}");
      }
      // Output X tool compensation
      if (Utils.GetArcPlaneType (segmentNormal, GetXForm ()) == Utils.EPlane.Top) sw.WriteLine ($"G93 Z0 T1");
      else sw.WriteLine ($"G93 Z=-Head_Height T1");
      if (circularMotionCmd) WritePlaneForCircularMotionCommand (Utils.GetArcPlaneFlangeType (segmentNormal, GetXForm ()));
      sw.WriteLine ("G61\t( Stop Block Preparation )");
      sw.WriteLine ("PM=Notch_PM CM=Notch_CM EM=Notch_EM ZRH=Notch_YRH\t( Block Process Specific Parametes )");
      sw.WriteLine ("Update_Param\t( Update Cutting Parameters )");
      sw.WriteLine ($"Lead_In={NotchApproachLength:F3}\t( Approach Length )");
      sw.WriteLine ();
   }

   /// <summary>
   /// This method initializes the tooling block by specifying
   /// flange type, exact position mode, time fed rate
   /// and other parameters. This also writes X bounds for the specific section
   /// </summary>
   /// <param name="toolingItem">The tooling item input</param>
   /// <param name="points">THe set of points that participate in the tooling section. 
   /// This is so in the case of approach to the tooling considering approach length 
   /// and wire joint distance in the case of non-edge notches</param>
   /// <param name="segmentNormal">The normal to the set of points</param>
   /// <param name="comment">User's comment</param>
   public void InitializeNotchToolingBlock (Tooling toolingItem, Tooling prevToolingItem, List<Point3> points,
      Vector3 segmentNormal, /*double frameFeed,*/double xStart, double xPartition, double xEnd, bool isFlexCut, bool isLast, string comment = "") {

      int offset;
      switch (Utils.GetArcPlaneFlangeType (segmentNormal, GetXForm ())) {
         case Utils.EFlange.Top:
            offset = 2;
            sw.WriteLine ("(-----CUTTING ON TOP FLANGE--------)");
            break;
         case Utils.EFlange.Bottom:
            offset = 1;
            sw.WriteLine ("(-----CUTTING ON BOTTOM FLANGE-----)");
            break;
         case Utils.EFlange.Web:
            offset = 3;// toolingItem.ShouldConsiderReverseRef ? 4 : 3; break;
            sw.WriteLine ("(-----CUTTING ON WEB FLANGE--------)");
            break;
         default: offset = -10; break;
      }
      sw.WriteLine ();
      sw.WriteLine ("( ** Notch: Tool Block Initialization ** )");
      sw.WriteLine ($"({comment})");
      WriteProgramHeader (toolingItem, points, xStart, xPartition, xEnd, isLast, isFlexCut, prevToolingItem, isValidNotch: true);

      if (offset > 0) {
         string sComment = offset switch {
            1 => string.Format ("( Machining on the Bottom Flange )"),
            2 => string.Format ("( Machining on the Top Flange )"),
            3 => string.Format ("( Machining on the Web Flange )"),
            _ => ""
         };
         sw.WriteLine ($"S{offset}\t{sComment}");
      }
      // Output X tool compensation
      if (Utils.GetArcPlaneType (segmentNormal, GetXForm ()) == Utils.EPlane.Top) sw.WriteLine ($"G93 Z0 T1");
      else sw.WriteLine ($"G93 Z=-Head_Height T1");

      sw.WriteLine ("G61\t( Stop Block Preparation )");
      sw.WriteLine ("PM=Notch_PM CM=Notch_CM EM=Notch_EM ZRH=Notch_YRH\t( Block Process Specific Parametes )");
      sw.WriteLine ("Update_Param\t( Update Cutting Parameters )");
      sw.WriteLine ($"Lead_In={NotchApproachLength:F3}\t( Approach Length )");
      sw.WriteLine ();
   }

   /// <summary>
   /// FinalizeToolingBlock is to be called at the end of the tooling of types
   /// other than non-edge notches
   /// </summary>
   /// <param name="toolingItem">The input tooling item</param>
   /// <param name="markLength">Mark length (text)</param>
   /// <param name="totalMarkLength">Total Mark Length</param>
   /// <param name="cutLength">cut length of tooling other than </param>
   /// <param name="totalCutLength">Total cut length of the toolings</param>
   public void FinalizeToolingBlock (Tooling toolingItem, double prevCutToolingsLength, double prevMarkToolingsLength,
      double totalMarkLength, double totalCutLength) {
      sw.WriteLine ();
      double percentage = 0;
      double markLength = 0, cutLength;
      if (toolingItem.IsMark ()) {
         //markLength = toolingItem.Perimeter + prevMarkToolingsLength;
         //percentage = markLength / totalMarkLength * 100;
         ;
      } else if (toolingItem.IsHole () || toolingItem.IsCutout () || toolingItem.IsNotch ()) {
         cutLength = toolingItem.Perimeter + prevCutToolingsLength;
         percentage = cutLength / totalCutLength * 100;
      }
      sw.WriteLine ($"G253 E0 F=\"{(toolingItem.IsMark () ? 1 : 2)}=1:1:{percentage.Round (0)}\"");
      sw.WriteLine ("G40 E1\t( Cancel Tool Dia Compensation )"); // Cancel tool diameter compensation
      sw.WriteLine ();
   }

   /// <summary>
   /// FinalizeNotchToolingBlock is exclusive to non-edge Notches. This writes G code 
   /// directive to write overall completion percentage
   /// </summary>
   /// <param name="toolingItem">The tooling Item</param>
   /// <param name="cutLength">The cut length of the notch tooling block</param>
   /// <param name="totalCutLength">Total cut length of the notch (including wire joint length
   /// and notch approach)</param>
   public void FinalizeNotchToolingBlock (Tooling toolingItem,
      double cutLength, double totalCutLength) {
      sw.WriteLine ();
      sw.WriteLine ("( ** Tooling Block Finalization ** )");
      double percentage = (cutLength / totalCutLength) * 100;
      sw.WriteLine ($"G253 E0 F=\"{(toolingItem.IsMark () ? 1 : 2)}=1:1:{percentage:F0}\"");
      sw.WriteLine ("G40 E1"); // Cancel tool diameter compensation
   }


   public void PrepareforToolApproach (Tooling toolingItem, List<ToolingSegment> modifiedToolingSegs,
      ToolingSegment? lastToolingSegment, Tooling prevToolingItem,
      List<ToolingSegment> prevToolingSegs, bool firstTooling, bool isValidNotch) {

      if (firstTooling) MoveToSafety ();
      else if (lastToolingSegment!= null ) MoveToRetract (lastToolingSegment.Value.Curve.End, lastToolingSegment.Value.Vec0, prevToolingItem.Name);
      if (isValidNotch) {
         var notchEntry = Notch.GetNotchEntry (Process.Workpiece.Bound, toolingItem, mPercentLengths,
            NotchApproachLength, NotchWireJointDistance, mCurveLeastLength);
         if (lastToolingSegment != null)
            MoveToNextTooling (lastToolingSegment.Value.Vec0, lastToolingSegment,
            notchEntry.Item1, notchEntry.Item2.Normalized (), prevToolingItem != null ? prevToolingItem.Name : "",
            toolingItem.Name, firstTooling, toolingItem.IsMark ());
         else
            MoveToNextTooling (prevToolingItem != null ? prevToolingItem.End.Vec : new Vector3 (),
               (prevToolingSegs != null && prevToolingSegs.Count > 0) ? prevToolingSegs[^1] : null,
            notchEntry.Item1, notchEntry.Item2.Normalized (), prevToolingItem != null ? prevToolingItem.Name : "",
            toolingItem.Name, firstTooling, toolingItem.IsMark ());
      } else {
         if (lastToolingSegment != null)
            MoveToNextTooling (lastToolingSegment.Value.Vec0, lastToolingSegment,
            modifiedToolingSegs[0].Curve.Start, modifiedToolingSegs[0].Vec0,
            prevToolingItem != null ? prevToolingItem.Name : "",
            toolingItem.Name, firstTooling, toolingItem.IsMark ());
         else
            MoveToNextTooling (prevToolingItem != null ? prevToolingItem.End.Vec : new Vector3 (),
               (prevToolingSegs != null && prevToolingSegs.Count > 0) ? prevToolingSegs[^1] : null,
               modifiedToolingSegs[0].Curve.Start, modifiedToolingSegs[0].Vec0,
               prevToolingItem != null ? prevToolingItem.Name : "",
               toolingItem.Name, firstTooling, toolingItem.IsMark ());
      }
   }

   public void WriteToolCorrectionData (Tooling toolingItem) {
      if (!toolingItem.IsMark ())
         sw.WriteLine ("ToolCorrection\t( Correct Tool Position based on Job )");
      if (Machine == MachineType.LCMLegacy) {
         if (toolingItem.IsCircle ()) sw.WriteLine ($"G{(mXformRHInv[1, 3] < 0.0 ? 41 : 42)} D1 R=TDC E0\t( Tool Dia Compensation)");
      } else if (Machine == MachineType.LCMMultipass2H) {
         if (toolingItem.IsCircle ()) sw.WriteLine ($"G{(mXformRHInv[1, 3] < 0.0 ? 41 : 42)} D1 R=KERF E0\t( Tool Dia Compensation)");
      }
      sw.WriteLine ();
   }
   /// <summary>
   /// This is the main method which prepares the machine with calling various pre-machining
   /// settings/macros, and then calls WriteTooling, which actually calls machining G Codes.
   /// This also adds post processing macros to complete
   /// </summary>
   /// <param name="toolingItems"></param>
   /// <param name="shouldOutputDigit"></param>
   void WriteCuts (List<Tooling> toolingItems, Bound3 bound, /*double frameFeed,*/double xStart, double xPartition,
      double xEnd, bool shouldOutputDigit) {
      Tooling prevToolingItem = null;
      List<ToolingSegment> prevToolingSegs = null;
      bool first = true;
      string traverseM = UsePingPong ? "M1014" : "";
      mProgramNumber = mPgmNo[Utils.EFlange.Web];

      // Compute the total tooling lengths of Hole, Cutouts and Notches
      double totalToolingCutLength = toolingItems.Where (a => (a.IsCutout () || a.IsHole ())).Sum (a => a.Perimeter);

      // For notches, compute the length
      foreach (var ti in toolingItems) {
         if (ti.IsNotch ()) {
            if (Notch.IsEdgeNotch (Process.Workpiece.Bound, ti, mPercentLengths, NotchApproachLength, mCurveLeastLength))
               //totalToolingCutLength += ti.Perimeter;
               continue;
            else {
               totalToolingCutLength += Notch.GetTotalNotchToolingLength (Process.Workpiece.Bound, ti, [0.25, 0.5, 0.75], NotchWireJointDistance,
                  NotchApproachLength, mCurveLeastLength);
            }
         }
      }
      double totalMarkLength = Process.Workpiece.Cuts.Where (a => a.IsMark ()).Sum (a => a.Perimeter);
      ToolingSegment? lastToolingSegment = null;
      double prevCutToolingsLength = 0, prevMarkToolingsLength = 0;

      for (int i = 0; i < toolingItems.Count; i++) {
         ResetNotchCutoutBlockTypes ();
         Tooling toolingItem = toolingItems[i];

         if (Notch.IsEdgeNotch (Process.Workpiece.Bound, toolingItem, mPercentLengths, NotchApproachLength, mCurveLeastLength))
            continue;

         var pr = PartitionRatio;
         var nwjDist = NotchWireJointDistance;
         var nApproachDist = NotchApproachLength;
         // The following switches are for tests.
         if (!Cutouts) if (toolingItem.IsCutout ()) continue;
         if (!CutNotches) if (toolingItem.IsNotch ()) continue;
         if (!CutMarks) if (toolingItem.IsMark ()) continue;
         if (!CutHoles) if (toolingItem.IsHole ()) continue;

         if (first) prevToolingItem = null;
         mProgramNumber = GetProgNo (toolingItem);
         mProgramNumber++;

         // Open shutter and go to program number
         // Output the first program as probing function always
         if (first) {
            string ncname = NCName;
            if (ncname.Length > 20) ncname = ncname[..20];
            sw.WriteLine ($"G253 E0 F=\"0=1:{ncname}:{Math.Round (totalToolingCutLength, 2)}," +
               $"{Math.Round (totalMarkLength, 2)}\"");
            if (shouldOutputDigit)
               sw.WriteLine ("G253 E0 F=\"3=THL RF\"");
         }

         // Output sync for reverse flange reference block
         if (!first &&
            Utils.GetPlaneType (prevToolingItem, GetXForm ()) !=
            Utils.GetPlaneType (toolingItem, GetXForm ()))
            sw.WriteLine ("G4 X2");
         sw.WriteLine ();
         bool isValidNotch = toolingItem.IsNotch () && !Notch.IsEdgeNotch (Process.Workpiece.Bound, toolingItem,
            mPercentLengths, NotchApproachLength, mCurveLeastLength);

         List<ToolingSegment> modifiedToolingSegs = [];

         // Assuming that the starting point of machining on a Cutout does not happen
         // on the flex
         if (toolingItem.IsHole () || toolingItem.IsMark ()) {
            InitializeToolingBlock (toolingItem, prevToolingItem, /*frameFeed,*/
               xStart, xPartition, xEnd, [.. toolingItem.Segs], /*isValidNotch:*/false, /*isFlexCut*/false,
               i == toolingItems.Count - 1);
            modifiedToolingSegs = GetSegmentsAccountedForApproachLength (toolingItem);
            if (modifiedToolingSegs == null || modifiedToolingSegs?.Count == 0) continue;

            PrepareforToolApproach (toolingItem, modifiedToolingSegs, lastToolingSegment, prevToolingItem, prevToolingSegs, first, isValidNotch);
            int CCNo = Utils.GetFlangeType (toolingItem, GetXForm ()) == Utils.EFlange.Web ? WebCCNo : FlangeCCNo;
            if (toolingItem.IsCircle ()) {
               var evalValue = Geom.EvaluateCenterAndRadius (toolingItem.Segs.ToList ()[0].Curve as Arc3);
               if (mControlDiameter.Any (a => a.EQ (2 * evalValue.Item2))) CCNo = 4;
            } else if (toolingItem.IsNotch ()) CCNo = 1;
            int outCCNO = CCNo;
            if (toolingItem.IsFlexCutout ()) outCCNO = 1;

            // Output the Cutting offset. Customer need to cut hole slightly larger than given in geometry
            // We are using G42 than G41 while cutting holes
            // If we are reversing y and not reversing x. We are in 4th quadrant. Flip 42 or 41
            // Tool diameter compensation
            WriteToolCorrectionData (toolingItem);
            if (!toolingItem.IsMark ())
               // ** Machining **
               lastToolingSegment = WriteTooling (modifiedToolingSegs, toolingItem, bound, prevCutToolingsLength, totalToolingCutLength, /*frameFeed*/
                  xStart, xPartition, xEnd);
         } else if (isValidNotch) {
            Utils.EPlane previousPlaneType = Utils.EPlane.None;

            // Write the Notch first
            Notch notch;
            notch = new (toolingItem, bound, Process.Workpiece.Bound, this, prevToolingItem, lastToolingSegment, prevToolingSegs, first, previousPlaneType,
               xStart, xPartition, xEnd, NotchWireJointDistance,
               NotchApproachLength, MinNotchLengthThreshold, mPercentLengths, prevCutToolingsLength, totalToolingCutLength, curveLeastLength: mCurveLeastLength);

            // Write the notch
            notch.WriteTooling ();
            mNotchAttributes.AddRange (NotchAttributes);
            SetProgNo (toolingItem, mProgramNumber);
            lastToolingSegment = notch.Exit;
         } else if (toolingItem.IsCutout ()) {
            CutOut co = new (this, toolingItem, prevToolingItem, prevToolingSegs,
               lastToolingSegment, xStart, xPartition, xEnd, prevCutToolingsLength, prevMarkToolingsLength,
               totalMarkLength, totalToolingCutLength, first);
            co.WriteTooling ();
            SetProgNo (toolingItem, mProgramNumber);
            lastToolingSegment = co.LastToolingSegment;
         }

         // ** Tooling block finalization - Start**
         if (!isValidNotch && !toolingItem.IsCutout ())
            FinalizeToolingBlock (toolingItem, prevCutToolingsLength, prevMarkToolingsLength,
            totalMarkLength, totalToolingCutLength);

         // Compute the cut tooling length
         if (!toolingItem.IsMark ()) {
            if (toolingItem.IsNotch () && isValidNotch)
               prevCutToolingsLength += Notch.GetTotalNotchToolingLength (Process.Workpiece.Bound, toolingItem, [0.25, 0.5, 0.75], NotchWireJointDistance,
                  NotchApproachLength, mCurveLeastLength);
            else prevCutToolingsLength += toolingItem.Perimeter;
         } else prevMarkToolingsLength += toolingItem.Perimeter;

         first = false;
         prevToolingItem = toolingItem;
         prevToolingSegs = modifiedToolingSegs;
         SetProgNo (toolingItem, mProgramNumber);
      }

      //if (toolingItems.Count > 0) SetProgNo (toolingItems.Last (), mProgramNumber);
      // Digit will be made 0 if it doesn't belong to this head
      if (shouldOutputDigit) {
         double x = MarkTextPosX, y = MarkTextPosY;
         var range = GetSerialDigitToOutput ();
         for (int i = range.Item1; i < range.Item2; i++) {
            int progNo = GetDigitProgNo (i) + 1;
            OutN (sw, progNo);
            sw.WriteLine ($"P1763={progNo}");
            if (i == 0) sw.WriteLine ("M58\r\nG61\t( Stop Block Preparation )");
            sw.WriteLine ($":P1707={i}");
            sw.WriteLine ($":P1708={DigitConst}+P{1860 + i}");
            sw.WriteLine ($":P1838=P1661+(P1707*{DigitPitch})+{x:F3} " +
                           $"(X-Axis Actual Distance from Flux)");
            Point3 markTextPoint = new (x, y, 0);
            markTextPoint = XfmToMachine (markTextPoint);
            double yVal = markTextPoint.Y;
            sw.WriteLine ($":P1839=P2005{(Math.Sign (yVal) == 1 ? "+" : "-")}{Math.Abs (yVal)} " +
               $"(Y-Axis Actual Distance from Flux)");
            string mark = PartConfigType == MCSettings.PartConfigType.LHComponent ? "L160" : "L161";
            sw.WriteLine ($"S100\r\nG93 X=P1838 Y=P1839\r\nG22 {mark} J=P1708\r\nG61\t( Stop Block Preparation )\r\n");
         }
      }
   }

   /// <summary>
   /// This method writes the G Code segment for wire joint trace jump(skip). 
   /// The wire joint trace is a set of segments that start from a tooling segment
   /// end point with a rapid move, (G0), reach the position along the outward normal 
   /// on the flange, at a distance of notch approach distance, from the next tooling 
   /// segment's start point and machine (G1) from this point to the point on the tooling segment
   /// </summary>
   /// <param name="endNormalCrv1">The normal at any point of the above</param>
   /// <param name="crv2">The underlying curve of the next tooling segment</param>
   /// <param name="stNormalCrv2">The normal at the starting point of the curve above</param>
   /// <param name="scrapSideNormalCrv2">The scrap side direction from the start of the next 
   /// curve of the next segment</param>
   /// <param name="notchApproachDistance">The notch approach distance</param>
   /// <param name="prevPlaneType"></param>
   /// <param name="currFlangeType"></param>
   /// <param name="toolingName"></param>
   public void WriteWireJointTraceForNotch (Vector3 endNormalCrv1,
      Curve3 crv2, Vector3 stNormalCrv2, Vector3 scrapSideNormalCrv2,
      double notchApproachDistance, ref Utils.EPlane prevPlaneType, Utils.EFlange currFlangeType, string toolingName) {
      Utils.EPlane currPlaneType = Utils.GetArcPlaneType (endNormalCrv1, GetXForm ());
      var nextMachiningStart = crv2.Start + scrapSideNormalCrv2.Normalized () * notchApproachDistance;
      RapidPositionWithClearance (nextMachiningStart, stNormalCrv2, mRetractClearance, toolingName, isMark: false);
      MoveToMachiningStartPosition (nextMachiningStart, stNormalCrv2, toolingName);
      prevPlaneType = currPlaneType;

      // Honouring notch approach distance 
      WriteLine (crv2.Start, stNormalCrv2, stNormalCrv2, currPlaneType, prevPlaneType,
         currFlangeType, toolingName);
   }

   /// <summary>
   /// This method positions the tool head exactly at the starting position 
   /// of the next tooling segment, but with a distance "clearance" along the 
   /// starting normal.
   /// </summary>
   /// <param name="toPoint">The next point of the tooling segment</param>
   /// <param name="endNormal">The normal at the next tooling starting point</param>
   /// <param name="clearance">A distance along the normal at the point</param>
   /// <param name="toolingName">Tooling name</param>
   public void RapidPositionWithClearance (Point3 toPoint, Vector3 endNormal, double clearance, string toolingName, bool isMark) {
      var toPointOffset =
             Utils.MovePoint (toPoint, endNormal, clearance);
      var angle = Utils.GetAngleAboutXAxis (XForm4.mZAxis, endNormal, GetXForm ()).R2D ();
      var mcCoordsToPointOffset = XfmToMachine (toPointOffset);
      //sw.WriteLine ("G0 X{0} Y{1} Z{2} A{3}", mcCoordsToPointOffset.X.ToString ("F3"),
      //   mcCoordsToPointOffset.Y.ToString ("F3"), mcCoordsToPointOffset.Z.ToString ("F3"), angle.ToString ("F3"));
      if (!isMark) {
         Utils.RapidPosition (sw, mcCoordsToPointOffset.X, mcCoordsToPointOffset.Y, mcCoordsToPointOffset.Z, angle,
            machine: Machine, slaveRun: IsDryRun);
         mTraces[ToolHead].Add (new (mToolPos[ToolHead], toPointOffset, endNormal, endNormal,
           EGCode.G0, EMove.RapidPosition, toolingName));
         mToolPos[ToolHead] = toPointOffset;
         mToolVec[ToolHead] = endNormal.Normalized ();
      }
   }

   /// <summary>
   /// This method writes the G Code segment for wire joint trace jump(skip). 
   /// The wire joint trace is a set of segments that start from a tooling segment
   /// end point with a rapid move, (G0), reach the position along the outward normal 
   /// on the flange, at a distance of notch approach distance, from the next tooling 
   /// segment's start point and machine (G1) from this point to the point on the tooling segment
   /// </summary>
   /// <param name="nextSegmentStartPoint">The starting point of the next tooling segment</param>
   /// <param name="stNormal">The start normal at the above point</param>
   /// <param name="endNormal">The end normal at the above point</param>
   /// <param name="scrapSideNormal">The direction in which the scrappable material exists</param>
   /// <param name="lastPosition">The last position of the tool head (from previous stroke)</param>
   /// <param name="notchApproachDistance">The notch approach distance</param>
   /// <param name="prevPlaneType">The previous plane type YPos, YNeg, or Top (for angle computation 
   /// about X axis)</param>
   /// <param name="currFlangeType">Web, Top or Bottom(for angle computation about X Axis)</param>
   /// <param name="toolingItem">The current tooling item</param>
   /// <param name="blockCutLength">The machining distance of the current wire joint trace</param>
   /// <param name="totalCutLength">The total machining length (of the notch)</param>
   /// <param name="comment">Comment to be written in G Code</param>
   public void WriteWireJointTraceForNotch (Point3 nextSegmentStartPoint, Vector3 stNormal, Vector3 endNormal, Vector3 scrapSideNormal,
      Point3 lastPosition, double notchApproachDistance, ref Utils.EPlane prevPlaneType, Utils.EFlange currFlangeType, Tooling toolingItem,
      ref double blockCutLength, double totalCutLength, /*double frameFeed,*/
      double xStart, double xPartition, double xEnd, string comment = "Notch: Wire Joint Jump Trace") {
      Utils.EPlane currPlaneType = Utils.GetArcPlaneType (endNormal, GetXForm ());
      var nextMachiningStart = nextSegmentStartPoint + scrapSideNormal.Normalized () * notchApproachDistance;
      RapidPositionWithClearance (nextMachiningStart, stNormal, mRetractClearance, toolingItem.Name, isMark: false);
      MoveToMachiningStartPosition (nextMachiningStart, stNormal, toolingItem.Name);
      prevPlaneType = currPlaneType;
      var fromPt = GetLastToolHeadPosition ().Item1;
      List<Point3> pts = [];
      pts.Add (nextMachiningStart);
      pts.Add (nextSegmentStartPoint);
      pts.Add (lastPosition);
      InitializeNotchToolingBlock (toolingItem, prevToolingItem: null, pts, endNormal, xStart, xPartition, xEnd, /*isFlexCut:*/ false, isLast: false, comment);
      {
         EnableMachiningDirective ();
         WriteLine (nextSegmentStartPoint, endNormal, endNormal, currPlaneType, prevPlaneType,
            currFlangeType, toolingItem.Name);
         DisableMachiningDirective ();
         blockCutLength += mToolPos[ToolHead].DistTo (fromPt);
      }
      FinalizeNotchToolingBlock (toolingItem, blockCutLength, totalCutLength);
   }

   /// <summary>
   /// This method is used to write G Code that moves the 
   /// tool head from current end of the tooling to the next tooling segment
   /// </summary>
   /// <param name="prevToolingEndNormal">The normal at the previous end</param>
   /// <param name="prevToolingEndSegment">The previous end segment</param>
   /// <param name="nextToolingStartPoint">The start point on the next tooling segment</param>
   /// <param name="nextToolingStartNormal">The start normal of the next tooling</param>
   /// <param name="nextToolingFlangeType">The Flange type of the next tooling segment</param>
   /// <param name="prevToolingItemName">The name of the previous tooling stroke</param>
   /// <param name="nextToolingItemName">The name of the current tooling stroke.</param>
   /// <param name="firstTime">A boolean flag that tells if the tooling item is the first one to start with.
   /// This is used for angle computation</param>
   public void MoveToNextTooling (Vector3 prevToolingEndNormal, ToolingSegment? prevToolingEndSegment,
      Point3 nextToolingStartPoint, Vector3 nextToolingStartNormal, string prevToolingItemName,
      string nextToolingItemName, bool firstTime, bool isMark) {
      double changeInAngle;
      if (firstTime) changeInAngle = Utils.GetAngleAboutXAxis (XForm4.mZAxis, nextToolingStartNormal,
         GetXForm ()).R2D ();
      else changeInAngle = Utils.GetAngleAboutXAxis (prevToolingEndNormal, nextToolingStartNormal,
         GetXForm ()).R2D ();

      bool movedToCurrToolingRetractedPos = false;
      bool planeChangeNeeded = false;
      if (!changeInAngle.LieWithin (-10.0, 10.0)) {
         planeChangeNeeded = true;
         if (!isMark) sw.WriteLine ("PlaneTransfer\t( Enable Plane Transformation for Tool TurnOver )");
         MoveFromRetractToSafety (prevToolingEndSegment,
            prevToolingItemName, nextToolingStartPoint,
         nextToolingStartNormal, nextToolingItemName, isMark);
         MoveFromSafetyToRetract (nextToolingStartPoint,
         nextToolingStartNormal, nextToolingItemName, planeChangeNeeded, isMark);
         movedToCurrToolingRetractedPos = true;
         sw.WriteLine ("EndPlaneTransfer\t( Disable Plane Transformation after Tool TurnOver)");
      } else {
         if (!isMark)
            sw.WriteLine ("ToolPlane\t( Confirm Cutting Plane )");
      }

      if (!movedToCurrToolingRetractedPos)
         MoveFromSafetyToRetract (nextToolingStartPoint,
            nextToolingStartNormal, nextToolingItemName, planeChangeNeeded, isMark);
   }

   /// <summary>
   /// This method makes the tool move from previous tooling retract position, which is 
   /// previous tooling end position away from the position by end normal of the previous tooling
   /// TO the position, whose coordinates are X of the next tooling, Y of the next tooling and Z as safety
   /// value (28 mm).
   /// </summary>
   /// <param name="prevToolingSegs">Segments of the previous tooling</param>
   /// <param name="prevToolingName">Name of the previous tooling</param>
   /// <param name="currToolingSegs">Segments of the current tooling.</param>
   /// <param name="currentToolingName">Name of the current tooling</param>
   public void MoveFromRetractToSafety (ToolingSegment? prevToolingLastSegment, string prevToolingName,
      Point3 currToolingStPoint, Vector3 currToolingStNormal, string currentToolingName, bool isMark) {
      if (prevToolingLastSegment != null) {
         (var prevSegEndCurve, _, var prevSegEndCurveEndNormal) = prevToolingLastSegment.Value;
         var prevToolingEPRetracted =
                Utils.MovePoint (prevSegEndCurve.End, prevSegEndCurveEndNormal, mRetractClearance);
         Point3 prevToolingEPRetractedSafeZ = new (prevToolingEPRetracted.X, prevToolingEPRetracted.Y,
            mSafeClearance);
         var mcCoordsPrevToolingEPRetractedSafeZ = XfmToMachine (prevToolingEPRetractedSafeZ);
         if (!isMark)
            Utils.LinearMachining (sw, mcCoordsPrevToolingEPRetractedSafeZ.X, mcCoordsPrevToolingEPRetractedSafeZ.Y,
               mcCoordsPrevToolingEPRetractedSafeZ.Z, 0, Rapid, comment: "", machine: Machine, slaveRun: IsDryRun);
         mTraces[ToolHead].Add (new GCodeSeg (mToolPos[ToolHead], prevToolingEPRetractedSafeZ, mToolVec[ToolHead],
            XForm4.mZAxis, EGCode.G0, EMove.Retract2SafeZ, prevToolingName));
         mToolPos[ToolHead] = prevToolingEPRetractedSafeZ;
         mToolVec[ToolHead] = XForm4.mZAxis;
      }

      // Move to the current tooling item start posotion safeZ
      var currToolingSPRetracted =
             Utils.MovePoint (currToolingStPoint, currToolingStNormal, mRetractClearance);
      Point3 currToolingSPRetractedSafeZ = new (currToolingSPRetracted.X, currToolingSPRetracted.Y,
         mSafeClearance);
      var mcCoordsCurrToolingSPRetractedSafeZ = XfmToMachine (currToolingSPRetractedSafeZ);
      if (!isMark) {
         Utils.RapidPosition (sw, mcCoordsCurrToolingSPRetractedSafeZ.X, mcCoordsCurrToolingSPRetractedSafeZ.Y,
            mcCoordsCurrToolingSPRetractedSafeZ.Z, 0, machine: Machine, slaveRun: IsDryRun);
         mTraces[ToolHead].Add (new (mToolPos[ToolHead], currToolingSPRetractedSafeZ, mToolVec[ToolHead], XForm4.mZAxis, EGCode.G0,
            EMove.SafeZ2SafeZ, currentToolingName));
         mToolPos[ToolHead] = currToolingSPRetractedSafeZ;
         mToolVec[ToolHead] = XForm4.mZAxis;
      }
   }

   /// <summary>
   /// Thius method gets the last position of the head
   /// </summary>
   /// <returns>The last position of the tool head</returns>
   public Tuple<Point3, Vector3> GetLastToolHeadPosition () {
      return new Tuple<Point3, Vector3> (mToolPos[ToolHead], mToolVec[ToolHead]);
   }

   // Tuple<Start, End> Start inclusive and End exclusive
   // That is in index format
   Tuple<int, int> GetSerialDigitToOutput () => Tuple.Create (0, (int)SerialNumber);
   static int GetDigitProgNo (int digitNo) => DigitProg + digitNo;
   #endregion

   public bool IsOppositeReference (string toolingName) {
      if (WPOptions == null) {
         if (string.IsNullOrEmpty (WorkpieceOptionsFilename)) return false;
         try {
            string json = File.ReadAllText (WorkpieceOptionsFilename);
            var data = JsonSerializer.Deserialize<List<WorkpieceOptions>> (json);
            WPOptions = [];
            foreach (var item in data) {
               WPOptions[item.FileName] = item;
            }
         } catch (Exception) {
            return false;
         }
      }
      var WPOptionsForTooling = GetWorkpieceOptions ();
      return WPOptionsForTooling?.IsOppositeReference (toolingName) ?? false;
   }

   public WorkpieceOptions? GetWorkpieceOptions () {
      if (WPOptions.TryGetValue (NCName, out var workpieceOptions)) return workpieceOptions;
      return null;
   }
}

public struct WorkpieceOptions {
   [JsonPropertyName ("FileName")]
   public string FileName { get; set; }

   [JsonPropertyName ("OppositeReference")]
   public string[] OppositeReference { get; set; }
   public readonly bool IsOppositeReference (string input) {
      if (Array.Exists (OppositeReference, element => element == input)) return true;
      return false;
   }
}