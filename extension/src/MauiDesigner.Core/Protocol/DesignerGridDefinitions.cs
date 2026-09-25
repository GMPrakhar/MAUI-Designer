using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MauiDesigner.Core.Protocol
{
    public enum DesignerGridUnitType
    {
        Auto,
        Star,
        Absolute
    }

    public readonly struct DesignerGridTrack
    {
        public DesignerGridTrack(DesignerGridUnitType unitType, double value)
        {
            UnitType = unitType;
            Value = value;
        }

        public DesignerGridUnitType UnitType { get; }

        public double Value { get; }
    }

    public static class DesignerGridDefinitions
    {
        public static bool TryParse(
            string? text,
            out IReadOnlyList<DesignerGridTrack> tracks)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                tracks = Array.Empty<DesignerGridTrack>();
                return true;
            }

            string[] parts = text!.Split(',');
            var parsed = new List<DesignerGridTrack>(parts.Length);
            foreach (string part in parts)
            {
                if (!TryParseTrack(part.Trim(), out DesignerGridTrack track))
                {
                    tracks = Array.Empty<DesignerGridTrack>();
                    return false;
                }

                parsed.Add(track);
            }

            tracks = parsed;
            return true;
        }

        public static string Serialize(IEnumerable<DesignerGridTrack> tracks) =>
            string.Join(",", tracks.Select(Serialize));

        public static string Serialize(DesignerGridTrack track)
        {
            switch (track.UnitType)
            {
                case DesignerGridUnitType.Auto:
                    return "Auto";
                case DesignerGridUnitType.Star when track.Value == 1:
                    return "*";
                case DesignerGridUnitType.Star:
                    return track.Value.ToString("G", CultureInfo.InvariantCulture) + "*";
                case DesignerGridUnitType.Absolute:
                    return track.Value.ToString("G", CultureInfo.InvariantCulture);
                default:
                    throw new ArgumentOutOfRangeException(nameof(track));
            }
        }

        private static bool TryParseTrack(string text, out DesignerGridTrack track)
        {
            if (text.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                track = new DesignerGridTrack(DesignerGridUnitType.Auto, 1);
                return true;
            }

            bool isStar = text.EndsWith("*", StringComparison.Ordinal);
            string numeric = isStar ? text.Substring(0, text.Length - 1) : text;
            double value;
            if (isStar && numeric.Length == 0)
            {
                value = 1;
            }
            else if (!double.TryParse(
                         numeric,
                         NumberStyles.Float,
                         CultureInfo.InvariantCulture,
                         out value) ||
                     double.IsNaN(value) ||
                     double.IsInfinity(value) ||
                     value < 0)
            {
                track = default;
                return false;
            }

            track = new DesignerGridTrack(
                isStar ? DesignerGridUnitType.Star : DesignerGridUnitType.Absolute,
                value);
            return true;
        }
    }
}
