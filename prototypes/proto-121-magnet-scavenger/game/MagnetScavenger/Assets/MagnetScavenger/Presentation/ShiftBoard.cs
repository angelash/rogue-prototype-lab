using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagnetShift
{
    /// <summary>The crane, field and parts use the same cell coordinates as the shift.</summary>
    public sealed class ShiftBoard : IDisposable
    {
        static Color32 C(int rgb) { return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255); }
        static readonly Color32 Ink = C(0x091722), Housing = C(0x223744), Edge = C(0x46616B);
        static readonly Color32 Mint = C(0x76E3CC), Gold = C(0xFFCB70), White = C(0xEDF4E9);
        static readonly Color32 Steel = C(0x8CA8AD), Dim = C(0x456570), SignalRed = C(0xFC9477);
        readonly Dictionary<char, Texture2D> parts = new Dictionary<char, Texture2D>();
        readonly List<Texture2D> textures = new List<Texture2D>();
        Texture2D car, bay, wall, orderTag;
        GUIStyle labels;
        Rect field;
        float cw, ch;
        float started = -10f;
        ShiftAction lastAction;
        ShiftPiece movingPart;
        ShiftCell previousEnd, lastDrawnEnd;
        ShiftGame lastGame;
        string previewHistory;
        ShiftResult cachedPreview;
        public bool Motion { get; set; } = true;

        public void Reset()
        {
            movingPart = null;
            started = -10f;
            lastGame = null;
            cachedPreview = null;
        }

        public void Notify(ShiftGame game, ShiftAction action, ShiftResult committed)
        {
            if (game == null || committed == null || !committed.Legal) return;
            previousEnd = lastGame == game ? lastDrawnEnd : game.End;
            lastAction = action;
            movingPart = committed.Piece;
            started = Time.unscaledTime;
        }

        public void Draw(Rect bounds, ShiftGame game, Font font)
        {
            if (Event.current.type != EventType.Repaint || game == null) return;
            if (car == null) BuildArt();
            labels.font = font;
            float w = bounds.width, h = bounds.height;
            float footer = Mathf.Clamp(h * .078f, 24f, 36f);
            float inner = Mathf.Clamp(w * .044f, 18f, 37f);
            float maxW = w - 2 * inner - 32f;
            ch = Mathf.Min((h - footer - 25f) / (game.Level.Height + 1.12f), maxW / (.9f * game.Level.Width));
            cw = Mathf.Min(ch * 1.23f, maxW / game.Level.Width);
            field = new Rect((w - cw * game.Level.Width) * .5f, (h - footer - ch * (game.Level.Height + 1.12f)) * .5f + ch * 1.12f,
                cw * game.Level.Width, ch * game.Level.Height);
            if (lastGame != game || previewHistory != game.History || cachedPreview == null)
            {
                cachedPreview = game.Preview(ShiftAction.Pulse);
                previewHistory = game.History;
            }
            lastGame = game;
            lastDrawnEnd = game.End;
            float elapsed = Motion ? Time.unscaledTime - started : 10f;
            float slide = 0f;
            if (elapsed < .14f && (lastAction == ShiftAction.Left || lastAction == ShiftAction.Right))
                slide = (lastAction == ShiftAction.Left ? 1f : -1f) * cw * (1f - Ease(elapsed / .14f));

            Color oldColor = GUI.color;
            GUI.BeginGroup(bounds);
            Enclosure(w, h, footer);
            Field(game);
            Vector2 carriage = Center(game.CarX, -1) + Vector2.right * slide;
            if (!game.Won && !game.Lost) Ray(game, cachedPreview, slide);
            foreach (var piece in game.Bin) Loose(piece, cachedPreview, game.Won || game.Lost);
            Assembly(game, slide);
            float trolleySize = Mathf.Min(cw * 1.25f, ch * 1.41f);
            Sprite(car, carriage + Vector2.up * ch * .045f, trolleySize);
            if (elapsed < .28f && movingPart != null) Animate(game, carriage, elapsed / .28f);
            Legend(w, h, footer);
            GUI.EndGroup();
            GUI.color = oldColor;
        }

        static float Ease(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
        Vector2 Center(float x, float y) { return new Vector2(field.x + (x + .5f) * cw, field.y + (y + .5f) * ch); }
        static void Fill(float x, float y, float w, float h, Color color)
        {
            if (w <= 0 || h <= 0) return;
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        }
        static void Tile(Texture2D tex, Rect rect)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, true);
        }
        static void Sprite(Texture2D tex, Vector2 p, float size) { Tile(tex, new Rect(p.x - size / 2, p.y - size / 2, size, size)); }
        static void Dot(Vector2 center, float size, Color color) { Fill(center.x - size / 2, center.y - size / 2, size, size, color); }
        static void Strut(Vector2 a, Vector2 b, float thick, Color color)
        {
            if (Mathf.Abs(a.x - b.x) > Mathf.Abs(a.y - b.y))
                Fill(Mathf.Min(a.x, b.x), a.y - thick / 2, Mathf.Abs(a.x - b.x) + .5f, thick, color);
            else
                Fill(a.x - thick / 2, Mathf.Min(a.y, b.y), thick, Mathf.Abs(a.y - b.y) + .5f, color);
        }
        void Label(Rect area, string text, float size, Color color)
        {
            labels.fontSize = Mathf.RoundToInt(size);
            labels.normal.textColor = color;
            GUI.color = Color.white;
            GUI.Label(area, text, labels);
        }
        void Enclosure(float w, float h, float footer)
        {
            Fill(0, 0, w, h, Edge);
            Fill(1, 2, w - 2, h - 3, Housing);
            Fill(7, 7, w - 14, h - 14, Ink);
            Fill(9, 8, w - 18, h - footer - 10, C(0x162A37));
            float gap = field.x - 10f;
            if (gap > 26f)
            {
                float vent = Mathf.Min(16f, gap * .29f);
                for (float y = field.y + 8f; y < field.yMax - 6f; y += 12f)
                {
                    Fill(13, y, vent, 3, Ink);
                    Fill(13, y + 3f, vent, 1, Housing);
                    Fill(w - 13f - vent, y, vent, 3, Ink);
                    Fill(w - 13f - vent, y + 3f, vent, 1, Housing);
                }
            }
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? 4f : w - 8f;
                Fill(x, 4, 4, 4, Steel); Fill(x + 1, 5, 2, 1, Ink);
                Fill(x, h - 8, 4, 4, Steel); Fill(x + 1, h - 7, 2, 1, Ink);
            }
        }

        void Field(ShiftGame game)
        {
            Fill(field.x - 3, field.y - 3, field.width + 6, field.height + 6, Ink);
            for (int y = 0; y < game.Level.Height; y++)
            {
                Label(new Rect(field.x - 24, field.y + ch * y, 18, ch), (y + 1).ToString(), Mathf.Clamp(ch * .2f, 9, 13), Steel);
                for (int x = 0; x < game.Level.Width; x++)
                {
                    var rect = new Rect(field.x + x * cw, field.y + y * ch, cw, ch);
                    Tile(game.Level.IsWall(new ShiftCell(x, y)) ? wall : bay, rect);
                }
            }
            float railY = Center(0, -1).y - ch * .09f;
            float railH = Mathf.Clamp(ch * .23f, 8f, 16f);
            Fill(field.x - 7f, railY - railH / 2 - 2, field.width + 14, railH + 5, Ink);
            Fill(field.x - 6f, railY - railH / 2, field.width + 12, railH, Edge);
            Fill(field.x - 6f, railY - railH / 2, field.width + 12, 2, Steel);
            Fill(field.x - 6f, railY - 1f, field.width + 12, railH * .38f, Ink);
            for (int i = 0; i < game.Level.Width; i++)
            {
                float center = Center(i, 0).x;
                Fill(center - cw * .36f, railY - 2, 2, 4, Steel);
                Fill(center + cw * .36f, railY - 2, 2, 4, Steel);
                Label(new Rect(center - cw / 2, field.y - ch * 1.20f, cw, ch * .3f),
                    (i + 1).ToString("00"), Mathf.Clamp(ch * .18f, 9f, 13f), i == game.CarX ? Gold : Steel);
            }
        }
        void Ray(ShiftGame game, ShiftResult preview, float slide)
        {
            if (preview.Ray.Count == 0) return;
            ShiftCell first = game.End, last = preview.Ray[preview.Ray.Count - 1];
            Vector2 a = Center(first.X, first.Y) + Vector2.right * slide;
            Vector2 b = Center(last.X, last.Y) + Vector2.right * slide;
            Vector2 dir = new Vector2(game.Heading.X, game.Heading.Y);
            float size = Mathf.Min(cw, ch);
            bool obstructed = !preview.Legal && (preview.Piece != null || game.Level.IsWall(last));
            Color color = obstructed ? SignalRed : Mint;
            foreach (ShiftCell cell in preview.Ray)
            {
                Vector2 p = Center(cell.X, cell.Y) + Vector2.right * slide;
                if (game.Level.IsWall(cell)) continue;
                Fill(p.x - cw * .48f, p.y - ch * .48f, cw * .96f, ch * .96f,
                    obstructed ? new Color(.96f, .40f, .24f, .09f) : new Color(.22f, .85f, .69f, .12f));
            }
            float length = Vector2.Distance(a, b);
            if (length < 1) return;
            for (float t = size * .23f; t < length; t += size * .26f)
                Strut(a + dir * t, a + dir * Mathf.Min(t + size * .13f, length), Mathf.Max(2f, size * .042f), color);
            if (preview.Piece == null)
            {
                Vector2 across = new Vector2(-dir.y, dir.x);
                Strut(b - across * size * .11f, b + across * size * .11f, 2, color);
            }
        }
        void Loose(ShiftPiece piece, ShiftResult preview, bool finished)
        {
            Vector2 p = Center(piece.Origin.X, piece.Origin.Y);
            float s = Mathf.Min(cw, ch);
            if (!finished && preview.Piece != null && preview.Piece.Id == piece.Id)
                Bracket(p, s * .43f, Mathf.Max(2, s * .043f), preview.Legal ? Mint : SignalRed);
            Sprite(parts[piece.Kind], p, s * .94f);
            if (piece.Target) Sprite(orderTag, p + new Vector2(s * .29f, -s * .29f), s * .31f);
        }
        static void Bracket(Vector2 p, float radius, float weight, Color color)
        {
            float reach = radius * .38f;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            {
                Vector2 corner = p + new Vector2(x, y) * radius;
                Strut(corner, corner - Vector2.right * x * reach, weight, color);
                Strut(corner, corner - Vector2.up * y * reach, weight, color);
            }
        }
        void Assembly(ShiftGame game, float slide)
        {
            float s = Mathf.Min(cw, ch);
            Vector2 prev = Center(game.CarX, -1) + Vector2.right * slide;
            int index = 0;
            foreach (ShiftPiece piece in game.Chain)
            {
                for (int i = 0; i < piece.Length; i++, index++)
                {
                    ShiftCell cell = game.ChainCells[index];
                    Vector2 p = Center(cell.X, cell.Y) + Vector2.right * slide;
                    Strut(prev + Vector2.one * 2, p + Vector2.one * 2, s * .28f, Ink);
                    Strut(prev, p, s * .21f, C(0x3E7777));
                    Strut(prev, p, s * .10f, Mint);
                    Vector2 dir = (p - prev).normalized, tangent = new Vector2(-dir.y, dir.x);
                    Vector2 joint = (p + prev) * .5f;
                    Strut(joint - tangent * s * .15f, joint + tangent * s * .15f, s * .08f, Ink);
                    Strut(joint - tangent * s * .13f, joint + tangent * s * .13f, s * .045f, Steel);
                    if (i == piece.Length - 1)
                    {
                        // The elbow drawing accepts a connection from above and opens right.
                        // Turn the drawing with its incoming link, including side and reverse picks.
                        var matrix = GUI.matrix;
                        if (piece.Kind == 'e')
                            GUIUtility.RotateAroundPivot(Mathf.Atan2(-dir.x, dir.y) * Mathf.Rad2Deg, p);
                        Sprite(parts[piece.Kind], p, s * .73f);
                        GUI.matrix = matrix;
                        if (piece.Target) Sprite(orderTag, p + new Vector2(s * .24f, -s * .24f), s * .27f);
                    }
                    prev = p;
                }
            }
            if (game.Chain.Count == 0) return;
            Vector2 heading = new Vector2(game.Heading.X, game.Heading.Y);
            Vector2 tip = prev + heading * s * .38f;
            Vector2 side = new Vector2(-heading.y, heading.x);
            Strut(prev + heading * s * .22f, tip, s * .135f, Mint);
            Strut(tip - side * s * .13f, tip + side * s * .13f, s * .072f, Mint);
            Strut(tip + side * s * .11f, tip + side * s * .11f + heading * s * .10f, s * .065f, White);
            Strut(tip - side * s * .11f, tip - side * s * .11f + heading * s * .10f, s * .065f, White);
        }
        void Animate(ShiftGame game, Vector2 carriage, float t)
        {
            Vector2 start, end;
            if (lastAction == ShiftAction.Pulse)
            {
                start = Center(movingPart.Origin.X, movingPart.Origin.Y);
                end = Center(game.End.X, game.End.Y);
            }
            else if (lastAction == ShiftAction.Unload)
            {
                start = Center(previousEnd.X, previousEnd.Y);
                end = carriage;
            }
            else return;
            float s = Mathf.Min(cw, ch);
            float progress = Ease(t);
            Vector2 point = Vector2.Lerp(start, end, progress);
            point.y -= Mathf.Sin(progress * Mathf.PI) * s * .22f;
            if (t < .88f) Sprite(parts[movingPart.Kind], point, s * Mathf.Lerp(.9f, .51f, progress));
            if (t > .55f)
            {
                float ring = Mathf.InverseLerp(.55f, 1f, t);
                var color = (Color)(lastAction == ShiftAction.Pulse ? Mint : Gold);
                color.a = 1f - ring;
                Bracket(end, s * (.20f + ring * .30f), 2f, color);
            }
        }
        void Legend(float w, float h, float footer)
        {
            float size = Mathf.Clamp(footer * .39f, 10f, 14f);
            float x = w / 2f - size * 9.1f, y = h - footer / 2f - 2;
            Fill(9, h - footer + 1, w - 18, 1, Edge);
            Sprite(orderTag, new Vector2(x, y), size * 1.34f);
            Label(new Rect(x + size * .9f, y - footer / 2f, size * 3.3f, footer), "订单件", size, White);
            x += size * 6.4f;
            for (int i = 0; i < 3; i++) Fill(x + i * size * .4f, y - 1, size * .24f, 2, Mint);
            Label(new Rect(x + size * 1.25f, y - footer / 2f, size * 4.4f, footer), "吸取范围", size, White);
            x += size * 7.9f;
            Tile(wall, new Rect(x - size * .6f, y - size * .6f, size * 1.2f, size * 1.2f));
            Label(new Rect(x + size * .8f, y - footer / 2f, size * 2.5f, footer), "隔板", size, White);
        }

        void BuildArt()
        {
            labels = new GUIStyle { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            car = Make("Trolley", CarArt);
            bay = Make("Bay", BayArt);
            wall = Make("Baffle", WallArt);
            orderTag = Make("Order", TagArt);
            parts.Add('r', Make("TelescopicRod", RodArt));
            parts.Add('e', Make("QuarterBend", ElbowArt));
            parts.Add('p', Make("SplicePlate", PlateArt));
            parts.Add('h', Make("Armature", CoreArt));
        }
        Texture2D Make(string name, Action<Stamp> draw)
        {
            var stamp = new Stamp();
            draw(stamp);
            Texture2D texture = stamp.Finish(name);
            textures.Add(texture);
            return texture;
        }
        public void Dispose()
        {
            foreach (Texture2D texture in textures) if (texture != null) UnityEngine.Object.Destroy(texture);
            textures.Clear();
            parts.Clear();
            car = null;
            Reset();
        }

        // Parts are stamped once, in local 100-unit artwork. No imported textures or physics proxies.
        sealed class Stamp
        {
            const int N = 144;
            const float Scale = N / 100f;
            readonly Color32[] pixels = new Color32[N * N];
            void Paint(int x, int y, Color32 c, float coverage)
            {
                if (coverage <= 0f) return;
                int i = (N - 1 - y) * N + x;
                Color32 old = pixels[i];
                float alpha = Mathf.Clamp01(coverage) * c.a / 255f;
                float outA = alpha + old.a / 255f * (1f - alpha);
                var result = Color32.Lerp(old, c, alpha / outA);
                result.a = (byte)Mathf.RoundToInt(outA * 255f);
                pixels[i] = result;
            }
            public void Rect(float x, float y, float w, float h, int rgb)
            {
                Color32 c = C(rgb);
                int x0 = Mathf.Clamp(Mathf.RoundToInt(x * Scale), 0, N), x1 = Mathf.Clamp(Mathf.RoundToInt((x + w) * Scale), 0, N);
                int y0 = Mathf.Clamp(Mathf.RoundToInt(y * Scale), 0, N), y1 = Mathf.Clamp(Mathf.RoundToInt((y + h) * Scale), 0, N);
                for (int iy = y0; iy < y1; iy++)
                for (int ix = x0; ix < x1; ix++) pixels[(N - 1 - iy) * N + ix] = c;
            }
            public void Bar(float ax, float ay, float bx, float by, float width, int rgb)
            {
                float r = width * .5f, dx = bx - ax, dy = by - ay, sq = dx * dx + dy * dy;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(ax,bx)-r-1)*Scale));
                int y0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(ay,by)-r-1)*Scale));
                int x1 = Mathf.Min(N - 1, Mathf.CeilToInt((Mathf.Max(ax,bx)+r+1)*Scale));
                int y1 = Mathf.Min(N - 1, Mathf.CeilToInt((Mathf.Max(ay,by)+r+1)*Scale));
                Color32 c = C(rgb);
                for (int iy = y0; iy <= y1; iy++)
                for (int ix = x0; ix <= x1; ix++)
                {
                    float x = (ix + .5f) / Scale - ax, y = (iy + .5f) / Scale - ay;
                    float t = sq == 0f ? 0f : Mathf.Clamp01((x * dx + y * dy) / sq);
                    x -= t * dx; y -= t * dy;
                    Paint(ix, iy, c, (r - Mathf.Sqrt(x * x + y * y)) * Scale + .5f);
                }
            }
            public void Disc(float x, float y, float d, int rgb) { Bar(x, y, x, y, d, rgb); }
            public void Poly(int rgb, params float[] xy)
            {
                // Four subpixels on each polygon boundary keep cut metal edges crisp at board scale.
                Color32 c = C(rgb);
                int n = xy.Length / 2;
                float minX = 100, minY = 100, maxX = 0, maxY = 0;
                for (int v=0; v<n; v++) { minX=Mathf.Min(minX,xy[2*v]); maxX=Mathf.Max(maxX,xy[2*v]); minY=Mathf.Min(minY,xy[2*v+1]); maxY=Mathf.Max(maxY,xy[2*v+1]); }
                for (int iy = Mathf.Max(0, Mathf.FloorToInt(minY*Scale)); iy < Mathf.Min(N,Mathf.CeilToInt(maxY*Scale)); iy++)
                for (int ix = Mathf.Max(0, Mathf.FloorToInt(minX*Scale)); ix < Mathf.Min(N,Mathf.CeilToInt(maxX*Scale)); ix++)
                {
                    int inside = 0;
                    for (int k=0; k<4; k++)
                    {
                        float x = (ix + ((k & 1) == 0 ? .25f : .75f)) / Scale;
                        float y = (iy + (k < 2 ? .25f : .75f)) / Scale;
                        bool hit = false;
                        for (int i=0, j=n-1; i<n; j=i++)
                        {
                            float x0=xy[j*2], y0=xy[j*2+1], x1=xy[i*2], y1=xy[i*2+1];
                            if ((y0>y)!=(y1>y) && x < x0+(y-y0)*(x1-x0)/(y1-y0)) hit = !hit;
                        }
                        if (hit) inside++;
                    }
                    Paint(ix, iy, c, inside * .25f);
                }
            }
            public void Bolt(float x, float y, float d)
            {
                Disc(x, y + .9f, d + 1f, 0x101D25); Disc(x, y, d, 0x8FA2A3);
                Bar(x - d * .19f, y + d * .12f, x + d * .19f, y - d * .12f, d * .22f, 0x344953);
            }
            public Texture2D Finish(string name)
            {
                var t = new Texture2D(N, N, TextureFormat.RGBA32, true, false);
                t.name = "Shift " + name;
                t.hideFlags = HideFlags.HideAndDontSave;
                t.filterMode = FilterMode.Trilinear; t.wrapMode = TextureWrapMode.Clamp;
                t.SetPixels32(pixels); t.Apply(true, true); return t;
            }
        }

        static void BayArt(Stamp s)
        {
            s.Rect(0,0,100,100,0x11232F);
            s.Rect(1.3f,1.3f,97.3f,97.2f,0x233947);
            s.Rect(2.7f,2.7f,94.6f,94.6f,0x1B303E);
            for (int y=12; y<100; y+=13)
            for (int x=12; x<100; x+=13) s.Disc(x, y, 1.3f, 0x2F4753);
            s.Bar(46,50,54,50,1.5f,0x344D58);
            s.Bar(50,46,50,54,1.5f,0x344D58);
            s.Rect(5,5,7,1.2f,0x42606A); s.Rect(5,5,1.2f,7,0x42606A);
        }
        static void WallArt(Stamp s)
        {
            s.Rect(0,0,100,100,0x0D1C27);
            s.Poly(0x637681,2,12,12,2,88,2,98,12,98,88,89,98,10,98,2,90);
            s.Poly(0x374957,5,12,13,5,87,5,95,13,95,85,85,95,12,95,5,86);
            s.Rect(10,12,80,15,0x465B68); s.Rect(10,73,80,15,0x293D49);
            s.Rect(5,32,90,35,0x172B36);
            for (int x=8; x<79; x+=19)
                s.Poly(0x79765C,x,62,x+10,37,x+19,37,x+9,62);
            s.Bolt(15,17,7); s.Bolt(85,17,7);
            s.Bolt(15,82,7); s.Bolt(85,82,7);
        }
        static void TagArt(Stamp s)
        {
            s.Poly(0x14252B,50,1,99,50,50,99,1,50);
            s.Poly(0xFFCD74,50,8,92,50,50,92,8,50);
            s.Poly(0x846132,50,33,67,50,50,67,33,50);
        }
        static void CarArt(Stamp s)
        {
            s.Disc(25,38,19,0x0C1A22); s.Disc(75,38,19,0x0C1A22);
            s.Disc(25,38,11,0x81949A); s.Disc(75,38,11,0x81949A);
            s.Bar(50,43,50,70,11,0x101E26); s.Bar(50,44,50,70,5,0x9BAEAA);
            s.Poly(0x0A1821,12,26,22,17,77,17,88,29,88,53,77,61,22,61,12,52);
            s.Poly(0xAF7537,15,27,24,19,76,19,85,28,85,50,75,57,23,57,15,50);
            s.Poly(0xF3B45E,16,27,25,20,75,20,82,27,82,46,76,50,24,50,16,45);
            s.Bar(26,24,72,24,2,0xFFE0A0);
            s.Rect(25,31,20,11,0x3E5357);
            for (int x=28; x<=39; x+=4) s.Rect(x,32,1.8f,8,0x142932);
            s.Rect(55,31,14,10,0x6B512F); s.Rect(57,33,9,4,0xFFDB90);
            s.Bolt(20,47,5); s.Bolt(78,47,5);
            s.Poly(0x10212B,33,70,41,61,59,61,67,70,67,88,56,88,56,77,44,77,44,88,33,88);
            s.Poly(0xF3B45E,35,70,42,63,58,63,65,70,65,85,58,85,58,74,42,74,42,85,35,85);
            s.Rect(35,80,7,5,0xCADBCE); s.Rect(58,80,7,5,0xCADBCE);
        }
        static void RodArt(Stamp s)
        {
            s.Bar(22,68,79,37,15,0x0B1920); s.Disc(24,66,27,0x0B1920); s.Disc(77,36,23,0x0B1920);
            s.Bar(23,62,77,33,11,0x7DADAD); s.Bar(29,57,73,34,4,0xD6E5D5);
            s.Bar(31,60,55,47,18,0x36585E); s.Bar(31,57,55,44,12,0x8CBDAD);
            s.Bar(32,54,53,43,3,0xCFEDD5);
            s.Disc(23,62,25,0x7FA7A5); s.Disc(23,62,15,0x405B60); s.Disc(23,62,9,0x172C38);
            s.Disc(78,32,22,0x9FC5BB); s.Disc(78,32,12,0x39545B); s.Disc(78,32,7,0x182B34);
            s.Bar(51,42,57,53,3,0x476B6D);
        }
        static void PlateArt(Stamp s)
        {
            s.Poly(0x091A25,20,36,32,24,71,24,83,38,74,70,64,79,25,79,15,66);
            s.Poly(0x526E87,20,31,32,22,70,22,81,34,71,67,62,75,26,75,16,63);
            s.Poly(0xA4BED0,23,32,33,25,68,25,76,35,68,62,59,69,29,69,21,61);
            s.Bar(32,30,65,30,2,0xE3E8DC);
            s.Disc(37,47,20,0x6F8995); s.Disc(37,45,14,0x20323F);
            s.Disc(61,47,12,0x607C89); s.Disc(61,45,7,0x253744);
            s.Bar(32,61,57,61,2,0x728E9E);
        }
        static void ElbowArt(Stamp s)
        {
            s.Bar(35,24,35,57,29,0x0B1921); s.Bar(35,59,72,59,29,0x0B1921);
            s.Bar(35,23,35,54,24,0x946447); s.Bar(35,54,72,54,24,0xAB7350);
            s.Bar(31,23,31,52,10,0xE2A770); s.Bar(36,50,71,50,10,0xF0BA7D);
            s.Disc(35,54,19,0xBD8559); s.Disc(34,52,11,0xE2AB73);
            s.Rect(18,17,33,11,0x855E46); s.Rect(18,17,33,4,0xF0C186);
            s.Rect(68,37,11,34,0x96684D); s.Rect(68,37,4,34,0xE4B483);
            s.Bar(26,33,43,33,2,0x855B42); s.Bar(58,45,58,62,2,0x925F42);
        }
        static void CoreArt(Stamp s)
        {
            s.Bar(51,18,51,84,13,0x152530); s.Bar(49,17,49,81,7,0x9EB3AF);
            s.Poly(0x11222C,32,20,64,20,78,33,78,66,66,80,32,80,21,66,21,34);
            s.Poly(0x597181,32,21,63,21,73,31,73,65,63,74,33,74,25,65,25,33);
            s.Rect(29,34,40,29,0x593D39);
            for (int i=0; i<5; i++)
            {
                float x=32+i*7;
                s.Bar(x,36,x,61,6,0xB77152); s.Bar(x-1,37,x-1,56,2,0xEEAE77);
            }
            s.Rect(24,29,50,7,0x9EB0B2); s.Rect(24,62,50,8,0x7B929A);
            s.Rect(25,29,48,2,0xD6DCD0); s.Bolt(31,66,5); s.Bolt(67,66,5);
        }
    }
}
