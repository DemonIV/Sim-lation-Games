# CLAUDE.md — Guide for future Claude Code sessions

This repository is a **Unity 6 (6000.5.9f1)** project simulating İHA (recon UAV) and SİHA (armed UAV)
with abstract, gamified, educational mechanics.

## Layout

- `Assets/Scripts/Core` — **Sim.Core**: pure C# business/game logic. No MonoBehaviour, no Unity
  scene dependencies. Fully unit-testable.
- `Assets/Scripts/Runtime` — **Sim.Runtime**: MonoBehaviour glue that wires Core logic into the 3D
  scene (controllers, target registry, runtime scene bootstrap).
- `Assets/Tests/EditMode` — **Sim.Tests.EditMode**: NUnit EditMode unit tests for the Core systems.

## Rules

- Put **all new game logic in `Sim.Core`** and cover it with EditMode unit tests.
- Keep **MonoBehaviours thin** — they should only translate Unity input/frames into calls on Core
  logic and reflect Core state back into the scene.
- Develop Core **test-driven**: write/extend EditMode tests alongside the logic.

## Environment note

- `dotnet` / Unity are **not installed** in the web environment, so code **cannot be compiled or run
  here**. Write carefully; correctness is verified by the EditMode tests when opened in the Unity
  Editor.

## Branch convention

- Develop on the assigned feature branch (do not commit directly to a shared default branch).

## Physics & electronic-warfare layer

- Additional Core systems (all in `Sim.Core`, all covered by EditMode tests): `Atmosphere`,
  `BallisticProjectile`, `RadarSystem`, `RadarCrossSection`, `ElectronicWarfare`, `TargetTracker`,
  `SeekerGimbal`, `ProportionalNavigation`.
- Runtime glue (thin MonoBehaviours in `Sim.Runtime`): `RadarSensor`, `RcsComponent`, `Jammer`,
  `GuidedMunition`.
- **Rule:** all guidance, sensor, and ballistics logic lives in `Sim.Core` with EditMode tests;
  the MonoBehaviours stay thin, only wiring Core logic into the scene.

## Tactical layer (M1)

- New Core systems (all in `Sim.Core`, all EditMode-tested): `MissionState`, `TargetAllocation`,
  `EngagementPolicy`, `FuelTank`. Runtime glue (thin MonoBehaviours): `SimulationDirector`
  (mission tracking/scoring), `Hud` (IMGUI overlay), `CameraRig` (free-fly + drone-follow camera).

## Worker kuralları (her oturumda geçerli)

- **Önce `docs/DEVLOG.md` oku.** Proje geçmişi, mimari, sistem envanteri ve mevcut durum orada
  tutulur; bunların prompt'ta tekrarlanmasına gerek yoktur.
- **Derleme yok.** Bu ortamda `dotnet`/Unity kurulu değildir, kod burada derlenemez. Her
  düzenlemeden sonra dosyayı yeniden oku ve mekanik olarak doğrula: parantez/blok dengesi, tipler,
  gerekli `using` satırları, çağrılan her üyenin gerçekten tanımlı olması.
- **Önce oku, sonra düzenle.** Üye adı tahmin etme; düzenleyeceğin dosyayı ve bağımlı olduğu
  dosyaları oku.
- **Unity 6 API'leri.** `FindAnyObjectByType` / `FindObjectsByType(FindObjectsSortMode.None)`
  kullan; `FindObjectOfType`, `FindObjectsOfType`, `GetInstanceID` kullanma.
- **UnityEngine.Object null kontrolü.** Her zaman açık `== null` yaz; asla `?.` kullanma — `?.`
  Unity'nin aşırı yüklenmiş `==` operatörünü baypas eder ve yok edilmiş nesnelerde hataya yol açar.
- **Mimari kuralı.** Yeni oyun mantığı `Sim.Core` içinde saf C# olarak ve EditMode testli yazılır;
  MonoBehaviour'lar ince kalır (yalnızca Core'u sahneye bağlar). Kozmetik/sunum işleri Runtime'da
  kalır, test gerekmez.
- **Oyun değerlerine dokunma.** Kozmetik bir görev verildiyse menzil, hasar, can, atış hızı,
  irtifa, spawn konumu gibi değerleri değiştirme.
- **Küçük dilimler halinde çalış.** Her mantıksal dilimi ayrı commit'le ve
  `git push -u origin <branch>` ile gönder (ağ hatasında 2s/4s/8s/16s bekleyerek en fazla 4 deneme;
  yetki hatasında dur ve hatayı bildir). Böylece iş yarıda kesilse bile repo derlenebilir kalır.
- **`docs/DEVLOG.md`'yi güncelle.** Yeni sistem eklediysen sistem tablosuna satır, her turda
  değişiklik günlüğüne madde ve commit geçmişine satır ekle.
- **Raporunu kısa tut.** Ne değiştirdiğin, tahmin/uyarlama yaptığın yerler, commit hash'i ve push
  sonucu.
