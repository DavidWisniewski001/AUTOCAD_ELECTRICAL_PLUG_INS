using System.Windows.Forms;
using Inv = Inventor;
namespace LSE.InventorFootprintTools;

public sealed class InventorGeometryService
{
    private static readonly string[] SupportedExtensions = { ".stl", ".stp", ".step", ".ipt", ".iam" };
    private readonly Inv.Application application;

    public InventorGeometryService(Inv.Application app) => application = app;

    public Inv.Document ImportModel(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Model file was not found.", path);
        string extension = Path.GetExtension(path);
        if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only STL, STEP (.stp/.step), .ipt and .iam files can be used.");

        bool silent = application.SilentOperation;
        application.SilentOperation = true;
        try
        {
            // Inventor routes foreign formats (STEP, STL) through its translators and returns the new part/assembly.
            return application.Documents.Open(path, true);
        }
        finally
        {
            application.SilentOperation = silent;
        }
    }

    public (double X, double Y, double Z) ModelExtentsCm(Inv.Document document)
    {
        dynamic definition = ((dynamic)document).ComponentDefinition;
        Inv.Box box = definition.RangeBox;
        return (box.MaxPoint.X - box.MinPoint.X, box.MaxPoint.Y - box.MinPoint.Y, box.MaxPoint.Z - box.MinPoint.Z);
    }

    public void PreviewView(FootprintView view)
    {
        Inv.Camera camera = application.ActiveView.Camera;
        if (view != FootprintView.Current)
            camera.ViewOrientationType = Orientation(view);
        camera.Fit();
        camera.Apply();
    }

    public FootprintGeometry Project(Inv.Document model, FootprintRequest request, AppSettings settings)
    {
        bool metric = settings.FootprintUnits.Equals("mm", StringComparison.OrdinalIgnoreCase);
        string template = settings.DrawingTemplatePath.Length > 0 && File.Exists(settings.DrawingTemplatePath)
            ? settings.DrawingTemplatePath
            : application.FileManager.GetTemplateFile(Inv.DocumentTypeEnum.kDrawingDocumentObject, Inv.SystemOfMeasureEnum.kDefaultSystemOfMeasure, Inv.DraftingStandardEnum.kDefault_DraftingStandard, null);

        var drawing = (Inv.DrawingDocument)application.Documents.Add(Inv.DocumentTypeEnum.kDrawingDocumentObject, template, false);
        try
        {
            Inv.Sheet sheet = drawing.Sheets[1];
            Inv.Point2d position = application.TransientGeometry.CreatePoint2d(sheet.Width / 2, sheet.Height / 2);
            Inv.DrawingViewStyleEnum style = request.HiddenLines
                ? Inv.DrawingViewStyleEnum.kHiddenLineDrawingViewStyle
                : Inv.DrawingViewStyleEnum.kHiddenLineRemovedDrawingViewStyle;
            Inv.DrawingView view = sheet.DrawingViews.AddBaseView((Inv._Document)model, position, 1.0, Orientation(request.View), style, "", null, null);
            view.DisplayTangentEdges = request.TangentEdges;

            // Sheet geometry is in centimetres. Convert to footprint units, undo the view scale and apply the STL unit correction.
            double factor = (metric ? 10.0 : 1.0 / 2.54) * request.ScaleCorrection / view.Scale;
            double centerX = view.Center.X, centerY = view.Center.Y;
            var geometry = new FootprintGeometry { Units = metric ? "mm" : "in" };

            if (request.OutlineOnly)
            {
                double halfWidth = view.Width * factor / 2, halfHeight = view.Height * factor / 2;
                AddLine(geometry, -halfWidth, -halfHeight, halfWidth, -halfHeight);
                AddLine(geometry, halfWidth, -halfHeight, halfWidth, halfHeight);
                AddLine(geometry, halfWidth, halfHeight, -halfWidth, halfHeight);
                AddLine(geometry, -halfWidth, halfHeight, -halfWidth, -halfHeight);
                return geometry;
            }

            int count = 0;
            foreach (Inv.DrawingCurve curve in view.get_DrawingCurves(null))
            {
                foreach (Inv.DrawingCurveSegment segment in curve.Segments)
                {
                    if (++count > settings.MaxCurves)
                        throw new InvalidOperationException($"The view has more than {settings.MaxCurves} curve segments. Mesh (STL) models produce one segment per facet edge; tick 'Outline rectangle only' or raise MaxCurves in Settings.");
                    AddSegment(geometry, segment, centerX, centerY, factor, settings.CurveTolerance);
                }
            }
            if (geometry.Curves.Count == 0)
                throw new InvalidOperationException("The drawing view produced no curves. Check the view orientation and that the model has solid or mesh bodies.");
            return geometry;
        }
        finally
        {
            drawing.Close(true);
        }
    }

