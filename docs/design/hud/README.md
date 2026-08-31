# HUD Tasarım Mockup'ı

Bu klasör oyunun **HUD tasarım mockup'ını** içerir. Bu tasarım **henüz Unity'ye uygulanmadı** —
hedeflenen görsel, yani ulaşmak istediğimiz görünüm.

## Nasıl açılır

`iha-siha-hud.html` dosyasını bir tarayıcıda aç (dosyaya **çift tıkla**). Dört artboard'lı bir
tasarım tuvali açılır; tuvali **kaydırıp yakınlaştırabilir**, tasarımı **PNG/PDF olarak dışa
aktarabilirsin**. Dosya kendi kendine yeter (self-contained), sunucu veya kurulum gerekmez.

## Dört artboard

| Artboard | İçerik |
|---|---|
| **Ana Muharebe HUD** | Görev/dalga durumu, drone listesi, radar temasları, skor. |
| **Pilot Modu HUD** | Elle uçuş göstergeleri: hız, irtifa, yakıt, mühimmat, nişangâh, karşı tedbirler. |
| **Görev Seçim Menüsü** | Senaryo brifing ekranı: görev başlığı, açıklaması, dalga sayısı. |
| **Görev Sonu Ekranı** | Sonuç, yıldız derecelendirmesi, skor dökümü ve istatistikler. |

## Tipografi ve palet

**Tipografi:** Barlow Condensed (etiketler) + JetBrains Mono (sayısal okumalar).

**Renkler:**

| Rol | Kod |
|---|---|
| Zemin | `#07090a` |
| Panel | `#0e1214` / `#12181a` |
| Çizgi | `#2b3336` |
| Kehribar aksan | `#e2a13f` |
| Dost / OK | `#35c39a` |
| Düşman / kritik | `#e05243` |
| Metin | `#ece7dd` |
| İkincil metin | `#8f8a80` |

**Bar renk kodu:** yeşil > %50, kehribar %20–50, kırmızı < %20.

## Kaynak dosyalar

`.dc.html` dosyaları (`Main`, `PilotHud`, `MissionSelect`, `MissionEnd`) ve `canvas.json`
tasarımın **kaynağıdır**; değişiklik istendiğinde tuval bunlardan yeniden üretilir.

## Not

Bu tasarım Unity'de **düz dolgular, ince kenarlıklar, çubuk göstergeler ve metinle** uygulanabilecek
şekilde tasarlandı — bulanıklık (blur) veya karmaşık degrade kullanılmadı.
