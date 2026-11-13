using System.Globalization;
using System.Text;
namespace LSE.InventorFootprintTools;

public static class DxfFootprintWriter
{
    // AutoCAD Electrical panel footprint attribute set. P_TAG1 is the visible tag; the rest are
    // invisible and filled by AcadE when the footprint is inserted from the catalog. (verify against
    // the AcadE 2026 "footprint attributes" help topic on a workstation that has AcadE.)
    private static readonly (string Tag, string Prompt, bool Visible)[] StandardAttributes =
    {
        ("P_TAG1", "Panel component tag", true),
        ("MFG", "Manufacturer", false),
        ("CAT", "Catalog number", false),
        ("ASSYCODE", "Assembly code", false),
        ("DESC1", "Description line 1", false),
        ("DESC2", "Description line 2", false),
        ("DESC3", "Description line 3", false),
        ("INST", "Installation", false),
        ("LOC", "Location", false),
        ("P_ITEM", "Item number", false),
        ("XREF", "Schematic cross-reference", false),
        ("XREFNO", "Schematic cross-reference number", false),
    };

    public static void Write(string path, FootprintGeometry geometry, AppSettings settings, IDictionary<string, string> defaults)
    {
        if (geometry.Curves.Count == 0)
            throw new InvalidOperationException("No footprint geometry to write.");

        bool metric = geometry.Units.Equals("mm", StringComparison.OrdinalIgnoreCase);
        double textHeight = settings.TagTextHeight * (metric ? 25.4 : 1.0);
        string geometryLayer = Layer(settings.GeometryLayer);
        string attributeLayer = Layer(settings.AttributeLayer);

        var sb = new StringBuilder();
        WriteHeader(sb, geometry, textHeight);
        WriteTables(sb, geometryLayer, attributeLayer);

        Group(sb, 0, "SECTION");
        Group(sb, 2, "ENTITIES");
        foreach (FootprintCurve curve in geometry.Curves)
            WriteCurve(sb, curve, geometryLayer);

        // Visible tag above the footprint, invisible attributes stacked below it.
        double tagY = geometry.MaxY + textHeight;
        double rowY = geometry.MinY - 2 * textHeight;
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (tag, prompt, visible) in StandardAttributes)
        {
            defaults.TryGetValue(tag, out string? value);
            WriteAttribute(sb, attributeLayer, tag, prompt, value ?? "", visible, geometry.MinX, visible ? tagY : rowY, textHeight);
            if (!visible) rowY -= 1.5 * textHeight;
            written.Add(tag);
        }
        foreach (var pair in defaults)
        {
            if (written.Contains(pair.Key)) continue;
            WriteAttribute(sb, attributeLayer, pair.Key.ToUpperInvariant(), "LSE part number", pair.Value, false, geometry.MinX, rowY, textHeight);
            rowY -= 1.5 * textHeight;
        }
        Group(sb, 0, "ENDSEC");
        Group(sb, 0, "EOF");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static void WriteHeader(StringBuilder sb, FootprintGeometry g, double textHeight)
    {
        Group(sb, 0, "SECTION");
        Group(sb, 2, "HEADER");
        Group(sb, 9, "$ACADVER");
        Group(sb, 1, "AC1009");
        Group(sb, 9, "$INSBASE");
        Point(sb, 10, 0, 0);
        Group(sb, 9, "$EXTMIN");
        Point(sb, 10, g.MinX, g.MinY - 20 * textHeight);
        Group(sb, 9, "$EXTMAX");
        Point(sb, 10, g.MaxX, g.MaxY + 2 * textHeight);
        Group(sb, 9, "$TEXTSIZE");
        Group(sb, 40, Num(textHeight));
        Group(sb, 9, "$ATTMODE");
        Group(sb, 70, "1");
        Group(sb, 0, "ENDSEC");
    }

