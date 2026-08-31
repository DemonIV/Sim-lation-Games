# Çalışma Zamanı Sahnesi — Analiz ve Referans

> Bu belge, projenin **Play** anında ürettiği sahneyi kaynak koddan yeniden kurgular: nesne
> hiyerarşisi, nesne başına bileşen envanteri, uzamsal yerleşim, yaşam döngüsü ve tespit edilen
> sorunlar. Referans amaçlıdır; hiçbir oyun değeri burada değiştirilmemiştir.
>
> **Son güncelleme:** 2026-08-31 · **Branch:** `claude/slack-session-f6uh9g`

---

## 0. Sahne varlığı yok — sahne kodla kuruluyor

`find Assets -name '*.unity'` **hiçbir sonuç döndürmez**; depoda `.unity` sahne varlığı da,
`.unity.meta` de yoktur. `ProjectSettings/` yalnızca `ProjectVersion.txt` içerir (yani
`EditorBuildSettings.asset` da yok → **Build Settings sahne listesi boş**).

Sahnenin tamamı `Sim.Runtime.SimulationBootstrap.Awake()` → `Build()`
(`Assets/Scripts/Runtime/SimulationBootstrap.cs`) içinde primitive'lerden kurulur. Kullanım (DEVLOG §4): boş bir sahne aç → boş bir GameObject'e
`SimulationBootstrap` ekle → Play. Bu kararın iki sonucu vardır ve ikisi de aşağıdaki bulgular
tablosunda yer alır: (a) `SceneManager.LoadScene(GetActiveScene().buildIndex)` tabanlı yeniden
başlatma yolu kayıtlı bir sahne varlığı olmadan çalışmaz (B-01); (b) sahnedeki her şey her Play'de
sıfırdan üretildiği için başlangıç maliyeti ve nesne sayısı tamamen kodun kontrolündedir.

> **Güncelleme (B-01 çözüldü):** yeniden başlatma artık sahne yüklemiyor. Üretilen dünyanın tamamı
> tek bir **`Simulation`** kök nesnesinin altında kuruluyor ve `SimulationBootstrap.Rebuild()` bu
> kökü yıkıp `Build()`'i yeniden çağırıyor (`R` ve görev ortasında menüden seçim bu yolu kullanır).
> `Main Camera` ile `Directional Light` bilerek kökün **dışında** kalır, yeniden kurulumdan sağ
> çıkar.

Render hattı: `Packages/manifest.json` **URP içermez** (`com.unity.render-pipelines.universal` yok)
→ proje **Built-in** hatta çalışır. Koddaki tüm `Universal Render Pipeline/Lit` yedekleri ölü
koddur; `RenderSettings.fog` / `skybox` / `ambientMode` çağrıları Built-in'de geçerlidir.

---

## 1. Nesne hiyerarşisi

### 1.1 Açılışta (Awake) kurulan kök nesneler

Not: aşağıdaki `Terrain`'den itibaren **her şey** tek bir `Simulation` kök nesnesinin çocuğudur
(yeniden kurulum için); `Main Camera` ve `Directional Light` kökün dışındadır.

```
<Kullanıcının boş GameObject'i>        ← SimulationBootstrap (sahnedeki tek elle kurulan nesne)
Main Camera                            ← yalnızca Camera.main yoksa oluşturulur, tag "MainCamera"
Directional Light                      ← yalnızca sahnede yönlü ışık yoksa oluşturulur
Simulation                             ← üretilen dünyanın tek kökü (Rebuild bunu yıkar)
Terrain                                ← prosedürel mesh, collider yok
Airbase
├── Apron            (Cylinder)
├── Runway           (Cube)
├── RunwayMark_0 … RunwayMark_8        (9 adet Cube)
├── Hangar_1         (Cube)
└── Hangar_2         (Cube)
Props
├── Tree_0 … Tree_219                  (boş kök; her biri 3 çocuk: Trunk, Foliage_0, Foliage_1)
├── Rock_0 … Rock_69                   (Sphere)
└── Building_0 … Building_17           (Cube)
IHA_1, IHA_2, SIHA_1                   (Capsule kök + "Model" çocuğu)
WP_<x>_<z>_0 … _3                      (drone başına 4 boş waypoint, Simulation kökünün altında)
ScenarioController                     (boş GameObject)
SimulationDirector                     (boş GameObject — 5 yönetici bileşen taşır)
```

Yaklaşık nesne sayısı: Props ≈ **969** GameObject (220×4 + 70 + 18 + kök), Airbase 15,
drone'lar 3×(1 kök + 1 `Model` + 11…15 parça) ≈ 45, waypoint'ler 12, arazi 1, yöneticiler 2.
Toplam açılış ≈ **1050 GameObject**, ~1000+ ayrı `MeshRenderer`.

### 1.2 Araç model hiyerarşileri

Tüm araçlar aynı desende kurulur (`VehicleModelBuilder`): spawner bir primitive kök yaratır,
`HideRootMesh` kökün `MeshRenderer`'ını **kapatır** (collider'ı bilerek bırakır,
`VehicleModelBuilder.cs:161`), sonra kökün altına tek bir `"Model"` çocuğu açılır. `Model`'in
`localScale`'i kökün ölçeğinin **tersidir** (`CreateModelRoot`, `VehicleModelBuilder.cs:175`), böylece
silüet kökün ölçeğinden etkilenmez. Her parçanın `Collider`'ı `Part()` içinde yok edilir
(`VehicleModelBuilder.cs:222`).

