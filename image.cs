// ============================================================
//  VehiclePartInspection.cs
//  Full detection pipeline — single file
//  Dependencies: OpenCvSharp4, OpenCvSharp4.runtime.win
//                System.Text.Json (built-in .NET 6+)
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCvSharp;

namespace VehicleInspection
{
    // ============================================================
    // SECTION 1 — PROFILE MODELS
    // Maps directly to your JSON config structure
    // ============================================================

    public class InspectionProfile
    {
        [JsonPropertyName("profile_id")]
        public string ProfileId { get; set; }

        [JsonPropertyName("part_name")]
        public string PartName { get; set; }

        [JsonPropertyName("plant")]
        public string Plant { get; set; }

        [JsonPropertyName("camera")]
        public CameraConfig Camera { get; set; }

        [JsonPropertyName("preprocessing")]
        public PreprocessingConfig Preprocessing { get; set; }

        [JsonPropertyName("anchors")]
        public List<AnchorConfig> Anchors { get; set; }

        [JsonPropertyName("zones")]
        public List<ZoneConfig> Zones { get; set; }

        [JsonPropertyName("ransac_min_inliers")]
        public int RansacMinInliers { get; set; }

        [JsonPropertyName("uncertain_band")]
        public double[] UncertainBand { get; set; }

        // Load profile from JSON file
        public static InspectionProfile LoadFromFile(string jsonPath)
        {
            string json = File.ReadAllText(jsonPath);
            return JsonSerializer.Deserialize<InspectionProfile>(json)
                   ?? throw new InvalidOperationException("Failed to deserialize profile.");
        }
    }

