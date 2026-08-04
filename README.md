# IGI_GAME_JAM-mbg

Game jam project — Unity 6000.4.8f1, URP 17.4.0 (2D Renderer), New Input System.

Aturan kerja untuk kontributor (manusia maupun AI) ada di [CLAUDE.md](CLAUDE.md). **Baca dulu sebelum menyentuh scene.**

## Setup scene (sekali saja)

1. **Import TMP Essentials:** `Window > TextMeshPro > Import TMP Essential Resources`.
   Package TextMeshPro sendiri sudah ikut `com.unity.ugui 2.0.0`, tapi TMP Settings dan font
   default belum ada di project sampai langkah ini dijalankan. Tanpa ini, `Build UI Hierarchy`
   akan menolak jalan.
2. Buka `Assets/Scenes/SampleScene.unity`.
3. Jalankan menu **Tools > MBG > Setup Systems Object**. Tool ini membuat GameObject root `__Systems`
   berisi `GameBootstrap`, `GameClock`, `AudioService`, dan `GameManager`.
4. Jalankan menu **Tools > MBG > Build UI Hierarchy**. Tool ini melengkapi `UICanvas` yang sudah ada
   dengan `UIManager`, enam panel, dan asset `UIStyle` — serta menukar Label prompt dari legacy
   `Text` ke `TextMeshProUGUI`.
5. Jalankan menu **Tools > MBG > Build Kitchen Stations**. Tool ini membuat `KitchenLayout.asset`,
   prefab `Station_Generic`, dan menata empat station di bawah `InteriorRoot/Stations`.
6. Jalankan menu **Tools > MBG > Build QTE Setup**. Tool ini membuat tiga preset kesulitan,
   memasang `QTEController` di `__Systems`, dan mengisi `QTEPanel` dengan bar, zona, dan indikator.
7. Jalankan menu **Tools > MBG > Build Catering Data**. Tool ini membuat resep, pemesan, tiga
   pesanan contoh, dan memasang `CateringController` di `__Systems`.
8. Jalankan menu **Tools > MBG > Build HUD**. Tool ini mengisi `HUDPanel` dengan kartu pesanan,
   uang & skor, timer, petunjuk langkah, dan slot gangguan.
9. Jalankan menu **Tools > MBG > Build Result Panel**. Tool ini mengisi layar hasil pesanan.
10. Jalankan menu **Tools > MBG > Build Menu Panels**. Tool ini mengisi menu utama, cara bermain,
    jeda, ringkasan hari, layar kalah, dan membuat `EventSystem` yang dibutuhkan tombol UI.
11. Jalankan **Tools > MBG > Build Catering Data** sekali lagi supaya `ResultPanel` dan `DayManager`
    ikut terikat. (Urutan tool bebas — menjalankan ulang selalu aman.)
12. Jalankan menu **Tools > MBG > Build Obstacle Setup**. Tool ini memasang `ObstacleManager`,
    peringatan di `Door_Interior`, dan indikator gangguan di HUD.
13. Jalankan menu **Tools > MBG > Build Exterior Encounters**. Tool ini membuat titik encounter di
    luar rumah dan memasang sisi runtime gangguan Ormas.
14. Jalankan menu **Tools > MBG > Build Santet Setup**. Tool ini membuat sprite lingkaran &
    vignette, tiga pola ritme, overlay layar, penunjuk objektif, dan area not di panel QTE.
15. Jalankan menu **Tools > MBG > Build Tax Setup**. Tool ini membuat naskah dialog, kutipan
    peraturan fiktif, panel dialog, dan area mengetik.
16. Simpan scene (Ctrl+S).

Semua tool di atas aman dijalankan berkali-kali (idempoten), mendukung Undo, dan menolak jalan
saat Play mode.

> Jangan pernah menjalankan `Tools > Placeholder > Rebuild Scene` — menu itu destruktif
> dan menghapus seluruh tuning manual.

## Kontrol

| Tombol | Fungsi |
| --- | --- |
| `W A S D` | Gerak (exterior top-down: 8 arah; interior side-scroller: A/D saja) |
| `Shift` | Lari |
| `F` | Interaksi (pintu, dan nanti objek dapur) |
| `Space` | Konfirmasi |
| `Esc` | Batal |

