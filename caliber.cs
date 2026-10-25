// =====================================================================
// SingleImageCalibration.cs
// Calibrates per-zone edge-density thresholds from ONE master image.
//
// Idea: a single image gives only one density sample per zone, so we
// can't measure real-world spread. Instead we simulate the variation the
// pipeline will see in production (small alignment error, exposure
// change, sensor noise, warp interpolation blur), re-measure each zone on
// every simulated variant, and place the threshold so that ALL plausible
// "part present" readings land above the Present cutoff
// (ratio > UncertainBand[1]), while a genuinely missing part (density
// collapse) lands below the Missing cutoff (ratio < UncertainBand[0]).
//
// Assumes: master image has every zone's part PRESENT, and zone bboxes
// are expressed in the master image's pixel frame.
// =====================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OpenCvSharp;

namespace VehicleInspection
{
    public class ZoneCalibration
    {
        public string ZoneId { get; set; }
        public string Label { get; set; }
        public double MasterDensity { get; set; }     // density on the untouched master
        public double MeanDensity { get; set; }       // mean over simulated variants
        public double StdDensity { get; set; }
        public double MinDensity { get; set; }
        public double MaxDensity { get; set; }
        public double RelativeSigma { get; set; }     // std / mean
        public double SuggestedThreshold { get; set; }
        public double MissingBelowDensity { get; set; }  // density under which zone => Missing
        public double PresentAboveDensity { get; set; }  // density above which zone => Present
        public double SelfCheckPresentRate { get; set; } // % of variants classified Present
        public string Reliability { get; set; }       // High / Medium / Low
        public string Note { get; set; }
    }

    public static class SingleImageCalibrator
    {
        // ---- Tunables ----
        private const int VariantCount = 60;
        private const double SigmaMultiplier = 3.0;        // cover +/-3 sigma of good readings
        private const double MinRelativeSigma = 0.08;      // floor: synthetic spread under-estimates reality
        private const double SafetyMargin = 1.05;          // keep good readings clear of the Present cutoff
        private const double MinThresholdFactor = 0.50;    // threshold never below 50% of mean
        private const double MaxThresholdFactor = 0.85;    // never above 85% of mean
        private const double MinUsableDensity = 0.01;      // below this a zone is nearly featureless

        // ---- Entry points ----

        public static List<ZoneCalibration> Calibrate(
            string profileJsonPath,
            string masterImagePath)
        {
            var profile = InspectionProfile.LoadFromFile(profileJsonPath);

            using Mat master = Cv2.ImRead(masterImagePath, ImreadModes.Color);
            if (master.Empty())
                throw new FileNotFoundException($"Master image not found: {masterImagePath}");

            return Calibrate(profile, master);
        }

