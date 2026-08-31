# İHA/SİHA Simülasyonu — Geliştirme Günlüğü (DEVLOG)

> Bu dosya, projede yapılan tüm işleri ve kararları kaydeder; oturumlar arası bağlamı (context) korumak ve projeyi hızlı anlamak içindir.

**Son güncelleme:** 2026-08-31
**Branch:** `claude/slack-session-f6uh9g`
**Unity sürümü:** 6000.5.9f1

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
| `SimulationBootstrap` | Play'de sahneyi primitive'lerden kurar (kamera, ışık, zemin, drone'lar, ScenarioController). |
| `ScenarioController` | Dalga tabanlı senaryo: seçilen göreve göre (`ScenarioLibrary.Composition`) her dalganın düşman karışımını spawn eder ve kazan/kaybet'i yönetir; `BeginMission()` çağrılana kadar bekler. |
| `SimulationDirector` | Görev takibi ve skorlama (dalga güvenli kill sayımı; kazan/kaybet artık ScenarioController'da). |
| `Hud` | Ekran üstü (IMGUI) bilgi paneli: görev, skor, radar temasları. |
| `CameraRig` | Serbest uçan kamera (WASD + fare) ve drone takip modu. |
| `ExplosionEffect` | Asset'siz patlama işareti: büyüyüp sönen emisyonlu küre (mühimmat isabeti + imha). |
| `GameControls` | Klavye kontrolleri: R yeniden başlat, P duraklat, +/- zaman ölçeği. |
| `GunTurret` | `GunSystem` + `HitProbability` sarmalayıcısı: hedefe veya serbest nişan noktasına top atışı, izli mermi. |
| `TracerEffect` | Asset'siz izli mermi görseli: `LineRenderer` ile kısa ömürlü parlak çizgi. |
| `EnemyDroneController` | Düşman avcı drone'u: uçar, dost drone'ları tespit eder ve topla taramaya alır (hava muharebesi). |
| `PlayerDroneController` | Pilot modu: oyuncu dost bir drone'u devralıp elle uçurur (C/Tab/W/S/A/D/↑↓/Space/F + Q/E/X). |
| `CountermeasureDispenser` | `CountermeasureSystem` sarmalayıcısı: flare/chaff salvosu, salvo sayacı (füzeler bunu izler), kısa görsel puf. |
| `ScenarioMenu` | Kurulum gerektirmeyen IMGUI görev seçim/brifing ekranı: açılışta çıkar, sim'i duraklatır, `M` ile tekrar açılır. |
| `MaterialLibrary` | Standard/URP uyumlu, önbelleklenmiş materyal fabrikası (renk, metalik, pürüzsüzlük, emisyon). |
| `VehicleModelBuilder` | Primitive'lerden araç siluetleri kurar (İHA, SİHA, düşman avcısı, SAM, AAA, yer hedefi); parçaların collider'ları silinir, fizik etkilenmez. |
| `EnvironmentBuilder` | Prosedürel arazi mesh'i, üs pisti, ağaç/kaya/bina dağılımı ve gökyüzü/sis/ortam ışığı/güneş ayarı (tamamı görsel). |
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
3. Boş sahnede boş bir GameObject'e `SimulationBootstrap` bileşenini ekle → **Play**.
4. Sahne kendini kurar ve **görev seçim menüsü** açılır (sim duraklatılmış olarak bekler).
   Bir görev seç → görev başlar. HUD sol üstte görev/skor/temasları gösterir. **M** menüye döner.

**Kamera kontrolleri:** Sağ tık basılı + fare = bak · WASD = uç · Shift = hızlan · Tab = drone takip et · F = serbest mod.

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
  görev sırasında yeniden açılıyor (seçim sahneyi yeniden yükleyerek sahayı temizliyor).
  Seçilen görev statik `ScenarioController.SelectedKind`'de tutulduğu için R/menü yeniden
  yüklemelerinden sağ çıkıyor.
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
