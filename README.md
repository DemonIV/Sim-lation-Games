# İHA / SİHA Simülasyonu

3 boyutlu, masaüstü bir Unity simülasyonu: insansız hava aracı (İHA) ve silahlı insansız hava aracı (SİHA) için soyut, oyunlaştırılmış mekaniklerle geliştirilmiş **eğitim / oyun** amaçlı bir simülasyon. Gerçek bir askeri sistem değildir.

## Önerilen Unity Sürümü

- **Unity 6 (6000.5.9f1)** (proje bu sürüm için yapılandırılmıştır).

> **Not:** Proje Unity 6 (`6000.5.9f1`) için yapılandırılmıştır ancak gerektiğinde 2022.3 LTS ile de açılabilir. İlk açılışta Package Manager paketleri güncellemeyi önerebilir; öneriyi kabul edin.

## Mimari

Proje, test edilebilirliği ön planda tutan üç katmana ayrılmıştır:

- **`Sim.Core`** (`Assets/Scripts/Core`): Saf, MonoBehaviour içermeyen, tamamen birim testi yazılabilir C# oyun mantığı.
  - `FlightModel` — kinematik uçuş modeli (hız, ivme, dönüş oranı)
  - `WaypointNavigator` — sıralı rota (waypoint) takibi
  - `TargetingSystem` — menzil/görüş açısı (FOV) içinde hedef tespiti ve zamana bağlı kilitlenme
  - `WeaponSystem` — şarjör, atış hızı, bekleme (cooldown) ve yeniden doldurma
  - `Ballistics` — hareketli hedefler için kesişim (intercept) noktası hesabı
  - `Health` — can (hit-point) havuzu
- **`Sim.Runtime`** (`Assets/Scripts/Runtime`): Çekirdek mantığı 3B sahneye bağlayan ince MonoBehaviour katmanı.
  - `IhaController` — keşif dronu (uçuş + devriye + hedef tespiti)
  - `SihaController` — silahlı drone (İHA davranışı + silah sistemi)
  - `TargetRegistry` / `Targetable` — canlı hedeflerin kaydı ve hasar mantığı
  - `SimulationBootstrap` — sahneyi çalışma anında primitiflerden kuran önyükleyici
- **`Sim.Tests.EditMode`** (`Assets/Tests/EditMode`): Tüm Core sistemleri için NUnit birim testleri.

## Projeyi Açma

1. Depoyu klonlayın.
2. Unity Hub'da **Unity 6 (6000.5.9f1)** ile klasörü açın (gerektiğinde 2022.3 LTS de kullanılabilir).

## Demoyu Çalıştırma

1. Yeni bir boş sahne oluşturun.
2. Boş bir GameObject ekleyin.
3. Üzerine `SimulationBootstrap` bileşenini ekleyin.
4. **Play**'e basın — kamera, ışık, zemin, İHA/SİHA dronları, devriye rotaları ve hedefler otomatik oluşturulur.

## Testleri Çalıştırma

`Window > General > Test Runner > EditMode > Run All`

## Notlar

- Fiziksel/görsel sahne, `SimulationBootstrap` tarafından **çalışma anında** kurulur. Primitifler yer tutucudur; 3B modeller ve materyaller sonradan değiştirilebilir.
- **Test odaklı geliştirme:** Çekirdek (Core) oyun mantığı önce birim testleriyle geliştirilir; MonoBehaviour'lar ince sarmalayıcılar olarak tutulur.

---

## Gerçekçi Fizik ve Elektronik Harp Katmanı

Simülasyona, ders kitabı fiziğiyle soyutlanmış gerçekçi bir fizik + elektronik harp (EH) katmanı eklenmiştir. Tüm çekirdek sistemler saf mantıktır ve EditMode birim testleriyle kapsanmıştır.

### Yeni Çekirdek Sistemler (`Sim.Core`)