        public static List<ZoneCalibration> Calibrate(InspectionProfile profile, Mat master)
        {
            Console.WriteLine("\n[Calibration] Single-image calibration starting...");
            Console.WriteLine($"[Calibration] Master: {master.Width}x{master.Height}, " +
                              $"zones: {profile.Zones.Count}, variants: {VariantCount}");

            double bandLow = profile.UncertainBand != null && profile.UncertainBand.Length == 2
                ? profile.UncertainBand[0] : 0.85;
            double bandHigh = profile.UncertainBand != null && profile.UncertainBand.Length == 2
                ? profile.UncertainBand[1] : 1.15;

            var rng = new Random(12345);

            // densities[zoneId] -> readings; index 0 is the untouched master
            var densities = profile.Zones.ToDictionary(z => z.Id, _ => new List<double>());

            // Sample 0: master itself (no augmentation)
            AddReadings(master, profile, densities);

            // Samples 1..N: simulated production variation
            for (int i = 0; i < VariantCount; i++)
            {
                using Mat variant = MakeVariant(master, rng);
                AddReadings(variant, profile, densities);
            }

            var results = new List<ZoneCalibration>();

            foreach (var zone in profile.Zones)
            {
                var list = densities[zone.Id];
                double masterDensity = list[0];
                double mean = list.Average();
                double std = Math.Sqrt(list.Sum(d => Math.Pow(d - mean, 2)) / list.Count);
                double relSigma = mean > 0 ? std / mean : 0;

                // Lowest density we expect from a GOOD part
                double sigmaEff = Math.Max(std, MinRelativeSigma * mean);
                double lowGood = Math.Max(0, mean - SigmaMultiplier * sigmaEff);

                // Choose threshold so lowGood/threshold sits just above the Present cutoff
                double threshold = lowGood / (bandHigh * SafetyMargin);
                threshold = Math.Clamp(threshold,
                                       MinThresholdFactor * mean,
                                       MaxThresholdFactor * mean);

                double presentAbove = threshold * bandHigh;
                double missingBelow = threshold * bandLow;

                // Self-check: how many simulated good readings come out Present?
                double presentRate = threshold > 0
                    ? 100.0 * list.Count(d => d / threshold > bandHigh) / list.Count
                    : 0;

                // Reliability grading
                string reliability;
                string note = "";
                if (mean < MinUsableDensity)
                {
                    reliability = "Low";
                    note = "Zone has almost no edges - missing-part detection will be unreliable. " +
                           "Move/enlarge the zone onto textured area.";
                }
                else if (relSigma > 0.18)
                {
                    reliability = "Low";
                    note = "Density is very sensitive to alignment/exposure in this zone. " +
                           "Enlarge the zone or avoid zone borders crossing strong edges.";
                }
                else if (relSigma > 0.10 || mean < 0.03)
                {
                    reliability = "Medium";
                    note = "Acceptable; verify with one real missing-part sample if possible.";
                }
                else
                {
                    reliability = "High";
                }

                if (presentRate < 95 && reliability != "Low")
                {
                    reliability = "Medium";
                    note = (note + " Only " + presentRate.ToString("F0") +
                            "% of simulated good readings classified Present.").Trim();
                }

                results.Add(new ZoneCalibration
                {
                    ZoneId = zone.Id,
                    Label = zone.Label,
                    MasterDensity = masterDensity,
                    MeanDensity = mean,
                    StdDensity = std,
                    MinDensity = list.Min(),
                    MaxDensity = list.Max(),
                    RelativeSigma = relSigma,
                    SuggestedThreshold = threshold,
                    PresentAboveDensity = presentAbove,
                    MissingBelowDensity = missingBelow,
                    SelfCheckPresentRate = presentRate,
                    Reliability = reliability,
                    Note = note
                });
            }

            PrintReport(results);
            return results;
        }

