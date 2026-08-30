# İHA/SİHA Simülasyonu — Geliştirme Günlüğü (DEVLOG)

> Bu dosya, projede yapılan tüm işleri ve kararları kaydeder; oturumlar arası bağlamı (context) korumak ve projeyi hızlı anlamak içindir.

**Son güncelleme:** 2026-08-30
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
| `ScenarioController` | Dalga tabanlı senaryo: her dalgada üç düşman tipini (Sabit hedef / SAM / AAA) `WavePlan`'e göre spawn eder ve kazan/kaybet'i yönetir. |
| `SimulationDirector` | Görev takibi ve skorlama (dalga güvenli kill sayımı; kazan/kaybet artık ScenarioController'da). |
| `Hud` | Ekran üstü (IMGUI) bilgi paneli: görev, skor, radar temasları. |
| `CameraRig` | Serbest uçan kamera (WASD + fare) ve drone takip modu. |
| `ExplosionEffect` | Asset'siz patlama işareti: büyüyüp sönen emisyonlu küre (mühimmat isabeti + imha). |
| `GameControls` | Klavye kontrolleri: R yeniden başlat, P duraklat, +/- zaman ölçeği. |
| `GunTurret` | `GunSystem` + `HitProbability` sarmalayıcısı: hedefe veya serbest nişan noktasına top atışı, izli mermi. |
| `TracerEffect` | Asset'siz izli mermi görseli: `LineRenderer` ile kısa ömürlü parlak çizgi. |
| `EnemyDroneController` | Düşman avcı drone'u: uçar, dost drone'ları tespit eder ve topla taramaya alır (hava muharebesi). |
| `PlayerDroneController` | Pilot modu: oyuncu dost bir drone'u devralıp elle uçurur (C/Tab/W/S/A/D/↑↓/Space/F). |

### Testler (Sim.Tests.EditMode)
Her Core sistemi için bir test dosyası. Toplam ~18 test dosyası. Çalıştırma:
`Window > General > Test Runner > EditMode > Run All`.

---

## 4. Nasıl çalıştırılır
1. Doğru klasörde: `git pull origin claude/slack-session-f6uh9g`
2. Unity Hub → 6000.5.9f1 ile projeyi aç (Package Manager güncelleme sorarsa kabul et).
3. Boş sahnede boş bir GameObject'e `SimulationBootstrap` bileşenini ekle → **Play**.
4. Sahne kendini kurar. HUD sol üstte görev/skor/temasları gösterir.

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

---

## 7. Mevcut durum & bilinen sınırlar
- **Görseller bilerek primitive** (kapsül/küp). Gerçek 3D modeller/materyaller en sona bırakıldı (M5).
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
