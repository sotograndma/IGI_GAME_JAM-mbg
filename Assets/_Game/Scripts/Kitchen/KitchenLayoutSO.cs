using System;
using System.Collections.Generic;
using UnityEngine;

namespace MBG.Kitchen
{
    /// <summary>
    /// Semua angka penataan dapur ada di sini — tidak satu pun boleh ditulis ulang
    /// di dalam logic atau di editor tool. Tools > MBG > Build Kitchen Stations
    /// membaca asset ini dan hanya asset ini.
    ///
    /// Sistem koordinat: offsetX dihitung relatif terhadap <see cref="roomCenterX"/>,
    /// dan setiap station berdiri dengan sisi bawahnya menempel di
    /// <see cref="floorSurfaceY"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "KitchenLayout", menuName = "MBG/Kitchen Layout")]
    public class KitchenLayoutSO : ScriptableObject
    {
        [Serializable]
        public class StationEntry
        {
            public StationType type = StationType.Prep;

            [Tooltip("Jarak horizontal dari pusat ruangan. Negatif = kiri.")]
            public float offsetX;

            public float width = 1.6f;
            public float height = 1.4f;
            public Color color = Color.gray;

            [Tooltip("Kosongkan untuk memakai prompt bawaan tipe station.")]
            public string promptOverride = "";

            [Tooltip("Kosongkan untuk memakai nama bawaan tipe station.")]
            public string labelOverride = "";

            public StationEntry() { }

            public StationEntry(StationType type, float offsetX, Color color)
            {
                this.type = type;
                this.offsetX = offsetX;
                this.color = color;
            }

            public string ResolvePrompt()
                => string.IsNullOrWhiteSpace(promptOverride) ? type.GetDefaultPrompt() : promptOverride;

            public string ResolveLabel()
                => string.IsNullOrWhiteSpace(labelOverride) ? type.GetDisplayName() : labelOverride;
        }

        [Header("Ruangan")]
        [Tooltip("Pusat dapur di world space. Interior berada di offset x = 1000.")]
        public float roomCenterX = 1000f;

        [Tooltip("Permukaan lantai — sisi bawah semua station menempel di sini.")]
        public float floorSurfaceY = -2.81f;

        [Tooltip("Lebar total dapur; dipakai untuk memperingatkan station yang keluar ruangan.")]
        public float roomWidth = 12.5f;

        [Header("Pintu (hanya untuk validasi — tool tidak pernah mengubah pintu)")]
        public float doorOffsetX = 0.145f;

        [Tooltip("Setengah lebar zona bebas di sekitar pintu. Station yang masuk zona ini diperingatkan.")]
        public float doorClearance = 1.35f;

        [Header("Zona interaksi")]
        [Tooltip("Seberapa jauh trigger interaksi melebihi kotak visual station, di tiap sisi.")]
        public float interactionPadding = 0.45f;

        [Header("Highlight saat jadi target terdekat")]
        [Tooltip("Kotak highlight digambar di belakang kotak station, sedikit lebih besar, " +
                 "sehingga terbaca sebagai outline.")]
        public Color highlightColor = Color.white;

        [Range(0f, 1f)] public float highlightAlpha = 0.85f;

        [Tooltip("Kecepatan fade highlight (per detik waktu gameplay).")]
        public float highlightFadeSpeed = 12f;

        [Tooltip("Seberapa jauh kotak highlight melebihi kotak visual, di tiap sisi.")]
        public float highlightPadding = 0.14f;

        [Header("Label placeholder")]
        public float labelFontSize = 1.1f;

        [Tooltip("Tinggi label di atas sisi atas station.")]
        public float labelOffsetY = 0.35f;

        [Header("Station (urut kiri ke kanan)")]
        public List<StationEntry> stations = new();

        /// <summary>Posisi pusat station di world space.</summary>
        public Vector2 GetStationCenter(StationEntry entry)
            => new Vector2(roomCenterX + entry.offsetX, floorSurfaceY + entry.height * 0.5f);

        /// <summary>
        /// Layout bawaan sesuai GDD: [COOKING] [PREP] [pintu] [PACKING] [HANDOVER].
        /// Dipanggil sekali saat asset dibuat oleh editor tool.
        /// </summary>
        public void ApplyDefaultLayout()
        {
            stations = new List<StationEntry>
            {
                new StationEntry(StationType.Cooking, -4.8f, new Color(0.85f, 0.36f, 0.25f)),
                new StationEntry(StationType.Prep, -2.6f, new Color(0.38f, 0.70f, 0.42f)),
                new StationEntry(StationType.Packing, 2.6f, new Color(0.36f, 0.56f, 0.85f)),
                new StationEntry(StationType.Handover, 4.8f, new Color(0.90f, 0.75f, 0.32f)),
            };
        }

        /// <summary>
        /// Cek layout terhadap batas ruangan, zona pintu, dan tumpang tindih antar
        /// station. Mengembalikan daftar masalah — kosong berarti aman.
        /// </summary>
        public List<string> Validate()
        {
            var problems = new List<string>();

            float roomMin = -roomWidth * 0.5f;
            float roomMax = roomWidth * 0.5f;
            float doorMin = doorOffsetX - doorClearance;
            float doorMax = doorOffsetX + doorClearance;

            for (int i = 0; i < stations.Count; i++)
            {
                StationEntry entry = stations[i];
                if (entry == null) continue;

                float half = entry.width * 0.5f;
                float min = entry.offsetX - half;
                float max = entry.offsetX + half;

                if (min < roomMin || max > roomMax)
                {
                    problems.Add($"{entry.type} keluar ruangan: rentang [{min:0.##}, {max:0.##}] " +
                                 $"melewati batas [{roomMin:0.##}, {roomMax:0.##}].");
                }

                if (max > doorMin && min < doorMax)
                {
                    problems.Add($"{entry.type} menabrak zona pintu: rentang [{min:0.##}, {max:0.##}] " +
                                 $"bersinggungan dengan [{doorMin:0.##}, {doorMax:0.##}].");
                }

                for (int j = i + 1; j < stations.Count; j++)
                {
                    StationEntry other = stations[j];
                    if (other == null) continue;

                    float otherHalf = other.width * 0.5f;
                    if (max > other.offsetX - otherHalf && min < other.offsetX + otherHalf)
                        problems.Add($"{entry.type} dan {other.type} saling tumpang tindih.");
                }
            }

            return problems;
        }
    }
}