    private static void AddSegment(FootprintGeometry geometry, Inv.DrawingCurveSegment segment, double centerX, double centerY, double factor, double tolerance)
    {
        object shape = segment.Geometry;
        switch (segment.GeometryType)
        {
            case Inv.Curve2dTypeEnum.kLineSegmentCurve2d:
            {
                var line = (Inv.LineSegment2d)shape;
                AddLine(geometry, (line.StartPoint.X - centerX) * factor, (line.StartPoint.Y - centerY) * factor, (line.EndPoint.X - centerX) * factor, (line.EndPoint.Y - centerY) * factor);
                return;
            }
            case Inv.Curve2dTypeEnum.kCircleCurve2d:
            {
                var circle = (Inv.Circle2d)shape;
                double cx = (circle.Center.X - centerX) * factor, cy = (circle.Center.Y - centerY) * factor, radius = circle.Radius * factor;
                geometry.Curves.Add(new FootprintCurve { Kind = "CIRCLE", Data = new[] { cx, cy, radius } });
                Extend(geometry, cx - radius, cy - radius);
                Extend(geometry, cx + radius, cy + radius);
                return;
            }
            case Inv.Curve2dTypeEnum.kCircularArcCurve2d:
            {
                var arc = (Inv.Arc2d)shape;
                double cx = (arc.Center.X - centerX) * factor, cy = (arc.Center.Y - centerY) * factor, radius = arc.Radius * factor;
                double start = arc.StartAngle * 180 / Math.PI, sweep = arc.SweepAngle * 180 / Math.PI;
                double from = sweep >= 0 ? start : start + sweep, to = sweep >= 0 ? start + sweep : start;
                geometry.Curves.Add(new FootprintCurve { Kind = "ARC", Data = new[] { cx, cy, radius, Normalize(from), Normalize(to) } });
                Extend(geometry, (arc.StartPoint.X - centerX) * factor, (arc.StartPoint.Y - centerY) * factor);
                Extend(geometry, (arc.EndPoint.X - centerX) * factor, (arc.EndPoint.Y - centerY) * factor);
                return;
            }
            default:
            {
                // Ellipses, splines and anything else: sample the curve and write a polyline.
                dynamic dynamicShape = shape;
                Inv.Curve2dEvaluator evaluator = dynamicShape.Evaluator;
                evaluator.GetParamExtents(out double minParam, out double maxParam);
                evaluator.GetLengthAtParam(minParam, maxParam, out double length);
                int steps = Math.Clamp((int)Math.Ceiling(length * factor / Math.Max(tolerance * 8, 1e-6)), 8, 256);
                double[] parameters = new double[steps + 1];
                for (int i = 0; i <= steps; i++)
                    parameters[i] = minParam + (maxParam - minParam) * i / steps;
                double[] points = new double[(steps + 1) * 2];
                evaluator.GetPointAtParam(ref parameters, ref points);
                double[] data = new double[points.Length];
                for (int i = 0; i < points.Length; i += 2)
                {
                    data[i] = (points[i] - centerX) * factor;
                    data[i + 1] = (points[i + 1] - centerY) * factor;
                    Extend(geometry, data[i], data[i + 1]);
                }
                geometry.Curves.Add(new FootprintCurve { Kind = "POLY", Data = data });
                return;
            }
        }
    }

    private static void AddLine(FootprintGeometry geometry, double x1, double y1, double x2, double y2)
    {
        geometry.Curves.Add(new FootprintCurve { Kind = "LINE", Data = new[] { x1, y1, x2, y2 } });
        Extend(geometry, x1, y1);
        Extend(geometry, x2, y2);
    }

    private static void Extend(FootprintGeometry geometry, double x, double y)
    {
        geometry.MinX = Math.Min(geometry.MinX, x);
        geometry.MinY = Math.Min(geometry.MinY, y);
        geometry.MaxX = Math.Max(geometry.MaxX, x);
        geometry.MaxY = Math.Max(geometry.MaxY, y);
    }

    private static double Normalize(double degrees)
    {
        double value = degrees % 360;
        return value < 0 ? value + 360 : value;
    }

    private static Inv.ViewOrientationTypeEnum Orientation(FootprintView view) => view switch
    {
        FootprintView.Top => Inv.ViewOrientationTypeEnum.kTopViewOrientation,
        FootprintView.Bottom => Inv.ViewOrientationTypeEnum.kBottomViewOrientation,
        FootprintView.Front => Inv.ViewOrientationTypeEnum.kFrontViewOrientation,
        FootprintView.Back => Inv.ViewOrientationTypeEnum.kBackViewOrientation,
        FootprintView.Left => Inv.ViewOrientationTypeEnum.kLeftViewOrientation,
        FootprintView.Right => Inv.ViewOrientationTypeEnum.kRightViewOrientation,
        _ => Inv.ViewOrientationTypeEnum.kCurrentViewOrientation,
    };
}

public sealed class InventorWindow : IWin32Window
{
    public IntPtr Handle { get; }
    public InventorWindow(Inv.Application app) => Handle = new IntPtr(app.MainFrameHWND);
}
