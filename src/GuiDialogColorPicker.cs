using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config;

namespace ResonanceTracker
{
    public class GuiDialogColorPicker : GuiDialog
    {
        private readonly string _initialColorHex;
        private readonly Action<string> _onSave;
        
        private double _hue;
        private double _saturation;
        private double _value;

        private GuiElementHsvBox _hsvBox;
        private GuiElementHueSlider _hueSlider;
        private GuiElementColorPreview _previewBox;

        private string _currentColorHex;

        public override string ToggleKeyCombinationCode => null;

        public GuiDialogColorPicker(ICoreClientAPI capi, string initialColorHex, Action<string> onSave) : base(capi)
        {
            _initialColorHex = initialColorHex;
            _currentColorHex = initialColorHex;
            _onSave = onSave;

            double r, g, b;
            ColorPickerMath.HexToRgb(initialColorHex, out r, out g, out b);
            ColorPickerMath.RgbToHsv(r, g, b, out _hue, out _saturation, out _value);
        }

        public override void OnGuiOpened()
        {
            SetupDialog();
        }

        private void SetupDialog()
        {
            ClearComposers();

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fixed(0, 0, 340, 305);

            ElementBounds hsvBounds = ElementBounds.Fixed(20, 40, 200, 200).WithParent(bgBounds);
            ElementBounds hueBounds = ElementBounds.Fixed(20, 255, 200, 20).WithParent(bgBounds);

            ElementBounds previewBounds = ElementBounds.Fixed(240, 40, 80, 50).WithParent(bgBounds);
            
            // Hex text input located above the Save button
            ElementBounds hexInputBounds = ElementBounds.Fixed(240, 150, 80, 25).WithParent(bgBounds);
            
            ElementBounds saveBtnBounds = ElementBounds.Fixed(240, 185, 80, 30).WithParent(bgBounds);
            ElementBounds cancelBtnBounds = ElementBounds.Fixed(240, 225, 80, 30).WithParent(bgBounds);

            _hsvBox = new GuiElementHsvBox(capi, hsvBounds, _hue, _saturation, _value, OnHsvChanged);
            _hueSlider = new GuiElementHueSlider(capi, hueBounds, _hue, OnHueChanged);
            _previewBox = new GuiElementColorPreview(capi, previewBounds, _initialColorHex, _currentColorHex);

            var composer = capi.Gui.CreateCompo("colorpicker", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(Lang.Get("resonancetracker:color-picker"), () => TryClose())
                .BeginChildElements(bgBounds)
                
                .AddInteractiveElement(_hsvBox, "hsvBox")
                .AddInteractiveElement(_hueSlider, "hueSlider")
                .AddInteractiveElement(_previewBox, "previewBox")
                
                .AddTextInput(hexInputBounds, OnHexInputChanged, CairoFont.WhiteSmallText(), "hexInput")
 
                .AddButton(Lang.Get("resonancetracker:save"), OnSavePressed, saveBtnBounds, EnumButtonStyle.Normal, "saveBtn")
                .AddButton(Lang.Get("resonancetracker:cancel"), OnCancelPressed, cancelBtnBounds, EnumButtonStyle.Normal, "cancelBtn")
                
                .EndChildElements();

            SingleComposer = composer.Compose();
            SingleComposer.GetTextInput("hexInput").SetValue(_currentColorHex);
        }

        private void OnHsvChanged(double saturation, double value)
        {
            _saturation = saturation;
            _value = value;
            UpdateColorFromHsv();
        }

        private void OnHueChanged(double hue)
        {
            _hue = hue;
            _hsvBox.UpdateHue(hue);
            UpdateColorFromHsv();
        }

        private void UpdateColorFromHsv()
        {
            double r, g, b;
            ColorPickerMath.HsvToRgb(_hue, _saturation, _value, out r, out g, out b);
            _currentColorHex = ColorPickerMath.RgbToHex(r, g, b);

            _previewBox.UpdateCurrentColor(_currentColorHex);

            var input = SingleComposer.GetTextInput("hexInput");
            if (input != null && input.GetText() != _currentColorHex)
            {
                input.SetValue(_currentColorHex);
            }
        }

        private void OnHexInputChanged(string text)
        {
            if (IsValidHex(text))
            {
                _currentColorHex = text.ToUpper();
                double r, g, b;
                ColorPickerMath.HexToRgb(_currentColorHex, out r, out g, out b);
                ColorPickerMath.RgbToHsv(r, g, b, out _hue, out _saturation, out _value);

                _hsvBox.UpdateHue(_hue);
                _hsvBox.UpdateSaturationValue(_saturation, _value);
                _hueSlider.UpdateHue(_hue);

                _previewBox.UpdateCurrentColor(_currentColorHex);
            }
        }

        private bool IsValidHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return false;
            if (hex.Length != 7 || hex[0] != '#') return false;
            
            for (int i = 1; i < 7; i++)
            {
                char c = hex[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                {
                    return false;
                }
            }
            return true;
        }

        private bool OnSavePressed()
        {
            _onSave?.Invoke(_currentColorHex);
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