| Araç | `Model` altındaki parçalar |
|---|---|
| **İHA** (`BuildReconUav`) | `Fuselage`, `Nose`, `SensorTurret`, `Wing`, `WingtipFinL`, `WingtipFinR`, `TailBoom`, `VTailL`, `VTailR`, `PropHub`, **`Propeller`** (11) |
| **SİHA** (`BuildArmedUav`) | İHA'nın 11 parçası + `PylonL`, `PylonR`, `MunitionL`, `MunitionR` (15) |
| **Düşman avcısı** (`BuildEnemyFighter`) | `Fuselage`, `NoseCone`, `DeltaWing`, `SweepL`, `SweepR`, `TailFinL`, `TailFinR`, `EngineGlow` (8) |
| **SAM** (`BuildSamSite`) | `Base`, **`Turret`** (boş, ölçeksiz pivot) → `TurretBody`, `TubeLF`, `TubeRF`, `TubeLB`, `TubeRB`; ayrıca `Model` altında **`Radar`** (8) |
| **AAA** (`BuildAaaSite`) | `Base`, **`Turret`** (pivot) → `TurretBody`, `BarrelL`, `BarrelR` (5) — `Radar` parçası **yok** |
| **Yer hedefi** (`BuildGroundTarget`) | `Hull`, `Cabin`, `WheelFL`, `WheelFR`, `WheelBL`, `WheelBR` (6) |

Animasyonla adı üzerinden bulunan parçalar: `"Propeller"` (`PropellerSpinner`), `"Model"`
(`BankingVisual`), `"Turret"` + `"Radar"` (`TurretVisual`).

### 1.3 Dalga başına üretilenler (`ScenarioController.SpawnWave`, `ScenarioController.cs:133`)

Adlandırma: `Hostile_W{dalga}_{i}`, `SAM_W{dalga}_{i}`, `AAA_W{dalga}_{i}`, `Fighter_W{dalga}_{i}`.
Hepsi **`Simulation` kökünün** altında oluşturulur (yeniden kurulumda birlikte yıkılsınlar diye).
Adet, seçilen senaryonun
`ScenarioLibrary.Composition(SelectedKind, waveIndex)` çıktısından gelir.

### 1.4 Geçici (kısa ömürlü) nesneler

| Nesne | Üreten | Ömür / temizlik |
|---|---|---|
| `GuidedMunition` (Sphere) | `SihaController.LaunchMunition` (`:156`) | `maxLifetime` 12 s, yakınlık tapası veya hedef kaybı; `Destroy` |
| `SAM_Munition` (Sphere) | `AirDefenseSite.LaunchMunition` (`:130`) | aynı |
| `EngineGlow` (Sphere) | `GuidedMunition.SetupVisuals` (`:173`) | mühimmatın çocuğu, onunla ölür |
| `Tracer` (LineRenderer) | `TracerEffect.Spawn` (`:24`) | 0.06 s |
| `ExplosionEffect` + `ExplosionSphere` | `ExplosionEffect.Spawn` (`:29`) | 0.45 s |
| `VfxGlow` / `VfxFlash` / `VfxDebris` / `VfxSmoke` / `VfxShockwave` / `VfxSpark` | `VfxLibrary` | `VfxTicker` ile kendini yok eder; global bütçe 220 |
| `ScorchMark` (Cylinder) | `Targetable.TakeDamage` (`TargetRegistry.cs:48`) | 25 s, en fazla 40 iz |

---

## 2. Nesne başına bileşen envanteri