        // Write suggested thresholds back into the profile JSON
        public static void ApplyToProfileFile(
            string profileJsonPath,
            List<ZoneCalibration> calibration,
            string outputPath = null)
        {
            var profile = InspectionProfile.LoadFromFile(profileJsonPath);

            foreach (var zone in profile.Zones)
            {
                var c = calibration.FirstOrDefault(x => x.ZoneId == zone.Id);
                if (c != null)
                    zone.Threshold = Math.Round(c.SuggestedThreshold, 6);
            }

            string json = JsonSerializer.Serialize(profile,
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(outputPath ?? profileJsonPath, json);
            Console.WriteLine($"[Calibration] Thresholds written to: {outputPath ?? profileJsonPath}");
        }

        // ---- Measurement (mirrors DetectionEngine.DetectZone exactly) ----

        private static void AddReadings(
            Mat image,
            InspectionProfile profile,
            Dictionary<string, List<double>> densities)
        {
            using Mat preprocessed = Preprocessor.Preprocess(image, profile.Preprocessing);

            foreach (var zone in profile.Zones)
            {
                var bbox = ClampRect(
                    new Rect(zone.Bbox[0], zone.Bbox[1], zone.Bbox[2], zone.Bbox[3]),
                    preprocessed.Size());

                using Mat crop = new Mat(preprocessed, bbox);
                using Mat edges = new Mat();

                Cv2.Canny(crop, edges,
                    profile.Preprocessing.CannyLow,
                    profile.Preprocessing.CannyHigh);

                int total = bbox.Width * bbox.Height;
                double density = total > 0 ? (double)Cv2.CountNonZero(edges) / total : 0.0;

                densities[zone.Id].Add(density);
            }
        }

        // ---- Synthetic production variation ----

        private static Mat MakeVariant(Mat master, Random rng)
        {
            // 1. Residual alignment error after homography: tiny rotate/scale/shift
            double angle = Uniform(rng, -0.7, 0.7);     // degrees
            double scale = Uniform(rng, 0.99, 1.01);
            double tx = Uniform(rng, -3, 3);            // pixels
            double ty = Uniform(rng, -3, 3);

            var center = new Point2f(master.Width / 2f, master.Height / 2f);
            using Mat M = Cv2.GetRotationMatrix2D(center, angle, scale);
            M.Set(0, 2, M.Get<double>(0, 2) + tx);
            M.Set(1, 2, M.Get<double>(1, 2) + ty);

            Mat warped = new Mat();
            Cv2.WarpAffine(master, warped, M, master.Size(),
                InterpolationFlags.Linear, BorderTypes.Replicate);

            // 2. Exposure / contrast change
            double alpha = Uniform(rng, 0.80, 1.20);
            double beta = Uniform(rng, -20, 20);
            warped.ConvertTo(warped, -1, alpha, beta);

            // 3. Sensor noise (float domain so negatives aren't clipped)
            double sigma = Uniform(rng, 1.5, 6.0);
            using Mat f = new Mat();
            warped.ConvertTo(f, MatType.CV_32F);
            using Mat noise = new Mat(f.Size(), f.Type());
            Cv2.Randn(noise, Scalar.All(0), Scalar.All(sigma));
            Cv2.Add(f, noise, f);

            Mat result = new Mat();
            f.ConvertTo(result, master.Type());
            warped.Dispose();
            return result;
        }

        private static double Uniform(Random rng, double min, double max)
            => min + rng.NextDouble() * (max - min);

        private static Rect ClampRect(Rect r, Size imageSize)
        {
            int x = Math.Max(0, Math.Min(r.X, imageSize.Width - 1));
            int y = Math.Max(0, Math.Min(r.Y, imageSize.Height - 1));
            int w = Math.Min(r.Width, imageSize.Width - x);
            int h = Math.Min(r.Height, imageSize.Height - y);
            return new Rect(x, y, w, h);
        }

        // ---- Reporting ----

        private static void PrintReport(List<ZoneCalibration> results)
        {
            Console.WriteLine("\n[Calibration] ===== RESULTS =====\n");

            foreach (var r in results)
            {
                Console.WriteLine($"  Zone {r.ZoneId} ({r.Label})  [{r.Reliability}]");
                Console.WriteLine($"    Master density     : {r.MasterDensity:F6}");
                Console.WriteLine($"    Variant mean +/- sd: {r.MeanDensity:F6} +/- {r.StdDensity:F6} " +
                                  $"(rel {r.RelativeSigma:P1})");
                Console.WriteLine($"    Variant range      : {r.MinDensity:F6} .. {r.MaxDensity:F6}");
                Console.WriteLine($"    Present above      : {r.PresentAboveDensity:F6}");
                Console.WriteLine($"    Missing below      : {r.MissingBelowDensity:F6}");
                Console.WriteLine($"    Self-check Present : {r.SelfCheckPresentRate:F0}%");
                Console.WriteLine($"    -> set \"threshold\": {r.SuggestedThreshold:F4} in profile JSON");
                if (!string.IsNullOrEmpty(r.Note))
                    Console.WriteLine($"    ! {r.Note}");
                Console.WriteLine();
            }
        }
    }
}

// ---- Usage ----
// var results = SingleImageCalibrator.Calibrate("profile.json", "master.jpg");
// SingleImageCalibrator.ApplyToProfileFile("profile.json", results, "profile_calibrated.json");
