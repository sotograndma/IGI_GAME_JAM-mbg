using System;
using System.Collections.Generic;
using MBG.Obstacles;
using UnityEngine;

namespace MBG.Data
{
    /// <summary>
    /// Satu gangguan yang dijadwalkan muncul di hari tertentu.
    /// Sistem yang membacanya belum ada — akan dipasang bersama MBG.Obstacles.
    /// </summary>
    [Serializable]
    public class ObstacleScheduleEntry
    {
        public ObstacleType type = ObstacleType.IllegalTax;

        [Tooltip("Detik sejak hari dimulai, saat gangguan ini dipicu.")]
        [Min(0f)]
        public float triggerAtSeconds = 30f;

        [Tooltip("0 = paling ringan, 1 = paling berat. Arti persisnya ditentukan tiap gangguan.")]
        [Range(0f, 1f)]
        public float difficulty = 0.5f;
    }

    /// <summary>
    /// Isi satu hari kerja: pesanan apa saja yang datang, seberapa cepat pesanan
    /// berikutnya menyusul, gangguan yang dijadwalkan, dan seberapa longgar
    /// deadline hari itu.
    /// </summary>
    [CreateAssetMenu(fileName = "Day_", menuName = "MBG/Day Config")]
    public class DayConfigSO : ScriptableObject
    {
        [Min(1)]
        public int dayNumber = 1;

        [Tooltip("Pesanan hari ini, dikerjakan berurutan.")]
        public List<OrderSO> orders = new();

        [Tooltip("Jeda sebelum pesanan berikutnya datang, dihitung setelah layar hasil ditutup.")]
        [Min(0f)]
        public float timeBetweenOrders = 3f;

        [Tooltip("Gangguan yang dijadwalkan hari ini. Sistemnya menyusul.")]
        public List<ObstacleScheduleEntry> obstacleSchedule = new();

        [Tooltip("Pengali deadline seluruh pesanan hari ini. <1 membuat hari terasa lebih menekan.")]
        [Min(0.1f)]
        public float orderDeadlineMultiplier = 1f;

        public int OrderCount => orders != null ? orders.Count : 0;

        public OrderSO GetOrder(int index)
        {
            if (orders == null || index < 0 || index >= orders.Count) return null;
            return orders[index];
        }
    }
}
