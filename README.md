# İHA / SİHA Simülasyonu

3 boyutlu, masaüstü bir Unity simülasyonu: insansız hava aracı (İHA) ve silahlı insansız hava aracı (SİHA) için soyut, oyunlaştırılmış mekaniklerle geliştirilmiş **eğitim / oyun** amaçlı bir simülasyon. Gerçek bir askeri sistem değildir.

## Önerilen Unity Sürümü

- **Unity 2022.3 LTS** (proje `2022.3.40f1` ile oluşturulmuştur).

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
2. Unity Hub'da **2022.3 LTS** ile klasörü açın.

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

## English Summary

**İHA / SİHA Simulation** is a 3D Unity desktop simulation of an unmanned aerial vehicle (İHA) and an armed unmanned aerial vehicle (SİHA), built with abstract, gamified mechanics as an educational/game project — not a real military system.

- **Recommended Unity version:** 2022.3 LTS.
- **Architecture:** `Sim.Core` (pure, testable C# logic — flight, navigation, targeting, weapons, ballistics, health), `Sim.Runtime` (MonoBehaviour glue for the 3D scene), `Sim.Tests.EditMode` (NUnit unit tests).
- **Open:** clone the repo and open the folder in Unity Hub with 2022.3 LTS.
- **Run the demo:** create an empty scene, add a GameObject, attach `SimulationBootstrap`, and press Play.
- **Run tests:** `Window > General > Test Runner > EditMode > Run All`.
- The physical/visual scene is built at runtime by `SimulationBootstrap`; 3D models and materials can be swapped in later.
- **Test-driven:** the Core logic is developed with unit tests first.