| Nesne | Bileşenler | Ekleyen |
|---|---|---|
| **Bootstrap host** | `SimulationBootstrap` | elle (kullanıcı) |
| **Main Camera** | `Camera`, `CameraRig` | `SimulationBootstrap.cs:70, :60` |
| **Directional Light** | `Light` (Directional) | `:79` |
| **Terrain** | `MeshFilter`, `MeshRenderer` | `EnvironmentBuilder.cs:79-80` |
| **Airbase / Props parçaları** | `MeshFilter`, `MeshRenderer` (collider **silinmiş**) | `EnvironmentBuilder.Prop` (`:250`) |
| **IHA_1 / IHA_2** | `MeshFilter`+`MeshRenderer` (kapalı) + **`CapsuleCollider` (duruyor)**, `GunTurret`(200, 8, 45, 3, 3), `CountermeasureDispenser`, `IhaController`, `RadarSensor`, `PropellerSpinner`, `BankingVisual`, `Targetable`(Faction 0, 100 HP), `DamageVisuals` | `SpawnIha` (`:99`) |
| **SIHA_1** | aynısı, ama `GunTurret`(300, 10, 60, 2.5, 4.5) ve `IhaController` yerine **`SihaController`** | `SpawnSiha` (`:129`) |
| **WP_\*** | (bileşen yok — yalnız `Transform`) | `RectangleRoute` (`:178`) |
| **ScenarioController** | `ScenarioController` | `:37` |
| **SimulationDirector** | `SimulationDirector`, `Hud`, `GameControls`, `ScenarioMenu`, `PlayerDroneController` | `:42-55` |
| **Hostile_\*** (yer hedefi) | Cube kök + **`BoxCollider` (duruyor)**, `Targetable`(F1, 60 HP), `DamageVisuals`, `RcsComponent` | `SpawnPlainHostile` (`:182`) |
| **SAM_\*** | Cylinder kök + **`CapsuleCollider` (duruyor)**, `Targetable`(F1, 120 HP), `DamageVisuals`, `RcsComponent`, `AirDefenseSite`(160/120/1.2 s/6/0.4 rps/150/55), `TurretVisual` | `SpawnSam` (`:210`) |
| **AAA_\*** | aynısı, `AirDefenseSite`(80/60/0.8 s/20/1.5 rps/130/20), `Targetable`(F1, 70 HP) | `SpawnAaa` (`:244`) |
| **Fighter_\*** | Capsule kök + **`CapsuleCollider` (duruyor)**, `Targetable`(F1, 70 HP), `DamageVisuals`, `RcsComponent`, `GunTurret`(200, 8, 55, 3, 3.5; kırmızı izli mermi), `CountermeasureDispenser`(6/2.5 s/%50), `EnemyDroneController`, `BankingVisual` | `SpawnFighter` (`:279`) |
| **Mühimmat** | Sphere kök + **`SphereCollider` (duruyor)**, `GuidedMunition`, `TrailRenderer` | `SihaController.cs:158` / `AirDefenseSite.cs:132` |

Sahnede hiç `Rigidbody`, `Physics.Raycast`, `OnTrigger*`/`OnCollision*` **yoktur** (tüm kaynak
tarandı). Yukarıda "duruyor" diye işaretlenen kök collider'lar hiçbir şey tarafından
kullanılmıyor (bkz. B-05).

---

## 3. Uzamsal yerleşim

### 3.1 Kamera ve ışık

| Öğe | Değer | Kaynak |
|---|---|---|
| Kamera konumu | `(0, 60, −80)`, `LookAt(0,0,0)` | `SimulationBootstrap.cs:71-72` |
| Far clip | önce 1000, sonra **1200** | `:73` → `EnvironmentBuilder.cs:224` |
| FOV | Unity varsayılanı 60 (art yakıcıda +12) | `CameraRig.cs:44` |
| Serbest uçuş hızı | 40 m/s (Shift ×3) | `CameraRig.cs:18-19` |
| Güneş | önce Euler(50, −30, 0) / yoğunluk 1 → sonra **Euler(45, −35, 0)**, yoğunluk 1.15, renk (1, 0.96, 0.88), **Soft shadows** | `:82` → `EnvironmentBuilder.cs:215-221` |
| Gökyüzü | `Skybox/Procedural`, `_SkyTint`(0.45, 0.58, 0.80), atmosfer 1.1, exposure 1.15 | `EnvironmentBuilder.cs:189-198` |
| Sis | `ExponentialSquared`, yoğunluk **0.0025**, renk (0.70, 0.79, 0.90) | `:205-208` |
| Ortam ışığı | `Trilight` (sky/equator/ground) | `:210-213` |

### 3.2 Arazi ve üs

- **Arazi:** `BuildTerrain(halfExtent = 150, cellSize = 5)` → **300 × 300 m**, 60×60 hücre,
  61×61 = 3721 vertex, 7200 üçgen, `IndexFormat.UInt32`, **collider yok**.
  Yükseklik `TerrainField.Height` (Perlin, genlik ±3 m, frekans 0.012).
- **Düz bölge:** `TerrainField.FlatRadius = 45 m` içinde yükseklik tam **0**; 45→70 m arasında
  (`FlatBlend = 25`) kabartıya geçiş.
- **Üs (origin):** `Apron` silindir ölçek (18, 0.05, 18) → **çap 18 m (yarıçap 9)**;
  `Runway` küp **6 × 44 m** (z ekseninde ±22); 9 pist çizgisi z = −18…+18, adım 4.5;
  `Hangar_1` (−13, 1.6, −8) 7×3.2×9; `Hangar_2` (−13, 1.4, 6) 6×2.8×8.
  **Üssün gerçek ayak izi ≈ 22 m yarıçap.**
- **Prop dağılımı:** `ScatterProps(halfExtent = 150)` — 220 ağaç, 70 kaya, 18 bina;
  keep-out = `FlatRadius + 10` = **55 m**; nokta başına 12 deneme, bulunamazsa atlanır;
  hepsi `TerrainField.Height` ile araziye oturtulur. Sabit tohum: `Random.InitState(12345)`
  (`EnvironmentBuilder.cs:122`) — bkz. B-08.

### 3.3 Drone spawn noktaları ve devriye rotaları

| Drone | Spawn (= `BasePosition` = seyir irtifası) | Rota (dikdörtgen, genişlik × derinlik) | Rota köşesi max yarıçap |
|---|---|---|---|
| `IHA_1` | (−20, **10**, −20) | 30 × 25 | ≈ 47.9 m |
| `IHA_2` | (20, **12**, 20) | 25 × 30 | ≈ 47.8 m |
| `SIHA_1` | (0, **14**, −30) | 40 × 20 | ≈ 44.7 m |

