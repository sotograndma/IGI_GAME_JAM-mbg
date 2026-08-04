# IGI_GAME_JAM-mbg

Game jam project — Unity 6000.4.8f1, URP 17.4.0 (2D Renderer), New Input System.

Aturan kerja untuk kontributor (manusia maupun AI) ada di [CLAUDE.md](CLAUDE.md). **Baca dulu sebelum menyentuh scene.**

## Setup scene (sekali saja)

1. Buka `Assets/Scenes/SampleScene.unity`.
2. Jalankan menu **Tools > MBG > Setup Systems Object**. Tool ini membuat GameObject root `__Systems`
   berisi `GameBootstrap`, `GameClock`, `AudioService`, dan `GameManager`.
   Aman dijalankan berkali-kali (idempoten) dan mendukung Undo.
3. Simpan scene (Ctrl+S).

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
| `F2`–`F12` | Belum dipakai — disediakan untuk sistem berikutnya (order, QTE, obstacle) |

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
| `GameBootstrap.cs` | Menginisialisasi semua service dengan urutan yang benar + debug key |
| `CoreTypes.cs` | Placeholder `OrderRuntime`, `OrderResult`, `QTEGrade`, `ObstacleType`, `GameOverReason` |

**Semua sistem gameplay baru wajib memakai `GameClock.DeltaTime`, bukan `Time.deltaTime`.**
