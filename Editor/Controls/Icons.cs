using UnityEngine;
using UnityEngine.UIElements;

namespace AndroidWireless
{
    /// <summary>
    /// Painter2D로 그리는 벡터 아이콘. 16×16 좌표계로 그리고 요소 크기에 맞춰 늘린다.
    /// 색은 USS `color`를 따르므로 테마 토큰으로 칠할 수 있다.
    /// </summary>
    public abstract class IconElement : VisualElement
    {
        protected IconElement()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("aw-icon");
            generateVisualContent += OnGenerate;
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyle(customStyle));
        }

        protected virtual void OnCustomStyle(ICustomStyle style) => MarkDirtyRepaint();

        private void OnGenerate(MeshGenerationContext mgc)
        {
            var rect = contentRect;
            float size = Mathf.Min(rect.width, rect.height);
            if (size <= 0) return;

            var origin = new Vector2(rect.x + (rect.width - size) * 0.5f, rect.y + (rect.height - size) * 0.5f);
            Draw(new IconCanvas(mgc.painter2D, origin, size / 16f), resolvedStyle.color);
        }

        protected abstract void Draw(IconCanvas c, Color color);
    }

    public readonly struct IconCanvas
    {
        private readonly Painter2D painter;
        private readonly Vector2 origin;
        private readonly float scale;

        public IconCanvas(Painter2D painter, Vector2 origin, float scale)
        {
            this.painter = painter;
            this.origin = origin;
            this.scale = scale;
        }

        private Vector2 Pt(float x, float y) => origin + new Vector2(x, y) * scale;

        private void BeginStroke(Color color, float width)
        {
            painter.strokeColor = color;
            painter.lineWidth = width * scale;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
        }

        /// <summary>각도는 +x 기준 시계 방향(화면 좌표). -90°가 위쪽.</summary>
        public void Arc(float cx, float cy, float radius, float fromDeg, float toDeg, Color color, float width)
        {
            BeginStroke(color, width);
            painter.Arc(Pt(cx, cy), radius * scale, Angle.Degrees(fromDeg), Angle.Degrees(toDeg));
            painter.Stroke();
        }

        public void Line(float x1, float y1, float x2, float y2, Color color, float width)
        {
            BeginStroke(color, width);
            painter.MoveTo(Pt(x1, y1));
            painter.LineTo(Pt(x2, y2));
            painter.Stroke();
        }

        public void Polyline(Color color, float width, params Vector2[] points)
        {
            BeginStroke(color, width);
            painter.MoveTo(Pt(points[0].x, points[0].y));
            for (int i = 1; i < points.Length; i++) painter.LineTo(Pt(points[i].x, points[i].y));
            painter.Stroke();
        }

        public void Ring(float cx, float cy, float radius, Color color, float width)
        {
            BeginStroke(color, width);
            painter.Arc(Pt(cx, cy), radius * scale, Angle.Degrees(0), Angle.Degrees(360));
            painter.ClosePath();
            painter.Stroke();
        }

        public void Disc(float cx, float cy, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.Arc(Pt(cx, cy), radius * scale, Angle.Degrees(0), Angle.Degrees(360));
            painter.ClosePath();
            painter.Fill();
        }
    }

    [UxmlElement]
    public partial class WifiIcon : IconElement
    {
        protected override void Draw(IconCanvas c, Color color)
        {
            for (int i = 1; i <= 3; i++)
                c.Arc(8, 13, 3.2f * i, -135, -45, color, 1.25f);
            c.Disc(8, 13, 1.1f, color);
        }
    }

    [UxmlElement]
    public partial class BugIcon : IconElement
    {
        protected override void Draw(IconCanvas c, Color color)
        {
            const float w = 1.1f;
            c.Ring(8, 9.6f, 4.1f, color, w);
            c.Arc(8, 5.6f, 2.3f, 180, 360, color, w);
            c.Line(8, 6.6f, 8, 13.6f, color, w);
            // 다리 (좌우 3개씩)
            c.Line(4.3f, 7.6f, 2.0f, 6.0f, color, w);
            c.Line(3.9f, 9.9f, 1.4f, 9.9f, color, w);
            c.Line(4.4f, 12.1f, 2.2f, 13.9f, color, w);
            c.Line(11.7f, 7.6f, 14.0f, 6.0f, color, w);
            c.Line(12.1f, 9.9f, 14.6f, 9.9f, color, w);
            c.Line(11.6f, 12.1f, 13.8f, 13.9f, color, w);
            // 더듬이
            c.Line(6.6f, 3.6f, 5.6f, 2.0f, color, w);
            c.Line(9.4f, 3.6f, 10.4f, 2.0f, color, w);
        }
    }

    [UxmlElement]
    public partial class SearchIcon : IconElement
    {
        protected override void Draw(IconCanvas c, Color color)
        {
            c.Ring(6.8f, 6.8f, 4.6f, color, 1.3f);
            c.Line(10.3f, 10.3f, 14.2f, 14.2f, color, 1.5f);
        }
    }

    /// <summary>와이파이 + 초록 더하기 배지. 배지 색은 USS 사용자 속성 --aw-icon-accent.</summary>
    [UxmlElement]
    public partial class PairIcon : IconElement
    {
        private static readonly CustomStyleProperty<Color> AccentProperty = new CustomStyleProperty<Color>("--aw-icon-accent");
        private Color accent = new Color(0.37f, 0.72f, 0.4f);

        protected override void OnCustomStyle(ICustomStyle style)
        {
            if (style.TryGetValue(AccentProperty, out var value)) accent = value;
            MarkDirtyRepaint();
        }

        protected override void Draw(IconCanvas c, Color color)
        {
            for (int i = 1; i <= 3; i++)
                c.Arc(6.6f, 10.2f, 2.3f * i, -150, -30, color, 1.1f);
            c.Disc(6.6f, 10.2f, 0.9f, color);

            c.Ring(11.8f, 12.2f, 3.1f, accent, 1.1f);
            c.Line(11.8f, 10.7f, 11.8f, 13.7f, accent, 1.1f);
            c.Line(10.3f, 12.2f, 13.3f, 12.2f, accent, 1.1f);
        }
    }

    /// <summary>닫기(X) 아이콘.</summary>
    [UxmlElement]
    public partial class CloseIcon : IconElement
    {
        protected override void Draw(IconCanvas c, Color color)
        {
            c.Line(4, 4, 12, 12, color, 1.4f);
            c.Line(12, 4, 4, 12, color, 1.4f);
        }
    }

    /// <summary>성공 화면의 체크 원. 원 색은 `color`, 체크는 흰색.</summary>
    [UxmlElement]
    public partial class CheckCircleIcon : IconElement
    {
        protected override void Draw(IconCanvas c, Color color)
        {
            c.Disc(8, 8, 8, color);
            c.Polyline(Color.white, 1.5f, new Vector2(4.6f, 8.3f), new Vector2(7.0f, 10.6f), new Vector2(11.6f, 5.6f));
        }
    }

    /// <summary>Android Studio식 8살 스피너. 화면에 붙어 있고 보일 때만 돈다.</summary>
    [UxmlElement]
    public partial class Spinner : IconElement
    {
        private const int Spokes = 8;
        private int step;
        private IVisualElementScheduledItem tick;

        public Spinner()
        {
            AddToClassList("aw-spinner");
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                tick = schedule.Execute(() =>
                {
                    if (resolvedStyle.display == DisplayStyle.None || resolvedStyle.visibility == Visibility.Hidden) return;
                    step = (step + 1) % Spokes;
                    MarkDirtyRepaint();
                }).Every(90);
            });
            RegisterCallback<DetachFromPanelEvent>(_ => tick?.Pause());
        }

        protected override void Draw(IconCanvas c, Color color)
        {
            for (int i = 0; i < Spokes; i++)
            {
                float angle = (i * 360f / Spokes - 90f) * Mathf.Deg2Rad;
                float alpha = 1f - ((step - i + Spokes) % Spokes) / (float)Spokes * 0.85f;
                var spokeColor = new Color(color.r, color.g, color.b, color.a * alpha);
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                c.Line(8 + cos * 3.4f, 8 + sin * 3.4f, 8 + cos * 6.6f, 8 + sin * 6.6f, spokeColor, 1.6f);
            }
        }
    }
}