Waypoint'ler spawn irtifasında düz kalır (offset'lerin `y` bileşeni 0). Rotalar 150 m'lik arazinin
çok içinde; rota **araziden çıkmıyor**. `minAltitude = 5 m` sert taban, `_cruiseAltitude` =
spawn irtifası (`IhaController.cs:280`). İkmal yarıçapı `baseRadius = 12 m`, servis süresi 4 s —
yani drone **üsse değil, kendi spawn noktasına** dönüyor (bkz. B-11).

### 3.4 Düşman spawn kuralları

`RandomScatterPosition` (`ScenarioController.cs:156`): X ve Z bağımsız olarak
`[−fieldHalfExtent, +fieldHalfExtent] = [−40, +40]`; düzlemsel yarıçap **`spawnMinRadius = 15 m`**
altındaysa 8 deneme, sonra savunma amaçlı olarak aynı kerteriz üzerinde 15 m'ye itilir.

- **Yer birimleri** (yer hedefi / SAM / AAA): `pos.y` spawn'da `TerrainField.Height(x, z)` ile
  ezilir (`:185`, `:213`, `:247`) → ±40 kutusunun neredeyse tamamı düz bölgede olduğu için
  fiilen **y = 0**. Serileştirilmiş `groundY = 1` alanı bu yüzden **hiç kullanılmıyor** (B-12).
- **Avcı drone'lar:** `RandomAirbornePosition` yalnız `y`'yi **`fighterAltitude = 14`** yapar
  (`:145`) — `EnemyDroneController.cruiseAltitude` ile aynı. Arama modunda hepsi merkez etrafında
  `standoff = 25 m` yarıçaplı, **aynı 14 m irtifadaki** yörüngeye yerleşir (B-09).
- Spawn'lar arasında **çakışma/ayrışma kontrolü yok**; keep-out yalnız merkeze göredir.

İrtifa özeti: dost seyir 10/12/14 m · düşman avcı 14 m · sert taban 5 m · yer birimleri ~0 m ·
kamera başlangıcı 60 m · sis görünürlüğü ~400 m · far clip 1200 m · arazi köşesi 212 m.

---

## 4. Yaşam döngüsü

### 4.1 Awake (tek kare, sırayla)

`SimulationBootstrap.Awake` → `Build()`:

1. `TargetRegistry.Clear()` — önceki kurulumdan kalan kayıtları düşürür; ardından `Simulation`
   kök nesnesi oluşturulur (`Root`) ve 3–6. adımdaki her şey onun altına bağlanır.
2. `EnsureCameraAndLight()` → kamera (yoksa), **yönlü** ışık (yoksa; patlama nokta ışıkları
   sayılmaz), `EnvironmentBuilder.ApplyAtmosphere(Camera.main, sun)`. İkisi de kökün dışında.
3. `CreateGround()` → `BuildTerrain()` + `BuildAirbase(Vector3.zero)` + `ScatterProps(150)`.
4. `SpawnIha` ×2, `SpawnSiha` ×1 — her birinde `GunTurret` ve `CountermeasureDispenser`
   controller'dan **önce** eklenir; `AssignRoute` patrol listesini **reflection** ile private
   `patrolWaypoints` alanına yazar (`:170`).
5. `ScenarioController` nesnesi.
6. `SimulationDirector` nesnesi + `Hud` + `GameControls` + `ScenarioMenu` + `PlayerDroneController`.
7. `Camera.main`'e `CameraRig`.

`AddComponent` çağrıldığı anda o bileşenin `Awake`'i çalışır: `Targetable.Awake` id alıp
`TargetRegistry`'ye kaydolur, `DamageVisuals.Awake`/`RcsComponent.Awake` referanslarını çözer.

### 4.2 Start (ilk Update'ten önce)

