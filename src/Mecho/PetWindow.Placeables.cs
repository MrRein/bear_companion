using System;
using System.Linq;
using System.Windows;

namespace Mecho;

// Нещата, които стоят на земята като отделни прозорци: печица, лампа, радио,
// хранилка, растение. Правилата са едни за всички:
//  • от всеки вид има най-много един (един прозорец; при грешка излишните се затварят);
//  • сложеното от Тут („Неща на Мечо“) стои, докато Тут не го махне;
//  • сложеното от мечока за някое занимание (радиото, докато танцува; саксията,
//    която изнася от ъгъла) се прибира, щом свърши заниманието;
//  • ако Тут сложи нещо, което мечокът вече е сложил сам – става нейно и остава.
public sealed partial class PetWindow
{
    /// <summary>При пускане: нещо, сложено от мечока, е останало от миналия път – прибира го.</summary>
    private void InitPlaceables()
    {
        if (_save.RadioAuto)
        {
            _save.RadioAuto = false;
            _save.RadioPlaced = false;
        }
        if (_save.RadioAutoOn)
        {
            _save.RadioAutoOn = false;
            _save.RadioOn = false;
        }
        if (_save.PlantPlaced && !Kitchen.Owns(_save, Kitchen.PlantPot)) _save.PlantPlaced = false;
    }

    // ── Радиото и танците ──

    /// <summary>Радиото е сложено от Тут (не от мечока за танц).</summary>
    public bool RadioPlacedByOwner => RadioPlaced && !_save.RadioAuto;

    /// <summary>
    /// „Неща на Мечо“ → Радио. Светнало е и когато мечокът сам го е сложил (танцува) –
    /// цъкането тогава спира танца и радиото се прибира. Иначе слага/прибира радиото на Тут.
    /// </summary>
    public void ToggleRadioPlaced()
    {
        if (!RadioPlaced)
        {
            SetRadio(true);
            return;
        }
        if (_inScene && _scene == Scenes.Music) GetUp();   // EndMusic маха сам сложеното
        if (RadioPlaced) CoreEngine.Bus.Send(new Mecho.Core.RemoveItem(Kitchen.Radio));
        Persist();
        NotifyCare();
    }

    /// <summary>Растението е навън: сложено от Тут или изнесено от мечока (грижи се за него).</summary>
    public bool PlantOut => _save.PlantPlaced || InGarden;

    /// <summary>
    /// „Неща на Мечо“ → Растение. Светнало е и докато мечокът сам се грижи за него – цъкането
    /// тогава спира грижата и той го прибира. Иначе слага/прибира саксията.
    /// </summary>
    public void TogglePlant()
    {
        if (!PlantOut)
        {
            SetPlant(true);
            return;
        }
        if (InGarden) GetUp();
        if (_save.PlantPlaced) SetPlant(false);
        NotifyCare();
    }

    /// <summary>Къде застава да танцува до радиото и от коя страна.</summary>
    private (double Spot, int Side) RadioSpot()
    {
        double cx = _radioWindow?.CenterX ?? _save.RadioX;
        int side = _x < cx ? -1 : 1;
        return (cx + side * (SpriteWidth * 0.6 + 8 * PixelSize), side);
    }

    /// <summary>
    /// Танци при сложено от Тут радио, което е далече: първо отива до него.
    /// true – тръгна (заниманието почва, като стигне).
    /// </summary>
    private bool WalkToRadioFirst(Action start)
    {
        if (InstantModes || !RadioPlacedByOwner || _radioWindow == null) return false;
        var (spot, side) = RadioSpot();
        if (Math.Abs(_x - spot) < SpriteWidth * 0.4) return false;
        if (_inScene) LeaveScene();
        WalkTo(spot, () =>
        {
            _facingLeft = side > 0;
            start();
        });
        return true;
    }

    /// <summary>
    /// Почва да танцува: няма радио – мечокът го слага до себе си и го пуска (PlacedBy.Bear –
    /// ядрото го маха с ReleaseByBear); радиото на Тут е изключено – пуска го (ByBear – после
    /// пак го спира). Правилата са в PlacementModule; тук са само мястото и посоката.
    /// </summary>
    private void BeginMusic()
    {
        if (!Kitchen.Owns(_save, Kitchen.Radio)) return;
        var bus = CoreEngine.Bus;
        if (!RadioPlaced)
        {
            var area = Area;
            int side = _facingLeft ? -1 : 1;
            double x = _x + side * (SpriteWidth * 0.6 + 8 * PixelSize);
            if (x < area.Left + 30 || x > area.Right - 30) x = _x - side * (SpriteWidth * 0.6 + 8 * PixelSize);
            x = Math.Clamp(x, area.Left + 30, area.Right - 30);
            _radioWindow?.Close();
            _radioWindow = null;
            bus.Send(new Mecho.Core.MoveItem(Kitchen.Radio, x));
            bus.Send(new Mecho.Core.PlaceItem(Kitchen.Radio, Mecho.Core.PlacedBy.Bear, x));
            bus.Send(new Mecho.Core.SwitchItem(Kitchen.Radio, true, ByBear: true));
            _facingLeft = x < _x;
        }
        else if (!_save.RadioOn) bus.Send(new Mecho.Core.SwitchItem(Kitchen.Radio, true, ByBear: true));
        Persist();
        NotifyCare();
    }

