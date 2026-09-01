# İHA/SİHA Simülasyonu — Geliştirme Günlüğü (DEVLOG)

> Bu dosya, projede yapılan tüm işleri ve kararları kaydeder; oturumlar arası bağlamı (context) korumak ve projeyi hızlı anlamak içindir.

**Son güncelleme:** 2026-09-01
**Branch:** `claude/slack-session-f6uh9g`
**Unity sürümü:** 6000.5.9f1

---

## Devam noktası (buradan devam et)

> **Bu bölüm bir devir-teslim notudur.** Sohbet geçmişi olmayan yeni bir oturum projeye buradan
> devam edebilir. Ayrıntılar aşağıdaki bölümlerde; burada tekrar edilmez.

### Nerede kaldık
Simülasyon **oynanabilir ve şimdilik özellik olarak tamam**: dalga tabanlı senaryolar (dört görev),
pilot modu, top/füze/karşı tedbirler ve elektronik harp, görsel yenileme ve tasarım mockup'ından
uygulanmış HUD hazır. Son yapılan iş **çalışma zamanı sahnesi analizi** (`docs/SCENE.md`) ve oradan
çıkan iki bulgunun düzeltilmesi oldu: **B-01** (yerinde yeniden başlatma) ve **B-03** (skorun görev
boyunca birikmesi). Bu turda `docs/SCENE.md`'nin kalan yüksek/orta öncelikli bulguları kapatıldı:
**B-08** (rastgelelik), **B-04** (materyal sızıntısı + izli mermi bütçesi), **B-02** (radar taraması),
**B-07/B-09** (spawn yerleşimi), **B-05** (kullanılmayan collider'lar), **B-06** (kare başına tahsis).
Son tur pilot modunu "uçağın içinde" hissettirmeye ayrıldı: `RadarScope` Core projeksiyonu (testli),
kaçınılabilir hava savunması füzeleri (SAM 85 / AAA 95), **V** ile açılıp kapanan kokpit kamerası ve
pilot HUD'ında gelen füzeleri de gösteren dairesel radar skopu. En son tur iki kullanıcı isteğini
karşıladı: atış imleci artık ekran ortasına çakılı değil — `GunPipper` (Core, testli) ile namlu
ekseninden dünyaya yansıtılıyor, uçak eğilince kayıyor/yatıyor ve ~1.7× büyütüldü; ayrıca **G**
sağdaki filo durumu panelini aç/kapat yapıyor. En son tur **üç uçulabilir arketip** geldi: görev
seçim ekranında Savaş Uçağı / SİHA / Keşif İHA seçiliyor (`AircraftProfile` + `AircraftCatalog`,
Core, testli) ve seçim yalnızca **oyuncunun uçağına** uygulanıyor; SİHA profili bugünkü değerlerin
birebir aynısı olduğu için selektöre dokunmayan oyuncu için hiçbir şey değişmiyor. En son tur
**kozmetik** oldu: üç arketip artık kendi gövdesini kullanıyor — savaş uçağı için gerçek bir
`BuildFighterJet` gövdesi (alan kuralına göndermeli beş segmentli gövde, saydam kabin, kırık delta
kanat, çift dikey stabilizatör, yan hava alıkları, lüle + `"EngineGlow"`, dört pilon, karın sensörü)
ve aynı detay seviyesine çekilmiş keşif İHA / SİHA gövdeleri. Hiçbir oyun değeri değişmedi.
En son tur yine **kozmetik**: dünya detayı. Hava savunması artık **Patriot tipi bir batarya**
(M901 rampa treyleri + yükselen 2×2 kanister rampası, faz dizisi radar treyleri, atış kontrol
sığınağı — 37 parça), AAA ise ayrı kalsın diye detaylandırılmış bir **top kundağı** (14 parça).
Dağılımdaki binalar tek kutu yerine **üç çok katlı arketip** (depo / blok / kule), ağaçlar ise
**üç tür** (kozalaklı / geniş yapraklı / çalı) oldu; yaprak ve duvar renkleri kuantalanmış palete
alındı. `docs/SCENE.md` bulgusu **B-19** kapandı, **B-16**'nın palet yarısı yapıldı. Hiçbir oyun
değeri değişmedi.

### Yeni oturum için okuma sırası
1. Bu bölüm.
2. `CLAUDE.md` → `## Worker kuralları`.
3. `docs/SCENE.md` → `## 5. Bulgular` tablosu (B-01…B-19).
4. Bu dosyanın devamı: `## 3. Sistemler`, `## 7. Mevcut durum & bilinen sınırlar`.

**Sohbet geçmişine ihtiyaç yoktur** — gereken bağlamın tamamı bu iki belgededir.

### Ortam gerçekleri
- **Derleme yok.** Web ortamında `dotnet`/Unity kurulu değildir; kod burada derlenemez ve
  çalıştırılamaz. Doğruluk dikkatli okumayla sağlanır; EditMode testlerini Editor'de **kullanıcı**
  çalıştırır.
- **Depoda `.unity` sahne varlığı yok.** Sahne çalışma zamanında `SimulationBootstrap.Awake` içinde
  kurulur. Kullanıcının tek seferlik kurulumu: boş sahne → boş GameObject → `SimulationBootstrap`
  ekle → Play.
- **Sahne gerçek bir sahne varlığı olarak kaydedilmeli** (ör. `Assets/Main.unity`): kaydedilmemiş
  "Untitled" sahne `SimulationBootstrap` nesnesini kaybeder ve Play tuşuna basıldığında hiçbir şey
  olmaz.
- **Build Settings sahne listesi boş** olduğu için yeniden başlatma `SceneManager.LoadScene`
  **kullanamaz**; `SimulationBootstrap.Rebuild()` ile `Simulation` kökü yıkılıp yeniden kurulur.
- **Render hattı Built-in**, URP değil. Koddaki URP shader yedekleri ölü koddur.
- Claude'un kullanabildiği **Unity eklentisi yalnızca dokümantasyon/skill paketidir**: derleyici,
  test koşucusu, Console okuyucu veya sahne köprüsü sağlamaz.
- **`glTFast` bir bağımlılık değildir.** `Packages/manifest.json` veya `Library/PackageCache`
  içinde görünürse yerel olarak eklenmiştir ve Built-in hatta derlemeyi bozar
  (`LitMaterialExport` bulunamaz). GLB içe aktarımı bilinçli olarak kurulmuyorsa kaldır.

### Açık işler (öncelik sırasıyla)
| # | Bulgu | İş |
|---|---|---|
| 1 | **B-10…B-18** | `docs/SCENE.md`'deki kalan **düşük** öncelikli bulgular (ölü `ApplyColor`, üs konumu ile `BasePosition` uyumsuzluğu, ölü alanlar, waypoint GameObject'leri, sis/dünya sınırı, skybox materyali, HUD string'leri). **B-19 kapandı**; **B-16** kısmen kapandı — palet yapıldı, `MaterialLibrary.Create` içinde `enableInstancing` hâlâ açık iş. |
| 2 | — | **Bilinen küçük hata:** görev kazanıldıktan sonra `MissionState.ElapsedTime` saymaya devam ediyor (director'ın örneği bilerek hiç bitmiyor); debrief saati director tarafında durdurulmalı. |
| 3 | — | **Henüz karar verilmedi:** gerçek 3D modeller (render hattı kararı + çalışan bir glTF içe aktarıcı gerekir), ses, zorluk seviyeleri. |

> **B-01…B-09 kapandı.** Yüksek/orta öncelikli bulguların tamamı çözüldü; ayrıntı için
> `docs/SCENE.md` → `## 5. Bulgular` (✅ işaretli satırlar).

### Çalışma yöntemi
- Küçük dilimler hâlinde çalış; her dilim kendi başına derlenebilir olmalı.
- Her dilimden sonra commit + push et ve bu DEVLOG'u aynı turda güncelle.
- Not: geçmişte büyük bir refactor, yarım uygulanan bir dilim derlenmediği için **geri alınmak
  zorunda kaldı** — bu yüzden dilimler küçük tutulur.

---

## 1. Amaç
Unity 6 ile masaüstü, 3D, **eğitim/oyun amaçlı** İHA (keşif) ve SİHA (silahlı) simülasyonu.
Sensör, elektronik harp (EW), balistik ve güdüm sistemleri **ders kitabı fiziğine dayalı, soyutlanmış** modellerle
gerçekçi *hissettirir*. Bu bir DCS/War Thunder tarzı **oyun simülasyonudur** — gerçek donanıma bağlı operasyonel bir
sistem değildir.

Geliştirme ilkesi: **test-driven**. Tüm oyun mantığı `Sim.Core` içinde saf C# olarak yazılır ve EditMode unit
testleriyle doğrulanır; MonoBehaviour'lar ince kalır (sadece Core'u sahneye bağlar).

---

## 2. Mimari
| Katman | Klasör | İçerik |
|---|---|---|
| **Sim.Core** | `Assets/Scripts/Core` | Saf C# iş/oyun mantığı. MonoBehaviour yok, sahne bağımlılığı yok. Tam test edilebilir. |
| **Sim.Runtime** | `Assets/Scripts/Runtime` | İnce MonoBehaviour'lar — Core mantığını 3D sahneye bağlar. |
| **Sim.Tests.EditMode** | `Assets/Tests/EditMode` | NUnit EditMode unit testleri. |

Her katman kendi `.asmdef` dosyasına sahiptir: `Sim.Core`, `Sim.Runtime` (→ Sim.Core), `Sim.Tests.EditMode` (→ Sim.Core + NUnit).

---

## 3. Sistemler

### Sim.Core (saf mantık, testli)
| Sistem | Görevi |
|---|---|
| `FlightModel` | Deterministik kinematik uçuş (hız, dönüş hızı sınırı, ivme). |
| `WaypointNavigator` | Sıralı waypoint takibi, varışta ilerleme, döngü. |
| `TargetingSystem` | Menzil + görüş açısı (FOV) içinde tespit, zaman tabanlı kilitlenme. |
| `DetectableTarget` | Hedef anlık görüntüsü (id, konum, hız). |
| `WeaponSystem` | Atış kontrolü: mühimmat, atış hızı, soğuma, yeniden doldurma. |
| `Ballistics` | Hareketli hedefe önleme (lead) noktası. |
| `Health` | Can havuzu, hasar, imha. |
| `Atmosphere` | İrtifaya bağlı hava yoğunluğu (ρ = ρ₀·e^(−h/H)). |
| `BallisticProjectile` | Yerçekimi + sürükleme + rüzgâr + irtifa ile mermi entegrasyonu. |
| `RadarSystem` | Radar menzil denklemi (menzil ∝ RCS^0.25), hüzme/LOS. |
| `RadarCrossSection` | Açıya bağlı radar kesit alanı (burun/yan). |
| `ElectronicWarfare` | Jamming → menzil kısalması / burn-through, ECM → kilit olasılığı. |
| `TargetTracker` | α-β filtresiyle gürültülü ölçümden konum+hız kestirimi. |
| `SeekerGimbal` | Arayıcı başlık açı + açısal hız sınırları. |
| `ProportionalNavigation` | Oransal seyrüsefer güdüm yasası (önleme). |
| `MissionState` | Görev hedefleri, kazan/kaybet, skor. |
| `MissionGrade` | Biten görevi 0..3 yıldıza çevirir (kayıp/süre cezası). |
| `TargetAllocation` | Atıcı-hedef paylaşımı (aynı hedefe boşa ateşi önler). |
| `EngagementPolicy` | Angajman durum kararı (Devriye/Angaje/Üsse Dönüş). |
| `FuelTank` | Yakıt/menzil modeli (gaz kesme oranına göre tüketim). |
| `EvasionSteering` | Tehditten kaçınma yönü (yana kırma + uzaklaşma bileşeni). |
| `WavePlan` | Dalga başına zorluk ölçekleme: her düşman tipinden kaç tane spawn edileceği (saf). |
| `ScenarioState` | Çok dalgalı senaryo ilerleyişi + kazan/kaybet (dalga temizlenince sonraki, son dalgada zafer). |
| `GunSystem` | Seri atışlı top/makineli: fişek bandı, atış hızı, etkili menzil, dağılma (saf). |
| `HitProbability` | Bir top mermisinin isabet olasılığı: menzildeki dağılma konisi yarıçapı ↔ hedef boyutu. |
| `ThrottleGovernor` | Yakıt durumuna göre kullanılabilir gaz: son %5 rezervde güç azalır, boş depoda sıfırlanır. |
| `SquadStatus` | Filo muharebe kabiliyeti: hiç drone kalmadıysa veya kalanların hepsinin yakıtı bittiyse etkisiz. |
| `CountermeasureSystem` | Flare/chaff atıcı: sınırlı hak, salvo arası soğuma, temel aldatma olasılığı. |
| `MissileThreat` | Gelen füze geometrisi: çarpışmaya kalan süre ve zamanlama+açıya bağlı aldatma şansı. |
| `EvasiveManeuver` | İsimli kaçış manevraları (Break/Dalış/Tırmanış/Makara) + irtifa duyarlı manevra seçici. |
| `ResupplyPoint` | Üste ikmal döngüsü: üs yarıçapı içinde tam süre bekleyince tamamlanır, erken ayrılınca ilerleme sıfırlanır. |
| `ScenarioLibrary` | Görev kütüphanesi: her senaryonun başlığı, brifingi, dalga sayısı ve dalga başına düşman kompozisyonu. |
| `ScenarioKind` | Seçilebilir görev tipleri: Keşif / SEAD / Hava Muharebesi / Karma Savunma. |
| `WaveComposition` | Bir dalganın düşman kompozisyonu (sabit hedef / SAM / AAA / avcı + toplam). |
| `TerrainField` | Deterministik prosedürel arazi yükseklik alanı (Perlin + üs çevresinde düz bölge); hem arazi mesh'i hem de yer birimlerinin yerleşimi bunu kullanır. |
| `RadarScope` | Dünya konumunu burun-yukarı radar skopu koordinatına (−1..1, +Y burun, +X sağ) yansıtır; irtifayı yok sayar (PPI), menzil dışını reddeder. |
| `GunPipper` | Namlu ekseninde belirli bir menzilde merminin vardığı dünya noktası (uçuş süresi = menzil/ağız hızı, düşüş = ½·g·t²); ağız hızı ≤ 0 veya menzil ≤ 0 ise hitscan gibi doğrudan namlu ekseni. |
| `AircraftKind` | Uçulabilir arketipler: Savaş uçağı (`FighterJet`) / SİHA (`Siha`) / Keşif İHA (`Iha`). |
| `AircraftProfile` | Bir arketipin değiştirilemez performans profili: hız/dönüş/pilot hız tavanı, seyir irtifası, yakıt, top (5 değer), füze (adet + menzil), tespit ve radar menzili, can + seçim ekranı için dört 0–1 gösterge puanı. Her alanın Runtime'da gerçek bir tüketicisi vardır. |
| `AircraftCatalog` | Üç profilin kataloğu: `All`, `Default` (SİHA temel değerleri), `TryGet`/`GetOrDefault` (bilinmeyen id'de hata değil varsayılan) ve klavyeyle sağa/sola dönen `Cycle`. |

### Sim.Runtime (ince MonoBehaviour'lar)
| Bileşen | Görevi |
|---|---|
| `IhaController` | Keşif İHA: uçuş + devriye + radar sensörüyle tespit; yakıt, angajman durumu, atanan hedefe gidiş ve tehdit kaçınması. |
| `SihaController` | Silahlı SİHA (IhaController'dan türer): kilitlenince güdümlü mühimmat fırlatır; mühimmat oranı. |
| `AirDefenseSite` | Düşman hava savunması: drone'ları tespit edip güdümlü mühimmat fırlatır, kendisi de imha edilebilir (SEAD). `Configure(...)` ile uzun menzilli SAM veya kısa menzilli hızlı-ateşli AAA varyantı kurulur. |
| `RadarSensor` | RadarSystem + RCS + EW + TargetTracker ile gerçekçi tespit/izleme. |
| `RcsComponent` | Hedefin açıya bağlı radar imzası. |
| `Jammer` | Gemi üstü gürültü karıştırıcı (EW menzil düşürme). |
| `GuidedMunition` | PN güdümü + arayıcı başlık + balistik ile güdümlü mühimmat, yakınlık tapası. |
| `TargetRegistry` / `Targetable` | Canlı hedeflerin kaydı; controller'lar her kare sorgular. |
| `SimulationBootstrap` | Play'de sahneyi primitive'lerden kurar (kamera, ışık, zemin, drone'lar, ScenarioController). Üretilen her şey tek bir `Simulation` kökünün altındadır; `Rebuild()` bu kökü yıkıp yeniden kurar (yerinde yeniden başlatma). |
| `ScenarioController` | Dalga tabanlı senaryo: seçilen göreve göre (`ScenarioLibrary.Composition`) her dalganın düşman karışımını spawn eder ve kazan/kaybet'i yönetir; `BeginMission()` çağrılana kadar bekler. |
| `SimulationDirector` | Görev takibi ve skorlama (dalga güvenli kill sayımı; kazan/kaybet ScenarioController'da, bu yüzden `MissionState` saf sayaç olarak kurulur ve kendi kendine bitmez). |
| `Hud` | Ekran üstü (IMGUI) bilgi paneli: görev, skor, radar temasları; pilot modunda dairesel radar skopu (temaslar + gelen füzeler). |
| `CameraRig` | Serbest uçan kamera (WASD + fare), drone takip modu ve pilot modunda kokpit görünümü (**V** ile geçiş); kokpitte `CockpitFrame`'i açar/kapatır. |
| `CockpitFrame` | Kameraya bağlı prosedürel kokpit içi (gösterge paneli, güneşlik dudağı, ön cam kirişi, A-direkleri, kanopi rayları, hafif cam tonu, önde uçağın burnu); tüm parçalar kameranın gerçek frustum'una oranla ölçeklenir, FOV/en-boy değişince yeniden yerleşir. |
| `ExplosionEffect` | Asset'siz patlama işareti: büyüyüp sönen emisyonlu küre (mühimmat isabeti + imha). |
| `GameControls` | Klavye kontrolleri: R yeniden başlat, P duraklat, +/- zaman ölçeği. |
| `GunTurret` | `GunSystem` + `HitProbability` sarmalayıcısı: hedefe veya serbest nişan noktasına top atışı, izli mermi. |
| `TracerEffect` | Asset'siz izli mermi görseli: `LineRenderer` ile kısa ömürlü parlak çizgi. |
| `EnemyDroneController` | Düşman avcı drone'u: uçar, dost drone'ları tespit eder ve topla taramaya alır (hava muharebesi). |
| `PlayerDroneController` | Pilot modu: oyuncu dost bir drone'u devralıp elle uçurur (C/Tab/W/S/A/D/↑↓/Space/F + Q/E/X). |
| `CountermeasureDispenser` | `CountermeasureSystem` sarmalayıcısı: flare/chaff salvosu, salvo sayacı (füzeler bunu izler), kısa görsel puf. |
| `ScenarioMenu` | Kurulum gerektirmeyen IMGUI görev seçim/brifing ekranı: açılışta çıkar, sim'i duraklatır, `M` ile tekrar açılır. |
| `MaterialLibrary` | Standard/URP uyumlu, önbelleklenmiş materyal fabrikası (renk, metalik, pürüzsüzlük, emisyon). |
| `VehicleModelBuilder` | Primitive'lerden araç siluetleri kurar (keşif İHA 19, SİHA 27, savaş uçağı 37, **Patriot tipi SAM bataryası 37**, **AAA topu 14** parça; ayrıca düşman avcısı, yer hedefi); parçaların collider'ları silinir, fizik etkilenmez. `"Model"` çocuğu kökün ölçeğini tersler; `"Fuselage"`/`"Propeller"`/`"Radar"`/`"EngineGlow"`/`"Turret"`+`"TurretBody"` adları animasyon ve kamera kodunun sözleşmesidir. Açılı alt gruplar (SAM rampası, faz dizisi paneli) ölçeksiz **döndürülmüş pivot** üzerinde durur. |
| `EnvironmentBuilder` | Prosedürel arazi mesh'i, üs pisti, gökyüzü/sis/ortam ışığı/güneş ayarı ve prop dağılımı (tamamı görsel, hiçbirinde collider yok). Dağılımda **üç ağaç türü** (kozalaklı 5 / geniş yapraklı 6 / çalı 3 parça) ve **üç bina arketipi** (depo 9 / orta katlı blok 9–11 / kule 11 parça); boy, taç yarıçapı, eğim, arketip ve renk seçimi hep aynı **sabit tohumlu** akıştan çekilir. Yaprak/duvar renkleri 6 + 5 tonluk **kuantalanmış palete** bağlıdır (materyal sayısı ~240 → 17). |
| `VfxLibrary` | Asset'siz efekt primitifleri (emisyonlu parlama, nokta ışığı patlaması, enkaz, saydam duman, şok dalgası halkası, kıvılcım); global efekt bütçesi ile sınırlı, hepsi kendini yok eder. |
| `ScorchMark` | İmha edilen yer birimlerinin arazide bıraktığı, zamanla sönen yanık izi (en fazla 40 iz). |
| `DamageVisuals` | Can oranı düşen birimlerde duman, kritik seviyede alev efekti (yalnızca `Health` okur). |
| `PropellerSpinner` | "Propeller" parçasını hıza bağlı olarak kendi Z ekseninde döndürür. |
| `BankingVisual` | Dönüşlerde yalnızca "Model" çocuğunu yatırır (kök transform'a asla dokunmaz). |
| `TurretVisual` | SAM/AAA'da "Turret" hedefi takip eder, "Radar" tabağı sürekli döner (yalnız çocuk transform'lar). |

### Testler (Sim.Tests.EditMode)
Her Core sistemi için bir test dosyası. Toplam ~18 test dosyası. Çalıştırma:
`Window > General > Test Runner > EditMode > Run All`.

---

## 4. Nasıl çalıştırılır
1. Doğru klasörde: `git pull origin claude/slack-session-f6uh9g`
2. Unity Hub → 6000.5.9f1 ile projeyi aç (Package Manager güncelleme sorarsa kabul et).
3. Boş sahnede boş bir GameObject'e `SimulationBootstrap` bileşenini ekle → sahneyi **gerçek bir
   sahne varlığı olarak kaydet** (ör. `Assets/Main.unity`; kaydedilmemiş "Untitled" sahne bu nesneyi
   kaybeder ve Play hiçbir şey yapmaz) → **Play**.
4. Sahne kendini kurar ve **görev seçim menüsü** açılır (sim duraklatılmış olarak bekler).
   Görev kartlarının altında **uçak seçimi** vardır (Savaş Uçağı / SİHA / Keşif İHA): fareyle tıkla
   ya da **←/→** ile değiştir. Bir görev seç → görev başlar (uçak seçimi o anda sahaya uygulanır).
   HUD sol üstte görev/skor/temasları gösterir. **M** menüye döner.

**Kamera kontrolleri:** Sağ tık basılı + fare = bak · WASD = uç · Shift = hızlan · Tab = drone takip et · F = serbest mod.
Pilot modunda (**C**): kamera varsayılan olarak **kokpite** oturur, **V** kokpit ↔ takip kamerası arasında geçiş yapar.
**HUD:** **G** sağdaki filo durumu panelini gizler/gösterir (varsayılan: açık).

---

## 5. Kilometre taşları (yol haritası)
- **M0** — Test-driven Core iskeleti + ince Runtime + Unity projesi. ✅
- **Fizik/EW katmanı** — Atmosphere, BallisticProjectile, Radar, RCS, EW, Tracker, SeekerGimbal, PN. ✅
- **Unity 6'ya geçiş** — 6000.5.9f1, obsolete API düzeltmeleri. ✅
- **M1 — Taktik katman** — MissionState, TargetAllocation, EngagementPolicy, FuelTank + HUD + serbest kamera. ✅
  Taktik beyin artık davranışa bağlı: SimulationDirector hedef paylaşımını controller'lara uyguluyor, bingo yakıt/mühimmat'ta RTB, drone durumları HUD'da. ✅
- **M2 — Çift taraflı tehdit** — `AirDefenseSite` (radar+füzeli hava savunması), `EvasionSteering` ile drone kaçınması, SEAD (hava savunması da imha edilebilir). ✅
- **M3** — ScriptableObject veri-güdümlü ayarlar + araç/silah çeşitliliği. ⏳
- **M4** — Oyuncu komuta katmanı (drone seç, waypoint/angaje/RTB) + taktik harita. ⏳
- **M5** — Sunum: 3D modeller, iz/patlama efektleri, ses, cilalı UI, mini harita. ⏳

---

## 6. Commit geçmişi
| Commit | Açıklama |
|---|---|
| `1fe24ab` | İHA/SİHA Unity simülasyon iskeleti (test-driven Core). |
| `8390e13` | Gerçekçi fizik ve elektronik harp katmanı. |
| `e67f472` | Projeyi Unity 6'ya (6000.5.9f1) taşıma. |
| `ce5c27f` | Unity 6 obsolete API düzeltmeleri (GetInstanceID, FindObjectOfType). |
| `d228e75` | M1 taktik katman + HUD + serbest kamera. |
| `bu tur (önceki)` | Taktik beyni davranışa bağlama + çift taraflı hava savunması muharebesi. |
| `bu tur (önceki)` | Muharebe cilası: patlama efektleri + mühimmat izleri, temiz mühimmat hasarı (reflection kaldırıldı), R/P/+- kontrolleri, yıldız derecelendirmeli bitiş ekranı, denge ayarı. |
| `bu tur (önceki)` | Hata düzeltmesi: drone'ların zemin altına dalması engellendi (yalnız SİHA hedef paylaşımı, irtifa koruyan güdüm, minimum irtifa tabanı). |
| `bu tur (önceki)` | Dalga tabanlı senaryo sistemi + düşman çeşitliliği (Sabit hedef / SAM / AAA); ScenarioController dalgaları spawn eder ve kazan/kaybet'i yönetir; HUD dalga göstergesi. |
| `bu tur (1/2)` | Top/makineli sistemi + izli mermi + düşman avcı drone'ları (hava muharebesi). |
| `bu tur (2/2)` | Oynanabilir pilot modu: dost drone'lardan birini elle uçur (C/Tab/W/S/A/D/↑↓/Space/F). |
| `bu tur (1/2)` | Yakıt artık gerçekten bitiyor: motor durur, drone süzülerek alçalır ve yere çakılır. |
| `bu tur (2/2)` | Füze kaçınması: flare/chaff karşı tedbirleri, kaçış manevraları ve pilot yetenekleri (Q/E/X). |
| `bu tur (1/2)` | Üste ikmal: drone'lar üste bekleyince yakıt, mühimmat ve flare ikmali alıp göreve dönüyor. |
| `bu tur (2/2)` | Senaryo kütüphanesi + görev seçim menüsü (Keşif / SEAD / Hava Muharebesi / Karma Savunma). |
| `bu tur (1/2)` | Görsel yenileme (1/2): araç siluetleri, prosedürel arazi ve atmosfer. |
| `bu tur (2/2a)` | Görsel yenileme (2/2a): katmanlı patlama efekti, namlu ateşi/kıvılcımlar, füze egzoz izi, hasar dumanı ve yanık izleri. |
| `bu tur (2/2b)` | Görsel yenileme (2/2b): dönen pervaneler, gövde yatışı, taret takibi, kamera hissi ve çubuk göstergeli HUD. |
| `bu tur` | HUD tasarım mockup'ı: dört artboard'lı tasarım tuvali (`docs/design/hud/`), askerî gösterge estetiği. |
| `bu tur (1/2)` | Düzeltme: taret namluları/füze tüpleri artık ölçeksiz taret pivotunun çocuğu, taretle birlikte dönüyor. |
| `c1ce582` | HUD tasarım mockup'ına göre yeniden stillendirildi (`HudTheme` + `Hud`). |
| `98213d4` | CLAUDE.md'ye kalıcı worker kuralları eklendi (önce DEVLOG oku, derleme yok, Unity 6 API, null kontrolü, küçük commit'ler). |
| `bu tur` | Görev seçim menüsü HUD tasarımına uygun hâle getirildi (`ScenarioMenu` artık `HudTheme` kullanıyor). |
| `bu tur` | Çalışma zamanı sahnesi analizi: `docs/SCENE.md` (hiyerarşi, bileşen envanteri, yerleşim, yaşam döngüsü, 19 bulgu). |
| `e62b9b0` | `EnvironmentBuilder.BuildAirbase`/`ScatterProps` artık ürettikleri kökü döndürüyor. |
| `110ab11` | Üretilen sahne tek bir `Simulation` kökü altına taşındı; `Build()` ayrıldı, statik `Instance`/`Root` eklendi. |
| `88e787b` | Dalga düşmanları da `Simulation` kökünün altında spawn ediliyor. |
| `ffbdadc` | **B-01:** yeniden başlatma artık sahne yüklemiyor, `SimulationBootstrap.Rebuild()` ile yerinde yeniden kuruluyor. |
| `7a0921b` | **B-03:** görev skoru artık tüm görev boyunca birikiyor (`MissionState` saf sayaç). |
| `bu tur` | DEVLOG'a devir-teslim bölümü: sıfırdan başlayan bir oturum sohbet geçmişi olmadan devam edebilir. |
| `535227b` | **B-08:** prop dağıtımından sonra global `Random.state` geri yükleniyor. |
| `64a0a9e` | **B-04:** efekt materyalleri yok ediliyor (paylaşımlı izli mermi materyali) + izli mermiler VFX bütçesine tabi. |
| `44abd2b` | **B-02:** radar adayları kare başına tek geçişte çözülüyor, `RcsComponent`/`Jammer` önbellekli. |
| `42c1ee9` | **B-07 + B-09:** düşman spawn'ları üssün dışında ve birbirinden ayrık; avcılar farklı irtifa/loiter yörüngesi alıyor. |
| `82bee03` | **B-05:** spawn edilen nesnelerden kullanılmayan collider'lar kaldırıldı. |
| `1f3cea1` | **B-06:** kare başına snapshot tahsisleri yerine yeniden kullanılan tamponlar (+ DEVLOG/SCENE güncellemesi). |
| `2485d38` | `RadarScope` Core projeksiyonu + EditMode testleri (burun-yukarı PPI). |
| `60dd15f` | Hava savunması füzeleri yavaşlatıldı (SAM 150→85, AAA 130→95) — kaçınılabilir hâle geldi. |
| `76202c2` | Pilot modunda kokpit görünümü; **V** ile kokpit/takip kamerası arasında geçiş. |
| `bu tur` | Pilot HUD'ına radar skopu: temaslar + gelen füzeler (+ DEVLOG güncellemesi). |
| `fa8d6fa` | Prosedürel kokpit içi (`CockpitFrame`): frustum oranlı gösterge paneli, kiriş, A-direkleri, raylar, cam tonu ve burun. |
| `bu tur` | `CockpitFrame` kokpit görünümüne bağlandı (pilotluk sürerken görünür, burun uçağın rengini alır) + DEVLOG. |
| `1ac88d2` | `GunPipper` Core nişan noktası geometrisi + EditMode testleri. |
| `d5d9a55` | Namlu ekseninden yansıtılan büyütülmüş atış imleci + **G** ile filo panelini aç/kapat. |
| `b0a9179` | `AircraftProfile`/`AircraftKind`/`AircraftCatalog` Core verisi + ilişki tabanlı EditMode testleri. |
| `de63482` | Seçilen uçak `ScenarioController.SelectedAircraftId` statiğinde taşınıyor (bilinmeyen id → varsayılan). |
| `03fd331` | Görev seçim ekranına uçak seçim satırı (kartlar + 0–1 çubuk göstergeleri, ←/→). |
| `9d6a8f7` | Seçilen profil oyuncunun uçağına uygulanıyor (uçuş, yakıt, top, füze, sensör, can). |
| `590236f` | Üç uçulabilir arketip için DEVLOG maddesi. |
| `4a612c6` | Gerçek savaş uçağı gövdesi (`BuildFighterJet`) + `Accent`/`Glow`/`CanopyGlass` materyal yardımcıları. |
| `16ace38` | `SpawnPlayerAircraft` jet için gerçek gövdeyi kuruyor; ödünç silüet notu kaldırıldı. |
| `1f56d83` | Keşif İHA ve SİHA gövdeleri detaylandırıldı (bağımsız SİHA gövdesi, ters V-kuyruk, satcom kubbesi, açık livre). |
| `a1abae6` | Üç detaylı uçak gövdesi için DEVLOG maddesi. |
| `eac02eb` | Patriot tipi SAM bataryası (M901 rampa treyleri + faz dizisi radar treyleri + atış kontrol sığınağı) ve detaylandırılmış AAA top kundağı. |
| `c669709` | Çok katlı bina arketipleri ve üç ağaç türü; yaprak/duvar renkleri kuantalanmış palete alındı (B-16'nın palet yarısı). |

---

## 7. Mevcut durum & bilinen sınırlar
- **Görsel yenileme tamamlandı (1/2 + 2/2):** görseller hâlâ primitive'lerden inşa ediliyor ama artık
  araç siluetleri, prosedürel arazi/atmosfer, katmanlı muharebe efektleri, hareketli parçalar
  (pervane/taret/radar), kamera hissi ve çubuk göstergeli HUD ile birlikte. İçe aktarılmış gerçek
  3D modeller/sesler hâlâ M5'e bırakıldı.
- **Taktik beyin artık davranışa bağlı:** `IhaController`/`SihaController` public kancalar sunuyor
  (`AssignedTargetId`, `State`, `FuelFraction`, `AmmoFraction`, `BasePosition`, `SetThreat`). `SimulationDirector`
  her kare `TargetAllocation` ile hostile'ları drone'lara paylaştırıyor (aynı hedefe boşa gidiş yok); `EngagementPolicy`
  bingo yakıt/mühimmat'ta drone'u üsse döndürüyor; tehdit altında `EvasionSteering` ile yana kırıyor.
- **Muharebe çift taraflı:** `AirDefenseSite` drone'ları tespit edip güdümlü mühimmat fırlatıyor ve
  kendisi de imha edilebilir hedef (SEAD).
- **Dalga tabanlı senaryo + düşman çeşitliliği:** Bootstrap artık sabit düşman kurmuyor; bunun yerine bir
  `ScenarioController` üç düşman tipini (Sabit hedef / uzun menzilli SAM / kısa menzilli hızlı-ateşli AAA)
  `WavePlan`'e göre artan dalgalar hâlinde spawn ediyor. **Kazan/kaybet artık ScenarioController'ın
  (`ScenarioState`)** sorumluluğunda (son dalga temizlenince zafer, tüm dostlar kaybedilince bozgun);
  `MissionState` yalnızca **skor** takipçisi olarak kalıyor. `SimulationDirector`'ın kill sayımı dalga
  güvenli (yeni dalga düşman sayısını artırınca negatif kill kaydedilmez, baz değer koşulsuz güncellenir).
- **Oynanabilir:** Artık sadece izlenen değil, **oynanan** bir simülasyon. `PlayerDroneController` ile
  dost drone'lardan biri devralınıp elle uçurulabiliyor (pilot modu: C/Tab/W/S/A/D/↑↓/Space/F);
  kamera piloted drone'u takip ediyor, HUD "PİLOT MODU" paneli + nişangâh gösteriyor. Kontrol
  bırakılınca drone'un YZ'si `SyncFlightTo` sayesinde bırakıldığı konum/yön/hızdan devam ediyor.
- **Hava muharebesi:** Dost İHA/SİHA'lar ve düşman avcı drone'ları `GunTurret` ile birbirine top
  atıyor; isabet `HitProbability` ile menzil, dağılma ve hedef boyutundan olasılıksal hesaplanıyor.
- **Yakıt bağlayıcı bir kaynak:** Gaz artık `ThrottleGovernor` ile yakıta göre sınırlanıyor; depo bitince
  motor durur, drone süzülerek alçalır ve yere çakılır (pilot modunda da). Bozgun koşulu `SquadStatus`
  ile filo bazlı: hiç drone kalmadıysa **veya** kalanların hepsinin yakıtı bittiyse görev başarısız.
- **Füze kaçınması:** Gelen güdümlü mühimmatlar `GuidedMunition.Active` üzerinden görülüyor; drone'lar
  `MissileIncoming`/`TimeToImpact` üretiyor, `CountermeasureDispenser` ile flare/chaff atıyor
  (etkisi zamanlama + açıya bağlı, `MissileThreat`) ve `EvasiveManeuver` ile kaçış manevrası yapıyor.
  Oyuncu aynı araçlara **Q** (flare), **E** (art yakıcı) ve **X** (otomatik kaçış) ile erişiyor.
- **Üste ikmal:** Üsse dönen drone `baseRadius` içinde `serviceSeconds` kadar bekleyince yakıt, top,
  füze ve flare ikmali alıp göreve dönüyor (`ResupplyPoint` + `IhaController.Resupply()`); servis
  sırasında üssün üzerinde yavaş tur atıyor. Mühimmatı biten filo yüzünden dalganın kilitlenmesi
  sorunu böylece ortadan kalktı.
- **Görev seçimi:** Artık tek bir sabit senaryo yok. `ScenarioLibrary` dört görev tanımlıyor
  (Keşif / SEAD / Hava Muharebesi / Karma Savunma), her biri kendi dalga sayısı ve dalga başına
  düşman kompozisyonuyla; `ScenarioMenu` açılışta brifing ekranı olarak çıkıyor ve **M** ile
  görev sırasında yeniden açılıyor (seçim, üretilen dünyayı `SimulationBootstrap.Rebuild()` ile
  yerinde yeniden kurarak sahayı temizliyor). Seçilen görev statik
  `ScenarioController.SelectedKind`'de tutulduğu için R/menü yeniden kurulumlarından sağ çıkıyor.
- **Skor:** `imha×100 − kayıp×150`. (Önceki sürümdeki saniyelik zaman cezası kaldırıldı; geçen süre HUD'da ayrı
  gösterilir.)

---

## 8. Sıradaki adımlar
1. Controller'lara public kancalar → `SimulationDirector` hedef paylaşımını uygular, yakıt/mühimmat bitince RTB,
   drone durumları HUD'da (Devriye/Angaje/Dönüş).
2. **M2:** Hedeflerden birini radar+füzeli hava savunması yap; drone'lara ateş etsin → gerçek çift taraflı muharebe.
3. Kaçınma manevraları ve SEAD (hava savunması bastırma).

---

## 9. Değişiklik günlüğü (kronolojik)
- **İskelet:** Sim.Core (FlightModel, WaypointNavigator, TargetingSystem, WeaponSystem, Ballistics, Health) +
  ince Runtime controller'lar + EditMode testleri + Unity proje iskeleti.
- **Fizik/EW:** Atmosphere, BallisticProjectile, RadarSystem, RadarCrossSection, ElectronicWarfare, TargetTracker,
  SeekerGimbal, ProportionalNavigation + testleri; RadarSensor/RcsComponent/Jammer/GuidedMunition Runtime bileşenleri;
  SİHA güdümlü mühimmat fırlatır hale geldi.
- **Unity 6:** ProjectVersion ve paket manifesti 6000.5.9f1'e güncellendi; `GetInstanceID` → `Targetable.Id`,
  `FindObjectOfType` → `FindAnyObjectByType` (Safe Mode derleme hataları giderildi).
- **M1:** MissionState, TargetAllocation, EngagementPolicy, FuelTank + testleri; SimulationDirector (skor/görev),
  Hud (IMGUI), CameraRig (serbest/takip kamera); SimulationBootstrap'a bağlandı.
- **Skor düzeltmesi:** Skordan saniyelik zaman cezası kaldırıldı; süre ayrı gösteriliyor. DEVLOG eklendi.
- **Taktik beyin + çift taraflı muharebe:** `IhaController`/`SihaController`'a `FuelTank`+`FuelFraction`,
  `AmmoFraction`, `EngagementPolicy` üzerinden `State`, `AssignedTargetId`, `BasePosition` ve `SetThreat`
  tehdit-kaçınma kancaları eklendi; yön seçimi RTB (bingo yakıt/mühimmat), atanan hedef ve kaçınmayı gözetiyor.
  `SimulationDirector` `TargetAllocation` ile hedef paylaşımını controller'lara uyguluyor ve HUD için `Friendlies`
  listesini sunuyor. Yeni `AirDefenseSite` (SAM) çift taraflı muharebe getiriyor; `EvasionSteering` Core + EditMode
  testi eklendi; HUD drone başına durum/yakıt/mühimmat gösteriyor; Bootstrap SAM sahaları kuruyor.
- **Muharebe cilası:** `MissionGrade` (0..3 yıldız) Core + EditMode testi eklendi. `GuidedMunition` artık
  temiz `Launch(target, velocity, damage)` aşırı yüklemesiyle hasar alıyor (reflection kaldırıldı),
  emisyonlu gövde + `TrailRenderer` izi kazandı ve isabette `ExplosionEffect.Spawn` ile patlıyor. Yeni
  asset'siz `ExplosionEffect` (mühimmat 3, imha 6 birim). `Targetable.TakeDamage` ölümde patlama üretiyor.
  Yeni `GameControls`: R yeniden başlat, P duraklat, +/- zaman ölçeği (0.25..4); `TargetRegistry.Clear()`
  yeniden başlatmada temiz kayıt için. `Hud` kazan/kaybet bitiş ekranı yıldız derecesi ve kontrol ipuçları
  gösteriyor. Hafif denge ayarı: SİHA silah menzili tespit menzili (120) ile eşitlendi.
- **Hata düzeltmesi (drone'lar zeminin altına dalıyordu):** Keşif İHA'ları ve SİHA'lar yerdeki hedefe
  burun aşağı dalıp (uçuş modelinde zemin çarpışması yok) sahanın altına geçip muharebeden çıkıyordu.
  Hedef paylaşımı yalnızca SİHA'lara yapıldı (keşif İHA'ları `AssignedTargetId == -1` ile devriyede kalır);
  güdüm irtifayı koruyacak şekilde hedefin üzerindeki seyir irtifasına yatayda yaklaşacak biçimde düzeltildi;
  hafif irtifa-tutma sapması ve sert minimum irtifa (`minAltitude`) tabanı eklendi (`_flight.Position` ve
  `transform.position` senkron kalır).
- **Dalga tabanlı senaryo + düşman çeşitliliği:** Test-driven `WavePlan` (dalga başına zorluk ölçekleme) ve
  `ScenarioState` (dalga ilerleyişi + kazan/kaybet) Core + EditMode testleri eklendi. Yeni `ScenarioController`
  üç düşman arketipini kendi başına spawn ediyor: gri **Sabit hedef** (silahsız amaç, cube), koyu-kırmızı
  uzun menzilli **SAM** ve turuncu kısa menzilli hızlı-ateşli **AAA** (ikisi de `AirDefenseSite` +
  `Configure(...)`). Her dalga temizlenince sonraki spawn olur; son dalga zafer, tüm dostlar bozgun.
  `AirDefenseSite.Configure(...)` Start'tan önce SAM/AAA varyantı için parametreleri set ediyor.
  `SimulationBootstrap` sabit `SpawnHostile`/`SpawnAirDefense` çağrıları (ve kullanılmayan metotları)
  kaldırıldı; yerine drone'lardan sonra bir `ScenarioController` kuruluyor. `SimulationDirector` kill sayımı
  dalga güvenli (yalnız skor). `Hud` dalga/düşman satırı gösteriyor ve bitiş ekranını ScenarioController.Status
  üzerinden sürüyor (ScenarioController null ise eski MissionState tabanlı ekrana düşüyor).
- **Top/makineli + hava muharebesi:** Top/makineli sistemi (isabet olasılığı menzil+dağılma+hedef
  boyutuna göre), izli mermi görseli, düşman avcı drone'ları (hava muharebesi), dalga planına avcı
  tipi eklendi. Test-driven `GunSystem` (fişek bandı/atış hızı/menzil/dağılma) ve `HitProbability`
  (dağılma konisi ↔ hedef yarıçapı) Core + EditMode testleri; `WavePlan.FightersForWave` ile dalga
  planına avcı tipi girdi (dalga-2 toplamı 7 → 8). Runtime tarafında `GunTurret` (+ asset'siz
  `TracerEffect`) İHA (`Configure(200, 8, 45, 3, 3)`) ve SİHA'ya (`Configure(300, 10, 60, 2.5, 4.5)`)
  takıldı; yeni `EnemyDroneController` dost drone'ları avlayan düşman avcı drone'unu getiriyor
  (`ScenarioController.SpawnFighter` onları ~14 birim irtifada, havada spawn ediyor).
  `IhaController` topla taciz bloğu + ileride oyuncu kontrolü için `ManualControl` bayrağı kazandı;
  `Hud` drone başına top mühimmatını da gösteriyor.
- **Oynanabilir pilot modu:** Yeni `PlayerDroneController` (yönetici GameObject'inde, drone'un üstünde
  değil) oyuncunun dost drone'lardan birini devralıp elle uçurmasını sağlıyor: **C** pilot modunu
  aç/kapat, **Tab** drone seç, **W/S** gaz, **A/D** dönüş, **↑/↓** (veya Sol Alt + fare) yunuslama
  (±60° sınırlı), **Space** top, **F** güdümlü füze (yalnız SİHA). Kontrol açıkken `ManualControl`
  ilgili drone'un yapay zekâ güdümünü ve otomatik top atışını durduruyor; yakıt, angajman durumu,
  top/füze soğuması ve sensör/kilit çalışmaya devam ediyor. `IhaController.SyncFlightTo(...)` her kare
  transform + `FlightModel`'i (konum/yön/hız) birlikte yazıyor, böylece kontrol bırakılınca YZ tam
  bırakıldığı yerden devam ediyor; `SihaController.TryManualLaunch()` mevcut fırlatma yolunu (menzil +
  mühimmat/soğuma kontrolleri + `LaunchMunition`) pilot komutuyla yeniden kullanıyor (YZ atışı
  değişmedi). `CameraRig` pilot modunda kontrol edilen drone'u takip ediyor ve kendi girdilerini
  yok sayıyor; `Hud` "PİLOT MODU" paneli (drone, hız, irtifa, top %, füze %) ve ekran ortasında
  nişangâh çiziyor, kontrol ipuçları satırı güncellendi. `SimulationBootstrap` pilot bileşenini
  SimulationDirector nesnesine ekliyor.
- **Yakıt artık gerçekten bitiyor:** Motor durur, drone süzülerek alçalır ve yere çakılır; tüm
  drone'lar düşerse veya hepsinin yakıtı biterse görev başarısız olur (pilot modu dahil).
  Test-driven `ThrottleGovernor` (son %5 rezervde güç azalır, boş depoda sıfır) ve `SquadStatus`
  (filo etkisiz: hiç drone kalmadı ya da kalanların hepsi kuru) Core + EditMode testleri eklendi.
  `IhaController` artık uçuş modelini **yönetilen gaz** ile besliyor (`_flight.Step(dir, effThrottle, dt)`)
  ve aynı değerle yakıt yakıyor; `IsOutOfFuel` iken irtifa-tutma yerine burun aşağı (`dir.y -= 0.6f`)
  süzülüyor, her kare `sinkRatePerSecond` kadar alçalıyor ve `minAltitude`'a inince kendi
  `Targetable`'ı üzerinden `TakeDamage(99999)` ile imha oluyor (patlama + dost kayıp sayımı normal
  düşürülme gibi işliyor). Yeni `IhaController.ConsumeFuel(throttle01, dt)` kancasıyla pilot da aynı
  depoyu harcıyor (`ManualControl` açıkken YZ tarafındaki yakma atlanıyor, çift tüketim yok);
  `PlayerDroneController` ulaşılabilir hızı yakıta göre sınırlıyor, depo bitince süzülüp yere
  çakılıyor ve kontrolü bırakıyor. `ScenarioController` bozgun koşulunu `SquadStatus` ile
  değiştirdi (dost controller listesi ~1 sn'de bir tazelenir, kare başına allocation yok);
  `Hud` kuru depoyu drone satırında `[YAKIT BİTTİ]` (kırmızı) ve pilot panelinde
  `YAKIT BİTTİ - SÜZÜLÜYOR` uyarısıyla gösteriyor.
- **Füze kaçınması + özel yetenekler:** Artık gelen füzeler görülüyor, aldatılabiliyor ve onlardan
  kaçılabiliyor. Test-driven `CountermeasureSystem` (hak + soğuma), `MissileThreat` (çarpışmaya kalan
  süre; aldatma şansı erken atışta en yüksek, füze burna doğruyken yarıya iner) ve `EvasiveManeuver`
  (Break / Dalış / Tırmanış / Makara + irtifa duyarlı `Choose`) Core + EditMode testleri eklendi.
  `GuidedMunition` artık statik `Active` listesinde kayıtlı (fırlatmada eklenir, `OnDestroy`'da
  çıkarılır) ve `Target` / `Velocity` kancalarını açıyor; hedefin **yeni** salvosu için (dispenser'ın
  `SalvoCount`'u ile bir kez) `MissileThreat.DecoyChance(...)` zarı atılıyor — tutarsa kilit kopuyor
  (`_target = null`) ve mevcut ıskalama yolu füzeyi imha ediyor. Yeni `CountermeasureDispenser`
  (ince sarmalayıcı, `Configure(...)` Start'tan önce çalışabilsin diye tembel kurulum) dost
  İHA/SİHA'lara (`SimulationBootstrap`) ve düşman avcılarına (`ScenarioController.SpawnFighter`,
  daha zayıf: 6 hak / 2.5 sn / %50) takıldı. `IhaController` her kare `GuidedMunition.Active`'i
  tarayıp `MissileIncoming` + `TimeToImpact` üretiyor, tehdit başına bir kez otomatik flare atıyor
  (`TimeToImpact < 3s`) ve yön önceliği **RTB > füze kaçışı > kayıtlı tehdit > atanan hedef >
  devriye** olacak şekilde kaçış manevrası uyguluyor (yakıtsız süzülme/çakılma hepsinin üstünde).
  `EnemyDroneController` aynı savunmayı basitleştirilmiş hâliyle kullanıyor. `PlayerDroneController`
  üç yetenek kazandı: **Q** flare, **E** (basılı) art yakıcı (×1.6 hız, ×3 yakıt), **X** ~1.5 sn
  otomatik kaçış manevrası; `Hud` ekranda kırmızı `⚠ FÜZE! x.xs` uyarısı, pilot panelinde
  `flare=%NN` + `ART YAKICI`/`KAÇIŞ` göstergeleri ve drone satırlarında `flare=%NN` gösteriyor.
- **Hata düzeltmesi:** yok edilmiş nesnelere erişimden kaynaklanan NullReferenceException'lar
  giderildi (statik kayıtlarda kalan ölü referanslar, `FindById` sonuçları ve yakıt bitince çakılan
  drone). Tüm kayıt taramalarına null koruması ve ölü kayıt temizliği eklendi.
  `TargetRegistry.Prune()` ve `GuidedMunition.Prune()` ölü girdileri listeden (sondan başa doğru)
  siliyor; `GetSnapshot`/`FindById` her çağrıda önce temizlik yapıyor, `RadarSensor` çözemediği
  hedefi tamamen atlıyor. `IhaController` artık `Crashed` mandalıyla çakılma sonrası Update'i
  tamamen atlıyor (`Destroy` kare sonuna ertelendiği için Update bir kez daha girilebiliyordu) ve
  saf-mantık çekirdekleri (`FlightModel`/`WaypointNavigator`/`TargetingSystem`/`FuelTank`) `Start`
  çalışmamış olsa bile `EnsureInitialized()` ile tembel kuruluyor — aynı tembel kurulum
  `RadarSensor`, `SihaController`, `EnemyDroneController`, `AirDefenseSite` ve `RcsComponent` için
  de yapıldı. Unity'nin aşırı yüklenmiş `==` karşılaştırması korunuyor (`is null`/`ReferenceEquals`
  ve UnityEngine.Object üzerinde `?.` kullanılmıyor).
- **Üste ikmal:** üsse dönen drone belirli bir süre bekleyince yakıt, mühimmat ve flare ikmali alıp
  göreve döner; mühimmatı biten filo yüzünden dalganın kilitlenmesi sorunu çözüldü. Test-driven
  `ResupplyPoint` (üs yarıçapında tam süre bekleme; erken ayrılınca ilerleme sıfırlanır, tamamlanınca
  bir kez `true` döner) Core + EditMode testleri eklendi. `IhaController` her kare `BasePosition`'a
  olan uzaklığı `baseRadius` ile kıyaslayıp `ResupplyPoint.Tick(atBase, dt)` çeviriyor; döngü
  tamamlanınca `Resupply()` yakıtı (`FuelTank.Refuel`), top bandını (yeni `GunTurret.Reload`) ve
  flare haklarını (`CountermeasureDispenser.Reload`) dolduruyor. `SihaController.Resupply()` üstüne
  füze şarjörünü de (`WeaponSystem.Reload`) dolduruyor. İkmal **yakıta bağlı değil**: depoyu bitirip
  süzülerek üsse dönebilen drone yere çakılmadan servis alabiliyor (yalnız `Crashed` mandalı
  engelliyor). Üsse dönen drone servis yarıçapına girince devriye rotasına düşmek yerine **üssün
  üzerinde yavaş tur atıyor** (`StationKeepThrottle`): rotanın en yakın kenarı bile servis yarıçapının
  dışında kaldığı için eski davranış her turda drone'u servis alanından çıkarıp ilerlemeyi sıfırlardı.
  Servis sonrası ayrı bir durum makinesine gerek yok — `EngagementPolicy.Decide` sadece
  yakıt/mühimmat/hedef okuduğu için dolu depo + dolu şarjör `State`'i aynı karede `ReturnToBase`'den
  `Patrol`/`Engage`'e çeviriyor. `Hud` drone satırında ve pilot panelinde `[İKMAL %NN]` gösteriyor.
- **Senaryo kütüphanesi ve görev seçim menüsü:** Keşif, SEAD, Hava Muharebesi ve Karma Savunma
  görevleri; her senaryonun kendi dalga sayısı ve düşman kompozisyonu var. **M** ile menüye dönülür.
  Test-driven `ScenarioLibrary` (+ `ScenarioKind`, `WaveComposition`) Core + EditMode testleri
  eklendi: Keşif yalnız yerdeki sabit hedefler + AAA (2 dalga), SEAD yalnız SAM/AAA (3 dalga),
  Hava Muharebesi yalnız avcı drone'lar (3 dalga), Karma Savunma ise dört tipi de mevcut `WavePlan`'e
  devrederek kuruyor (4 dalga). `ScenarioController` artık `WavePlan` yerine
  `ScenarioLibrary.Composition(SelectedKind, dalga)` ile spawn ediyor; seçilen görev
  **statik** `SelectedKind`'de tutuluyor (R ile sahne yeniden yüklenince kaybolmasın diye) ve
  `Started` bayrağı `BeginMission()` çağrılana kadar `Update`'i tamamen (spawn + bozgun kontrolü
  dahil) durduruyor. Yeni `ScenarioMenu` kurulum gerektirmeyen IMGUI brifing ekranı: açılışta
  `Time.timeScale = 0` ile sim'i dondurup görevleri başlık/açıklama/dalga sayısıyla listeliyor,
  seçimde `BeginMission()` çağırıp zamanı serbest bırakıyor; görev sırasında **M** menüyü yeniden
  açıyor ve oradaki seçim `GameControls`'un R yolunu (`TargetRegistry.Clear()` + `LoadScene`)
  aynen kullanarak sahayı temizliyor (statik `_autoBegin` sayesinde yeniden yüklemeden sonra
  brifing bir daha sorulmuyor). Menü `timeScale == 0` altında çalıştığı için `Time.deltaTime`
  kullanmıyor. `SimulationBootstrap` menüyü ScenarioController'dan **sonra** ekliyor (menü onu
  bulabilsin diye); `Hud` panel başında aktif görev adını gösteriyor, menü açıkken tamamen
  gizleniyor ve kontrol ipuçlarına `M: görev menüsü` eklendi.
- **Görsel yenileme (1/2):** primitive'lerden inşa edilen gerçek araç siluetleri (İHA, SİHA, düşman
  avcısı, SAM, AAA, yer hedefi), prosedürel tepeli arazi + üs pisti + ağaç/kaya/bina dağılımı,
  gökyüzü/sis/ortam ışığı ve metalik malzemeler. Test-driven `TerrainField` (Core) yükseklik alanını
  hem `EnvironmentBuilder`'ın arazi mesh'i hem de `ScenarioController`'ın yer birimleri kullanıyor,
  böylece SAM/AAA/yer hedefleri araziye oturuyor. Modeller birimin kökü altında tek bir `"Model"`
  çocuğunda toplanıyor, her parçanın collider'ı siliniyor ve kökün mesh'i gizleniyor — **oynanış
  değerleri (menzil, hasar, irtifa, spawn konumları) değişmedi**, değişiklik tamamen kozmetik.
- **Görsel yenileme (2/2a):** katmanlı patlama efekti (ışık patlaması, ateş topu, şok dalgası,
  enkaz, duman sütunu), namlu ateşi ve isabet kıvılcımları, füze motor alevi ve egzoz izi, hasarlı
  birimlerde duman/alev, imha yerinde yanık izi, patlamada kamera sarsıntısı. Tüm efektler
  `VfxLibrary`'nin global bütçesine (220 canlı efekt) tabi, collider'sız ve ölçekli `Time.deltaTime`
  ile kendini yok ediyor — **oynanış değerleri değişmedi**, değişiklik tamamen kozmetik.
- **Görsel yenileme (2/2b):** dönen pervaneler, dönüşlerde gövde yatışı, SAM/AAA taret takibi ve
  dönen radar tabağı, yumuşatılmış kamera + patlama sarsıntısı + art yakıcıda FOV artışı, çubuk
  göstergeli ve daha okunaklı HUD. `BankingVisual`/`TurretVisual` **yalnızca çocuk transform'ları**
  döndürür (kök transform'a dokunulmaz), kamera sarsıntısı takip mantığından sonra eklenip bir
  sonraki karede geri alınır — **oynanış değerleri değişmedi**, değişiklik tamamen kozmetik.
- **HUD tasarım mockup'ı:** Oyunun hedeflenen HUD görünümü `docs/design/hud/` klasöründe dört
  artboard'lı bir tasarım tuvali olarak duruyor (Ana Muharebe HUD, Pilot Modu HUD, Görev Seçim
  Menüsü, Görev Sonu Ekranı). Görüntülemek için `docs/design/hud/iha-siha-hud.html` dosyasını bir
  tarayıcıda aç; kaydırıp yakınlaştırabilir, PNG/PDF olarak dışa aktarabilirsin. Detaylar ve palet
  için `docs/design/hud/README.md`. **Henüz Unity'ye uygulanmadı** — bu yalnızca görsel hedef.
- **Düzeltme:** SAM/AAA taretlerinde namlular ve füze tüpleri artık ölçeksiz bir taret pivotunun
  çocuğu; taret hedefe döndüğünde namlular da dönüyor. (`"Turret"` artık boş bir pivot,
  görünen silindir `"TurretBody"` olarak onun altında; pivotun ölçeği `(1,1,1)` olduğu için
  silindirin `(0.9, 0.5, 0.9)` / `(0.8, 0.4, 0.8)` ölçeği namluları ezmiyor. Kozmetik.)
- **Görev seçim menüsü yeniden stillendirildi:** `ScenarioMenu` artık eski IMGUI kutu/buton
  görünümü yerine `HudTheme`'i (muharebe HUD'uyla aynı palet, panel, ince çerçeve, tag ve çizim
  yardımcıları) kullanıyor ve `docs/design/hud/MissionSelect.dc.html` mockup'ına uyuyor: tam ekran
  koyu fon + amber köşe ayraçları, ortalanmış `İHA / SİHA TAKTİK SİMÜLASYONU` başlığı ve amber
  `GÖREV SEÇ` alt başlığı, yan yana dört görev kartı (M-01…M-04 kodu, üç bloklu zorluk göstergesi,
  büyük başlık, tek satırlık Türkçe brifing, `N DALGA` + KOLAY/ORTA/ZOR satırı, fare üzerindeyken
  amber vurgu ve `BAŞLAT` şeridi) ve altta derli toplu kontrol lejandı. **Davranış birebir aynı:**
  `Time.timeScale` yönetimi, `IsOpen`, `M` ile yeniden açma, `ScenarioController.SelectedKind`
  ataması, `BeginMission()` ve görev ortasında seçim yapıldığında sahne yeniden yükleme yolu
  değişmedi; hiçbir oyun değeri okunmuyor/yazılmıyor. Zorluk göstergesi tamamen sunum amaçlı,
  görev uzunluğundan (`ScenarioLibrary.TotalWaves`) türetiliyor. Tipografi HUD'daki gibi yaklaşık
  (font asset'i yok, IMGUI'de harf aralığı ayarı yok).
- **Çalışma zamanı sahnesi analizi (`docs/SCENE.md`):** Depoda `.unity` sahne varlığı olmadığı
  doğrulandı (sahne tamamen `SimulationBootstrap.Awake` içinde primitive'lerden kuruluyor) ve
  üretilen sahne kaynak koddan yeniden kurgulandı: kök hiyerarşi (~1050 GameObject), araç tipi
  başına `"Model"` alt ağacı ve adlı animasyon parçaları, nesne başına bileşen envanteri, uzamsal
  yerleşim (kamera/ışık, 300×300 m arazi, üs ayak izi, prop dağılımı, drone spawn/rotaları, düşman
  spawn kuralları ve irtifalar) ve yaşam döngüsü (Awake/Start sırası, dalga döngüsü, `R`/menü
  yeniden yüklemesi ve reload'dan sağ çıkan statikler). Belge ciddiyet etiketli **19 bulgu**
  içeriyor; en kritikleri: Build Settings'te kayıtlı sahne olmadığı için `LoadScene(buildIndex)`
  tabanlı yeniden başlatmanın çalışmaması (B-01), `RadarSensor`'ın kare başına O(n²) taraması
  (B-02) ve `SimulationDirector.Start`'ın artık hiç düşman sayamaması + `MissionState`'in ikinci
  dost kaybında skoru dondurması (B-03). **Yalnızca analiz — hiçbir oyun/çalışma zamanı kodu
  değiştirilmedi.**
- **Yerinde yeniden başlatma (B-01) + skor birikimi (B-03):** `SimulationBootstrap` ürettiği her
  şeyi (arazi, üs, proplar, drone'lar, waypoint'ler, `ScenarioController`, `SimulationDirector`)
  artık tek bir **`Simulation`** kök nesnesinin altına kuruyor; kurulum gövdesi `Awake`'ten
  `Build()`'e taşındı ve sınıf statik `Instance`/`Root` sunuyor. Yeni `Rebuild()` pilot kontrolünü
  bırakır, kökü devre dışı bırakıp yok eder, kökün dışında kalan güdümlü mühimmatları temizler,
  yaşamaması gereken statikleri sıfırlar (`TargetRegistry`, `GuidedMunition.Active`,
  `VfxLibrary.ResetBudget()`, `Time.timeScale = 1`) ve `Build()`'i yeniden çağırır. `Main Camera`
  ve `Directional Light` bilerek kökün dışında kalır (ışık araması artık **yönlü** ışığa bakıyor,
  patlama nokta ışıkları güneş sanılmıyor). `GameControls`'un **R** yolu ve `ScenarioMenu`'nün
  görev ortası değişimi `SceneManager.LoadScene` yerine `Rebuild()` çağırıyor — depoda Build
  Settings'e kayıtlı sahne olmadığı için o yol zaten çalışmıyordu (B-01). Seçilen görev
  (`ScenarioController.SelectedKind`) ve menünün `_autoBegin` bayrağı statik oldukları için
  yeniden kurulumdan sağ çıkar; `Rebuild()` bunlara **dokunmaz**. Ayrıca `SimulationDirector`
  artık `new MissionState(0, int.MaxValue)` kuruyor: kazan/kaybet `ScenarioController`'ın işi
  olduğu için bu örnek saf bir istatistik/skor sayacı ve ikinci dost kaybında skoru dondurmuyor
  (B-03). `Core/MissionState.cs` ve testleri **değişmedi**; HUD'un `İMHA`/`KAYIP` hücreleri
  anlamsız paydaları (`/0`, `/2147483647`) göstermemek için yalnız sayacı yazıyor. **Oyun
  değerleri (menzil, hasar, can, irtifa, spawn konumları) değişmedi.**
- **SCENE.md bulgu temizliği (B-02, B-04, B-05, B-06, B-07, B-08, B-09):** Altı ayrı dilim hâlinde,
  her biri kendi başına derlenebilir şekilde:
  **(B-08)** `EnvironmentBuilder.ScatterProps` sabit tohumu artık yereldir — `Random.state`
  kaydedilip dağıtım sonunda geri yükleniyor, böylece proplar tekrarlanabilir kalırken düşman
  yerleşimi/isabet zarları/radar gürültüsü her koşuda yeniden rastgele.
  **(B-04)** İzli mermiler mermi başına `Material` sızdırmıyor: yeni `MaterialLibrary.CreateUnlit`
  renk başına önbellekli bir materyal veriyor ve `LineRenderer.sharedMaterial`'a atanıyor; ayrıca
  `TracerEffect` `VfxLibrary` bütçesine tabi (bütçe dolduğunda hiç spawn etmez, `OnDestroy`'da
  slotu bırakır) ve `ExplosionEffect` kendi `_material`'ını `OnDestroy`'da yok ediyor.
  **(B-02)** `RadarSensor.Update` snapshot + aday başına `FindById` yerine tek bir `Prune()` ve
  `TargetRegistry.All` üzerinde tek geçiş yapıyor; `RcsComponent`/`Jammer` referansları
  `Targetable.Rcs`/`Targetable.Jammer` önbellekli özelliklerinden okunuyor (jamming yolu duruyor,
  jammer yokken maliyeti sıfır). Tespit davranışı ve tüm ayar değerleri aynı.
  **(B-07 + B-09)** Yerleşim düzeltmesi: `spawnMinRadius` 15 → 32 m (üs ayak izi ~22 m) ve
  `RandomScatterPosition` dalga içi ≥12 m ayrışma için reddetme örneklemesi yapıyor (24 deneme
  sınırı + geri düşüş, sonsuz döngü yok); avcılar seyir irtifası çevresinde ±2.4 m kaydırılmış
  irtifalarda doğuyor ve `EnemyDroneController.SetLoiterOffsets` ile her biri ayrı loiter
  yarıçapı/fazı alıyor. Menzil/hasar/can/atış hızı/seyir-minimum irtifa değerlerine dokunulmadı.
  **(B-05)** Projede `Rigidbody`, raycast ve çarpışma geri çağrısı bulunmadığı doğrulandıktan sonra
  kök collider'lar da kaldırıldı: `VehicleModelBuilder.HideRootMesh` artık `StripCollider`'ı
  çağırıyor ve `GuidedMunition.SetupVisuals` mühimmatın kendi collider'ını düşürüyor.
  **(B-06)** Kare başına tahsisler: `TargetRegistry.GetSnapshot(faction, buffer)` ve tahsissiz
  `CountAlive(faction)` eklendi; `IhaController`, `EnemyDroneController`, `AirDefenseSite`,
  `GunTurret` birer yeniden kullanılan tampon tutuyor; `ScenarioController`/`SimulationDirector`
  yalnız sayım gereken yerlerde `CountAlive` kullanıyor; `SimulationDirector.UpdateAllocation` dört
  tamponunu ve yeni `TargetAllocation.Assign(shooters, targets, List<int> result)` aşırı yüklemesini
  yeniden kullanıyor (Core tarafında paylaşılan scratch tamponlar + dört yeni EditMode testi).
  Tahsis eden eski aşırı yüklemeler soğuk yollar için korundu; davranış birebir aynı.
- **Kokpit görünümü + pilot radar skopu + kaçınılabilir SAM'ler:** Pilot modu artık "uçağın içinde"
  hissettiriyor ve gelen füzeler hem skopta hem gökyüzünde okunuyor. Dört dilim:
  **(1)** Test-driven `RadarScope` (Core): dünya konumunu burun-yukarı skop koordinatına yansıtır
  (`TryProject`, −1..1, +Y burun, +X sağ, irtifa yok sayılır, menzil dışı `false`); el yönü (handedness)
  testlerle sabitlendi (`Vector3.forward` burun → `(0, 0.5)`, `Vector3.right` → `(0.5, 0)`).
  **(2)** **Oyun ayarı (kullanıcı isteği):** hava savunması füzeleri kaçınılabilir hâle getirildi —
  SAM `munitionSpeed` 150 → **85**, AAA 130 → **95**. Hasar, atış hızı, kilitlenme süresi, menziller ve
  şarjör boyutları **değişmedi**; SİHA'nın kendi füzesi (180) **değişmedi**. Kritik ayrıntı:
  `GuidedMunition` her adımda hızı kendi `cruiseSpeed`'ine çektiği için yalnız fırlatma hızını düşürmek
  yetmiyordu; yeni `Launch(target, velocity, damage, cruiseSpeed)` aşırı yüklemesiyle hava savunması
  seyir hızını da veriyor. Ayrıca iz daha okunaklı (TrailRenderer `time` 0.5 → 1.1 s, genişlik 0.3 →
  0.38, egzoz pufu aralığı 0.08 → 0.06 s; pufları VfxLibrary bütçesi sınırlamaya devam ediyor).
  **(3)** `CameraRig` kokpit modu: **C** ile kontrol alındığında kamera doğrudan uçağın burnuna oturuyor
  (kökün kendi eksenlerinde ileri 1.6 m + yukarı 0.5 m; `TransformPoint` değil, çünkü birim kökleri
  ölçekli), gövdenin boresight'ına bakıyor ve takip kamerasından çok daha sert bir lerp ile bağlı
  kalıyor. **V** kokpit/takip arasında geçiş yapıyor, kontrol bırakılınca eski kamera davranışı geri
  geliyor. Kokpitteyken pilotun uçağının `"Model"` alt ağacındaki renderer'lar kapatılıyor (kokpitten
  çıkarken, Tab ile uçak değiştirirken, kontrol bırakılırken ve rig kapatılırken/yok edilirken geri
  açılıyor; her renderer ayrı ayrı null kontrolünden geçtiği için uçak düşürülse bile güvenli).
  Kokpitte taban FOV 8° daralıyor, art yakıcı FOV artışı bu yeni tabana göre çalışmaya devam ediyor;
  HUD için `CameraRig.CockpitView` açıldı.
  **(4)** `Hud` sağ altta dairesel radar skopu çiziyor (yalnız pilot modunda, mevcut tek `OnGUI`
  içinde): koyu disk + 1px çerçeve, üç menzil halkası, artı çizgileri, tepede burun işareti, `Time.time`
  ile dönen (duraklatılınca donan) tarama çizgisi ve `MENZİL 250 m` etiketi (`scopeRange` serileştirilmiş).
  Blipler `RadarScope.TryProject` ile pilotun konum/yönünden yansıtılıyor: düşman kırmızı kare, dost
  yeşil küçük kare, tespit edilen/kilitlenen hedef daha büyük amber kare, **gelen füzeler** ise
  `GuidedMunition.Active` içinden pilotun `Targetable`'ını hedefleyenler olarak süzülüp yanıp sönen
  kırmızı üçgen + merkeze doğru kısa tehdit ekseni çizgisi olarak çiziliyor. Skopun altında füze sayısı
  ve en kısa çarpma süresi yazıyor. IMGUI'de çizgi/daire primitifi olmadığı için disk satırlardan,
  halkalar ve çizgiler noktalardan kuruluyor. Mevcut HUD öğelerinin hiçbiri kaldırılmadı; alt kontrol
  şeridine yalnızca `V: KOKPİT / TAKİP KAMERASI` ipucu eklendi.

- **Gerçek kokpit içi (`CockpitFrame`):** Kokpit görünümü artık yalnız buruna oturmuş bir kamera
  değil; oyuncu gerçekten camın ardında oturuyor. Kokpit primitive'lerden kuruluyor ve **uçağa değil
  KAMERAYA** bağlanıyor (birim kökleri ölçekli olduğu için uçağa bağlı bir kokpit shear olurdu ve her
  uçak için ayrı ayarlanması gerekirdi). Hiçbir ölçü sabit metre değil: seçilen kare mesafesinde
  (`d = max(0.6 m, nearClip * 2)`) kameranın gerçek frustum yarı-yüksekliği
  `halfH = d * tan(FOV/2)` ve yarı-genişliği `halfW = halfH * aspect` hesaplanıp her parça bunların
  oranı olarak yerleştiriliyor — böylece kokpit her FOV ve en-boy oranında aynı kadrajı veriyor.
  Parçalar: pilota doğru hafifçe eğimli (−12°) koyu **gösterge paneli** (alttan görüş yüksekliğinin ~%28'ine kadar),
  üstünde ayrı okunan **güneşlik dudağı**, üst kenarda görüş yüksekliğinin ~%8'i kalınlığında **ön cam
  kirişi**, panelin uçlarından kirişin uçlarına hafifçe içe yakınsayan iki **A-direği** (her biri görüş
  genişliğinin %7'si — ileri görüş açık kalıyor), iki yanda tam boy koyu **kanopi rayları**, `d * 3`
  mesafesinde alçakta duran ve pilotun üzerinden baktığı iki parçalı **burun kaması**, tüm görüşü
  kaplayan çok hafif soğuk tonlu **kanopi camı** (alfa 0.06, `MaterialLibrary.CreateTransparent`;
  materyal kurulamazsa cam hiç çizilmiyor) ve panelde üç sönük dekoratif **gösterge ışığı** (amber/teal
  — gerçek veri HUD'da olduğu için bilinçli olarak silik). Her parçanın collider'ı siliniyor, gölge
  atmıyor/almıyor (`VehicleModelBuilder` konvansiyonu); materyali kurulamayan parça çizilmiyor.
  `LateUpdate` FOV/aspect'i önbellekle karşılaştırıp (0.25° / 0.005) sapma olunca yerleşimi yeniden
  çalıştırıyor — art yakıcı FOV artışında birkaç kare boyunca yalnızca ~13 transform yazımı, tahsis yok.
  Kameranın clip düzlemlerine dokunulmadı; bunun yerine kare mesafesi near plane'in iki katından
  aşağı inemiyor (en yakın parça olan cam bile `0.9 * d` ile önde kalıyor).
  `CameraRig` tarafı: kare ilk kokpit girişinde tembel kuruluyor, yalnızca pilotluk + `CockpitView`
  doğruyken görünür (takip kamerası, serbest uçuş, kontrol bırakma, uçağın düşmesi, rig'in kapanması →
  gizleniyor), rig yok edilirken kamera altında kalmasın diye siliniyor. Burun rengi, kokpitte zaten
  gizlenen `"Model"` alt ağacındaki `"Fuselage"` materyalinden **uçak değiştikçe bir kez** okunup
  `SetBodyColor` ile veriliyor. Mevcut davranışların hiçbiri değişmedi: **V** geçişi, **C** ile varsayılan
  kokpit, `"Model"` renderer'larının gizlenmesi ve art yakıcı FOV artışı aynen duruyor.

- **Atış imleci namluya bağlandı + filo paneli aç/kapat (kullanıcı isteği):** İki dilim.
  **(1)** Test-driven `GunPipper` (Core): namlu ekseninde `range` metrede merminin vardığı dünya
  noktasını verir (`AimPoint(muzzle, forward, muzzleSpeed, range, gravity)`); `forward` normalize
  edilir, uçuş süresi `t = range / muzzleSpeed`, düşüş `Vector3.down * ½·g·t²`
  (`BallisticProjectile`'ın konvansiyonu: yerçekimi aşağı yönlü ve **pozitif büyüklük**,
  `GunPipper.EarthGravity = 9.81`). Bozuk girdiler sıfıra bölmez: sıfır/negatif ağız hızı veya
  menzil → doğrudan namlu ekseni, sıfır `forward` → namlunun kendisi. 13 EditMode testi (elle
  hesaplanmış düşüş, düşüşün t² ile büyümesi, burun yukarıyken imlecin yukarı kayması, bozuk
  girdiler). Sim'in topu **hitscan** olduğu için (`GunTurret.TryFireAtPoint` mermi harcayıp isabeti
  doğrudan nişan ışını üzerinden zar atar, mermi simüle edilmez) oyunda ağız hızı diye bir değer
  yok; HUD alanları `pipperMuzzleSpeed`/`pipperGravity` bu yüzden **0** varsayılıyor — imleç tam
  olarak mermilerin gittiği yeri, namlu eksenini gösteriyor.
  **(2)** `Hud` artık nişangâhı ekran ortasına çakmıyor: `GunPipper.AimPoint` ile topun **kendi**
  etkili menzilinde (`GunTurret.Gun.EffectiveRange`; top yoksa `PlayerDroneController.GunRange`)
  dünya noktası bulunuyor, `CameraRig`'in sürdüğü kameranın `WorldToScreenPoint`'i ile ekrana
  yansıtılıyor ve IMGUI koordinatına çevriliyor (`y = Screen.height - y`). Böylece **burun yukarı/
  aşağı eğildiğinde imleç ekranda kayıyor**. İmleç ayrıca uçağın yatışıyla **dönüyor**: yatış açısı
  `"Model"` çocuğunun (yatışı `BankingVisual` oraya yazar) üst vektörü ile kameranın sağ/üst
  vektörlerinden `Atan2` ile çıkarılıp `GUIUtility.RotateAroundPivot` ile uygulanıyor; `GUI.matrix`
  `try/finally` ile geri yükleniyor, istisna GUI matrisini kirli bırakamıyor. Boyut **64 → 110 px**
  (~1.7×) büyütüldü ve mevcut `HudTheme.Crosshair`'in (orta nokta + boşluklu dört kol + menzil
  tikleri) üstüne ince bir halka eklendi. Renk paleti değişmedi: normalde amber, tespit edilen canlı
  bir düşman top menzilindeyken `HudTheme.Critical`. Nişan noktası kameranın arkasında (`z <= 0`)
  veya görüntü alanı dışında kalırsa imleç hiç çizilmiyor. Kamera `CameraRig` üzerinden bulunuyor
  (yoksa `Camera.main`), UnityEngine nesnelerinde açık `== null` kullanılıyor.
  **Filo paneli aç/kapat:** yeni **G** tuşu (`GameControls.FleetPanelVisible`, diğer global tuşlarla
  aynı yerde) sağdaki `FİLO DURUMU` panelini gizler/gösterir. **F daha iyi okunurdu ("filo") ama
  zaten pilotun güdümlü füze tuşu ve kameranın serbest mod tuşu**, bu yüzden G seçildi. Panel
  **varsayılan olarak açık** (`GameControls` yoksa da açık), yani tuşa hiç basmayan oyuncu için
  hiçbir şey değişmiyor. Alt kontrol şeridine `G: FİLO PANELİ (GİZLE/GÖSTER)` ipucu eklendi.
  **Hiçbir oyun değeri (menzil, hasar, atış hızı, irtifa) değişmedi** — değişiklik sunum katmanında.

- **Üç uçulabilir arketip + uçak seçimi (kullanıcı isteği):** Oyuncu artık görev seçim ekranında
  hangi uçağı uçuracağını seçiyor. Dört dilim.
  **(1) Core verisi:** Yeni `AircraftKind` (FighterJet / Siha / Iha), değiştirilemez
  `AircraftProfile` ve `AircraftCatalog` (`All`, `Default`, `TryGet`, `GetOrDefault`, `Cycle`).
  **SİHA profili 1.0× temel değerdir**: sim'in bugün kullandığı değerlerin birebir aynısı
  (uçuş 30 m/s, dönüş 80°/s, pilot tavanı 40 m/s, seyir 14 m, yakıt 100 @ 2/s, top 300 fişek /
  10 atış-s / 60 m / 2.5° / 4.5 hasar, 6 füze @ 120 m, tespit 120 m, radar 250 m, 100 can).
  Savaş uçağı bu temelin çarpanı olarak yazıldı (hız ×1.5 → 45, pilot tavanı 60, dönüş ×1.25 → 100,
  seyir 18 m, yakıt ×0.7 → 70 ve yakma ×1.6 → 3.2, top ×1.6 atış hızı → 16 / 400 fişek / 70 m /
  2.0° / 6 hasar, **yalnız 2 füze** @ 100 m, tespit 100 m, radar ×0.8 → 200, 90 can); keşif İHA ise
  ters yönde (hız ×0.7 → 21, pilot tavanı 28, dönüş 65, seyir 12 m, yakıt ×1.8 → 180 ve yakma
  ×0.7 → 1.4, mevcut keşif topu 200/8/45 m/3°/3, **füze yok**, tespit 150 m, radar ×1.4 → 350,
  70 can). Ayrıca her profilde seçim ekranı için dört **0–1 gösterge puanı** (hız / çeviklik /
  ateş gücü / havada kalış) var, böylece UI ham birimleri hiç bilmiyor. `AircraftProfileTests`
  sihirli sayı değil **ilişki** doğruluyor (jet > SİHA > İHA hız; İHA > SİHA > jet yakıt; en geniş
  radar İHA'da; en yüksek atış hızı jette; en çok füze SİHA'da; puanlar 0..1 aralığında; `Cycle`
  iki yönde de dönüyor; bilinmeyen/null id `TryGet`'te hata atmadan `false`).
  **(2) Seçimin taşınması:** `ScenarioController` mevcut `SelectedKind` deseninin aynısıyla
  statik `SelectedAircraftId` (varsayılan: SİHA) ve savunmacı `SelectedAircraft` kancasını sunuyor;
  seçim `SimulationBootstrap.Rebuild()`'i sağ kalıyor, bilinmeyen/boş id hata atmadan varsayılana
  düşüyor. Selektöre hiç dokunmayan oyuncu **bugünkü davranışın birebir aynısını** alıyor.
  **(3) Seçim ekranı:** `ScenarioMenu` görev kartlarının altına uçak satırı çiziyor — her profil
  için bir kart (ad, tek satır açıklama ve dört puanın `HudTheme.Bar` göstergesi). Yeni renk yok;
  seçili kart görev kartlarının **aynı amber vurgusunu** alıyor. Fareyle tıklama veya **←/→**
  (`AircraftCatalog.Cycle`) seçiyor; bu iki tuş projede başka hiçbir yerde bağlı değil (pilot ↑/↓
  ile yunusluyor, kamera WASD ile uçuyor).
  **(4) Sahaya uygulanması:** `SimulationBootstrap` sabit SİHA yuvasının yerine profilden
  **oyuncunun uçağını** kuruyor (`SpawnPlayerAircraft`): füze taşıyan arketipler `SihaController`,
  keşif arketipi `IhaController` alıyor; değerler mevcut yollarla giriyor — yeni
  `IhaController.ApplyProfile` (hız/dönüş/pilot tavanı/tespit/yakıt) ve `SihaController.ApplyProfile`
  (füze şarjörü + menzil), `GunTurret.Configure`, yeni `RadarSensor.ConfigureRange` ve yeni
  `Targetable.SetMaxHealth`. Sonuncusu şart: `Awake` **`AddComponent` içinde** çalıştığı için
  `MaxHealth` alanını sonradan yazmak zaten kurulmuş can havuzuna ulaşmıyordu. `PlayerDroneController`
  artık sabit 40 m/s yerine uçtuğu uçağın kendi `PilotMaxSpeed` değerini okuyor (YZ'nin kurduğu
  drone'larda bu değer 40 olarak kalıyor, yani davranış değişmiyor) ve `SetPreferredAircraft` ile
  **C** doğrudan menüde seçilen uçağı devralıyor. İki keşif İHA'sı yapay zekâ kanadı olarak
  **hiç dokunulmadan** kalıyor; profil `Rebuild()` sırasında sızmıyor, her kurulumda statik
  seçimden yeniden okunuyor.
  **Kapsam dışı bırakılan:** arketiplerin radar kesit alanı (RCS) farkı. Sim'de **dost** bir uçağın
  RCS'ini okuyan hiçbir şey yok (düşman sensörleri düz menzil/FOV ile tespit ediyor), bu yüzden
  profile ölü bir alan eklenmedi; düşman sensörlerini imza duyarlı yapmak ayrı bir iş.
  **Model notu:** savaş uçağı bu turda silahlı İHA siluetini ödünç alıyordu; gerçek gövdeler
  bir sonraki turda geldi (aşağıdaki "Uçak modelleri" maddesi).
- **Uçak modelleri (kozmetik tur):** üç uçulabilir arketip artık **kendi gövdesini** kullanıyor;
  hiçbir oyun değeri (hız, dönüş, yakıt, top, füze, radar, can, spawn) değişmedi.
  **(1) Savaş uçağı — `VehicleModelBuilder.BuildFighterJet` (37 parça):** beş segmentli, bel veren
  (alan kuralına göndermeli) gövde; sivri radome + pitot çubuğu; sırt omurgası; **saydam** kabin
  camı (`MaterialLibrary.CreateTransparent`; materyal kurulamazsa cam **çizilmiyor**, asla opak
  çizilmiyor); kırık delta kanat (LERX + dış panelde artan ok açısı + flaperon); iki dışa yatık
  dikey stabilizatör + yatay kuyruk; yan hava alıkları (ağızları koyu, "delik" gibi okunsun diye);
  merkez hattında lüle + geleneksel `"EngineGlow"` parçası; dört pilon + ince mühimmat; karın
  altında `"Turret"`/`"TurretBody"` pivot konvansiyonunda sensör küresi. Ölçüler kokpit kamerasıyla
  uyumlu: pilotun gözü model uzayında ~(0, 0.5, 1.6) olduğu için kabin o noktanın etrafına kuruldu.
  Jette pervane **yok**; `PropellerSpinner` parçayı bulamayınca sessizce hiçbir şey yapmıyor.
  **(2) Keşif İHA (19 parça):** yüksek en-boy oranlı (~7.1 m açıklık, AR ~10) üç parçalı planör
  kanadı (dihedral + aileron + kanat ucu kanatçıkları), şişkin avyonik burnu, `"Radar"` satcom
  kubbesi, `"Turret"` pivotuna taşınan çene sensör küresi, kuyruk kirişi + anten, yukarı yatık
  V-kuyruk, itici pervane (`"Propeller"` adı korundu) ve **en açık livre** (kanat kaplaması koyu
  trim yerine yeni `Accent` tonunda).
  **(3) SİHA (27 parça):** artık keşif gövdesini süslemiyor, **bağımsız** kuruluyor: kademeli burun
  bölümü, sırt satcom kubbesi, daha büyük çene küresi, flap ve uç kanatçıklı düz kanat, kirişin
  **altında ters V-kuyruk**, itici pervane ve iç/dış pilonlarda dört mühimmat.
  **Sözleşmeler korundu:** her parça `"Model"` çocuğu altında (kökün ölçeğinin tersiyle), collider'lar
  siliniyor, `"Fuselage"` **doğrudan** `"Model"` çocuğu (`CameraRig.TryReadBodyColor` özyinelemesiz
  `Find` kullanıyor), `CameraRig` kokpit görünümünde tüm yeni renderer'ları kapatıyor.
  **Materyal:** yeni palet yok — `Accent` (birimin kendi renginden türetilen açık ton) ve `Glow`
  (`BuildEnemyFighter` içinden çıkarılan, değeri değişmemiş egzoz materyali) yardımcıları eklendi;
  kabin camı **önbelleğe alınıyor** (`CreateTransparent` önbelleksiz örnek döndürdüğü için, her
  kurulumda bir cam materyali sızmasın diye — bkz. B-04).
- **Dünya detayı (kozmetik tur, kullanıcı isteği: "hava savunma füzeleri de patriotlar gibi olsun,
  gerçek bina ve ağaçlar olsun"):** üç dilim, **hiçbir oyun değeri değişmedi** (menzil, kilitlenme,
  şarjör, atış hızı, mühimmat hızı, hasar, can, spawn sayısı/konumu aynı).
  **(1) Patriot tipi SAM bataryası — `BuildSamSite` (37 parça, eskiden 8):** artık tek bir kutu +
  dört tüp değil, bir **atış birimi**: M901 tipi alçak yataklı **rampa treyleri** (yatak, boyun,
  çeki kancası, dört bojili teker, iki destek kirişi), üzerinde **yükselen 2×2 kanister rampası**
  (dört kare kanister, patlayan ön kapaklar, arka plaka, iki yan ray), yanına park etmiş
  **faz dizisi radar treyleri** (yatak, iki teker, kriko, ~30° geriye yatık büyük panel + ince
  çerçeve, koyu metal materyalde) ve bir **atış kontrol sığınağı** (kızaklı kabin, kapı, jeneratör,
  egzoz, anten direği + whip). Rampa mevcut `"Turret"` (ölçeksiz, boş) pivotunda durduğu için
  `TurretVisual`'ın taraması aynen çalışıyor; yükseliş açısı ikinci bir ölçeksiz pivotta (`"Rack"`)
  tutuluyor, böylece kanisterler dönmemiş koordinatlarda yazılabiliyor ve hiçbir parça kesme
  (shear) yemiyor. Rampa gerçek M901 gibi **arkadan** mafsallanıyor: yükselince kanister ağızları
  yukarı-ileri gidiyor, kuyrukları yatağa gömülmüyor ve ağızlar kabaca **model orijininin üstünde**
  kalıyor. Bu önemli, çünkü `AirDefenseSite.LaunchMunition` mermiyi **birim kökünün konumundan**
  spawn ediyor (adlandırılmış namlu transform'u yok, oyun değeri olduğu için değiştirilmedi) —
  ön destek kirişi de o noktayı boşta bırakacak şekilde geri çekildi.
  **(2) AAA topu — `BuildAaaSite` (14 parça, eskiden 4):** SAM'den görsel olarak ayrı kalması için
  bilerek **top** olarak bırakıldı; çekili kundak (teker, destek kızakları, cephane sandığı) ve
  taretinde koruma kalkanı, beşik, nişangâh ve iki namlu ağızlığı eklendi.
  **(3) Binalar — `EnvironmentBuilder` (ortalama ~10 parça/bina, eskiden 1):** üç arketip —
  **depo** (podyum, uzun hol, tepe pencere şeridi, kepenk kapı, 20° yatık üç testere dişi çatı
  şeridi, iki çatı bacası), **orta katlı blok** (2–3 kat, kat silmesi + cam şerit, korkuluk,
  merdiven kulesi, anten) ve **kule** (köşe payandaları, üç cam şerit, korkuluk, geri çekilmiş üst
  kat, baca, direk). Pencere sıraları **doku değil panel**: duvar bloğundan birkaç santim geniş
  koyu "bant" kutuları, tek parçayla dört cepheyi birden boyuyor. Ölçüler eski 3–8 m zarfına yakın
  tutuldu; yalnız ince anten/direkler yukarı taşıyor, böylece binalar drone'ların 10–14 m seyir
  bandını yutmuyor.
  **(4) Ağaçlar (ortalama ~4.85 parça/ağaç, eskiden 3):** üç tür — **kozalaklı** (ince gövde +
  daralan üç kademe + tepe sivrisi; Unity'de koni primitifi yok, klasik kademeli silindir çamı
  kullanıldı), **geniş yapraklı** (gövde + iki açılı dal güdüğü + üç örtüşen, eksenden kaydırılmış
  taç küresi) ve **çalı** (gövdesiz, üç alçak küme). Her örnekte boy, taç yarıçapı, eğim (yaw +
  birkaç derece yatıklık) ve yeşil tonu aynı **sabit tohumlu** akıştan çekiliyor.
  **Materyal yolu:** yeni API eklenmedi — `MaterialLibrary.Create`'in mevcut **renk anahtarlı
  önbelleği** kullanıldı, ama örnek başına sürekli rastgele renk yerine **kuantalanmış palet**
  (6 yaprak + 5 duvar tonu). Eski kod önbelleği fiilen baypas edip ağaç/bina başına birer materyal
  üretiyordu; dağılımın toplam materyal sayısı ~240'tan **17'ye** indi (B-16'nın palet yarısı;
  `enableInstancing` hâlâ açık iş).
  **Nesne bütçesi:** prop kökü ~968 → ~1553 GameObject (aynı büyüklük mertebesi). Ağaç sayısı,
  kaya sayısı, bina sayısı ve dağılım tohumu **değişmedi**.
  **Collider sözleşmesi:** aynı — dağılımdaki her parça hâlâ collider'ı silen `Prop()`'tan geçiyor,
  yeni prop kökleri boş `GameObject`. `B-19` bu turda kapandı: `"Radar"` artık ölçeksiz ve
  **döndürülmemiş** bir pivot, 30°'lik yatıklık altındaki `"ArrayMount"` çocuğunda; tabak yalpalamak
  yerine düzgün azimut taraması yapıyor.
