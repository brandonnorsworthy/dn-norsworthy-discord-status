using SkiaSharp;
using StatusImageCard.Models;

namespace StatusImageCard.Service
{
    public static class StatusCardRenderService
    {
        private static readonly SKColor PageBg = SKColor.Parse("#0b0d10");
        private static readonly SKColor CardBg = SKColor.Parse("#14161a");
        private static readonly SKColor CardBorder = SKColor.Parse("#262a31");
        private static readonly SKColor MutedText = SKColor.Parse("#a6adbb");
        private static readonly SKColor MainText = SKColor.Parse("#f2f4f8");
        private static readonly SKColor Divider = SKColor.Parse("#232730");

        private static readonly SKColor OnlinePill = SKColor.Parse("#22c55e");
        private static readonly SKColor OnlineText = SKColor.Parse("#0b0d10");
        private static readonly SKColor OfflinePill = SKColor.Parse("#2b2f37");
        private static readonly SKColor OfflineText = MainText;

        private static readonly SKColor CpuBlue = SKColor.Parse("#3b82f6");
        private static readonly SKColor MemGreen = SKColor.Parse("#22c55e");

        public static byte[] Render(ContainerCard c, int width, int height)
        {
            using var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bmp);

            canvas.Clear(PageBg);

            float pad = 18;
            var cardRect = new SKRect(pad, pad, width - pad, height - pad);

            DrawShadowedCard(canvas, cardRect, 18);

            float x0 = cardRect.Left + 18;
            float y0 = cardRect.Top + 16;

            var iconRect = new SKRect(x0, y0, x0 + 34, y0 + 34);
            DrawIconBadge(canvas, iconRect);

            float textLeft = iconRect.Right + 12;

            using var nameFont = new SKFont { Size = 20 };
            using var namePaint = new SKPaint { IsAntialias = true, Color = MainText };
            canvas.DrawText(c.Name, textLeft, y0 + 22, SKTextAlign.Left, nameFont, namePaint);

            using var idFont = new SKFont { Size = 13 };
            using var idPaint = new SKPaint { IsAntialias = true, Color = MutedText };
            canvas.DrawText(c.IdShort, textLeft, y0 + 40, SKTextAlign.Left, idFont, idPaint);

            string pillText = c.StatusText;
            var (pillFill, pillTextColor) = c.StatusState == StatusState.Online
                ? (OnlinePill, OnlineText)
                : (OfflinePill, OfflineText);

            using var pillFont = new SKFont { Size = 13 };
            float pillPadX = 14, pillPadY = 8;
            float pillW = pillFont.MeasureText(pillText) + pillPadX * 2;
            float pillH = pillFont.Size + pillPadY * 2;

            var pillRect = new SKRect(cardRect.Right - 18 - pillW, y0 + 4, cardRect.Right - 18, y0 + 4 + pillH);
            DrawRoundedRect(canvas, pillRect, pillH / 2f, pillFill, border: null);

            using var pillPaint = new SKPaint { IsAntialias = true, Color = pillTextColor };
            canvas.DrawText(pillText, pillRect.Left + pillPadX, pillRect.MidY + pillFont.Size * 0.35f,
                SKTextAlign.Left, pillFont, pillPaint);

            using var upFont = new SKFont { Size = 13 };
            using var upPaint = new SKPaint { IsAntialias = true, Color = MutedText };
            canvas.DrawText($"{c.UptimeText} ({c.HealthText})", x0, y0 + 72, SKTextAlign.Left, upFont, upPaint);

            float metricsY = y0 + 104;

            DrawInlineLabelValue(canvas, x0, metricsY, "CPU: ", $"{c.CpuNowPercent:0.##}%", MutedText, MainText);

            string memValue = $"{c.MemoryUsedGb:0.##}GB / {c.MemoryTotalGb:0.##}GB";
            DrawInlineLabelValue(canvas, cardRect.MidX + 20, metricsY, "Memory: ", memValue, MutedText, MainText);

            float divY = metricsY + 18;
            using (var divPaint = new SKPaint { IsAntialias = true, Color = Divider, StrokeWidth = 1 })
                canvas.DrawLine(cardRect.Left + 16, divY, cardRect.Right - 16, divY, divPaint);

            using var chartTitleFont = new SKFont { Size = 14 };
            using var chartTitlePaint = new SKPaint { IsAntialias = true, Color = MainText };
            canvas.DrawText("Usage History", cardRect.MidX, divY + 28, SKTextAlign.Center, chartTitleFont, chartTitlePaint);

