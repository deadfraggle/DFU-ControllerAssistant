using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility.AssetInjection;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class TravelMapAssist
    {
        // Overworld routed nodes
        private const int AlikrDesert = 0;
        private const int DragontailMountains = 1;
        private const int Dwynnen = 5;
        private const int IsleOfBalfiera = 9;
        private const int Dakfron = 11;
        private const int WrothgarianMountains = 16;
        private const int Daggerfall = 17;
        private const int Glenpoint = 18;
        private const int Betony = 19;
        private const int Sentinel = 20;
        private const int Anticlere = 21;
        private const int Lainlyn = 22;
        private const int Wayrest = 23;
        private const int OrsiniumArea = 26;
        private const int Northmoor = 32;
        private const int Menevia = 33;
        private const int Alcaire = 34;
        private const int Koegria = 35;
        private const int Bhoriane = 36;
        private const int Kambria = 37;
        private const int Phrygias = 38;
        private const int Urvaius = 39;
        private const int Ykalon = 40;
        private const int Daenia = 41;
        private const int Shalgora = 42;
        private const int AbibonGora = 43;
        private const int Kairou = 44;
        private const int Pothago = 45;
        private const int Myrkwasa = 46;
        private const int Ayasofya = 47;
        private const int Tigonus = 48;
        private const int Kozanset = 49;
        private const int Satakalaam = 50;
        private const int Totambu = 51;
        private const int Mournoth = 52;
        private const int Ephesus = 53;
        private const int Santaki = 54;
        private const int Antiphyllos = 55;
        private const int Bergama = 56;
        private const int Gavaudon = 57;
        private const int Tulune = 58;
        private const int GlenumbraMoors = 59;
        private const int IlessanHills = 60;
        private const int Cybiades = 61;

        // Bottom buttons as routed targets from overworld
        private const int DungeonsButton = 1000;
        private const int HomesButton = 1001;
        private const int TemplesButton = 1002;
        private const int TownsButton = 1003;
        private const int ExitButton = 1004;

        private struct SelectorButtonInfo
        {
            public int id;
            public Rect rect;
            public int N, NE, E, SE, S, SW, W, NW;
        }

        private DefaultSelectorBoxHost overworldButtonSelector;
        private SelectorButtonInfo[] overworldButtons;
        private bool overworldButtonsBuilt = false;
        private bool inOverworldButtonMode = false;
        private int overworldButtonSelected = DungeonsButton;

        private const string OverworldImgName = "TRAV0I00.IMG";

        partial void OnOpenedOverworld(DaggerfallTravelMapWindow menuWindow, ControllerManager cm)
        {
            controllerRegion = GetInitialControllerRegion(menuWindow);
            if (controllerRegion >= 0)
                HighlightControllerRegion(menuWindow, controllerRegion);

            EnsureOverworldButtonsBuilt();
            inOverworldButtonMode = false;
            overworldButtonSelected = DungeonsButton;
        }

        partial void TickOverworld(DaggerfallTravelMapWindow menuWindow, ControllerManager cm)
        {
            int selectedRegion = fiSelectedRegion != null ? (int)fiSelectedRegion.GetValue(menuWindow) : -1;
            bool overworldMode = selectedRegion == -1;
            if (!overworldMode)
                return;

            if (!inOverworldButtonMode && controllerRegion >= 0)
                KeepIdentifyAlive(menuWindow);

            ControllerManager.StickDir8 dir =
                cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                ? cm.RStickDir8Pressed
                : cm.RStickDir8HeldSlow;

            if (inOverworldButtonMode)
            {
                RefreshOverworldButtonSelector(menuWindow);

                if (dir != ControllerManager.StickDir8.None)
                {
                    int next = GetButtonDirectionalTarget(overworldButtonSelected, dir);
                    if (next >= 1000)
                    {
                        if (next != overworldButtonSelected)
                        {
                            overworldButtonSelected = next;
                            RefreshOverworldButtonSelector(menuWindow);
                        }
                        return;
                    }
                    else if (next >= 0)
                    {
                        ExitOverworldButtonMode(menuWindow);
                        controllerRegion = next;
                        HighlightControllerRegion(menuWindow, controllerRegion);
                        return;
                    }
                }

                if (cm.Action1Released)
                {
                    ActivateOverworldButton(menuWindow);
                    RefreshOverworldButtonSelector(menuWindow);
                    return;
                }

                return;
            }

            if (dir != ControllerManager.StickDir8.None)
            {
                TryMoveRegionByRoute(menuWindow, dir);
                return;
            }

            if (cm.Action1Released)
            {
                OpenCurrentControllerRegion(menuWindow);
                return;
            }
        }

        private void EnsureOverworldButtonsBuilt()
        {
            if (overworldButtonsBuilt)
                return;

            overworldButtons = new SelectorButtonInfo[]
            {
                new SelectorButtonInfo
                {
                    id = DungeonsButton,
                    rect = new Rect(49.6f, 174.5f, 99.8f, 11.5f),
                    N = AlikrDesert, NE = AlikrDesert, E = HomesButton, SE = TownsButton,
                    S = TemplesButton, SW = DungeonsButton, W = -1, NW = AbibonGora
                },
                new SelectorButtonInfo
                {
                    id = TemplesButton,
                    rect = new Rect(49.6f, 185.6f, 99.8f, 11.5f),
                    N = DungeonsButton, NE = HomesButton, E = TownsButton, SE = -1,
                    S = -1, SW = -1, W = -1, NW = -1
                },
                new SelectorButtonInfo
                {
                    id = HomesButton,
                    rect = new Rect(148.5f, 174.5f, 80.8f, 11.5f),
                    N = Dakfron, NE = Dakfron, E = ExitButton, SE = ExitButton,
                    S = TownsButton, SW = TemplesButton, W = DungeonsButton, NW = Dakfron
                },
                new SelectorButtonInfo
                {
                    id = TownsButton,
                    rect = new Rect(148.5f, 185.6f, 80.8f, 11.5f),
                    N = HomesButton, NE = ExitButton, E = ExitButton, SE = -1,
                    S = -1, SW = -1, W = TemplesButton, NW = DungeonsButton
                },
                new SelectorButtonInfo
                {
                    id = ExitButton,
                    rect = new Rect(277.8f, 174.0f, 39.7f, 23.5f),
                    N = DragontailMountains, NE = DragontailMountains, E = -1, SE = -1,
                    S = -1, SW = -1, W = HomesButton, NW = DragontailMountains
                },
            };

            overworldButtonsBuilt = true;
        }

        private void ResetOverworldButtonState()
        {
            inOverworldButtonMode = false;
            overworldButtonSelected = DungeonsButton;
            overworldButtonsBuilt = false;

            if (overworldButtonSelector != null)
            {
                overworldButtonSelector.Destroy();
                overworldButtonSelector = null;
            }
        }

        private SelectorButtonInfo GetOverworldButtonInfo(int id)
        {
            if (overworldButtons == null)
                return new SelectorButtonInfo { id = -1 };

            for (int i = 0; i < overworldButtons.Length; i++)
            {
                if (overworldButtons[i].id == id)
                    return overworldButtons[i];
            }

            return new SelectorButtonInfo { id = -1 };
        }

        private void RefreshOverworldButtonSelector(DaggerfallTravelMapWindow menuWindow)
        {
            EnsureOverworldButtonsBuilt();

            if (!inOverworldButtonMode)
                return;

            Panel currentPanel = fiPanelRenderWindow != null ? fiPanelRenderWindow.GetValue(menuWindow) as Panel : null;
            if (currentPanel == null)
                return;

            SelectorButtonInfo info = GetOverworldButtonInfo(overworldButtonSelected);
            if (info.id < 0)
                return;

            if (overworldButtonSelector == null)
                overworldButtonSelector = new DefaultSelectorBoxHost();

            overworldButtonSelector.ShowAtNativeRect(
                currentPanel,
                info.rect,
                new Color(0.1f, 1f, 1f, 1f));
        }

        private void HideOverworldButtonSelector()
        {
            if (overworldButtonSelector != null)
                overworldButtonSelector.Destroy();
            overworldButtonSelector = null;
        }

        private int GetButtonDirectionalTarget(int current, ControllerManager.StickDir8 dir)
        {
            SelectorButtonInfo info = GetOverworldButtonInfo(current);
            if (info.id < 0)
                return -1;

            return TargetFromRect(dir, info.N, info.NE, info.E, info.SE, info.S, info.SW, info.W, info.NW);
        }

        private void EnterOverworldButtonMode(DaggerfallTravelMapWindow menuWindow, int buttonId)
        {
            inOverworldButtonMode = true;
            overworldButtonSelected = buttonId;
            HideOverworldRegionSelector(menuWindow);
            RefreshOverworldButtonSelector(menuWindow);
        }

        private void ExitOverworldButtonMode(DaggerfallTravelMapWindow menuWindow)
        {
            inOverworldButtonMode = false;
            HideOverworldButtonSelector();

            if (controllerRegion >= 0)
                HighlightControllerRegion(menuWindow, controllerRegion);
        }

        private void ActivateOverworldButton(DaggerfallTravelMapWindow menuWindow)
        {
            BaseScreenComponent target = null;

            switch (overworldButtonSelected)
            {
                case DungeonsButton:
                    target = fiDungeonsFilterButton != null ? fiDungeonsFilterButton.GetValue(menuWindow) as BaseScreenComponent : null;
                    break;
                case HomesButton:
                    target = fiHomesFilterButton != null ? fiHomesFilterButton.GetValue(menuWindow) as BaseScreenComponent : null;
                    break;
                case TemplesButton:
                    target = fiTemplesFilterButton != null ? fiTemplesFilterButton.GetValue(menuWindow) as BaseScreenComponent : null;
                    break;
                case TownsButton:
                    target = fiTownsFilterButton != null ? fiTownsFilterButton.GetValue(menuWindow) as BaseScreenComponent : null;
                    break;
                case ExitButton:
                    target = fiExitButton != null ? fiExitButton.GetValue(menuWindow) as BaseScreenComponent : null;
                    break;
            }

            Button button = target as Button;
            if (button != null)
                button.TriggerMouseClick();
        }

        private void HideOverworldRegionSelector(DaggerfallTravelMapWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            if (fiIdentifying != null)
                fiIdentifying.SetValue(menuWindow, false);

            if (fiIdentifyChanges != null)
                fiIdentifyChanges.SetValue(menuWindow, 0f);

            Panel identifyOverlayPanel = fiIdentifyOverlayPanel != null ? fiIdentifyOverlayPanel.GetValue(menuWindow) as Panel : null;
            if (identifyOverlayPanel != null)
                identifyOverlayPanel.BackgroundTexture = null;
        }

        private int GetInitialControllerRegion(DaggerfallTravelMapWindow menuWindow)
        {
            if (menuWindow == null)
                return -1;

            int selectedRegion = fiSelectedRegion != null ? (int)fiSelectedRegion.GetValue(menuWindow) : -1;
            if (selectedRegion >= 0)
                return selectedRegion;

            int mouseOverRegion = fiMouseOverRegion != null ? (int)fiMouseOverRegion.GetValue(menuWindow) : -1;
            if (mouseOverRegion >= 0)
                return mouseOverRegion;

            int playerRegion = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetPoliticIndex(
                TravelTimeCalculator.GetPlayerTravelPosition().X,
                TravelTimeCalculator.GetPlayerTravelPosition().Y) - 128;

            if (playerRegion >= 0 && playerRegion < DaggerfallUnity.Instance.ContentReader.MapFileReader.RegionCount)
                return playerRegion;

            return 0;
        }

        
        private void OpenCurrentControllerRegion(DaggerfallTravelMapWindow menuWindow)
        {
            if (menuWindow == null || controllerRegion < 0)
                return;

            if (miOpenRegionPanel != null)
                miOpenRegionPanel.Invoke(menuWindow, new object[] { controllerRegion });
        }

        private void HighlightControllerRegion(DaggerfallTravelMapWindow menuWindow, int region)
        {
            if (menuWindow == null || region < 0)
                return;

            if (fiMouseOverRegion != null)
                fiMouseOverRegion.SetValue(menuWindow, region);

            UpdateIdentifyTextureForRegion(menuWindow, region);

            if (miStartIdentify != null)
                miStartIdentify.Invoke(menuWindow, null);
        }

        private void UpdateIdentifyTextureForRegion(DaggerfallTravelMapWindow menuWindow, int region)
        {
            if (menuWindow == null || region < 0)
                return;

            object pickerBitmapObj = fiRegionPickerBitmap != null ? fiRegionPickerBitmap.GetValue(menuWindow) : null;
            Color32[] identifyPixelBuffer = fiIdentifyPixelBuffer != null ? fiIdentifyPixelBuffer.GetValue(menuWindow) as Color32[] : null;
            Texture2D identifyTexture = fiIdentifyTexture != null ? fiIdentifyTexture.GetValue(menuWindow) as Texture2D : null;
            Panel identifyOverlayPanel = fiIdentifyOverlayPanel != null ? fiIdentifyOverlayPanel.GetValue(menuWindow) as Panel : null;

            if (pickerBitmapObj == null || identifyPixelBuffer == null || identifyTexture == null || identifyOverlayPanel == null)
                return;

            // Match vanilla player-region behavior:
            // first try an imported overlay texture named TRAV0I00.IMG-RegionName,
            // then fall back to bitmap fill.
            Dictionary<int, Texture2D> importedOverlays =
                fiImportedOverlays != null ? fiImportedOverlays.GetValue(menuWindow) as Dictionary<int, Texture2D> : null;

            Texture2D customRegionOverlayTexture = null;

            if (importedOverlays != null && importedOverlays.TryGetValue(region, out customRegionOverlayTexture))
            {
                identifyOverlayPanel.BackgroundTexture = customRegionOverlayTexture;
                return;
            }

            if (TextureReplacement.TryImportImage(
                string.Format("{0}-{1}", OverworldImgName, GetRegionNameForMapReplacement(region)),
                false,
                out customRegionOverlayTexture))
            {
                identifyOverlayPanel.BackgroundTexture = customRegionOverlayTexture;

                if (importedOverlays != null)
                    importedOverlays[region] = customRegionOverlayTexture;

                return;
            }

            System.Type pickerType = pickerBitmapObj.GetType();

            int width = 0;
            int height = 0;
            byte[] data = null;

            PropertyInfo piWidth = pickerType.GetProperty("Width");
            PropertyInfo piHeight = pickerType.GetProperty("Height");
            PropertyInfo piData = pickerType.GetProperty("Data");

            if (piWidth != null)
                width = (int)piWidth.GetValue(pickerBitmapObj, null);
            else
            {
                FieldInfo fiWidth = pickerType.GetField("Width");
                if (fiWidth != null)
                    width = (int)fiWidth.GetValue(pickerBitmapObj);
            }

            if (piHeight != null)
                height = (int)piHeight.GetValue(pickerBitmapObj, null);
            else
            {
                FieldInfo fiHeight = pickerType.GetField("Height");
                if (fiHeight != null)
                    height = (int)fiHeight.GetValue(pickerBitmapObj);
            }

            if (piData != null)
                data = piData.GetValue(pickerBitmapObj, null) as byte[];
            else
            {
                FieldInfo fiData = pickerType.GetField("Data");
                if (fiData != null)
                    data = fiData.GetValue(pickerBitmapObj) as byte[];
            }

            if (width <= 0 || height <= 0 || data == null)
            {
                Debug.Log("[TravelMapAssist] Failed to read DFBitmap Width/Height/Data.");
                return;
            }

            object flashColorObj = fiIdentifyFlashColor != null ? fiIdentifyFlashColor.GetValue(menuWindow) : null;
            Color32 identifyFlashColor = flashColorObj is Color32 ? (Color32)flashColorObj : new Color32(255, 0, 0, 255);

            System.Array.Clear(identifyPixelBuffer, 0, identifyPixelBuffer.Length);

            int pickerOverlayPanelHeightDifference = height - regionTextureHeight - regionPanelOffset + 1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int srcOffset = y * width + x;
                    int dstOffset = ((height - y - pickerOverlayPanelHeightDifference) * width) + x;

                    if (dstOffset < 0 || dstOffset >= identifyPixelBuffer.Length)
                        continue;

                    int sampleRegion = data[srcOffset] - 128;
                    if (sampleRegion == region)
                        identifyPixelBuffer[dstOffset] = identifyFlashColor;
                }
            }

            identifyTexture.SetPixels32(identifyPixelBuffer);
            identifyTexture.Apply();
            identifyOverlayPanel.BackgroundTexture = identifyTexture;
        }

        private void KeepIdentifyAlive(DaggerfallTravelMapWindow menuWindow)
        {
            if (fiIdentifying != null)
                fiIdentifying.SetValue(menuWindow, true);

            if (fiIdentifyChanges != null)
                fiIdentifyChanges.SetValue(menuWindow, 0f);
        }

        private int TargetFromRect(
            ControllerManager.StickDir8 dir,
            int n, int ne, int e, int se, int s, int sw, int w, int nw)
        {
            switch (dir)
            {
                case ControllerManager.StickDir8.N: return n;
                case ControllerManager.StickDir8.NE: return ne;
                case ControllerManager.StickDir8.E: return e;
                case ControllerManager.StickDir8.SE: return se;
                case ControllerManager.StickDir8.S: return s;
                case ControllerManager.StickDir8.SW: return sw;
                case ControllerManager.StickDir8.W: return w;
                case ControllerManager.StickDir8.NW: return nw;
                default: return -1;
            }
        }
        private void TryMoveRegionByRoute(DaggerfallTravelMapWindow menuWindow, ControllerManager.StickDir8 dir)
        {
            //if (controllerRegion < 0)
            //    controllerRegion = GetInitialControllerRegion(menuWindow);

            int next = GetDirectionalTarget(controllerRegion, dir);
            if (next < 0)
                return;

            if (next >= 1000)
            {
                EnterOverworldButtonMode(menuWindow, next);
                return;
            }

            if (next == controllerRegion)
                return;

            controllerRegion = next;
            HighlightControllerRegion(menuWindow, controllerRegion);
        }

        private string GetRegionNameForMapReplacement(int region)
        {
            return DaggerfallUnity.Instance.ContentReader.MapFileReader.GetRegionName(region);
        }

        private int GetDirectionalTarget(int current, ControllerManager.StickDir8 dir)
        {
            switch (current)
            {
                case AlikrDesert:
                    return TargetFromRect(dir,
                        Sentinel,              // N
                        Bergama,               // NE
                        Bergama,               // E
                        Dakfron,               // SE
                        DungeonsButton,        // S
                        DungeonsButton,        // SW
                        AbibonGora,            // W
                        Myrkwasa);             // NW

                case DragontailMountains:
                    return TargetFromRect(dir, Totambu, Mournoth, Ephesus, ExitButton, ExitButton, Dakfron, Santaki, Tigonus);

                case Dwynnen:
                    return TargetFromRect(dir, Phrygias, WrothgarianMountains, Kambria, Bhoriane, Tigonus, Anticlere, Urvaius, Phrygias);

                case IsleOfBalfiera:
                    return TargetFromRect(dir, Alcaire, Menevia, Wayrest, Satakalaam, Lainlyn, DragontailMountains, Anticlere, Bhoriane);

                case Dakfron:
                    return TargetFromRect(dir, Santaki, Santaki, DragontailMountains, HomesButton, HomesButton, DungeonsButton, Bergama, AlikrDesert);

                case WrothgarianMountains:
                    return TargetFromRect(dir, -1, -1, -1, -1, OrsiniumArea, Alcaire, Koegria, -1);

                case Daggerfall:
                    return TargetFromRect(dir, Glenpoint, IlessanHills, Shalgora, Cybiades, Pothago, Betony, -1, Tulune);

                case Glenpoint:
                    return TargetFromRect(dir, Northmoor, IlessanHills, IlessanHills, Daggerfall, Daggerfall, Daggerfall, Tulune, GlenumbraMoors);

                case Betony:
                    return TargetFromRect(dir, Tulune, Daggerfall, Cybiades, Pothago, AbibonGora, -1, -1, Tulune);

                case Sentinel:
                    return TargetFromRect(dir, Cybiades, Cybiades, Ayasofya, Antiphyllos, AlikrDesert, Myrkwasa, Myrkwasa, Betony);

                case Anticlere:
                    return TargetFromRect(dir, Daenia, Urvaius, Dwynnen, Tigonus, Cybiades, Pothago, Shalgora, Daenia);

                case Lainlyn:
                    return TargetFromRect(dir, IsleOfBalfiera, Menevia, Satakalaam, Totambu, Kozanset, DragontailMountains, DragontailMountains, Dwynnen);

                case Wayrest:
                    return TargetFromRect(dir, OrsiniumArea, WrothgarianMountains, Gavaudon, Mournoth, Mournoth, Satakalaam, IsleOfBalfiera, Menevia);

                case OrsiniumArea:
                    return TargetFromRect(dir, WrothgarianMountains, WrothgarianMountains, WrothgarianMountains, Wayrest, Wayrest, Menevia, Menevia, WrothgarianMountains);

                case Northmoor:
                    return TargetFromRect(dir, -1, -1, Ykalon, IlessanHills, Glenpoint, GlenumbraMoors, GlenumbraMoors, -1);

                case Menevia:
                    return TargetFromRect(dir, WrothgarianMountains, OrsiniumArea, Wayrest, Wayrest, Satakalaam, Lainlyn, IsleOfBalfiera, Alcaire);

                case Alcaire:
                    return TargetFromRect(dir, WrothgarianMountains, WrothgarianMountains, WrothgarianMountains, Menevia, IsleOfBalfiera, IsleOfBalfiera, Koegria, Koegria);

                case Koegria:
                    return TargetFromRect(dir, WrothgarianMountains, WrothgarianMountains, Alcaire, Alcaire, IsleOfBalfiera, Bhoriane, Kambria, WrothgarianMountains);

                case Bhoriane:
                    return TargetFromRect(dir, Kambria, Koegria, Koegria, IsleOfBalfiera, IsleOfBalfiera, Tigonus, Dwynnen, Dwynnen);

                case Kambria:
                    return TargetFromRect(dir, WrothgarianMountains, WrothgarianMountains, Koegria, Koegria, Bhoriane, Dwynnen, Dwynnen, Dwynnen);

                case Phrygias:
                    return TargetFromRect(dir, -1, -1, WrothgarianMountains, WrothgarianMountains, Dwynnen, Urvaius, Ykalon, -1);

                case Urvaius:
                    return TargetFromRect(dir, Phrygias, Phrygias, Dwynnen, Dwynnen, Anticlere, Anticlere, Daenia, Ykalon);

                case Ykalon:
                    return TargetFromRect(dir, -1, -1, Phrygias, Urvaius, Daenia, IlessanHills, Northmoor, -1);

                case Daenia:
                    return TargetFromRect(dir, Ykalon, Ykalon, Urvaius, Urvaius, Anticlere, Shalgora, IlessanHills, Ykalon);

                case Shalgora:
                    return TargetFromRect(dir, IlessanHills, Daenia, Anticlere, Cybiades, Sentinel, Daggerfall, Daggerfall, IlessanHills);

                case AbibonGora:
                    return TargetFromRect(dir, Betony, Kairou, AlikrDesert, DungeonsButton, DungeonsButton, DungeonsButton, -1, Betony);

                case Kairou:
                    return TargetFromRect(dir, Pothago, Pothago, Myrkwasa, AlikrDesert, AlikrDesert, AbibonGora, AbibonGora, Betony);

                case Pothago:
                    return TargetFromRect(dir, Daggerfall, Anticlere, Myrkwasa, Myrkwasa, Myrkwasa, Kairou, Kairou, Betony);

                case Myrkwasa:
                    return TargetFromRect(dir, Daggerfall, Sentinel, Sentinel, AlikrDesert, AlikrDesert, Kairou, Pothago, Pothago);

                case Ayasofya:
                    return TargetFromRect(dir, Tigonus, Tigonus, AlikrDesert, AlikrDesert, Antiphyllos, Antiphyllos, Sentinel, Cybiades);

                case Tigonus:
                    return TargetFromRect(dir, Dwynnen, IsleOfBalfiera, DragontailMountains, Santaki, AlikrDesert, Ayasofya, Cybiades, Anticlere);

                case Kozanset:
                    return TargetFromRect(dir, Lainlyn, Lainlyn, Totambu, Ephesus, DragontailMountains, DragontailMountains, DragontailMountains, Lainlyn);

                case Satakalaam:
                    return TargetFromRect(dir, Menevia, Wayrest, Mournoth, Totambu, Totambu, Kozanset, Lainlyn, IsleOfBalfiera);

                case Totambu:
                    return TargetFromRect(dir, Satakalaam, Mournoth, Mournoth, DragontailMountains, Ephesus, DragontailMountains, Kozanset, Lainlyn);

                case Mournoth:
                    return TargetFromRect(dir, Wayrest, Gavaudon, -1, -1, DragontailMountains, Ephesus, Totambu, Satakalaam);

                case Ephesus:
                    return TargetFromRect(dir, Totambu, Mournoth, DragontailMountains, DragontailMountains, DragontailMountains, DragontailMountains, Santaki, DragontailMountains);

                case Santaki:
                    return TargetFromRect(dir, DragontailMountains, Totambu, Ephesus, DragontailMountains, Dakfron, Dakfron, Antiphyllos, Tigonus);

                case Antiphyllos:
                    return TargetFromRect(dir, Ayasofya, Ayasofya, Santaki, AlikrDesert, AlikrDesert, Bergama, Bergama, Sentinel);

                case Bergama:
                    return TargetFromRect(dir, Sentinel, Antiphyllos, Dakfron, Dakfron, AlikrDesert, AlikrDesert, AlikrDesert, Sentinel);

                case Gavaudon:
                    return TargetFromRect(dir, WrothgarianMountains, WrothgarianMountains, -1, -1, Mournoth, Mournoth, Wayrest, OrsiniumArea);

                case Tulune:
                    return TargetFromRect(dir, GlenumbraMoors, GlenumbraMoors, Glenpoint, Daggerfall, Betony, -1, -1, -1);

                case GlenumbraMoors:
                    return TargetFromRect(dir, -1, Northmoor, Glenpoint, Glenpoint, Tulune, Tulune, -1, -1);

                case IlessanHills:
                    return TargetFromRect(dir, Ykalon, Ykalon, Daenia, Shalgora, Daggerfall, Glenpoint, Glenpoint, Northmoor);

                case Cybiades:
                    return TargetFromRect(dir, Anticlere, IsleOfBalfiera, Tigonus, Ayasofya, Sentinel, Sentinel, Betony, Daggerfall);

                // Optional: routed exits from buttons back into overworld later
                case DungeonsButton:
                case HomesButton:
                case TemplesButton:
                case TownsButton:
                case ExitButton:
                    return -1;
            }

            return -1;
        }
    }
}