Semua input dibaca lewat `MBG.Core.InputService`. Legacy `Input` class dilarang, dan tidak
ada sistem yang boleh memanggil `Keyboard.current` langsung.

## Debug key

| Tombol | Fungsi |
| --- | --- |
| `F1` | Cetak ringkasan state ke Console: `GameState`, `IsGameplayActive`, multiplier & status pause `GameClock`, `TextInputMode`, `MoveAxis`, musik aktif |
| `F2` | Memicu satu `TimingBarQTE` dengan preset `QTE_Normal` dari mana saja, untuk tes cepat |
| `F3` | Mulai hari kerja langsung, tanpa lewat menu utama |
| `F4` | Tekan sekali: lompati semua batch → siap diserahkan. Tekan lagi: serahkan pesanan |
| `F5` | Picu gangguan **Ormas** (gedoran keras) |
| `F6` | Picu gangguan **Santet** |
| `F7` | Picu gangguan **Pajak Ilegal** (ketukan sopan) |
| `F8` | Selesaikan gangguan yang sedang berjalan (berhasil) |
| `F9`–`F12` | Belum dipakai |

Untuk menguji prompt **"Belum saatnya"** tanpa sistem pesanan: centang **Debug Force Irrelevant**
di Inspector station mana pun.

> **Penting:** setelah `CateringController` terpasang, station hanya bisa dipakai kalau ada
> pesanan berjalan **dan** station itu adalah langkah yang sedang ditunggu. Tanpa pesanan, semua
> station menjawab "Belum saatnya" — tekan `F3` dulu.

Debug key dibaca oleh `GameBootstrap` dan bisa dimatikan lewat checkbox **Enable Debug Keys**
di Inspector `__Systems`.

## Lapisan core (`Assets/_Game/Scripts/Core/`, namespace `MBG.Core`)

| File | Isi |
| --- | --- |
| `GameEventBus.cs` | Event bus static. Sistem penerbit memanggil `Raise*()`, pendengar subscribe ke `On*`. `ClearAll()` untuk membersihkan subscriber saat restart |
| `GameState.cs` | `enum GameState { Boot, MainMenu, Playing, Paused, InQTE, InObstacle, DaySummary, GameOver }` |
| `GameManager.cs` | Singleton per-scene pemegang state. `ChangeState()`, `IsGameplayActive`, `RestartGame()` |
| `GameClock.cs` | Sumber waktu gameplay. `GameClock.DeltaTime`, `SetMultiplier()`, `PushPause(reason)` / `PopPause(reason)` berpenghitung |
| `InputService.cs` | Wrapper keyboard. `MoveAxis`, `SprintHeld`, `InteractPressed`, `ConfirmPressed`, `CancelPressed`, `AnyQTEKeyPressed`, `GetLastPressedKey()`, `OnTextInput`, `BackspacePressed`, `TextInputMode` |
| `AudioService.cs` | Stub audio (`PlaySFX`, `PlayMusic`, `StopMusic`) plus enum `SfxId` / `MusicId` |
| `EconomyService.cs` | `Gold`, `Score`, `Reputation`; menerapkan hasil pesanan dan menyiarkan perubahannya. Reputasi 0 memancarkan `OnGameOver` |
| `HighScoreStore.cs` | Satu-satunya pemakaian `PlayerPrefs` di project ini (key `MBG_HighScore`) |
| `GameOverReason.cs` | `enum { ReputationZero, Bankrupt, OrmasInvasion, SantetFatal }` + kalimat bahasa Indonesia |
| `GameBootstrap.cs` | Menginisialisasi semua service dengan urutan yang benar + debug key |
| `CoreTypes.cs` | Placeholder `OrderRuntime`, `OrderResult`, `QTEGrade`, `ObstacleType`, `GameOverReason` |

**Semua sistem gameplay baru wajib memakai `GameClock.DeltaTime`, bukan `Time.deltaTime`.**

## Lapisan Kitchen (`Assets/_Game/Scripts/Kitchen/`, namespace `MBG.Kitchen`)

