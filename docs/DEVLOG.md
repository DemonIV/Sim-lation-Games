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
| `TargetAllocation` | Atıcı-hedef paylaşımı (aynı hedefe boşa ateşi önler). |
| `EngagementPolicy` | Angajman durum kararı (Devriye/Angaje/Üsse Dönüş). |
| `FuelTank` | Yakıt/menzil modeli (gaz kesme oranına göre tüketim). |

### Sim.Runtime (ince MonoBehaviour'lar)
| Bileşen | Görevi |
|---|---|
| `IhaController` | Keşif İHA: uçuş + devriye + radar sensörüyle tespit. |
| `SihaController` | Silahlı SİHA (IhaController'dan türer): kilitlenince güdümlü mühimmat fırlatır. |
| `RadarSensor` | RadarSystem + RCS + EW + TargetTracker ile gerçekçi tespit/izleme. |
| `RcsComponent` | Hedefin açıya bağlı radar imzası. |
| `Jammer` | Gemi üstü gürültü karıştırıcı (EW menzil düşürme). |
| `GuidedMunition` | PN güdümü + arayıcı başlık + balistik ile güdümlü mühimmat, yakınlık tapası. |
| `TargetRegistry` / `Targetable` | Canlı hedeflerin kaydı; controller'lar her kare sorgular. |
| `SimulationBootstrap` | Play'de sahneyi primitive'lerden kurar (kamera, ışık, zemin, drone'lar, hedefler). |
| `SimulationDirector` | Görev takibi ve skorlama. |
| `Hud` | Ekran üstü (IMGUI) bilgi paneli: görev, skor, radar temasları. |
| `CameraRig` | Serbest uçan kamera (WASD + fare) ve drone takip modu. |

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
- **M1 — Taktik katman** — MissionState, TargetAllocation, EngagementPolicy, FuelTank + HUD + serbest kamera. ✅ (taktik beynin davranışa bağlanması sıradaki iş)
- **M2** — Çift taraflı tehdit: radar+füzeli hava savunması, drone kaçınma, SEAD. ⏳
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

---

## 7. Mevcut durum & bilinen sınırlar
- **Görseller bilerek primitive** (kapsül/küp). Gerçek 3D modeller/materyaller en sona bırakıldı (M5).
- **Taktik beyin henüz davranışa bağlı değil:** `TargetAllocation`, `EngagementPolicy`, `FuelTank` yazıldı ve
  test edildi, ancak controller'lar dışarıdan hedef/durum enjekte edecek public kanca sunmadığı için drone'ların
  davranışını **henüz yönetmiyor**. Drone'lar şu an kendi tespitleriyle uçup ateş ediyor. Bunu gerçek davranışa
  bağlamak sıradaki iştir (controller'lara `AssignedTargetId`, `EngagementState`, `FuelFraction`, `AmmoFraction`
  kancaları eklenecek).
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