    /// <summary>Спря да танцува: ядрото прибира/изключва каквото мечокът е сложил/пуснал сам.</summary>
    private void EndMusic()
    {
        CoreEngine.Bus.Send(new Mecho.Core.ReleaseByBear(Kitchen.Radio));
        Persist();
        NotifyCare();
    }

    // ── Растението ──

    /// <summary>„Неща на Мечо“ → Растение: саксията стои на земята, докато Тут не я прибере.</summary>
    public void SetPlant(bool placed)
    {
        if (!Kitchen.Owns(_save, Kitchen.PlantPot)) return;
        if (placed == _save.PlantPlaced) return;
        if (placed)
        {
            // Вече я е изнесъл сам: остава там, където е.
            double x = InGarden && _gardenPhase >= 2 ? PlantSpot
                : _x + (_facingLeft ? -1 : 1) * SpriteWidth * 1.2;
            var area = Area;
            _save.PlantX = Math.Clamp(x, area.Left + PlantW * PixelSize * 0.7, area.Right - PlantW * PixelSize * 0.7);
            _save.PlantPlaced = true;
            _plantOut = 1;
            _plantShownAsVisible = IsVisible;
            ShowGarden();
        }
        else
        {
            if (InGarden) GetUp();
            _save.PlantPlaced = false;
            _plantShownAsVisible = false;
            if (!InGarden && !_plantPushing)
            {
                _plantOut = 0;
                HideGarden();
            }
        }
        Persist();
        NotifyCare();
    }

    private bool _plantShownAsVisible;

    private CookerWindow? _potWindow;
    private string _potArtShown = "";

    /// <summary>Сложеното от Тут растение стои в свой прозорец: мести се с мишката като другите предмети.</summary>
    private bool PotShown => _save.PlantPlaced && Kitchen.Owns(_save, Kitchen.PlantPot) && !InGarden && !_plantPushing;

    private void UpdatePotWindow()
    {
        if (!PotShown)
        {
            if (_potWindow != null)
            {
                _potWindow.Close();
                _potWindow = null;
                _potArtShown = "";
            }
            return;
        }
        _gardenWindow?.Conceal();   // докато стои само – в своя прозорец (не в този на градинката)
        _plantOut = 1;
        string artName = PlantArt;
        var art = _lib.GetProp(artName, (int)PlantW, (int)PlantH, 0).Image;
        if (_potWindow == null)
        {
            var area = AreaAt(_save.PlantX, _y - 1);
            _potWindow = new CookerWindow("Растение");
            Companion(_potWindow, Kitchen.PlantPot);
            _potWindow.SetTime(null);
            _potWindow.Hint = "Растението.";
            _potWindow.Clicked += () => Ask(new ChatQuestion("Растението. Какво да правим?",
                new ChatOption("🌱 Погрижи се за него", p => { if (p.CanDo(Scenes.Plant)) p.StartScene(Scenes.Plant, 120 + p._rng.Next(120)); }),
                new ChatOption("Прибери го", p => p.SetPlant(false)),
                new ChatOption("Остави", _ => { })));
            _potWindow.Dropped += (cx, _) =>
            {
                var a = AreaAt(cx, _potWindow!.Top + _potWindow.ActualHeight / 2);
                double half = PlantW * PixelSize * 0.7;
                double nx = Math.Clamp(cx, a.Left + half, a.Right - half);
                _potWindow.FallTo(nx, a.Bottom);
                _save.PlantX = nx;
                Persist();
            };
            _potWindow.SetAway(!IsVisible);
            _potWindow.Place(art, PixelSize, _save.PlantX, area.Bottom);
            _potArtShown = artName;
        }
        else if (artName != _potArtShown)
        {
            _potArtShown = artName;
            _potWindow.SetArt(art, PixelSize);   // порасна
        }
        _potWindow.Step(0.033);
    }

    /// <summary>Сложеното растение стои на мястото си (и се скрива/показва с мечока).</summary>
    private void UpdatePlacedPlant()
    {
        UpdatePotWindow();
        if (!_save.PlantPlaced || InGarden || _plantPushing || PotShown) return;
        if (_gardenWindow == null || IsVisible != _plantShownAsVisible)
        {
            _plantShownAsVisible = IsVisible;
            _plantOut = 1;
            ShowGarden();
        }
    }

    // ── Само по едно от вид ──

    /// <summary>
    /// Предпазна мрежа: ако по някаква причина има два прозореца за едно и също
    /// (две печици, две радиа…), затваря излишния.
    /// </summary>
    private void CloseDuplicatePlaceables()
    {
        var keep = new Window?[] { _heaterWindow, _lampWindow, _radioWindow, _feederWindow, _potWindow }.Concat(_furnishings.Select(f => f.Window)).ToArray();
        foreach (string title in new[] { "Печица", "Лампа", "Радио", "Хранилка", "Растение" }.Concat(_furnishings.Select(f => f.Title)))
        {
            var mine = keep.FirstOrDefault(w => w?.Title == title);
            foreach (var w in Application.Current.Windows.OfType<CookerWindow>().Where(w => w.Title == title && !ReferenceEquals(w, mine)).ToList())
                w.Close();
        }
    }
}
