using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace LSE.AutoCADElectrical.Librarian;

public static class IconRenderService
{
    private const int MaxDepth = 4;
    private const int MaxPolylines = 20000;

    public static void WriteIcon(IconSource source, string drawingPath, string imagePath, string outputPath, int pixels)
    {
        using Bitmap bitmap = Build(source, drawingPath, imagePath, pixels);
        SavePng(bitmap, outputPath);
    }

    public static Bitmap Build(IconSource source, string drawingPath, string imagePath, int pixels)
    {
        return source switch
        {
            IconSource.Render => RenderDrawing(drawingPath, pixels),
            IconSource.Thumbnail => ThumbnailIcon(drawingPath, pixels),
            IconSource.ImageFile => FromImageFile(imagePath, pixels),
            IconSource.ExistingImage => FromImageFile(imagePath, pixels),
            _ => throw new InvalidOperationException("No icon image is produced for " + source + "."),
        };
    }

    public static void SavePng(Bitmap bitmap, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        bitmap.Save(temporary, ImageFormat.Png);
        File.Move(temporary, path, true);
    }

    public static Bitmap FromImageFile(string path, int pixels)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Image file was not found.", path);
        }

        using FileStream stream = File.OpenRead(path);
        using var source = System.Drawing.Image.FromStream(stream);
        return Fit(source, pixels);
    }

    public static Bitmap ThumbnailIcon(string drawingPath, int pixels)
    {
        using Bitmap? thumbnail = DrawingInspectionService.Thumbnail(drawingPath)
            ?? throw new InvalidOperationException("The drawing has no embedded preview image (it was saved with thumbnail previews off). Choose 'Render from the drawing geometry' instead.");
        return Fit(thumbnail, pixels);
    }

    public static Bitmap Fit(System.Drawing.Image source, int pixels)
    {
        var bitmap = new Bitmap(pixels, pixels, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        double scale = Math.Min((double)pixels / source.Width, (double)pixels / source.Height);
        int width = Math.Max(1, (int)Math.Round(source.Width * scale));
        int height = Math.Max(1, (int)Math.Round(source.Height * scale));
        graphics.DrawImage(source, (pixels - width) / 2, (pixels - height) / 2, width, height);
        return bitmap;
    }

    public static Bitmap RenderDrawing(string drawingPath, int pixels)
    {
        List<List<PointF>> polylines = CollectPolylines(drawingPath);
        if (polylines.Count == 0)
        {
            using Bitmap? thumbnail = DrawingInspectionService.Thumbnail(drawingPath);
            if (thumbnail is not null)
            {
                return Fit(thumbnail, pixels);
            }

            throw new InvalidOperationException("The drawing has no lines, arcs, circles or polylines to render and no embedded preview image. Import an image file for the icon instead.");
        }

        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (List<PointF> polyline in polylines)
        {
            foreach (PointF point in polyline)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
        }

        float width = Math.Max(maxX - minX, 1e-6f);
        float height = Math.Max(maxY - minY, 1e-6f);
        float margin = pixels * 0.08f;
        float scale = Math.Min((pixels - 2 * margin) / width, (pixels - 2 * margin) / height);
        float offsetX = (pixels - width * scale) / 2f;
        float offsetY = (pixels - height * scale) / 2f;

        var bitmap = new Bitmap(pixels, pixels, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.Black, pixels >= 128 ? 2f : 1f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };

        foreach (List<PointF> polyline in polylines)
        {
            if (polyline.Count < 2)
            {
                continue;
            }

            var points = new PointF[polyline.Count];
            for (int index = 0; index < polyline.Count; index++)
            {
                points[index] = new PointF(
                    offsetX + (polyline[index].X - minX) * scale,
                    pixels - (offsetY + (polyline[index].Y - minY) * scale));
            }

            graphics.DrawLines(pen, points);
        }

        return bitmap;
    }

    private static List<List<PointF>> CollectPolylines(string drawingPath)
    {
        if (!File.Exists(drawingPath))
        {
            throw new FileNotFoundException("Drawing was not found.", drawingPath);
        }

        var polylines = new List<List<PointF>>();
        using var database = new Database(false, true);
        database.ReadDwgFile(drawingPath, FileOpenMode.OpenForReadAndAllShare, true, null);
        database.CloseInput(true);

        using (Transaction transaction = database.TransactionManager.StartTransaction())
        {
            var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            var modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            Collect(modelSpace, Matrix3d.Identity, 0, transaction, polylines);
            transaction.Commit();
        }

        return polylines;
    }

    private static void Collect(BlockTableRecord record, Matrix3d transform, int depth, Transaction transaction, List<List<PointF>> polylines)
    {
        foreach (ObjectId entityId in record)
        {
            if (polylines.Count >= MaxPolylines)
            {
                return;
            }

            DBObject dbObject = transaction.GetObject(entityId, OpenMode.ForRead);
            if (dbObject is BlockReference reference)
            {
                if (depth >= MaxDepth)
                {
                    continue;
                }

                var nested = (BlockTableRecord)transaction.GetObject(reference.BlockTableRecord, OpenMode.ForRead);
                Collect(nested, transform * reference.BlockTransform, depth + 1, transaction, polylines);
                continue;
            }

            if (dbObject is not Curve curve)
            {
                continue;
            }

            try
            {
                polylines.Add(Sample(curve, transform));
            }
            catch
            {
                // A curve that cannot be evaluated (degenerate spline, zero-length line) is skipped.
            }
        }
    }

    private static List<PointF> Sample(Curve curve, Matrix3d transform)
    {
        int steps = curve switch
        {
            Line => 1,
            Circle => 64,
            Arc => 32,
            Autodesk.AutoCAD.DatabaseServices.Polyline polyline => Math.Clamp(polyline.NumberOfVertices * 8, 8, 512),
            Ellipse => 64,
            _ => 64,
        };

        double start = curve.StartParam;
        double end = curve.EndParam;
        var points = new List<PointF>(steps + 1);
        for (int index = 0; index <= steps; index++)
        {
            double parameter = start + (end - start) * index / steps;
            Point3d point = curve.GetPointAtParameter(parameter).TransformBy(transform);
            points.Add(new PointF((float)point.X, (float)point.Y));
        }

        return points;
    }
}
