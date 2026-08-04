using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MBG.Core
{
    /// <summary>
    /// Satu-satunya tempat project ini menyentuh keyboard. Legacy Input class
    /// dilarang, dan tidak ada sistem lain yang boleh memanggil Keyboard.current
    /// langsung — semua lewat sini, supaya rebinding dan penonaktifan input
    /// (misal saat typing test) cukup diubah di satu file.
    ///
    /// Class static murni: property dibaca on-demand dari Keyboard.current, jadi
    /// tetap bekerja walau __Systems belum ada di scene. Yang butuh lifecycle
    /// hanyalah <see cref="OnTextInput"/>; <see cref="GameBootstrap"/> memanggil
    /// <see cref="Initialize"/>, <see cref="Tick"/>, dan <see cref="Shutdown"/>.
    /// </summary>
    public static class InputService
    {
        /// <summary>
        /// Saat true, seluruh input gerakan dan aksi dimatikan supaya huruf yang
        /// diketik pemain (typing test) tidak ikut menggerakkan karakter atau
        /// memicu interaksi. Escape sengaja tetap hidup sebagai jalan keluar.
        /// </summary>
        public static bool TextInputMode { get; set; }

        /// <summary>Karakter yang diketik pemain, diteruskan dari Keyboard.onTextInput.</summary>
        public static event Action<char> OnTextInput;

        static Keyboard _subscribedKeyboard;

        // ---- Gerakan -----------------------------------------------------

        /// <summary>
        /// WASD mentah, per sumbu bernilai -1/0/1 dan TIDAK dinormalisasi —
        /// pemanggil yang menentukan cara menangani diagonal.
        /// Vector2.zero saat <see cref="TextInputMode"/> aktif.
        /// </summary>
        public static Vector2 MoveAxis
        {
            get
            {
                if (TextInputMode) return Vector2.zero;

                var kb = Keyboard.current;
                if (kb == null) return Vector2.zero;

                float x = 0f;
                if (kb.aKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed) x += 1f;

                float y = 0f;
                if (kb.sKey.isPressed) y -= 1f;
                if (kb.wKey.isPressed) y += 1f;

                return new Vector2(x, y);
            }
        }

        public static bool SprintHeld
        {
            get
            {
                if (TextInputMode) return false;
                var kb = Keyboard.current;
                return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            }
        }

        // ---- Aksi --------------------------------------------------------

        /// <summary>Tombol F ditekan frame ini (interaksi: pintu, kompor, meja).</summary>
        public static bool InteractPressed => PressedThisFrame(Key.F);

        /// <summary>Tombol Space ditekan frame ini.</summary>
        public static bool ConfirmPressed => PressedThisFrame(Key.Space);

        /// <summary>
        /// Tombol Escape ditekan frame ini. Satu-satunya aksi yang tetap terbaca
        /// saat <see cref="TextInputMode"/> aktif.
        /// </summary>
        public static bool CancelPressed
        {
            get
            {
                var kb = Keyboard.current;
                return kb != null && kb.escapeKey.wasPressedThisFrame;
            }
        }

        /// <summary>Backspace ditekan frame ini (typing test). Aktif juga saat TextInputMode.</summary>
        public static bool BackspacePressed
        {
            get
            {
                var kb = Keyboard.current;
                return kb != null && kb.backspaceKey.wasPressedThisFrame;
            }
        }

        // ---- QTE ---------------------------------------------------------

        /// <summary>
        /// Ada tombol apa pun yang baru ditekan frame ini — untuk rhythm QTE yang
        /// mengukur timing lebih dulu, baru mencocokkan tombolnya.
        /// </summary>
        public static bool AnyQTEKeyPressed
        {
            get
            {
                if (TextInputMode) return false;
                var kb = Keyboard.current;
                return kb != null && kb.anyKey.wasPressedThisFrame;
            }
        }

        /// <summary>
        /// Tombol terakhir yang ditekan pada frame ini, atau <see cref="Key.None"/>
        /// kalau tidak ada. Dipasangkan dengan <see cref="AnyQTEKeyPressed"/>.
        /// </summary>
        public static Key GetLastPressedKey()
        {
            var kb = Keyboard.current;
            if (kb == null || TextInputMode) return Key.None;

            var keys = kb.allKeys;
            for (int i = 0; i < keys.Count; i++)
            {
                var control = keys[i];
                if (control.keyCode == Key.None) continue;
                if (control.wasPressedThisFrame) return control.keyCode;
            }
            return Key.None;
        }

        /// <summary>Cek satu tombol tertentu — dipakai debug key F1..F12.</summary>
        public static bool WasKeyPressedThisFrame(Key key)
        {
            var kb = Keyboard.current;
            if (kb == null || key == Key.None) return false;
            return kb[key].wasPressedThisFrame;
        }

        public static bool IsKeyHeld(Key key)
        {
            var kb = Keyboard.current;
            if (kb == null || key == Key.None) return false;
            return kb[key].isPressed;
        }

        // ---- Lifecycle ---------------------------------------------------

        public static void Initialize()
        {
            TextInputMode = false;
            Tick();
        }

        /// <summary>
        /// Dipanggil sekali per frame oleh <see cref="GameBootstrap"/>. Tugasnya
        /// hanya memastikan langganan onTextInput menempel pada keyboard yang
        /// sedang aktif — device bisa berganti saat keyboard dicabut/dipasang.
        /// </summary>
        public static void Tick()
        {
            var kb = Keyboard.current;
            if (ReferenceEquals(kb, _subscribedKeyboard)) return;

            if (_subscribedKeyboard != null)
                _subscribedKeyboard.onTextInput -= HandleTextInput;

            _subscribedKeyboard = kb;

            if (_subscribedKeyboard != null)
                _subscribedKeyboard.onTextInput += HandleTextInput;
        }

        public static void Shutdown()
        {
            if (_subscribedKeyboard != null)
                _subscribedKeyboard.onTextInput -= HandleTextInput;

            _subscribedKeyboard = null;
            OnTextInput = null;
            TextInputMode = false;
        }

        static void HandleTextInput(char c)
        {
            // Buang karakter kontrol (backspace, enter, tab). Backspace punya
            // jalurnya sendiri lewat BackspacePressed.
            if (c < ' ') return;
            OnTextInput?.Invoke(c);
        }

        static bool PressedThisFrame(Key key)
        {
            if (TextInputMode) return false;
            var kb = Keyboard.current;
            return kb != null && kb[key].wasPressedThisFrame;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            _subscribedKeyboard = null;
            OnTextInput = null;
            TextInputMode = false;
        }
    }
}
