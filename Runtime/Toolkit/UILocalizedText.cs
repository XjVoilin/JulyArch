using JulyCore;
using TMPro;
using UnityEngine;

namespace JulyArch
{
    /// <summary>
    /// 挂在带有 TMP 的物体上，填入 Key 即可自动本地化
    /// 支持格式化参数，语言切换时自动刷新
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class UILocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;

        private TextMeshProUGUI _text;
        private object[] _args;

        private void Awake()
        {
            _text = GetComponent<TextMeshProUGUI>();
        }

        private void OnEnable()
        {
            GF.Event.Subscribe<LanguageChangedEvent>(OnLanguageChanged, this);
            Apply();
        }

        private void OnDisable()
        {
            GF.Event.UnsubscribeAll(this);
        }

        /// <summary>
        /// 设置 key，无参数
        /// </summary>
        public void SetKey(string newKey)
        {
            key = newKey;
            _args = null;
            Apply();
        }

        /// <summary>
        /// 设置 key + 格式化参数
        /// Language 表: "DAMAGE_MSG" → "造成 {0} 点伤害"
        /// 调用: SetKey("DAMAGE_MSG", 100) → "造成 100 点伤害"
        /// </summary>
        public void SetKey(string newKey, params object[] args)
        {
            key = newKey;
            _args = args is { Length: > 0 } ? args : null;
            Apply();
        }

        private void Apply()
        {
            if (_text == null || string.IsNullOrEmpty(key)) return;

            _text.text = _args != null
                ? GF.Localization.GetFormat(key, _args)
                : GF.Localization.Get(key);
        }

        private void OnLanguageChanged(LanguageChangedEvent e)
        {
            Apply();
        }
    }
}
