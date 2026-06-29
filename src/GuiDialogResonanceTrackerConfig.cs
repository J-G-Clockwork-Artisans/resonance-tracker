using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config;
using Cairo;

namespace ResonanceTracker
{
    public class GuiElementPlayerPreview : GuiElement
    {
        private readonly Func<double[]> _getBorderColor;
        private readonly Func<double[]> _getFillColor;
        private LoadedTexture _texture;

        public GuiElementPlayerPreview(ICoreClientAPI capi, ElementBounds bounds, Func<double[]> getBorderColor, Func<double[]> getFillColor) 
            : base(capi, bounds)
        {
            _getBorderColor = getBorderColor;
            _getFillColor = getFillColor;
            _texture = new LoadedTexture(capi);
            Recompose();
        }

        public void Recompose()
        {
            _texture?.Dispose();
            _texture = new LoadedTexture(api);

            int size = 48; // Size of the preview icon
            var surface = new ImageSurface(Format.Argb32, size, size);
            using (var ctx = new Context(surface))
            {
                ctx.SetSourceRGBA(0, 0, 0, 0);
                ctx.Paint();
                
                double[] borderColor = _getBorderColor();
                double[] fillColor = _getFillColor();
                
                api.Gui.Icons.DrawMapPlayer(ctx, 0, 0, size, size, borderColor, fillColor);
            }
            _texture.TextureId = api.Gui.LoadCairoTexture(surface, false);
            surface.Dispose();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (_texture != null && !_texture.Disposed)
            {
                api.Render.Render2DTexturePremultipliedAlpha(_texture.TextureId, (float)Bounds.renderX, (float)Bounds.renderY, (float)Bounds.OuterWidth, (float)Bounds.OuterHeight);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _texture?.Dispose();
        }
    }

    public class GuiDialogResonanceTrackerConfig : GuiDialog
    {
        private readonly ResonanceTrackerConfig _config;
        private readonly Action _onConfigChanged;
        private readonly IClientNetworkChannel _channel;

        private readonly bool _isAdmin;
        private int _updateIntervalMs;
        private bool _hideCrouching;
        
        private GuiElementPlayerPreview ownPreview;
        private GuiElementPlayerPreview otherPreview;

        private GuiElementColorSquare ownFillSquare;
        private GuiElementColorSquare ownBorderSquare;
        private GuiElementColorSquare otherFillSquare;
        private GuiElementColorSquare otherBorderSquare;

        public override string ToggleKeyCombinationCode => null; // Opened via command

        public GuiDialogResonanceTrackerConfig(
            ICoreClientAPI capi, 
            ResonanceTrackerConfig config, 
            Action onConfigChanged, 
            IClientNetworkChannel channel,
            bool isAdmin = false,
            int updateIntervalMs = 1000,
            bool hideCrouching = false
        ) : base(capi)
        {
            _config = config;
            _onConfigChanged = onConfigChanged;
            _channel = channel;
            _isAdmin = isAdmin;
            _updateIntervalMs = updateIntervalMs;
            _hideCrouching = hideCrouching;
        }

        public override void OnGuiOpened()
        {
            SetupDialog();
        }