| Bileşen | Start'ta ne yapar |
|---|---|
| `IhaController`/`SihaController` | `EnsureInitialized()`: FlightModel, WaypointNavigator (waypoint konumları **kopyalanır**), TargetingSystem, FuelTank, EngagementPolicy, ResupplyPoint, `_gun`/`_self`/`_cm`, `BasePosition` = spawn |
| `RadarSensor`, `GunTurret`, `CountermeasureDispenser`, `AirDefenseSite`, `EnemyDroneController` | saf mantık çekirdeklerini tembel kurar (`EnsureInitialized`/`EnsureGun`) |
| `ScenarioController` | `_state = new ScenarioState(ScenarioLibrary.TotalWaves(SelectedKind))`; **`Started` false kalır** |
| `SimulationDirector` | `MissionState(0, int.MaxValue)` — saf skor sayacı, kendi kendine asla bitmez; kazan/kaybet `ScenarioController`'da (B-03 çözüldü) |
| `ScenarioMenu` | `_autoBegin` ise doğrudan `BeginMission()` + `timeScale = 1`; değilse `IsOpen = true`, `timeScale = 0` |
| `Hud` | yönetici referanslarını + `RadarSensor[]` listesini bulur (2 s'de bir tazelenir) |
| `CameraRig` | yaw/pitch'i mevcut rotasyondan tohumlar, `_baseFov` |
| `PropellerSpinner`/`BankingVisual`/`TurretVisual` | adlı çocuk transform'ları bulur |

### 4.3 Dalga döngüsü (`ScenarioController.Update`, `:85`)

`Started` false iken **hiçbir şey** çalışmaz. Sonrasında her kare:
`AwaitingSpawn` ise dalgayı spawn eder → `TargetRegistry.GetSnapshot(1).Count` ile canlı düşmanı
sayar → `ScenarioState.UpdateEnemies` dalga ilerletir (son dalga temizlenince zafer) → 1 s'de bir
tazelenen dost listesi üzerinden `SquadStatus.IsCombatIneffective(alive, fuelled)` ile bozgun
kontrolü yapar. Dalga nesneleri kök seviyesinde birikir; ölenler `Targetable.OnDestroy` ile
kayıttan düşer, sahnede iz olarak yalnız `ScorchMark`'lar kalır (maks. 40).

### 4.4 Yeniden başlatma (`R`) ve menüden görev değiştirme

Her ikisi de aynı yolu kullanır (`GameControls.cs:29` ve `ScenarioMenu.cs:391`):

```
Time.timeScale = 1;
TargetRegistry.Clear();
SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
```

Sahne yeniden yüklenince `SimulationBootstrap.Awake` yeniden çalışır ve sahne sıfırdan kurulur.

**Yeniden yükleme boyunca yaşayan statik durum:**

| Statik | Davranış |
|---|---|
| `ScenarioController.SelectedKind` | **Kasıtlı** — seçilen görev reload'dan sağ çıkar |
| `ScenarioMenu._autoBegin` | **Kasıtlı** — reload sonrası brifing bir daha sorulmaz |
| `TargetRegistry.All` | `Clear()` ile boşaltılır; ayrıca `Prune()` ölü sarmalayıcıları atar |
| `TargetRegistry._nextId` | **Sıfırlanmaz** — id'ler artmaya devam eder (istenen davranış: id çakışması olmaz) |
| `GuidedMunition.Active` | Temizlenmez; yalnız `Prune()` ölü girdileri atar (B-06) |
| `ScorchMark.Live` | Temizlenmez; `Spawn`'da ölü girdiler ayıklanır |
| `VfxLibrary._live` | `VfxTicker.OnDestroy` → `Release()` ile boşalır; `ResetBudget()` **hiç çağrılmıyor** |
| `MaterialLibrary.Cache`, `_shader` | Kasıtlı önbellek; yüklemeler arası korunur |
| `HudTheme` stilleri, `_white` | `GUIStyle` saf C#, korunur; `_white` null kontrollü yeniden kurulur |
| `CameraRig._instance` | `OnDestroy`'da temizlenir |

---

## 5. Bulgular

Ciddiyet: **yüksek** = özelliği bozar / kesin hata · **orta** = performans veya davranış kaybı,
kullanıcı fark eder · **düşük** = ölü kod, kozmetik tutarsızlık, küçük risk.

| # | Ciddiyet | Bulgu | Yer | Önerilen düzeltme |
|---|---|---|---|---|
| **B-01** ✅ **ÇÖZÜLDÜ** | **yüksek** | Depoda kayıtlı bir `.unity` sahnesi ve `EditorBuildSettings.asset` yok. Build Settings'e eklenmemiş bir sahnede `Scene.buildIndex` **−1** döner, `SceneManager.LoadScene(-1)` istisna atar. Yani **`R` yeniden başlatma ve menüden görev değiştirme kutu dışında çalışmaz** — mid-mission görev seçimi de aynı yolu kullandığı için sessizce ölür (`_autoBegin` true kalır ve sonraki gerçek reload'da brifingi atlar). | `GameControls.cs:33`, `ScenarioMenu.cs:400` | Ya minimal bir `Assets/Scenes/Main.unity` (içinde tek `SimulationBootstrap` nesnesi) ekleyip Build Settings'e koy, ya da yeniden yükleme yolunu `buildIndex < 0` durumunda `SceneManager.LoadScene(GetActiveScene().name)`'e / sahne içi yeniden kuruluma düşür. **Çözüm:** sahne içi yeniden kurulum — üretilen dünya tek bir `Simulation` kökü altında toplandı, `SimulationBootstrap.Rebuild()` bu kökü yıkıp `Build()`'i yeniden çağırıyor; `GameControls` (R) ve `ScenarioMenu` artık `SceneManager` kullanmıyor. |
| **B-02** | **yüksek** | `RadarSensor.Update` her kare **her aday hedef için** `TargetRegistry.FindById` çağırıyor; `FindById` de her çağrıda `Prune()` + tam liste taraması yapıyor → drone başına **O(n²)** + üstüne aday başına iki `GetComponent` (`RcsComponent`, `Jammer`). Sahnede hiçbir nesneye `Jammer` **eklenmiyor**, yani bu `GetComponent` her kare boşuna. | `RadarSensor.cs:83-105` | `GetSnapshot` yerine `TargetRegistry.All` üzerinde tek geçiş yap (id→Targetable araması gereksiz), `RcsComponent`/`Jammer` referanslarını `Targetable` üzerinde bir kez çözülüp önbelleklenmiş alanlardan oku. |
| **B-03** ✅ **ÇÖZÜLDÜ** | **orta** | `SimulationDirector.Start` başlangıç düşman sayısını sayıyor ama o anda **hiç düşman yok** (spawn artık `ScenarioController.Update`'te) → `MissionState.HostilesTotal` her zaman 0. Bootstrap'teki "created LAST so the director's Start() counts every hostile that was just spawned" yorumu **eskimiş**. Ayrıca `MaxFriendlyLosses = 1`: **ikinci dost kaybında `MissionState.Status = Lost`** olur ve `RecordHostileDestroyed` erken döner → **skor sessizce donar**, oysa `ScenarioController` görevi sürdürmeye devam eder. | `SimulationDirector.cs:40-52`, `MissionState.cs:45-49`, `SimulationBootstrap.cs:39-41` | Skor takibini kazan/kaybet değerlendirmesinden ayır: `MissionState`'i yalnız sayaç olarak kullan (`Evaluate()` çağrısını kaldır veya `MaxFriendlyLosses = int.MaxValue` ver) ve eskimiş yorumu güncelle. **Çözüm:** `SimulationDirector.Start` artık `new MissionState(0, int.MaxValue)` kuruyor (asla kendi kendine bitmez, skor görev boyunca birikir), `maxFriendlyLosses` alanı kaldırıldı, bootstrap'teki eskimiş yorum güncellendi; HUD `İMHA`/`KAYIP` hücreleri anlamsız `/0` ve `/2147483647` paydalarını göstermemek için yalnız sayacı yazıyor (`Core/MissionState.cs` değişmedi). |
| **B-04** | **orta** | `TracerEffect.Spawn` her mermi için bir `GameObject` + `LineRenderer` + `Shader.Find` + **`new Material`** üretiyor ve materyali **hiç yok etmiyor**; `ExplosionEffect` de `_material`'ı `OnDestroy`'da bırakmıyor. `GunTurret` 8–10 atış/sn ile çalıştığından saniyede onlarca materyal sızıyor. Ayrıca izli mermiler `VfxLibrary` bütçesine **tabi değil**. | `TracerEffect.cs:24-51`, `ExplosionEffect.cs:74-118` | İzli mermi materyalini statik olarak bir kez üret ve paylaş (`sharedMaterial`); `ExplosionEffect`'e `OnDestroy` içinde `Destroy(_material)` ekle; izli mermileri de `VfxLibrary.TrySpawnBudget()` ile sınırla. |
| **B-05** | **orta** | Projede **hiç `Rigidbody`, raycast veya çarpışma geri çağrısı yok** — ama drone, düşman, SAM/AAA ve **her mühimmat** kökünde collider duruyor (`HideRootMesh` collider'ı bilerek bırakıyor). Rigidbody'siz collider'lar her kare transform'la taşınıyor → PhysX'in statik broadphase'ini sürekli yeniden kurması. `com.unity.modules.physics` bağımlılığı da fiilen kullanılmıyor. | `VehicleModelBuilder.cs:161`, `SihaController.cs:158`, `AirDefenseSite.cs:132`, `ScenarioController.cs:187/215/249/281` | Kök collider'ları da sil (mühimmat dahil) veya en azından mühimmatlarınkini kaldır. İleride raycast gerekirse collider'ları ayrı bir katmanda bilinçli olarak geri getir. |
| **B-06** | **orta** | Yönetici katmanı her kare `TargetRegistry.GetSnapshot()` ile **yeni `List<DetectableTarget>`** ayırıyor: `ScenarioController.Update` 1, `SimulationDirector.Update` 2 + `UpdateAllocation` 1, artı drone başına 1 (`RunSensing`) + 1 (`RadarSensor`), AAA/SAM başına 1, avcı başına 1. `UpdateAllocation` ayrıca her kare 3 liste + 1 `int[]` ayırıyor. 8 düşmanlı bir dalgada kare başına 15+ tahsis. | `TargetRegistry.cs:110-124`, `SimulationDirector.cs:71-72, 108-141`, `ScenarioController.cs:99` | `GetSnapshot`'a yeniden kullanılabilir bir tampon alan aşırı yüklemesi ekle (`GetSnapshot(int faction, List<DetectableTarget> buffer)`); yalnız sayım gereken yerlerde (`ScenarioController.cs:99`) tahsissiz bir `CountAlive(faction)` kullan. |
| **B-07** | **orta** | Yer düşmanları merkezden **15 m** keep-out ile spawn ediyor, oysa üssün ayak izi ~**22 m** (pist z ±22, hangarlar (−13, ±8) → r ≈ 15.3–16.6). Yani SAM/AAA/yer hedefi **pistin üstünde veya hangarın içinde** doğabilir. Ayrıca spawn'lar arasında ayrışma kontrolü olmadığından iki birim üst üste gelebilir. | `ScenarioController.cs:28` (`spawnMinRadius = 15`), `EnvironmentBuilder.cs:99-109` | `spawnMinRadius`'u üssün ayak izinin üstüne çıkar (≥ 25) **veya** üs dikdörtgenini ayrı bir keep-out olarak ekle; spawn edilen konumu son N konuma karşı minimum mesafeyle (örn. 6 m) reddet. Not: bu bir **oyun değeri** değişikliğidir, ayrı bir görevde bilinçli yapılmalı. |
| **B-08** | **orta** | `ScatterProps` global `Random.InitState(12345)` çağırıyor ve durumu **geri yüklemiyor**. Bootstrap'te prop dağılımı düşman spawn'ından önce çalıştığı için tüm `Random.Range` tüketicileri (düşman konumları, isabet zarları, radar gürültüsü, aldatma zarı) sabit bir diziden besleniyor → her Play/restart **birebir aynı** düşman yerleşimi ve aynı zar sırası. | `EnvironmentBuilder.cs:122` | `Random.State` kaydet/geri yükle (`var s = Random.state; Random.InitState(12345); … Random.state = s;`) veya prop dağılımı için ayrı bir `System.Random` kullan. |
| **B-09** | **orta** | Bir dalganın tüm avcı drone'ları aynı karede, **tam olarak aynı irtifada (14 m)** ve mesafe kontrolü olmadan spawn ediyor; arama modunda hepsi merkez etrafında aynı `standoff = 25 m` yörüngesine oturuyor → üst üste binen, iç içe geçen siluetler. | `ScenarioController.cs:145-150`, `EnemyDroneController.cs:35, 40, 136-143` | Spawn irtifasına ve loiter yarıçapına indekse bağlı küçük bir ofset ver (örn. `y += i * 2`, `standoff += i * 4`) ve `_wanderAngle` faz farkını koru. Kozmetik ofset olarak yapılırsa oyun değerlerine dokunmaz. |
| **B-10** | **düşük** | Görsel yenilemeden sonra **`ApplyColor` artık gereksiz**: hemen ardından `HideRootMesh` renderer'ı kapatıyor. Yine de her spawn'da `Shader.Find` + `renderer.material` (materyal örnekleme) + `new Material` yapılıyor — spawn başına iki boşa materyal. Aynı ölü kod hem bootstrap'te hem senaryo denetleyicisinde kopyalanmış. | `SimulationBootstrap.cs:103, 136, 199-213`, `ScenarioController.cs:191, 219, 253, 285, 319-334` | `ApplyColor` çağrılarını ve her iki `ApplyColor` metodunu sil; renk zaten `VehicleModelBuilder`'a `primary` parametresiyle geçiyor. |
| **B-11** | **düşük** | `BasePosition` = drone'un **spawn noktası** (havada, (−20, 10, −20) vb.), görsel üs ise **origin**'de. İkmal drone'ları üssün üzerinde değil, boş gökyüzünde tur atıyor; üs pisti/hangarları hiçbir oyun mantığına bağlı değil. | `IhaController.cs:278`, `EnvironmentBuilder.cs:93` | Ya `BasePosition`'ı üs merkezinin üzerine (örn. `(0, cruise, 0)`) al, ya da drone spawn noktalarını üssün üstüne taşı. **Oyun değeri** değişikliği — ayrı görev. |
| **B-12** | **düşük** | Ölü serileştirilmiş alanlar: `ScenarioController.groundY` (`RandomScatterPosition` döndürüyor ama her spawner `TerrainField.Height` ile eziyor) ve `ScenarioController.totalWaves` (yalnız `_state == null` iken fallback). `VfxLibrary.ResetBudget()` hiç çağrılmıyor. `SihaController.projectileSpeed`/`damagePerHit`'in `useGuidedMunition == true` dalında sınırlı etkisi var (`intercept` hesaplanıp kullanılmıyor). | `ScenarioController.cs:27, 23, 178`, `VfxLibrary.cs:103`, `SihaController.cs:97` | Kullanılmayan alanları kaldır veya kullanıldıkları yeri netleştir; `intercept` hesabını yalnız `useGuidedMunition == false` dalına taşı. |
| **B-13** | **düşük** | 12 waypoint GameObject'i kök seviyesinde, parent'sız oluşturuluyor; `IhaController.EnsureInitialized` konumları `List<Vector3>`'e **kopyaladığı** için Start'tan sonra transform'lar hiç okunmuyor. Drone yok edilse bile hiyerarşide kalıyorlar. Ayrıca rota, private alana **reflection** ile yazılıyor. | `SimulationBootstrap.cs:170-196`, `IhaController.cs:245-253` | Waypoint'leri drone'un çocuğu yap veya tek bir `Routes` kökü altında topla; daha iyisi, `IhaController`'a `public void SetRoute(IList<Vector3>)` ekleyip reflection'ı ve GameObject'leri tümden kaldır. |
| **B-14** | **düşük** | Ordering kırılganlığı: `ScenarioController.Start` (`:72`) `_state`'i **koşulsuz** yeniden kuruyor. `ScenarioMenu.Start` `_autoBegin` yolunda `BeginMission()` çağırdığı için, Unity `ScenarioMenu.Start`'ı önce çalıştırırsa `Started = true` kalır ama dalga durumu sıfırlanır. Bugün nesne oluşturma sırası sayesinde zararsız, ama garanti değil. Benzer şekilde bootstrap'teki "GunTurret'ı controller'dan önce ekle" yorumları gereksiz — bağımlılıklar `Start`'ta `GetComponent` ile çözülüyor. | `ScenarioController.cs:68-73`, `ScenarioMenu.cs:76-86`, `SimulationBootstrap.cs:109-116` | `Start`'ı `if (_state == null)` ile korumalı hâle getir. |
| **B-15** | **düşük** | Görünür dünya kenarı: arazi ±150 m, sis yoğunluğu 0.0025 (≈400 m'de doyum), far clip 1200 m. Arazi sınırı (150–212 m) sis tarafından gizlenmiyor → kamera yükseldiğinde keskin bir kenar ve altında boş gökyüzü görünür. Kameranın ve pilot modundaki drone'un **hiçbir dünya sınırı yok**. | `EnvironmentBuilder.cs:22, 205-208, 224`, `CameraRig.cs:200-233` | Sis yoğunluğunu ~0.006–0.008'e çıkar (kozmetik) veya araziyi büyüt; opsiyonel olarak serbest kamerayı ±200 m kutusuna sıkıştır. |
| **B-16** | **düşük** | Draw call yükü: `ScatterProps` her ağaç için **rastgele renkli** bir yaprak materyali, her bina için rastgele bir duvar materyali üretiyor. `MaterialLibrary` renk anahtarına göre önbelleklediği için bunlar birleşmiyor → ~220 benzersiz yaprak materyali, 660 ağaç renderer'ı; `enableInstancing` hiçbir yerde açılmıyor, çalışma zamanı nesnelerinde static batching de yok. | `EnvironmentBuilder.cs:144-149, 172-176`, `MaterialLibrary.cs:38-68` | Yaprak/duvar renklerini küçük bir palete (örn. 4–6 varyant) yuvarla ve `Create` içinde `mat.enableInstancing = true` ayarla. |
| **B-17** | **düşük** | `ApplyAtmosphere` her sahne yüklemesinde **yeni bir skybox `Material`** üretip `RenderSettings.skybox`'a atıyor; eskisi serbest bırakılmıyor. Ayrıca ışığı `FindAnyObjectByType<Light>()` ile buluyor — kullanıcının sahnesinde bir point light varsa **onu** güneşe dönüştürür (`sun.type = Directional`). URP yedek kodları da (`Universal Render Pipeline/Lit`, `_Surface`/`_Blend` yazımları) bu manifest ile hiç çalışmaz; ters yönde, bir gün URP'ye geçilirse `RenderSettings.fog`/`Skybox/Procedural` davranışı ve `Shader.Find("Standard")` **sessizce** bozulur. | `EnvironmentBuilder.cs:185-225`, `SimulationBootstrap.cs:76-86` | Skybox materyalini statik olarak bir kez üret; ışığı `light.type == LightType.Directional` filtresiyle seç. Çalışma zamanı `Shader.Find` bağımlılıklarını (`Standard`, `Sprites/Default`, `Unlit/Color`) Graphics Settings → Always Included Shaders'a ekle, aksi hâlde bir player build'inde materyaller macenta düşer. |
| **B-18** | **düşük** | `Hud.OnGUI` kare başına en az iki kez (Layout + Repaint) çalışıp ~19 string interpolasyonu üretiyor; `HudTheme.Fill/Border/Bar` her çağrıda `GUI.DrawTexture` yapıyor. Görünür bir kare kaybı yok ama sürekli GC baskısı var. | `Hud.cs:85-108` | Değişmeyen metinleri (görev adı, kontrol şeridi) önbellekle veya yalnız `Event.current.type == EventType.Repaint` içinde çiz. |
| **B-19** | **düşük** | `TurretVisual`, `Radar` tabağını `Space.Self` üzerinde Y ekseninde döndürüyor; tabağın local euler'ı `(60, 0, 0)` olduğu için dönüş ekseni eğik → tabak taramak yerine yalpalıyor. | `TurretVisual.cs:44`, `VehicleModelBuilder.cs:113` | Tabağı ölçeksiz bir pivot altına al (tüpler/namlular için zaten yapılan desen) ve pivotu döndür. |

### Kontrol edilip **sorun bulunmayan** noktalar

- **Dalgalar arası sızıntı yok:** ölen her birim `Targetable.OnDestroy` ile kayıttan düşüyor;
  `TargetRegistry.Prune()` ve `GuidedMunition.Prune()` ölü sarmalayıcıları temizliyor.
  `ScorchMark` 40, `VfxLibrary` 220 canlı efektle sınırlı; `VfxTicker.OnDestroy` bütçeyi bırakıyor.
- **Model parçalarının collider'ları** gerçekten siliniyor (`VehicleModelBuilder.Part`,
  `EnvironmentBuilder.Prop`, `VfxLibrary.StripCollider`) — yalnız **kök** collider'lar duruyor (B-05).
- **`BankingVisual`/`TurretVisual`/`PropellerSpinner` kök transform'a dokunmuyor**, yalnız adlı
  çocukları döndürüyor; uçuş modeliyle çakışma yok.
- **Devriye rotaları araziden çıkmıyor** (maks. yarıçap ≈ 48 m, arazi 150 m).
- **Prop'lar spawn noktalarını nadiren etkiliyor:** prop keep-out 55 m, yer düşmanı spawn kutusu
  ±40 m → yalnız kutunun köşelerinde (r ≈ 55–56.6) çakışma penceresi var.
- **`CameraRig._instance`** `OnDestroy`'da temizleniyor; sahne yeniden yüklemesinde ölü referans kalmıyor.