| File | Isi |
| --- | --- |
| `StationType.cs` | `enum { Prep, Cooking, Packing, Handover }` + prompt & nama bawaan bahasa Indonesia |
| `KitchenStation.cs` | `IInteractable` + `IInteractableFocus`. Prompt dinamis, highlight saat jadi target terdekat |
| `RecipientNPC.cs` | Penerima placeholder di seberang counter Handover; muncul saat pesanan dimulai, hilang setelah diserahkan |
| `KitchenLayoutSO.cs` | Semua angka penataan dapur — asset di `Assets/_Game/Data/KitchenLayout.asset` |

Prompt station saat belum boleh dipakai: **"Belum saatnya"**, kecuali Handover yang memakai
**"Catering belum siap"**. Saat pesanan sudah `ReadyToDeliver`, Handover berubah menjadi
**"Serahkan catering"**.

Layout dapur (offset X dari pusat ruangan `x = 1000`, sisi bawah menempel di `y = -2.81`):

| Station | Offset X | Rentang | Catatan |
| --- | --- | --- | --- |
| Cooking | −4.8 | [−5.6, −4.0] | |
| Prep | −2.6 | [−3.4, −1.8] | |
| *(pintu)* | *+0.145* | *[−0.44, +0.73]* | tidak disentuh tool |
| Packing | +2.6 | [+1.8, +3.4] | |
| Handover | +4.8 | [+4.0, +5.6] | |

Ruangan lebar 12.5 unit → batas [−6.25, +6.25]. `KitchenLayoutSO.Validate()` memperingatkan
kalau ada station yang keluar ruangan, menabrak zona pintu, atau saling tumpang tindih.

Relevansi station diisi dari luar lewat `KitchenStation.RelevanceCheck` — station tidak boleh
tahu apa pun tentang sistem pesanan. Selama belum diisi, semua station relevan.

## Lapisan Catering (`Assets/_Game/Scripts/Catering/`, namespace `MBG.Catering`)

| File | Isi |
| --- | --- |
| `FoodQuality.cs` | `enum FoodQuality { Perfect, Good, Bad, Failed }` + `enum OrderState` |
| `OrderRuntime.cs` | State pesanan berjalan: porsi, batch, langkah, timer, riwayat grade, `AverageQuality`, `QualityTier` |
| `OrderResult.cs` | Hasil akhir pesanan (kualitas, porsi, gold, skor, sisa waktu) |
| `CateringController.cs` | Mengelola pesanan aktif, memicu QTE per langkah, timer deadline |

Alur satu pesanan:

```
StartOrder(OrderSO)
  └─ batch 1..N, tiap batch menjalankan seluruh langkah resep:
       station benar + tekan F → QTE langkah itu
         ├─ Perfect / Good / Miss → lanjut langkah berikutnya
         └─ CriticalMiss          → batch DIULANG dari langkah pertama
       semua langkah selesai → portionsCompleted += portionsPerBatch
  └─ semua porsi siap → ReadyToDeliver → station SERAH TERIMA aktif
       └─ tekan F di Handover → OnOrderCompleted
  └─ timer habis kapan pun → OnOrderFailed
```

Aturan catering:

- Station tahu dirinya relevan lewat `KitchenStation.RelevanceCheck`, yang diisi
  `CateringController`. Station tidak tahu apa pun tentang resep.
- Interaksi di station yang salah **tidak menghukum** apa pun — prompt hanya jadi "Belum saatnya".
- `CriticalMiss` mengulang batch dari langkah pertama. Tidak ada gold yang dipotong; yang hilang
  adalah waktu. (Grade-nya tetap masuk `gradeHistory`, jadi kualitas rata-rata ikut turun.)
- Saat `GameState = InObstacle`, semua station jadi tidak relevan sehingga QTE tidak bisa dipicu.
  **Timer deadline tetap berjalan** — obstacle nanti memperlambatnya lewat `GameClock.SetMultiplier`.
- Timer memakai `GameClock.DeltaTime`.

## Lapisan Obstacle (`Assets/_Game/Scripts/Obstacles/`, namespace `MBG.Obstacles`)