    private static void WriteTables(StringBuilder sb, string geometryLayer, string attributeLayer)
    {
        Group(sb, 0, "SECTION");
        Group(sb, 2, "TABLES");

        Group(sb, 0, "TABLE");
        Group(sb, 2, "LTYPE");
        Group(sb, 70, "1");
        Group(sb, 0, "LTYPE");
        Group(sb, 2, "CONTINUOUS");
        Group(sb, 70, "0");
        Group(sb, 3, "Solid line");
        Group(sb, 72, "65");
        Group(sb, 73, "0");
        Group(sb, 40, "0.0");
        Group(sb, 0, "ENDTAB");

        var layers = new List<string> { "0" };
        foreach (string layer in new[] { geometryLayer, attributeLayer })
            if (!layers.Contains(layer, StringComparer.OrdinalIgnoreCase)) layers.Add(layer);
        Group(sb, 0, "TABLE");
        Group(sb, 2, "LAYER");
        Group(sb, 70, layers.Count.ToString(CultureInfo.InvariantCulture));
        foreach (string layer in layers)
        {
            Group(sb, 0, "LAYER");
            Group(sb, 2, layer);
            Group(sb, 70, "0");
            Group(sb, 62, "7");
            Group(sb, 6, "CONTINUOUS");
        }
        Group(sb, 0, "ENDTAB");

        Group(sb, 0, "TABLE");
        Group(sb, 2, "STYLE");
        Group(sb, 70, "1");
        Group(sb, 0, "STYLE");
        Group(sb, 2, "STANDARD");
        Group(sb, 70, "0");
        Group(sb, 40, "0.0");
        Group(sb, 41, "1.0");
        Group(sb, 50, "0.0");
        Group(sb, 71, "0");
        Group(sb, 42, "0.2");
        Group(sb, 3, "txt");
        Group(sb, 4, "");
        Group(sb, 0, "ENDTAB");

        Group(sb, 0, "ENDSEC");
    }

    private static void WriteCurve(StringBuilder sb, FootprintCurve curve, string layer)
    {
        double[] d = curve.Data;
        switch (curve.Kind)
        {
            case "LINE":
                Group(sb, 0, "LINE");
                Group(sb, 8, layer);
                Point(sb, 10, d[0], d[1]);
                Point(sb, 11, d[2], d[3]);
                break;
            case "CIRCLE":
                Group(sb, 0, "CIRCLE");
                Group(sb, 8, layer);
                Point(sb, 10, d[0], d[1]);
                Group(sb, 40, Num(d[2]));
                break;
            case "ARC":
                Group(sb, 0, "ARC");
                Group(sb, 8, layer);
                Point(sb, 10, d[0], d[1]);
                Group(sb, 40, Num(d[2]));
                Group(sb, 50, Num(d[3]));
                Group(sb, 51, Num(d[4]));
                break;
            case "POLY":
                Group(sb, 0, "POLYLINE");
                Group(sb, 8, layer);
                Group(sb, 66, "1");
                Group(sb, 70, "0");
                for (int i = 0; i + 1 < d.Length; i += 2)
                {
                    Group(sb, 0, "VERTEX");
                    Group(sb, 8, layer);
                    Point(sb, 10, d[i], d[i + 1]);
                }
                Group(sb, 0, "SEQEND");
                Group(sb, 8, layer);
                break;
            default:
                throw new InvalidOperationException("Unknown footprint curve kind: " + curve.Kind);
        }
    }

    private static void WriteAttribute(StringBuilder sb, string layer, string tag, string prompt, string value, bool visible, double x, double y, double height)
    {
        Group(sb, 0, "ATTDEF");
        Group(sb, 8, layer);
        Point(sb, 10, x, y);
        Group(sb, 40, Num(height));
        Group(sb, 1, value);
        Group(sb, 3, prompt);
        Group(sb, 2, tag);
        Group(sb, 70, visible ? "0" : "1");
    }

    private static string Layer(string name)
    {
        string layer = (name ?? "").Trim();
        return layer.Length == 0 ? "0" : layer;
    }

    private static void Point(StringBuilder sb, int xCode, double x, double y)
    {
        Group(sb, xCode, Num(x));
        Group(sb, xCode + 10, Num(y));
        Group(sb, xCode + 20, "0.0");
    }

    private static void Group(StringBuilder sb, int code, string value)
    {
        sb.Append(code.ToString(CultureInfo.InvariantCulture)).Append("\r\n").Append(value).Append("\r\n");
    }

    private static string Num(double v) => v.ToString("0.########", CultureInfo.InvariantCulture);
}