- **`Atmosphere`** — üstel atmosfer modeli: irtifaya bağlı hava yoğunluğu (deniz seviyesinde 1.225 kg/m³, 8500 m ölçek yüksekliği).
- **`BallisticProjectile`** — nokta-kütle balistik entegratörü: yerçekimi, karesel aerodinamik sürükleme, rüzgar ve irtifaya bağlı hava yoğunluğu; yarı-örtük (semi-implicit) Euler.
- **`RadarSystem`** — basitleştirilmiş monostatik radar: tespit menzili RCS'nin dördüncü köküyle ölçeklenir (radar menzil denklemi), hüzme genişliği ve görüş hattıyla sınırlıdır.
- **`RadarScan`** — tek bir radar taraması: aday listesine menzil denklemini, karıştırma kaynaklı menzil düşümünü ve hüzme sınırını uygulayıp en yakın teması döndürür.
- **`RadarCrossSection`** — açıya (aspect) bağlı radar kesit alanı: burun/kuyruk yönü en düşük, yan (broadside) en yüksek.
- **`ElectronicWarfare`** — EH etkileri: gürültü karıştırması (jamming) etkin tespit menzilini/yakma (burn-through) menzilini düşürür; ECM kilitlenme olasılığını azaltır.
- **`TargetTracker`** — alfa-beta filtresi: gürültülü konum ölçümlerinden konum ve hız kestirimi.
- **`SeekerGimbal`** — gimbal üzerindeki arayıcı başlık: azami dönüş oranı ve azami eksen-dışı (off-boresight) açı sınırlarıyla hedefe yönelir.
- **`ProportionalNavigation`** — oransal seyrüsefer güdüm yasası: görüş hattı dönüş oranıyla orantılı yanal ivme komutu; kapanma hızı hesabı.
- **`MunitionAutopilot`** — güdümlü mühimmat otopilotu: g-limitiyle sınırlanmış oransal seyrüsefer yanal komutu ile hızı seyir hızına çeken eksenel itki komutunu birleştirir. Arayıcı hedefi kaybettiğinde yalnızca itki kalır, mühimmat serbest uçuşa geçer.

### Yeni Çalışma Zamanı Bileşenleri (`Sim.Runtime`)

- **`RadarSensor`** — naif tespiti değiştiren gerçekçi sensör: sahnedeki açıya bağlı RCS ve karıştırma değerlerini toplayıp `RadarScan`'e verir, sonucu alfa-beta filtresinden geçirir. Taramayı `IhaController` sürer (`Scan(dt)`), böylece drone hareket ettikten sonraki güncel konumla çalışır. **İHA/SİHA'nın tespiti artık buradan gelir** — RCS ve karıştırma doğrudan oyun mekaniğine etki eder.
- **`RcsComponent`** — bir Targetable'a açıya bağlı radar imzası ekler.
- **`Jammer`** — düşman radar tespit menzilini kısaltan onboard gürültü karıştırıcı.
- **`GuidedMunition`** — SİHA güdümlü mühimmatı: güdüm/gaz kolu mantığı `MunitionAutopilot`'ta, yerçekimi ve sürükleme `BallisticProjectile`'da. `SeekerGimbal` güdümü kapılar — hedef arayıcının eksen-dışı konisinden çıkarsa takip kaybedilir ve mühimmat serbest uçuşa geçip ıskalar. Yakınlık tapasıyla hasar verir. `SihaController` kilitlenince anında hasar yerine bu mühimmatı fırlatır.

Bu sistemlerin tamamı **EditMode birim testleri** ile kapsanmıştır (`Assets/Tests/EditMode`).

---

## English Summary

**İHA / SİHA Simulation** is a 3D Unity desktop simulation of an unmanned aerial vehicle (İHA) and an armed unmanned aerial vehicle (SİHA), built with abstract, gamified mechanics as an educational/game project — not a real military system.

- **Recommended Unity version:** Unity 6 (6000.5.9f1); also opens in 2022.3 LTS if needed.
- **Architecture:** `Sim.Core` (pure, testable C# logic — flight, navigation, targeting, weapons, ballistics, health), `Sim.Runtime` (MonoBehaviour glue for the 3D scene), `Sim.Tests.EditMode` (NUnit unit tests).
- **Open:** clone the repo and open the folder in Unity Hub with Unity 6 (6000.5.9f1).
- **Run the demo:** create an empty scene, add a GameObject, attach `SimulationBootstrap`, and press Play.
- **Run tests:** `Window > General > Test Runner > EditMode > Run All`.
- The physical/visual scene is built at runtime by `SimulationBootstrap`; 3D models and materials can be swapped in later.
- **Test-driven:** the Core logic is developed with unit tests first.