| File | Isi |
| --- | --- |
| `ObstacleType.cs` | `enum { Ormas, Santet, IllegalTax }` + `DoorAlertState` + label/ikon/onomatope |
| `IObstacle.cs` | Kontrak gangguan: `Begin`, `Tick`, `Resolve`, `UrgencyNormalized`, `OnResolved` |
| `ObstacleManager.cs` | Jadwal harian, satu gangguan aktif, pelambatan waktu, perpindahan state |
| `DoorAlertSystem.cs` | Peringatan di pintu: getaran, ikon, onomatope, guncangan layar |
| `Tax/IllegalTaxObstacle.cs` | Ketukan sabar, dialog petugas, lalu tantangan mengetik |
| `Tax/TaxPresenter.cs` + `TaxCollectorNPC.cs` | Petugas placeholder dan panel dialognya |
| `Santet/SantetObstacle.cs` | Serangan ritme di dapur, lalu perburuan dukun di luar |
| `Santet/SantetPresenter.cs` | Overlay layar, penunjuk objektif, dan dukun placeholder |
| `Santet/DukunNPC.cs` | Target interaksi "[F] Hentikan santetnya" |
| `Ormas/OrmasObstacle.cs` | Gangguan Ormas lengkap: Intrusion Meter + encounter |
| `Ormas/OrmasEncounterController.cs` | Memunculkan grup di luar rumah dan penerobos di dapur |
| `Ormas/OrmasGroup.cs` | Target interaksi "[F] Hadapi mereka" |
| `ScreenShakeService.cs` | Satu-satunya penulis offset guncangan kamera |

### Ormas

```
Gedoran mulai  →  Intrusion Meter mengisi (default 22s)
   ├─ diam di dapur → meter penuh → ormas MASUK
   │     1 batch hancur, -250 gold, -2 reputasi, layar berguncang keras,
   │     sprite ormas muncul sebentar di dapur → gangguan selesai (gagal)
   └─ keluar rumah → meter BERHENTI
         [F] Hadapi mereka → mini-game tarik-menarik (MashQTE)
            menang → +1 reputasi, mereka pergi, pemain kembali ke dapur
            kalah  → -200 gold, -1 reputasi, mereka tetap pergi
```

- Masuk kembali ke dapur tanpa menghadapi membuat meter **jalan lagi** dari posisi terakhir —
  keluar sebentar bukan cara murah menghentikan waktu.
- Selama encounter, `GameClock` dikembalikan ke 1.0 supaya batas waktu 15 detik di config benar-benar
  15 detik, bukan 30.
- Kalah tidak pernah membuat pemain terjebak: ormas selalu pergi.
- Semua angkanya di `OrmasConfigSO`, termasuk daftar kalimat provokasi.

### Santet

Santet **tidak lewat pintu** — pintunya diam, serangannya langsung mengenai pemain di dapur.

```
Fase 1 (dapur)  layar memerah + vignette + lapisan kelabu, pemain dibekukan
                → RhythmQTE (ring luar mengecil ke ring dalam, tekan Spasi/klik)
                   akurasi ≥ 0.6 → efek hilang, lanjut fase 2
                   akurasi < 0.6 → -1 batch, -150 gold, -1 reputasi, efek tetap hilang
Fase 2 (luar)   objektif "Cari dukun di luar" + panah penunjuk di tepi layar
                → [F] Hentikan santetnya → TimingBarQTE Hard 3 hit
                   berhasil → +1 reputasi, gangguan selesai
                   gagal    → -100 gold, dukun tetap pergi
```

- Gagal di fase mana pun **tidak pernah** langsung mengakhiri permainan; game over hanya lewat
  reputasi 0.
- Pola ritme **tidak disinkronkan ke audio track** — murni berbasis waktu, jadi mengganti musik
  tidak merusak satu pun pola.
- Efek layar memakai tiga UI Image fullscreen, bukan post-processing volume. Konsekuensinya:
  saturasi tidak benar-benar diturunkan, hanya ditumpuk lapisan kelabu.
- `GameClock` dikembalikan ke 1.0 selama QTE (pola berbasis waktu absolut), dan 0.5 selama
  perburuan dukun.

### Pajak Ilegal

