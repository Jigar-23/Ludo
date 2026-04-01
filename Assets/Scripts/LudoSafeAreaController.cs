using System.Collections.Generic;
using UnityEngine;

namespace PremiumLudo
{
    public sealed class LudoSafeAreaController : MonoBehaviour
    {
        private readonly List<RectTransform> _targets = new List<RectTransform>(8);
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        public void Register(RectTransform target)
        {
            if (target == null || _targets.Contains(target))
            {
                return;
            }

            _targets.Add(target);
            ApplyToTarget(target);
        }

        private void LateUpdate()
        {
            Rect safeArea = Screen.safeArea;
            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
            if (safeArea == _lastSafeArea && screenSize == _lastScreenSize)
            {
                return;
            }

            _lastSafeArea = safeArea;
            _lastScreenSize = screenSize;

            for (int i = 0; i < _targets.Count; i++)
            {
                ApplyToTarget(_targets[i]);
            }
        }

        private void ApplyToTarget(RectTransform target)
        {
            if (target == null)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            Vector2 min = safeArea.position;
            Vector2 max = safeArea.position + safeArea.size;
            float width = Mathf.Max(1f, Screen.width);
            float height = Mathf.Max(1f, Screen.height);
            target.anchorMin = new Vector2(min.x / width, min.y / height);
            target.anchorMax = new Vector2(max.x / width, max.y / height);
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
            target.localScale = Vector3.one;
        }
    }
}