        private void SetupDialog()
        {
            ClearComposers();

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            int height = _isAdmin ? 420 : 350;
            ElementBounds bgBounds = ElementBounds.Fixed(0, 0, 560, height);

            ElementBounds saveBtnBounds = ElementBounds.Fixed(110, height - 65, 150, 35).WithParent(bgBounds);
            ElementBounds cancelBtnBounds = ElementBounds.Fixed(300, height - 65, 150, 35).WithParent(bgBounds);

            ownFillSquare = new GuiElementColorSquare(capi, ElementBounds.Fixed(115, 130, 25, 25).WithParent(bgBounds), _config.OwnFillColorHex);
            ownBorderSquare = new GuiElementColorSquare(capi, ElementBounds.Fixed(115, 170, 25, 25).WithParent(bgBounds), _config.OwnBorderColorHex);

            var composer = capi.Gui.CreateCompo("resonancetrackerconfig", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(Lang.Get("resonancetracker:gui-title"), () => TryClose())
                .BeginChildElements(bgBounds)
                
                // Section 1: Your Marker
                .AddStaticText(Lang.Get("resonancetracker:your-marker"), CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold), ElementBounds.Fixed(30, 40, 220, 20).WithParent(bgBounds))
                .AddInteractiveElement(ownPreview = new GuiElementPlayerPreview(capi, ElementBounds.Fixed(110, 70, 48, 48).WithParent(bgBounds), () => HexToRGBA(_config.OwnBorderColorHex), () => HexToRGBA(_config.OwnFillColorHex)), "ownPreview")
                
                .AddStaticText(Lang.Get("resonancetracker:fill-color"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(30, 135, 80, 25).WithParent(bgBounds))
                .AddInteractiveElement(ownFillSquare, "ownFillSquare")
                .AddButton(Lang.Get("resonancetracker:edit"), OnOwnFillEdit, ElementBounds.Fixed(150, 130, 80, 25).WithParent(bgBounds), EnumButtonStyle.Normal, "ownFillBtn")
                
                .AddStaticText(Lang.Get("resonancetracker:border-color"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(30, 175, 80, 25).WithParent(bgBounds))
                .AddInteractiveElement(ownBorderSquare, "ownBorderSquare")
                .AddButton(Lang.Get("resonancetracker:edit"), OnOwnBorderEdit, ElementBounds.Fixed(150, 170, 80, 25).WithParent(bgBounds), EnumButtonStyle.Normal, "ownBorderBtn")

                // Section 2: Others' Markers
                .AddStaticText(Lang.Get("resonancetracker:others-markers"), CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold), ElementBounds.Fixed(310, 40, 220, 20).WithParent(bgBounds));

            if (_config.UsePlayerCustomColors)
            {
                composer.AddStaticText(Lang.Get("resonancetracker:helper-text"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(310, 80, 220, 100).WithParent(bgBounds));
            }
            else
            {
                otherFillSquare = new GuiElementColorSquare(capi, ElementBounds.Fixed(395, 130, 25, 25).WithParent(bgBounds), _config.OtherFillColorHex);
                otherBorderSquare = new GuiElementColorSquare(capi, ElementBounds.Fixed(395, 170, 25, 25).WithParent(bgBounds), _config.OtherBorderColorHex);

                composer
                    .AddInteractiveElement(otherPreview = new GuiElementPlayerPreview(capi, ElementBounds.Fixed(390, 70, 48, 48).WithParent(bgBounds), () => HexToRGBA(_config.OtherBorderColorHex), () => HexToRGBA(_config.OtherFillColorHex)), "otherPreview")
                    
                    .AddStaticText(Lang.Get("resonancetracker:fill-color"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(310, 135, 80, 25).WithParent(bgBounds))
                    .AddInteractiveElement(otherFillSquare, "otherFillSquare")
                    .AddButton(Lang.Get("resonancetracker:edit"), OnOtherFillEdit, ElementBounds.Fixed(430, 130, 80, 25).WithParent(bgBounds), EnumButtonStyle.Normal, "otherFillBtn")
                    
                    .AddStaticText(Lang.Get("resonancetracker:border-color"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(310, 175, 80, 25).WithParent(bgBounds))
                    .AddInteractiveElement(otherBorderSquare, "otherBorderSquare")
                    .AddButton(Lang.Get("resonancetracker:edit"), OnOtherBorderEdit, ElementBounds.Fixed(430, 170, 80, 25).WithParent(bgBounds), EnumButtonStyle.Normal, "otherBorderBtn");
            }

            // Row 5: Use player custom colors toggle
            composer
                .AddStaticText(Lang.Get("resonancetracker:use-player-colors"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(310, 210, 120, 25).WithParent(bgBounds))
                .AddButton(Lang.Get(_config.UsePlayerCustomColors ? "resonancetracker:yes" : "resonancetracker:no"), OnToggleUsePlayerColors, ElementBounds.Fixed(435, 205, 75, 25).WithParent(bgBounds), EnumButtonStyle.Normal, "usePlayerColorsBtn");

            // Server settings added if player is an administrator
            if (_isAdmin)
            {
                composer
                    .AddStaticText(Lang.Get("resonancetracker:server-settings-admin"), CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold), ElementBounds.Fixed(30, 250, 500, 20).WithParent(bgBounds))
                    
                    .AddStaticText(Lang.Get("resonancetracker:update-rate"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(30, 285, 120, 25).WithParent(bgBounds))
                    .AddTextInput(ElementBounds.Fixed(160, 280, 80, 25).WithParent(bgBounds), null, CairoFont.WhiteSmallText(), "updateIntervalInput")
                    
                    .AddStaticText(Lang.Get("resonancetracker:hide-sneaking"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(310, 285, 120, 25).WithParent(bgBounds))
                    .AddButton(Lang.Get(_hideCrouching ? "resonancetracker:yes" : "resonancetracker:no"), OnToggleHideCrouching, ElementBounds.Fixed(435, 280, 75, 25).WithParent(bgBounds), EnumButtonStyle.Normal, "hideCrouchingBtn");
            }

            composer
                .AddButton(Lang.Get("resonancetracker:save-close"), OnSavePressed, saveBtnBounds)
                .AddButton(Lang.Get("resonancetracker:cancel"), OnCancelPressed, cancelBtnBounds)
                .EndChildElements();

            SingleComposer = composer.Compose();

            if (_isAdmin)
            {
                SingleComposer.GetTextInput("updateIntervalInput").SetValue(_updateIntervalMs.ToString());
            }
        }

        private bool OnToggleUsePlayerColors()
        {
            if (_isAdmin)
            {
                var input = SingleComposer.GetTextInput("updateIntervalInput");
                if (input != null && int.TryParse(input.GetText(), out int val))
                {
                    _updateIntervalMs = val;
                }
            }
            _config.UsePlayerCustomColors = !_config.UsePlayerCustomColors;
            SetupDialog();
            return true;
        }

        private bool OnToggleHideCrouching()
        {
            if (_isAdmin)
            {
                var input = SingleComposer.GetTextInput("updateIntervalInput");
                if (input != null && int.TryParse(input.GetText(), out int val))
                {
                    _updateIntervalMs = val;
                }
            }
            _hideCrouching = !_hideCrouching;
            SetupDialog();
            return true;
        }

        private double[] HexToRGBA(string hex)
        {
            try
            {
                if (string.IsNullOrEmpty(hex)) return new double[] { 1, 1, 1, 1 };
                if (hex.StartsWith("#")) hex = hex.Substring(1);
                int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                int b = Convert.ToInt32(hex.Substring(4, 2), 16);
                return new double[] { r / 255.0, g / 255.0, b / 255.0, 1.0 };
            }
            catch
            {
                return new double[] { 1, 1, 1, 1 };
            }
        }

        private bool OnOwnFillEdit()
        {
            var picker = new GuiDialogColorPicker(capi, _config.OwnFillColorHex, (newColor) =>
            {
                _config.OwnFillColorHex = newColor;
                ownPreview?.Recompose();
                ownFillSquare?.SetColor(newColor);
            });
            picker.TryOpen();
            return true;
        }

        private bool OnOwnBorderEdit()
        {
            var picker = new GuiDialogColorPicker(capi, _config.OwnBorderColorHex, (newColor) =>
            {
                _config.OwnBorderColorHex = newColor;
                ownPreview?.Recompose();
                ownBorderSquare?.SetColor(newColor);
            });
            picker.TryOpen();
            return true;
        }

        private bool OnOtherFillEdit()
        {
            var picker = new GuiDialogColorPicker(capi, _config.OtherFillColorHex, (newColor) =>
            {
                _config.OtherFillColorHex = newColor;
                otherPreview?.Recompose();
                otherFillSquare?.SetColor(newColor);
            });
            picker.TryOpen();
            return true;
        }

        private bool OnOtherBorderEdit()
        {
            var picker = new GuiDialogColorPicker(capi, _config.OtherBorderColorHex, (newColor) =>
            {
                _config.OtherBorderColorHex = newColor;
                otherPreview?.Recompose();
                otherBorderSquare?.SetColor(newColor);
            });
            picker.TryOpen();
            return true;
        }

        private bool OnSavePressed()
        {
            if (_isAdmin)
            {
                var input = SingleComposer.GetTextInput("updateIntervalInput");
                if (input != null && int.TryParse(input.GetText(), out int val))
                {
                    _updateIntervalMs = val;
                }

                _channel.SendPacket(new SaveAdminConfigPacket 
                { 
                    UpdateIntervalMs = _updateIntervalMs,
                    HideCrouchingPlayers = _hideCrouching
                });
            }

            // Sync custom colors to the server
            _channel.SendPacket(new PlayerColorUpdatePacket
            {
                FillColorHex = _config.OwnFillColorHex,
                BorderColorHex = _config.OwnBorderColorHex
            });

            _onConfigChanged?.Invoke();
            TryClose();
            return true;
        }

        private bool OnCancelPressed()
        {
            TryClose();
            return true;
        }
    }
}