    public class CameraConfig
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("resolution")]
        public int[] Resolution { get; set; }
    }

    public class PreprocessingConfig
    {
        [JsonPropertyName("clahe_clip_limit")]
        public double ClaheClipLimit { get; set; }

        [JsonPropertyName("clahe_tile_size")]
        public int[] ClaheTileSize { get; set; }

        [JsonPropertyName("gaussian_blur")]
        public int[] GaussianBlur { get; set; }

        [JsonPropertyName("canny_low")]
        public double CannyLow { get; set; }

        [JsonPropertyName("canny_high")]
        public double CannyHigh { get; set; }
    }

    public class AnchorConfig
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; }

        [JsonPropertyName("master_position")]
        public int[] MasterPosition { get; set; }   // [x, y]

        [JsonPropertyName("patch_size")]
        public int[] PatchSize { get; set; }         // [w, h]

        [JsonPropertyName("search_window")]
        public int SearchWindow { get; set; }        // ± pixels
    }

    public class ZoneConfig
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; }

        [JsonPropertyName("bbox")]
        public int[] Bbox { get; set; }             // [x, y, w, h]

        [JsonPropertyName("threshold")]
        public double Threshold { get; set; }        // baseline edge density

        [JsonPropertyName("criticality")]
        public string Criticality { get; set; }      // "high" | "medium" | "low"
    }

    // ============================================================
    // SECTION 2 — RESULT MODELS
    // ============================================================

    public enum ZoneDecision { Present, Missing, Uncertain }

    public class AlignmentResult
    {
        public bool Success { get; set; }
        public int InlierCount { get; set; }
        public string FailureReason { get; set; }
    }

    public class ZoneResult
    {
        public string ZoneId { get; set; }
        public string Label { get; set; }
        public string Criticality { get; set; }
        public double EdgeDensity { get; set; }
        public double Threshold { get; set; }
        public double Ratio { get; set; }            // edgeDensity / threshold
        public ZoneDecision Decision { get; set; }
    }

    public class InspectionResult
    {
        public string InspectionId { get; set; }
        public string ProfileId { get; set; }
        public string PartName { get; set; }
        public string Plant { get; set; }
        public DateTime Timestamp { get; set; }
        public AlignmentResult Alignment { get; set; }
        public List<ZoneResult> Zones { get; set; } = new();
        public bool OverallPass { get; set; }
        public bool OperatorReviewRequired { get; set; }

        // Serialize result to JSON string
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            });
        }
    }

    // ============================================================
    // SECTION 3 — ALIGNMENT MODULE
    // Extracts anchor patches from master, finds them in captured
    // image, computes RANSAC homography, warps captured image
    // ============================================================

    public class AlignmentModule : IDisposable
    {
        private readonly InspectionProfile _profile;
        private readonly List<(AnchorConfig config, Mat patch)> _anchorPatches = new();
        private bool _disposed = false;

        public AlignmentModule(InspectionProfile profile, Mat masterImage)
        {
            _profile = profile;
            ExtractAnchorPatches(masterImage);
        }

        // Cut anchor patches from master image once at startup
        private void ExtractAnchorPatches(Mat masterImage)
        {
            foreach (var anchor in _profile.Anchors)
            {
                int x = anchor.MasterPosition[0] - anchor.PatchSize[0] / 2;
                int y = anchor.MasterPosition[1] - anchor.PatchSize[1] / 2;
                int w = anchor.PatchSize[0];
                int h = anchor.PatchSize[1];

                // Clamp to image bounds
                x = Math.Max(0, x);
                y = Math.Max(0, y);
                w = Math.Min(w, masterImage.Width - x);
                h = Math.Min(h, masterImage.Height - y);

                var rect = new Rect(x, y, w, h);
                Mat patch = new Mat(masterImage, rect).Clone();
                _anchorPatches.Add((anchor, patch));

                Console.WriteLine($"[Alignment] Extracted anchor patch: {anchor.Id} " +
                                  $"at [{anchor.MasterPosition[0]},{anchor.MasterPosition[1]}]");
            }
        }

        // Find anchor positions in captured image using constrained template matching
        private List<(Point2f master, Point2f captured, double confidence)>
            FindAnchorMatches(Mat capturedImage)
        {
            var matches = new List<(Point2f, Point2f, double)>();

            foreach (var (anchor, patch) in _anchorPatches)
            {
                int sw = anchor.SearchWindow;
                int pw = anchor.PatchSize[0];
                int ph = anchor.PatchSize[1];

                // Search region = expected position ± search_window
                int sx = anchor.MasterPosition[0] - pw / 2 - sw;
                int sy = anchor.MasterPosition[1] - ph / 2 - sw;
                int sWidth  = pw + sw * 2;
                int sHeight = ph + sw * 2;

                // Clamp search region to image bounds
                sx = Math.Max(0, sx);
                sy = Math.Max(0, sy);
                sWidth  = Math.Min(sWidth,  capturedImage.Width  - sx);
                sHeight = Math.Min(sHeight, capturedImage.Height - sy);

                if (sWidth <= pw || sHeight <= ph)
                {
                    Console.WriteLine($"[Alignment] WARNING: Search region too small for {anchor.Id}, skipping.");
                    continue;
                }

                var searchRect = new Rect(sx, sy, sWidth, sHeight);

                using Mat searchRegion = new Mat(capturedImage, searchRect);
                using Mat matchResult  = new Mat();

                // Normalized cross-correlation — illumination invariant
                Cv2.MatchTemplate(searchRegion, patch, matchResult,
                    TemplateMatchModes.CCoeffNormed);

                Cv2.MinMaxLoc(matchResult,
                    out _, out double maxVal,
                    out _, out Point maxLoc);

                // Convert maxLoc (relative to searchRegion) to full image coordinates
                // maxLoc is the top-left of the matched patch, so center it
                float foundX = sx + maxLoc.X + pw / 2f;
                float foundY = sy + maxLoc.Y + ph / 2f;

                Point2f masterPt   = new(anchor.MasterPosition[0], anchor.MasterPosition[1]);
                Point2f capturedPt = new(foundX, foundY);

                matches.Add((masterPt, capturedPt, maxVal));

                Console.WriteLine($"[Alignment] Anchor {anchor.Id}: " +
                                  $"found at [{foundX:F1},{foundY:F1}] " +
                                  $"confidence={maxVal:F3}");
            }

            return matches;
        }

        // Compute homography from anchor matches using RANSAC
        // Returns warped image aligned to master, or null if alignment fails
        public (Mat warpedImage, AlignmentResult result) Align(Mat capturedImage)
        {
            var matches = FindAnchorMatches(capturedImage);

            if (matches.Count < 4)
            {
                return (null, new AlignmentResult
                {
                    Success       = false,
                    InlierCount   = 0,
                    FailureReason = $"Only {matches.Count} anchors found. Minimum 4 required for homography."
                });
            }

            // Filter out low-confidence matches before passing to RANSAC
            // Confidence < 0.5 means template matching was uncertain
            var goodMatches = matches.Where(m => m.confidence > 0.5).ToList();

            if (goodMatches.Count < 4)
            {
                return (null, new AlignmentResult
                {
                    Success       = false,
                    InlierCount   = 0,
                    FailureReason = $"Only {goodMatches.Count} high-confidence anchor matches. Minimum 4 required."
                });
            }

            Point2f[] masterPts   = goodMatches.Select(m => m.master).ToArray();
            Point2f[] capturedPts = goodMatches.Select(m => m.captured).ToArray();

            // RANSAC homography — reprojThreshold=3px, finds best fitting transform
            using Mat inlierMask = new Mat();
            using Mat H = Cv2.FindHomography(
                capturedPts,
                masterPts,
                HomographyMethods.Ransac,
                ransacReprojThreshold: 3.0,
                mask: inlierMask);

            if (H == null || H.Empty())
            {
                return (null, new AlignmentResult
                {
                    Success       = false,
                    InlierCount   = 0,
                    FailureReason = "Homography computation failed — degenerate point configuration."
                });
            }

            int inliers = Cv2.CountNonZero(inlierMask);
            Console.WriteLine($"[Alignment] RANSAC inliers: {inliers} / {goodMatches.Count}");

            if (inliers < _profile.RansacMinInliers)
            {
                return (null, new AlignmentResult
                {
                    Success       = false,
                    InlierCount   = inliers,
                    FailureReason = $"RANSAC inliers ({inliers}) below minimum ({_profile.RansacMinInliers})."
                });
            }

            // Warp captured image to match master perspective
            Mat warped = new Mat();
            Cv2.WarpPerspective(capturedImage, warped, H,
                new Size(capturedImage.Width, capturedImage.Height));

            Console.WriteLine($"[Alignment] SUCCESS — image warped. Inliers: {inliers}");

            return (warped, new AlignmentResult
            {
                Success     = true,
                InlierCount = inliers
            });
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var (_, patch) in _anchorPatches)
                    patch?.Dispose();
                _disposed = true;
            }
        }
    }

    // ============================================================
    // SECTION 4 — PREPROCESSING MODULE
    // Grayscale → CLAHE → Gaussian blur → ready for detection
    // ============================================================

    public static class Preprocessor
    {
        public static Mat Preprocess(Mat image, PreprocessingConfig config)
        {
            // Step 1 — Grayscale
            using Mat gray = new Mat();
            if (image.Channels() == 3 || image.Channels() == 4)
                Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
            else
                image.CopyTo(gray);

            // Step 2 — CLAHE (normalizes local contrast, kills exposure variance)
            using var clahe = Cv2.CreateCLAHE(
                clipLimit:    config.ClaheClipLimit,
                tileGridSize: new Size(config.ClaheTileSize[0], config.ClaheTileSize[1]));

            using Mat claheResult = new Mat();
            clahe.Apply(gray, claheResult);

            // Step 3 — Gaussian blur (removes sensor noise before edge detection)
            Mat blurred = new Mat();
            Cv2.GaussianBlur(claheResult, blurred,
                new Size(config.GaussianBlur[0], config.GaussianBlur[1]), 0);

            Console.WriteLine($"[Preprocess] CLAHE(clip={config.ClaheClipLimit}, " +
                              $"tile={config.ClaheTileSize[0]}x{config.ClaheTileSize[1]}) " +
                              $"+ Blur({config.GaussianBlur[0]}x{config.GaussianBlur[1]}) applied.");

            return blurred; // caller owns this Mat — must dispose
        }
    }

    // ============================================================
    // SECTION 5 — DETECTION ENGINE
    // Per-zone: Canny edges → edge density → threshold decision
    // ============================================================

    public class DetectionEngine
    {
        private readonly InspectionProfile _profile;

        public DetectionEngine(InspectionProfile profile)
        {
            _profile = profile;
        }

        // Run detection on all zones in the profile
        public List<ZoneResult> DetectAllZones(Mat preprocessedImage)
        {
            var results = new List<ZoneResult>();

            foreach (var zone in _profile.Zones)
            {
                var result = DetectZone(preprocessedImage, zone);
                results.Add(result);

                Console.WriteLine($"[Detection] Zone {zone.Id} ({zone.Label}): " +
                                  $"density={result.EdgeDensity:F4} " +
                                  $"threshold={zone.Threshold:F4} " +
                                  $"ratio={result.Ratio:F3} → {result.Decision}");
            }

            return results;
        }

        // Detect single zone — returns ZoneResult
        private ZoneResult DetectZone(Mat preprocessedImage, ZoneConfig zone)
        {
            // Crop zone from preprocessed image
            var bbox = new Rect(zone.Bbox[0], zone.Bbox[1], zone.Bbox[2], zone.Bbox[3]);

            // Safety clamp — prevent out-of-bounds crop
            bbox = ClampRect(bbox, preprocessedImage.Size());

            using Mat crop  = new Mat(preprocessedImage, bbox);
            using Mat edges = new Mat();

            // Canny edge detection using profile thresholds
            Cv2.Canny(crop, edges,
                _profile.Preprocessing.CannyLow,
                _profile.Preprocessing.CannyHigh);

            // Edge density = edge pixels / total pixels in zone
            int totalPixels = bbox.Width * bbox.Height;
            int edgePixels  = Cv2.CountNonZero(edges);
            double edgeDensity = totalPixels > 0
                ? (double)edgePixels / totalPixels
                : 0.0;

            // Ratio = how far current density is from baseline threshold
            // ratio > 1  →  more edges than baseline (part likely present)
            // ratio < 1  →  fewer edges than baseline (part may be missing)
            double ratio = zone.Threshold > 0
                ? edgeDensity / zone.Threshold
                : 0.0;

            // Apply uncertain band from profile: [0.85, 1.15]
            ZoneDecision decision;
            if (ratio > _profile.UncertainBand[1])
                decision = ZoneDecision.Present;
            else if (ratio < _profile.UncertainBand[0])
                decision = ZoneDecision.Missing;
            else
                decision = ZoneDecision.Uncertain;

            return new ZoneResult
            {
                ZoneId      = zone.Id,
                Label       = zone.Label,
                Criticality = zone.Criticality,
                EdgeDensity = Math.Round(edgeDensity, 6),
                Threshold   = zone.Threshold,
                Ratio       = Math.Round(ratio, 4),
                Decision    = decision
            };
        }

        // Clamp rect to stay within image bounds
        private static Rect ClampRect(Rect r, Size imageSize)
        {
            int x = Math.Max(0, Math.Min(r.X, imageSize.Width  - 1));
            int y = Math.Max(0, Math.Min(r.Y, imageSize.Height - 1));
            int w = Math.Min(r.Width,  imageSize.Width  - x);
            int h = Math.Min(r.Height, imageSize.Height - y);
            return new Rect(x, y, w, h);
        }
    }

    // ============================================================
    // SECTION 6 — RESULT RENDERER
    // Draws colored overlay on image — green=present, red=missing,
    // orange=uncertain. Saves annotated image to disk.
    // ============================================================

    public static class ResultRenderer
    {
        // Zone border thickness
        private const int BorderThickness = 2;
        // Text scale
        private const double FontScale = 0.45;

        public static Mat DrawOverlay(Mat originalImage, List<ZoneResult> zoneResults,
            InspectionProfile profile)
        {
            Mat overlay = originalImage.Clone();

            foreach (var result in zoneResults)
            {
                var zone = profile.Zones.First(z => z.Id == result.ZoneId);
                var bbox = new Rect(zone.Bbox[0], zone.Bbox[1], zone.Bbox[2], zone.Bbox[3]);

                // Color by decision
                Scalar color = result.Decision switch
                {
                    ZoneDecision.Present   => new Scalar(0, 200, 0),    // green
                    ZoneDecision.Missing   => new Scalar(0, 0, 220),    // red (BGR)
                    ZoneDecision.Uncertain => new Scalar(0, 140, 255),  // orange
                    _                      => new Scalar(200, 200, 200)
                };

                // Draw zone rectangle
                Cv2.Rectangle(overlay, bbox, color, BorderThickness);

                // Label above the box
                string label = $"{result.Label} [{result.Decision}]";
                var labelPos = new Point(bbox.X, Math.Max(0, bbox.Y - 5));

                // Dark background behind text for readability
                Size textSize = Cv2.GetTextSize(label, HersheyFonts.HersheySimplex,
                    FontScale, 1, out int baseline);
                Cv2.Rectangle(overlay,
                    new Rect(labelPos.X, labelPos.Y - textSize.Height - 2,
                             textSize.Width, textSize.Height + baseline + 2),
                    new Scalar(0, 0, 0), -1);

                Cv2.PutText(overlay, label, labelPos,
                    HersheyFonts.HersheySimplex, FontScale, color, 1);

                // Small ratio text inside the box
                string ratioText = $"r={result.Ratio:F2}";
                Cv2.PutText(overlay, ratioText,
                    new Point(bbox.X + 2, bbox.Y + 12),
                    HersheyFonts.HersheySimplex, FontScale - 0.05,
                    color, 1);
            }

            // Summary banner at top of image
            int missing   = zoneResults.Count(z => z.Decision == ZoneDecision.Missing);
            int uncertain = zoneResults.Count(z => z.Decision == ZoneDecision.Uncertain);
            int present   = zoneResults.Count(z => z.Decision == ZoneDecision.Present);

            string summary = $"PRESENT:{present}  MISSING:{missing}  UNCERTAIN:{uncertain}  " +
                             $"| {profile.PartName} | {profile.Plant}";

            Scalar bannerColor = missing > 0
                ? new Scalar(0, 0, 200)   // red banner if any missing
                : uncertain > 0
                    ? new Scalar(0, 120, 255) // orange if uncertain
                    : new Scalar(0, 160, 0);  // green if all clear

            Cv2.Rectangle(overlay, new Rect(0, 0, overlay.Width, 28),
                new Scalar(0, 0, 0), -1);
            Cv2.PutText(overlay, summary, new Point(8, 18),
                HersheyFonts.HersheySimplex, 0.55, bannerColor, 1);

            return overlay;
        }

        public static void SaveOverlay(Mat overlay, string outputPath)
        {
            Cv2.ImWrite(outputPath, overlay);
            Console.WriteLine($"[Renderer] Overlay saved to: {outputPath}");
        }
    }

    // ============================================================
    // SECTION 7 — INSPECTION ORCHESTRATOR
    // Top-level class — plug in master image + captured image,
    // get back InspectionResult + annotated overlay image
    // ============================================================

    public class InspectionOrchestrator : IDisposable
    {
        private readonly InspectionProfile _profile;
        private readonly AlignmentModule   _alignmentModule;
        private readonly DetectionEngine   _detectionEngine;
        private readonly Mat               _masterImage;
        private bool _disposed = false;

        public InspectionOrchestrator(string profileJsonPath, string masterImagePath)
        {
            // Load profile
            _profile = InspectionProfile.LoadFromFile(profileJsonPath);
            Console.WriteLine($"[Orchestrator] Profile loaded: {_profile.ProfileId}");

            // Load master image
            _masterImage = Cv2.ImRead(masterImagePath, ImreadModes.Color);
            if (_masterImage.Empty())
                throw new FileNotFoundException($"Master image not found: {masterImagePath}");

            Console.WriteLine($"[Orchestrator] Master image loaded: " +
                              $"{_masterImage.Width}x{_masterImage.Height}");

            // Initialize modules
            _alignmentModule = new AlignmentModule(_profile, _masterImage);
            _detectionEngine = new DetectionEngine(_profile);
        }

        // Run full inspection on a captured image file path
        public (InspectionResult result, Mat overlayImage)
            Inspect(string capturedImagePath)
        {
            using Mat captured = Cv2.ImRead(capturedImagePath, ImreadModes.Color);
            if (captured.Empty())
                throw new FileNotFoundException($"Captured image not found: {capturedImagePath}");

            return Inspect(captured);
        }

        // Run full inspection on a Mat (use when camera feeds Mat directly)
        public (InspectionResult result, Mat overlayImage) Inspect(Mat capturedImage)
        {
            string inspectionId = $"INS_{DateTime.Now:yyyyMMdd_HHmmss_fff}";

            Console.WriteLine($"\n[Orchestrator] === Starting inspection: {inspectionId} ===");

            var inspectionResult = new InspectionResult
            {
                InspectionId = inspectionId,
                ProfileId    = _profile.ProfileId,
                PartName     = _profile.PartName,
                Plant        = _profile.Plant,
                Timestamp    = DateTime.UtcNow
            };

            // ── STEP 1: ALIGNMENT ──────────────────────────────────
            Console.WriteLine("[Orchestrator] Step 1: Alignment");
            var (warped, alignmentResult) = _alignmentModule.Align(capturedImage);

            inspectionResult.Alignment = alignmentResult;

            if (!alignmentResult.Success)
            {
                Console.WriteLine($"[Orchestrator] ALIGNMENT FAILED: {alignmentResult.FailureReason}");

                inspectionResult.OverallPass            = false;
                inspectionResult.OperatorReviewRequired = true;

                // Return original image with failure banner
                using Mat failOverlay = capturedImage.Clone();
                Cv2.Rectangle(failOverlay, new Rect(0, 0, failOverlay.Width, 36),
                    new Scalar(0, 0, 180), -1);
                Cv2.PutText(failOverlay,
                    $"ALIGNMENT FAILED: {alignmentResult.FailureReason}",
                    new Point(8, 24), HersheyFonts.HersheySimplex,
                    0.55, new Scalar(255, 255, 255), 1);

                return (inspectionResult, failOverlay.Clone());
            }

            // ── STEP 2: PREPROCESSING ─────────────────────────────
            Console.WriteLine("[Orchestrator] Step 2: Preprocessing");
            using Mat preprocessed = Preprocessor.Preprocess(warped, _profile.Preprocessing);
            warped.Dispose();

            // ── STEP 3: ZONE DETECTION ────────────────────────────
            Console.WriteLine("[Orchestrator] Step 3: Zone detection");
            var zoneResults = _detectionEngine.DetectAllZones(preprocessed);
            inspectionResult.Zones = zoneResults;

            // ── STEP 4: OVERALL DECISION ──────────────────────────
            bool anyMissing   = zoneResults.Any(z => z.Decision == ZoneDecision.Missing);
            bool anyUncertain = zoneResults.Any(z => z.Decision == ZoneDecision.Uncertain);

            // Fail if ANY zone is missing
            // Operator review if ANY zone is uncertain
            inspectionResult.OverallPass            = !anyMissing;
            inspectionResult.OperatorReviewRequired = anyUncertain;

            Console.WriteLine($"[Orchestrator] Overall: {(inspectionResult.OverallPass ? "PASS" : "FAIL")} " +
                              $"| Review required: {inspectionResult.OperatorReviewRequired}");

            // ── STEP 5: RENDER OVERLAY ────────────────────────────
            Console.WriteLine("[Orchestrator] Step 5: Rendering overlay");
            Mat overlay = ResultRenderer.DrawOverlay(capturedImage, zoneResults, _profile);

            Console.WriteLine($"[Orchestrator] === Inspection complete: {inspectionId} ===\n");

            return (inspectionResult, overlay);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _alignmentModule?.Dispose();
                _masterImage?.Dispose();
                _disposed = true;
            }
        }
    }

    // ============================================================
    // SECTION 8 — CALIBRATION HELPER
    // Run 20+ known-good images to auto-compute per-zone thresholds
    // rather than guessing them manually in the JSON
    // ============================================================

    public static class CalibrationHelper
    {
        /// <summary>
        /// Run calibration across known-good reference images.
        /// Computes mean edge density per zone and sets threshold
        /// as mean * 0.7 (30% below mean = likely missing).
        /// Prints suggested zone threshold values to update in JSON.
        /// </summary>
        public static void Calibrate(
            string profileJsonPath,
            string masterImagePath,
            string[] referenceImagePaths)
        {
            Console.WriteLine("\n[Calibration] Starting calibration...");
            Console.WriteLine($"[Calibration] Reference images: {referenceImagePaths.Length}");

            var profile  = InspectionProfile.LoadFromFile(profileJsonPath);
            using var orchestrator = new InspectionOrchestrator(profileJsonPath, masterImagePath);

            // Per zone: collect edge densities across all reference images
            var densities = new Dictionary<string, List<double>>();
            foreach (var zone in profile.Zones)
                densities[zone.Id] = new List<double>();

            int processed = 0;
            foreach (var imgPath in referenceImagePaths)
            {
                try
                {
                    using Mat img = Cv2.ImRead(imgPath, ImreadModes.Color);
                    if (img.Empty())
                    {
                        Console.WriteLine($"[Calibration] Skipping unreadable image: {imgPath}");
                        continue;
                    }

                    // Only run alignment + preprocessing, not full detection
                    // so we get raw density values for threshold computation
                    using var alignMod = new AlignmentModule(profile, img);
                    var (warped, alignResult) = alignMod.Align(img);

                    if (!alignResult.Success)
                    {
                        Console.WriteLine($"[Calibration] Alignment failed for {imgPath}, skipping.");
                        continue;
                    }

                    using Mat preprocessed = Preprocessor.Preprocess(warped, profile.Preprocessing);
                    warped.Dispose();

                    foreach (var zone in profile.Zones)
                    {
                        var bbox = new Rect(zone.Bbox[0], zone.Bbox[1],
                                            zone.Bbox[2], zone.Bbox[3]);
                        using Mat crop  = new Mat(preprocessed, bbox);
                        using Mat edges = new Mat();

                        Cv2.Canny(crop, edges,
                            profile.Preprocessing.CannyLow,
                            profile.Preprocessing.CannyHigh);

                        double density = (double)Cv2.CountNonZero(edges)
                                         / (bbox.Width * bbox.Height);
                        densities[zone.Id].Add(density);
                    }

                    processed++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Calibration] Error processing {imgPath}: {ex.Message}");
                }
            }

            Console.WriteLine($"\n[Calibration] Processed {processed} images.");
            Console.WriteLine("[Calibration] Suggested thresholds (update in profile JSON):\n");

            foreach (var zone in profile.Zones)
            {
                var list = densities[zone.Id];
                if (list.Count == 0)
                {
                    Console.WriteLine($"  Zone {zone.Id} ({zone.Label}): no data");
                    continue;
                }

                double mean   = list.Average();
                double stddev = Math.Sqrt(list.Sum(d => Math.Pow(d - mean, 2)) / list.Count);

                // Threshold = mean * 0.7
                // Zone is "missing" when density drops 30% below the good-vehicle mean
                double suggestedThreshold = mean * 0.70;

                Console.WriteLine($"  Zone {zone.Id} ({zone.Label}):");
                Console.WriteLine($"    Mean density   : {mean:F6}");
                Console.WriteLine($"    Std deviation  : {stddev:F6}");
                Console.WriteLine($"    Suggested threshold: {suggestedThreshold:F6}");
                Console.WriteLine($"    → set \"threshold\": {suggestedThreshold:F4} in profile JSON");
                Console.WriteLine();
            }
        }
    }

    // ============================================================
    // SECTION 9 — ENTRY POINT (console demo)
    // Replace with your WinForms / WPF / ASP.NET call as needed
    // ============================================================

    class Program
    {
        static void Main(string[] args)
        {
            // ── Paths — update these ────────────────────────────────
            string profilePath  = "profile.json";       // your JSON config
            string masterPath   = "master.jpg";         // master reference image
            string capturedPath = "captured.jpg";       // image from camera
            string outputPath   = "result_overlay.jpg"; // annotated output image
            string resultJson   = "result.json";        // JSON result log

            // ── Run calibration (do this once before deploying) ─────
            // string[] refImages = Directory.GetFiles("reference_images", "*.jpg");
            // CalibrationHelper.Calibrate(profilePath, masterPath, refImages);
            // return;

            // ── Run inspection ──────────────────────────────────────
            try
            {
                using var orchestrator = new InspectionOrchestrator(profilePath, masterPath);

                var (result, overlay) = orchestrator.Inspect(capturedPath);

                // Save annotated overlay image
                ResultRenderer.SaveOverlay(overlay, outputPath);
                overlay.Dispose();

                // Save JSON result
                File.WriteAllText(resultJson, result.ToJson());
                Console.WriteLine($"[Main] Result JSON saved to: {resultJson}");

                // Print summary
                Console.WriteLine("\n========== INSPECTION SUMMARY ==========");
                Console.WriteLine($"  ID       : {result.InspectionId}");
                Console.WriteLine($"  Part     : {result.PartName} @ {result.Plant}");
                Console.WriteLine($"  Result   : {(result.OverallPass ? "PASS ✓" : "FAIL ✗")}");
                Console.WriteLine($"  Review   : {(result.OperatorReviewRequired ? "YES" : "NO")}");
                Console.WriteLine($"  Zones    : {result.Zones.Count} total");
                Console.WriteLine($"    Present  : {result.Zones.Count(z => z.Decision == ZoneDecision.Present)}");
                Console.WriteLine($"    Missing  : {result.Zones.Count(z => z.Decision == ZoneDecision.Missing)}");
                Console.WriteLine($"    Uncertain: {result.Zones.Count(z => z.Decision == ZoneDecision.Uncertain)}");
                Console.WriteLine("=========================================\n");

                // List missing zones specifically
                var missing = result.Zones.Where(z => z.Decision == ZoneDecision.Missing).ToList();
                if (missing.Any())
                {
                    Console.WriteLine("MISSING PARTS:");
                    foreach (var z in missing)
                        Console.WriteLine($"  [{z.Criticality.ToUpper()}] {z.ZoneId}: {z.Label} " +
                                          $"(density={z.EdgeDensity:F4}, ratio={z.Ratio:F3})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Main] ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}