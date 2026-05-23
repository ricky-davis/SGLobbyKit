using System.Collections.Generic;
using UnityObject = UnityEngine.Object;

namespace MultiplayerTools.Features.Lobby
{
    internal sealed class LobbyUiController
    {
        private readonly HashSet<int> _boundNativeControlIds = new HashSet<int>();

        public static LobbyUiController Instance { get; } = new LobbyUiController();

        private LobbyUiController()
        {
        }

        public bool TryMarkNativeControlBound(UnityObject control)
        {
            if (control == null)
                return false;

            return _boundNativeControlIds.Add(control.GetInstanceID());
        }

        public void ClearNativeControlBindings()
        {
            _boundNativeControlIds.Clear();
        }
    }
}

