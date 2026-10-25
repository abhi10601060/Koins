// =====================================================================
// DetectionEngineV2.cs  -  drop-in replacement for DetectionEngine
//
// Three cues per zone, fused into one 0..1 "presence score":
//   1. Edge density ratio      (your existing cue, uses calibrated threshold)
//   2. Master edge coverage    (are the MASTER's specific edges present?)
//   3. Template-match NCC      (does the zone still look like the master?)
// Plus: per-zone local re-alignment (+/- SearchPx) to absorb residual
// homography error before measuring anything.
//
// REQUIRED CHANGES ELSEWHERE
//  1) Add to ZoneResult:
//        public double Coverage { get; set; }   // NaN if unavailable
//        public double Ncc { get; set; }        // NaN if unavailable
//        public double Score { get; set; }      // fused 0..1
//        public int OffsetX { get; set; }
//        public int OffsetY { get; set; }
//  2) Orchestrator: _detectionEngine = new DetectionEngine(_profile, _masterImage);
//     and call _detectionEngine?.Dispose() in InspectionOrchestrator.Dispose().
// =====================================================================

using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace VehicleInspection
{
    public class DetectionTuning
    {
        public int SearchPx = 6;                 // local re-alignment window (+/- px)
        public int EdgeTolerancePx = 2;          // edge matching tolerance
        public int MinMasterEdgePixels = 30;     // below this, coverage cue is skipped
        public double MinTemplateStd = 3.0;      // below this, NCC cue is skipped (flat zone)

        // Cue ramps: value <= Missing -> score 0, value >= Present -> score 1
        public double CoverageMissing = 0.30, CoveragePresent = 0.60;
        public double NccMissing = 0.20, NccPresent = 0.50;

        // Cue weights (unavailable cues are dropped and weights renormalised)
        public double WDensity = 0.30, WCoverage = 0.45, WNcc = 0.25;

        // Final decision on fused score
        public double MissingBelow = 0.35, PresentAbove = 0.65;
    }

    public class DetectionEngine : IDisposable
    {
        private class ZoneRef
        {
            public Rect Bbox;
            public Mat Gray;      // master zone, preprocessed
            public Mat Edges;     // master zone Canny edges
            public int EdgePixels;
            public double Std;
        }

        private readonly InspectionProfile _profile;
        private readonly DetectionTuning _t;
        private readonly Mat _masterPre;
        private readonly Dictionary<string, ZoneRef> _refs = new();
        private bool _disposed;

        public DetectionEngine(InspectionProfile profile, Mat masterImage,
                               DetectionTuning tuning = null)
        {
            _profile = profile;
            _t = tuning ?? new DetectionTuning();

            // Preprocess master ONCE, with the exact same pipeline as captures
            _masterPre = Preprocessor.Preprocess(masterImage, profile.Preprocessing);

            foreach (var zone in profile.Zones)
            {
                var bbox = ClampRect(
                    new Rect(zone.Bbox[0], zone.Bbox[1], zone.Bbox[2], zone.Bbox[3]),
                    _masterPre.Size());

                var gray = new Mat(_masterPre, bbox).Clone();
                var edges = new Mat();
                Cv2.Canny(gray, edges,
                    profile.Preprocessing.CannyLow,
                    profile.Preprocessing.CannyHigh);

                Cv2.MeanStdDev(gray, out _, out Scalar std);

                _refs[zone.Id] = new ZoneRef
                {
                    Bbox = bbox,
                    Gray = gray,
                    Edges = edges,
                    EdgePixels = Cv2.CountNonZero(edges),
                    Std = std.Val0
                };
            }
        }

        public List<ZoneResult> DetectAllZones(Mat preprocessedImage)
        {
            var results = new List<ZoneResult>();

            foreach (var zone in _profile.Zones)
            {
                var r = DetectZone(preprocessedImage, zone);
                results.Add(r);

                Console.WriteLine($"[Detection] Zone {zone.Id} ({zone.Label}): " +
                    $"ratio={r.Ratio:F3} cov={Fmt(r.Coverage)} ncc={Fmt(r.Ncc)} " +
                    $"shift=({r.OffsetX},{r.OffsetY}) score={r.Score:F2} -> {r.Decision}");
            }

            return results;
        }

        private ZoneResult DetectZone(Mat img, ZoneConfig zone)
        {
            var zr = _refs[zone.Id];
            var bbox = zr.Bbox;
            double band0 = _profile.UncertainBand[0];
            double band1 = _profile.UncertainBand[1];

            // ---- 1. Local re-alignment via template matching ----
            var imgRect = new Rect(0, 0, img.Width, img.Height);
            var search = Rect.Intersect(
                new Rect(bbox.X - _t.SearchPx, bbox.Y - _t.SearchPx,
                         bbox.Width + 2 * _t.SearchPx, bbox.Height + 2 * _t.SearchPx),
                imgRect);

            Rect aligned = bbox;
            double ncc = double.NaN;

            if (zr.Std >= _t.MinTemplateStd &&
                search.Width >= bbox.Width && search.Height >= bbox.Height)
            {
                using var region = new Mat(img, search);
                using var res = new Mat();
                Cv2.MatchTemplate(region, zr.Gray, res, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(res, out _, out double maxVal, out _, out Point maxLoc);

                if (!double.IsNaN(maxVal) && !double.IsInfinity(maxVal))
                {
                    ncc = Math.Clamp(maxVal, -1.0, 1.0);
                    aligned = new Rect(search.X + maxLoc.X, search.Y + maxLoc.Y,
                                       bbox.Width, bbox.Height);
                }
            }

            // ---- 2. Edges on the re-aligned crop ----
            using Mat crop = new Mat(img, aligned);
            using Mat edges = new Mat();
            Cv2.Canny(crop, edges,
                _profile.Preprocessing.CannyLow,
                _profile.Preprocessing.CannyHigh);

            int total = aligned.Width * aligned.Height;
            double density = total > 0 ? (double)Cv2.CountNonZero(edges) / total : 0.0;
            double ratio = zone.Threshold > 0 ? density / zone.Threshold : 0.0;

            // ---- 3. Chance-corrected master edge coverage ----
            double coverage = double.NaN;
            if (zr.EdgePixels >= _t.MinMasterEdgePixels)
            {
                int k = 2 * _t.EdgeTolerancePx + 1;
                using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(k, k));
                using Mat dil = new Mat();
                Cv2.Dilate(edges, dil, kernel);

                using Mat inter = new Mat();
                Cv2.BitwiseAnd(zr.Edges, dil, inter);

                double recall = (double)Cv2.CountNonZero(inter) / zr.EdgePixels;
                double chance = (double)Cv2.CountNonZero(dil) / total; // random-hit rate in a busy zone

                coverage = chance < 0.98
                    ? Math.Clamp((recall - chance) / (1.0 - chance), 0.0, 1.0)
                    : 0.0;
            }

            // ---- 4. Fuse cues ----
            double wSum = 0, sSum = 0;

            sSum += _t.WDensity * Ramp(ratio, band0, band1);
            wSum += _t.WDensity;

            if (!double.IsNaN(coverage))
            {
                sSum += _t.WCoverage * Ramp(coverage, _t.CoverageMissing, _t.CoveragePresent);
                wSum += _t.WCoverage;
            }
            if (!double.IsNaN(ncc))
            {
                sSum += _t.WNcc * Ramp(ncc, _t.NccMissing, _t.NccPresent);
                wSum += _t.WNcc;
            }

            double score = wSum > 0 ? sSum / wSum : 0.0;

            ZoneDecision decision =
                score >= _t.PresentAbove ? ZoneDecision.Present :
                score <= _t.MissingBelow ? ZoneDecision.Missing :
                                           ZoneDecision.Uncertain;

            return new ZoneResult
            {
                ZoneId = zone.Id,
                Label = zone.Label,
                Criticality = zone.Criticality,
                EdgeDensity = Math.Round(density, 6),
                Threshold = zone.Threshold,
                Ratio = Math.Round(ratio, 4),
                Coverage = double.IsNaN(coverage) ? double.NaN : Math.Round(coverage, 4),
                Ncc = double.IsNaN(ncc) ? double.NaN : Math.Round(ncc, 4),
                Score = Math.Round(score, 4),
                OffsetX = aligned.X - bbox.X,
                OffsetY = aligned.Y - bbox.Y,
                Decision = decision
            };
        }

        // Linear ramp: <=lo -> 0, >=hi -> 1
        private static double Ramp(double x, double lo, double hi)
            => hi <= lo ? (x >= hi ? 1.0 : 0.0) : Math.Clamp((x - lo) / (hi - lo), 0.0, 1.0);

        private static string Fmt(double v) => double.IsNaN(v) ? "n/a" : v.ToString("F2");

        private static Rect ClampRect(Rect r, Size imageSize)
        {
            int x = Math.Max(0, Math.Min(r.X, imageSize.Width - 1));
            int y = Math.Max(0, Math.Min(r.Y, imageSize.Height - 1));
            int w = Math.Min(r.Width, imageSize.Width - x);
            int h = Math.Min(r.Height, imageSize.Height - y);
            return new Rect(x, y, w, h);
        }

        public void Dispose()
        {
            if (_disposed) return;
            foreach (var z in _refs.Values) { z.Gray?.Dispose(); z.Edges?.Dispose(); }
            _masterPre?.Dispose();
            _disposed = true;
        }
    }
}
