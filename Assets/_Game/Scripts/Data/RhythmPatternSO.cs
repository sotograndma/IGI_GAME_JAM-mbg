using System;
using System.Collections.Generic;
using UnityEngine;

namespace MBG.Data
{
    /// <summary>
    /// Satu not dalam pola ritme.
    ///
    /// <see cref="time"/> dihitung dari awal sesi, BUKAN dari posisi lagu — pola di
    /// project ini sengaja tidak disinkronkan ke audio track, supaya tidak ada
    /// ketergantungan pada panjang atau BPM klip tertentu.
    /// </summary>
    [Serializable]
    public class RhythmNote
    {
        [Tooltip("Detik sejak sesi dimulai, saat ring luar bertemu ring dalam.")]
        [Min(0f)]
        public float time = 1f;

        [Tooltip("Posisi di dalam area ritme, 0..1 pada tiap sumbu. (0,0) kiri bawah.")]
        public Vector2 normalizedPosition = new Vector2(0.5f, 0.5f);

        [Tooltip("Berapa lama ring luar mengecil sebelum saatnya ditekan.")]
        [Min(0.15f)]
        public float approachDuration = 1.1f;

        public RhythmNote() { }

        public RhythmNote(float time, Vector2 normalizedPosition, float approachDuration)
        {
            this.time = time;
            this.normalizedPosition = normalizedPosition;
            this.approachDuration = approachDuration;
        }

        /// <summary>Kapan not ini mulai terlihat.</summary>
        public float SpawnTime => Mathf.Max(0f, time - approachDuration);
    }

    /// <summary>
    /// Urutan not untuk satu sesi RhythmQTE. Murni berbasis waktu.
    /// </summary>
    [CreateAssetMenu(fileName = "Rhythm_", menuName = "MBG/Rhythm Pattern")]
    public class RhythmPatternSO : ScriptableObject
    {
        public string displayName = "Pola";

        [Tooltip("Diurutkan berdasarkan waktu saat dibaca, jadi urutan di sini tidak wajib rapi.")]
        public List<RhythmNote> notes = new();

        public int NoteCount => notes != null ? notes.Count : 0;

        /// <summary>Total durasi sesi: not terakhir plus sedikit napas.</summary>
        public float GetDuration(float tailSeconds = 1f)
        {
            float last = 0f;
            for (int i = 0; i < NoteCount; i++)
            {
                if (notes[i] == null) continue;
                last = Mathf.Max(last, notes[i].time);
            }
            return last + Mathf.Max(0f, tailSeconds);
        }

        /// <summary>Salinan terurut berdasarkan waktu, aman dipakai runtime.</summary>
        public List<RhythmNote> GetSortedNotes()
        {
            var sorted = new List<RhythmNote>();
            for (int i = 0; i < NoteCount; i++)
            {
                if (notes[i] != null) sorted.Add(notes[i]);
            }

            sorted.Sort((a, b) => a.time.CompareTo(b.time));
            return sorted;
        }
    }
}