Ketukannya sopan dan sabar — meternya mengisi jauh lebih lambat daripada gedoran ormas.

```
Ketukan "TOK TOK TOK"  →  meter mengisi (default 40s)
   ├─ diabaikan → usaha disegel sementara: -400 gold, -1 reputasi
   └─ keluar rumah → petugas muncul di TaxCollectorPoint
         [F] Layani petugas → 4 baris tuntutan (Spasi untuk lanjut)
            [F] Tunjukkan dokumen resmi → TypingChallenge
               akurasi ≥ 0.95 → PERFECT: pemeras kabur, +1 reputasi
               akurasi ≥ 0.80 → GOOD   : pemeras pergi, +1 reputasi
               selesai tapi kacau      : bayar sebagian -200 gold
               tidak selesai / Escape  : bayar penuh -450 gold, -1 reputasi
            lalu satu baris dialog penutup
```

- **Semua nama lembaga, program, dan nomor peraturan FIKTIF.** Contoh: "Peraturan Dinas Pangan
  Wilayah Nomor 17 Tahun 2024 tentang Penyelenggaraan Program Gizi Rakyat".
- Enam kutipan tersedia: 2 pendek, 2 sedang, 2 panjang — dipilih sesuai kesulitan hari.
- Selama mengetik, `InputService.TextInputMode` menyala sehingga gerakan dan tombol aksi mati.
  Mode itu dimatikan lagi di **setiap** jalur keluar: selesai, kehabisan waktu, Escape,
  pembatalan dari luar, dan sekali lagi oleh `QTEController` sebagai jaring pengaman.
- Karakter benar hijau, salah merah, sisanya redup, dengan kursor `|` — warnanya dari `UIStyle`.
- Backspace memperbaiki teks, tapi tidak menghapus catatan kesalahan: itu yang membedakan
  cepat-tepat dari cepat-asal.

Pintu bukan dekorasi — ia sistem peringatan:

| State | Dipakai oleh | Efek |
| --- | --- | --- |
| `Idle` | — | Tidak ada apa-apa |
| `Knock` | Pajak Ilegal | Getar pelan, ikon `?`, SFX `DoorKnock`, teks **"TOK TOK TOK"** |
| `Bang` | Ormas | Getar keras, ikon `!` merah, SFX `DoorBang`, **"BRAK! BRAK! BRAK!"**, guncangan layar |
| Urgent | urgency > 0.75 | Getaran mempercepat, warna makin merah, bunyi makin rapat |

Aturan gangguan:

- **Transform `Door_Interior` tidak pernah disentuh.** Semua getaran terjadi pada child
  `AlertVisual`; posisi awalnya dicatat di `Awake` dan dikembalikan saat peringatan berhenti.
- Selama gangguan aktif: `GameClock.SetMultiplier(0.5)` (timer pesanan melambat, tidak berhenti),
  `GameState` menjadi `InObstacle`, sehingga station menolak memicu QTE masak.
- Hanya satu gangguan aktif; jadwal yang bertabrakan **ditunda**, bukan dibuang.
- Jadwal dan gangguan di-tick dengan `GameClock.RawDeltaTime` — sistem ini yang memperlambat
  clock, jadi ia tidak boleh ikut melambat oleh perlambatannya sendiri.
- Prompt pintu berubah jadi **"Keluar dan hadapi mereka"** berwarna merah selama peringatan aktif.

## Alur permainan (`Assets/_Game/Scripts/World/`, namespace `MBG.World`)

| File | Isi |
| --- | --- |
| `DayManager.cs` | Antrian pesanan per hari, jeda antar pesanan, penutupan hari |
| `DayStats.cs` | Rekap satu hari (gold, skor, sukses/gagal, reputasi) |

```
MainMenu ──[Mulai]──> Playing
   Hari N: pesanan 1..M dari DayConfigSO
     pesanan selesai/gagal → ResultPanel → SPASI → jeda timeBetweenOrders
   antrian habis → OnDaySummary + OnDayCompleted → DaySummary
     [Lanjut ke Hari Berikutnya] → hari N+1
     hari terakhir → layar kemenangan → reload scene → MainMenu
   reputasi 0 → OnGameOver → GameOver → [Ulangi] reload scene
Escape saat Playing → Paused → [Lanjut] / [Ulangi] / [Menu Utama]
```