            float legendY = divY + 52;
            DrawLegendItem(canvas, cardRect.MidX - 60, legendY, CpuBlue, "CPU %");
            DrawLegendItem(canvas, cardRect.MidX + 10, legendY, MemGreen, "Memory %");

            if (!string.IsNullOrWhiteSpace(c.Subtitle))
            {
                using var subFont = new SKFont { Size = 12 };
                using var subPaint = new SKPaint { IsAntialias = true, Color = MutedText };
                canvas.DrawText(c.Subtitle, x0, legendY + 22, SKTextAlign.Left, subFont, subPaint);
            }

            var plotRect = new SKRect(cardRect.Left + 50, legendY + 16, cardRect.Right - 24, cardRect.Bottom - 22);

            var plot = OxyChartService.BuildUsagePlot(c.UsageLastDay);
            var chartPng = OxyChartService.RenderPlotPng(plot, (int)plotRect.Width, (int)plotRect.Height);

            using (var chartBmp = SKBitmap.Decode(chartPng))
                canvas.DrawBitmap(chartBmp, plotRect);

            using var img = SKImage.FromBitmap(bmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        private static void DrawShadowedCard(SKCanvas canvas, SKRect rect, float radius)
        {
            using (var shadow = new SKPaint
            {
                IsAntialias = true,
                Color = new SKColor(0, 0, 0, 140),
                ImageFilter = SKImageFilter.CreateBlur(10, 10)
            })
            {
                var shadowRect = rect;
                shadowRect.Offset(0, 6);
                canvas.DrawRoundRect(shadowRect, radius, radius, shadow);
            }

            DrawRoundedRect(canvas, rect, radius, CardBg, CardBorder);
        }

        private static void DrawRoundedRect(SKCanvas canvas, SKRect rect, float radius, SKColor fill, SKColor? border)
        {
            using var fillPaint = new SKPaint { IsAntialias = true, Color = fill, Style = SKPaintStyle.Fill };
            canvas.DrawRoundRect(rect, radius, radius, fillPaint);

            if (border.HasValue)
            {
                using var borderPaint = new SKPaint { IsAntialias = true, Color = border.Value, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
                canvas.DrawRoundRect(rect, radius, radius, borderPaint);
            }
        }

        private static void DrawIconBadge(SKCanvas canvas, SKRect rect)
        {
            DrawRoundedRect(canvas, rect, 10, SKColor.Parse("#1f232b"), border: SKColor.Parse("#2b303a"));

            using var p = new SKPaint { IsAntialias = true, Color = MainText, Style = SKPaintStyle.Stroke, StrokeWidth = 1.8f };

            float cx = rect.MidX, cy = rect.MidY;
            float s = rect.Width * 0.22f;

            var f = new SKRect(cx - s, cy - s, cx + s, cy + s);
            canvas.DrawRect(f, p);

            float o = s * 0.7f;
            var b = new SKRect(f.Left - o, f.Top - o, f.Right - o, f.Bottom - o);
            canvas.DrawRect(b, p);

            canvas.DrawLine(f.Left, f.Top, b.Left, b.Top, p);
            canvas.DrawLine(f.Right, f.Top, b.Right, b.Top, p);
            canvas.DrawLine(f.Left, f.Bottom, b.Left, b.Bottom, p);
            canvas.DrawLine(f.Right, f.Bottom, b.Right, b.Bottom, p);
        }

        private static void DrawInlineLabelValue(SKCanvas canvas, float x, float y, string label, string value, SKColor labelColor, SKColor valueColor)
        {
            using var font = new SKFont { Size = 13 };
            using var labelPaint = new SKPaint { IsAntialias = true, Color = labelColor };
            using var valuePaint = new SKPaint { IsAntialias = true, Color = valueColor };

            canvas.DrawText(label, x, y, SKTextAlign.Left, font, labelPaint);
            float labelW = font.MeasureText(label);
            canvas.DrawText(value, x + labelW, y, SKTextAlign.Left, font, valuePaint);
        }

        private static void DrawLegendItem(SKCanvas canvas, float x, float y, SKColor color, string text)
        {
            using var dotPaint = new SKPaint { IsAntialias = true, Color = color, Style = SKPaintStyle.Fill };
            canvas.DrawCircle(x, y - 4, 3.5f, dotPaint);

            using var f = new SKFont { Size = 12 };
            using var p = new SKPaint { IsAntialias = true, Color = MainText };
            canvas.DrawText(text, x + 10, y, SKTextAlign.Left, f, p);
        }
    }
}
