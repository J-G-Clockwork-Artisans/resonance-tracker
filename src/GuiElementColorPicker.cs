using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ResonanceTracker
{
    public static class ColorPickerMath
    {
        public static void HsvToRgb(double h, double s, double v, out double r, out double g, out double b)
        {
            if (s == 0)
            {
                r = v;
                g = v;
                b = v;
                return;
            }

            double sector = h / 60.0;
            int i = (int)Math.Floor(sector);
            double f = sector - i;
            double p = v * (1.0 - s);
            double q = v * (1.0 - s * f);
            double t = v * (1.0 - s * (1.0 - f));

            switch (i % 6)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }
        }

        public static void RgbToHsv(double r, double g, double b, out double h, out double s, out double v)
        {
            double min = Math.Min(r, Math.Min(g, b));
            double max = Math.Max(r, Math.Max(g, b));
            v = max;
            double delta = max - min;

            if (max != 0)
            {
                s = delta / max;
            }
            else
            {
                s = 0;
                h = 0;
                return;
            }

            if (delta == 0)
            {
                h = 0;
            }
            else
            {
                if (r == max)
                    h = (g - b) / delta;
                else if (g == max)
                    h = 2.0 + (b - r) / delta;
                else
                    h = 4.0 + (r - g) / delta;

                h *= 60.0;
                if (h < 0)
                    h += 360.0;
            }
        }

        public static string RgbToHex(double r, double g, double b)
        {
            int ri = GameMath.Clamp((int)(r * 255.0), 0, 255);
            int gi = GameMath.Clamp((int)(g * 255.0), 0, 255);
            int bi = GameMath.Clamp((int)(b * 255.0), 0, 255);
            return string.Format("#{0:X2}{1:X2}{2:X2}", ri, gi, bi);
        }

        public static bool HexToRgb(string hex, out double r, out double g, out double b)
        {
            r = 1.0; g = 1.0; b = 1.0;
            try
            {
                if (string.IsNullOrEmpty(hex)) return false;
                if (hex.StartsWith("#")) hex = hex.Substring(1);
                if (hex.Length != 6) return false;

                int ri = Convert.ToInt32(hex.Substring(0, 2), 16);
                int gi = Convert.ToInt32(hex.Substring(2, 2), 16);
                int bi = Convert.ToInt32(hex.Substring(4, 2), 16);

                r = ri / 255.0;
                g = gi / 255.0;
                b = bi / 255.0;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class GuiElementColorSquare : GuiElement
    {
        private string _colorHex;
        private LoadedTexture _texture;

        public GuiElementColorSquare(ICoreClientAPI capi, ElementBounds bounds, string colorHex) : base(capi, bounds)
        {
            _colorHex = colorHex;
            _texture = new LoadedTexture(capi);
        }

        public void SetColor(string colorHex)
        {
            if (_colorHex != colorHex)
            {
                _colorHex = colorHex;
                Recompose();
            }
        }

        public void Recompose()
        {
            _texture?.Dispose();
            _texture = new LoadedTexture(api);

            int width = (int)Bounds.OuterWidth;
            int height = (int)Bounds.OuterHeight;
            if (width <= 0 || height <= 0) return;

            var surface = new ImageSurface(Format.Argb32, width, height);
            using (var ctx = new Context(surface))
            {
                ctx.SetSourceRGBA(0, 0, 0, 0);
                ctx.Paint();

                double r, g, b;
                ColorPickerMath.HexToRgb(_colorHex, out r, out g, out b);
                
                ctx.SetSourceRGBA(r, g, b, 1.0);
                ctx.Rectangle(1, 1, width - 2, height - 2);
                ctx.Fill();

                ctx.SetSourceRGBA(0.3, 0.2, 0.1, 1.0); // vintage dark brown
                ctx.LineWidth = 2;
                ctx.Rectangle(1, 1, width - 2, height - 2);
                ctx.Stroke();
            }
            _texture.TextureId = api.Gui.LoadCairoTexture(surface, false);
            surface.Dispose();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (_texture != null && !_texture.Disposed)
            {
                if (_texture.TextureId == 0)
                {
                    Recompose();
                }
                api.Render.Render2DTexturePremultipliedAlpha(_texture.TextureId, (float)Bounds.renderX, (float)Bounds.renderY, (float)Bounds.OuterWidth, (float)Bounds.OuterHeight);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _texture?.Dispose();
        }
    }

    public class GuiElementHsvBox : GuiElement
    {
        private double _hue;
        private double _saturation;
        private double _value;
        private readonly Action<double, double> _onColorChanged;
        private LoadedTexture _texture;
        private bool _isDragging;

        public GuiElementHsvBox(ICoreClientAPI capi, ElementBounds bounds, double hue, double saturation, double value, Action<double, double> onColorChanged) 
            : base(capi, bounds)
        {
            _hue = hue;
            _saturation = saturation;
            _value = value;
            _onColorChanged = onColorChanged;
            _texture = new LoadedTexture(capi);
        }

        public void UpdateHue(double hue)
        {
            if (Math.Abs(_hue - hue) > 0.001)
            {
                _hue = hue;
                Recompose();
            }
        }

        public void UpdateSaturationValue(double saturation, double value)
        {
            if (Math.Abs(_saturation - saturation) > 0.001 || Math.Abs(_value - value) > 0.001)
            {
                _saturation = saturation;
                _value = value;
                Recompose();
            }
        }

        public void Recompose()
        {
            _texture?.Dispose();
            _texture = new LoadedTexture(api);

            int width = (int)Bounds.OuterWidth;
            int height = (int)Bounds.OuterHeight;
            if (width <= 0 || height <= 0) return;

            var surface = new ImageSurface(Format.Argb32, width, height);
            using (var ctx = new Context(surface))
            {
                ctx.SetSourceRGBA(0, 0, 0, 0);
                ctx.Paint();

                double r, g, b;
                ColorPickerMath.HsvToRgb(_hue, 1.0, 1.0, out r, out g, out b);

                using (var hGrad = new LinearGradient(0, 0, width, 0))
                {
                    hGrad.AddColorStop(0, new Color(1.0, 1.0, 1.0, 1.0));
                    hGrad.AddColorStop(1, new Color(r, g, b, 1.0));
                    ctx.SetSource(hGrad);
                    ctx.Rectangle(0, 0, width, height);
                    ctx.Fill();
                }

                using (var vGrad = new LinearGradient(0, 0, 0, height))
                {
                    vGrad.AddColorStop(0, new Color(0, 0, 0, 0));
                    vGrad.AddColorStop(1, new Color(0, 0, 0, 1.0));
                    ctx.SetSource(vGrad);
                    ctx.Rectangle(0, 0, width, height);
                    ctx.Fill();
                }

                double selectionX = _saturation * width;
                double selectionY = (1.0 - _value) * height;

                ctx.SetSourceRGBA(0, 0, 0, 1.0);
                ctx.LineWidth = 2.0;
                ctx.Arc(selectionX, selectionY, 6.0, 0, 2 * Math.PI);
                ctx.Stroke();

                ctx.SetSourceRGBA(1.0, 1.0, 1.0, 1.0);
                ctx.LineWidth = 1.5;
                ctx.Arc(selectionX, selectionY, 5.0, 0, 2 * Math.PI);
                ctx.Stroke();

                ctx.SetSourceRGBA(0.3, 0.2, 0.1, 1.0);
                ctx.LineWidth = 2;
                ctx.Rectangle(1, 1, width - 2, height - 2);
                ctx.Stroke();
            }

            _texture.TextureId = api.Gui.LoadCairoTexture(surface, false);
            surface.Dispose();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (_texture != null && !_texture.Disposed)
            {
                if (_texture.TextureId == 0)
                {
                    Recompose();
                }
                api.Render.Render2DTexturePremultipliedAlpha(_texture.TextureId, (float)Bounds.renderX, (float)Bounds.renderY, (float)Bounds.OuterWidth, (float)Bounds.OuterHeight);
            }
        }

        public override void OnMouseDown(ICoreClientAPI api, MouseEvent args)
        {
            base.OnMouseDown(api, args);
            if (Bounds.PointInside(args.X, args.Y))
            {
                _isDragging = true;
                UpdateFromMouse(args.X, args.Y);
                args.Handled = true;
            }
        }

        public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
        {
            base.OnMouseMove(api, args);
            if (_isDragging)
            {
                UpdateFromMouse(args.X, args.Y);
                args.Handled = true;
            }
        }

        public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
        {
            base.OnMouseUp(api, args);
            _isDragging = false;
        }

        private void UpdateFromMouse(double mouseX, double mouseY)
        {
            double localX = mouseX - Bounds.renderX;
            double localY = mouseY - Bounds.renderY;

            double s = GameMath.Clamp(localX / Bounds.OuterWidth, 0.0, 1.0);
            double v = GameMath.Clamp(1.0 - (localY / Bounds.OuterHeight), 0.0, 1.0);

            _saturation = s;
            _value = v;

            Recompose();
            _onColorChanged?.Invoke(s, v);
        }

        public override void Dispose()
        {
            base.Dispose();
            _texture?.Dispose();
        }
    }

    public class GuiElementHueSlider : GuiElement
    {
        private double _hue;
        private readonly Action<double> _onHueChanged;
        private LoadedTexture _texture;
        private bool _isDragging;

        public GuiElementHueSlider(ICoreClientAPI capi, ElementBounds bounds, double hue, Action<double> onHueChanged)
            : base(capi, bounds)
        {
            _hue = hue;
            _onHueChanged = onHueChanged;
            _texture = new LoadedTexture(capi);
        }

        public void UpdateHue(double hue)
        {
            if (Math.Abs(_hue - hue) > 0.001)
            {
                _hue = hue;
                Recompose();
            }
        }

        public void Recompose()
        {
            _texture?.Dispose();
            _texture = new LoadedTexture(api);

            int width = (int)Bounds.OuterWidth;
            int height = (int)Bounds.OuterHeight;
            if (width <= 0 || height <= 0) return;

            var surface = new ImageSurface(Format.Argb32, width, height);
            using (var ctx = new Context(surface))
            {
                ctx.SetSourceRGBA(0, 0, 0, 0);
                ctx.Paint();

                using (var hGrad = new LinearGradient(0, 0, width, 0))
                {
                    hGrad.AddColorStop(0.00, new Color(1.0, 0.0, 0.0, 1.0));
                    hGrad.AddColorStop(0.17, new Color(1.0, 1.0, 0.0, 1.0));
                    hGrad.AddColorStop(0.33, new Color(0.0, 1.0, 0.0, 1.0));
                    hGrad.AddColorStop(0.50, new Color(0.0, 1.0, 1.0, 1.0));
                    hGrad.AddColorStop(0.67, new Color(0.0, 0.0, 1.0, 1.0));
                    hGrad.AddColorStop(0.83, new Color(1.0, 0.0, 1.0, 1.0));
                    hGrad.AddColorStop(1.00, new Color(1.0, 0.0, 0.0, 1.0));

                    ctx.SetSource(hGrad);
                    ctx.Rectangle(0, 0, width, height);
                    ctx.Fill();
                }

                double selectionX = (_hue / 360.0) * width;

                ctx.SetSourceRGBA(0, 0, 0, 1.0);
                ctx.LineWidth = 2.0;
                ctx.Arc(selectionX, height / 2.0, 6.0, 0, 2 * Math.PI);
                ctx.Stroke();

                ctx.SetSourceRGBA(1.0, 1.0, 1.0, 1.0);
                ctx.LineWidth = 1.5;
                ctx.Arc(selectionX, height / 2.0, 5.0, 0, 2 * Math.PI);
                ctx.Stroke();

                ctx.SetSourceRGBA(0.3, 0.2, 0.1, 1.0);
                ctx.LineWidth = 2;
                ctx.Rectangle(1, 1, width - 2, height - 2);
                ctx.Stroke();
            }

            _texture.TextureId = api.Gui.LoadCairoTexture(surface, false);
            surface.Dispose();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (_texture != null && !_texture.Disposed)
            {
                if (_texture.TextureId == 0)
                {
                    Recompose();
                }
                api.Render.Render2DTexturePremultipliedAlpha(_texture.TextureId, (float)Bounds.renderX, (float)Bounds.renderY, (float)Bounds.OuterWidth, (float)Bounds.OuterHeight);
            }
        }

        public override void OnMouseDown(ICoreClientAPI api, MouseEvent args)
        {
            base.OnMouseDown(api, args);
            if (Bounds.PointInside(args.X, args.Y))
            {
                _isDragging = true;
                UpdateFromMouse(args.X);
                args.Handled = true;
            }
        }

        public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
        {
            base.OnMouseMove(api, args);
            if (_isDragging)
            {
                UpdateFromMouse(args.X);
                args.Handled = true;
            }
        }

        public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
        {
            base.OnMouseUp(api, args);
            _isDragging = false;
        }

        private void UpdateFromMouse(double mouseX)
        {
            double localX = mouseX - Bounds.renderX;
            double h = GameMath.Clamp((localX / Bounds.OuterWidth) * 360.0, 0.0, 360.0);

            _hue = h;

            Recompose();
            _onHueChanged?.Invoke(h);
        }

        public override void Dispose()
        {
            base.Dispose();
            _texture?.Dispose();
        }
    }

    public class GuiElementColorPreview : GuiElement
    {
        private string _originalColorHex;
        private string _currentColorHex;
        private LoadedTexture _texture;

        public GuiElementColorPreview(ICoreClientAPI capi, ElementBounds bounds, string originalColorHex, string currentColorHex) 
            : base(capi, bounds)
        {
            _originalColorHex = originalColorHex;
            _currentColorHex = currentColorHex;
            _texture = new LoadedTexture(capi);
        }

        public void UpdateCurrentColor(string currentColorHex)
        {
            if (_currentColorHex != currentColorHex)
            {
                _currentColorHex = currentColorHex;
                Recompose();
            }
        }

        public void Recompose()
        {
            _texture?.Dispose();
            _texture = new LoadedTexture(api);

            int width = (int)Bounds.OuterWidth;
            int height = (int)Bounds.OuterHeight;
            if (width <= 0 || height <= 0) return;

            var surface = new ImageSurface(Format.Argb32, width, height);
            using (var ctx = new Context(surface))
            {
                ctx.SetSourceRGBA(0, 0, 0, 0);
                ctx.Paint();

                double r, g, b;

                if (ColorPickerMath.HexToRgb(_originalColorHex, out r, out g, out b))
                {
                    ctx.SetSourceRGBA(r, g, b, 1.0);
                    ctx.Rectangle(0, 0, width / 2.0, height);
                    ctx.Fill();
                }

                if (ColorPickerMath.HexToRgb(_currentColorHex, out r, out g, out b))
                {
                    ctx.SetSourceRGBA(r, g, b, 1.0);
                    ctx.Rectangle(width / 2.0, 0, width / 2.0, height);
                    ctx.Fill();
                }

                ctx.SetSourceRGBA(0.3, 0.2, 0.1, 1.0);
                ctx.LineWidth = 2;
                ctx.Rectangle(1, 1, width - 2, height - 2);
                ctx.Stroke();
            }

            _texture.TextureId = api.Gui.LoadCairoTexture(surface, false);
            surface.Dispose();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (_texture != null && !_texture.Disposed)
            {
                if (_texture.TextureId == 0)
                {
                    Recompose();
                }
                api.Render.Render2DTexturePremultipliedAlpha(_texture.TextureId, (float)Bounds.renderX, (float)Bounds.renderY, (float)Bounds.OuterWidth, (float)Bounds.OuterHeight);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _texture?.Dispose();
        }
    }
}
