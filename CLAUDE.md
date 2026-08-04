# ATURAN KERJA PROJECT INI

## Environment
- Unity 6000.4.8f1, URP 17.4.0 dengan 2D Renderer
- New Input System 1.19.0. Legacy `Input` class DILARANG dipakai. Semua input lewat `MBG.Core.InputService`.
- Satu scene aktif saja: `Assets/Scenes/SampleScene.unity`
- Interior (dapur) berada di offset x = 1000. Exterior (dunia luar) di sekitar x = 0. Keduanya di-toggle dengan `SetActive` lewat `AreaManager`.

## LARANGAN KERAS
1. **JANGAN PERNAH** menjalankan atau menyarankan menu `Tools > Placeholder > Rebuild Scene`. Menu itu destruktif dan akan menghapus semua tuning manual.
2. **JANGAN** mengubah nilai transform berikut yang merupakan hasil tuning tangan:
   - `Player/Visual` localScale X = `0.4619736`
   - `Player` dan `InteriorSpawn` di posisi `(997, -2.26)`
   - `Floor` di y = `-3.32`, permukaan lantai `-2.81`
   - `Door_Interior` di `(1000.145, -2.002)`, scale seragam `0.55806`, collider size `2.1 x 2`
   - Semua `Border_*` dan posisi `Door_Exterior`
3. **JANGAN** menghapus atau merename GameObject yang sudah ada di hierarki `__Placeholder`.
4. **JANGAN** membuat scene baru. **JANGAN** memakai `SceneManager.LoadScene` kecuali untuk restart game (reload scene yang sama).
5. **JANGAN** memakai `DontDestroyOnLoad`.
6. **JANGAN** memakai localStorage, `PlayerPrefs` untuk state gameplay (`PlayerPrefs` hanya boleh untuk high score).
7. Kalau kamu merasa perlu melanggar salah satu di atas, **BERHENTI dan tanya dulu**.

## CARA MENAMBAH KONTEN KE SCENE
- Semua object baru dibuat sebagai **PREFAB** di `Assets/_Game/Prefabs/`
- Penempatan ke scene lewat **editor tool additive dan idempoten** di menu `Tools > MBG/`
- Editor tool wajib: bisa dijalankan berkali-kali tanpa menduplikasi object, mendukung Undo, dan menolak jalan saat Play mode
- Unity MCP hanya boleh dipakai untuk **MEMBACA** scene dan diagnostik, bukan untuk menulis

## KONVENSI KODE
- Namespace: `MBG.Core`, `MBG.Kitchen`, `MBG.QTE`, `MBG.Obstacles`, `MBG.UI`, `MBG.Data`, `MBG.World`
- Folder kode baru: `Assets/_Game/Scripts/<subfolder>/`
- ScriptableObject data di `Assets/_Game/Data/`
- Semua angka balancing **WAJIB** di ScriptableObject, tidak boleh hardcode di logic
- Komunikasi antar sistem lewat `MBG.Core.GameEventBus`, hindari referensi langsung antar sistem
- UI pakai **TextMeshPro**, bukan legacy `Text`
- Bahasa string in-game: **Indonesia**

## WORKFLOW
- Satu prompt = satu commit. Selalu usulkan pesan commit di akhir.
- Setelah selesai, selalu tulis **CHECKLIST TES MANUAL** yang harus dijalankan user di Unity Editor.
- Kalau butuh testing, sediakan debug key (F1 sampai F12) dan tulis di README.
