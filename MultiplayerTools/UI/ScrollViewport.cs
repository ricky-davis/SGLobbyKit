using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerTools
{
    internal sealed class ScrollViewport
    {
        public GameObject Root;
        public RectTransform ViewportRect;
        public RectTransform ContentRect;
        public ScrollRect ScrollRect;
        public Scrollbar Scrollbar;
    }
}