Pembagian tugas: `DayManager` tahu antrian dan urutan hari; `CateringController` hanya tahu satu
pesanan yang sedang dimasak. Keduanya berbicara lewat event bus.

## Lapisan Data (`Assets/_Game/Scripts/Data/`, namespace `MBG.Data`)

| File | Isi |
| --- | --- |
| `RecipeStepSO.cs` | `StationType` + `QTEConfigSO` + `actionLabel` |
| `RecipeSO.cs` | Nama menu, daftar langkah, `portionsPerBatch` |
| `RecipientSO.cs` | Institusi pemesan, ikon, `patienceMultiplier` (mengali deadline) |
| `OrderSO.cs` | Resep + pemesan + porsi + deadline + bayaran dasar |
| `CateringBalanceSO.cs` | Nilai tiap grade QTE dan ambang tingkat kualitas |
| `DayConfigSO.cs` | Isi satu hari: pesanan, jeda antar pesanan, jadwal gangguan, pengali deadline |
| `ScoringConfigSO.cs` | **Semua rumus gold & skor** — satu-satunya tempat angka bayaran hidup |

Rumus bayaran (`ScoringConfigSO.CalculateResult`, fungsi murni yang dipanggil
`CateringController`, `EconomyService`, dan `ResultPanel`):

```
qualityMultiplier : Perfect 1.5 | Good 1.0 | Bad 0.6 | Failed 0.2
timeBonus         : (sisa waktu / total waktu) * maxTimeBonus   (default 200)
gold              : baseGoldReward  * qualityMultiplier + timeBonus
score             : baseScoreReward * qualityMultiplier + (jumlah Perfect * perfectBonus)
gagal (timeout)   : gold = -failPenaltyGold (default 150), score = 0
```

Asset contoh di `Assets/_Game/Data/Catering/`:

| Pesanan | Porsi | Batch | Deadline | Gold |
| --- | --- | --- | --- | --- |
| `Order_Sekolah_Kecil` | 40 | 2 | 180s | 300 |
| `Order_Sekolah_Sedang` | 100 | 5 | 300s | 800 |
| `Order_Sekolah_Besar` | 200 | 10 | 480s | 1600 |

`Recipe_NasiKotak` = Potong sayur (`QTE_Easy`) → Masak nasi & lauk (`QTE_Normal`) →
Kemas ke kotak (`QTE_Easy`), 20 porsi per batch.

## Lapisan QTE (`Assets/_Game/Scripts/QTE/`, namespace `MBG.QTE`)

| File | Isi |
| --- | --- |
| `QTEGrade.cs` | `enum { Perfect, Good, Miss, CriticalMiss }` — urut dari terbaik ke terburuk |
| `QTEType.cs` | `enum { TimingBar, Rhythm, Mash }` |
| `QTERequest.cs` / `QTEResult.cs` | Permintaan sesi dan hasilnya |
| `IQTEModule.cs` | Kontrak module + `ITimingBarReadout` untuk UI |
| `QTEConfigSO.cs` | Semua angka kesulitan — preset di `Assets/_Game/Data/QTE/` |
| `QTEController.cs` | Singleton: pilih module, bekukan pemain, atur `GameState`, pancarkan hasil |
| `Modules/TimingBarQTE.cs` | Skill check ala Dead by Daylight, mendukung multi-hit |
| `Modules/MashQTE.cs` | Tarik-menarik: tekan Spasi melawan dorongan lawan, dengan kalimat provokasi |
| `Modules/RhythmQTE.cs` | Ring luar mengecil menuju ring dalam; pola murni berbasis waktu, tanpa sinkronisasi audio |
| `Modules/TypingChallenge.cs` | Ketik ulang kutipan peraturan persis; menyalakan `TextInputMode` |

Cara memanggil QTE dari sistem lain:

```csharp
QTEController.Instance.Begin(QTERequest.TimingBar(
    config,                       // QTEConfigSO
    "Potong bawang!",             // instruksi yang tampil di panel
    result => { /* result.grade, result.accuracy, ... */ }));
```

