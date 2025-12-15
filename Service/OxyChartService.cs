using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.SkiaSharp;
using StatusImageCard.Models;

namespace StatusImageCard.Service
{
    public static class OxyChartService
    {
        private static readonly OxyColor CpuBlue = OxyColor.FromRgb(59, 130, 246);
        private static readonly OxyColor MemGreen = OxyColor.FromRgb(34, 197, 94);

        public static PlotModel BuildUsagePlot(List<UsageSample> samples)
        {
            var model = new PlotModel
            {
                Background = OxyColors.Transparent,
                PlotAreaBackground = OxyColors.Transparent,
                IsLegendVisible = false,
                TextColor = OxyColor.FromRgb(242, 244, 248),
                DefaultFontSize = 11
            };

            var grid = OxyColor.FromRgb(35, 39, 48);
            var axis = OxyColor.FromRgb(90, 98, 112);

            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "yyyy-MM-ddTHH:mm:ss",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = grid,
                MinorGridlineStyle = LineStyle.None,
                AxislineColor = axis,
                TicklineColor = axis,
                TextColor = OxyColor.FromRgb(166, 173, 187)
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Minimum = 0,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = grid,
                MinorGridlineStyle = LineStyle.None,
                AxislineColor = axis,
                TicklineColor = axis,
                TextColor = OxyColor.FromRgb(166, 173, 187)
            });

            var cpu = new LineSeries { Color = CpuBlue, StrokeThickness = 2, MarkerType = MarkerType.None };
            var mem = new LineSeries { Color = MemGreen, StrokeThickness = 2, MarkerType = MarkerType.None };

            foreach (var s in samples.OrderBy(x => x.TsUtc))
            {
                var t = DateTimeAxis.ToDouble(s.TsUtc.UtcDateTime);
                cpu.Points.Add(new DataPoint(t, s.CpuPercent));
                mem.Points.Add(new DataPoint(t, s.MemoryPercent));
            }

            model.Series.Add(cpu);
            model.Series.Add(mem);
            return model;
        }

        public static byte[] RenderPlotPng(PlotModel model, int width, int height)
        {
            using var ms = new MemoryStream();
            new PngExporter
            {
                Width = Math.Max(width, 1),
                Height = Math.Max(height, 1)
            }.Export(model, ms);
            return ms.ToArray();
        }
    }
}
