# İHA/SİHA Simülasyonu — Geliştirme Günlüğü (DEVLOG)

> Bu dosya, projede yapılan tüm işleri ve kararları kaydeder; oturumlar arası bağlamı (context) korumak ve projeyi hızlı anlamak içindir.

**Son güncelleme:** 2026-09-02
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
En son tur **düşman tespit menzilleri saha ölçeğine çekildi** (`ThreatEnvelope`, Core, testli) ve
**karıştırıcı gerçek, satın alınabilir bir sistem** oldu: hangara sekizinci hat olarak *Elektronik
Harp* geldi, seviye 0 = karıştırıcı yok, üstünde `SimulationBootstrap` oyuncunun uçağına `Jammer`
takıyor ve **K** tek bir 6 sn'lik yayın atımı başlatıyor (14 sn soğuma, yayın sürerken yakıt ×1.5).
`docs/SCENE.md`'deki "hiçbir şey `Jammer` takmıyor" notu kapandı. Ayrıntı ve menzil tabloları
`## 9. Değişiklik günlüğü`'nün son iki maddesinde.
En son tur bir **oynanış hatası** düzeltildi (kullanıcı: "manevra hareketi ile kaçamıyorum
roketlerden"). Kök neden X tuşunun uçuş modeline ulaşmaması **değil**, füzenin **tanımı gereği
yenilemez** olmasıydı: PN komutu hiç kırpılmıyordu, arayıcı başlık güdümü kapıya almıyordu ve güdüm
aslında PN değil takip (pursuit) rotasıydı. Artık füze **sınırlı bir takipçi** (`MissileAgility` ile
yük sınırı, kilit kaybında balistik), hava savunması **lead'li** atıyor ve **X** gerçek bir over-g
kırış yeteneği (2 sn, 6 sn soğuma) — HUD'da `KAÇIŞ MANEVRASI` penceresiyle. Ayrıntı ve ayarlanan
oyun değerleri `## 9. Değişiklik günlüğü`'nün son maddesinde.
En son tur oyuna **kampanya** geldi (kullanıcı isteği): artık görev seçmek yerine **SEVİYE 1…8**
oynanıyor. Seviyeler mevcut `ScenarioLibrary`/`WavePlan` verisini kullanır (paralel senaryo sistemi
yok), zorluk ve ödül index'ten formülle türer. Her sortiden **kredi** kazanılır — ilk başarılı
geçişte tam, tekrar oynayışta ve başarısız sortide %25 (para musluğu yok). Kredi **hangar**da
harcanır: yedi yükseltme hattı (motor/hız, namlu gücü, füze yuvası = yeni silah, çeviklik, gövde,
yakıt, radar), maliyet `round(BaseCost·1.6^(L−1)/25)·25`. Yükseltmeler `AircraftUpgrades.Apply` ile
taban profilin üstüne **çarpanla** uygulanır: sıfır yükseltmeyle uçak bugünkünün birebir aynısıdır.
İlerleme/kredi/garaj `PlayerPrefs` + `JsonUtility` ile kaydedilir (bozuk kayıt istisna değil taze
başlangıç üretir).
En son tur **iki hata düzeltmesi** oldu (`docs/SCENE.md` **B-20** ve **B-21**). (1) Düşman can
değerleri hiç uygulanmıyordu — `Targetable.Awake` `AddComponent` içinde çalıştığı için spawner'ın
sonradan yazdığı `MaxHealth` ölü atamaydı ve her düşman 100 HP ile doğuyordu; `MaxHealth` artık
canlı havuzu `Health.SetMax` ile yeniden boyutlandıran bir **property**. (2) Bina tepeleri
(arazi 3 m + en yüksek prop 11 m = **14 m tavan**) drone'ların 10–14 m seyir bandının içindeydi;
yeni Core sistemi `FlightEnvelope` **18 m taban** (14 + 4 m pay) tanımlıyor ve bant +6 m ötelendi
(İHA 18 · SİHA 20 · Jet 24 · düşman avcı 20). Denge notu: yalnız SAM zorlaştı (100→120), AAA / yer
hedefi / avcı kolaylaştı.
En son tur **elektronik harp katmanı dekoratif olmaktan çıktı**: düşman sensörleri artık dost
uçağın **radar imzasını** okuyor. Tespit yasası tek bir Core dosyasında toplandı
(`SignatureDetection`: menzil ∝ RCS^0.25 + karıştırma + görüş konisi); `AircraftProfile`'a
`RadarSignature` geldi (jet 4 m² · SİHA 1 m² taban · keşif İHA 0.25 m²) ve `SimulationBootstrap`
bunu oyuncunun uçağına (ve dost YZ İHA'larına) `RcsComponent` olarak takıyor. SAM/AAA ve düşman
avcıların ayarlı tespit menzilleri **değişmedi** — artık 1 m² hedefe karşı referans menzil, yani
SİHA bugünkü mesafede, jet ×√2 daha uzaktan, keşif İHA ÷√2 daha yakından görülüyor. HUD'da pilot
modunda **İMZA** paneli, seçim kartlarında **GİZLİ** çubuğu var.
En son iki tur **denge + ses** oldu. (1) Bir önceki turda bayraklanan **seviye 2 boşluğu** kapandı:
AAA **tespit** menzili 60 → **67 m** (atış menzili 50 m'de sabit), çünkü boşluğu yaratan terim
tespitti — keşif İHA'ya cevap mesafesi 42.4 → **47.4 m** oldu (kendi topu 45 m), jet ve SİHA'nın
bandı ise atış-kırpımlı oldukları için **hiç değişmedi**. (2) Oyun artık **sesli**: projede ses
varlığı olmadığı ve içe aktarılamadığı için her ses kodla üretiliyor — sentez matematiği
`Sim.Core.AudioSynth`'te (testli), `AudioClip`/`AudioSource` tarafı `AudioLibrary` +
`AudioDirector`'da. Motor (gaza bağlı, arketipe göre pervane/türbin), top, füze atışı, gelen füze
uyarı tonu (kaçış penceresinde değişiyor), patlama ve menü/hangar sesleri var; ana ses/sessiz
**N** tuşunda ve `PlayerPrefs`'te saklanıyor. Ayrıntı `## 9`'un son iki maddesinde.

En son tur **üç cila maddesi** (hepsi kullanıcının oynarken bildirdiği): (1) düşman renkleri —
yer hedefi tam orta gri, SAM ise neredeyse siyah bordoydu, ikisi de araziye/binalara gömülüyordu;
AAA turuncu, düşman avcı mordu, yani ortak bir fraksiyon dili yoktu. Yeni `HostilePalette` dört
arketipi tek ılık banda topladı (ton ~350°–22°, doygunluk 0.45–0.56, değer 0.70–0.86); doygunluk
bilinçli düşük tutuldu ki dost SİHA'nın tam doygun turuncusu ve grimsi bina paletiyle çakışmasın.
(2) jet sesi — ilmek artık geniş bantlı: yeni `AudioSynth.AddLoopableNoise` ilmek dikişini
*kaçınarak değil çözerek* halletti (ilmek sonrasının örnekleri de üretilip başa çapraz geçişle
bindiriliyor), üstüne kanat geçiş frekansı + şaft yan bantlı kompresör ıslığı geldi; ayrıca
gök gürültüsü gibi ayrı bir **art yakıcı katmanı** (kameranın FOV tekmesiyle aynı durumdan
tetikleniyor). (3) yakıt — tam gaz menzili jette **21.9 sn** idi (art yakıcıyla 7.3 sn), yani
seviye 1 bile bitmiyordu; tüm depolar **×3** oldu (65.6 / 150 / 385.7 sn), yakım hızlarına
dokunulmadı, sıralama ve oranlar korundu. Ayrıntı `## 9`'un son maddesinde.

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
| 3 | ~~**Denge (bayraklandı, ayarlanmadı)**~~ | ✅ **Kapandı.** Seviye 2 boşluğu `AaaDetectionRange` 60 → **67 m** ile kapatıldı (atış menzili 50 m'de sabit). Gerekçe ve yeni bantlar `## 9`'un son maddesinde. |
| 4 | — | **Henüz karar verilmedi:** gerçek 3D modeller (render hattı kararı + çalışan bir glTF içe aktarıcı gerekir), zorluk seviyeleri. **Ses artık var** (kodla üretilen); eksikleri `## 9`'un son maddesindeki "Yapılmayanlar" listesinde. |

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
| `TargetingSystem` | Menzil + görüş açısı (FOV) içinde tespit, zaman tabanlı kilitlenme. **İmza duyarlı:** `DetectionRange` artık 1 m² (`ReferenceRcs`) hedefe karşı referans menzil; her adayın kendi RCS'i ve karıştırması `SignatureDetection` üzerinden kendi menzilini üretir (`EffectiveRangeFor`). İmzasız anlık görüntüde sonuç birebir eski davranış. |
| `DetectableTarget` | Hedef anlık görüntüsü (id, konum, hız) + **radar imzası ve karıştırma gücü**. `Signature` özelliği ayarlanmamış (≤ 0) imzayı taban 1 m² olarak okur, böylece `default(DetectableTarget)` ve eski kurucular eski menzille aynı davranır. |
| `SignatureDetection` | **Projenin tespit yasası, tek kopya:** menzil ∝ RCS^0.25 (`RangeForRcs`), üstüne `ElectronicWarfare` gürültü karıştırması (`EffectiveRange`), üstüne görüş konisi (`CanDetect`). `RadarSystem` ve `TargetingSystem` buraya delege eder. `DetectionRangeMultiplier` = "şu an ne kadar görünürüm" (sensörden bağımsız tek sayı, HUD bunu gösterir). Bozuk girdiler (≤ 0 RCS/menzil/FOV, ≤ 0 referans RCS) istisna atmadan makul cevap verir. |
| `WeaponSystem` | Atış kontrolü: mühimmat, atış hızı, soğuma, yeniden doldurma. |
| `Ballistics` | Hareketli hedefe önleme (lead) noktası. |
| `Health` | Can havuzu, hasar, imha ve `SetMax` ile **yerinde yeniden boyutlandırma** (mevcut can oranını korur; yok edilmiş havuz dirilmez). |
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
| `EvasionSteering` | Tehditten kaçınma yönü: `Evade` (yana kırma + uzaklaşma bileşeni) ve `BreakTurn` (tehdit kerterizine **tam dik** — füzeyi traversa alan gerçek sert kırış; iki dik yönden mevcut başa yakın olanı seçer, asla füzeye dönmez). |
| `WavePlan` | Dalga başına zorluk ölçekleme: her düşman tipinden kaç tane spawn edileceği (saf). |
| `ScenarioState` | Çok dalgalı senaryo ilerleyişi + kazan/kaybet (dalga temizlenince sonraki, son dalgada zafer). |
| `GunSystem` | Seri atışlı top/makineli: fişek bandı, atış hızı, etkili menzil, dağılma (saf). |
| `HitProbability` | Bir top mermisinin isabet olasılığı: menzildeki dağılma konisi yarıçapı ↔ hedef boyutu. |
| `ThrottleGovernor` | Yakıt durumuna göre kullanılabilir gaz: son %5 rezervde güç azalır, boş depoda sıfırlanır. |
| `SquadStatus` | Filo muharebe kabiliyeti: hiç drone kalmadıysa veya kalanların hepsinin yakıtı bittiyse etkisiz. |
| `CountermeasureSystem` | Flare/chaff atıcı: sınırlı hak, salvo arası soğuma, temel aldatma olasılığı. |
| `MissileThreat` | Gelen füze geometrisi: çarpışmaya kalan süre ve zamanlama+açıya bağlı aldatma şansı; sert kırış sırasında atılan salvo `BreakTurnDecoyBonus` (×1.5) ile ödüllendirilir. |
| `EvasiveManeuver` | İsimli kaçış manevraları (Break/Dalış/Tırmanış/Makara — Break/Dalış/Tırmanış artık `EvasionSteering.BreakTurn` üzerine kurulu) + irtifa duyarlı manevra seçici; `MaxWarningSeconds` (6 sn) ve `BreakWindowSeconds` (2.5 sn) + `InBreakWindow` — kırışın gerçekten işe yaradığı pencere. |
| `ResupplyPoint` | Üste ikmal döngüsü: üs yarıçapı içinde tam süre bekleyince tamamlanır, erken ayrılınca ilerleme sıfırlanır. |
| `ScenarioLibrary` | Görev kütüphanesi: her senaryonun başlığı, brifingi, dalga sayısı ve dalga başına düşman kompozisyonu. |
| `ScenarioKind` | Seçilebilir görev tipleri: Keşif / SEAD / Hava Muharebesi / Karma Savunma. |
| `WaveComposition` | Bir dalganın düşman kompozisyonu (sabit hedef / SAM / AAA / avcı + toplam). |
| `FlightEnvelope` | Sahnenin **dikey zarfı**: en yüksek prop (11 m) + arazi kabartması (`TerrainField.Amplitude` = 3 m) = **14 m silüet tavanı**, üstüne **4 m pay** → `MinCruiseAltitude` = **18 m**. `ClearsStructures`/`ClampToCruiseFloor` ile uçak profilleri ve spawner'lar bu tabana bağlanır; bina büyütülürse test kırılır. |
| `TerrainField` | Deterministik prosedürel arazi yükseklik alanı (Perlin + üs çevresinde düz bölge); hem arazi mesh'i hem de yer birimlerinin yerleşimi bunu kullanır. |
| `RadarScope` | Dünya konumunu burun-yukarı radar skopu koordinatına (−1..1, +Y burun, +X sağ) yansıtır; irtifayı yok sayar (PPI), menzil dışını reddeder. |
| `GunPipper` | Namlu ekseninde belirli bir menzilde merminin vardığı dünya noktası (uçuş süresi = menzil/ağız hızı, düşüş = ½·g·t²); ağız hızı ≤ 0 veya menzil ≤ 0 ise hitscan gibi doğrudan namlu ekseni. |
| `AircraftKind` | Uçulabilir arketipler: Savaş uçağı (`FighterJet`) / SİHA (`Siha`) / Keşif İHA (`Iha`). |
| `AircraftProfile` | Bir arketipin değiştirilemez performans profili: hız/dönüş/pilot hız tavanı, seyir irtifası, yakıt, top (5 değer), füze (adet + menzil), tespit ve radar menzili, **radar imzası (`RadarSignature`, m²)**, **karıştırıcı gücü (`JammerStrength`, kataloğun üçünde de 0)**, can + seçim ekranı için **beş** 0–1 gösterge puanı (beşincisi `StealthRating`). Her alanın Runtime'da gerçek bir tüketicisi vardır; imzayı `SimulationBootstrap` `RcsComponent`'e yazar, düşman sensörleri oradan okur. |
| `AircraftCatalog` | Üç profilin kataloğu: `All`, `Default` (SİHA temel değerleri), `TryGet`/`GetOrDefault` (bilinmeyen id'de hata değil varsayılan) ve klavyeyle sağa/sola dönen `Cycle`. |
| `MissileAgility` | Bir füzenin **yapısal çeviklik sınırı**: `maxTurnRate = maxG · 9.81 / hız` (hızlı füze daha az çevik), dönüş yarıçapı ve komut edilen yönü bu hıza göre bir adımda ulaşılabilir yöne kırpan saf `ClampTurn`. Sıfır/negatif girdilerde dönüş yetkisi yok — asla sıfıra bölme. |
| `CampaignLevel` | Bir kampanya seviyesinin tanımı: 1 tabanlı index, Türkçe ad/brifing, hangi `ScenarioKind`'ı kaç dalga ve dalga rampasının kaçıncı adımından (`StartWaveOffset`) başlatarak uçtuğu, zorluk çarpanı ve ödül parametreleri. `Composition(w)` doğrudan `ScenarioLibrary.Composition`'a iner — paralel senaryo sistemi yok. |
| `CampaignLibrary` | Elle yazılmış 8 seviyelik sıralı rampa. Yalnız "şekil" elle (senaryo, dalga sayısı, ofset, ad/brifing); her sayı index'ten formülle: zorluk `1 + 0.25·(n−1)`, taban ödül `200·zorluk` (10'a yuvarlı), kill başı `25·zorluk`, kayıp cezası sabit 60. Geçersiz index'te `null`/`First`, asla istisna. |
| `CampaignProgress` | Kilit/tamamlanma/en iyi derece. Seviye 1 hep açık, N biterse N+1 açılır, tamamlanan tekrar oynanabilir, derece yalnız yükselir, geçersiz index reddedilir. `Restore` kilitleri kayıttan güvenmez, tamamlanmalardan yeniden türetir. |
| `CampaignReward` | Görev sonu parası: `(taban·yıldızÇarpanı + imha·killÖdülü − kayıp·ceza)·zorluk`, negatife düşmez. `IsFullRate(won, alreadyCompleted)` — tam ödeme yalnız ilk BAŞARILI geçişte; tekrar oynayış ve başarısız sorti `ReplayFactor` (%25) ile ödenir (para musluğu engeli). |
| `Wallet` | Kredi kesesi: `Earn` (pozitif olmayanı yok sayar), `TrySpend` (negatif/yetersizde `false` döner ve bakiyeyi HİÇ değiştirmez), `CanAfford`, `LifetimeEarned`. |
| `UpgradeCatalog` | **Sekiz** yükseltme hattı (Motor/hız, Namlu Gücü/hasar, Füze Yuvası/yeni silah, Kanat-Çeviklik, Gövde Zırhı, Yakıt Tankı, Radar, **Elektronik Harp**). Maliyet tablo değil FORMÜL: `round(BaseCost·1.6^(L−1)/25)·25`; etki de formül: `1 + L·PerLevelGain`. Her hat için Türkçe ad/açıklama ve `EffectSummary`. Elektronik Harp hattı ölçekleyen değil **takan** tek hat: `JammerStrengthAtLevel` (0/1.5/3.0/4.5) + `JammerDetectionFactor` + sabit `JammerBurstSeconds`/`JammerCooldownSeconds`. **Listenin sonunda** durur, böylece kayıtlı seviye dizisinin indeksleri bozulmaz. |
| `UpgradeState` | Hangi hat kaçıncı seviyede. `TryPurchase` önce tavanı ve bakiyeyi kontrol eder, sonra ATOMİK olarak harcayıp seviye atlatır — reddedilen alışveriş ne cüzdanı ne durumu kıpırdatır. `Restore`/`Snapshot` kayıt katmanı için, bozuk diziye karşı kırpmalı. |
| `AircraftUpgrades` | `Apply(baseProfile, upgradeState) → AircraftProfile`: yükseltmeleri taban profilin üstüne ÇARPANLA uygular (taban profiller otoriter kalır), sıfır yükseltmede taban profile birebir eşit döner, tabanı asla mutasyona uğratmaz. Tek sayısal istisna füze yuvası (adet); topu olup füzesi olmayan gövdede yeni yuvanın menzili tespit menzilinden türer. `AffectedFields` hangi hattın hangi alanı oynattığını söyler. |
| `ThreatEnvelope` | Sahanın **yatay zarfı** (`FlightEnvelope`'un yatay muadili): saha yarı-genişliği 40 m, spawn keep-out 32 m, köşe-köşe **113 m**, tipik düşman mevzisinden en uzak noktaya **96.6 m** (`MaxEngagementDistance`). Her düşman arketipinin tespit ve atış menzili tek yerde ve arenaya bağlı olarak tanımlı; `DetectionRangeAgainst`/`FireDistanceAgainst`/`CoversWholeField` denge tablosunu tek çağrıyla ifade eder. Değişmezler (tespit > atış; taban SİHA'ya karşı tespit sahayı KAPLAMAMALI; keşif İHA atış menzilinin dışından görülmemeli) `ThreatEnvelopeTests` tarafından doğrulanır. |
| `JammerSystem` | Karıştırıcının **görev döngüsü**: yayın süresi, soğuma ve şu an yayılan güç (`Ready`/`Active`/`Cooling`). Karıştırmanın fiziği tekrar edilmez — `ElectronicWarfare.EffectiveRange`'e delege eder; bu sınıf yalnız "açık mı?" sorusunu sahiplenir. Yayın iptal edilemez, tuşa basmak süreyi uzatmaz, tek uzun kare soğumayı atlatamaz. `BurstSeconds <= 0` = sürekli yayın (elle kurulmuş sahnelerdeki eski davranış). |
| `AudioSynth` | **Projenin ses sentezi matematiği:** `float[]` PCM tamponu üzerinde toplamalı üretim — sinüs, tek harmonikli ton, analitik fazlı doğrusal süpürme (chirp), deterministik xorshift gürültüsü, **dikişsiz ilmeklenen bant sınırlı gürültü** (`AddLoopableNoise`: filtre ısınma bölgesi + ilmek sonrası örneklerin başa çapraz geçişi, böylece `buffer[0]` tam olarak `buffer[N-1]`'i izleyen örnek olur), **`SnapToLoop`** (bir frekansı ilmek içinde tam çevrim tamamlayacak şekilde oturtur), tek kutuplu alçak/yüksek geçiren filtre, tremolo, normalize zamanlı ADSR ve vurmalı zarf, tepe/ölçek/normalize/kırp. Projede ses varlığı olmadığı için **her ses buradan** doğar; `AudioClip`/`AudioSource` bilmez. Bozuk girdi (null tampon, 0 uzunluk/örnekleme hızı, negatif frekans) istisna atmaz, hiçbir şey yapmaz. |
| `SoundSettings` | Ana ses seviyesi + sessize alma kuralları. Sessize alma "seviyeyi 0 yap" DEĞİL, altındaki seviyeyi hatırlar; `CycleVolume` tek tuşluk merdiveni (%100 → %70 → %40 → KAPALI) yürütür ve merdiven dışı bir seviyeden bile mutlaka hareket eder; `Restore` bozuk/NaN değerde varsayılana düşer. `EffectiveVolume` Runtime'ın mixer'a yazdığı tek sayı. |

### Sim.Runtime (ince MonoBehaviour'lar)
| Bileşen | Görevi |
|---|---|
| `IhaController` | Keşif İHA: uçuş + devriye + radar sensörüyle tespit; yakıt, angajman durumu, atanan hedefe gidiş ve tehdit kaçınması. |
| `SihaController` | Silahlı SİHA (IhaController'dan türer): kilitlenince güdümlü mühimmat fırlatır; mühimmat oranı. |
| `AirDefenseSite` | Düşman hava savunması: drone'ları tespit edip güdümlü mühimmat fırlatır, kendisi de imha edilebilir (SEAD). `Configure(...)` ile uzun menzilli SAM veya kısa menzilli hızlı-ateşli AAA varyantı kurulur. Tespiti **imza duyarlı** (ayarlı menzil = 1 m² hedefe karşı referans); temas kaybı kilidi sıfırlar ve atışı keser. `IsLocked` HUD için salt okunur. |
| `RadarSensor` | RadarSystem + RCS + EW + TargetTracker ile gerçekçi tespit/izleme. |
| `RcsComponent` | Bir birimin radar imzası. `NominalRcs` (açıdan bağımsız) düşman sensörlerinin okuduğu değerdir; `RcsFrom(radarPos)` dost `RadarSensor` için açıya bağlı imzayı verir. `Configure(nominalRcs)` `RadarCrossSection`'ın burun/yan oranlarını koruyarak eğriyi ölçekler. |
| `Jammer` | Gemi üstü gürültü karıştırıcı. `Sim.Core.JammerSystem` üzerine ince sarmalayıcı (tembel `EnsureSystem` + `Configure`, `CountermeasureDispenser` kalıbı). `Strength` **yalnız yayın sürerken** sıfırdan farklıdır, yani boşta duran karıştırıcı hiç yokmuş gibi davranır. Varsayılan (`burstSeconds <= 0`) sürekli yayın = eski davranış. **Artık gerçekten takılıyor:** hangardaki Elektronik Harp hattı 0'ın üstündeyse `SimulationBootstrap` oyuncunun uçağına ekliyor. |
| `GuidedMunition` | PN güdümü + arayıcı başlık + balistik ile güdümlü mühimmat, yakınlık tapası. **Sınırlı takipçi:** güdüm komutu `MissileAgility` ile `maxLoadG`'ye kırpılır, arayıcı başlık güdümü gerçekten kapıya alır (koniden çıkan hedefte `lostLockGraceSeconds` sonra balistik), hedef hızı konum farkından kestirilip **gerçek** PN'e beslenir; `IsGuiding` kancası. |
| `TargetRegistry` / `Targetable` | Canlı hedeflerin kaydı; controller'lar her kare sorgular. |
| `SimulationBootstrap` | Play'de sahneyi primitive'lerden kurar (kamera, ışık, zemin, drone'lar, ScenarioController). Üretilen her şey tek bir `Simulation` kökünün altındadır; `Rebuild()` bu kökü yıkıp yeniden kurar (yerinde yeniden başlatma). |
| `ScenarioController` | Dalga tabanlı senaryo: seçilen göreve göre (`ScenarioLibrary.Composition`) her dalganın düşman karışımını spawn eder ve kazan/kaybet'i yönetir; `BeginMission()` çağrılana kadar bekler. |
| `SimulationDirector` | Görev takibi ve skorlama (dalga güvenli kill sayımı; kazan/kaybet ScenarioController'da, bu yüzden `MissionState` saf sayaç olarak kurulur ve kendi kendine bitmez). |
| `Hud` | Ekran üstü (IMGUI) bilgi paneli: görev, skor, radar temasları; pilot modunda dairesel radar skopu (temaslar + gelen füzeler) ve skopun üstünde **İMZA paneli** (uçağın m² kesit alanı, canlı GİZLİ göstergesi, RADAR TEMASI / RADAR KİLİDİ durumu). |
| `CameraRig` | Serbest uçan kamera (WASD + fare), drone takip modu ve pilot modunda kokpit görünümü (**V** ile geçiş); kokpitte `CockpitFrame`'i açar/kapatır. |
| `CockpitFrame` | Kameraya bağlı prosedürel kokpit içi (gösterge paneli, güneşlik dudağı, ön cam kirişi, A-direkleri, kanopi rayları, hafif cam tonu, önde uçağın burnu); tüm parçalar kameranın gerçek frustum'una oranla ölçeklenir, FOV/en-boy değişince yeniden yerleşir. |
| `ExplosionEffect` | Asset'siz patlama işareti: büyüyüp sönen emisyonlu küre (mühimmat isabeti + imha). |
| `GameControls` | Klavye kontrolleri: R yeniden başlat, P duraklat, +/- zaman ölçeği. |
| `GunTurret` | `GunSystem` + `HitProbability` sarmalayıcısı: hedefe veya serbest nişan noktasına top atışı, izli mermi. |
| `TracerEffect` | Asset'siz izli mermi görseli: `LineRenderer` ile kısa ömürlü parlak çizgi. |
| `EnemyDroneController` | Düşman avcı drone'u: uçar, dost drone'ları tespit eder ve topla taramaya alır (hava muharebesi). |
| `PlayerDroneController` | Pilot modu: oyuncu dost bir drone'u devralıp elle uçurur (C/Tab/W/S/A/D/↑↓/Space/F + Q/E/X). |
| `CountermeasureDispenser` | `CountermeasureSystem` sarmalayıcısı: flare/chaff salvosu, salvo sayacı (füzeler bunu izler), kısa görsel puf. |
| `ScenarioMenu` | Kurulum gerektirmeyen IMGUI kampanya ekranı: SEVİYE kartları ızgarası (ad, brifing, en iyi derece, kilit durumu — kilitli kart tıklanamaz), kalıcı KREDİ göstergesi, `H` ile açılan HANGAR sayfası (hat başına satır, seviye pipleri, bedel, devre dışıyken de nedenini söyleyen SATIN AL butonu), iki adımlı kampanya sıfırlama ve korunan uçak seçim satırı. Açılışta çıkar, sim'i duraklatır, `M` ile tekrar açılır. |
| `CampaignSave` / `CampaignSaveData` | Kampanya kaydı: düz `[Serializable]` DTO + `JsonUtility` + `PlayerPrefs`. Eksik/boş/bozuk/farklı sürümlü kayıt istisna fırlatmaz, "kayıt yok" sayılır; `Clear()` açık sıfırlama yolu. Core tipleri Unity serileştirme özniteliğiyle kirletilmez, eşleme burada. |
| `CampaignSession` | Statik kampanya sahibi (ilerleme + cüzdan + garaj); `Rebuild()` bileşenleri yok ettiği için statik. Tembel yükler, yalnız gerçek kayıt noktalarında yazar (görev bitti / satın alındı). `PlayerProfile` = seçili arketip + yükseltmeler; `CompleteMission` ödülü hesaplar, seviyeyi tamamlar ve kaydeder. |
| `MaterialLibrary` | Standard/URP uyumlu, önbelleklenmiş materyal fabrikası (renk, metalik, pürüzsüzlük, emisyon). |
| `VehicleModelBuilder` | Primitive'lerden araç siluetleri kurar (keşif İHA 19, SİHA 27, savaş uçağı 37, **Patriot tipi SAM bataryası 37**, **AAA topu 14** parça; ayrıca düşman avcısı, yer hedefi); parçaların collider'ları silinir, fizik etkilenmez. `"Model"` çocuğu kökün ölçeğini tersler; `"Fuselage"`/`"Propeller"`/`"Radar"`/`"EngineGlow"`/`"Turret"`+`"TurretBody"` adları animasyon ve kamera kodunun sözleşmesidir. Açılı alt gruplar (SAM rampası, faz dizisi paneli) ölçeksiz **döndürülmüş pivot** üzerinde durur. |
| `EnvironmentBuilder` | Prosedürel arazi mesh'i, üs pisti, gökyüzü/sis/ortam ışığı/güneş ayarı ve prop dağılımı (tamamı görsel, hiçbirinde collider yok). Dağılımda **üç ağaç türü** (kozalaklı 5 / geniş yapraklı 6 / çalı 3 parça) ve **üç bina arketipi** (depo 9 / orta katlı blok 9–11 / kule 11 parça); boy, taç yarıçapı, eğim, arketip ve renk seçimi hep aynı **sabit tohumlu** akıştan çekilir. Yaprak/duvar renkleri 6 + 5 tonluk **kuantalanmış palete** bağlıdır (materyal sayısı ~240 → 17). |
| `HostilePalette` | Düşman fraksiyonunun tek yerde duran **livrezi** (kozmetik): yer hedefi 0.80/0.50/0.44, SAM 0.70/0.33/0.32, AAA 0.85/0.55/0.38, düşman avcı 0.78/0.38/0.45. Dört arketip tek ılık bantta (ton ~350°–22°) durur; doygunluk 0.45–0.56 ve değer 0.70–0.86 aralığında tutulur, çünkü ayrışmayı sağlayan şey budur: dost SİHA'nın livresi tam doygun turuncu (1.00/0.35/0.20), bina/beton paleti ise S≈0.16. Namlu, faz dizisi yüzü ve kanister ağızları `VehicleModelBuilder`'da bilerek karanlık kalır — gövdeler açıldıktan sonra siluetleri okutan tek şey onlar. |
| `VfxLibrary` | Asset'siz efekt primitifleri (emisyonlu parlama, nokta ışığı patlaması, enkaz, saydam duman, şok dalgası halkası, kıvılcım); global efekt bütçesi ile sınırlı, hepsi kendini yok eder. |
| `ScorchMark` | İmha edilen yer birimlerinin arazide bıraktığı, zamanla sönen yanık izi (en fazla 40 iz). |
| `DamageVisuals` | Can oranı düşen birimlerde duman, kritik seviyede alev efekti (yalnızca `Health` okur). |
| `PropellerSpinner` | "Propeller" parçasını hıza bağlı olarak kendi Z ekseninde döndürür. |
| `BankingVisual` | Dönüşlerde yalnızca "Model" çocuğunu yatırır (kök transform'a asla dokunmaz). |
| `TurretVisual` | SAM/AAA'da "Turret" hedefi takip eder, "Radar" tabağı sürekli döner (yalnız çocuk transform'lar). |
| `AudioLibrary` | Asset'siz **ses fabrikası** (`MaterialLibrary`/`VfxLibrary` kalıbı): on bir klip tarifi (patlama, top, füze atışı, füze uyarısı, kaçış uyarısı, UI tık/onay/ret, pervane ilmeği, **turbofan ilmeği** ve **art yakıcı ilmeği**) `AudioSynth` ile üretilip önbelleklenir. Klip **ilk kullanımda bir kez** kurulur, atış başına asla; başarısız üretim null olarak hatırlanır ve tekrar denenmez. İlmekler dikişsiz: her tonal kısmi `Loop()`/`SnapToLoop` ile 2 Hz ızgarasına oturur, gürültü yatakları `AddLoopableNoise`'tan gelir (artık gürültüsüz değiller), zarf yok. Jet ilmeği: çekirdek kükremesi + egzoz tıslaması + şaft tonları + kanat geçiş frekansı (3300 Hz) ve şaft yan bantları (3190/3410 Hz). |
| `AudioDirector` | Sesin ön bürosu: `AudioListener`'ı garantiler (elle kurulan kameranın dinleyicisi yoktur), **12 uzamsal + 1 iki boyutlu** `AudioSource`'u bir kez kurup dönüşümlü kullanır (olay başına bileşen yok; havuz doluysa en eskisini çalar), `PlayAt`/`Play2D` tek giriş noktasıdır ve **N** ana ses tuşunu taşır. Ayar statiktir, `Rebuild()` sesi geri açmaz; sessizken çalma çağrıları hemen döner. |
| `AudioSave` | Ses ayarının `PlayerPrefs` katmanı — kampanya kaydından **ayrı iki anahtar** (`sim.audio.volume`/`sim.audio.muted`), çünkü kampanya blob'unun sürüm çakışması "sıfırdan başla" demektir ve bir ses ayarı kimsenin kampanyasına mal olmamalıdır. Okuma/yazma istisna atmaz. |
| `EngineAudio` | Uçak başına tek döngüsel kaynak; ses yüksekliği ve perdesi hız/azami hız oranını takip eder. Arketip karakteri gövdeden gelir: `"Propeller"` parçası olan (keşif İHA / SİHA) pervane uğultusu, olmayan (savaş uçağı) turbofan kükremesi alır. Oyuncu o uçağı uçarken **2D**, diğer hâllerde uzamsal; duraklamada ve yakıt bitince sıfıra iner. **Art yakıcı** motorun ALTINA bindirilen ikinci bir ilmek: kaynak yalnız o uçak ilk kez art yakıcı yaktığında (yani pratikte sadece oyuncunun uçağında) TEMBEL kuruluyor, ses sönünce durduruluyor. Tetik, kameranın FOV tekmesiyle birebir aynı kaynak: `PlayerDroneController.AfterburnerActive` + uçulan uçağın eşleşmesi. |
| `MissileWarningAudio` | Kokpit füze uyarı tonu. HUD'un `FÜZE!` bandı ve `KAÇIŞ MANEVRASI` satırıyla **aynı iki bayrağı** okur (`MissileIncoming` / `BreakWindowOpen`), böylece göz ile kulak çelişemez: 0.6 sn aralıklı alçak bip, kaçış penceresi açılınca anında 0.2 sn aralıklı yüksek/sert bip. 2D ve ölçekli zamanda (duraklamada susar). |

### Testler (Sim.Tests.EditMode)
Her Core sistemi için bir test dosyası. Toplam **46** test dosyası. Çalıştırma:
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
| `71c7365` | Dünya detayı turu için DEVLOG + SCENE maddeleri. |
| `27da260` | `MissileAgility` (yapısal dönüş sınırı) + `EvasionSteering.BreakTurn` + kırış penceresi sabitleri + flare/kırış birleşimi; hepsi EditMode testli. |
| `136690f` | Füze artık **sınırlı bir takipçi**: yük sınırı, arayıcı başlığın güdümü gerçekten kapıya alması, gerçek PN, hava savunmasında lead'li atış, oyuncuda over-g kırış + soğuma. |
| `f1a2858` | HUD `KAÇIŞ MANEVRASI` ipucu, soğuma/hazır göstergesi ve yenilen atışın tehdit tablosundan düşmesi. |
| `0e36521` | Kampanya çekirdeği: `CampaignLevel`/`CampaignLibrary` (8 seviyelik rampa), `CampaignProgress`, `CampaignReward` — hepsi EditMode testli. |
| `9a8eb3d` | `Wallet`, `UpgradeCatalog`/`UpgradeState` (yedi hat, formül maliyet eğrisi) ve `AircraftUpgrades.Apply` (profil üstüne çarpan) + testleri. |
| `10c310d` | Kampanya kaydı (`CampaignSave`, PlayerPrefs + JsonUtility) ve statik `CampaignSession`; `CampaignReward.IsFullRate` ile tekrar/başarısız sortide %25 ödeme. |
| `60213f9` | Görev artık seçilen kampanya seviyesini uçuyor; oyuncunun uçağı `CampaignSession.PlayerProfile` (arketip + yükseltmeler) ile spawn ediliyor. |
| `14717f4` | Seviye listesi, kalıcı kredi göstergesi ve hangar/yükseltme ekranı (yalnız mevcut `HudTheme` paleti). |
| `b94bc2a` | Görev sonu: sonucun bir kez işlenmesi (para + seviye açma + kayıt) ve raporda kazanılan/toplam kredi ile "SEVİYE N AÇILDI" bildirimi. |
| `709fe2e` | `Health.SetMax` + `Targetable.MaxHealth` property'si: düşman can değerleri artık gerçekten uygulanıyor (B-20). |
| `9e66fa8` | `FlightEnvelope` (Core, testli) ve seyir bandının +6 m ötelenmesi — binalar artık seyir irtifasının altında (B-21). |
| `7cfcc3d` | `SignatureDetection` (tek kopya tespit yasası) + `AircraftProfile.RadarSignature`/`StealthRating` + imza taşıyan `DetectableTarget`; hepsi oran tabanlı EditMode testli. |
| `3705689` | Düşman sensörleri dost uçağın imzasını okuyor: `RcsComponent.Configure/NominalRcs`, imza+karıştırma taşıyan `TargetRegistry.GetSnapshot`, oyuncunun ve dost YZ uçaklarının imzası. |
| `315fa0c` | HUD İMZA paneli (canlı GİZLİ göstergesi + RADAR TEMASI/KİLİDİ) ve uçak kartlarında GİZLİ çubuğu; `AirDefenseSite.IsLocked`. |
| `2f4a97f` | DEVLOG + SCENE: imza duyarlı tespit turu. |
| `b5912d3` | `ThreatEnvelope` (Core, testli): düşman tespit/atış menzilleri saha ölçeğine çekildi — SAM 160→85 / 120→70, AAA 80→60 / 60→50, düşman avcı 130→65 (top 55 sabit). |
| `32aae95` | Hangara **Elektronik Harp** hattı (3 seviye, 350/550/900 kredi, güç 1.5/3.0/4.5) + `AircraftProfile.JammerStrength` + `JammerSystem` görev döngüsü; 21 yeni EditMode testi. |
| `bddf4f9` | Karıştırıcı sahneye takıldı: `SimulationBootstrap` seviye > 0 iken `Jammer` ekliyor, **K** atım tuşu, yayın sürerken yakıt ×1.5, HUD İMZA panelinde karıştırıcı satırı. |
| `cc75866` | Denge: `ThreatEnvelope.AaaDetectionRange` 60 → **67 m** (atış 50 sabit) — seviye 2'deki bedava bant kapandı. |
| `90f4610` | `AudioSynth` + `SoundSettings` (Core, testli): prosedürel ses sentezi ve ana ses ayarı; 20 yeni EditMode testi. |
| `867327e` | Ses altyapısı: `AudioLibrary` (önbellekli klip fabrikası), `AudioDirector` (havuzlanmış kaynaklar + **N** tuşu), `AudioSave`, patlama sesi. |
| `77e4bbc` | Motor sesi: gaza bağlı döngü, `"Propeller"` parçasına göre pervane/türbin, uçulurken 2D. |
| `d0edca9` | Top raporu, füze atış sesi (dost/düşman ayrı) ve gelen füze uyarı tonu (kaçış penceresinde değişiyor). |
| `312685f` | Menü/hangar arayüz sesleri: tık, onay ve ret. |
| `981f847` | DEVLOG: denge + ses turu. |
| `a9bf6c6` | `HostilePalette`: düşman fraksiyonuna açık, ortak ve okunur bir livre (gri yer hedefi + karanlık SAM kalktı); SAM kanister kapakları koyulaştı. |
| `e348035` | `AudioSynth.AddLoopableNoise` + `SnapToLoop` (Core, testli); geniş bantlı turbofan ilmeği, yeni art yakıcı ilmeği ve `EngineAudio`'nun tembel art yakıcı katmanı. |
| `6bb43d9` | Denge: tüm yakıt depoları ×3 (jet 70→210, SİHA 100→300, keşif İHA 180→540), yakım hızları sabit; YZ kanat uçaklarının varsayılanı da 300. |

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
  Oyuncu aynı araçlara **Q** (flare), **E** (art yakıcı) ve **X** (sert kırış / kaçış manevrası,
  2 sn + 6 sn soğuma) ile erişiyor. Füzeler **yenilebilir**: güdüm `maxLoadG`'ye kırpılıyor
  (SAM 6 g, AAA 9 g, SİHA füzesi 18 g), arayıcı başlık koniden düşen hedefte kilidi bırakıyor ve
  hava savunması lead'li ateş ettiği için **düz uçan** hedef kesin vuruluyor — kaçmak için
  çarpışmaya ≤2.5 sn kala kırmak gerekiyor.
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
  **O turda kapsam dışı bırakılan (ARTIK ÇÖZÜLDÜ):** arketiplerin radar kesit alanı (RCS) farkı.
  O tarihte sim'de **dost** bir uçağın RCS'ini okuyan hiçbir şey yoktu (düşman sensörleri düz
  menzil/FOV ile tespit ediyordu), bu yüzden profile ölü bir alan eklenmemişti. Tüketici sonradan
  yazıldı — bkz. `## 9`'un son maddesi: "imza duyarlı tespit".
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
- **Kaçış manevrası düzeltmesi (kullanıcı hata bildirimi: "manevra hareketi ile kaçamıyorum
  roketlerden"):** Sorun **X tuşunun uçuş modeline ulaşmaması değildi** — `PlayerDroneController`
  girdiyi zaten `FlyControlled` içinden `SyncFlightTo`'ya taşıyordu. Kök neden **füzenin tanımı
  gereği yenilemez** olmasıydı; `GuidedMunition`'da üç ayrı kusur:
  **(1) Dönüş sınırı yoktu.** PN ivmesi doğrudan hıza ekleniyor, ardından hız her adımda seyir
  hızına yeniden normalize ediliyordu — yani her ivme komutu **bedava bir yön değişimine**
  dönüşüyordu. Oyuncu ne kadar görüş hattı açısal hızı üretirse üretsin, füze bir karede eşliyordu.
  **(2) Arayıcı başlık hiçbir şeyi kapıya almıyordu.** `SeekerGimbal.Track()`'in dönüş değeri
  atılıyordu; `maxOffBoresightDeg`/`maxSlewRateDeg` süstü, füze kilidi hiç kaybetmiyordu.
  **(3) Güdüm aslında PN değildi.** `relVel = Vector3.zero - _velocity` (hedef sabit varsayılmış)
  yasayı bir **takip (pursuit)** rotasına indirgiyor; onun yük talebi hedef düz uçarken de sert
  kırarken de `1/menzil` gibi büyüdüğü için hiçbir yük sınırı ikisini ayırt edemezdi.
  **Core (testli):** yeni `MissileAgility` (`maxTurnRate = maxG·9.81/hız`, dönüş yarıçapı, saf
  `ClampTurn`), `EvasionSteering.BreakTurn` (tehdit kerterizine tam dik gerçek kırış — PN'i yenen
  geometri budur; `Evade` dokunulmadı), `EvasiveManeuver`'a `MaxWarningSeconds`/`BreakWindowSeconds`
  + `InBreakWindow` ve Break/Dalış/Tırmanış'ın gerçek kırış üzerine kurulması,
  `MissileThreat.DecoyChance(..., breaking)` + `BreakTurnDecoyBonus`.
  **Runtime:** `GuidedMunition` artık **sınırlı bir takipçi** — güdüm `maxLoadG`'ye kırpılıyor,
  arayıcı başlık `lostLockGraceSeconds` (0.4 sn) boyunca hedefi tutamazsa kilit kopuyor ve mühimmat
  1.5 sn balistik süzülüp kendini imha ediyor (`IsGuiding`; yenilen atış HUD tehdit tablosundan ve
  füze ikazından **anında** düşüyor), hedef hızı konum farkından kestirilip gerçek PN'e besleniyor.
  `AirDefenseSite` artık **önleme (lead) noktasına** ateş ediyor (`Ballistics.ComputeInterceptPoint`)
  ve kendi yük sınırını fırlatmada geçiriyor — lead olmadan yük sınırı **düz uçan** hedefi de
  ıskalatırdı; lead ile düz-seyir garantili bir çarpışma rotası olur ve yalnız gerçek bir manevra
  çözümü bozar. `PlayerDroneController`'da **X gerçek bir yetenek**: burun, traversa yönüne pilotun
  normal dönüş hızının **3 katıyla** (over-g atağı) salınıyor (eski kod başı **anında** çeviriyordu —
  hem bedavaydı hem de ekranda hiçbir şey olmuyormuş gibi görünüyordu), süre 1.5 → **2 sn**, üstüne
  **6 sn soğuma**. Yeni `IhaController.Breaking` (kimi steer ediyorsa o yazar) ile kırış sırasında
  atılan flare daha değerli.
  **Ayarlanan oyun değerleri (bilinçli, yalnız bu düzeltmenin gerektirdikleri):** SAM mühimmatı
  **6 g** @85 m/s (~40°/s), AAA mühimmatı **9 g** @95 m/s (~55°/s), SİHA'nın kendi havadan-yere
  füzesi varsayılan **18 g** @180 m/s (~57°/s — sabit yer hedeflerine karşı fazlasıyla yeterli,
  bugünkü öldürücülüğü korur); kaçış manevrası süresi 1.5 → 2 sn + 6 sn soğuma.
  **Oyuncu artık ne yapmalı:** düz uçarsan lead'li atış seni **kesin** vurur. `FÜZE!` ikazı çıkınca
  HUD'daki **KAÇIŞ MANEVRASI** satırını izle: `BEKLE` (amber) iken erken kırma — füze rahat bir
  yükle rotasını düzeltir; satır **kırmızı `ŞİMDİ! [X]`** olduğunda (çarpışmaya ≤2.5 sn) **X**'e bas,
  ideal olarak **Q** flare ile birlikte (kırış sırasındaki salvo ×1.5 değerinde). Uçak traversa
  salınır, füzenin talep ettiği yük yapısal sınırını aşar ve arayıcı başlık koniden düşer.
  **HUD:** `KAÇIŞ MANEVRASI` ipucu (BEKLE / ŞİMDİ! [X] / UYGULANIYOR / DOLUYOR n.n sn) + şarj
  çubuğu, pilot panelindeki `KAÇIŞ` çipi durum ve soğuma gösteriyor, kontrol şeridi
  `X: KAÇIŞ MANEVRASI (SERT KIRIŞ)`. Eşik HUD'da sabit yazılmadı — `EvasiveManeuver`'dan okunuyor.

- **Seviye seviye kampanya + uçak yükseltme (kullanıcı isteği: "bir de seviye seviye bölüm ekleyelim
  lvl1 lvl2 gibi ve savaş uçağımızı geliştirelim, her seviyeden para kazanma açık olsun, yeni
  silahlar ve hız güç gibi"):** Dört dilim, her biri ayrı commit.
  **(1) Kampanya çekirdeği (Core, testli).** `CampaignLevel` bir seviyeyi tanımlar; düşman
  kompozisyonu `ScenarioLibrary.Composition`'a devredilir, paralel senaryo sistemi kurulmaz.
  `CampaignLibrary` 8 seviyelik elle yazılmış rampa: **1** Recon 1 dalga (hava savunması yok) →
  **2** Recon 2 dalga (ilk hafif AAA) → **3** Hava muharebesi 2 dalga (ilk avcılar) → **4** SEAD
  3 dalga (SAM/AAA) → **5** Karma 3 dalga, ofset 1 → **6** Karma 4 dalga, ofset 2 → **7** Hava
  muharebesi 4 dalga, ofset 2 → **8** Karma 5 dalga, ofset 3. Elle yazılan yalnız bu şekildir;
  her sayı index'ten formülle türer (zorluk `1+0.25·(n−1)`, taban ödül `200·zorluk`, kill başı
  `25·zorluk`, kayıp cezası 60). `CampaignProgress` kilit/tamamlanma/en iyi derece kurallarını
  tutar. `CampaignReward` parayı hesaplar ve **tekrar oynayışı para musluğu olmaktan çıkarır**:
  tam ödeme yalnız ilk BAŞARILI geçişte, sonraki her deneme (ve her başarısız sorti) `ReplayFactor`
  %25 ile ödenir.
  **(2) Cüzdan + yükseltmeler (Core, testli).** `Wallet.TrySpend` yetersiz bakiyede `false` döner ve
  hiçbir şeyi değiştirmez. `UpgradeCatalog` yedi hat: **Motor** (hız ×`1+0.08L`, 5 sv),
  **Namlu Gücü** (top hasarı ×`1+0.12L`, 5 sv), **Füze Yuvası** (seviye başına +1 füze ve menzil
  ×`1+0.10L`, 3 sv), **Kanat/Çeviklik** (dönüş ×`1+0.07L`, 5 sv), **Gövde Zırhı**
  (can ×`1+0.10L`, 5 sv), **Yakıt Tankı** (depo ×`1+0.15L`, 4 sv), **Radar** (radar + tespit
  ×`1+0.12L`, 4 sv). Maliyet eğrisi formül: `round(BaseCost·1.6^(L−1)/25)·25` — her seviye bir
  öncekinden ~%60 pahalı. `AircraftUpgrades.Apply` yükseltmeleri taban profilin üstüne **çarpanla**
  uygular; **sıfır yükseltmeyle sonuç taban profile birebir eşittir**, yani yeni oyuncu bugünkü
  uçağın aynısını uçar.
  **(3) Kalıcılık (Runtime).** `CampaignSave` düz DTO + `JsonUtility` + `PlayerPrefs`; eksik ya da
  bozuk kayıt istisna değil taze durum üretir, `Clear()` açık sıfırlama yolu. `CampaignSession`
  statik sahiptir (Rebuild bileşenleri yok ettiği için) ve yalnız kayıt noktalarında yazar.
  **(4) Döngü + arayüz (Runtime).** `ScenarioController` dalga sayısını ve kompozisyonu seçilen
  seviyeden alır, `SelectedKind`'ı seviyeden türetir. `SimulationBootstrap` oyuncunun uçağını
  `CampaignSession.PlayerProfile` ile spawn eder; `Apply` katalog profilinin saf fonksiyonu olduğu
  için `Rebuild()` yükseltmeleri üst üste bindirmez. `ScenarioMenu` artık seviye ızgarası + kredi
  göstergesi + `H` ile hangar (devre dışı satın alma butonu bile nedenini yazar). `Hud`
  `TryBookMissionResult()` ile görev biter bitmez sonucu **bir kez** işler: ödül → `Wallet.Earn` →
  seviye tamamlandı (bir sonraki açılır) → kayıt; rapor ekranında kazanılan/toplam kredi ve
  "SEVİYE N AÇILDI" bildirimi.


### Tur — iki hata düzeltmesi: uygulanmayan düşman canları + seyir bandına giren binalar

**(1) Düşman can değerleri hiç uygulanmıyordu (B-20).** `Targetable.Awake`, `AddComponent`'in
**içinde senkron** çalışıp `Health` havuzunu kuruyor; spawner'ın hemen ardından yazdığı
`targetable.MaxHealth = 120f` gibi satırlar bu yüzden **ölü atamaydı**. Sonuç: SAM (120), AAA (70),
yer hedefi (60) ve düşman avcı (70) — dördü de sahada **varsayılan 100 HP** ile dolaşıyordu.
Düzeltme, çağrı yerlerini tek tek yamamak yerine **yanlış kullanımı imkânsız** kılan yoldan yapıldı:
`Targetable.MaxHealth` artık serileştirilmiş bir alanın üstünde duran bir **property**, setter'ı
`SetMaxHealth`'e yönleniyor ve yeni `Health.SetMax(float)` ile **canlı havuzu yerinde yeniden
boyutlandırıyor**. `SetMax` mevcut can **oranını** korur — dolu birim dolu kalır (yani spawn anında
doğru değeri alır), yarı canlı birim yarı canlı kalır, **yok edilmiş havuz dirilmez** — ve havuz
değiştirilmediği için `DamageVisuals` gibi referans tutan bileşenler etkilenmez. Dört spawn çağrısı
**tek satır bile değişmeden** doğru değeri aldı.

*Denge sonucu:* yalnız SAM **zorlaştı** (100 → 120 HP); AAA (100 → 70), yer hedefi (100 → 60) ve
düşman avcı (100 → 70) **kolaylaştı**. Yani ilk seviyeler kesinlikle daha kolay: Seviye 1 sadece iki
yer hedefi (60 HP), Seviye 2 + hafif AAA (70 HP), Seviye 3 avcılar (70 HP). SAM 120 HP = SİHA'nın
**tam 3 füzesi** (40 hasar) ya da ~2.7 sn nişan üstünde top (10 atış/sn × 4.5 = 45 hasar/sn) — eski
100 HP de zaten 3 füze gerektiriyordu, yani füze ekonomisi değişmedi. Tek dikkat çeken uç durum:
füzesi olmayan **Keşif İHA** (3 hasar, 8 atış/sn = 24 hasar/sn) bir SAM'i düşürmek için **5 sn**
kesintisiz nişan üstünde kalmak zorunda, üstelik SAM 120 m'den atarken İHA'nın topu 45 m — SEAD
(Seviye 4) İHA ile bilinçli olarak zor bir seçim. Değer olarak yeniden ayarlanmadı, yalnız kayda
geçirildi.

**(2) Bina tepeleri seyir bandının içindeydi (B-21).** Aritmetik: kule arketipi kendi tabanından
**10.90 m** yükseliyor (0.50 plint + 7.2 gövde tavanı + 2.30 direk ofseti + 0.90 direk
yarı-yüksekliği), blok anteni 10.54 m, en yüksek çam ~8.7 m; arazi kabartması
(`TerrainField.Amplitude = 3`) bunun altına +3 m ekliyor → **14 m silüet tavanı**. Drone'lar
**10–14 m**'de seyrediyordu: Keşif İHA (12 m) ve SİHA (14 m) çatıların **içinden** geçiyordu (eski
8 m kutularda pay zaten ~1 m idi). Binaları tavanın altına indirmek onları 5 m'ye kırpmak demek
olurdu, o yüzden **siluet korundu ve bant +6 m ötelendi**: yeni Core sistemi `FlightEnvelope`
sayıyı tek yerde tanımlıyor (11 m prop + 3 m arazi + **4 m pay** = **18 m taban**) ve
`FlightEnvelopeTests` her uçak profilinin bu tabanı geçtiğini doğruluyor — bina büyütülüp sabit
güncellenmezse test kırılır. Ötelenen değerler: İHA 12→18, SİHA 14→20, Jet 18→24 (bandın **şekli**
aynı, sadece taşındı), düşman avcı 14→20 (SİHA ile aynı kotta kalsın diye), kanat pilotları
10/12→18/20; avcı spawn kademesi (±2.4 m) `ClampToCruiseFloor` ile tabana yükseltiliyor.

`minAltitude = 5 m` **bilerek değişmedi**: o bir yer çarpma tabanı, siluet payı değil. Yakıtsız
süzülüş onunla yere çakılır, `EvasiveManeuver.Choose` eşiklerini ondan türetir; 18 m'ye çekilseydi
yakıtı biten drone havada patlardı. Yani kasıtlı alçalmalar (dalış, alçak taarruz, dead-stick)
hâlâ siluetin içine inebilir — garanti edilen şey **normal seyrin** oraya girmemesi. Sahnede zaten
hiçbir prop'ta collider yok, dolayısıyla bunun bedeli geçici görsel kesişmeden ibaret.
Üs/düz bölge (r ≤ 45 m, hangarlar 3.2 m), radar skopunun menzil halkaları (yatay projeksiyon) ve
kamera çerçevelemesi (sabit irtifa varsayımı yok) etkilenmedi.

### Tur — imza duyarlı tespit: elektronik harp katmanı nihayet oyuna bağlandı

**Sorun.** Proje `RadarSystem` / `RadarCrossSection` / `ElectronicWarfare` / `RcsComponent` /
`Jammer` ile tam bir elektronik harp katmanı taşıyordu ama **oyuncu açısından tamamen dekoratifti**:
düşman sensörleri (SAM/AAA'nın `AirDefenseSite`'ı ve düşman avcının `EnemyDroneController`'ı) düz
menzil + FOV ile tespit ediyordu, sahada hiçbir **dost** uçak `RcsComponent` taşımıyordu ve hiçbir
şey bir dostun imzasını okumuyordu. Sonuç: üç arketipin boyut farkının **taktik hiçbir sonucu
yoktu**.

**(1) Tespit yasası tek yerde (Core).** Yeni `SignatureDetection` üç parçayı birleştiriyor:
`RangeForRcs` (radar menzil denklemi, menzil ∝ RCS^0.25), `EffectiveRange` (üstüne mevcut
`ElectronicWarfare.EffectiveRange` gürültü karıştırması) ve `CanDetect` (üstüne görüş konisi).
İkinci bir kopya yazılmadı: `RadarSystem` bu fonksiyonlara **delege ediyor** (ve karıştırmalı
`DetectionRange(rcs, jam)` / `CanDetect(..., jam)` aşırı yüklemeleri kazandı), `TargetingSystem` de
öyle. `DetectableTarget` artık konumun yanında **imzayı ve karıştırma gücünü** de taşıyor; ayarlanmamış
imza taban 1 m² okunduğu için `default(DetectableTarget)` ve eski kurucular birebir eski davranışı
veriyor. Testler **oran ve sıralama** üzerinden yazıldı (16× RCS = 2× menzil, ×4 imza = ×√2 menzil,
FOV dışında boyut işe yaramıyor, karıştırma menzili kısaltıyor, ≤ 0 RCS/menzil/FOV ve ≤ 0 referans
RCS istisna atmıyor), böylece ileride ayar değişince suite kırılmıyor.

**(2) Profilde imza.** `AircraftProfile.RadarSignature` (m²) — `RadarCrossSection`'ın zaten
modellediği ölçekte, nominal 1 m² etrafında: **jet 4 · SİHA 1 (taban) · keşif İHA 0.25**. Dördüncü
kök yasası bunu temiz bir ±√2 tespit mesafesine çeviriyor. `AircraftUpgrades` imzayı ve yeni
`StealthRating`'i olduğu gibi geçiriyor — hangar sensör satıyor, gizlilik satmıyor.

**(3) Sahada.** `SimulationBootstrap` oyuncunun uçağına `RcsComponent.Configure(profile.RadarSignature)`
takıyor; dost YZ keşif İHA'ları da keşif arketipinin imzasını alıyor (iki taraf simetrik — düşman
birimleri zaten `RcsComponent` taşıyordu). `TargetRegistry.GetSnapshot` her kayda birimin
`NominalRcs`'ini ve varsa `Jammer.Strength`'ini yazıyor (ikisi de `Targetable`'ın önbelleklediği
referanslardan, alan okuması kadar ucuz). Düşman tespit yolları böylece **tek satır davranış
değişmeden** imza duyarlı oldu.

**Ortaya çıkan menziller** (ayarlı menziller değişmedi; artık 1 m² hedefe karşı referans):

| Sensör (ayarlı) | Jet (4 m²) | SİHA (1 m²) | Keşif İHA (0.25 m²) |
|---|---|---|---|
| SAM tespit 160 m (atış 120 m) | 226 m | **160 m** | 113 m |
| AAA tespit 80 m (atış 60 m) | 113 m | **80 m** | 57 m |
| Düşman avcı tespit 130 m | 184 m | **130 m** | 92 m |

**Karıştırma** artık uçtan uca çalışıyor: bir birime `Jammer` takıldığı anda yukarıdaki her sayı
`(1 + güç)^0.25`'e bölünür (varsayılan güç 4 → ÷1.50; keşif İHA'ya karşı SAM 113 → **76 m**).
Sahnede **hiçbir dost birime jammer takılı değil** ve bu turda yeni bir toplama/yetenek
uydurulmadı — yalnızca yolun çalıştığı doğrulandı.

**Temas kaybı.** `TargetingSystem.UpdateLock(false, …)` `HasDetection`'ı düşürüp `LockProgress`'i
sıfırlıyor; `AirDefenseSite` `!found` durumunda `CurrentTargetId = -1` yazıp **erken dönüyor**
(tehdit uyarısı ve tüm atış yolları atlanıyor), `EnemyDroneController` gunnery'yi `found` şartına
bağlıyor. Yani stealth bir drone kendi menzilinin dışına çıktığında iz gerçekten düşüyor, kilit
sıfırdan kuruluyor. Bu davranış zaten doğruydu; değiştirilmedi, yalnız belgelendi. Yan sonuç:
**atış menzili tespitle sınırlanıyor** — SAM keşif İHA'yı 120 m'den değil, en fazla 113 m'den
vurabiliyor.

**(4) Oyuncuya okunur hâle getirme** (yalnız mevcut `HudTheme` paleti, yeni renk yok). Pilot modunda
radar skopunun **üstünde** bir `İMZA` paneli: başlıkta ham kesit alanı (`1.00 m²`), altında
`GİZLİ` göstergesi (`SignatureDetection.DetectionRangeMultiplier`'dan türetiliyor, karıştırma açılınca
anında uzuyor) ve `×1.41` okuması, en altta durum etiketi — `RADAR TEMASI YOK` (soluk) /
`RADAR TEMASI x2` (amber) / yanıp sönen `RADAR KİLİDİ` (kırmızı, `AirDefenseSite.IsLocked`).
Skopun altındaki füze tehdit satırının aynası: o "bana ne atıyor", bu "beni ne kadar kolay
görüyorlar". Seçim ekranındaki uçak kartlarına beşinci çubuk **GİZLİ** (`StealthRating`) eklendi;
kart satırı bir çubuk boyu büyüdü (120 → 134 px).

**Denge kontrolü (aritmetik).**
- *Keşif İHA gizli mi, görünmez mi?* Saha ±40 m, yani köşeden köşeye en fazla ~113 m. İHA'ya karşı
  SAM tespiti tam **113 m** — sahanın çapı kadar, yani İHA hâlâ neredeyse her yerde görülüyor:
  **gizli, görünmez değil**. Gerçek kazanç düşman **atış** zarfının küçülmesinde: AAA'ya karşı
  tespit **57 m** ama AAA atış menzili 60 m → İHA 57–60 m bandında ateş yemeden dolaşabiliyor;
  SAM'de aynı şekilde 113 m < 120 m. Düşman avcı da İHA'yı ancak 92 m'den fark ediyor (jeti 184
  m'den, yani sahanın her yerinden).
- *Jet Seviye 4'ün SAM'ini atlatabiliyor mu?* Evet, **maruziyeti hiç değişmedi**. SAM'in atış
  menzili 120 m ve tespiti taban hâlde zaten 160 m > 120 m idi; jetin 226 m'ye çıkması ilk atışın
  **ne zaman** geldiğini değiştirmiyor (hâlâ 120 m). Şarjör, atış hızı, mühimmat hızı ve yük sınırı
  da aynı. Jetin imza cezası bugün sadece **düşman avcıların erken toplanması** olarak hissediliyor.
- *Şüpheli görünen değer:* düşman sensör menzilleri sahayı zaten baştan kaplıyor (SAM 160 m vs ±40 m
  saha), bu yüzden imza farkı hak ettiğinden **daha az** hissediliyor. Bu bir ayar sorunu, ama
  görevin kapsamı "ayarlı menzilleri koru" olduğu için **hiçbir değer değiştirilmedi** — kayda
  geçiriliyor: ileride imzayı gerçekten belirleyici yapmak istenirse doğru kaldıraç düşman tespit
  menzillerini saha ölçeğine (~60–100 m) çekmek, imza katsayılarını şişirmek değil.

`docs/SCENE.md`: "sahnede hiçbir nesneye `Jammer` eklenmiyor" notu (B-02'nin içinde) hâlâ geçerli,
ama artık **bedelsiz ve işlevsel** — dost uçaklar `RcsComponent` taşıdığı için nesne envanteri
güncellendi.

---

### Tur — düşman tespit menzilleri saha ölçeğine çekildi (`ThreatEnvelope`)

**Sorun (önceki turun kendi notu).** İmza duyarlı tespit çalışıyordu ama hissedilmiyordu: düşman
tespit menzilleri sahayı zaten baştan kaplıyordu, yani "113 m'den görülüyorum" = "her zaman
görülüyorum" demekti ve ±√2'lik imza farkının gösterecek yeri yoktu.

**Arenanın gerçek boyu (kaynaktan doğrulandı, hiçbir sayıya güvenilmedi).**
`ScenarioController.fieldHalfExtent` = **40 m** (80×80 m kutu), `spawnMinRadius` = **32 m**, yani
düşmanlar 32 m ≤ r ≤ 56.6 m halkasında; tipik mevzi merkezden ~40 m. Köşeden köşeye
**2·40·√2 ≈ 113 m**; tipik bir mevziden oyuncunun uçabileceği en uzak noktaya
**40 + 40·√2 ≈ 96.6 m** (`ThreatEnvelope.MaxEngagementDistance`). Karşılaştırma: eski SAM tespiti
160 m = sahanın **1.4 katı**.

**Yeni `ThreatEnvelope` (Core, testli).** Sayılar tek yerde ve arenaya bağlı; `ThreatEnvelopeTests`
elle aritmetik yapmak yerine ilişkileri doğruluyor: tespit > atış (taban SİHA), taban tespit sahayı
kaplamamalı, keşif İHA atış menzilinin dışından görülmemeli.

| Sensör | Tespit (eski → yeni) | Atış (eski → yeni) | Gerekçe |
|---|---|---|---|
| SAM bataryası | 160 → **85 m** | 120 → **70 m** | 85 m kabaca mevzinin kendi yarısını kaplar; 70 m hâlâ her oyuncu topundan uzun (İHA 45 / SİHA 60 / jet 70), yani SAM uzun menzilli tehdit kimliğini koruyor. |
| AAA | 80 → **60 m** | 60 → **50 m** | Yerel sensör: yalnız kendi mahallesine gireni fark eder. |
| Düşman avcı | 130 → **65 m** | 55 (değişmedi) | 130 m tüm gökyüzüydü; her avcı doğar doğmaz oyuncuya dönüyordu ve loiter yörüngesi hiç uçulmuyordu. |

**Atış menzilleri neden de indi?** Değişmez kural "tespit > atış" — göremediğini vuramazsın
(`AirDefenseSite`'taki erken dönüş). Eski SAM'de 160 > 120 sağlanıyordu ama 120 m zaten saha
çapından uzundu: batarya arenanın her noktasından her noktasına ateş edebiliyordu, yani "standoff"
diye bir konum yoktu. Tespit 85'e inince atışın 120'de kalması onu ölü harfe çevirirdi (etkin atış
mesafesi min(120, 85) = 85 olurdu). 70 m hem tespitin altında kalıyor hem de standoff'u gerçek bir
konum yapıyor. Aynı gerekçeyle AAA 60 → 50. Düşman avcının 55 m'lik topu zaten saha ölçeğindeydi;
yeni tespit menzili **onun etrafında** seçildi, o yüzden değişmedi.

**Sonuçta ortaya çıkan zarf** (`tespit = menzil·RCS^0.25`, `etkin atış = min(atış, tespit)`):

| Sensör | Jet (4 m²) tespit / atış | SİHA (1 m²) tespit / atış | Keşif İHA (0.25 m²) tespit / atış |
|---|---|---|---|
| SAM (85 / 70) | **120 m** / 70 m | **85 m** / 70 m | **60 m** / 60 m |
| AAA (60 / 50) | **85 m** / 50 m | **60 m** / 50 m | **42 m** / 42 m |
| Düşman avcı (65 / 55) | **92 m** / 55 m | **65 m** / 55 m | **46 m** / 46 m |

Arenaya oranla (96.6 m): jet SAM'e karşı %124 (hâlâ her yerden görülür — **göze batan ama
yaşayabilir**, çünkü atış zarfı 70 m'de sabit), SİHA %88, keşif İHA %62 → AAA'ya karşı %44.
Keşif İHA **gizli ama görünmez değil**: SAM onu hâlâ 60 m'den yakalıyor, ama üç sensörün de
**etkin atış mesafesi** tespitine kırpılıyor — yani küçük imza artık doğrudan "daha yakından
vurulurum"a çevriliyor.

**Kampanya kontrolü (aritmetik, hiçbir şey sessizce ayarlanmadı).** `CampaignLevel.cs` menzil
içermiyor — seviyeler yalnız senaryo/dalga/ofset seçiyor, ekonomi değerleri de düşman istatistiği
değil. Yani doğrudan bir tutarsızlık yok. Bayraklanan tek gerçek sonuç:

> **Seviye 2 ("Saha Taraması"), keşif İHA ile artık bedava.** Kompozisyonu Recon dalga 0 + 1 =
> 5 sabit hedef + **1 AAA**. AAA'nın keşif İHA'ya karşı etkin atış mesafesi
> `min(50, 60·0.25^0.25)` = **42.4 m**, keşif İHA'nın topu ise **45 m** → oyuncu, AAA'nın cevap
> veremediği 2.6 m'lik bir bantta duruyor. SİHA'da fark daha ılımlı (top 60 m, AAA cevabı 50 m),
> jette yok (top 70, AAA 50, ama jet 85 m'den görülüyor). Bu bir ayar kararıdır; görev kapsamı
> "kampanyayı yeniden dengele" olmadığı için **hiçbir kampanya değeri değiştirilmedi**, kayda
> geçiriliyor: doğru kaldıraç ya AAA atış menzilini 50 → 55 m'ye çekmek ya da seviye 2'ye ikinci
> bir AAA koymaktır.

> **Not (sonraki tur):** bu boşluk kapatıldı, ama **önerilen iki kaldıraçla da değil** — AAA
> **tespit** menzili 60 → **67 m** yapıldı. Yukarıdaki AAA satırları (`60 / 50`, İHA `42 m`) o
> yüzden tarihsel kayıttır; güncel tablo `## 9`'un son maddesindedir.

---

### Tur — karıştırıcı gerçek, kazanılabilir bir sistem oldu (Elektronik Harp)

**Sorun.** `Jammer`/`ElectronicWarfare` uçtan uca çalışıyordu ama **hiçbir sahne nesnesi `Jammer`
takmıyordu** (`docs/SCENE.md` → B-02'nin içindeki not). Tamamen ölü bir katmandı.

**(1) Hangarda sekizinci hat: "Elektronik Harp".** Mevcut yedi hattın kalıbına birebir uyar
(Türkçe ad/açıklama, seviye sayısı, aynı `BaseCost · 1.6^(L−1)` eğrisi, seviye başına etki).
`UpgradeTrack` listesinin **sonuna** eklendi: kayıtlı seviye dizisinin indeksleri korunuyor ve
hattın var olmadığı zamandan kalan (bir kısa) dizi `Restore` tarafından bu hat için **0** okunuyor.

| Seviye | Bedel | Karıştırma gücü | Tespit menzili çarpanı `1/(1+güç)^0.25` |
|---|---|---|---|
| 0 | — | **0 — karıştırıcı yok** | ×1.00 (taze kayıt hiç değişmez) |
| 1 | 350 | 1.5 | **×0.795** (−%20) |
| 2 | 550 | 3.0 | **×0.707** (−%29) |
| 3 | 900 | 4.5 | **×0.653** (−%35) |

Seviye 2 tam olarak **√2**, yani savaş uçağının 4 m²'lik imza cezasını bir yayın boyunca **tamamen
siler**: yayın yapan bir jet, temiz bir SİHA kadar geç fark edilir (SAM'e karşı 120 → 85 m).
Taban SİHA'da seviye 3'le SAM tespiti 85 → **55.5 m**, AAA 60 → **39.2 m**, düşman avcı 65 →
**42.4 m** düşer.

**(2) `JammerStrength` ölçeklenen değil YAZILAN tek alan.** Kataloğun üç arketipi de 0 ile doğar;
değeri yalnız `AircraftUpgrades.Apply` hattan yazar — garaj gövdeyi küçültmez ama **donanım takar**.
`Apply` durumun saf fonksiyonu olduğu için `SimulationBootstrap.Rebuild()` (tüm kökü yok edip
profili `CampaignSession.PlayerProfile`'dan yeniden türetir) ne çift uygulayabilir ne de bayat bir
karıştırıcı bırakabilir.

**(3) Sürekli mi, tetiklemeli mi? → Tetiklemeli, ve bu bir karar.** Sürekli açık bir karıştırıcı
kalıcı bir istatistiktir (imza hattı gibi), tetiklemeli olan ise **ne zaman** sorusudur — pilotun
mevcut X kaçış manevrasıyla aynı dilde. Görev döngüsü Core'da: `JammerSystem`, `EvasiveManeuver`'ın
yanında, 12 + 9 EditMode testiyle. **6 sn yayın / 14 sn soğuma = %30 duty**; yayın iptal edilemez,
tuşa basmak süreyi uzatmaz, tek uzun kare soğumayı atlatamaz.

**Tuş: K.** `Assets/Scripts/Runtime/` içindeki **her** `KeyCode` tarandı — kullanımda olanlar
W/A/S/D, Q/E/R/P/M/G/H/C/V/F/X, Tab, Space, LeftShift, LeftControl, RightAlt, ok tuşları, Keypad
+/−. **K boş** ve "karıştırıcı"nın baş harfi. Bağlama yeri `PlayerDroneController.HandleAbilities`,
yani Q/E/X'in tam yanı: `GameControls` global sim tuşlarını (yeniden başlat / duraklat / hız /
panel) taşır ve pilot edilen uçağı hiç tanımaz; yetenek tuşlarının bağlama katmanı bu metottur.

**(4) Bedeli var: yayın sürerken yakıt ×1.5.** Mevcut sistemlerin üstüne temiz oturan tek gerçek
dezavantaj bu: art yakıcı zaten aynı `FuelTank` yolundan ×3 yakıyor, karıştırıcı de aynı yoldan
×1.5 yakıyor (`JammerFuelMultiplier`). "Karıştırıcı kendisi bir fenerdir" fikrinin diğer okuması
(seni zaten yakalamış bir düşman izi daha uzun tutar) bunun için **yeni bir hafıza sistemi**
gerektirirdi; kapsam dışı bırakıldı. Boşta duran ya da hiç takılı olmayan karıştırıcının bedeli
sıfırdır.

**(5) HUD.** İMZA panelinde dördüncü satır (yalnız mevcut `HudTheme` renkleri):
`KARIŞTIRICI YOK` (soluk) / `KARIŞTIRICI HAZIR (K)` (gri) / `KARIŞTIRICI AKTİF ×0.71 4.2 sn`
(yeşil) / `KARIŞTIRICI ŞARJ %63` (amber). Panelin üstündeki `GİZLİ` çubuğu ve `×` okuması zaten
canlı karıştırmayı içeriyordu (`Jammer.Strength` yalnız yayın sürerken sıfırdan farklı), bu satır
o sayının **nereden geldiğini** söylüyor. Alt kontrol şeridine `K: KARIŞTIRICI` eklendi.

**(6) Yaşam döngüsü.** `IhaController` karıştırıcıyı `_cm` (flare kutusu) ile birebir aynı şekilde
taşıyor: `Start`'ta `GetComponent`, `Update`'te `Tick` (pilot uçarken de akar), `Resupply`'da
`Rearm`. Elle kurulmuş bir sahneye bırakılan `Jammer` varsayılan olarak **sürekli** yayın yapar
(`burstSeconds <= 0`), yani hiç tick edilmesine gerek yoktur ve eskisi gibi davranır.

`docs/SCENE.md`: "`Jammer` hiçbir birime takılı değil" notu **kapandı**.

---

### Tur — seviye 2 denge boşluğu kapandı (AAA tespit menzili)

**Sorun (bir önceki turda bayraklanmıştı).** Kampanya seviye 2 "Saha Taraması" = Recon dalga 0 + 1 =
**5 sabit hedef + 1 AAA**, yani sahada ateş edebilen tek şey o AAA. Etkin atış mesafesi
`FireDistanceAgainst = min(atış, tespit·RCS^0.25)`; 0.25 m²'lik keşif İHA'ya karşı
`min(50, 60·0.7071) = 42.4 m`, İHA'nın topu ise **45 m**. Arada **2.6 m**'lik, İHA'nın ateş edip
AAA'nın cevap veremediği bir bant vardı → seviye bedavaydı.

**Kaldıraç seçimi: `AaaDetectionRange` 60 → 67 m (atış menzili 50 m'de SABİT).**

- **Önerilen "atış 50 → 55" kaldıracı işe yaramıyor.** İHA terimini **tespit** kırpıyor:
  `min(55, 42.4)` yine **42.4**. Yani İHA hiç etkilenmez, buna karşılık SİHA'nın serbest bandı
  10 → 5 m, jetinki 20 → 15 m düşerdi — üstelik AAA bulunan **her** seviyede (2, 4, 5, 6, 8).
  Yanlış uçaklara, yanlış seviyelerde dokunan bir kaldıraç.
- **"Seviye 2'ye ikinci AAA" da düzeltmiyor.** Geometri aynı kalır (İHA yine ikisini de cevapsız
  vurur), yalnız süre uzar; ayrıca `ScenarioLibrary`'nin Recon kompozisyonunu değiştirmek gerekirdi.
- **Tespit menzili cerrahi knob.** Jet (94.7 m) ve SİHA (67 m) zaten **atış-kırpımlı** olduğundan
  (ikisi de 50 m'lik silahtan uzakta görülür) tespiti oynatmak onların bandını **tam olarak sıfır**
  kadar değiştirir. Yalnız tabandaki 0.25 m²'lik İHA'nın terimi değişir — düzeltilmesi gereken tek
  şey oydu.
- **Neden tam 67?** Çalışan tek pencere: AAA'nın İHA'ya cevap verebilmesi için tespit > `45·√2` =
  **63.6 m**, küçük imzanın hâlâ bir şey satın alması için tespit < `50·√2` = **70.7 m** (aksi hâlde
  İHA da herkes gibi tam 50 m'den vurulur ve `ReconIha_IsShotAtCloserThanTheStatedFireRange`
  değişmezi düşer). **67 = pencerenin orta noktası**: İHA 47.4 m'den, yani kendi topu yetişmeden
  2.4 m önce ateş yer, ama hâlâ SİHA/jetten 2.6 m daha yakında.

**Sonuçtaki AAA zarfı** (`tespit = 67·RCS^0.25`, `etkin atış = min(50, tespit)`):

| Arketip | AAA tespiti | AAA'nın cevap mesafesi | Oyuncu topu | Serbest bant (eski → yeni) |
|---|---|---|---|---|
| Savaş uçağı (4 m²) | 84.9 → **94.7 m** | 50 m (değişmedi) | 70 m | 20 m → **20 m** (aynı) |
| SİHA (1 m²) | 60 → **67 m** | 50 m (değişmedi) | 60 m | 10 m → **10 m** (aynı) |
| Keşif İHA (0.25 m²) | 42.4 → **47.4 m** | 42.4 → **47.4 m** | 45 m | +2.6 m → **−2.4 m (yok)** |

SAM ve düşman avcı zarflarına dokunulmadı; kampanya, ekonomi, can, hasar ve dalga kompozisyonu
değerlerinin hiçbiri değişmedi. Değişmezler korunuyor: AAA tespit (67) > atış (50); SAM tespiti
(85) > AAA tespiti (67); AAA jete karşı bile sahayı kaplamıyor (94.7 < 96.6 m).

**Testler.** `ThreatEnvelopeTests`'te yayımlanan tablo AAA satırı güncellendi (94.75 / 67.00 / 47.38)
ve iki yeni test eklendi: `NoPlayerGun_OutRangesTheAaaItCannotBeAnsweredBy` (boşluğun kendisi bir
değişmez olarak) ve `AaaDetectionChange_LeftTheOtherArchetypesUntouched` (jet/SİHA bandı tam 50 m'de
kalıyor + AAA sahayı kaplamıyor).

---

### Tur — oyunun sesi (tamamı kodla üretilen)

**Kısıt.** Depoda **hiç ses varlığı yok ve içe aktarılamıyor**. Dolayısıyla her ses çalışma zamanında
`AudioClip.Create` ile bir `float[]` örnek tamponu doldurularak **sayıdan üretiliyor**. Mimari kural
gereği sentez matematiği `Sim.Core`'da (`AudioSynth`, EditMode testli), `AudioClip`/`AudioSource`
tarafı `Sim.Runtime`'da ince bir katman.

**Core (testli).**
- `AudioSynth`: toplamalı üreticiler (sinüs, tek harmonikli ton, **analitik fazlı** doğrusal süpürme,
  deterministik xorshift gürültüsü), tek kutuplu alçak/yüksek geçiren filtre, tremolo, normalize
  zamanlı ADSR, vurmalı üstel zarf, tepe/ölçek/normalize/kırp. Testler DSP'nin *gerçekten* doğru
  olduğunu doğruluyor: 100 Hz sinüs saniyede 200 işaret değiştiriyor, 100→300 Hz süpürme **ortalama
  frekansı** kadar salınıyor (faz analitik integre edilmezse tutmaz), zarf 0'da başlayıp 1'e çıkıp
  0'da bitiyor ve hiçbir yerde [0,1] dışına çıkmıyor, filtreler doğru tarafı kesiyor, aynı tohum
  aynı gürültüyü veriyor, bozuk istek (0 uzunluk / 0 örnekleme hızı / null tampon) **boş dönüyor,
  fırlatmıyor**.
- `SoundSettings`: ana ses seviyesi + sessize alma. Sessize alma altındaki seviyeyi hatırlıyor,
  `CycleVolume` tek tuşluk merdiveni yürütüyor, `Restore` bozuk değerde varsayılana düşüyor.

**Runtime.** `AudioLibrary` (önbellekli klip fabrikası, `MaterialLibrary`/`VfxLibrary` kalıbı),
`AudioDirector` (dinleyici garantisi + havuzlanmış kaynaklar + ana ses tuşu), `AudioSave`
(PlayerPrefs), `EngineAudio`, `MissileWarningAudio` ve mevcut yollara eklenen çağrılar.

**Sesler ve nasıl üretildikleri.**

| Ses | Tarif | Nerede |
|---|---|---|
| Patlama | 650 Hz'den alçak geçirilmiş gürültü + 140→24 Hz düşen süpürme, vurmalı zarf | `ExplosionEffect.Spawn` (uzamsal; boyutla yükseklik/perde/menzil) |
| Top | 850 Hz'den yüksek geçirilmiş gürültü (çatlama) + 420→70 Hz gövde, çok hızlı sönüm | `GunTurret.MuzzleFlash` — her iki atış yolu buradan geçer, mermi başına, ±%9 perde |
| Füze atışı | 2.4 kHz'den alçak geçirilmiş gürültü + 60→280 Hz **yükselen** süpürme, ADSR | `SihaController.LaunchMunition` (0.55) ve `AirDefenseSite.LaunchMunition` (0.7, perde 0.85 — düşman atışı daha pes) |
| Füze uyarısı | 760 Hz harmonik ton + 1520 Hz, kısa ADSR bip | `MissileWarningAudio`, 0.6 sn aralık |
| Kaçış uyarısı | 1180 + 2360 Hz, vurmalı ve daha sert | Aynı bileşen, kaçış penceresinde 0.2 sn aralık |
| UI tık | 1400 + 2100 Hz, çok kısa vurmalı, kasıtlı olarak kısık | Sayfa değişimi, uçak kartı, ←/→ |
| UI onay | 660→990 Hz yükselen süpürme + 1320 Hz | Yükseltme alındı, seviye başlatıldı |
| UI ret | 150 Hz harmonik ton + 18 Hz tremolo (vızıltı) | Kredi yetmiyor / azami seviye / kilitli seviye / kampanya sıfırlama |
| Motor (pervane) | 60/120/180/240 Hz yığın + 20 Hz kanat çırpma tremolosu | `EngineAudio`, keşif İHA ve SİHA |
| Motor (türbin) | 110/220/440 Hz gürleme + 3300/4400 Hz kompresör ıslığı | `EngineAudio`, savaş uçağı |

**Dikişsiz ilmek nasıl garantilendi.** Motor ilmekleri 0.5 sn ve **yalnız 2 Hz'in katı** kısmi
frekanslardan oluşuyor (60/120/180/240/1200 ve 110/220/440/3300/4400), tremolo da tepe noktasında
başlıyor. Böylece tampon tam sayıda çevrimle bitiyor ve ilmek noktasında tık olmuyor — bu yüzden
motor seslerinde gürültü ve zarf **yok**.

**Uzamsallık.** Patlama, top, füze atışı ve başkasının motoru **3D** (logaritmik rolloff, min 4–8 m,
maks 90–200 m; arena köşe-köşe 113 m). **2D kalanlar:** arayüz sesleri, füze uyarı tonu ve oyuncunun
**uçtuğu** uçağın motoru (kokpittesiniz; kamera dönünce solmamalı) — `EngineAudio` bunu
`ManualControl` bayrağına bakarak kendisi değiştiriyor.

**Ana ses / sessize alma: N tuşu.** `Assets/Scripts/Runtime/` içindeki **her** `KeyCode` yeniden
tarandı; kullanımdakiler: W/A/S/D, Q/E/R/P/M/G/H/C/V/F/X/K, Tab, Space, LeftShift, LeftControl,
LeftAlt/RightAlt, ok tuşları, +/− (üst sıra ve keypad). Ses için akla gelen bütün mnemonik harfler
(**S**es, **K**ıs, **M**ute, **V**olume, **C**) doluydu; **N boş**. Tek tuş bütün merdiveni
yürütüyor: **%100 → %70 → %40 → KAPALI → %100** (susmak en fazla üç basış). Her basış küçük bir tık
ile onaylanıyor, HUD kontrol şeridinde `N: SES %70` / `N: SES KAPALI` yazıyor. Ayar `PlayerPrefs`'te
**kampanya kaydından ayrı** iki anahtarda saklanıyor: kampanya blob'unun sürüm çakışması "sıfırdan
başla" demektir ve bir ses ayarı kimsenin kampanyasına mal olmamalı.

**Bütçe.** Klipler ilk kullanımda bir kez üretilip oturum boyunca saklanıyor (atış başına asla).
Tek seferlik sesler `AudioDirector`'ın **12 uzamsal + 1 iki boyutlu** kaynaklık havuzundan geçiyor;
havuz doluysa en eski ses çalınıyor, havuz **büyümüyor** — `VfxLibrary`'nin efekt bütçesiyle aynı
mantık. Sürekli sesler (motor, uyarı tonu) kendi tek kaynağını `Start`'ta kurup ömür boyu yeniden
kullanıyor. Sessizken (`IsSilent`) çalma çağrıları hemen dönüyor, hiçbir şey konumlandırılmıyor.

**Dayanıklılık.** Klip üretilemezse `null` dönüyor ve her çağrı sessizce no-op oluyor; `AudioDirector`
yoksa (elle kurulmuş sahne) çağrılar yine no-op; `AudioListener` yoksa `AudioDirector` onu ana
kameraya kendisi ekliyor — bu olmadan oyun, hiçbir ses ayarının açıklayamayacağı biçimde tamamen
sessiz kalırdı.

**Yapılmayanlar (bilinçli).** Müzik yok; çarpma/isabet sesi ayrı değil (patlama yolunu kullanıyor);
düşman avcılarının motor sesi yok (uçak başına bir kaynak, kalabalık dalgada bütçeyi bozar);
karıştırıcı/flare için ayrı ses yok (art yakıcının artık kendi katmanı var, bir sonraki maddeye
bakın); ses seviyesi ekranda kaydırıcı değil, tek tuşluk merdiven.

---

### Tur — cila: düşman paleti, jet sesi ve uçuş menzili

Kullanıcının oynarken bildirdiği üç madde; üçü ayrı dilim, ayrı commit.

**1) Düşman renkleri açıldı ve bir fraksiyon oldu** (kullanıcı: *"düşman hedeflerin bazılarının
renkleri gri, onları daha açık bir renk ile değiştirelim"*).

Gri okuyan iki birim vardı ve ikisi de gerçekten griydi:

| Birim | Eskiden | Şimdi | Not |
|---|---|---|---|
| Yer hedefi | `0.50, 0.50, 0.50` (tam orta gri) | `0.80, 0.50, 0.44` | En açık üye; artık binaların tan/gri paletine karışmıyor. |
| SAM bataryası | `0.50, 0.05, 0.05` (parlaklık **0.19**, neredeyse siyah bordo) | `0.70, 0.33, 0.32` | Ailenin ağır üyesi; parlaklık 2.3 katına çıktı. |
| AAA | `1.00, 0.55, 0.10` (parlak turuncu) | `0.85, 0.55, 0.38` | En ılık/açık üye — topu bataryadan ayıran ipucu korunuyor. |
| Düşman avcı | `0.55, 0.10, 0.60` (mor) | `0.78, 0.38, 0.45` | Gül/şarap; eski morun izini taşıyor ama artık fraksiyonun içinde. |

Dördü tek bir ılık bantta (ton ~350°–22°) ve tek bir dosyada: `Assets/Scripts/Runtime/HostilePalette.cs`.
Bant bilinçli olarak **doygunluğu düşük** (0.45–0.56) ve **değeri yüksek** (0.70–0.86) — ayrışmayı
sağlayan şey bu: dost SİHA'nın livresi tam doygun turuncu (`1.00, 0.35, 0.20`, S 0.80 / V 1.00),
bina ve beton ise S≈0.16, arazi yeşil. HUD zaten düşmanı kırmızı kodladığı için (`HudTheme.Critical`)
dünya renkleri artık HUD'un diliyle çakışmıyor, onu tekrarlıyor.

Siluetler için **karanlık parçalar karanlık kaldı** (namlular, faz dizisi yüzü, tekerlekler). Tek
model değişikliği: SAM kanister ağzındaki dört kapak `accent`'ten `dark`'a alındı — gövde açık tuğla
rengine çıkınca açık kapaklar kanisterlere karışıyor ve dört ağız okunmuyordu.

Kozmetik: hiçbir oyun değeri değişmedi.

**2) Jet sesi ve gök gürültüsü gibi art yakıcı** (kullanıcı: *"savaş uçağının sesinde daha çok bir
jet sesi, gök gürültüsüne benzer (art yakıcı çalıştığında)"*).

Eski jet ilmeği beş sinüstü (110/220/440/3300/4400 Hz) — matematiksel olarak dikişsiz ama kulakta
bir sentezleyici, motor değil. Eksik olan şey **gürültüydü**, ve gürültü bilerek yoktu: filtrelenmiş
gürültü öylece ilmeklenemez, çünkü filtreler sıfırdan başlar; tampon sessizlikle açılır, akışın
rastgele bir değeriyle kapanır ve sarma noktası ilmek başına bir "tık" olur.

Bu tur dikiş **kaçınılarak değil, kaynağında çözüldü** — `Sim.Core.AudioSynth.AddLoopableNoise`:

1. atılacak bir **ısınma bölgesi** üretilip filtrelenir, böylece saklanan malzeme filtrenin kalıcı
   rejimidir, açılış geçici rejimi değil;
2. gürültü akışı tampon uzunluğu **artı bir geçiş bölgesi** kadar üretilir — yani ilmek noktasından
   *sonra* gelecek örnekler de hesaplanır;
3. bu devam örnekleri tamponun başına **çapraz geçişle** bindirilir. Sonuçta `buffer[0]` tam olarak
   `buffer[N-1]`'i izleyen örnektir; sarma, aynı akışın sıradan bir adımıdır, süreksizlik değil.
   İlmek şansa değil, **tasarım gereği** dikişsiz.

Yanına ikinci bir Core yardımcısı geldi: `SnapToLoop(frekans, ilmekSüresi)` bir kısmi frekansı ilmek
içinde tam sayıda çevrim tamamlayacak en yakın değere oturtur. Dikiş güvenliği artık elle seçilmiş
yuvarlak sayılara değil mekanik bir kurala bağlı, bu yüzden tarifler istedikleri frekansı
isteyebiliyor (ör. 3300 − 110 Hz). Her ikisi de EditMode testli.

Yeni **turbofan ilmeği**: çekirdek kükremesi (LP 1100 / HP 70) + egzoz tıslaması (2.6–9 kHz) +
şaft tonları (110/220/440 Hz) + kompresör ıslığı — ıslık tek ton değil, **kanat geçiş frekansı**
(3300 Hz = 30 kanat × 110 Hz) ve onun **şaft hızındaki iki yan bandı** (3190 / 3410 Hz); yan bantlar
temel tonla vurarak jete özgü metalik kenarı veriyor.

Yeni **art yakıcı katmanı** (`AudioLibrary.EngineAfterburner`): 22–240 Hz ağır gürleme yatağı,
180–1500 Hz cızırtı, 46/92 Hz alt tonlar ve birbiriyle vuran **iki yavaş tremolo** (6 ve 14 Hz), yani
seviye oturmuyor, yuvarlanıyor. "Aynı ses ama daha yüksek" değil, ayrı bir olay. `EngineAudio` bunu
motorun ALTINA ikinci bir `AudioSource` olarak bindiriyor; kaynak **tembel** kuruluyor (yalnız o uçak
ilk kez art yakıcı yaktığında — pratikte sadece oyuncunun uçağı, YZ filosu ikinci bir ses kanalı
ödemiyor) ve ses sönünce durduruluyor. Tetikleyici, kameranın FOV tekmesinin okuduğu durumun
birebir aynısı: `PlayerDroneController.AfterburnerActive` **ve** o uçağın uçulan uçak olması.
Klip üretilemezse katman sessizce atlanıyor, hiçbir şey fırlatmıyor.

**3) Yakıt: uçuş menzili ×3** (kullanıcı: *"uçakların yakıtları hemen bitiyor, onları biraz
artıralım"*).

Önce bugünkü menzil ölçüldü (tam gaz = kapasite ÷ yakım hızı):

| Arketip | Eski depo | Tam gaz | Art yakıcı ×3 | Art yakıcı + karıştırıcı ×4.5 |
|---|---|---|---|---|
| Savaş uçağı | 70 @ 3.2/sn | **21.9 sn** | 7.3 sn | 4.9 sn |
| SİHA | 100 @ 2.0/sn | **50.0 sn** | 16.7 sn | 11.1 sn |
| Keşif İHA | 180 @ 1.4/sn | **128.6 sn** | 42.9 sn | 28.6 sn |

Seviye süresiyle karşılaştırma (dalgalar arasında bekleme **yok**, bir dalga temizlenince sonraki
hemen doğuyor): seviye 1'de 2 düşman, 2'de 6, 3'te 5, 4'te 12, 5'te 24, 6'da 50, 7'de 22 avcı,
8'de 85. Düşman başına gerçekçi ~10 sn ile kabaca **20 / 60 / 120 / 240 / 500 / 850 sn**. Yani savaş
uçağı **seviye 1'i bile** bitiremiyordu; 22 saniye, 80×80 m arenada bir turdan ve bir atış
geçişinden kısa.

Değişiklik: **tüm depolar ×3, yakım hızları sabit.**

| Arketip | Yeni depo | Tam gaz | Art yakıcı | Art yakıcı + karıştırıcı |
|---|---|---|---|---|
| Savaş uçağı | 70 → **210** | **65.6 sn** | 21.9 sn | 14.6 sn |
| SİHA | 100 → **300** | **150.0 sn** | 50.0 sn | 33.3 sn |
| Keşif İHA | 180 → **540** | **385.7 sn** | 128.6 sn | 85.7 sn |

Tek ve düzgün bir çarpan olduğu için sıralama ve bütün oranlar aynen korunuyor (keşif İHA hâlâ jetin
5.9 katı), dolayısıyla seçim ekranındaki **dayanıklılık puanlarına dokunmak gerekmedi**. Bir bacak
artık birkaç angajmanı artı eve dönüşü kaldırıyor; uzun seviyelerin kalanını üssün ikmal döngüsü
karşılıyor (zaten sistemin tasarımı bu — YZ de bingo'da üsse dönüyor).

YZ kanat uçakları (`IHA_1`/`IHA_2`) profil almıyor, `IhaController`'ın serialize edilmiş
varsayılanını uçuyor; o da SİHA tabanı olduğu için **100 → 300** yapıldı. Bu ihmal edilecek bir
ayrıntı değil: `SquadStatus` sağ kalan **herkesin** deposu boşalınca görevi başarısız sayıyor.

**Yakıt yükseltme hattı ölmedi.** 4 seviye × %15 = ×1.6 kapasite; uzun seviyelerde (6/7/8) hâlâ
bir-iki ikmal turu kazandırıyor. Yalnız keşif İHA için zayıf bir alım hâline geldi (385.7 sn zaten
6. seviyeye kadar her şeyi kapıyor) — ama o zaten dayanıklılık arketipi. Öneri, ileri bir tur için:
ya keşif İHA'ya hattın etkisini menzil yerine **karıştırıcı/radar süresi** gibi başka bir kaynağa
bağlamak, ya da depoyu değil **yakım hızını** düşüren bir üst seviye eklemek. Ekonomi bu dilimde
bilerek elden geçirilmedi.

`ResupplyPoint`, yakıtsız süzülme yolu ve `FlightEnvelope`/irtifa mantığı **hiç değişmedi**.