Preset bawaan:

| Preset | Durasi lintasan | Zona Good | Zona Perfect | Hit |
| --- | --- | --- | --- | --- |
| `QTE_Easy` | 1.6s ÷ 0.85 | 0.42 | 0.16 | 1 |
| `QTE_Normal` | 1.2s ÷ 1.00 | 0.30 | 0.10 | 2 |
| `QTE_Hard` | 0.9s ÷ 1.25 | 0.20 | 0.06 | 3 |

Aturan QTE:

- Nilai akhir sesi = nilai **terburuk** di antara semua hit. Satu `CriticalMiss` (indikator sampai
  ujung tanpa input) langsung mengakhiri sesi.
- Module di-tick dengan `GameClock.DeltaTime`, jadi QTE ikut berhenti saat clock gameplay di-pause.
- Pemain dibekukan lewat `PlayerController2D.SetFrozen()` selama sesi berjalan.
- Panel muncul karena `UIManager` mendengar `GameState` berubah ke `InQTE` — sistem QTE tidak
  pernah memanggil UI secara langsung.

## Lapisan UI (`Assets/_Game/Scripts/UI/`, namespace `MBG.UI`)

| File | Isi |
| --- | --- |
| `UIPanel.cs` | Base class abstract. `Show()`, `Hide()`, `IsVisible`, fade lewat `CanvasGroup` |
| `UIManager.cs` | Singleton pendaftar panel + pemetaan `GameState` → panel |
| `UIStyle.cs` | ScriptableObject tema — asset-nya di `Assets/_Game/Data/UIStyle.asset` |
| `Panels/HUDPanel.cs` | HUD gameplay lengkap (lihat di bawah) |
| `Panels/QTEPanel.cs` | Bar timing QTE |
| `Panels/ResultPanel.cs` | Layar hasil pesanan; mem-pause `GameClock` selama tampil, SPASI untuk lanjut |
| `Panels/MainMenuPanel.cs` | Judul, Mulai, Cara Bermain, Keluar |
| `Panels/HowToPlayPanel.cs` | Daftar kontrol dan cara main |
| `Panels/PausePanel.cs` | Lanjut, Ulangi, Menu Utama |
| `Panels/DaySummaryPanel.cs` | Rekap hari; berubah jadi layar kemenangan setelah hari terakhir |
| `Panels/GameOverPanel.cs` | Sebab kalah, statistik akhir, high score, Ulangi & Keluar |

### HUD

| Posisi | Isi |
| --- | --- |
| Kiri atas | Kartu pesanan: nama penerima, nama makanan, progress bar + "60 / 100 porsi", bar kualitas rata-rata |
| Kanan atas | Uang (`Rp`) dan skor |
| Tengah atas | Timer `MM:SS` — merah dan berdenyut saat sisa waktu di bawah `timerWarningThreshold` (default 25%) |
| Bawah tengah | "Berikutnya: Potong sayur di meja persiapan" dengan panah `<` / `>` ke arah station tujuan |
| Kanan bawah | `ObstacleSlot` — masih kosong, diakses lewat `HUDPanel.ObstacleSlot` |

- HUD hanya tampil saat `GameState.Playing` atau `InQTE`; `UIManager` yang mengaturnya.
- Semua datanya masuk lewat `GameEventBus`. HUD **tidak** punya referensi ke `CateringController`.
- Progress bar dan bar kualitas dianimasikan `MoveTowards`, bukan snap.
- Saat porsi bertambah, muncul teks melayang "+20 porsi".
- Arah panah dihitung dari registry posisi di `KitchenStation.TryGetPosition()`.

Aturan UI:

- Semua teks pakai **TextMeshPro**, tidak ada legacy `UnityEngine.UI.Text` di project ini.
- Warna dan ukuran font **wajib** dibaca dari `UIStyle`, jangan ditulis ulang di tiap panel.
- Fade panel memakai waktu **unscaled**, supaya panel tetap mulus saat `GameClock` di-pause.
- Urutan child `UICanvas`: `PromptPanel` → panel-panel MBG → **`Fade` selalu terakhir**, karena
  overlay fade harus menutupi seluruh UI saat transisi antar area.
